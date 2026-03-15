namespace Editor;

using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// A custom AssetBrowser location rooted at a generated FMOD event resource directory.
/// Keeps navigation within the FMOD tree (separate from the main project root).
/// </summary>
public record FMODProjectLocation : DiskLocation
{
	private static string GetRootIcon()
	{
		return "logo_fmod.png";
	}

	public FMODProjectLocation( string rootPath, string currentPath = null ) : base( currentPath ?? rootPath )
	{
		RootPath = System.IO.Path.GetFullPath( rootPath );
		Path = System.IO.Path.GetFullPath( currentPath ?? rootPath );

		if ( Path.EndsWith( '\\' ) )
			Path = Path.TrimEnd( '\\' );

		RootTitle = $"FMOD Project ({Project.Current.Config.Title})";

		IsRoot = string.Equals( RootPath, Path, StringComparison.OrdinalIgnoreCase );
		Name = IsRoot ? RootTitle : new DirectoryInfo( Path ).Name;
		RelativePath = IsRoot ? "" : ToSlashPath( System.IO.Path.GetRelativePath( RootPath, Path ) );

		Icon = IsRoot ? GetRootIcon() : "folder";
		Type = LocalAssetBrowser.LocationType.Assets;
	}

	public override bool CanGoUp() => !IsRoot;
	public override bool IsValid() => Directory.Exists( Path );

	public override IEnumerable<AssetBrowser.Location> GetDirectories()
	{
		if ( !Directory.Exists( Path ) )
			yield break;

		foreach ( var subDir in Directory.GetDirectories( Path ) )
		{
			var dir = new DirectoryInfo( subDir );
			if ( dir.Attributes.HasFlag( FileAttributes.Hidden ) )
				continue;

			var name = dir.Name;
			if ( name.StartsWith( '.' ) || name.StartsWith( '_' ) )
				continue;

			if ( name.Equals( "obj", StringComparison.OrdinalIgnoreCase ) )
				continue;

			yield return new FMODProjectLocation( RootPath, dir.FullName );
		}
	}

	public override IEnumerable<FileInfo> GetFiles()
	{
		if ( !Directory.Exists( Path ) )
			yield break;

		foreach ( var filePath in Directory.GetFiles( Path, "*.fmevent", SearchOption.TopDirectoryOnly ) )
		{
			var file = new FileInfo( filePath );
			if ( file.Attributes.HasFlag( FileAttributes.Hidden ) )
				continue;

			if ( file.Name.StartsWith( '.' ) )
				continue;

			yield return file;
		}
	}

	private static string ToSlashPath( string value )
	{
		return (value ?? string.Empty).Replace( '\\', '/' );
	}
}
