namespace Editor;

using FMODSbox;

/// <summary>
/// Editor widget for selecting an FMOD event either as a raw event path string, or via an <see cref="FMODEventResource"/> reference.
/// </summary>
[CustomEditor( typeof( string ), NamedEditor = "fmod_event_ref" )]
public sealed class FMODEventRefControlWidget : ControlWidget
{
	private readonly Layout _body;
	private readonly IconButton _modeButton;

	private SerializedProperty _useResourceProp;
	private SerializedProperty _resourceProp;

	public override bool SupportsMultiEdit => true;

	public FMODEventRefControlWidget( SerializedProperty property ) : base( property )
	{
		PaintBackground = false;

		HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
		Layout = Layout.Row();
		Layout.Spacing = 2;

		_body = Layout.AddColumn( 1 );

		_modeButton = Layout.Add( new IconButton( "category" )
		{
			Background = Color.Transparent,
			IconSize = 16,
			ToolTip = "Event source",
			OnClick = ShowMenu
		} );
		_modeButton.FixedSize = Theme.RowHeight;
		_modeButton.Enabled = SerializedProperty.IsEditable;

		ResolveSiblingProps();
		Rebuild();
	}

	private void ResolveSiblingProps()
	{
		_useResourceProp = SerializedProperty.Parent?.GetProperty( nameof( StudioSoundPoint.UseEventResource ) );
		_resourceProp = SerializedProperty.Parent?.GetProperty( nameof( StudioSoundPoint.EventResource ) );
	}

	private bool IsUsingResource()
	{
		if ( _useResourceProp is null )
			return false;

		if ( _useResourceProp.IsMultipleDifferentValues )
			return false;

		return _useResourceProp.GetValue( false );
	}

	private void ShowMenu()
	{
		var menu = new ContextMenu();
		var usingRes = IsUsingResource();
		var multiple = _useResourceProp?.IsMultipleDifferentValues == true;

		var raw = menu.AddOption( "Event Path", "music_note", () => SwitchMode( useResource: false ) );
		raw.Checkable = true;
		raw.Checked = !multiple && !usingRes;

		var res = menu.AddOption( "Event Resource", "folder", () => SwitchMode( useResource: true ) );
		res.Checkable = true;
		res.Checked = !multiple && usingRes;

		menu.OpenNextTo( _modeButton, WidgetAnchor.BottomEnd with { AdjustSize = true, ConstrainToScreen = true } );
	}

	private void SwitchMode( bool useResource )
	{
		if ( _useResourceProp is null )
			return;

		_useResourceProp.Parent?.NoteStartEdit( _useResourceProp );
		_useResourceProp.SetValue( useResource );
		_useResourceProp.Parent?.NoteFinishEdit( _useResourceProp );

		// Convenience: when switching back to raw, populate the string from the resource if empty.
		if ( !useResource && _resourceProp?.IsMultipleDifferentValues != true )
		{
			var raw = SerializedProperty.GetValue( string.Empty ) ?? string.Empty;
			if ( string.IsNullOrWhiteSpace( raw ) && _resourceProp is not null )
			{
				var r = _resourceProp.GetValue<FMODEventResource>( null );
				if ( r is not null && !string.IsNullOrWhiteSpace( r.EventPath ) )
				{
					SerializedProperty.Parent?.NoteStartEdit( SerializedProperty );
					SerializedProperty.SetValue( r.EventPath );
					SerializedProperty.Parent?.NoteFinishEdit( SerializedProperty );
				}
			}
		}

		Rebuild();
	}

	private void Rebuild()
	{
		_body?.Clear( true );

		var usingRes = IsUsingResource();

		// Update button icon to reflect the active mode.
		_modeButton.Icon = usingRes ? "folder" : "music_note";

		if ( _useResourceProp?.IsMultipleDifferentValues == true )
		{
			_body.Add( new Label( "Multiple Values" ) { Color = Theme.MultipleValues } );
			return;
		}

		if ( usingRes )
		{
			if ( _resourceProp is null )
			{
				_body.Add( new Label( "Missing EventResource property" ) { Color = Theme.Red } );
				return;
			}

			_body.Add( ControlWidget.Create( _resourceProp ) );
			return;
		}

		_body.Add( new FMODEventDropdown( SerializedProperty ) );
	}

	protected override void OnValueChanged()
	{
		base.OnValueChanged();
		ResolveSiblingProps();
		Rebuild();
	}
}
