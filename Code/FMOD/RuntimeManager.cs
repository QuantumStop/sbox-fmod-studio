namespace FMODSbox;

using FMOD;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

public class FMODSystem : GameObjectSystem
{
	public FMODSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.SceneLoaded, 100, SpawnManager, "Spawn Manager" );
	}

	void SpawnManager()
	{
		if ( Game.IsPlaying ) // just in case
		{
			GameObject gameObject = new() { Name = "FMOD Manager" };
			gameObject.AddComponent<FMODManager>();
			gameObject.Flags = GameObjectFlags.NotSaved | GameObjectFlags.Hidden;

			var listener = Scene.Get<StudioListener>(); // should be created AFTER the manager

			if ( listener == null )
			{
				var listen = Scene.Camera.Components.Create<StudioListener>();
				listen.NonRigidbodyVelocity = true;
			}
			else
			{
				listener.NonRigidbodyVelocity = true;
			}
		}
	}
}

[Hide]
public partial class FMODManager : Component
{
	//	private static SystemNotInitializedException initException = null;
	[Property, JsonIgnore, ReadOnly] public static FMODManager Instance;

	//	[Property, JsonIgnore, ReadOnly] private FMOD.DEBUG_CALLBACK debugCallback;
	[Property, JsonIgnore, ReadOnly] private FMOD.SYSTEM_CALLBACK errorCallback;

	[Property, JsonIgnore, ReadOnly] private FMOD.Studio.System studioSystem;
	[Property, JsonIgnore, ReadOnly] private FMOD.System coreSystem;
	//	[Property, JsonIgnore, ReadOnly] private FMOD.DSP mixerHead;

	private bool isMuted = false;

	private Dictionary<FMOD.GUID, FMOD.Studio.EventDescription> cachedDescriptions = [];

	[Property, JsonIgnore, ReadOnly] private Dictionary<string, LoadedBank> loadedBanks = [];
	[Property, JsonIgnore, ReadOnly] private List<string> sampleLoadRequests = [];

	[Property, JsonIgnore, ReadOnly] private List<AttachedInstance> attachedInstances = new( 128 );

	private class AttachedInstance
	{
		public FMOD.Studio.EventInstance Instance;
		public Transform transform;
		public GameObject attachedGameObject;
		public Rigidbody rigidBody;

		public Vector3 lastFramePosition;
	}

	private int loadingBanksRef = 0;

	private static byte[] masterBusPrefix;
	private static byte[] eventSet3DAttributes;
	private static byte[] systemGetBus;

	[Property, ReadOnly] public List<string> Banks { get; set; }
	[Property, ReadOnly] public List<string> BanksToLoad { get; set; }

	static FMODManager()
	{
		UTF8Encoding encoding = new();

		masterBusPrefix = encoding.GetBytes( "bus:/, " );
		eventSet3DAttributes = encoding.GetBytes( "EventInstance::set3DAttributes" );
		systemGetBus = encoding.GetBytes( "System::getBus" );
	}

	public static bool IsMuted => Instance.isMuted;

	protected override void OnAwake()
	{
		Instance = this;

		if ( Instance == null )
		{
			if ( !Game.IsPlaying )
			{
				Log.Error( "[FMOD] RuntimeManager accessed outside of runtime. Do not use RuntimeManager for Editor-only functionality, create your own System objects instead." );
				return;
			}
		}
		else
		{
			RuntimeUtils.EnforceLibraryOrder();
			Instance.Initialize();
		}
	}

	public static FMOD.Studio.System StudioSystem
	{
		get { return Instance.studioSystem; }
	}

	public static FMOD.System CoreSystem
	{
		get { return Instance.coreSystem; }
	}

	private struct LoadedBank
	{
		public FMOD.Studio.Bank Bank;
		public int RefCount;
	}

	private FMOD.RESULT Initialize()
	{
		FMOD.RESULT result = FMOD.RESULT.OK;
		FMOD.RESULT initResult = FMOD.RESULT.OK;

		int sampleRate = fmodSettings.SampleRate;
		int realChannels = fmodSettings.RealChannels;
		int virtualChannels = fmodSettings.VirtualChannels;
		uint dspBufferLength = fmodSettings.DSPBufferLength;
		int dspBufferCount = fmodSettings.DSPBufferCount;
		FMOD.SPEAKERMODE speakerMode = fmodSettings.SpeakerMode;
		FMOD.OUTPUTTYPE outputType = fmodSettings.OutputType;
		FMOD.ADVANCEDSETTINGS advancedSettings = new();

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

		result = coreSystem.set3DSettings( 1, 1, 1 );
		CheckInitResult( result, "FMOD.System.set3DSettings" );

		if ( dspBufferLength > 0 && dspBufferCount > 0 )
		{
			result = coreSystem.setDSPBufferSize( dspBufferLength, dspBufferCount );
			CheckInitResult( result, "FMOD.System.setDSPBufferSize" );
		}

		errorCallback = new FMOD.SYSTEM_CALLBACK( ERROR_CALLBACK );
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
			if ( result == FMOD.RESULT.ERR_NET_SOCKET_ERROR )
			{
				studioInitFlags &= ~FMOD.Studio.INITFLAGS.LIVEUPDATE;
				Log.Info( "[FMOD] Cannot open network port for Live Update (in-use), restarting with Live Update disabled." );

				result = studioSystem.release();
				CheckInitResult( result, "FMOD.Studio.System.Release" );

				goto retry;
			}
		}


		//currentPlatform.LoadPlugins( coreSystem, CheckInitResult );
		LoadBanks( fmodSettings );

		return initResult;
	}
	protected override void OnDestroy()
	{
		coreSystem.setCallback( null, 0 );
		ReleaseStudioSystem();

		//		initException = null;
		Instance = null;
	}

	protected override void OnUpdate()
	{
		if ( studioSystem.isValid() )
		{
			for ( int i = 0; i < attachedInstances.Count; i++ )
			{
				FMOD.Studio.PLAYBACK_STATE playbackState = FMOD.Studio.PLAYBACK_STATE.STOPPED;
				if ( attachedInstances[i].Instance.isValid() )
				{
					attachedInstances[i].Instance.getPlaybackState( out playbackState );
				}

				if ( playbackState == FMOD.Studio.PLAYBACK_STATE.STOPPED )
				{
					attachedInstances[i] = attachedInstances[attachedInstances.Count - 1];
					attachedInstances.RemoveAt( attachedInstances.Count - 1 );
					i--;
					continue;
				}


				if ( attachedInstances[i].rigidBody.IsValid() )
				{
					attachedInstances[i].Instance.set3DAttributes( RuntimeUtils.To3DAttributes( attachedInstances[i].transform, attachedInstances[i].rigidBody.Velocity ) );
				}
				else
				{
					// fucking why
					//	if ( !attachedInstances[i].nonRigidbodyVelocity )
					//	{
					//		attachedInstances[i].Instance.set3DAttributes( RuntimeUtils.To3DAttributes( attachedInstances[i].transform ) );
					//	}
					//	else
					{
						if ( attachedInstances[i].attachedGameObject.IsValid() )
							attachedInstances[i].transform = attachedInstances[i].attachedGameObject.WorldTransform;

						var position = attachedInstances[i].transform.Position;
						var velocity = Vector3.Zero;

						if ( Time.Delta != 0 )
						{
							velocity = (position - attachedInstances[i].lastFramePosition) / Time.Delta;
							velocity = velocity.Clamp( velocity, 20f ); // Stops pitch fluttering when moving too quickly
						}


						attachedInstances[i].lastFramePosition = position;
						attachedInstances[i].Instance.set3DAttributes( RuntimeUtils.To3DAttributes( attachedInstances[i].transform, velocity ) );
					}
				}
			}

			studioSystem.update();
		}
	}

	private static FMOD.RESULT ERROR_CALLBACK( IntPtr system, FMOD.SYSTEM_CALLBACK_TYPE type, IntPtr commanddata1, IntPtr commanddata2, IntPtr userdata )
	{
		FMOD.ERRORCALLBACK_INFO callbackInfo = Marshal.PtrToStructure<FMOD.ERRORCALLBACK_INFO>( commanddata1 );

		// Filter out benign expected errors.
		if ( (callbackInfo.instancetype == FMOD.ERRORCALLBACK_INSTANCETYPE.CHANNEL || callbackInfo.instancetype == FMOD.ERRORCALLBACK_INSTANCETYPE.CHANNELCONTROL)
			&& (callbackInfo.result == FMOD.RESULT.ERR_INVALID_HANDLE || callbackInfo.result == FMOD.RESULT.ERR_CHANNEL_STOLEN) )
		{
			return FMOD.RESULT.OK;
		}
		if ( callbackInfo.instancetype == FMOD.ERRORCALLBACK_INSTANCETYPE.STUDIO_EVENTINSTANCE
			&& callbackInfo.functionname.Equals( eventSet3DAttributes )
			&& callbackInfo.result == FMOD.RESULT.ERR_INVALID_HANDLE )
		{
			return FMOD.RESULT.OK;
		}
		if ( callbackInfo.instancetype == FMOD.ERRORCALLBACK_INSTANCETYPE.STUDIO_SYSTEM
			&& callbackInfo.functionname.Equals( systemGetBus )
			&& callbackInfo.result == FMOD.RESULT.ERR_EVENT_NOTFOUND
			&& callbackInfo.functionparams.StartsWith( masterBusPrefix ) )
		{
			return FMOD.RESULT.OK;
		}

		Log.Error( string.Format( "[FMOD] {0}({1}) returned {2} for {3} (0x{4}).",
			(string)callbackInfo.functionname, (string)callbackInfo.functionparams, callbackInfo.result, callbackInfo.instancetype, callbackInfo.instance.ToString( "X" ) ) );
		return FMOD.RESULT.OK;
	}
}
