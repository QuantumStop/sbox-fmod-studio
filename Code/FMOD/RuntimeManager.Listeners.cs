using System;

namespace FMODSbox;

public partial class FMODManagerSystem
{
	public List<StudioListener> Listeners = [];

	public static float DistanceToNearestListener( Vector3 position )
	{
		float result = float.MaxValue;

		foreach ( var listener in Current.Listeners )
		{
			result = MathF.Min( result, position.Distance( !listener.AttenuationObject.IsValid() ? listener.WorldTransform.Position : listener.AttenuationObject.WorldTransform.Position ) );
		}
		return result;
	}

	public static float DistanceSquaredToNearestListener( Vector3 position )
	{
		float result = float.MaxValue;

		foreach ( var listener in Current.Listeners )
		{
			result = MathF.Min( result, (position - (!listener.AttenuationObject.IsValid() ? listener.WorldTransform.Position : listener.AttenuationObject.WorldTransform.Position)).LengthSquared );
		}

		return result;
	}

	public static void AddListener( StudioListener listener )
	{
		// Is the listener already in the list?
		if ( Current.Listeners.Contains( listener ) )
		{
			Log.Warning( string.Format( "[FMOD] Listener has already been added at index {0}.", listener.ListenerNumber ) );
			return;
		}

		// If already at the max numListeners
		if ( Current.Listeners.Count >= FMOD.CONSTANTS.MAX_LISTENERS )
		{
			Log.Warning( string.Format( "[FMOD] Max number of Listeners reached : {0}.", FMOD.CONSTANTS.MAX_LISTENERS ) );
		}

		Current.Listeners.Add( listener );
		StudioSystem.setNumListeners( Math.Clamp( Current.Listeners.Count, 1, FMOD.CONSTANTS.MAX_LISTENERS ) );
	}

	public static void RemoveListener( StudioListener listener )
	{
		Current.Listeners.Remove( listener );
		StudioSystem.setNumListeners( Math.Clamp( Current.Listeners.Count, 1, FMOD.CONSTANTS.MAX_LISTENERS ) );
	}

	private void SpawnListenerOnCamera()
	{
		if ( !Scene.IsEditor )
		{
			var listener = Scene.Get<StudioListener>();

			if ( !listener.IsValid() )
			{
				var listen = Scene.Camera?.Components.Create<StudioListener>();
				listen?.NonRigidbodyVelocity = true;
			}
			else
			{
				listener?.NonRigidbodyVelocity = true;
			}
		}
	}
}
