namespace FMODSbox;

/// <summary>
/// The real entrance point to use FMOD everywhere, instead of referencing the Instance manually
/// </summary>
static public partial class FMODSound
{
	/// <summary>
	/// Set parameter globally, even for events that are not playing
	/// </summary>
	/// <param name="param">Parameter name</param>
	/// <param name="value">What do we set it to</param>
	/// <param name="ignoreseekspeed">Ignore smooth transition (if exists)</param>
	public static void SetParameterGlobal( string param, float value, bool ignoreseekspeed = false ) => FMODManagerSystem.SetParameterGlobal( param, value, ignoreseekspeed );
	/// <summary>
	/// Set parameter globally, even for events that are not playing
	/// </summary>
	/// <param name="param">Parameter name</param>
	/// <param name="value">What do we set it to</param>
	/// <param name="ignoreseekspeed">Ignore smooth transition (if exists)</param>
	public static void SetParameterGlobal( string param, string value, bool ignoreseekspeed = false ) => FMODManagerSystem.SetParameterGlobal( param, value, ignoreseekspeed );
}
