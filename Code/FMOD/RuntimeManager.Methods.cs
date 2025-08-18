namespace FMODSbox;

public partial class FMODManager
{
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
}
