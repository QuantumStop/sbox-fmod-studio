namespace Editor;

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using FMOD;
using FMOD.Studio;
using FMODSbox;
using System.Threading;

public static class FMODEventResourceGenerator
{
	private const int ResourceVersion = 1;
	private const int GeneratorSchemaVersion = 2;
	private const string RootFolderName = "_fmod_project";
	private const string MarkerFileName = ".fmod_event_cache.json";

	private static readonly Lock LockObj = new();
	private static DateTime LastCheckUtc = DateTime.MinValue;
	private static DateTime LastFolderMetadataUtc = DateTime.MinValue;

	private static bool _assetsRegisteredThisSession;
	private static bool _typeReady;

	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	public static string GetGeneratedRoot()
	{
		return Path.Combine( Project.Current.GetAssetsPath(), RootFolderName, Project.Current.Config.Ident );
	}

	public static string GetGeneratedEventsRoot()
	{
		return Path.Combine( GetGeneratedRoot(), "events" );
	}

	public static void EnsureGenerated( bool force = false )
	{
		lock ( LockObj )
		{
			if ( !_typeReady )
			{
				if ( AssetType.FromExtension( "fmevent" ) is null )
					return;

				_typeReady = true;
			}

			if ( !force && (DateTime.UtcNow - LastCheckUtc) < TimeSpan.FromSeconds( 2 ) )
				return;

			LastCheckUtc = DateTime.UtcNow;

			var bankFolder = FMODManagerSystem.GetBankFolderLocation();
			if ( string.IsNullOrWhiteSpace( bankFolder ) || !Directory.Exists( bankFolder ) )
				return;

			var bankFiles = Directory.GetFiles( bankFolder, "*.bank", SearchOption.TopDirectoryOnly )
				.Where( p => !p.EndsWith( ".assets.bank", StringComparison.OrdinalIgnoreCase ) )
				.ToArray();

			if ( bankFiles.Length == 0 )
				return;

			var newestBankWriteUtc = bankFiles
				.Select( File.GetLastWriteTimeUtc )
				.Max();

			var markerPath = Path.Combine( GetGeneratedRoot(), MarkerFileName );

			if ( !force && TryReadMarker( markerPath, out var cachedWriteUtc, out var cachedBankCount, out var cachedFiles )
			   && cachedWriteUtc == newestBankWriteUtc
			   && cachedBankCount == bankFiles.Length )
			{
				if ( !_assetsRegisteredThisSession )
				{
					EnsureAssetsRegistered( cachedFiles );
					_assetsRegisteredThisSession = true;
				}

				EnsureFolderMetadata();
				return;
			}

			// Banks changed or no valid marker, means full rebuild.
			_assetsRegisteredThisSession = false;
			GenerateAll( bankFiles, newestBankWriteUtc );
			EnsureFolderMetadata();
		}
	}

	/// <summary>
	/// Walks existing .fmevent files and registers any that AssetSystem doesn't
	/// know about yet. Called on cache-hit path.
	/// </summary>
	private static void EnsureAssetsRegistered( string[] paths )
	{
		foreach ( var path in paths )
		{
			if ( AssetSystem.FindByPath( path ) is null )
				AssetSystem.RegisterFile( path );
		}
}

	/// <summary>
	/// Ensure generated FMOD directories get a distinct folder color in folder view.
	/// </summary>
	public static void EnsureFolderMetadata()
	{
		if ( (DateTime.UtcNow - LastFolderMetadataUtc) < TimeSpan.FromSeconds( 15 ) )
			return;

		LastFolderMetadataUtc = DateTime.UtcNow;
		TryApplyFolderMetadata( GetGeneratedEventsRoot() );
	}

	private static bool TryReadMarker( string markerPath, out DateTime newestBankWriteUtc, out int bankCount, out string[] generatedFiles )
	{
		newestBankWriteUtc = DateTime.MinValue;
		bankCount = 0;
		generatedFiles = [];

		try
		{
			if ( !File.Exists( markerPath ) )
				return false;

			using var doc = JsonDocument.Parse( File.ReadAllText( markerPath ) );
			var root = doc.RootElement;

			if ( !root.TryGetProperty( "SchemaVersion", out var schemaProp )
				|| schemaProp.ValueKind != JsonValueKind.Number
				|| schemaProp.GetInt32() != GeneratorSchemaVersion )
				return false;

			if ( !root.TryGetProperty( "NewestBankWriteUtc", out var timeProp )
				|| timeProp.ValueKind != JsonValueKind.String
				|| !DateTime.TryParse( timeProp.GetString(), null,
					global::System.Globalization.DateTimeStyles.RoundtripKind, out var dt ) )
				return false;

			newestBankWriteUtc = dt;

			if ( root.TryGetProperty( "BankCount", out var countProp )
				&& countProp.ValueKind == JsonValueKind.Number )
				bankCount = countProp.GetInt32();

			if ( root.TryGetProperty( "GeneratedFiles", out var filesProp )
				&& filesProp.ValueKind == JsonValueKind.Array )
			{
				generatedFiles = [.. filesProp.EnumerateArray()
					.Where( x => x.ValueKind == JsonValueKind.String )
					.Select( x => x.GetString() )
					.Where( x => !string.IsNullOrWhiteSpace( x ) )];
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	private static void WriteMarker( string markerPath, DateTime newestBankWriteUtc, int bankCount, IEnumerable<string> generatedFiles )
	{
		Directory.CreateDirectory( Path.GetDirectoryName( markerPath )! );

		var payload = new Dictionary<string, object>
		{
			["SchemaVersion"] = GeneratorSchemaVersion,
			["NewestBankWriteUtc"] = newestBankWriteUtc.ToString( "O" ),
			["BankCount"] = bankCount,
			["GeneratedUtc"] = DateTime.UtcNow.ToString( "O" ),
			["GeneratedFiles"] = generatedFiles
		};

		File.WriteAllText( markerPath, JsonSerializer.Serialize( payload, JsonOptions ) );
	}

	private static void GenerateAll( string[] bankFiles, DateTime newestBankWriteUtc )
	{
		var root = GetGeneratedRoot();
		var eventsRoot = GetGeneratedEventsRoot();

		TryMarkHidden( Path.Combine( Project.Current.GetAssetsPath(), RootFolderName ) );
		TryMarkHidden( root );

		Directory.CreateDirectory( eventsRoot );

		var eventData = BuildEventMetadataFromBanks( bankFiles );
		var desiredFiles = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var (eventPath, data) in eventData )
		{
			var relative = EventPathToRelativeFile( eventPath );
			if ( string.IsNullOrWhiteSpace( relative ) )
				continue;

			var absPath = Path.Combine( eventsRoot, relative );
			Directory.CreateDirectory( Path.GetDirectoryName( absPath )! );

			var json = JsonSerializer.Serialize( data, JsonOptions );;

			var write = true;
			if ( File.Exists( absPath ) )
			{
				try { write = !string.Equals( File.ReadAllText( absPath ), json, StringComparison.Ordinal ); }
				catch { }
			}

			if ( write )
				File.WriteAllText( absPath, json );

			// Always register — either newly written or already on disk from a
			// previous session that the asset system hasn't indexed yet.
			if ( AssetSystem.FindByPath( absPath ) is null )
				AssetSystem.RegisterFile( absPath );

			desiredFiles.Add( absPath );
		}

		// Remove stale generated resources.
		foreach ( var existing in Directory.GetFiles( eventsRoot, "*.fmevent", SearchOption.AllDirectories ) )
		{
			if ( !desiredFiles.Contains( existing ) )
				try { File.Delete( existing ); } catch { }
		}

		var relativeFiles = desiredFiles
			.Select( p => Path.GetRelativePath( eventsRoot, p ) )
			.ToArray();

		WriteMarker( Path.Combine( root, MarkerFileName ), newestBankWriteUtc, bankFiles.Length, relativeFiles );
	}

	private sealed class FolderMetadataProxy
	{
		public Color Color { get; set; }
		public string Icon { get; set; } = "";
	}

	private static void TryApplyFolderMetadata( string eventsRoot )
	{
		try
		{
			if ( string.IsNullOrWhiteSpace( eventsRoot ) || !Directory.Exists( eventsRoot ) )
				return;

			const string metadataPath = "Directory.metadata";

			var loaded = FileSystem.ProjectSettings.ReadJsonOrDefault<IEnumerable<KeyValuePair<string, FolderMetadataProxy>>>( metadataPath );
			var dict = loaded?.ToDictionary( x => x.Key, x => x.Value ) ?? [];
			var purple = Color.Parse( "#8d368d" )!.Value;
			var projectRoot = Project.Current.GetRootPath();

			var dirty = false;
			foreach ( var dir in Directory.GetDirectories( eventsRoot, "*", SearchOption.AllDirectories ) )
			{
				var rel = Path.GetRelativePath( projectRoot, dir );
				if ( string.IsNullOrWhiteSpace( rel ) )
					continue;

				if ( !dict.TryGetValue( rel, out var meta ) || meta is null )
				{
					meta = new FolderMetadataProxy();
					dict[rel] = meta;
					dirty = true;
				}

				if ( meta.Color != purple )
				{
					meta.Color = purple;
					dirty = true;
				}
			}

			if ( dirty )
			{
				FileSystem.ProjectSettings.WriteJson(
					metadataPath,
					dict.Select( x => new KeyValuePair<string, FolderMetadataProxy>( x.Key, x.Value ) )
				);
				TryInvalidateDirectoryMetadataCache();
			}
		}
		catch
		{
		}
	}

	private static void TryInvalidateDirectoryMetadataCache()
	{
		try
		{
			var field = typeof( DirectoryEntry ).GetField( "AllMetadata", BindingFlags.Static | BindingFlags.NonPublic );
			field?.SetValue( null, null );
		}
		catch
		{
		}
	}

	private static void TryMarkHidden( string dir )
	{
		try
		{
			if ( string.IsNullOrWhiteSpace( dir ) || !Directory.Exists( dir ) )
				return;

			var attrs = File.GetAttributes( dir );
			if ( !attrs.HasFlag( FileAttributes.Hidden ) )
				File.SetAttributes( dir, attrs | FileAttributes.Hidden );
		}
		catch
		{
		}
	}

	private static string EventPathToRelativeFile( string eventPath )
	{
		if ( string.IsNullOrWhiteSpace( eventPath ) ) return null;

		var path = eventPath.Trim();
		if ( path.StartsWith( "event:/", StringComparison.OrdinalIgnoreCase ) )
			path = path["event:/".Length..];

		path = path.Trim( '/', '\\' );
		if ( string.IsNullOrWhiteSpace( path ) ) return null;

		// Split to folders + filename
		var parts = path.Split( ['/', '\\'], StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length == 0 ) return null;

		// Sanitize
		for ( int i = 0; i < parts.Length; i++ )
		{
			parts[i] = SanitizeFilePart( parts[i] );
			if ( string.IsNullOrWhiteSpace( parts[i] ) )
				parts[i] = "_";
		}

		var rel = Path.Combine( parts );
		return rel + ".fmevent";
	}

	private static string SanitizeFilePart( string value )
	{
		var invalid = Path.GetInvalidFileNameChars();
		var chars = value.ToCharArray();
		for ( int i = 0; i < chars.Length; i++ )
		{
			if ( invalid.Contains( chars[i] ) )
				chars[i] = '_';
		}
		return new string( chars );
	}

	private static Dictionary<string, Dictionary<string, object>> BuildEventMetadataFromBanks( string[] bankFiles )
	{
		RuntimeUtils.EnforceLibraryOrder();

		var unique = new Dictionary<string, Dictionary<string, object>>( StringComparer.OrdinalIgnoreCase );

		if ( FMOD.Studio.System.create( out var studioSystem ) != RESULT.OK )
			return unique;

		if ( studioSystem.getCoreSystem( out var coreSystem ) != RESULT.OK )
		{
			studioSystem.release();
			return unique;
		}

		coreSystem.setOutput( OUTPUTTYPE.NOSOUND );

		var studioInitFlags = FMOD.Studio.INITFLAGS.NORMAL | FMOD.Studio.INITFLAGS.ALLOW_MISSING_PLUGINS;
		var coreInitFlags = FMOD.INITFLAGS.NORMAL;
		if ( studioSystem.initialize( 0, studioInitFlags, coreInitFlags, IntPtr.Zero ) != RESULT.OK )
		{
			studioSystem.release();
			return unique;
		}

		var banks = new List<Bank>( bankFiles.Length );
		foreach ( var bankPath in bankFiles )
		{
			if ( studioSystem.loadBankFile( bankPath, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out var bank ) == RESULT.OK && bank.isValid() )
				banks.Add( bank );
		}

		foreach ( var bank in banks )
		{
			if ( !bank.isValid() ) continue;
			if ( bank.getEventList( out var events ) != RESULT.OK || events == null ) continue;

			foreach ( var desc in events )
			{
				if ( !desc.isValid() ) continue;
				if ( desc.getPath( out var path ) != RESULT.OK ) continue;
				if ( string.IsNullOrWhiteSpace( path ) ) continue;

				path = path.Trim();
				if ( unique.ContainsKey( path ) ) continue;

				var payload = BuildEventPayload( desc, path );
				unique[path] = payload;
			}
		}

		foreach ( var bank in banks )
		{
			try { if ( bank.isValid() ) bank.unload(); }
			catch { }
		}

		try { studioSystem.release(); } catch { }

		return unique;
	}

	private static Dictionary<string, object> BuildEventPayload( EventDescription desc, string eventPath )
	{
		var payload = new Dictionary<string, object>
		{
			["EventPath"] = eventPath,
			["Guid"] = string.Empty,
			["LengthMs"] = 0,
			["Is3D"] = false,
			["MinDistance"] = 0f,
			["MaxDistance"] = 0f,
			["Parameters"] = new List<Dictionary<string, object>>(),
			["__references"] = Array.Empty<object>(),
			["__version"] = ResourceVersion
		};

		if ( desc.getID( out var guid ) == RESULT.OK )
			payload["Guid"] = guid.ToString();

		if ( desc.getLength( out var lengthMs ) == RESULT.OK )
			payload["LengthMs"] = lengthMs;

		if ( desc.is3D( out var is3d ) == RESULT.OK )
			payload["Is3D"] = is3d;

		if ( is3d && desc.getMinMaxDistance( out var min, out var max ) == RESULT.OK )
		{
			payload["MinDistance"] = min;
			payload["MaxDistance"] = max;
		}

		if ( desc.getParameterDescriptionCount( out var paramCount ) == RESULT.OK && paramCount > 0 )
		{
			var list = (List<Dictionary<string, object>>)payload["Parameters"];

			for ( int i = 0; i < paramCount; i++ )
			{
				if ( desc.getParameterDescriptionByIndex( i, out var p ) != RESULT.OK )
					continue;

				var flags = p.flags;
				list.Add( new Dictionary<string, object>
				{
					["Name"] = (string)p.name,
					["Type"] = p.type.ToString(),
					["Min"] = p.minimum,
					["Max"] = p.maximum,
					["Default"] = p.defaultvalue,
					["IsGlobal"] = flags.HasFlag( PARAMETER_FLAGS.GLOBAL ),
					["IsReadOnly"] = flags.HasFlag( PARAMETER_FLAGS.READONLY ),
					["IsLabeled"] = flags.HasFlag( PARAMETER_FLAGS.LABELED ),
					["IsDiscrete"] = flags.HasFlag( PARAMETER_FLAGS.DISCRETE )
				} );
			}
		}

		return payload;
	}
}
