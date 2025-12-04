using FMOD;
using Sandbox;
using System;

namespace FMODSbox;

/// <summary>
/// The real entrance point to use FMOD everywhere, instead of referencing the Instance manually
/// </summary>
static public class FMODSound
{
	/// <summary>
	/// Free the event from memory by releasing it (if it wasn't automatically).
	/// </summary>
	/// <param name="instance"></param>
	static public void Release( FMOD.Studio.EventInstance instance ) => instance.release();
	/// <summary>
	/// Play an FMOD sound the easy way. 
	/// </summary>
	/// <param name="guid">GUID of the event</param>
	/// <param name="pos">Static position of the event</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public FMOD.Studio.EventInstance Play( GUID guid, Vector3 pos = default, bool release = true ) => FMODManager.PlayOnce( guid, pos, release );
	/// <summary>
	/// Play an FMOD sound the easy way. 
	/// </summary>
	/// <param name="path">Full path string of the event</param>
	/// <param name="pos">Static position of the event</param>
	/// <param name="release">Should the instance be released?</param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public FMOD.Studio.EventInstance Play( string path, Vector3 pos = default, bool release = true ) => FMODManager.PlayOnce( path, pos, release );
	/// <summary>
	/// Play the sound and attach it to the game object (or the rigidbody)
	/// </summary>
	/// <param name="path"></param>
	/// <param name="gameObject"></param>
	/// <param name="release"></param>
	/// <returns>The EventInstance, which shouldnt be used if the sound was released</returns>
	static public FMOD.Studio.EventInstance Play( string path, GameObject gameObject, bool release = true ) => FMODManager.PlayOnObject( path, gameObject, release );

	/// <summary>
	/// Set parameter on a given EventInstance
	/// </summary>
	/// <param name="instance">The affected EventInstance</param>
	/// <param name="param">Parameter name</param>
	/// <param name="value">What do we set it to</param>
	/// <param name="ignoreseekspeed">Ignore smooth transition (if exists)</param>
	static public void SetParameter( FMOD.Studio.EventInstance instance, string param, float value, bool ignoreseekspeed = false ) => FMODManager.SetParameter( instance, param, value, ignoreseekspeed );
	/// <summary>
	/// Set parameter on a given EventInstance
	/// </summary>
	/// <param name="instance">The affected EventInstance</param>
	/// <param name="param">Parameter name</param>
	/// <param name="value">What do we set it to</param>
	/// <param name="ignoreseekspeed">Ignore smooth transition (if exists)</param>
	static public void SetParameter( FMOD.Studio.EventInstance instance, string param, string value, bool ignoreseekspeed = false ) => FMODManager.SetParameter( instance, param, value, ignoreseekspeed );
	/// <summary>
	/// Set parameter on a given EventInstance
	/// </summary>
	/// <param name="instance">The affected EventInstance</param>
	/// <param name="param">Parameter shortcut, has both</param>
	/// <param name="ignoreseekspeed">Ignore smooth transition (if exists)</param>
	static public void SetParameter( FMOD.Studio.EventInstance instance, ParamFloat param, bool ignoreseekspeed = false ) => FMODManager.SetParameter( instance, param.ParameterName, param.Value, ignoreseekspeed );
	/// <summary>
	/// Set parameter on a given EventInstance
	/// </summary>
	/// <param name="instance">The affected EventInstance</param>
	/// <param name="param">Parameter shortcut, has both</param>
	/// <param name="ignoreseekspeed">Ignore smooth transition (if exists)</param>
	static public void SetParameter( FMOD.Studio.EventInstance instance, ParamLabel param, bool ignoreseekspeed = false ) => FMODManager.SetParameter( instance, param.ParameterName, param.Value, ignoreseekspeed );

}
