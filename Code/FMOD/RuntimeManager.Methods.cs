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
}
