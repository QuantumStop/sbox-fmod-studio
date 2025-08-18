namespace FMODSbox;
public partial class FMODManager
{
	public static void SetListenerLocation( int listenerIndex, GameObject gameObject, GameObject attenuationObject = null, Vector3 velocity = new Vector3() )
	{
		if ( attenuationObject.IsValid() )
		{
			Instance.studioSystem.setListenerAttributes( listenerIndex, RuntimeUtils.To3DAttributes( gameObject.Transform.World, velocity ), RuntimeUtils.ToFMODVector( attenuationObject.WorldPosition ) );
		}
		else
		{
			Instance.studioSystem.setListenerAttributes( listenerIndex, RuntimeUtils.To3DAttributes( gameObject.Transform.World, velocity ) );
		}
	}

	public static void SetListenerLocation( GameObject gameObject, GameObject attenuationObject = null )
	{
		SetListenerLocation( 0, gameObject, attenuationObject );
	}

	public static void SetListenerLocation( int listenerIndex, GameObject gameObject, GameObject attenuationObject = null )
	{
		if ( attenuationObject.IsValid() )
		{
			Instance.studioSystem.setListenerAttributes( listenerIndex, RuntimeUtils.To3DAttributes( gameObject.WorldPosition ), RuntimeUtils.ToFMODVector( attenuationObject.WorldPosition ) );
		}
		else
		{
			Instance.studioSystem.setListenerAttributes( listenerIndex, RuntimeUtils.To3DAttributes( gameObject.WorldPosition ) );
		}
	}
}
