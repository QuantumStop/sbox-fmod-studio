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

namespace SteamAudio;

public struct SteamAudioSettings
{
	public SteamAudioSettings() { }
	public const bool HRTFDisabled = false;
	public const bool PerspectiveCorrection = false;
	public const float PerspectiveCorrectionFactor = 1.0f;
	public const float HRTFVolumeGainDB = 0.0f;
	public const HRTFNormType hrtfNormalizationType = HRTFNormType.None;
	public SOFAFile[] SOFAFiles = null;
	public SteamAudioMaterial defaultMaterial = null;

	public const SceneType SceneType = SceneType.Default;
	public const int MaxOcclusionSamples = 16;

	public const int realTimeRays = 4096;
	public const int realTimeBounces = 4;
	public const float realTimeDuration = 1.0f;
	public const int realTimeAmbisonicOrder = 1;
	public const int realTimeMaxSources = 32;
	public const int realTimeCPUCoresPercentage = 5;
	public const float realTimeIrradianceMinDistance = 1.0f;

	public const bool bakeConvolution = true;
	public const bool bakeParametric = false;
	public const int bakingRays = 16384;
	public const int bakingBounces = 16;
	public const float bakingDuration = 1.0f;
	public const int bakingAmbisonicOrder = 1;
	public const int bakingCPUCoresPercentage = 50;
	public const float bakingIrradianceMinDistance = 1.0f;
	public const int bakingVisibilitySamples = 4;
	public const float bakingVisibilityRadius = 1.0f;
	public const float bakingVisibilityThreshold = 0.1f;
	public const float bakingVisibilityRange = 1000.0f;
	public const float bakingPathRange = 1000.0f;
	public const int bakedPathingCPUCoresPercentage = 50;
	public const float simulationUpdateInterval = 0.1f;
	public const ReflectionEffectType ReflectionEffectType = ReflectionEffectType.Convolution;
	public const float hybridReverbTransitionTime = 1.0f;
	public const int hybridReverbOverlapPercent = 25;
	public const OpenCLDeviceType deviceType = OpenCLDeviceType.GPU;
	public const int maxReservedComputeUnits = 8;
	public const float fractionComputeUnitsForIRUpdate = 0.5f;
	public const int bakingBatchSize = 8;
	public const float TANDuration = 1.0f;
	public const int TANAmbisonicOrder = 1;
	public const int TANMaxSources = 32;
	public const bool EnableValidation = false;
}

/// <summary>
/// Put this into a GameResource Extention of the surface and eat that
/// </summary>
public class SteamAudioMaterial
{
	[Header( "Absorption" ), Range( 0.0f, 1.0f )]
	public float LowFreqAbsorption { get; set; } = 0.1f;
	[Range( 0.0f, 1.0f )]
	public float MidFreqAbsorption { get; set; } = 0.1f;
	[Range( 0.0f, 1.0f )]
	public float HighFreqAbsorption { get; set; } = 0.1f;
	[Header( "Scattering" ), Range( 0.0f, 1.0f )]
	public float Scattering { get; set; } = 0.5f;
	[Header( "Transmission" ), Range( 0.0f, 1.0f )]
	public float LowFreqTransmission { get; set; } = 0.1f;
	[Range( 0.0f, 1.0f )]
	public float MidFreqTransmission { get; set; } = 0.1f;
	[Range( 0.0f, 1.0f )]
	public float HighFreqTransmission { get; set; } = 0.1f;
}
