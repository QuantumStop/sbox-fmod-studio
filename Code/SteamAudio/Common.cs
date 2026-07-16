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
using System.IO;
using System.Numerics;
using System.Text;

namespace SteamAudio;

// why are these even needed
public static class Common
{
	/// <summary>
	/// Source 2 Right handed Z up to Steam Audio's whatever the fuck
	/// </summary>
	/// <param name="point">S2 Vector3</param>
	/// <returns>Converted Vector3</returns>
	public static Vector3 ConvertVector( Vector3 point ) => new()
	{
		x = -point.y,
		y = point.z,
		z = -point.x
	};

	public static Matrix4x4 ConvertTransform( Transform transform )
	{
		Matrix flipZ = new(
		 0, 0, -1, 0,
		-1, 0, 0, 0,
		 0, 1, 0, 0,
		 0, 0, 0, 1 );

		Matrix mat =
			Matrix.CreateScale( transform.Scale ) *
			Matrix.CreateRotation( transform.Rotation ) *
			Matrix.CreateTranslation( transform.Position );

		return flipZ * mat * flipZ;
	}

	public static byte[] ConvertString( string s ) => Encoding.UTF8.GetBytes( s + char.MinValue );

	public static string HumanReadableDataSize( int dataSize )
	{
		if ( dataSize < 1e3 )
		{
			return dataSize.ToString() + " bytes";
		}
		else if ( dataSize < 1e6 )
		{
			return (dataSize / 1e3f).ToString( "0.0" ) + " kB";
		}
		else if ( dataSize < 1e9 )
		{
			return (dataSize / 1e6f).ToString( "0.0" ) + " MB";
		}
		else
		{
			return (dataSize / 1e9f).ToString( "0.0" ) + " GB";
		}
	}
}
