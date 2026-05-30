using FMOD;
using FMOD.Studio;

namespace FMODSbox;

/// <summary>
/// The real entrance point to use FMOD everywhere, instead of referencing the Instance manually
/// </summary>
static public partial class FMODSound
{
	/// <summary>
	/// Create an event instance to be played later
	/// </summary>
	/// <param name="guid">GUID of the event</param>
	static public EventInstance Create( GUID guid ) => FMODManagerSystem.CreateInstance( guid );
	/// <summary>
	/// Play an FMOD sound the easy way, without specified position (for 2D sounds).
	/// </summary>
	/// <param name="guid">GUID of the event</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public EventInstance Play( GUID guid, bool release = true ) => FMODManagerSystem.PlayOnce( guid, Vector3.Zero, release );

	/// <summary>
	/// Play an FMOD sound the easy way. 
	/// </summary>
	/// <param name="guid">GUID of the event</param>
	/// <param name="pos">Static position of the event</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public EventInstance Play( GUID guid, Vector3 pos, bool release = true ) => FMODManagerSystem.PlayOnce( guid, pos, release );

	/// <summary>
	/// Play the sound and attach it to the game object (or the rigidbody)
	/// </summary>
	/// <param name="guid">GUID of the event</param>
	/// <param name="gameObject">GameObject to attach the event to</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public EventInstance Play( GUID guid, GameObject gameObject, bool release = true ) => FMODManagerSystem.PlayOnObject( guid, gameObject, release );
}
