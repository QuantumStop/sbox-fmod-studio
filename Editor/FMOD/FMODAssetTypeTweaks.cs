namespace Editor;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// AssetTypeAttribute doesn't currently carry color metadata, so we need this to patch the registered AssetType at runtime.
/// </summary>
public static class FMODAssetTypeTweaks
{
	private static bool _thumbsQueued;
	private static bool _browserRefreshed;
	private static bool _iconsApplied;
	private static bool _allDone;

	private static DateTime _lastRefreshAttemptUtc = DateTime.MinValue;
	private static Color? _lastKnownColor;
	private static AssetType[] _types;


	[EditorEvent.Frame]
	private static void OnFrame()
	{
		if ( _allDone )
			return;

		_types ??= [.. new[]
		{
			AssetType.FromExtension( "fmevent" )
		}.Where( t => t is not null ).Distinct()];

		if ( _types.Length == 0 )
		{
			_types = null; // not ready yet, retry next frame
			return;
		}

		var desiredColor = Color.Parse( "#8d368d" )!.Value;
		foreach ( var type in _types )
		{
			// Only re-apply if the color was stomped back to the default green.
			var current = type.Color;
			if ( current != desiredColor )
			{
				TrySetAssetTypeColor( type, desiredColor );
				_lastKnownColor = desiredColor;
			}

			TrySetAssetTypeBool( type, "HiddenByDefault", true );
		}

		// Icons are supplied by the resource type itself (CreateAssetTypeIcon/RenderThumbnail),
		// not by patching AssetType pixmaps at runtime.
		_iconsApplied = true;


		if ( !_thumbsQueued )
		{
			foreach ( var asset in AssetSystem.All )
			{
				var ext = asset?.AssetType?.FileExtension;
				if ( ext == "fmevent" )
				{
					asset.RebuildThumbnail( startBuild: true );
				}
			}

			_thumbsQueued = true;
		}

		if ( !_browserRefreshed && (DateTime.UtcNow - _lastRefreshAttemptUtc) > TimeSpan.FromSeconds( 1 ) )
		{
			_lastRefreshAttemptUtc = DateTime.UtcNow;
			try
			{
				var any = false;
				if ( AssetBrowser.Get() is { } focused )
				{
					//	focused.UpdateAssetList();
					any = true;
				}

				if ( MainAssetBrowser.Instance?.Local is { } main )
				{
					main.UpdateAssetList();
					any = true;
				}

				_browserRefreshed = any;
			}
			catch
			{
				_browserRefreshed = false;
			}
		}

		if ( _iconsApplied && _thumbsQueued && _browserRefreshed )
			_allDone = true;
	}

	[EditorEvent.Hotload]
	private static void OnHotload()
	{
		// Ensure thumbnails reflect any render-code changes after hotload.
		_thumbsQueued = false;
		_browserRefreshed = false;
		_iconsApplied = false;
		_allDone = false;
		_lastRefreshAttemptUtc = DateTime.MinValue;
		_types = null;
	}

	private static void TrySetAssetTypeColor( AssetType type, Color color )
	{
		try
		{
			var prop = typeof( AssetType ).GetProperty( "Color", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic );
			if ( prop?.CanWrite == true )
			{
				prop.SetValue( type, color );
				return;
			}

			var field = typeof( AssetType ).GetField( "<Color>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic );
			field?.SetValue( type, color );
		}
		catch
		{
		}
	}

	private static void TrySetAssetTypeBool( AssetType type, string name, bool value )
	{
		try
		{
			var prop = typeof( AssetType ).GetProperty( name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic );
			if ( prop?.CanWrite == true && prop.PropertyType == typeof( bool ) )
			{
				prop.SetValue( type, value );
				return;
			}

			var field = typeof( AssetType ).GetField( $"<{name}>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic );
			if ( field?.FieldType == typeof( bool ) )
				field.SetValue( type, value );
		}
		catch
		{
		}
	}

}
