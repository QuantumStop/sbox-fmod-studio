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
using System.Runtime.InteropServices;

namespace SteamAudio;

public static class FMODStudioAPI
{
	// FMOD STUDIO PLUGIN

	[DllImport( "phonon_fmod" )]
	public static extern void iplFMODInitialize( IntPtr context );

	[DllImport( "phonon_fmod" )]
	public static extern void iplFMODSetHRTF( IntPtr hrtf );

	[DllImport( "phonon_fmod" )]
	public static extern void iplFMODSetSimulationSettings( SimulationSettings simulationSettings );

	[DllImport( "phonon_fmod" )]
	public static extern void iplFMODSetReverbSource( IntPtr reverbSource );

	[DllImport( "phonon_fmod" )]
	public static extern void iplFMODTerminate();

	[DllImport( "phonon_fmod" )]
	public static extern int iplFMODAddSource( IntPtr source );

	[DllImport( "phonon_fmod" )]
	public static extern void iplFMODRemoveSource( int handle );

	[DllImport( "phonon_fmod" )]
	public static extern void iplFMODSetHRTFDisabled( bool disabled );
}
