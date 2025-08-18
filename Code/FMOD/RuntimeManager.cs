namespace FMODSbox;

using System;
using System.Text;
using System.Text.Json.Serialization;

[Title( "FMOD Manager" )]
public class FMODManager : Component
{
	public const string BankStubPrefix = "bank stub:";

	private static SystemNotInitializedException initException = null;
	[Property, JsonIgnore, ReadOnly] private static FMODManager Instance;


	[Property, JsonIgnore, ReadOnly] private FMOD.DEBUG_CALLBACK debugCallback;
	[Property, JsonIgnore, ReadOnly] private FMOD.SYSTEM_CALLBACK errorCallback;

	[Property, JsonIgnore, ReadOnly] private FMOD.Studio.System studioSystem;
	[Property, JsonIgnore, ReadOnly] private FMOD.System coreSystem;
	[Property, JsonIgnore, ReadOnly] private FMOD.DSP mixerHead;

	private bool isMuted = false;

	private Dictionary<FMOD.GUID, FMOD.Studio.EventDescription> cachedDescriptions = [];

	[Property, JsonIgnore, ReadOnly] private Dictionary<string, LoadedBank> loadedBanks = [];
	[Property, JsonIgnore, ReadOnly] private List<string> sampleLoadRequests = [];

	[Property, JsonIgnore, ReadOnly] private List<AttachedInstance> attachedInstances = new( 128 );

	private class AttachedInstance
	{
		public FMOD.Studio.EventInstance Instance;
		public Transform transform;

		public Vector3 lastFramePosition;
		public bool nonRigidbodyVelocity;
	}

	private int loadingBanksRef = 0;

	private static byte[] masterBusPrefix;
	private static byte[] eventSet3DAttributes;
	private static byte[] systemGetBus;

	static FMODManager()
	{
		//NativeHelper.AddDllSearchPath( "balls" );

		UTF8Encoding encoding = new();

		masterBusPrefix = encoding.GetBytes( "bus:/, " );
		eventSet3DAttributes = encoding.GetBytes( "EventInstance::set3DAttributes" );
		systemGetBus = encoding.GetBytes( "System::getBus" );
	}

	public static bool IsMuted
	{
		get
		{
			return Instance.isMuted;
		}
	}

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

			FMOD.RESULT initResult = FMOD.RESULT.OK; // Initialize can return an error code if it falls back to NO_SOUND, throw it as a non-cached exception
		}
		else
		{
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

	// Explicit comparer to avoid issues on platforms that don't support JIT compilation
	private class GuidComparer : IEqualityComparer<FMOD.GUID>
	{
		bool IEqualityComparer<FMOD.GUID>.Equals( FMOD.GUID x, FMOD.GUID y )
		{
			return x.Equals( y );
		}

		int IEqualityComparer<FMOD.GUID>.GetHashCode( FMOD.GUID obj )
		{
			return obj.GetHashCode();
		}
	}

	private void CheckInitResult( FMOD.RESULT result, string cause )
	{
		if ( result != FMOD.RESULT.OK )
		{
			ReleaseStudioSystem();
			throw new SystemNotInitializedException( result, cause );
		}
	}

	private void ReleaseStudioSystem()
	{
		if ( studioSystem.isValid() )
		{
			studioSystem.release();
			studioSystem.clearHandle();
		}
	}

	private FMOD.RESULT Initialize()
	{
		FMOD.RESULT result = FMOD.RESULT.OK;
		FMOD.RESULT initResult = FMOD.RESULT.OK;

		int sampleRate = 48000;
		int realChannels = 256;
		int virtualChannels = 128;
		uint dspBufferLength = 0;
		int dspBufferCount = 0;
		FMOD.SPEAKERMODE speakerMode = FMOD.SPEAKERMODE.STEREO;
		FMOD.OUTPUTTYPE outputType = FMOD.OUTPUTTYPE.AUTODETECT;

		FMOD.Studio.INITFLAGS studioInitFlags = FMOD.Studio.INITFLAGS.NORMAL | FMOD.Studio.INITFLAGS.DEFERRED_CALLBACKS;
		//	if ( currentPlatform.IsLiveUpdateEnabled )
		//	{
		//		studioInitFlags |= FMOD.Studio.INITFLAGS.LIVEUPDATE;
		//		advancedSettings.profilePort = (ushort)currentPlatform.LiveUpdatePort;
		//	}

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

		if ( dspBufferLength > 0 && dspBufferCount > 0 )
		{
			result = coreSystem.setDSPBufferSize( dspBufferLength, dspBufferCount );
			CheckInitResult( result, "FMOD.System.setDSPBufferSize" );
		}

		/*		if ( fmodSettings.EnableErrorCallback )
				{
					errorCallback = new FMOD.SYSTEM_CALLBACK( ERROR_CALLBACK );
					result = coreSystem.setCallback( errorCallback, FMOD.SYSTEM_CALLBACK_TYPE.ERROR );
					CheckInitResult( result, "FMOD.System.setCallback" );
				}

				if ( fmodSettings.EnableMemoryTracking )
				{
					studioInitFlags |= FMOD.Studio.INITFLAGS.MEMORY_TRACKING;
				}*/

		//currentPlatform.PreInitialize( studioSystem );

		//PlatformCallbackHandler callbackHandler = currentPlatform.CallbackHandler;

		//if ( callbackHandler != null )
		//{
		//	callbackHandler.PreInitialize( studioSystem, CheckInitResult );
		//}

		result = studioSystem.initialize( virtualChannels, studioInitFlags, FMOD.INITFLAGS.NORMAL, IntPtr.Zero );
		if ( result != FMOD.RESULT.OK && initResult == FMOD.RESULT.OK )
		{
			initResult = result; // Save this to throw at the end (we'll attempt NO SOUND to shield ourselves from unexpected device failures)
			outputType = FMOD.OUTPUTTYPE.NOSOUND;
			Log.Warning( "[FMOD] Studio::System::initialize returned {0}, defaulting to no-sound mode." );

			//goto retry;
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

				//	goto retry;
			}
		}

		//currentPlatform.LoadPlugins( coreSystem, CheckInitResult );
		//	LoadBanks( fmodSettings );

		return initResult;
	}

	protected override void OnDestroy()
	{
		coreSystem.setCallback( null, 0 );
		ReleaseStudioSystem();

		initException = null;
		Instance = null;
	}
}
