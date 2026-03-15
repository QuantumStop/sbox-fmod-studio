namespace Editor;

using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FMOD;
using FMODSbox;

/// <summary>
/// Editor-only FMOD event cache that can enumerate event paths from bank files
/// even when the runtime FMOD system isn't initialized (i.e. before the game runs).
/// </summary>
public static class FMODEditorEventCache
{
	private static readonly Lock LockObj = new();

	private static IReadOnlyList<string> Cached = [];
	private static DateTime LastBuildUtc = DateTime.MinValue;

	public static IReadOnlyList<string> GetAllEventPaths( bool forceRefresh = false )
	{
		lock ( LockObj )
		{
			if ( !forceRefresh && Cached.Count > 0 )
				return Cached;

			// Basic throttling to avoid repeated heavy work if multiple inspectors open at once.
			if ( !forceRefresh && (DateTime.UtcNow - LastBuildUtc) < TimeSpan.FromSeconds( 2 ) )
				return Cached;

			LastBuildUtc = DateTime.UtcNow;

			try
			{
				Cached = BuildCache();
			}
			catch ( Exception e )
			{
				Log.Warning( $"[FMOD] Editor event cache build failed: {e.Message}" );
				Cached = [];
			}

			return Cached;
		}
	}

	private static IReadOnlyList<string> BuildCache()
	{
		RuntimeUtils.EnforceLibraryOrder();

		var bankFolder = FMODManagerSystem.GetBankFolderLocation();
		if ( string.IsNullOrWhiteSpace( bankFolder ) || !Directory.Exists( bankFolder ) )
			return [];

		var bankFiles = Directory.GetFiles( bankFolder, "*.bank", SearchOption.TopDirectoryOnly )
			.Where( p => !p.EndsWith( ".assets.bank", StringComparison.OrdinalIgnoreCase ) )
			.ToArray();

		if ( bankFiles.Length == 0 )
			return [];

		RESULT result = RESULT.OK;

		result = FMOD.Studio.System.create( out var studioSystem );
		if ( result != RESULT.OK ) return [];

		result = studioSystem.getCoreSystem( out var coreSystem );
		if ( result != RESULT.OK )
		{
			studioSystem.release();
			return [];
		}

		// Use no-sound output so we can run in-editor without needing an audio device.
		coreSystem.setOutput( OUTPUTTYPE.NOSOUND );

		var studioInitFlags = FMOD.Studio.INITFLAGS.NORMAL | FMOD.Studio.INITFLAGS.ALLOW_MISSING_PLUGINS;
		var coreInitFlags = FMOD.INITFLAGS.NORMAL;

		result = studioSystem.initialize( 0, studioInitFlags, coreInitFlags, IntPtr.Zero );
		if ( result != RESULT.OK )
		{
			studioSystem.release();
			return [];
		}

		var loadedBanks = new List<FMOD.Studio.Bank>( bankFiles.Length );

		foreach ( var bankPath in bankFiles )
		{
			if ( studioSystem.loadBankFile( bankPath, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out var bank ) == RESULT.OK && bank.isValid() )
			{
				loadedBanks.Add( bank );
			}
		}

		var unique = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var bank in loadedBanks )
		{
			if ( !bank.isValid() ) continue;

			if ( bank.getEventList( out var eventDescriptions ) != RESULT.OK || eventDescriptions == null )
				continue;

			foreach ( var desc in eventDescriptions )
			{
				if ( !desc.isValid() ) continue;
				if ( desc.getPath( out var path ) != RESULT.OK ) continue;
				if ( string.IsNullOrWhiteSpace( path ) ) continue;
				unique.Add( path.Trim() );
			}
		}

		// Cleanup
		foreach ( var bank in loadedBanks )
		{
			try { if ( bank.isValid() ) bank.unload(); }
			catch { }
		}

		try { studioSystem.release(); } catch { }

		return [];
	}
}

