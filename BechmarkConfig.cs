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
using Byn.Awrtc;
using Byn.Awrtc.Base;
using Byn.Unity.Examples;
using UnityEngine;

namespace Byn.Awrtc.Extra.Benchmark
{
    /// <summary>
    /// Shared config values for echo / sender
    /// </summary>
    public class BenchmarkConfig
    {
        /// <summary>
        /// Configuration used for both echo and sender
        /// </summary>
        public static NetworkConfig NetConfig
        {
            get{
                var netConf = new NetworkConfig();
                //e.g.: "ws://signaling.because-why-not.com/test"
                netConf.SignalingUrl = ExampleGlobals.Signaling;
                netConf.IceServers.Add(new IceServer(ExampleGlobals.TurnUrl, ExampleGlobals.TurnUser, ExampleGlobals.TurnPass));
                return netConf;
            }
        }

        /// <summary>
        /// Address used
        /// </summary>
        public static string Address{
            get{
                return Application.productName + "_Benchmark";
            }
        }

        /// <summary>
        /// True will log every single message
        /// </summary>
        public static bool VERBOSE = false;





        public static void GlobalSetup()
        {
            //Can be used to block direct connections to
            //benchmark using the turn server.
            //please don't kill the shared test turn server with this ;)
#if !UNITY_WEBGL || UNITY_EDITOR
            //AWebRtcPeer.sDebugIgnoreTypHost = true;
            //AWebRtcPeer.sDebugIgnoreTypSrflx = true;
#endif
        }


    }
}
