//
// Copyright 2017-2023 Valve Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Reflection;
using FMODSbox;


namespace SteamAudio;

public sealed class FMODStudioAudioEngineState : AudioEngineState
{
	public override void Initialize( IntPtr context, IntPtr defaultHRTF, SimulationSettings simulationSettings, PerspectiveCorrection correction )
	{
		FMODStudioAPI.iplFMODInitialize( context );
		FMODStudioAPI.iplFMODSetHRTF( defaultHRTF );
		FMODStudioAPI.iplFMODSetSimulationSettings( simulationSettings );
	}

	public override void Destroy() => FMODStudioAPI.iplFMODTerminate();

	public override void SetHRTF( IntPtr hrtf ) => FMODStudioAPI.iplFMODSetHRTF( hrtf );

	public override void SetReverbSource( Source reverbSource ) => FMODStudioAPI.iplFMODSetReverbSource( reverbSource.Get() );

	public override void SetHRTFDisabled( bool disabled )
	{
		base.SetHRTFDisabled( disabled );

		FMODStudioAPI.iplFMODSetHRTFDisabled( disabled );
	}
}

public sealed class FMODStudioAudioEngineStateHelpers : AudioEngineStateHelpers
{
	public override Transform GetListenerTransform() => FMODManagerSystem.Current.Listeners.First().WorldTransform;

	public override AudioSettings GetAudioSettings()
	{
		var audioSettings = new AudioSettings { };

		FMOD.SPEAKERMODE speakerMode;
		FMODManagerSystem.CoreSystem.getSoftwareFormat( out int samplingRate, out speakerMode, out int numRawSpeakers );

		FMODManagerSystem.CoreSystem.getDSPBufferSize( out uint frameSize, out int numBuffers );

		audioSettings.samplingRate = samplingRate;
		audioSettings.frameSize = (int)frameSize;

		return audioSettings;
	}
}
