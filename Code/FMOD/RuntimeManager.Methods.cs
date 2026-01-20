using FMOD;
using FMOD.Studio;
using Sandbox;
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection.Metadata;
using System.Xml.Linq;
using static Sandbox.Gizmo;

namespace FMODSbox;

public partial class FMODManagerSystem
{
	[ConVar( "debug_fmod_initresult", Help = "Show initialization results, if something doesn't show OK, then it's bad" )] static public bool DebugResult { get; set; } = false;
	private void CheckInitResult( FMOD.RESULT result, string cause )
	{
		if ( result != RESULT.OK )
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

	public static void SetParameter( EventInstance instance, string param, float value, bool ignoreseekspeed )
	{
		SetParameterByName( instance, param, value, ignoreseekspeed );
	}

	public static void SetParameter( EventInstance instance, string param, string value, bool ignoreseekspeed )
	{
		SetParameterByName( instance, param, value, ignoreseekspeed );
	}

	private static void SetParameterByName( EventInstance instance, string name, float value, bool ignoreseekspeed )
	{
		if ( instance.isValid() && !string.IsNullOrEmpty( name ) )
			instance.setParameterByName( name, value, ignoreseekspeed );
	}

	private static void SetParameterByName( EventInstance instance, string name, string value, bool ignoreseekspeed )
	{
		if ( instance.isValid() && !string.IsNullOrEmpty( name ) && !string.IsNullOrEmpty( value ) )
			instance.setParameterByNameWithLabel( name, value, ignoreseekspeed );
	}

	private static void SetParameterByID( PARAMETER_ID id, string value, bool ignoreseekspeed )
	{
		if ( !string.IsNullOrEmpty( value ) )
			StudioSystem.setParameterByIDWithLabel( id, value, ignoreseekspeed );
	}

	private static void SetParameterByID( PARAMETER_ID id, float value, bool ignoreseekspeed )
	{
		StudioSystem.setParameterByID( id, value, ignoreseekspeed );
	}

	public static void SetParameterGlobal( string param, float value, bool ignoreseekspeed )
	{
		RESULT result = RESULT.OK;
		result = StudioSystem.getParameterDescriptionByName( param, out var parameterDescription );

		if ( result != RESULT.OK )
		{
			Log.Warning( string.Format( ("[FMOD] StudioGlobalParameterTrigger failed to lookup parameter {0} : result = {1}"), param, result ) );
			return;
		}

		SetParameterByID( parameterDescription.id, value, ignoreseekspeed );
	}

	public static void SetParameterGlobal( string param, string value, bool ignoreseekspeed )
	{
		RESULT result = RESULT.OK;
		result = StudioSystem.getParameterDescriptionByName( param, out var parameterDescription );

		if ( result != RESULT.OK )
		{
			Log.Warning( string.Format( ("[FMOD] StudioGlobalParameterTrigger failed to lookup parameter {0} : result = {1}"), param, result ) );
			return;
		}

		SetParameterByID( parameterDescription.id, value, ignoreseekspeed );
	}

	public static Bus GetBus( string path )
	{
		if ( StudioSystem.getBus( path, out Bus bus ) != RESULT.OK )
		{
			throw new BusNotFoundException( path );
		}
		return bus;
	}

	public static VCA GetVCA( string path )
	{
		if ( StudioSystem.getVCA( path, out VCA vca ) != RESULT.OK )
		{
			throw new VCANotFoundException( path );
		}
		return vca;
	}

	public static void SetVCAVolume( string vca, float volume ) => GetVCA( vca ).setVolume( volume );

	public static void PauseAllEvents( bool paused )
	{
		if ( StudioSystem.getBus( "bus:/", out Bus masterBus ) == RESULT.OK )
		{
			masterBus.setPaused( paused );
		}
	}

	public static void PauseEventsOnBus( bool paused, string bus ) => GetBus( bus ).setPaused( paused );


	public static void MuteAllEvents( bool muted )
	{
		Current.isMuted = muted;

		ApplyMuteState();
	}

	private static void ApplyMuteState()
	{
		if ( StudioSystem.getBus( "bus:/", out Bus masterBus ) == RESULT.OK )
		{
			masterBus.setMute( Current.isMuted );
		}
	}
	/// <summary>
	/// PlayOnObject a sound which is immediately released, making it innacessible (oneshot sound)
	/// </summary>
	/// <param name="path">Path string of the event</param>
	/// <param name="position">WorldPosition of the event</param>
	/// <param name="release">Should the instance be released or we do that ourselves</param>
	public static EventInstance PlayOnce( string path, Vector3 position = default, bool release = true )
	{
		try
		{
			return PlayOnce( PathToGUID( path ), position, release );
		}
		catch ( EventNotFoundException )
		{
			throw new EventNotFoundException( path );
		}
	}

	/// <summary>
	/// PlayOnObject a sound which is immediately released, making it innacessible (oneshot sound)
	/// </summary>
	/// <param name="guid">GUID of the event</param>
	/// <param name="position">WorldPosition of the event</param>
	/// <param name="release">Should the instance be released or we do that ourselves</param>
	public static EventInstance PlayOnce( GUID guid, Vector3 position = new Vector3(), bool release = true )
	{
		var instance = CreateInstance( guid );

		instance.set3DAttributes( RuntimeUtils.To3DAttributes( position ) );
		instance.start();
		if ( release ) instance.release();

		return instance;    // generally not a good idea to get the instance if its released buuuuut...
	}

	public static GUID PathToGUID( string path )
	{
		GUID guid;
		if ( path.StartsWith( "{" ) )
		{
			Util.parseID( path, out guid );
		}
		else
		{
			var result = Current.studioSystem.lookupID( path, out guid );
			if ( result == RESULT.ERR_EVENT_NOTFOUND )
			{
				throw new EventNotFoundException( path );
			}
		}
		return guid;
	}

	public static EventInstance CreateInstance( EventReference eventReference )
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

	public static EventInstance CreateInstance( string path )
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

	public static EventInstance CreateInstance( GUID guid )
	{
		EventDescription eventDesc = GetEventDescription( guid );
		EventInstance newInstance;
		eventDesc.createInstance( out newInstance );

		return newInstance;
	}

	public static EventDescription GetEventDescription( EventReference eventReference )
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

	public static EventDescription GetEventDescription( string path )
	{
		try
		{
			return GetEventDescription( PathToGUID( path ) );
		}
		catch ( EventNotFoundException )
		{
			throw new EventNotFoundException( path );
		}
	}

	public static EventDescription GetEventDescription( GUID guid )
	{
		EventDescription eventDesc;
		if ( Current.cachedDescriptions.TryGetValue( guid, out EventDescription value ) && value.isValid() )
		{
			eventDesc = value;
		}
		else
		{
			var result = Current.studioSystem.getEventByID( guid, out eventDesc );

			if ( result != RESULT.OK )
			{
				Log.Error( guid );
			}

			if ( eventDesc.isValid() )
			{
				Current.cachedDescriptions[guid] = eventDesc;
			}
		}
		return eventDesc;
	}

	public static EventInstance PlayOnObject( EventReference eventReference, GameObject gameObject = null, bool release = true )
	{
		try
		{
			PlayOnObject( eventReference.Guid, gameObject, out var eventInstance, release );
			return eventInstance;
		}
		catch ( EventNotFoundException )
		{
			throw new EventNotFoundException( eventReference );
		}
	}

	public static EventInstance PlayOnObject( string path, GameObject gameObject, bool release = true )
	{
		try
		{
			PlayOnObject( PathToGUID( path ), gameObject, out var instance, release );
			return instance;
		}
		catch ( EventNotFoundException )
		{
			throw new EventNotFoundException( path );
		}
	}

	public static void PlayOnObject( GUID guid, GameObject gameObject, out EventInstance eventInstance, bool release = true )
	{
		if ( CreateInstanceWithinMaxDistance( guid, gameObject.WorldTransform.Position, out EventInstance instance ) )
		{
			if ( gameObject.Components.TryGet<Rigidbody>( out var rigid ) )
				AttachInstanceToGameObject( instance, gameObject, rigid );
			else
				AttachInstanceToGameObject( instance, gameObject );

			instance.start();
			if ( release ) instance.release();
		}

		eventInstance = instance;
	}

	public static bool CreateInstanceWithinMaxDistance( GUID guid, Vector3 position, out EventInstance instance )
	{
		EventDescription description = GetEventDescription( guid );
		if ( fmodSettings.StopEventsOutsideMaxDistance )
		{
			description.is3D( out bool is3D );
			if ( is3D )
			{
				description.getMinMaxDistance( out float min, out float max );
				if ( DistanceSquaredToNearestListener( position ) > (max * max) )
				{
					instance = new EventInstance();
					return false;
				}
			}
		}

		description.createInstance( out instance );
		return true;
	}

	private static AttachedInstance FindOrAddAttachedInstance( EventInstance instance, GameObject gameObject, ATTRIBUTES_3D attributes )
	{
		return FindOrAddAttachedInstance( instance, gameObject.WorldTransform, gameObject, attributes );
	}

	private static AttachedInstance FindOrAddAttachedInstance( EventInstance instance, Transform transform, ATTRIBUTES_3D attributes )
	{
		return FindOrAddAttachedInstance( instance, transform, null, attributes );
	}

	private static AttachedInstance FindOrAddAttachedInstance( EventInstance instance, Transform transform, GameObject gameObject, ATTRIBUTES_3D attributes )
	{
		AttachedInstance attachedInstance = Current.attachedInstances.Find( x => x.Instance.handle == instance.handle );

		if ( attachedInstance == null )
		{
			attachedInstance = new AttachedInstance();
			Current.attachedInstances.Add( attachedInstance );
		}
		attachedInstance.Instance = instance;
		attachedInstance.transform = transform;
		attachedInstance.attachedGameObject = gameObject;
		attachedInstance.Instance.set3DAttributes( attributes );
		return attachedInstance;
	}

	public static void AttachInstanceToGameObject( EventInstance instance, GameObject gameObject )
	{
		AttachedInstance attachedInstance = FindOrAddAttachedInstance( instance, gameObject, RuntimeUtils.To3DAttributes( gameObject.WorldTransform ) );

		attachedInstance.lastFramePosition = gameObject.WorldTransform.Position;
	}

	public static void AttachInstanceToGameObject( EventInstance instance, GameObject gameObject, Rigidbody rigidBody )
	{
		AttachedInstance attachedInstance = FindOrAddAttachedInstance( instance, gameObject, RuntimeUtils.To3DAttributes( gameObject.WorldTransform, rigidBody.WorldPosition ) );

		attachedInstance.rigidBody = rigidBody;
	}
	public static void DetachInstanceFromGameObject( EventInstance instance )
	{
		foreach ( var attached in Current.attachedInstances )
		{
			if ( attached.Instance.handle == instance.handle )
			{
				Current.attachedInstances.Remove( attached );
				return;
			}
		}
	}
}
