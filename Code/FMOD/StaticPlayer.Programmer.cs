using FMOD;
using FMOD.Studio;

namespace FMODSbox;

static public partial class FMODSound
{
	/// <summary>
	/// Play an event with programmer instrument in it, send a file to it
	/// </summary>
	/// <param name="path">Full path string of the event</param>
	/// <param name="key">The dialogue key</param>
	/// <param name="callback">The callback (created separately)</param>
	/// <param name="gameObject">GameObject to attach the event to</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public EventInstance Play( string path, string key, GameObject gameObject, EVENT_CALLBACK callback, bool release = true ) => FMODManagerSystem.PlayProgrammerOnObject( path, key, gameObject, callback, release );
	/// <summary>
	/// Play an event with programmer instrument in it, send a file to it
	/// </summary>
	/// <param name="path">Full path string of the event</param>
	/// <param name="key">The dialogue key</param>
	/// <param name="callback">The callback (created separately)</param>
	/// <param name="pos">GameObject to attach the event to</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public EventInstance Play( string path, string key, Vector3 pos, EVENT_CALLBACK callback, bool release = true ) => FMODManagerSystem.PlayProgrammerOnce( path, key, callback, pos, release );
}
