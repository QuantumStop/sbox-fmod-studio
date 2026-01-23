using System;
using System.IO;

namespace FMODSbox;

public partial class FMODManagerSystem : GameObjectSystem<FMODManagerSystem>
{
	/// <summary>
	/// The list of plugins to load
	/// </summary>
	[Property] public List<string> Plugins { get; set; } = ["phonon_fmod.dll"]; // hardcode steam audio for now, but otherwise it should be definable as soon as we have a widget

	/// <summary>
	/// Load Dynamic plugins, platforms that want Static plugins are niche even by Unreal standards, so we don't care about statics. 
	/// </summary>
	/// <param name="coreSystem">FMOD Core system</param>
	/// <param name="reportResult">Result of the plugin linking</param>
	private void LoadPlugins( FMOD.System coreSystem, Action<FMOD.RESULT, string> reportResult )
	{
		List<string> pluginNames = Plugins;

		if ( pluginNames == null )
			return;

		foreach ( string pluginName in pluginNames )
		{
			if ( string.IsNullOrEmpty( pluginName ) )
				continue;

			string pluginPath = GetPluginPath( CheckForDll( pluginName ) );

			FMOD.RESULT result = coreSystem.loadPlugin( pluginPath, out uint handle );

			// Add a "64" suffix and try again
			if ( result == FMOD.RESULT.ERR_FILE_BAD || result == FMOD.RESULT.ERR_FILE_NOTFOUND )
			{
				string pluginPath64 = GetPluginPath( CheckForDll( pluginName + "64" ) );
				result = coreSystem.loadPlugin( pluginPath64, out handle );
			}

			reportResult( result, string.Format( "Loading plugin '{0}' from '{1}'", pluginName, pluginPath ) );
		}
	}

	/// <summary>
	/// Sanitize input by appending .dll in case it's not there
	/// </summary>
	/// <param name="name">The string</param>
	/// <returns>Modified (or unmodified) string</returns>
	private static string CheckForDll( string name )
	{
		var adjust = name;

		if ( !name.EndsWith( ".dll" ) )
			adjust = $"{name}.dll";

		return adjust;
	}

	/// <summary>
	/// The dlls are stored in the balls
	/// </summary>
	/// <param name="name">Name of dll we are looking for</param>
	/// <returns>Full path</returns>
	private static string GetPluginPath( string name ) => Path.GetFullPath( $"bin/thirdparty/{name}" );
}
