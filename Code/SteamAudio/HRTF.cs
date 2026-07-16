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

public class HRTF
{
	IntPtr mHRTF = IntPtr.Zero;

	public HRTF( Context context, AudioSettings audioSettings, string sofaFileName, byte[] sofaFileData, float gaindB, HRTFNormType normType )
	{
		IntPtr sofaData = IntPtr.Zero;

		var hrtfSettings = new HRTFSettings { };
		if ( sofaFileData != null && sofaFileData.Length > 0 )
		{
			hrtfSettings.type = HRTFType.SOFA;

			sofaData = Marshal.AllocHGlobal( sofaFileData.Length );
			Marshal.Copy( sofaFileData, 0, sofaData, sofaFileData.Length );

			hrtfSettings.sofaFileData = sofaData;
			hrtfSettings.sofaFileDataSize = sofaFileData.Length;
		}
		else if ( sofaFileName != null )
		{
			hrtfSettings.type = HRTFType.SOFA;
			hrtfSettings.sofaFileName = sofaFileName;
		}
		else
		{
			hrtfSettings.type = HRTFType.Default;
		}

		hrtfSettings.volume = DBToGain( gaindB );
		hrtfSettings.normType = normType;

		var status = API.iplHRTFCreate( context.Get(), ref audioSettings, ref hrtfSettings, out mHRTF );
		if ( status != Error.Success )
		{
			Log.Error( string.Format( "Unable to load HRTF: {0}. [{1}]", sofaFileName ?? "default", status ) );
			mHRTF = IntPtr.Zero;
		}
		else
		{
			Log.Info( string.Format( "Loaded HRTF: {0}.", sofaFileName ?? "default" ) );
		}

		if ( sofaData != IntPtr.Zero )
		{
			Marshal.FreeHGlobal( sofaData );
		}
	}

	public HRTF( HRTF hrtf ) => mHRTF = API.iplHRTFRetain( hrtf.Get() );

	~HRTF()
	{
		Release();
	}

	public void Release() => API.iplHRTFRelease( ref mHRTF );

	public IntPtr Get() => mHRTF;

	private float DBToGain( float gaindB )
	{
		const float kMinDBLevel = -90.0f;

		if ( gaindB <= kMinDBLevel )
			return 0.0f;

		return MathF.Pow( 10.0f, gaindB * (1.0f / 20.0f) );
	}
}
