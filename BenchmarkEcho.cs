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
    /// Replies to each message with a confirmation to
    /// ensure order is correct and no messages get dropped.
    /// Designed for use with BenchmarkSender only!
    /// 
    /// * only one sender can connect
    /// * Always uses reliable messages
    /// * Only echo's message id and timeout only. The random data behind is ignored
    ///   to low the risk of a bottleneck
    /// * WATCH OUT: If the echo servers upload is much slower than the sender
    ///   it starts filling up the buffer and even drops messages.
    ///   It works only if the Echo server has enough speed to confirm all messages
    ///   it receives! This situation get worse the smaller the sender's messages become!
    /// </summary>
    public class BenchmarkEcho : MonoBehaviour
    {
        public static readonly bool AUTOSTART = true;

        ICall echo;
        ConnectionId mToSender;

        /// <summary>
        /// True if active either waiting for a incoming call
        /// or already connected
        /// </summary>
        internal bool mActive;

        /// <summary>
        /// A sender is connected
        /// </summary>
        internal bool mConnected;

        internal uint mStatReceived;
        /// <summary>
        /// Dropped messages (happens if we get messages so quickly our
        /// upload is too slow to keep up)
        /// </summary>
        internal int mStatDropped;
        /// <summary>
        /// Average incoming speed
        /// </summary>
        internal int mStatAvgReceived;

        /// <summary>
        /// Timer to update the average values
        /// </summary>
        private float mAverageTimer;

        /// <summary>
        /// Counter to calculate the average bytes received
        /// </summary>
        private int mSumBytesRec;

        public int mBuffered = 0;

        void Start()
        {
            Cleanup();
            UnityCallFactory.Instance.RequestLogLevel(UnityCallFactory.LogLevel.Info);
            BenchmarkConfig.GlobalSetup();
            if(AUTOSTART)
                StartEchoServer();
        }

        private void StartEchoServer()
        {
            if (UnityCallFactory.Instance == null)
            {
                //if it is null something went terribly wrong
                Debug.LogError("UnityCallFactory missing. Platform not supported / dll's missing?");
                return;
            }
            SetupEcho();
        }

        public void Restart()
        {
            StartCoroutine(CoroutineRestart());
        }
        IEnumerator CoroutineRestart()
        {

            yield return new WaitForSeconds(1);
            Cleanup();
            StartEchoServer();
        }


        private void Cleanup()
        {
            if(echo != null)
            {
                echo.Dispose();
                echo = null;
            }
            mToSender = ConnectionId.INVALID;
            mActive = false;
            mConnected = false;

            mStatReceived = 0;
            mStatDropped = 0;

            mAverageTimer = 0;
            mSumBytesRec = 0;
            mStatAvgReceived = 0;
        }

        private void OnMessageReceived(byte[] data)
        {
            if (data.Length < BenchmarkSender.MESSAGE_SIZE_MIN)
            {
                Debug.LogError("Message ignored! Received message too small. Incompatible sender version?");
                return;
            }
            mSumBytesRec += data.Length;


            uint messageNumber = BitConverter.ToUInt32(data, 0);
            uint messageTimeMs = BitConverter.ToUInt32(data, 4);


            mStatReceived = messageNumber;
            if (BenchmarkConfig.VERBOSE)
                Debug.Log("echo: received message number: " + messageNumber + " bytes: " + data.Length);
            SendEcho(messageNumber, messageTimeMs);
        }

        private void SendEcho(uint number, uint timeMs)
        {
            byte[] data = new byte[BenchmarkSender.MESSAGE_SIZE_MIN];

            byte[] data1 = BitConverter.GetBytes(number);
            byte[] data2 = BitConverter.GetBytes(timeMs);
            for (int i = 0; i < 4; i++)
                data[i] = data1[i];
            for (int i = 0; i < 4; i++)
                data[4 + i] = data2[i];
            

            bool sent = echo.Send(data, true, mToSender);
            //might be a problem if the sender is much faster than the echo server.
            //even though the sender needs to send hundreds of times more data
            if (sent == false)
            {
                mStatDropped++;
                Debug.LogWarning("echo message dropped. Buffer full?");
            }
        }

        private void Update()
        {
            mAverageTimer += Time.unscaledDeltaTime;
            if (mAverageTimer > 4)
            {
                //watch out we round down a few bytes here
                mStatAvgReceived =(int)(mSumBytesRec / mAverageTimer);

                mSumBytesRec = 0;
                mAverageTimer = 0;
            }


            if (echo != null)
            {
                echo.Update();

                if (mToSender != ConnectionId.INVALID && mActive)
                {
                    mBuffered = echo.GetBufferedAmount(mToSender, true);
                }
            }
        }
        private void SetupEcho()
        {
            if(echo != null)
            {
                echo.Dispose();
                echo = null;
            }
            Debug.Log("echo setup");

            MediaConfig mediaConf1 = new MediaConfig();
            mediaConf1.Video = false;
            mediaConf1.Audio = false;

            //this creates the receiver 
            echo = UnityCallFactory.Instance.Create(BenchmarkConfig.NetConfig);
            echo.CallEvent += Echo_CallEvent;
            echo.Configure(mediaConf1);
        }


        private void Echo_CallEvent(object src, CallEventArgs args)
        {

            if (args.Type == CallEventType.ConfigurationComplete)
            {
                string address = BenchmarkConfig.Address;
                Debug.Log("echo configuration done. Listening on address " + address);
                echo.Listen(address);
            }
            else if (args.Type == CallEventType.WaitForIncomingCall)
            {
                Debug.Log("echo is ready to accept incoming calls");
                mActive = true;
            }
            else if (args.Type == CallEventType.ListeningFailed)
            {
                mActive = false;
                Debug.LogError("echo failed to listen to the address");
                Restart();
            }
            else if (args.Type == CallEventType.CallAccepted)
            {
                Debug.Log("echo CallAccepted");
                CallAcceptedEventArgs ev = args as CallAcceptedEventArgs;
                mToSender = ev.ConnectionId;
                mConnected = true;
            }
            else if(args.Type == CallEventType.DataMessage)
            {
                var margs = args as DataMessageEventArgs;
                byte[] data = margs.Content;
                OnMessageReceived(data);
            }else if(args.Type == CallEventType.CallEnded)
            {
                mActive = false;
                mConnected = false;
                Restart();
            }
        }
        private void OnDestroy()
        {
            Cleanup();
        }
    }
}