namespace Editor;

using System.IO;

/// <summary>
/// Tree node for browsing the generated FMOD event resource directory.
/// </summary>
public sealed class FMODProjectNode( AssetBrowser.Location location ) : TreeNode<AssetBrowser.Location>( location )
{
	public override string Name => Value.Name;

	protected override void BuildChildren()
	{
		Clear();

		foreach ( var dir in Value.GetDirectories() )
		{
			AddItem( new FMODProjectNode( dir ) );
		}
	}

	public override void OnPaint( VirtualWidget item )
	{
		PaintSelection( item );

		var rect = item.Rect;

		Paint.SetPen( Color.Parse( "#8d368d" )!.Value );
		Paint.DrawIcon( rect, Value.Icon ?? "folder", 18, TextFlag.LeftCenter );

		rect.Left += 24;
		Paint.SetPen( Theme.Text );
		Paint.SetDefaultFont();
		Paint.DrawText( rect, Name, TextFlag.LeftCenter );
	}

	public override bool OnContextMenu()
	{
		var menu = new ContextMenu();
		menu.AddOption( "Refresh FMOD Events", "refresh", () =>
		{
			FMODEventResourceGenerator.EnsureGenerated( force: true );
			Dirty();
		} );

		var root = FMODEventResourceGenerator.GetGeneratedRoot();
		if ( Directory.Exists( root ) )
		{
			menu.AddOption( "Open Generated Folder", "folder_open", () => EditorUtility.OpenFolder( root ) );
		}

		menu.OpenAt( Application.CursorPosition );
		return true;
	}
}
