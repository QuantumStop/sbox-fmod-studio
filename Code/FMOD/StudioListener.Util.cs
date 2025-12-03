using System;

namespace FMODSbox;

public partial class StudioListener : Component
{
	public static float DistanceToNearestListener( Vector3 position )
	{
		float result = float.MaxValue;

		for ( int i = 0; i < listeners.Count; i++ )
		{
			result = MathF.Min( result, position.Distance( listeners[i].AttenuationObject == null ? listeners[i].WorldTransform.Position : listeners[i].AttenuationObject.WorldTransform.Position ) );
		}
		return result;
	}

	public static float DistanceSquaredToNearestListener( Vector3 position )
	{
		float result = float.MaxValue;
		for ( int i = 0; i < listeners.Count; i++ )
		{
			result = MathF.Min( result, (position - (listeners[i].AttenuationObject == null ? listeners[i].WorldTransform.Position : listeners[i].AttenuationObject.WorldTransform.Position)).LengthSquared );
		}

		return result;
	}

	private static void AddListener( StudioListener listener )
	{
		// Is the listener already in the list?
		if ( listeners.Contains( listener ) )
		{
			Log.Warning( string.Format( ("[FMOD] Listener has already been added at index {0}."), listener.ListenerNumber ) );
			return;
		}

		// If already at the max numListeners
		if ( listeners.Count >= FMOD.CONSTANTS.MAX_LISTENERS )
		{
			Log.Warning( string.Format( ("[FMOD] Max number of listeners reached : {0}."), FMOD.CONSTANTS.MAX_LISTENERS ) );
		}

		listeners.Add( listener );
		FMODManager.StudioSystem.setNumListeners( Math.Clamp( listeners.Count, 1, FMOD.CONSTANTS.MAX_LISTENERS ) );
	}

	private static void RemoveListener( StudioListener listener )
	{
		listeners.Remove( listener );
		FMODManager.StudioSystem.setNumListeners( Math.Clamp( listeners.Count, 1, FMOD.CONSTANTS.MAX_LISTENERS ) );
	}
}
