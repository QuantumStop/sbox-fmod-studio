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

public enum DistanceAttenuationInput
{
	CurveDriven,
	PhysicsBased
}

public enum AirAbsorptionInput
{
	SimulationDefined,
	UserDefined
}

public enum DirectivityInput
{
	SimulationDefined,
	UserDefined
}

public enum OcclusionInput
{
	SimulationDefined,
	UserDefined
}

public enum TransmissionInput
{
	SimulationDefined,
	UserDefined
}

public enum ReflectionsType
{
	Realtime,
	BakedStaticSource,
	BakedStaticListener
}

public struct AudioSourceAttenuationData
{
	public AudioRolloffMode rolloffMode;
	public float minDistance;
	public float maxDistance;
	public Curve curve;
}

public class SteamAudioSource
{
	public bool directBinaural = true;
	public HRTFInterpolation interpolation = HRTFInterpolation.Nearest;
	public bool perspectiveCorrection = false;

	public bool distanceAttenuation = false;
	public DistanceAttenuationInput distanceAttenuationInput = DistanceAttenuationInput.CurveDriven;
	public float distanceAttenuationValue = 1.0f;
	public bool airAbsorption = false;
	public AirAbsorptionInput airAbsorptionInput = AirAbsorptionInput.SimulationDefined;
	[Range( 0.0f, 1.0f )]
	public float airAbsorptionLow = 1.0f;
	[Range( 0.0f, 1.0f )]
	public float airAbsorptionMid = 1.0f;
	[Range( 0.0f, 1.0f )]
	public float airAbsorptionHigh = 1.0f;
	public bool directivity = false;
	public DirectivityInput directivityInput = DirectivityInput.SimulationDefined;
	[Range( 0.0f, 1.0f )]
	public float dipoleWeight = 0.0f;
	[Range( 0.0f, 4.0f )]
	public float dipolePower = 0.0f;
	[Range( 0.0f, 1.0f )]
	public float directivityValue = 1.0f;

	public bool occlusion = false;
	public OcclusionInput occlusionInput = OcclusionInput.SimulationDefined;
	public OcclusionType occlusionType = OcclusionType.Raycast;
	[Range( 0.0f, 4.0f )]
	public float occlusionRadius = 1.0f;
	[Range( 1, 128 )]
	public int occlusionSamples = 16;
	[Range( 0.0f, 1.0f )]
	public float occlusionValue = 1.0f;
	public bool transmission = false;
	public TransmissionType transmissionType = TransmissionType.FrequencyIndependent;
	public TransmissionInput transmissionInput = TransmissionInput.SimulationDefined;
	[Range( 0.0f, 1.0f )]
	public float transmissionLow = 1.0f;
	[Range( 0.0f, 1.0f )]
	public float transmissionMid = 1.0f;
	[Range( 0.0f, 1.0f )]
	public float transmissionHigh = 1.0f;
	[Range( 1, 8 )]
	public int maxTransmissionSurfaces = 1;

	[Range( 0.0f, 1.0f )]
	public float directMixLevel = 1.0f;

	public bool reflections = false;
	public ReflectionsType reflectionsType = ReflectionsType.Realtime;
	public bool useDistanceCurveForReflections = false;
	//	public SteamAudioBakedSource currentBakedSource = null;
	public IntPtr reflectionsIR = IntPtr.Zero;
	public float reverbTimeLow = 0.0f;
	public float reverbTimeMid = 0.0f;
	public float reverbTimeHigh = 0.0f;
	public float hybridReverbEQLow = 1.0f;
	public float hybridReverbEQMid = 1.0f;
	public float hybridReverbEQHigh = 1.0f;
	public int hybridReverbDelay = 0;
	public bool applyHRTFToReflections = false;
	[Range( 0.0f, 10.0f )]
	public float reflectionsMixLevel = 1.0f;

	public bool pathing = false;
	//	public SteamAudioProbeBatch pathingProbeBatch = null;
	public bool pathValidation = true;
	public bool findAlternatePaths = true;
	public float[] pathingEQ = [1.0f, 1.0f, 1.0f];
	public float[] pathingSH = new float[16];
	public bool applyHRTFToPathing = false;
	[Range( 0.0f, 10.0f )]
	public float pathingMixLevel = 1.0f;
	public bool normalizePathingEQ = false;

	Simulator mSimulator = null;
	Source mSource = null;
	AudioEngineSource mAudioEngineSource = null;

	AudioSource mAudioSource = null;
	AudioSourceAttenuationData mAttenuationData = new() { };
	DistanceAttenuationModel mCurveAttenuationModel = new() { };
	GCHandle mThis;
	SteamAudioSettings mSettings = new();

	protected override void OnAwake()
	{
		mSimulator = SteamAudioManager.Simulator;

		var settings = SteamAudioManager.GetSimulationSettings( false );
		mSource = new Source( SteamAudioManager.Simulator, settings );

		mAudioEngineSource = AudioEngineSource.Create();
		if ( mAudioEngineSource != null )
		{
			mAudioEngineSource.Initialize( gameObject );
			mAudioEngineSource.UpdateParameters( this );
		}

		mAudioSource = GetComponent<AudioSource>();

		mThis = GCHandle.Alloc( this );
	}

	protected override void OnStart() => mAudioEngineSource?.UpdateParameters( this );

	protected override void OnDestroy()
	{
		mAudioEngineSource?.Destroy();
		mAudioEngineSource = null;
		mSource?.Release();
		mSource = null;
	}

	~SteamAudioSource()
	{
		if ( mThis.IsAllocated )
		{
			mThis.Free();
		}
	}

	protected override void OnEnabled()
	{
		mSource.AddToSimulator( mSimulator );
		SteamAudioManager.AddSource( this );

		mAudioEngineSource?.UpdateParameters( this );
	}

	protected override void OnDisabled()
	{
		SteamAudioManager.RemoveSource( this );
		mSource.RemoveFromSimulator( mSimulator );
	}

	protected override void OnUpdate() => mAudioEngineSource?.UpdateParameters( this );

	public void SetInputs( SimulationFlags flags )
	{
		var listener = SteamAudioManager.GetSteamAudioListener();

		var inputs = new SimulationInputs { };
		inputs.source.origin = Common.ConvertVector( transform.position );
		inputs.source.ahead = Common.ConvertVector( transform.forward );
		inputs.source.up = Common.ConvertVector( transform.up );
		inputs.source.right = Common.ConvertVector( transform.right );

		if ( pathing && distanceAttenuationInput == DistanceAttenuationInput.CurveDriven )
		{
			inputs.distanceAttenuationModel = mCurveAttenuationModel;
		}
		else
		{
			inputs.distanceAttenuationModel.type = DistanceAttenuationModelType.Default;
		}

		inputs.airAbsorptionModel.type = AirAbsorptionModelType.Default;
		inputs.directivity.dipoleWeight = dipoleWeight;
		inputs.directivity.dipolePower = dipolePower;
		inputs.occlusionType = occlusionType;
		inputs.occlusionRadius = occlusionRadius;
		inputs.numOcclusionSamples = occlusionSamples;
		inputs.numTransmissionRays = maxTransmissionSurfaces;
		inputs.reverbScaleLow = 1.0f;
		inputs.reverbScaleMid = 1.0f;
		inputs.reverbScaleHigh = 1.0f;
		inputs.hybridReverbTransitionTime = mSettings.HybridReverbTransitionTime;
		inputs.hybridReverbOverlapPercent = mSettings.HybridReverbOverlapPercent / 100.0f;
		inputs.baked = (reflectionsType != ReflectionsType.Realtime) ? Bool.True : Bool.False;
		inputs.pathingProbes = (pathingProbeBatch != null) ? pathingProbeBatch.GetProbeBatch() : IntPtr.Zero;
		inputs.visRadius = mSettings.BakingVisibilityRadius;
		inputs.visThreshold = mSettings.BakingVisibilityThreshold;
		inputs.visRange = mSettings.BakingVisibilityRange;
		inputs.pathingOrder = mSettings.RealTimeAmbisonicOrder;
		inputs.enableValidation = pathValidation ? Bool.True : Bool.False;
		inputs.findAlternatePaths = findAlternatePaths ? Bool.True : Bool.False;

		if ( reflectionsType == ReflectionsType.BakedStaticSource )
		{
			if ( currentBakedSource != null )
			{
				inputs.bakedDataIdentifier = currentBakedSource.GetBakedDataIdentifier();
			}
		}
		else if ( reflectionsType == ReflectionsType.BakedStaticListener )
		{
			if ( listener != null && listener.currentBakedListener != null )
			{
				inputs.bakedDataIdentifier = listener.currentBakedListener.GetBakedDataIdentifier();
			}
		}

		inputs.flags = SimulationFlags.Direct;
		if ( reflections )
		{
			if ( (reflectionsType == ReflectionsType.Realtime) ||
				(reflectionsType == ReflectionsType.BakedStaticSource && currentBakedSource != null) ||
				(reflectionsType == ReflectionsType.BakedStaticListener && listener != null && listener.currentBakedListener != null) )
			{
				inputs.flags |= SimulationFlags.Reflections;
			}
		}
		if ( pathing )
		{
			if ( pathingProbeBatch == null )
			{
				pathing = false;
				Log.Warning( $"Pathing probe batch not set, disabling pathing for source {this}." );
			}
			else
			{
				inputs.flags |= SimulationFlags.Pathing;
			}
		}

		inputs.directFlags = 0;
		if ( distanceAttenuation )
			inputs.directFlags |= DirectSimulationFlags.DistanceAttenuation;
		if ( airAbsorption )
			inputs.directFlags |= DirectSimulationFlags.AirAbsorption;
		if ( directivity )
			inputs.directFlags |= DirectSimulationFlags.Directivity;
		if ( occlusion )
			inputs.directFlags |= DirectSimulationFlags.Occlusion;
		if ( transmission )
			inputs.directFlags |= DirectSimulationFlags.Transmission;

		mSource.SetInputs( flags, inputs );
	}

	public SimulationOutputs GetOutputs( SimulationFlags flags ) => mSource.GetOutputs( flags );

	public Source GetSource() => mSource;

	public void UpdateOutputs( SimulationFlags flags )
	{
		var outputs = mSource.GetOutputs( flags );

		if ( pathing && ((flags & SimulationFlags.Pathing) != 0) )
		{
			outputs.pathing.eqCoeffsLow = MathF.Max( 0.1f, outputs.pathing.eqCoeffsLow );
			outputs.pathing.eqCoeffsMid = MathF.Max( 0.1f, outputs.pathing.eqCoeffsMid );
			outputs.pathing.eqCoeffsHigh = MathF.Max( 0.1f, outputs.pathing.eqCoeffsHigh );
		}
	}

	Vector3 DeformedVertex( Vector3 vertex ) => vertex * MathF.Pow( MathF.Abs( 1.0f - dipoleWeight + dipoleWeight * vertex.z ), dipolePower );

	public static float EvaluateDistanceCurve( float distance, IntPtr userData )
	{
		var target = (SteamAudioSource)GCHandle.FromIntPtr( userData ).Target;

		var rMin = target.mAttenuationData.minDistance;
		var rMax = target.mAttenuationData.maxDistance;

		switch ( target.mAttenuationData.rolloffMode )
		{
			case AudioRolloffMode.Logarithmic:
				if ( distance < rMin )
					return 1.0f;
				else return distance > rMax ? 0.0f : rMin / distance;
			case AudioRolloffMode.Linear:
				if ( distance < rMin )
					return 1.0f;
				else return distance > rMax ? 0.0f : (rMax - distance) / (rMax - rMin);
			case AudioRolloffMode.Custom:
				if ( distance < rMin )
					return 1.0f;
				else return distance > rMax ? 0.0f : rMin / distance;
			default:
				return 0.0f;
		}
	}
}

public enum AudioRolloffMode
{
	Logarithmic,
	Linear,
	Custom
}
