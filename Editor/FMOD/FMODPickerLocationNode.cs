namespace Editor;

using System.Linq;
using System.Collections.Generic;
using System.IO;
using Sandbox;

/// <summary>
/// Minimal public equivalent of the internal tools FolderNode.
/// Used by our custom pickers so we can curate the locations tree without depending on internal types.
/// </summary>
public sealed class FMODPickerLocationNode( AssetBrowser.Location location ) : TreeNode<AssetBrowser.Location>( location )
{
	public override string Name => Value?.Name ?? string.Empty;

	private static Dictionary<string, FolderMetadataProxy> _metadata;
	private static RealTimeSince _sinceMetaRefresh = 0;

	private sealed class FolderMetadataProxy
	{
		public Color Color { get; set; } = Theme.Yellow;
		public string Icon { get; set; } = "";
	}

	protected override void BuildChildren()
	{
		Clear();

		if ( Value is null )
			return;

		foreach ( var dir in Value.GetDirectories().OrderBy( x => x.Name ) )
		{
			AddItem( new FMODPickerLocationNode( dir ) );
		}
	}

	public override void OnPaint( VirtualWidget item )
	{
		PaintSelection( item );

		var rect = item.Rect;

		// Use folder metadata coloring for disk-backed directories (so generated FMOD folders show own color).
		var iconColor = Theme.Yellow;
		if ( Value is DiskLocation )
		{
			iconColor = GetFolderColor( Value.Path );
		}

		Paint.SetPen( iconColor );
		Paint.DrawIcon( rect, Value?.Icon ?? "folder", 18, TextFlag.LeftCenter );

		rect.Left += 24;
		Paint.SetPen( Theme.Text );
		Paint.SetDefaultFont();
		Paint.DrawText( rect, Name, TextFlag.LeftCenter );
	}

	private static Color GetFolderColor( string absoluteFolder )
	{
		try
		{
			if ( string.IsNullOrWhiteSpace( absoluteFolder ) )
				return Theme.Yellow;

			RefreshMetadata();

			var rootPath = Project.Current.GetRootPath();
			var rel = Path.GetRelativePath( rootPath, absoluteFolder );
			if ( string.IsNullOrWhiteSpace( rel ) )
				return Theme.Yellow;

			if ( _metadata is not null && _metadata.TryGetValue( rel, out var meta ) && meta is not null )
				return meta.Color;
		}
		catch
		{
		}

		return Theme.Yellow;
	}

	private static void RefreshMetadata()
	{
		// Only refresh occasionally.
		if ( _metadata is not null && _sinceMetaRefresh < 1.0f )
			return;

		_sinceMetaRefresh = 0;

		try
		{
			const string metadataPath = "Directory.metadata";
			var loaded = FileSystem.ProjectSettings.ReadJsonOrDefault<IEnumerable<KeyValuePair<string, FolderMetadataProxy>>>( metadataPath );
			_metadata = loaded?.ToDictionary( x => x.Key, x => x.Value ) ?? [];
		}
		catch
		{
			_metadata = [];
		}
	}
}
