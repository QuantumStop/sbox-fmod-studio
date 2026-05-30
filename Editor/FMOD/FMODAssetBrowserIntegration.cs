namespace Editor;

using System.Collections;
using System.Reflection;

/// <summary>
/// Injects an "FMOD Project (...)" root node into the Asset Browser locations panel.
/// </summary>
public static class FMODAssetBrowserIntegration
{
	private static readonly FieldInfo AssetLocationsField =
		typeof( AssetBrowser ).GetField( "AssetLocations", BindingFlags.Instance | BindingFlags.NonPublic );

	private static readonly FieldInfo TreeViewItemsField =
		typeof( TreeView ).GetField( "_items", BindingFlags.Instance | BindingFlags.NonPublic );

	private static bool _nodeInjected;
	private static AssetBrowser _trackedBrowser;

	[EditorEvent.Frame]
	public static void OnFrame()
	{
		// Ensure generated assets exist to begin with
		FMODEventResourceGenerator.EnsureGenerated();

		var browser = MainAssetBrowser.Instance?.Local;

		// Browser closed or replaced.
		if ( !browser.IsValid() || !_trackedBrowser.IsValid() && _trackedBrowser != browser )
		{
			Reset();
			if ( !browser.IsValid() )
				return;
		}

		_trackedBrowser = browser;

		if ( _nodeInjected )
			return;

		if ( AssetLocationsField?.GetValue( browser ) is not AssetLocations locations )
			return;

		if ( HasNode( locations ) )
		{
			_nodeInjected = true;
			return;
		}

		var root = FMODEventResourceGenerator.GetGeneratedEventsRoot();
		var location = new FMODProjectLocation( root );

		// Add a spacer so it doesn't stick to the bottom of another group.
		locations.AddItem( new TreeNode.Spacer( 10 ) );
		var node = new FMODProjectNode( location );
		locations.AddItem( node );
		locations.Open( node );

		_nodeInjected = true;
	}

	private static void Reset()
	{
		_trackedBrowser = null;
		_nodeInjected = false;
	}

	private static bool HasNode( AssetLocations locations )
	{
		var itemsObj = TreeViewItemsField?.GetValue( locations );
		if ( itemsObj is not IEnumerable items )
			return false;

		foreach ( var item in items )
		{
			if ( item is FMODProjectNode )
				return true;
		}

		return false;
	}
}

