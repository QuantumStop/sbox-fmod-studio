using FMOD;
using FMOD.Studio;

namespace FMODSbox;

/// <summary>
/// The real entrance point to use FMOD everywhere, instead of referencing the Instance manually
/// </summary>
static public partial class FMODSound
{
	/// <summary>
	/// Is this event currently playing?
	/// </summary>
	/// <param name="instance">What do we check</param>
	/// <returns>Yes/No</returns>
	public static bool IsPlaying( EventInstance instance )
	{
		if ( !instance.isValid() ) return false;

		instance.getPlaybackState( out var state );
		return state == PLAYBACK_STATE.PLAYING;
	}
	/// <summary>
	/// Force stop all events curently playing, cancel
	/// </summary>
	/// <param name="allowfadeout"></param>
	public static void StopAllEvents( bool allowfadeout = true ) => FMODManagerSystem.StopAllEvents( allowfadeout );
}
