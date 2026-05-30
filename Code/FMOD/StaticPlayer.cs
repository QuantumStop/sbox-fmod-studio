using FMOD;
using FMOD.Studio;

namespace FMODSbox;

/// <summary>
/// The real entrance point to use FMOD everywhere, instead of referencing the Instance manually
/// </summary>
static public partial class FMODSound
{
	/// <summary>
	/// Free the event from memory by releasing it (if it wasn't automatically).
	/// </summary>
	/// <param name="instance"></param>
	static public void Release( EventInstance instance ) => instance.release();
	/// <summary>
	/// Create an event instance to be played later
	/// </summary>
	/// <param name="path">Full path string of the event</param>
	static public EventInstance Create( string path ) => FMODManagerSystem.CreateInstance( path );
	/// <summary>
	/// Play an already existing FMOD event instance the easy way, without specified position (for 2D sounds).
	/// </summary>
	/// <param name="instance">The created event instance of the event</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public EventInstance Play( EventInstance instance, bool release = true ) => FMODManagerSystem.PlayCreated( instance, Vector3.Zero, release );
	/// <summary>
	/// Play an already existing FMOD event instance the easy way, without specified position (for 2D sounds).
	/// </summary>
	/// <param name="instance">The created event instance of the event</param>
	/// <param name="pos">Static position of the event</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public EventInstance Play( EventInstance instance, Vector3 pos, bool release = true ) => FMODManagerSystem.PlayCreated( instance, pos, release );
	/// <summary>
	/// Play an FMOD sound the easy way, without specified position (for 2D sounds).
	/// </summary>
	/// <param name="path">Full path string of the event</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public EventInstance Play( string path, bool release = true ) => FMODManagerSystem.PlayOnce( path, Vector3.Zero, release );
	/// <summary>
	/// Play an FMOD sound the easy way. 
	/// </summary>
	/// <param name="path">Full path string of the event</param>
	/// <param name="pos">Static position of the event</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public EventInstance Play( string path, Vector3 pos, bool release = true ) => FMODManagerSystem.PlayOnce( path, pos, release );
	/// <summary>
	/// Play the sound and attach it to the game object (or the rigidbody)
	/// </summary>
	/// <param name="path">Full path string of the event</param>
	/// <param name="gameObject">GameObject to attach the event to</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public EventInstance Play( string path, GameObject gameObject, bool release = true ) => FMODManagerSystem.PlayOnObject( path, gameObject, release );
	/// <summary>
	/// Play the event instance and attach it to the game object (or the rigidbody)
	/// </summary>
	/// <param name="instance">The created event instance of the event</param>
	/// <param name="gameObject">GameObject to attach the event to</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public EventInstance Play( EventInstance instance, GameObject gameObject, bool release = true ) => FMODManagerSystem.PlayOnObject( instance, gameObject, release );
	/// <summary>
	/// Shortcut to play a null sound so theres something to return in case of null or default. You should probably add this to your project.
	/// </summary>
	/// <returns>1ms of silence</returns>
	static public EventInstance Null() => Play( "event:/null" );
	/// <summary>
	/// Force stop this event, you have to release it if you haven't.
	/// </summary>
	/// <param name="Instance">What do we pause</param>
	/// <param name="AllowFadeOut">Do we fade out if the event has AHDSR configured</param>
	static public void Stop( EventInstance Instance, bool AllowFadeOut = true ) => Instance.stop( AllowFadeOut ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE );
	/// <summary>
	/// Pause/Unpause the event
	/// </summary>
	/// <param name="Instance">What do we commit pausing on</param>
	/// <param name="pause">Do we pause or unpause</param>
	static public void SetPause( EventInstance Instance, bool pause ) => Instance.setPaused( pause );
}
