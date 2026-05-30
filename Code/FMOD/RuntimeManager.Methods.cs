using System;
using System.Runtime.InteropServices;
using FMOD;
using FMOD.Studio;

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
		SetParameterByName( instance, param.Trim(), value, ignoreseekspeed );
	}

	public static void SetParameter( EventInstance instance, string param, string value, bool ignoreseekspeed )
	{
		SetParameterByName( instance, param.Trim(), value.Trim(), ignoreseekspeed );
	}

	private static void SetParameterByName( EventInstance instance, string name, float value, bool ignoreseekspeed )
	{
		if ( instance.isValid() && !string.IsNullOrEmpty( name.Trim() ) )
			instance.setParameterByName( name.Trim(), value, ignoreseekspeed );
	}

	private static void SetParameterByName( EventInstance instance, string name, string value, bool ignoreseekspeed )
	{
		if ( instance.isValid() && !string.IsNullOrEmpty( name.Trim() ) && !string.IsNullOrEmpty( value.Trim() ) )
			instance.setParameterByNameWithLabel( name.Trim(), value.Trim(), ignoreseekspeed );
	}

	private static void SetParameterByID( PARAMETER_ID id, string value, bool ignoreseekspeed )
	{
		if ( !string.IsNullOrEmpty( value.Trim() ) )
			StudioSystem.setParameterByIDWithLabel( id, value.Trim(), ignoreseekspeed );
	}

	private static void SetParameterByID( PARAMETER_ID id, float value, bool ignoreseekspeed )
	{
		StudioSystem.setParameterByID( id, value, ignoreseekspeed );
	}

	public static void SetParameterGlobal( string param, float value, bool ignoreseekspeed )
	{
		RESULT result = RESULT.OK;
		result = StudioSystem.getParameterDescriptionByName( param.Trim(), out var parameterDescription );

		if ( result != RESULT.OK )
		{
			Log.Warning( string.Format( "[FMOD] StudioGlobalParameterTrigger failed to lookup parameter {0} : result = {1}", param, result ) );
			return;
		}

		SetParameterByID( parameterDescription.id, value, ignoreseekspeed );
	}

	public static void SetParameterGlobal( string param, string value, bool ignoreseekspeed )
	{
		RESULT result = RESULT.OK;
		result = StudioSystem.getParameterDescriptionByName( param.Trim(), out var parameterDescription );

		if ( result != RESULT.OK )
		{
			Log.Warning( string.Format( "[FMOD] StudioGlobalParameterTrigger failed to lookup parameter {0} : result = {1}", param.Trim(), result ) );
			return;
		}

		SetParameterByID( parameterDescription.id, value.Trim(), ignoreseekspeed );
	}

	public static Bus GetBus( string path )
	{
		if ( StudioSystem.getBus( path.Trim(), out Bus bus ) != RESULT.OK )
		{
			throw new BusNotFoundException( path );
		}
		return bus;
	}

	public static VCA GetVCA( string path )
	{
		if ( StudioSystem.getVCA( path.Trim(), out VCA vca ) != RESULT.OK )
		{
			throw new VCANotFoundException( path );
		}
		return vca;
	}

	public static void SetVCAVolume( string vca, float volume ) => GetVCA( vca.Trim() ).setVolume( volume );

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
			return PlayOnce( PathToGUID( path.Trim() ), position, release );
		}
		catch ( EventNotFoundException )
		{
			throw new EventNotFoundException( path.Trim() );
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

	public static EventInstance PlayCreated( EventInstance instance, Vector3 position = new Vector3(), bool release = true )
	{
		instance.set3DAttributes( RuntimeUtils.To3DAttributes( position ) );
		instance.start();
		if ( release ) instance.release();

		return instance;    // generally not a good idea to get the instance if its released buuuuut...
	}

	public static EventInstance PlayProgrammerOnce( string path, string key, EVENT_CALLBACK callback, Vector3 position = new Vector3(), bool release = true )
	{
		try
		{
			return PlayProgrammerOnce( PathToGUID( path.Trim() ), key, callback, position, release );
		}
		catch ( EventNotFoundException )
		{
			throw new EventNotFoundException( path.Trim() );
		}
	}

	/// <summary>
	/// Programmer Instrument version of regular PlayOnce, so we can have more things than in the Unity example (like 3D attributes)
	/// </summary>
	/// <param name="guid">GUID of the event</param>
	/// <param name="key">The string for the audio table</param>
	/// <param name="callback">The callback that needs to be created separately</param>
	/// <param name="position">WorldPosition of the event</param>
	/// <param name="release">Should the instance be released or we do that ourselves</param>
	public static EventInstance PlayProgrammerOnce( GUID guid, string key, EVENT_CALLBACK callback, Vector3 position = new Vector3(), bool release = true )
	{
		var instance = CreateInstance( guid );

		// Pin the key string in memory and pass a pointer through the user data
		GCHandle stringHandle = GCHandle.Alloc( key );
		instance.setUserData( GCHandle.ToIntPtr( stringHandle ) );

		instance.setCallback( callback );

		instance.set3DAttributes( RuntimeUtils.To3DAttributes( position ) );
		instance.start();
		if ( release ) instance.release();

		return instance;    // generally not a good idea to get the instance if its released buuuuut...
	}

	public static GUID PathToGUID( string path )
	{
		GUID guid;
		if ( path.StartsWith( '{' ) )
		{
			Util.parseID( path.Trim(), out guid );
		}
		else
		{
			var result = Current.studioSystem.lookupID( path, out guid );
			if ( result == RESULT.ERR_EVENT_NOTFOUND )
			{
				throw new EventNotFoundException( path.Trim() );
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
			return CreateInstance( PathToGUID( path.Trim() ) );
		}
		catch ( EventNotFoundException )
		{
			// Switch from exception with GUID to exception with path
			throw new EventNotFoundException( path.Trim() );
		}
	}

	public static EventInstance CreateInstance( GUID guid )
	{
		EventDescription eventDesc = GetEventDescription( guid );
		eventDesc.createInstance( out EventInstance newInstance );

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
			return GetEventDescription( PathToGUID( path.Trim() ) );
		}
		catch ( EventNotFoundException )
		{
			throw new EventNotFoundException( path.Trim() );
		}
	}

	public static EventDescription GetEventDescription( GUID guid )
	{
		EventDescription eventDesc;
		if ( Current._cachedDescriptions.TryGetValue( guid, out EventDescription value ) && value.isValid() )
		{
			eventDesc = value;
		}
		else
		{
			var result = Current.studioSystem.getEventByID( guid, out eventDesc );

			if ( result != RESULT.OK )
			{
				Log.Warning( guid );
			}

			if ( eventDesc.isValid() )
			{
				Current._cachedDescriptions[guid] = eventDesc;
			}
		}
		return eventDesc;
	}

	public static EventInstance PlayOnObject( EventReference eventReference, GameObject gameObject = null, bool release = true )
	{
		try
		{
			return PlayOnObject( eventReference.Guid, gameObject, release );
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
			return PlayOnObject( PathToGUID( path.Trim() ), gameObject, release );
		}
		catch ( EventNotFoundException )
		{
			throw new EventNotFoundException( path );
		}
	}

	public static EventInstance PlayProgrammerOnObject( string path, string key, GameObject gameObject, EVENT_CALLBACK callback, bool release = true )
	{
		try
		{
			return PlayProgrammerOnObject( PathToGUID( path.Trim() ), key, gameObject, callback, release );
		}
		catch ( EventNotFoundException )
		{
			throw new EventNotFoundException( path );
		}
	}

	public static EventInstance PlayOnObject( GUID guid, GameObject gameObject, bool release = true )
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

		return instance;
	}


	public static EventInstance PlayOnObject( EventInstance instance, GameObject gameObject, bool release = true )
	{
		// not checking for max distance, because we already created the instance

		if ( gameObject.Components.TryGet<Rigidbody>( out var rigid ) )
			AttachInstanceToGameObject( instance, gameObject, rigid );
		else
			AttachInstanceToGameObject( instance, gameObject );

		instance.start();
		if ( release ) instance.release();

		return instance;
	}

	public static EventInstance PlayProgrammerOnObject( GUID guid, string key, GameObject gameObject, EVENT_CALLBACK callback, bool release = true )
	{
		if ( CreateInstanceWithinMaxDistance( guid, gameObject.WorldTransform.Position, out EventInstance instance ) )
		{
			if ( gameObject.Components.TryGet<Rigidbody>( out var rigid ) )
				AttachInstanceToGameObject( instance, gameObject, rigid, callback );
			else
				AttachInstanceToGameObject( instance, gameObject, callback );

			GCHandle stringHandle = GCHandle.Alloc( key );
			instance.setUserData( GCHandle.ToIntPtr( stringHandle ) );

			instance.setCallback( callback );
			instance.start();
			if ( release ) instance.release();
		}

		return instance;
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

	private static AttachedInstance FindOrAddAttachedInstance( EventInstance instance, GameObject gameObject, ATTRIBUTES_3D attributes, EVENT_CALLBACK callback )
	{
		return FindOrAddAttachedInstance( instance, gameObject.WorldTransform, gameObject, attributes, callback );
	}

	private static AttachedInstance FindOrAddAttachedInstance( EventInstance instance, Transform transform, GameObject gameObject, ATTRIBUTES_3D attributes )
	{
		AttachedInstance attachedInstance = Current._attachedInstances.Find( x => x.Instance.handle == instance.handle );

		if ( attachedInstance is null )
		{
			attachedInstance = new AttachedInstance();
			Current._attachedInstances.Add( attachedInstance );
		}

		attachedInstance.Instance = instance;
		attachedInstance.Transform = transform;
		attachedInstance.AttachedGameObject = gameObject;
		attachedInstance.Instance.set3DAttributes( attributes );
		return attachedInstance;
	}

	private static AttachedInstance FindOrAddAttachedInstance( EventInstance instance, Transform transform, GameObject gameObject, ATTRIBUTES_3D attributes, EVENT_CALLBACK callback )
	{
		AttachedInstance attachedInstance = Current._attachedInstances.Find( x => x.Instance.handle == instance.handle );

		if ( attachedInstance is null )
		{
			attachedInstance = new AttachedInstance();
			Current._attachedInstances.Add( attachedInstance );
		}

		attachedInstance.Instance = instance;
		attachedInstance.Transform = transform;
		attachedInstance.AttachedGameObject = gameObject;
		attachedInstance.Instance.set3DAttributes( attributes );
		attachedInstance.Callback = callback;
		return attachedInstance;
	}

	public static void AttachInstanceToGameObject( EventInstance instance, GameObject gameObject )
	{
		AttachedInstance attachedInstance = FindOrAddAttachedInstance( instance, gameObject, RuntimeUtils.To3DAttributes( gameObject.WorldTransform ) );

		attachedInstance.LastFramePosition = gameObject.WorldTransform.Position;
	}

	public static void AttachInstanceToGameObject( EventInstance instance, GameObject gameObject, Rigidbody rigidBody )
	{
		AttachedInstance attachedInstance = FindOrAddAttachedInstance( instance, gameObject, RuntimeUtils.To3DAttributes( gameObject.WorldTransform, rigidBody.WorldPosition ) );

		attachedInstance.RigidBody = rigidBody;
	}

	public static void AttachInstanceToGameObject( EventInstance instance, GameObject gameObject, EVENT_CALLBACK callback )
	{
		AttachedInstance attachedInstance = FindOrAddAttachedInstance( instance, gameObject, RuntimeUtils.To3DAttributes( gameObject.WorldTransform ), callback );

		attachedInstance.LastFramePosition = gameObject.WorldTransform.Position;
	}

	public static void AttachInstanceToGameObject( EventInstance instance, GameObject gameObject, Rigidbody rigidBody, EVENT_CALLBACK callback )
	{
		AttachedInstance attachedInstance = FindOrAddAttachedInstance( instance, gameObject, RuntimeUtils.To3DAttributes( gameObject.WorldTransform, rigidBody.WorldPosition ), callback );

		attachedInstance.RigidBody = rigidBody;
	}

	public static void DetachInstanceFromGameObject( EventInstance instance )
	{
		foreach ( var attached in Current._attachedInstances )
		{
			if ( attached.Instance.handle == instance.handle )
			{
				Current._attachedInstances.Remove( attached );
				return;
			}
		}
	}

	// from the Unity scripting examples but it does the job
	public static RESULT ProgrammerEventCallback( EVENT_CALLBACK_TYPE type, IntPtr instancePtr, IntPtr parameterPtr )
	{
		EventInstance instance = new( instancePtr );

		// Retrieve the user data
		instance.getUserData( out IntPtr stringPtr );

		// Get the string object
		GCHandle stringHandle = GCHandle.FromIntPtr( stringPtr );
		string key = stringHandle.Target as string;

		switch ( type )
		{
			case EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND:
				{
					MODE soundMode = MODE.LOOP_NORMAL | FMOD.MODE.CREATECOMPRESSEDSAMPLE | FMOD.MODE.NONBLOCKING;
					var parameter = Marshal.PtrToStructure<PROGRAMMER_SOUND_PROPERTIES>( parameterPtr );

					if ( key.Contains( '.' ) )
					{
						var soundResult = CoreSystem.createSound( GetAssetFolderLocation() + "/" + key, soundMode, out FMOD.Sound programmerSound );
						if ( soundResult == RESULT.OK )
						{
							parameter.sound = programmerSound.handle;
							parameter.subsoundIndex = -1;
							Marshal.StructureToPtr( parameter, parameterPtr, false );
						}
					}
					else
					{
						var keyResult = StudioSystem.getSoundInfo( key, out SOUND_INFO programmerSoundInfo );
						if ( keyResult != FMOD.RESULT.OK )
						{
							break;
						}

						var soundResult = CoreSystem.createSound( programmerSoundInfo.name_or_data, soundMode | programmerSoundInfo.mode, ref programmerSoundInfo.exinfo, out FMOD.Sound programmerSound );
						if ( soundResult == FMOD.RESULT.OK )
						{
							parameter.sound = programmerSound.handle;
							parameter.subsoundIndex = programmerSoundInfo.subsoundindex;
							Marshal.StructureToPtr( parameter, parameterPtr, false );
						}
					}
					break;
				}
			case EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND:
				{
					var parameter = Marshal.PtrToStructure<PROGRAMMER_SOUND_PROPERTIES>( parameterPtr );
					var sound = new FMOD.Sound( parameter.sound );
					sound.release();
					break;
				}
			case EVENT_CALLBACK_TYPE.DESTROYED:
				{
					// Now the event has been destroyed, unpin the string memory so it can be garbage collected
					stringHandle.Free();
					break;
				}
		}
		return RESULT.OK;
	}
}
