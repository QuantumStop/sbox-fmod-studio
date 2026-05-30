namespace Editor;

using System;
using System.Linq;
using System.Reflection;
using FMODSbox;

/// <summary>
/// Custom asset picker for <see cref="FMODEventResource"/> that includes the "FMOD Project (...)" location tree.
/// </summary>
[AssetPicker( typeof( FMODEventResource ) )]
public sealed class FMODEventResourcePicker : AssetPicker
{
	private static readonly FieldInfo AssetLocationsField =
		typeof( AssetBrowser ).GetField( "AssetLocations", BindingFlags.Instance | BindingFlags.NonPublic );

	public LocalAssetBrowser LocalBrowser { get; private set; }
	public CloudAssetBrowser CloudBrowser { get; private set; }

	private DockManager _dock;
	private Button _confirm;
	private Button _cancel;

	private Asset[] _highlighted = [];

	public FMODEventResourcePicker( Widget parent, AssetType assetType, PickerOptions options )
		: base( parent, assetType, options )
	{
		Window.Size = new Vector2( 1280, 720 );
		Window.SetModal( true );
		Window.MinimumSize = 200;
		Window.MaximumSize = 10000;
		Window.StateCookie = "FmodEventResourcePicker";
		Window.RestoreFromStateCookie();

		Window.Title = "Select Event Resource";
		Window.StatusBar = new StatusBar( this );

		Layout = Layout.Column();
		CreateUI();
	}

	private void CreateUI()
	{
		_dock = new DockManager( this );
		var properties = DockManager.DockProperty.HideCloseButton
			| DockManager.DockProperty.DisallowUserDocking
			| DockManager.DockProperty.DisableDraggableTab;

		LocalBrowser = new( _dock, AssetType is null ? null : [AssetType] )
		{
			WindowTitle = "Asset Browser",
			MultiSelect = Options.EnableMultiselect,
			ViewModeType = AssetListViewMode.MediumIcons,
			ShowRecursiveFiles = true
		};

		LocalBrowser.SetWindowIcon( "folder" );

		LocalBrowser.OnAssetHighlight += Highlight;
		LocalBrowser.OnAssetsHighlight += Highlight;
		LocalBrowser.OnAssetSelected += _ => Select();
		LocalBrowser.OnHighlight += _ => _confirm.Enabled = false;

		_dock.AddDock( null, LocalBrowser, DockArea.Inside, properties );

		if ( Options.EnableCloud )
		{
			CloudBrowser = new CloudAssetBrowser( _dock, AssetType is null ? null : [AssetType] )
			{
				WindowTitle = "Cloud Browser",
				MultiSelect = Options.EnableMultiselect,
				OnPackageSelected = _ => Select(),
			};

			CloudBrowser.SetWindowIcon( "cloud_download" );

			_dock.AddDock( null, CloudBrowser, DockArea.Inside, properties );
		}

		Layout.Add( _dock, 1 );
		Layout.AddSeparator();

		var bottom = Layout.AddRow();
		bottom.Spacing = 10;
		bottom.Margin = 10;
		bottom.AddStretchCell();

		_confirm = bottom.Add( new Button.Primary( "Select" ) );
		_confirm.Enabled = false;
		_confirm.Clicked = Select;

		_cancel = bottom.Add( new Button( "Cancel" ) );
		_cancel.Enabled = true;
		_cancel.Clicked = Close;

		EnsureFmodLocationPresent();
	}

	private void EnsureFmodLocationPresent()
	{
		try
		{
			// Ensure generated assets exist so the node has something to browse.
			FMODEventResourceGenerator.EnsureGenerated();

			var locations = AssetLocationsField?.GetValue( LocalBrowser ) is AssetLocations loc ? loc : null;

			if ( !locations.IsValid() )
				return;

			// Hide the standard project/library roots for this picker (we only want the FMOD stuff)
			locations.Clear();

			locations.AddItem( new FMODPickerLocationNode( new RecentsLocation() ) );
			locations.AddItem( new FMODPickerLocationNode( new EverythingLocation() ) );

			locations.AddItem( new TreeNode.Spacer( 10 ) );

			var root = FMODEventResourceGenerator.GetGeneratedEventsRoot();
			var location = new FMODProjectLocation( root );

			var node = new FMODProjectNode( location );
			locations.AddItem( node );
			locations.Open( node );
		}
		catch
		{
		}
	}

	public override void SetSelection( Asset asset )
	{
		_dock?.RaiseDock( LocalBrowser );

		if ( asset is null )
		{
			// Default to the generated FMOD events root.
			var location = new FMODProjectLocation( FMODEventResourceGenerator.GetGeneratedEventsRoot() );
			if ( location.IsValid() )
				LocalBrowser.NavigateTo( location, addToHistory: false );
			else
				LocalBrowser.NavigateTo( Project.Current.GetAssetsPath(), addToHistory: false );
			return;
		}

		LocalBrowser.FocusOnAsset( asset );
	}

	public override void SetSearchText( string value )
	{
		if ( LocalBrowser?.Search is not null )
			LocalBrowser.Search.Value = value ?? string.Empty;
	}

	private void Select()
	{
		if ( LocalBrowser.Visible )
		{
			var assets = LocalBrowser.GetSelected<AssetEntry>().Select( x => x.Asset ).Where( x => x is not null ).ToArray();
			if ( assets.Length == 0 )
				return;

			Submit( assets );
			return;
		}

		if ( CloudBrowser.IsValid() && CloudBrowser.Visible )
		{
			var pkg = CloudBrowser.GetSelected<PackageEntry>().FirstOrDefault()?.Package;
			if ( pkg is not null )
			{
				Submit( pkg );
			}
		}
	}

	private void Highlight( Asset asset )
	{
		if ( asset is null )
		{
			_confirm.Enabled = false;
			return;
		}

		_highlighted = [asset];
		_confirm.Enabled = true;

		try { OnAssetHighlighted?.Invoke( _highlighted ); }
		catch ( NullReferenceException ) { }

		BindSystem.Flush();
		EditorUtility.PlayAssetSound( asset );
	}

	private void Highlight( Asset[] assets )
	{
		if ( assets is null )
		{
			return;
		}

		_highlighted = assets?.Where( x => x is not null ).ToArray() ?? [];
		_confirm.Enabled = _highlighted.Length > 0;

		try { OnAssetHighlighted?.Invoke( _highlighted ); }
		catch ( NullReferenceException ) { }

		BindSystem.Flush();
	}

	protected override void OnKeyPress( KeyEvent e )
	{
		base.OnKeyPress( e );
		if ( e.Key == KeyCode.Escape )
			Close();
	}
}
