using System;
using System.Linq;

namespace FMODSbox;

public partial class FMODManager
{
	public static List<StudioListener> Listeners = [];
	public static int ListenerCount { get => Listeners.Count; }

	public static float DistanceToNearestListener( Vector3 position )
	{
		float result = float.MaxValue;

		for ( int i = 0; i < Listeners.Count; i++ )
		{
			result = MathF.Min( result, position.Distance( Listeners[i].AttenuationObject == null ? Listeners[i].WorldTransform.Position : Listeners[i].AttenuationObject.WorldTransform.Position ) );
		}
		return result;
	}

	public static float DistanceSquaredToNearestListener( Vector3 position )
	{
		float result = float.MaxValue;
		for ( int i = 0; i < Listeners.Count; i++ )
		{
			result = MathF.Min( result, (position - (Listeners[i].AttenuationObject == null ? Listeners[i].WorldTransform.Position : Listeners[i].AttenuationObject.WorldTransform.Position)).LengthSquared );
		}

		return result;
	}

	public static void AddListener( StudioListener listener )
	{
		// Is the listener already in the list?
		if ( Listeners.Contains( listener ) )
		{
			Log.Warning( string.Format( ("[FMOD] Listener has already been added at index {0}."), listener.ListenerNumber ) );
			return;
		}

		// If already at the max numListeners
		if ( Listeners.Count >= FMOD.CONSTANTS.MAX_LISTENERS )
		{
			Log.Warning( string.Format( ("[FMOD] Max number of Listeners reached : {0}."), FMOD.CONSTANTS.MAX_LISTENERS ) );
		}

		Listeners.Add( listener );
		StudioSystem.setNumListeners( Math.Clamp( Listeners.Count, 1, FMOD.CONSTANTS.MAX_LISTENERS ) );
	}

	public static void RemoveListener( StudioListener listener )
	{
		Listeners.Remove( listener );
		StudioSystem.setNumListeners( Math.Clamp( Listeners.Count, 1, FMOD.CONSTANTS.MAX_LISTENERS ) );
	}
}
