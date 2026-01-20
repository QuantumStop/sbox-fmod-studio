using FMOD;
using FMOD.Studio;
using Sandbox;
using System;

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
	/// Play an FMOD sound the easy way. 
	/// </summary>
	/// <param name="guid">GUID of the event</param>
	/// <param name="pos">Static position of the event</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public EventInstance Play( GUID guid, Vector3 pos = default, bool release = true ) => FMODManagerSystem.PlayOnce( guid, pos, release );
	/// <summary>
	/// Play an FMOD sound the easy way. 
	/// </summary>
	/// <param name="path">Full path string of the event</param>
	/// <param name="pos">Static position of the event</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public EventInstance Play( string path, Vector3 pos = default, bool release = true ) => FMODManagerSystem.PlayOnce( path, pos, release );
	/// <summary>
	/// Play the sound and attach it to the game object (or the rigidbody)
	/// </summary>
	/// <param name="path"></param>
	/// <param name="gameObject"></param>
	/// <param name="release"></param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public EventInstance Play( string path, GameObject gameObject, bool release = true ) => FMODManagerSystem.PlayOnObject( path, gameObject, release );
	/// <summary>
	/// Shortcut to play a null sound so theres something to return in case of null or default
	/// </summary>
	/// <returns>1ms of silence</returns>
	static public EventInstance Null() => Play( "event:/null" );
}
