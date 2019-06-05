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
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Byn.Awrtc.Extra.Benchmark
{
    /// <summary>
    /// See BenchmarkSender.cs for docs
    /// </summary>
    public class BenchmarkSenderUi : MonoBehaviour
    {

        public Button _StartStopButton;
        public Text _ActiveText;
        public Text _Sent;
        public Text _Received;
        public Text _SpeedSent;
        public Text _SpeedConfirmed;
        public Text _SpeedReceived;
        public Text _BufferFull;
        public Text _Buffered;
        public Text _Unexpected;
        public Text _Dropped;
        public Text _LatencyMs;
        public InputField _TargetSpeed;

        private BenchmarkSender _Benchmark;

        void Start()
        {
            _Benchmark = FindObjectOfType<BenchmarkSender>();
            _StartStopButton.onClick.AddListener(HandleUnityAction);

        }

        void HandleUnityAction()
        {
            _Benchmark.Restart();
        }


        public static string BytePerSecToText(int bytes)
        {
            if(bytes < 1024)
            {
                return bytes + " Byte/s";
            }
            else
            {
                return (int)Math.Round(bytes / 1024.0) + " KByte/s";
            }
        }
        public static string BytesToText(int bytes)
        {
            if (bytes < 1024)
            {
                return bytes + " Bytes";
            }
            else
            {
                return (int)Math.Round(bytes / 1024.0) + " KBytes";
            }
        }

        void Update()
        {
            _ActiveText.text = "" + _Benchmark.mIsActive;
            _Sent.text = "" + _Benchmark.mNumberSent;
            _Received.text = "" + _Benchmark.mNumberReceived;
            _SpeedSent.text = BytePerSecToText(_Benchmark.mAverageSent);
            _SpeedConfirmed.text = BytePerSecToText(_Benchmark.mAverageConfirmed);
            _SpeedReceived.text = BytePerSecToText(_Benchmark.mAverageReceived);

            _BufferFull.text = "" + _Benchmark.mBufferFullCount;
            _Buffered.text = BytesToText(_Benchmark.mBuffered);
            _Unexpected.text = "" + _Benchmark.mUnexpectedMessages;
            _Dropped.text = "" + _Benchmark.mDroppedMessages;
            _LatencyMs.text = "" + _Benchmark.mLatencyMs;

            int val = 1;
            int.TryParse(_TargetSpeed.text, out val);
            _Benchmark.SetTargetByteSpeed(val * 1024);
        }
    }
}