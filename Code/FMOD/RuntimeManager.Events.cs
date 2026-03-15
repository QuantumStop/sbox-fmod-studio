using System;

namespace FMODSbox;

public partial class FMODManagerSystem
{
	private static readonly object EventCacheLock = new();
	private static int EventCacheVersion = 1;
	private static int EventCacheBuiltVersion = 0;
	private static List<string> CachedEventPaths = [];

	internal static void MarkEventCacheDirty()
	{
		lock ( EventCacheLock )
		{
			EventCacheVersion++;
		}
	}

	/// <summary>
	/// Returns the list of all known FMOD event paths from currently loaded banks.
	/// </summary>
	public static IReadOnlyList<string> GetAllEventPaths( bool forceRefresh = false )
	{
		if ( !IsInitialized )
			return Array.Empty<string>();

		lock ( EventCacheLock )
		{
			if ( forceRefresh )
			{
				EventCacheVersion++;
			}

			if ( EventCacheBuiltVersion == EventCacheVersion && CachedEventPaths.Count > 0 )
				return CachedEventPaths;

			var unique = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

			foreach ( var kv in Current.loadedBanks )
			{
				var bank = kv.Value.Bank;
				if ( !bank.isValid() )
					continue;

				var result = bank.getEventList( out var eventDescriptions );
				if ( result != FMOD.RESULT.OK || eventDescriptions == null )
					continue;

				foreach ( var desc in eventDescriptions )
				{
					if ( !desc.isValid() )
						continue;

					if ( desc.getPath( out var path ) != FMOD.RESULT.OK )
						continue;

					if ( string.IsNullOrWhiteSpace( path ) )
						continue;

					unique.Add( path.Trim() );
				}
			}

			CachedEventPaths = unique.OrderBy( x => x, StringComparer.OrdinalIgnoreCase ).ToList();
			EventCacheBuiltVersion = EventCacheVersion;
			return CachedEventPaths;
		}
	}
}

