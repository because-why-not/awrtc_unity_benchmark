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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Byn.Awrtc.Extra.Benchmark
{
    /// <summary>
    /// See BenchmarkEcho.cs and BenchmarkSender.cs for full documentation.
    /// </summary>
    public class BenchmarkEchoUi : MonoBehaviour
    {
        public Button _StartStopButton;
        public Text _ActiveText;
        public Text _ConnectedText;
        public Text _Received;
        public Text _Dropped;
        public Text _SpeedReceived;
        public Text _Buffered;

        private BenchmarkEcho _Benchmark;

        void Start()
        {
            _Benchmark = FindObjectOfType<BenchmarkEcho>();

            _StartStopButton.onClick.AddListener(HandleUnityAction);
        }

        void HandleUnityAction()
        {
            _Benchmark.Restart();
        }


        void Update()
        {
            _ActiveText.text = "" + _Benchmark.mActive;
            _ConnectedText.text = "" + _Benchmark.mConnected;
            _Received.text = "" + _Benchmark.mStatReceived;
            _Dropped.text = "" + _Benchmark.mStatDropped;
            _SpeedReceived.text = BenchmarkSenderUi.BytePerSecToText(_Benchmark.mStatAvgReceived);
            _Buffered.text = BenchmarkSenderUi.BytesToText(_Benchmark.mBuffered);
        }
    }
}