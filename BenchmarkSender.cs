/* BSD 3-Clause License

Copyright (c) 2019, because-why-not.com Limited
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using Byn.Awrtc;
using Byn.Awrtc.Base;
using Byn.Awrtc.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Byn.Awrtc.Extra.Benchmark
{
    /// <summary>
    /// Connects to the BenchmarkEcho server and then starts sending a fixed amount of data. 
    /// Last tested unity version: 2018.4.0f1 LTS
    /// 
    /// 
    /// * The echo server will confirm all messages (that get through) by responding with their
    ///   message number & timestamp. Messages that are out of order or lost are logged
    ///   (note it won't know the difference between out of order or lost messages at the time of
    ///   receiving)
    /// * Change CONFIRMATION_TIMEOUT to decide how long to wait until a messages is considered lost
    /// * Target speed can be set via the ui. The sender will try to spread the
    ///   messages evenly to not send too much at once. If the network can't keep up the buffers
    ///   will get too full and the sender will have to slow down sending.
    /// * Watch for the buffer influence! If the messages are sent to fast they
    ///   are getting buffered internally and the latency will drop because of it
    /// * Change SEND_RELIABLE to compare reliable / unreliable messages
    /// * Change MESSAGE_SIZE to change how much should be sent at once in a single message
    ///     (MTU might be around 1200 or 1192?)
    /// * Change MAX_BUFFER. Large buffer can speed up sending for fast connections but
    ///   reduces latency massively for slow connections
    ///   IMPORTANT: A large buffer can cause messages being stuck in the buffer so long 
    ///   that the value CONFIRMATION_TIMEOUT kicks in and declares the messages dead
    /// * Note that for very fast connections the CPU load / multi threading issues will likely
    ///   be the bottleneck. Send calls from C# to C++ are CPU intensive and require sync between
    ///   Unity thread and WebRTC thread (and later with another WebRTC network thread)
    /// 
    /// See member variables to understand the meaning of the statistics shown in the UI
    /// </summary>
    public class BenchmarkSender : MonoBehaviour
    {
        /// <summary>
        /// Change this to test performance with different packages sizes
        /// smaller messages will increase the overhead
        /// larger messages risk fragmentation (unclear how WebRTC handles
        /// this internally)
        /// needs to be at least big enough for a message number
        /// and timestamp!
        /// </summary>
        private readonly static int MESSAGE_SIZE = 1024;

        /// <summary>
        /// Update if the message content is changed and needs more bytes
        /// </summary>
        public static readonly int MESSAGE_SIZE_MIN = 8;

        /// <summary>
        /// If messages aren't confirmed by the echo server after
        /// set seconds they will be considered dropped by the 
        /// network layer.
        /// Watch out if setting the value too low or let the buffer
        /// increase too high! Messages might spend a few seconds
        /// in the buffer and be declared dead before they are even sent.
        /// </summary>
        private static int CONFIRMATION_TIMEOUT = 30;

        /// <summary>
        /// Pause sending if buffer gets above this
        /// </summary>
        private static int MAX_BUFFER = 256 * 1024;

        /// <summary>
        /// Sender will send messages reliably if true
        /// unreliable if false. 
        /// </summary>
        public static bool SEND_RELIABLE = true;

        /// <summary>
        /// Current speed we try to reach. Set by ui
        /// </summary>
        private int mTargetByteSpeed = 1024 * 1024 / 8;

        /// <summary>
        /// Sets the speed the sender should try to reach
        /// </summary>
        /// <param name="speedInBytesPerSecond"></param>
        public void SetTargetByteSpeed(int speedInBytesPerSecond)
        {
            mTargetByteSpeed = speedInBytesPerSecond;
        }

        /// <summary>
        /// Packets per second we need to send to reach the 
        /// mTargetByteSpeed
        /// </summary>
        private float PacketsPerSecond{
            get{
                return mTargetByteSpeed / (float)MESSAGE_SIZE;
            }
        }


        /// <summary>
        /// Buffer used to store the messages that is sent. Only the first
        /// few bytes are used. The rest is just to increase traffic
        /// </summary>
        private byte[] buffer;


        private DateTime mStartTime = DateTime.Now;
        private int TimeMs
        {
            get
            {
                return (int)(DateTime.Now - mStartTime).TotalMilliseconds;
            }
        }

        /// <summary>
        /// True if connected and sending
        /// </summary>
        public bool mIsActive;

        /// <summary>
        /// Last message number sent == count of all messages sent
        /// </summary>
        public int mNumberSent;

        /// <summary>
        /// Last message received back from echo server
        /// </summary>
        public int mNumberReceived;

        /// <summary>
        /// Next messages that is expected to be received
        /// </summary>
        private int mExpectedToReceive;

        /// <summary>
        /// Buffer currently too full to send new messages?
        /// </summary>
        private bool mBufferFull;

        /// <summary>
        /// Number of times the buffer became so full sending had to be paused
        /// </summary>
        public int mBufferFullCount = 0;

        /// <summary>
        /// Amount currently buffered by WebRTC. Note that WebRTC has
        /// another internal buffer that can't be checked. (might be 256 KB
        /// last checked for WebRTC 72)
        /// </summary>
        public int mBuffered = 0;

        /// <summary>
        /// Averages bytes sent since last calculation
        /// </summary>
        public int mAverageSent = 0;

        /// <summary>
        /// This is the data the echo server confirmed.
        /// (not the data received from the echo server)
        /// </summary>
        public int mAverageConfirmed = 0;

        /// <summary>
        /// Averages data received from the echo server. 
        /// Can by used to check if the echo server might
        /// be the bottleneck. At the moment it only needs
        /// to send 8 bytes to confirm each 1kb message.
        /// </summary>
        public int mAverageReceived = 0;

        /// <summary>
        /// Latency of the last messages. Note that this is influenced by the
        /// buffer and the framerate as the other side will only reply once a frame.
        /// </summary>
        public int mLatencyMs = 0;

        /// <summary>
        /// Increased by 1 for each dropped message
        /// Increased by 3 for two messages out of order.
        /// e.g. following message numbers received:
        /// 1 2 4 5 6
        /// -> message 3 missing counter is increased once
        /// 
        /// 1 2 4 3 5 6
        ///     Message 4 received instead of 3 -> increased by 1
        ///       Message 3 received instead of 5 -> increased by 1
        ///         Message 5 received instead of 4 -> increased by 1 
        /// (expected message is always last messages + 1)
        /// 
        /// Messages that are dropped are detected later again
        /// due to timeout and recorded separately again
        /// </summary>
        public int mUnexpectedMessages = 0;

        /// <summary>
        /// 
        /// </summary>
        public int mDroppedMessages = 0;

        /// <summary>
        /// Bytes received overall
        /// </summary>
        private int mBytesReceived;

        /// <summary>
        /// Used to count seconds since last average update
        /// </summary>
        private float mAverageTimer;
        
        /// <summary>
        /// Counter of bytes sent since last average calulation
        /// </summary>
        private int mSumBytesSent = 0;
        /// <summary>
        /// Counter of bytes received since last average calulation
        /// </summary>
        private int mSumBytesReceived = 0;
        /// <summary>
        /// Counter of bytes confirmed since last average calulation
        /// </summary>
        private int mSumBytesConfirmed = 0;


        /// <summary>
        /// How often the average is updated (in seconds)
        /// </summary>
        private readonly int AVERAGE_UPDATE_TIME = 4;


        //counts up the time over multiple frames until
        //it reaches a value high enough to send another package
        // (1.0f / mPackagesPerSecond)
        private float mTargetPackagesCounter;




        private ICall sender;
        private ConnectionId mToEcho;

        /// <summary>
        /// Info in our message
        /// </summary>
        private struct Msg
        {
            //unique message number
            public int number;
            //time in ms since start for delay / timeout
            public int time;

            public override int GetHashCode()
            {
                return number.GetHashCode();
            }
            public override bool Equals(object obj)
            {
                if (obj is Msg)
                {
                    Msg m2 = (Msg)obj;
                    return number.Equals(m2.number);
                }
                return false;
            }
        }

        //buffer to keep the message until the echo server confirmed it
        private HashSet<Msg> mAwaitingMessage;



        void Start()
        {

            Cleanup();
            //should create reproducable fake random data
            System.Random mr = new System.Random(0);
            buffer = new byte[MESSAGE_SIZE];
            mr.NextBytes(buffer);

            UnityCallFactory.Instance.RequestLogLevel(UnityCallFactory.LogLevel.Info);
            BenchmarkConfig.GlobalSetup();
        }

        void Cleanup()
        {
            if(sender != null)
            {
                sender.Dispose();
                sender = null;
            }
            mToEcho = ConnectionId.INVALID;

            mTargetPackagesCounter = 0;
            mIsActive = false;
            mStartTime = DateTime.Now;
            mBytesReceived = 0;
            mUnexpectedMessages = 0;
            mDroppedMessages = 0;
            mNumberSent = 0;
            mNumberReceived = 0;
            mExpectedToReceive = 0;
            mBufferFull = false;
            mAverageTimer = 0;
            mSumBytesSent = 0;
            mSumBytesConfirmed = 0;
            mSumBytesReceived = 0;
            mBufferFullCount = 0;
            mAwaitingMessage = new HashSet<Msg>();
        }

        private void OnMessageReceived(byte[] data)
        {
            if (data.Length < BenchmarkSender.MESSAGE_SIZE_MIN)
            {
                Debug.LogError("Message ignored! Received message too small. Incompatible sender version?");
                return;
            }


            int messageNumber = BitConverter.ToInt32(data, 0);
            if (BenchmarkConfig.VERBOSE)
                Debug.Log("sender: confirmation for message received: " + messageNumber);

            int messageTimeMs = BitConverter.ToInt32(data, 4);
            mLatencyMs = (int)(TimeMs - messageTimeMs);

            if (mExpectedToReceive != messageNumber)
            {
                mUnexpectedMessages++;
                if (SEND_RELIABLE)
                {
                    //fatal error for reliable sender. should never happen
                    Debug.LogError("Unexpected number received: " + messageNumber + " instead of " + mNumberReceived);
                    Debug.LogError("Message lost or out of order in reliable mode!!!");
                    mIsActive = false;
                }
                else{
                    //likely message out of order or dropped
                    Debug.LogWarning("sender: Message out of order or lost. Expected: " + mNumberReceived + " but received" + messageNumber);
                }     
            }


            Msg m = new Msg();
            m.number = messageNumber;
            m.time = messageTimeMs;
            bool removed = mAwaitingMessage.Remove(m);
            if(removed == false)
            {
                Debug.LogWarning("sender: Received unexpected message: " + m.number + " delay:" + (TimeMs - m.time));
            }

            mBytesReceived += data.Length;
            mSumBytesConfirmed += MESSAGE_SIZE;
            mSumBytesReceived += data.Length;
            mNumberReceived++;
            mExpectedToReceive = messageNumber + 1;
        }


        /// <summary>
        /// Called by Unitys update loop. Will refresh the state of the call, sync the events with
        /// unity and trigger the event callbacks below.
        ///
        /// </summary>
        private void Update()
        {
            UpdateStatistics();
            UpdateSender();

            int curTime = TimeMs;
            mAwaitingMessage.RemoveWhere((Msg m) =>
            {
                int td = curTime - m.time;
                if (td > (CONFIRMATION_TIMEOUT * 1000))
                {
                    Debug.Log("sender: timeout message " + m.number + " reply took longer than " + CONFIRMATION_TIMEOUT + " sec");
                    mDroppedMessages++;
                    return true;
                }
                return false;
            });

        }

        private bool SendOneMessage()
        {
            //sanity check
            if (buffer.Length < MESSAGE_SIZE_MIN)
            {
                Debug.LogError("sender: Message size set too small!");
                return false;
            }


            int bytesWritten = 0;
            byte[] nb = BitConverter.GetBytes(mNumberSent);
            for (int i = 0; i < 4; i++)
                buffer[i] = nb[i];
            bytesWritten += 4;

            int time = TimeMs;
            byte[] timeData = BitConverter.GetBytes(time);
            for (int i = 0; i < timeData.Length; i++)
                buffer[bytesWritten + i] = timeData[i];
            bytesWritten += bytesWritten;

            //sanity check
            if (bytesWritten > MESSAGE_SIZE_MIN)
            {
                Debug.LogError("sender: Created message too large");
                return false;
            }
            Msg m = new Msg();
            m.number = mNumberSent;
            m.time = time;
            mAwaitingMessage.Add(m);
            if(BenchmarkConfig.VERBOSE)
                Debug.Log("sender: sent message " + m.number);
            bool successful = sender.Send(buffer, SEND_RELIABLE, mToEcho);
            return successful;
        }
        private void UpdateSender()
        {
            if (sender != null)
            {
                sender.Update();

                //actively sending?
                if (mIsActive)
                {
                    bool wasFull = mBufferFull;
                    //reset buffer full flag. set to true again if send fails
                    mBufferFull = false;
                    //timer used to balance out the messages to avoid sending in bulk
                    mTargetPackagesCounter += Time.unscaledDeltaTime;
                    float secPerPacket = 1.0f / PacketsPerSecond;
                    while (mTargetPackagesCounter > secPerPacket)
                    {
                        mBuffered = sender.GetBufferedAmount(mToEcho, SEND_RELIABLE);
                        bool successful = false;
                        if ((mBuffered + buffer.Length) < MAX_BUFFER)
                        {
                            //false here means WebRTC refused to send either internal
                            //buffer is full (MAX_BUFFER bigger than supported allowed buffer)
                            //or it might have just disconnected and failed to send
                            successful = SendOneMessage();
                            if(successful == false)
                            {
                                Debug.LogWarning("Send returned false. Either internal WebRTC buffer is full or disconnect / internal error");
                            }
                        }

                        if (successful)
                        {
                            mNumberSent++;
                            mSumBytesSent += buffer.Length;
                            mTargetPackagesCounter -= secPerPacket;
                        }
                        else
                        {
                            mBufferFull = true;
                            mBufferFullCount++;
                            if(BenchmarkConfig.VERBOSE && wasFull == false)
                                Debug.LogWarning("sender: Buffer full. Pause sending");
                            mTargetPackagesCounter = 0;
                            break;
                        }
                    }


                }
            }
        }

        private void UpdateStatistics()
        {
            mAverageTimer += Time.unscaledDeltaTime;
            if (mAverageTimer > AVERAGE_UPDATE_TIME)
            {
                //rounding down a few bytes here. Should be fine as long
                //we don't use tiny amounts / short time spans
                mAverageSent = (int)(mSumBytesSent / mAverageTimer);
                mAverageConfirmed = (int)(mSumBytesConfirmed / mAverageTimer);
                mAverageReceived = (int)(mSumBytesReceived / mAverageTimer);
                mSumBytesSent = 0;
                mSumBytesConfirmed = 0;
                mSumBytesReceived = 0;
                mAverageTimer = 0;
            }

        }

        /// <summary>
        /// Triggered by UI
        /// </summary>
        public void Restart()
        {
            Cleanup();
            Debug.Log("Restarting sender");
            StartCoroutine(CoroutineStartTest());
        }

        private IEnumerator CoroutineStartTest()
        {
            //Give the other side some time to restart as well
            //+makes sure we don't kill the server by constant reconnects
            yield return new WaitForSeconds(1.5f);
            StartTest();
        }



        private void StartTest()
        {
            if (UnityCallFactory.Instance == null)
            {
                //if it is null something went terribly wrong
                Debug.LogError("UnityCallFactory missing. Platform not supported / dll's missing?");
                return;
            }
            SenderSetup();
        }








        /// <summary>
        /// Setting up the sender. This is called once the receiver is registered 
        /// at the signaling server and is ready to receive an incoming connection.
        /// </summary>
        private void SenderSetup()
        {
            if (sender != null)
            {
                sender.Dispose();
                sender = null;
            }
            Debug.Log("sender:  setup");

            sender = UnityCallFactory.Instance.Create(BenchmarkConfig.NetConfig);
            MediaConfig mediaConf2 = new MediaConfig();
            mediaConf2.Video = false;
            mediaConf2.Audio = false;
            sender.CallEvent += Sender_CallEvent;
            sender.Configure(mediaConf2);
        }
        private void Sender_CallEvent(object src, CallEventArgs args)
        {
            if (args.Type == CallEventType.ConfigurationComplete)
            {
                Debug.Log("sender: configuration done. Listening on address " + BenchmarkConfig.Address);
                sender.Call(BenchmarkConfig.Address);
            }
            else if (args.Type == CallEventType.ConfigurationFailed)
            {
                Debug.LogError("sender: failed to access the audio device");
            }
            else if (args.Type == CallEventType.ConnectionFailed)
            {
                mIsActive = false;
                Debug.LogError("sender: failed to connect");
            }
            else if (args.Type == CallEventType.CallAccepted)
            {
                Debug.Log("sender CallAccepted");
                CallAcceptedEventArgs ev = args as CallAcceptedEventArgs;
                mToEcho = ev.ConnectionId;
                mIsActive = true;
                mStartTime = DateTime.Now;
            }
            else if (args.Type == CallEventType.CallEnded)
            {
                Debug.Log("sender: received CallEnded event");
                mIsActive = false;

            }
            else if (args.Type == CallEventType.DataMessage)
            {
                var margs = args as DataMessageEventArgs;
                OnMessageReceived(margs.Content);
            }
        }

        private void OnDestroy()
        {
            if (sender != null)
            {
                sender.Dispose();
                sender = null;
            }
        }
    }
}