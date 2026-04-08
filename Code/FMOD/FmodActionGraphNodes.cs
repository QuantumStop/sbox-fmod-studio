namespace FMODSbox;

public sealed class FmodActionGraphNodes
{
	[ActionGraphNode( "fmod.set_parameter_float" )]
	[Title( "Set FMOD Parameter (Float)" )]
	[Group( "FMOD" )]
	[Icon( "tune" )]
	private static void SetParameterFloat( StudioSoundPoint sound, string parameter, float value )
	{
		if ( sound == null || !sound.Instance.isValid() )
			return;

		sound.Instance.setParameterByName( parameter, value );
	}

	[ActionGraphNode( "fmod.play_event_resource" )]
	[Title( "Play FMOD Event (From Resource)" )]
	[Group( "FMOD" )]
	[Icon( "tune" )]
	private static FMODEventResource PlayFMODEvent( StudioSoundPoint sound, [ActionGraphProperty] FMODEventResource resource )
	{
		if ( sound == null || resource == null )
			return null;

		sound.Event = resource;
		//sound.UseEventResource = true;
		sound.StartSound();

		return resource;
	}
}
