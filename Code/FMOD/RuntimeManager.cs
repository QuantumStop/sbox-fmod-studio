namespace FMODSbox;

using FMOD;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
/// <summary>
/// Main handler for everything FMOD
/// </summary>
public partial class FMODManagerSystem : GameObjectSystem<FMODManagerSystem>
{
	[Property, ReadOnly, Hide] public bool SceneInitialized { get; set; } = false;

	public FMODManagerSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.SceneLoaded, 0, SceneLoaded, "FMOD OnStart" );
		Listen( Stage.StartUpdate, 0, StartUpdate, "FMOD OnUpdate" );
	}

	[Property, JsonIgnore, ReadOnly, Hide] private SYSTEM_CALLBACK errorCallback;

	[Property, JsonIgnore, ReadOnly, Hide] private FMOD.Studio.System studioSystem;
	[Property, JsonIgnore, ReadOnly, Hide] private System coreSystem;


	private bool isMuted = false;

	private Dictionary<GUID, FMOD.Studio.EventDescription> cachedDescriptions = [];

	[Property, JsonIgnore, ReadOnly] private readonly Dictionary<string, LoadedBank> loadedBanks = [];
	[Property, JsonIgnore, ReadOnly] private readonly List<string> sampleLoadRequests = [];

	[Property, JsonIgnore, ReadOnly] private readonly List<AttachedInstance> attachedInstances = new( 128 );

	private class AttachedInstance
	{
		public FMOD.Studio.EventInstance Instance;
		public Transform Transform;
		public GameObject AttachedGameObject;
		public Rigidbody RigidBody;
		public Vector3 LastFramePosition;
	}

	private int loadingBanksRef = 0;

	private static readonly byte[] masterBusPrefix;
	private static readonly byte[] eventSet3DAttributes;
	private static readonly byte[] systemGetBus;

	[Property, ReadOnly, Hide] public List<string> Banks { get; set; }
	[Property, ReadOnly, Hide] public List<string> BanksToLoad { get; set; }

	static FMODManagerSystem()
	{
		UTF8Encoding encoding = new();

		masterBusPrefix = encoding.GetBytes( "bus:/, " );
		eventSet3DAttributes = encoding.GetBytes( "EventInstance::set3DAttributes" );
		systemGetBus = encoding.GetBytes( "System::getBus" );
	}

	public static bool IsMuted => Current.isMuted;

	private void SceneLoaded()
	{
		if ( NotDisposed ) return;

		RuntimeUtils.EnforceLibraryOrder();
		Current.Initialize();
		Current.SpawnListenerOnCamera();
		Current.AfterInit();
	}

	public override void Dispose()
	{
		NotDisposed = false;
		coreSystem.setCallback( null, 0 );
		ReleaseStudioSystem();
		base.Dispose();
	}

	/// <summary>
	/// Because GOS dont dispose on scene switching, SceneLoaded initializes a new FMOD system on top of the one that was supposed to be destroyed, so we check if we need one inited first
	/// </summary>
	static private bool NotDisposed { get; set; } = false;


	/// <summary>
	/// SceneLoaded is too late for us to do stuff in OnEnabled, do it ourselves
	/// </summary>
	private void AfterInit()
	{
		if ( Scene.IsEditor ) return;

		SceneInitialized = true;
		NotDisposed = true;

		IFMODEvents.Post( x => x.OnAfterInit() );
	}

	public static FMOD.Studio.System StudioSystem { get => Current.studioSystem; }

	public static System CoreSystem { get => Current.coreSystem; }

	private struct LoadedBank
	{
		public FMOD.Studio.Bank Bank;
		public int RefCount;
	}

	private RESULT Initialize()
	{
		RESULT result = RESULT.OK;
		RESULT initResult = RESULT.OK;

		int sampleRate = fmodSettings.SampleRate;
		int realChannels = fmodSettings.RealChannels;
		int virtualChannels = fmodSettings.VirtualChannels;
		uint dspBufferLength = fmodSettings.DSPBufferLength;
		int dspBufferCount = fmodSettings.DSPBufferCount;
		SPEAKERMODE speakerMode = fmodSettings.SpeakerMode;
		OUTPUTTYPE outputType = fmodSettings.OutputType;
		ADVANCEDSETTINGS advancedSettings = new();

		FMOD.Studio.INITFLAGS studioInitFlags = FMOD.Studio.INITFLAGS.NORMAL | FMOD.Studio.INITFLAGS.ALLOW_MISSING_PLUGINS | FMOD.Studio.INITFLAGS.LIVEUPDATE;

		advancedSettings.profilePort = fmodSettings.ProfilerPort; // the port it expects

		retry:

		result = FMOD.Studio.System.create( out studioSystem );
		CheckInitResult( result, "FMOD.Studio.System.create" );

		result = studioSystem.getCoreSystem( out coreSystem );
		CheckInitResult( result, "FMOD.Studio.System.getCoreSystem" );

		result = coreSystem.setOutput( outputType );
		CheckInitResult( result, "FMOD.System.setOutput" );

		result = coreSystem.setSoftwareChannels( realChannels );
		CheckInitResult( result, "FMOD.System.setSoftwareChannels" );

		result = coreSystem.setSoftwareFormat( sampleRate, speakerMode, 0 );
		CheckInitResult( result, "FMOD.System.setSoftwareFormat" );

		// this is fucked, it doesnt affect the attenuation?
		result = coreSystem.set3DSettings( 1, 1, 1 );
		CheckInitResult( result, "FMOD.System.set3DSettings" );

		if ( dspBufferLength > 0 && dspBufferCount > 0 )
		{
			result = coreSystem.setDSPBufferSize( dspBufferLength, dspBufferCount );
			CheckInitResult( result, "FMOD.System.setDSPBufferSize" );
		}

		errorCallback = new SYSTEM_CALLBACK( ERROR_CALLBACK );
		result = coreSystem.setCallback( errorCallback, FMOD.SYSTEM_CALLBACK_TYPE.ERROR );
		CheckInitResult( result, "FMOD.System.setCallback" );

#if DEBUG  // used for memory profiling in studio profiler, use it only in edtior builds                                                                                
		studioInitFlags |= FMOD.Studio.INITFLAGS.MEMORY_TRACKING;
#endif

		// Source 2 is X+ Forward, Y+ Left, Z+ Up = Righthanded, FMOD is lefthanded Y-Up
		FMOD.INITFLAGS coreInitFlags = FMOD.INITFLAGS.NORMAL | FMOD.INITFLAGS._3D_RIGHTHANDED;

		result = studioSystem.initialize( virtualChannels, studioInitFlags, coreInitFlags, IntPtr.Zero );
		if ( result != FMOD.RESULT.OK && initResult == FMOD.RESULT.OK )
		{
			initResult = result; // Save this to throw at the end (we'll attempt NO SOUND to shield ourselves from unexpected device failures)
			outputType = FMOD.OUTPUTTYPE.NOSOUND;
			Log.Warning( "[FMOD] Studio::System::initialize returned {0}, defaulting to no-sound mode." );

			goto retry;
		}

		CheckInitResult( result, "Studio::System::initialize" );

		// Test network functionality triggered during System::update
		if ( (studioInitFlags & FMOD.Studio.INITFLAGS.LIVEUPDATE) != 0 )
		{
			studioSystem.flushCommands(); // Any error will be returned through Studio.System.update

			result = studioSystem.update();
			if ( result == RESULT.ERR_NET_SOCKET_ERROR )
			{
				studioInitFlags &= ~FMOD.Studio.INITFLAGS.LIVEUPDATE;
				Log.Info( "[FMOD] Cannot open network port for Live Update (in-use), restarting with Live Update disabled." );

				result = studioSystem.release();
				CheckInitResult( result, "FMOD.Studio.System.Release" );

				goto retry;
			}
		}


		LoadPlugins( coreSystem, CheckInitResult );
		LoadBanks( fmodSettings );

		return initResult;
	}

	private void StartUpdate()
	{
		if ( studioSystem.isValid() )
		{
			foreach ( var attached in attachedInstances.ToList() ) // could be bad
			{
				FMOD.Studio.PLAYBACK_STATE playbackState = FMOD.Studio.PLAYBACK_STATE.STOPPED;
				if ( attached.Instance.isValid() )
				{
					attached.Instance.getPlaybackState( out playbackState );
				}

				if ( playbackState == FMOD.Studio.PLAYBACK_STATE.STOPPED )
				{
					attachedInstances.Remove( attached );
					continue;
				}

				if ( attached.RigidBody.IsValid() )
				{
					attached.Instance.set3DAttributes( RuntimeUtils.To3DAttributes( attached.Transform, attached.RigidBody.Velocity ) );
				}
				else
				{
					if ( attached.AttachedGameObject.IsValid() )
						attached.Transform = attached.AttachedGameObject.WorldTransform;

					var position = attached.Transform.Position;
					var velocity = Vector3.Zero;

					if ( Time.Delta != 0 )
					{
						velocity = (position - attached.LastFramePosition) / Time.Delta;
						velocity = velocity.Clamp( velocity, 512f ); // Stops pitch fluttering when moving too quickly
					}


					attached.LastFramePosition = position;
					attached.Instance.set3DAttributes( RuntimeUtils.To3DAttributes( attached.Transform, velocity ) );
				}
			}

			studioSystem.update();
#if CHAOS
			MuteAllEvents( Sandbox.Audio.AudioEngine.Mute || (!Sandbox.Audio.AudioEngine.IsFocused && Sandbox.Audio.AudioEngine.MuteLoseFocus) );
#endif
		}
	}

	private static RESULT ERROR_CALLBACK( IntPtr system, SYSTEM_CALLBACK_TYPE type, IntPtr commanddata1, IntPtr commanddata2, IntPtr userdata )
	{
		ERRORCALLBACK_INFO callbackInfo = Marshal.PtrToStructure<ERRORCALLBACK_INFO>( commanddata1 );

		// Filter out benign expected errors.
		if ( (callbackInfo.instancetype == ERRORCALLBACK_INSTANCETYPE.CHANNEL || callbackInfo.instancetype == FMOD.ERRORCALLBACK_INSTANCETYPE.CHANNELCONTROL)
			&& (callbackInfo.result == RESULT.ERR_INVALID_HANDLE || callbackInfo.result == RESULT.ERR_CHANNEL_STOLEN) )
		{
			return RESULT.OK;
		}
		if ( callbackInfo.instancetype == ERRORCALLBACK_INSTANCETYPE.STUDIO_EVENTINSTANCE
			&& callbackInfo.functionname.Equals( eventSet3DAttributes )
			&& callbackInfo.result == RESULT.ERR_INVALID_HANDLE )
		{
			return RESULT.OK;
		}
		if ( callbackInfo.instancetype == ERRORCALLBACK_INSTANCETYPE.STUDIO_SYSTEM
			&& callbackInfo.functionname.Equals( systemGetBus )
			&& callbackInfo.result == RESULT.ERR_EVENT_NOTFOUND
			&& callbackInfo.functionparams.StartsWith( masterBusPrefix ) )
		{
			return RESULT.OK;
		}

		Log.Warning( string.Format( "[FMOD] {0}({1}) returned {2} for {3} (0x{4}).",
			(string)callbackInfo.functionname, (string)callbackInfo.functionparams, callbackInfo.result, callbackInfo.instancetype, callbackInfo.instance.ToString( "X" ) ) );
		return RESULT.OK;
	}
}
