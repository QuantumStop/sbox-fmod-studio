using System;

namespace FMODSbox;

public partial class FMODManager
{
	[ConVar( "debug_fmod_initresult", Help = "Show initialization results, if something doesn't show OK, then it's bad" )] static public bool DebugResult { get; set; } = false;
	private void CheckInitResult( FMOD.RESULT result, string cause )
	{
		if ( result != FMOD.RESULT.OK )
		{
			ReleaseStudioSystem();
			throw new SystemNotInitializedException( result, cause );
		}
		else { if ( DebugResult ) Log.Info( cause + " is: " + result ); }
	}

	private void ReleaseStudioSystem()
	{
		if ( studioSystem.isValid() )
		{
			studioSystem.release();
			studioSystem.clearHandle();
		}
	}

	public static FMOD.Studio.Bus GetBus( string path )
	{
		if ( StudioSystem.getBus( path, out FMOD.Studio.Bus bus ) != FMOD.RESULT.OK )
		{
			throw new BusNotFoundException( path );
		}
		return bus;
	}

	public static FMOD.Studio.VCA GetVCA( string path )
	{
		if ( StudioSystem.getVCA( path, out FMOD.Studio.VCA vca ) != FMOD.RESULT.OK )
		{
			throw new VCANotFoundException( path );
		}
		return vca;
	}

	public static void PauseAllEvents( bool paused )
	{
		if ( StudioSystem.getBus( "bus:/", out FMOD.Studio.Bus masterBus ) == FMOD.RESULT.OK )
		{
			masterBus.setPaused( paused );
		}
	}

	public static void MuteAllEvents( bool muted )
	{
		Instance.isMuted = muted;

		ApplyMuteState();
	}

	private static void ApplyMuteState()
	{
		if ( StudioSystem.getBus( "bus:/", out FMOD.Studio.Bus masterBus ) == FMOD.RESULT.OK )
		{
			masterBus.setMute( Instance.isMuted );
		}
	}

	public static void PlayOneShot( string path, Vector3 position = default )
	{
		try
		{
			PlayOneShot( PathToGUID( path ), position );
		}
		catch
		{
			Log.Error( "Could not play sound :(" );
		}
	}

	public static void PlayOneShot( FMOD.GUID guid, Vector3 position = new Vector3() )
	{
		var instance = CreateInstance( guid );

		instance.set3DAttributes( RuntimeUtils.To3DAttributes( position ) );
		instance.start();
		instance.release();
	}

	public static FMOD.GUID PathToGUID( string path )
	{
		FMOD.GUID guid;
		if ( path.StartsWith( "{" ) )
		{
			FMOD.Studio.Util.parseID( path, out guid );
		}
		else
		{
			var result = Instance.studioSystem.lookupID( path, out guid );
			if ( result == FMOD.RESULT.ERR_EVENT_NOTFOUND )
			{
				throw new EventNotFoundException( path );
			}
		}
		return guid;
	}

	public static FMOD.Studio.EventInstance CreateInstance( EventReference eventReference )
	{
		try
		{
			return CreateInstance( eventReference.Guid );
		}
		catch ( EventNotFoundException )
		{
			throw new EventNotFoundException( eventReference );
		}
	}

	public static FMOD.Studio.EventInstance CreateInstance( string path )
	{
		try
		{
			return CreateInstance( PathToGUID( path ) );
		}
		catch ( EventNotFoundException )
		{
			// Switch from exception with GUID to exception with path
			throw new EventNotFoundException( path );
		}
	}

	public static FMOD.Studio.EventInstance CreateInstance( FMOD.GUID guid )
	{
		FMOD.Studio.EventDescription eventDesc = GetEventDescription( guid );
		FMOD.Studio.EventInstance newInstance;
		eventDesc.createInstance( out newInstance );

		return newInstance;
	}

	public static FMOD.Studio.EventDescription GetEventDescription( EventReference eventReference )
	{
		try
		{
			return GetEventDescription( eventReference.Guid );
		}
		catch ( EventNotFoundException )
		{
			throw new EventNotFoundException( eventReference );
		}
	}

	public static FMOD.Studio.EventDescription GetEventDescription( string path )
	{
		try
		{
			return GetEventDescription( PathToGUID( path ) );
		}
		catch ( EventNotFoundException )
		{
			Log.Warning( path );
			throw new();
		}
	}

	public static FMOD.Studio.EventDescription GetEventDescription( FMOD.GUID guid )
	{
		FMOD.Studio.EventDescription eventDesc;
		if ( Instance.cachedDescriptions.ContainsKey( guid ) && Instance.cachedDescriptions[guid].isValid() )
		{
			eventDesc = Instance.cachedDescriptions[guid];
		}
		else
		{
			var result = Instance.studioSystem.getEventByID( guid, out eventDesc );

			if ( result != FMOD.RESULT.OK )
			{
				Log.Error( guid );
			}

			if ( eventDesc.isValid() )
			{
				Instance.cachedDescriptions[guid] = eventDesc;
			}
		}
		return eventDesc;
	}

	public static void PlayOneShotAttached( EventReference eventReference, GameObject gameObject )
	{
		try
		{
			PlayOneShotAttached( eventReference.Guid, gameObject );
		}
		catch ( EventNotFoundException )
		{
			Log.Warning( "[FMOD] Event not found: " + eventReference );
		}
	}

	public static void PlayOneShotAttached( string path, GameObject gameObject )
	{
		try
		{
			PlayOneShotAttached( PathToGUID( path ), gameObject );
		}
		catch ( EventNotFoundException )
		{
			Log.Warning( "[FMOD] Event not found: " + path );
		}
	}

	public static void PlayOneShotAttached( FMOD.GUID guid, GameObject gameObject )
	{
		if ( CreateInstanceWithinMaxDistance( guid, gameObject.WorldTransform.Position, out FMOD.Studio.EventInstance instance ) )
		{
			if ( gameObject.Components.TryGet<Rigidbody>( out var rigid ) )
				AttachInstanceToGameObject( instance, gameObject, rigid );
			else
				AttachInstanceToGameObject( instance, gameObject );

			instance.start();
			instance.release();
		}
	}

	private static bool CreateInstanceWithinMaxDistance( FMOD.GUID guid, Vector3 position, out FMOD.Studio.EventInstance instance )
	{
		FMOD.Studio.EventDescription description = GetEventDescription( guid );
		if ( fmodSettings.StopEventsOutsideMaxDistance )
		{
			description.is3D( out bool is3D );
			if ( is3D )
			{
				description.getMinMaxDistance( out float min, out float max );
				if ( StudioListener.DistanceSquaredToNearestListener( position ) > (max * max) )
				{
					instance = new FMOD.Studio.EventInstance();
					return false;
				}
			}
		}

		description.createInstance( out instance );
		Log.Info( instance );
		return true;
	}

	private static AttachedInstance FindOrAddAttachedInstance( FMOD.Studio.EventInstance instance, GameObject gameObject, FMOD.ATTRIBUTES_3D attributes )
	{
		return FindOrAddAttachedInstance(instance, gameObject.WorldTransform, gameObject, attributes);
	}

	private static AttachedInstance FindOrAddAttachedInstance( FMOD.Studio.EventInstance instance, Transform transform, FMOD.ATTRIBUTES_3D attributes )
	{
		return FindOrAddAttachedInstance( instance, transform, null, attributes );
	}

	private static AttachedInstance FindOrAddAttachedInstance( FMOD.Studio.EventInstance instance, Transform transform, GameObject gameObject, FMOD.ATTRIBUTES_3D attributes )
	{
		AttachedInstance attachedInstance = Instance.attachedInstances.Find( x => x.Instance.handle == instance.handle );

		if ( attachedInstance == null )
		{
			attachedInstance = new AttachedInstance();
			Instance.attachedInstances.Add( attachedInstance );
		}
		attachedInstance.Instance = instance;
		attachedInstance.transform = transform;
		attachedInstance.attachedGameObject = gameObject;
		attachedInstance.Instance.set3DAttributes( attributes );
		return attachedInstance;
	}

	public static void AttachInstanceToGameObject( FMOD.Studio.EventInstance instance, GameObject gameObject )
	{
		AttachedInstance attachedInstance = FindOrAddAttachedInstance( instance, gameObject, RuntimeUtils.To3DAttributes( gameObject.WorldTransform ) );

		attachedInstance.lastFramePosition = gameObject.WorldTransform.Position;
	}

	public static void AttachInstanceToGameObject( FMOD.Studio.EventInstance instance, GameObject gameObject, Rigidbody rigidBody )
	{
		AttachedInstance attachedInstance = FindOrAddAttachedInstance( instance, gameObject, RuntimeUtils.To3DAttributes( gameObject.WorldTransform, rigidBody.WorldPosition ) );

		attachedInstance.rigidBody = rigidBody;
	}
}
