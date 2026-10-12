using System;

namespace FMODSbox;

public partial class FMODManagerSystem
{
	private static readonly object _eventCacheLock = new();
	private static int _eventCacheVersion = 1;
	private static int _eventCacheBuiltVersion = 0;
	private static List<string> _cachedEventPaths = [];

	internal static void MarkEventCacheDirty()
	{
		lock ( _eventCacheLock )
		{
			_eventCacheVersion++;
		}
	}

	/// <summary>
	/// Returns the list of all known FMOD event paths from currently loaded banks.
	/// </summary>
	public static IReadOnlyList<string> GetAllEventPaths( bool forceRefresh = false )
	{
		if ( !IsInitialized ) return [];

		lock ( _eventCacheLock )
		{
			if ( forceRefresh )
			{
				_eventCacheVersion++;
			}

			if ( _eventCacheBuiltVersion == _eventCacheVersion && _cachedEventPaths.Count > 0 )
				return _cachedEventPaths;

			var unique = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

			foreach ( var kv in Current._loadedBanks )
			{
				var bank = kv.Value.Bank;
				if ( !bank.isValid() )
					continue;

				var result = bank.getEventList( out var eventDescriptions );
				if ( result != FMOD.RESULT.OK || eventDescriptions is null )
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

			_cachedEventPaths = unique.OrderBy( x => x, StringComparer.OrdinalIgnoreCase ).ToList();
			_eventCacheBuiltVersion = _eventCacheVersion;
			return _cachedEventPaths;
		}
	}
}

