namespace FMODSbox;

using FMOD;
using Sandbox.ActionGraphs;
using Sandbox.Diagnostics;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

/// <summary>
/// Main handler for everything FMOD
/// </summary>
public partial class FMODManagerSystem : GameObjectSystem<FMODManagerSystem>, ISceneLoadingEvents
{
	public FMODManagerSystem( Scene scene ) : base( scene ) => Listen( Stage.StartUpdate, 0, StartUpdate, "FMOD OnUpdate" );

	void ISceneLoadingEvents.BeforeLoad( Scene scene, SceneLoadOptions options ) => SceneLoaded();

	[Property, JsonIgnore, ReadOnly, Hide] private SYSTEM_CALLBACK errorCallback;

	[Property, JsonIgnore, ReadOnly, Hide] private FMOD.Studio.System studioSystem;
	[Property, JsonIgnore, ReadOnly, Hide] private System coreSystem;


	private bool isMuted = false;

	private Dictionary<GUID, FMOD.Studio.EventDescription> _cachedDescriptions = [];

	[Property, JsonIgnore, ReadOnly] private readonly Dictionary<string, LoadedBank> _loadedBanks = [];
	[Property, JsonIgnore, ReadOnly] private readonly List<string> _sampleLoadRequests = [];

	[Property, JsonIgnore, ReadOnly] private readonly List<AttachedInstance> _attachedInstances = new( 128 );

	/// <summary>
	/// Unreleased instances only, so calling to stop all instances also releases the ones unreleased
	/// </summary>
	[Property, ReadOnly] public List<FMOD.Studio.EventInstance> UnreleasedInstances { get; private set; } = [];
	/// <summary>
	/// Literally all of instances. It's useful to have, believe me. Very useful for a save system, or to know how many sounds there are ever or something.
	/// </summary>
	[Property, ReadOnly] public List<InstanceHistory> AllInstancesEver { get; private set; } = [];

	/// <summary>
	/// For tracking whether this instance was ever played, so we don't remove the "just created but not yet played" instances
	/// Kinda stupid but whatever
	/// </summary>
	public struct InstanceHistory( FMOD.Studio.EventInstance instance )
	{
		public FMOD.Studio.EventInstance Instance { get; set; } = instance;
		public bool EverStarted { get; set; } = false;
	}

	public static InstanceHistory FindInstanceHistory( FMOD.Studio.EventInstance instance ) => Current.AllInstancesEver.Find( p => p.Instance.handle == instance.handle );

	private class AttachedInstance
	{
		public FMOD.Studio.EventInstance Instance;
		public Transform Transform;
		public GameObject AttachedGameObject;
		public Rigidbody RigidBody;
		public Vector3 LastFramePosition;
		public FMOD.Studio.EVENT_CALLBACK Callback;
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

		if ( !Scene.IsEditor ) NotDisposed = true;
	}

	public override void Dispose()
	{
		NotDisposed = false;
		coreSystem.setCallback( null, 0 );
		ReleaseStudioSystem();
		GC.SuppressFinalize( this );
		base.Dispose();
	}

	/// <summary>
	/// Because GOS dont dispose on scene switching, SceneLoaded initializes a new FMOD system on top of the one that was supposed to be destroyed, so we check if we need one inited first
	/// </summary>
	static private bool NotDisposed { get; set; } = false;

	public static FMOD.Studio.System StudioSystem => Current.studioSystem;

	public static System CoreSystem => Current.coreSystem;

	private struct LoadedBank
	{
		public FMOD.Studio.Bank Bank;
		public int RefCount;
	}

	private RESULT Initialize()
	{
		RESULT result = RESULT.OK;
		RESULT initResult = RESULT.OK;

		OUTPUTTYPE outputType = _fmodSettings.OutputType;
		ADVANCEDSETTINGS advancedSettings = new();

		FMOD.Studio.INITFLAGS studioInitFlags = FMOD.Studio.INITFLAGS.NORMAL | FMOD.Studio.INITFLAGS.ALLOW_MISSING_PLUGINS | FMOD.Studio.INITFLAGS.LIVEUPDATE;

		advancedSettings.profilePort = _fmodSettings.ProfilerPort; // the port it expects

		retry:

		result = FMOD.Studio.System.create( out studioSystem );
		CheckInitResult( result, "FMOD.Studio.System.create" );

		result = studioSystem.getCoreSystem( out coreSystem );
		CheckInitResult( result, "FMOD.Studio.System.getCoreSystem" );

		result = coreSystem.setOutput( outputType );
		CheckInitResult( result, "FMOD.System.setOutput" );

		result = coreSystem.setSoftwareChannels( _fmodSettings.RealChannels );
		CheckInitResult( result, "FMOD.System.setSoftwareChannels" );

		result = coreSystem.setSoftwareFormat( _fmodSettings.SampleRate, _fmodSettings.SpeakerMode, 0 );
		CheckInitResult( result, "FMOD.System.setSoftwareFormat" );

		// this is fucked, it doesnt affect the attenuation?
		result = coreSystem.set3DSettings( 1, 1, 1 );
		CheckInitResult( result, "FMOD.System.set3DSettings" );

		if ( _fmodSettings.DSPBufferLength > 0 && _fmodSettings.DSPBufferCount > 0 )
		{
			result = coreSystem.setDSPBufferSize( _fmodSettings.DSPBufferLength, _fmodSettings.DSPBufferCount );
			CheckInitResult( result, "FMOD.System.setDSPBufferSize" );
		}

		errorCallback = new SYSTEM_CALLBACK( ERROR_CALLBACK );
		result = coreSystem.setCallback( errorCallback, FMOD.SYSTEM_CALLBACK_TYPE.ERROR );
		CheckInitResult( result, "FMOD.System.setCallback" );

#if DEBUG  // used for memory profiling in studio profiler, use it only in edtior builds                                                                                
		studioInitFlags |= FMOD.Studio.INITFLAGS.MEMORY_TRACKING;
#endif

		// Source 2 is X+ Forward, Y+ Left, Z+ Up = Righthanded, FMOD is lefthanded Y-Up
		INITFLAGS coreInitFlags = FMOD.INITFLAGS.NORMAL | FMOD.INITFLAGS._3D_RIGHTHANDED;

		result = studioSystem.initialize( _fmodSettings.VirtualChannels, studioInitFlags, coreInitFlags, IntPtr.Zero );
		if ( result != FMOD.RESULT.OK && initResult == FMOD.RESULT.OK )
		{
			initResult = result; // Save this to throw at the end (we'll attempt NO SOUND to shield ourselves from unexpected device failures)
			outputType = OUTPUTTYPE.NOSOUND;
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
		LoadBanks( _fmodSettings );

		return initResult;
	}

	private void StartUpdate()
	{
		using var _ = PerformanceStats.Timings.Audio.Scope();
		if ( studioSystem.isValid() )
		{
			for ( int i = 0; i < _attachedInstances.Count; i++ )
			{
				FMOD.Studio.PLAYBACK_STATE playbackState = FMOD.Studio.PLAYBACK_STATE.STOPPED;
				if ( _attachedInstances[i].Instance.isValid() ) _attachedInstances[i].Instance.getPlaybackState( out playbackState );

				if ( playbackState == FMOD.Studio.PLAYBACK_STATE.STOPPED )
				{
					_attachedInstances.Remove( _attachedInstances[i] );
					continue;
				}

				if ( _attachedInstances[i].RigidBody.IsValid() )
				{
					_attachedInstances[i].Instance.set3DAttributes( RuntimeUtils.To3DAttributes( _attachedInstances[i].Transform, _attachedInstances[i].RigidBody.Velocity ) );
				}
				else
				{
					if ( _attachedInstances[i].AttachedGameObject.IsValid() )
						_attachedInstances[i].Transform = _attachedInstances[i].AttachedGameObject.WorldTransform;

					var position = _attachedInstances[i].Transform.Position;
					var velocity = Vector3.Zero;

					if ( Time.Delta != 0 )
					{
						velocity = (position - _attachedInstances[i].LastFramePosition) / Time.Delta;
						velocity = velocity.Clamp( velocity, 512f ); // Stops pitch fluttering when moving too quickly
					}


					_attachedInstances[i].LastFramePosition = position;
					_attachedInstances[i].Instance.set3DAttributes( RuntimeUtils.To3DAttributes( _attachedInstances[i].Transform, velocity ) );
				}

				for ( int inst = 0; inst < AllInstancesEver.Count; inst++ ) // clean out the instances that are finished
				{
					FMOD.Studio.PLAYBACK_STATE playbackAll = FMOD.Studio.PLAYBACK_STATE.STOPPED;
					bool paused = false;
					if ( AllInstancesEver[inst].Instance.isValid() )
					{
						AllInstancesEver[inst].Instance.getPlaybackState( out playbackAll );
						AllInstancesEver[inst].Instance.getPaused( out paused );
					}

					// don't clean the paused instances, because we might be unpausing them later
					if ( AllInstancesEver[inst].EverStarted && playbackAll == FMOD.Studio.PLAYBACK_STATE.STOPPED && !paused ) AllInstancesEver.Remove( AllInstancesEver[inst] );
				}
			}
#if IGNIS
			MuteAllEvents( Sandbox.Audio.AudioEngine.Mute || (!Sandbox.Audio.AudioEngine.IsFocused && Sandbox.Audio.AudioEngine.MuteLoseFocus) );
#endif
			studioSystem.update();
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
