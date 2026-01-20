namespace FMODSbox;

public partial class FMODManagerSystem
{
	public static void SetListenerLocation( int listenerIndex, GameObject gameObject, GameObject attenuationObject = null, Vector3 velocity = new Vector3() )
	{
		if ( attenuationObject.IsValid() )
			Current.studioSystem.setListenerAttributes( listenerIndex, RuntimeUtils.To3DAttributes( gameObject.Transform.World, velocity ), RuntimeUtils.SourceToFMODVector( attenuationObject.WorldPosition ) );
		else
			Current.studioSystem.setListenerAttributes( listenerIndex, RuntimeUtils.To3DAttributes( gameObject.Transform.World, velocity ) );
	}

	public static void SetListenerLocation( GameObject gameObject, GameObject attenuationObject = null )
	{
		SetListenerLocation( 0, gameObject, attenuationObject );
	}

	public static void SetListenerLocation( int listenerIndex, GameObject gameObject, GameObject attenuationObject = null )
	{
		if ( attenuationObject.IsValid() )
			Current.studioSystem.setListenerAttributes( listenerIndex, RuntimeUtils.To3DAttributes( gameObject.WorldPosition ), RuntimeUtils.SourceToFMODVector( attenuationObject.WorldPosition ) );
		else
			Current.studioSystem.setListenerAttributes( listenerIndex, RuntimeUtils.To3DAttributes( gameObject.WorldPosition ) );
	}

	public static void SetListenerLocation( GameObject gameObject, Rigidbody rigidBody, GameObject attenuationObject = null )
	{
		SetListenerLocation( 0, gameObject, rigidBody, attenuationObject );
	}

	public static void SetListenerLocation( int listenerIndex, GameObject gameObject, Rigidbody rigidBody, GameObject attenuationObject = null )
	{
		if ( attenuationObject.IsValid() )
			Current.studioSystem.setListenerAttributes( listenerIndex, RuntimeUtils.To3DAttributes( gameObject.WorldTransform, rigidBody.Velocity ), RuntimeUtils.SourceToFMODVector( attenuationObject.WorldTransform.Position ) );
		else
			Current.studioSystem.setListenerAttributes( listenerIndex, RuntimeUtils.To3DAttributes( gameObject.WorldTransform, rigidBody.Velocity ) );
	}
}
