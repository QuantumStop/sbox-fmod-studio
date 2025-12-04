namespace FMODSbox;

/// <summary>
/// The real entrance point to use FMOD everywhere, instead of referencing the Instance manually
/// </summary>
static public partial class FMODSound
{
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
	/// <summary>
	/// Send pause state absolutely ALL events
	/// </summary>
	/// <param name="isPaused"></param>
	static public void SetPauseOnAll( bool isPaused ) => FMODManager.PauseAllEvents( isPaused );
	/// <summary>
	/// Send pause state to a single bus (group)
	/// </summary>
	/// <param name="isPaused"></param>
	/// <param name="bus"></param>
	static public void SetPauseBus( bool isPaused, string bus ) => FMODManager.PauseEventsOnBus( isPaused, bus );
	/// <summary>
	/// Set the volume of a VCA
	/// </summary>
	/// <param name="VCA"></param>
	/// <param name="value"></param>
	static public void SetVCA( string VCA, float value ) => FMODManager.SetVCAVolume( VCA, value );
}
