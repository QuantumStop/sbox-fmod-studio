namespace Editor;

using System;
using System.Collections.Generic;
using System.Linq;
using FMODSbox;

/// <summary>
/// Dropdown widget for selecting FMOD event paths from loaded banks.
/// </summary>
public sealed class FMODEventDropdown : ControlWidget
{
	private PopupWidget _menu;
	private ScrollArea _scroller;
	private LineEdit _searchEdit;
	private Widget _listCanvas;
	private string _searchText = string.Empty;
	private IReadOnlyList<string> _events = [];

	public override bool IsControlActive => base.IsControlActive || _menu.IsValid();
	public override bool IsControlHovered => base.IsControlHovered || _menu.IsValid();
	public override bool IsControlButton => true;
	public override bool SupportsMultiEdit => true;

	public FMODEventDropdown( SerializedProperty property ) : base( property )
	{
		Cursor = CursorShape.Finger;
		Layout = Layout.Row();
		Layout.Spacing = 2;
	}

	protected override void PaintControl()
	{
		var color = IsControlHovered ? Theme.Blue : Theme.TextControl;
		if ( IsControlDisabled ) color = color.WithAlpha( 0.5f );

		var rect = LocalRect.Shrink( 8, 0 );

		Paint.SetPen( SerializedProperty.IsMultipleDifferentValues ? Theme.MultipleValues : color );
		Paint.DrawText( rect, GetCurrentLabel(), TextFlag.LeftCenter );
		Paint.SetPen( color );
		Paint.DrawIcon( rect, "Arrow_Drop_Down", 17, TextFlag.RightCenter );
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( IsControlDisabled ) return;
		if ( !e.LeftMouseButton ) return;
		if ( _menu.IsValid() ) return;

		OpenMenu();
	}

	public override void StartEditing()
	{
		if ( IsControlDisabled ) return;
		if ( _menu.IsValid() ) return;
		OpenMenu();
	}

	private string GetCurrentLabel()
	{
		if ( SerializedProperty.IsMultipleDifferentValues )
			return "Multiple Values";

		var value = SerializedProperty.GetValue( string.Empty ) ?? string.Empty;
		if ( string.IsNullOrWhiteSpace( value ) )
			return "None";

		return value;
	}

	private void OpenMenu()
	{
		PropertyStartEdit();

		_events = FMODManagerSystem.IsInitialized
			? FMODManagerSystem.GetAllEventPaths()
			: FMODEditorEventCache.GetAllEventPaths();

		var menuWidth = ScreenRect.Width;

		_menu = new( null );
		_menu.Layout = Layout.Column();
		_menu.MinimumWidth = menuWidth;
		_menu.MaximumWidth = menuWidth;
		_menu.VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
		_menu.OnLostFocus += PropertyFinishEdit;
		_menu.OnPaintOverride = PaintMenuBackground;

		var searchRow = _menu.Layout.AddRow();
		searchRow.Margin = 6;
		searchRow.Spacing = 6;
		searchRow.Add( new IconButton( "search" ) { Background = Color.Transparent, TransparentForMouseEvents = true, IconSize = 16 } );

		_searchEdit = new LineEdit
		{
			Text = _searchText ?? string.Empty
		};
		_searchEdit.TextChanged += value =>
		{
			_searchText = value ?? string.Empty;
			RebuildList();
		};

		searchRow.Add( _searchEdit, 1 );

		_scroller = _menu.Layout.Add( new ScrollArea( this ), 1 );
		_scroller.NoSystemBackground = true;
		_scroller.TranslucentBackground = true;

		_listCanvas = new Widget( _scroller )
		{
			Layout = Layout.Column(),
			VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand,
			MaximumWidth = menuWidth
		};
		_scroller.Canvas = _listCanvas;

		RebuildList();

		_menu.Position = ScreenRect.BottomLeft;
		_menu.Visible = true;
		_menu.AdjustSize();
		_menu.ConstrainToScreen();
	}

	private void RebuildList()
	{
		if ( !_menu.IsValid() || !_listCanvas.IsValid() )
			return;

		_listCanvas.Layout.Clear( true );

		AddEntry( "(None)", string.Empty );
		AddAction( "(Refresh List)", () =>
		{
			_events = FMODManagerSystem.IsInitialized
				? FMODManagerSystem.GetAllEventPaths( forceRefresh: true )
				: FMODEditorEventCache.GetAllEventPaths( forceRefresh: true );
			RebuildList();
		} );
		_listCanvas.Layout.AddSpacingCell( 4 );
		if ( _events is null || _events.Count == 0 )
		{
			var hint = FMODManagerSystem.IsInitialized
				? "No events found in loaded banks"
				: "No events found (check bank files under Assets/fmod)";
			_listCanvas.Layout.Add( new Label( hint ) );
			_listCanvas.AdjustSize();
			return;
		}

		var query = (_searchText ?? string.Empty).Trim();
		var filtered = string.IsNullOrEmpty( query )
			? _events
			: [.. _events.Where( e => e.Contains( query, StringComparison.OrdinalIgnoreCase ) )];

		foreach ( var ev in filtered )
		{
			AddEntry( ev, ev );
		}

		_listCanvas.AdjustSize();
	}

	private void AddAction( string label, Action action )
	{
		var option = _listCanvas.Layout.Add( new FmodEventMenuOption( label, null, SerializedProperty ) );
		option.MouseLeftPress = () => action?.Invoke();
	}

	private void AddEntry( string label, string value )
	{
		var option = _listCanvas.Layout.Add( new FmodEventMenuOption( label, value, SerializedProperty ) );
		option.MouseLeftPress = () =>
		{
			SerializedProperty.SetValue( value );
			_menu?.Close();
		};
	}

	private bool PaintMenuBackground()
	{
		Paint.SetBrushAndPen( Theme.ControlBackground, Theme.WidgetBackground, 1 );
		Paint.DrawRect( Paint.LocalRect.Shrink( 1 ), 4 );
		return true;
	}
}

file class FmodEventMenuOption : Widget
{
	private readonly string _label;
	private readonly string _value;
	private readonly SerializedProperty _property;

	public FmodEventMenuOption( string label, string value, SerializedProperty property ) : base( null )
	{
		_label = label ?? string.Empty;
		_value = value;
		_property = property;

		Layout = Layout.Row();
		Layout.Margin = 0;
		VerticalSizeMode = SizeMode.Default;
		FixedHeight = Theme.RowHeight;
		Cursor = CursorShape.Finger;

		var col = Layout.AddColumn();
		col.Margin = new Sandbox.UI.Margin( 8, 4 );
		var title = col.Add( new Label( _label ) );
		title.Color = Theme.Text;
	}

	private bool IsSelected()
	{
		if ( _value is null ) return false;
		if ( _property.IsMultipleDifferentValues ) return false;

		var value = _property.GetValue( string.Empty ) ?? string.Empty;
		return string.Equals( value, _value, StringComparison.Ordinal );
	}

	protected override void OnPaint()
	{
		if ( Paint.HasMouseOver || IsSelected() )
		{
			Paint.SetBrushAndPen( Theme.Blue.WithAlpha( IsSelected() ? 0.5f : 0.1f ) );
			Paint.DrawRect( LocalRect );
		}
	}
}
