namespace Editor.Assets;

using System.Threading.Tasks;
using FMODSbox;
using System;
using System.Globalization;

[AssetPreview( "fmevent" )]
[AssetPreview( "fmodevent" )]
public sealed class PreviewFMODEvent( Asset asset ) : AssetPreview( asset )
{
	private FMODEventPreviewWidget _widget;

	public override bool IsAnimatedPreview => false;
	public override float VideoLength => 0.0f;

	public override Task InitializeScene()
	{
		// Asset preview pipeline expects a Scene to exist (it will tick it).
		// We don't render anything, but we still create the default editor scene to avoid null refs.
		return base.InitializeScene();
	}

	public override Widget CreateWidget( Widget parent )
	{
		_widget = new FMODEventPreviewWidget( parent )
		{
			Asset = Asset
		};

		return _widget;
	}

	public override void Dispose()
	{
		_widget?.Stop();
		_widget = null;
		base.Dispose();
	}
}

sealed class FMODEventPreviewWidget : Widget
{
	private const float HeaderHeight = 30f;
	private const float RowHeight = 26f;

	public Asset Asset
	{
		get => _asset;
		set
		{
			_asset = value;
			Reload();
		}
	}

	private Asset _asset;
	private FMODEventResource _resource;
	private FMOD.Studio.EventInstance _instance;
	private FMOD.Studio.EventDescription _desc;
	private int _eventLengthMs;

	private Label _title;
	private Label _path;
	private Label _error;
	private IconButton _playStop;
	private IconButton _refreshBanks;

	private WaveformTimelineSlider _timeline;
	private readonly List<float> _timelineRms = [];

	private ScrollArea _paramScroll;
	private Widget _paramCanvas;
	private Label _paramHeader;
	private Label _noParams;

	private readonly Dictionary<string, FloatSlider> _floatSliders = [];
	private readonly Dictionary<string, ComboBox> _labelCombos = [];

	public FMODEventPreviewWidget( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();
		Layout.Margin = 8;
		Layout.Spacing = 8;

		BuildStaticUI();
	}

	private static Widget AddDivider( Widget parent )
	{
		var div = new Widget( parent )
		{
			MinimumHeight = 1,
			MaximumHeight = 1
		};
		div.SetStyles( "background-color: rgba(255,255,255,0.06);" );
		return div;
	}

	private static Layout AddCompactRow( Layout layout, Widget parent, float height = RowHeight, float spacing = 6f )
	{
		var host = new Widget( parent )
		{
			Layout = Layout.Row(),
			HorizontalSizeMode = SizeMode.Expand,
			MinimumHeight = height,
			MaximumHeight = height
		};

		host.Layout.Spacing = spacing;
		host.Layout.Alignment = TextFlag.LeftCenter;
		layout.Add( host );
		return host.Layout;
	}

	private static void Compact( Widget w, float height = RowHeight )
	{
		w.MinimumHeight = height;
		w.MaximumHeight = height;
	}

	private static void CompactSquare( Widget w, float size )
	{
		w.MinimumHeight = size;
		w.MaximumHeight = size;
		w.MinimumWidth = size;
		w.MaximumWidth = size;
	}

	private static void TrySetIcon( IconButton button, string icon )
	{
		if ( !button.IsValid() )
			return;

		var t = button.GetType();
		var iconProp = t.GetProperty( "Icon" );
		if ( iconProp?.CanWrite == true )
		{
			iconProp.SetValue( button, icon );
			return;
		}

		var iconField = t.GetField( "Icon" );
		if ( iconField is not null )
		{
			iconField.SetValue( button, icon );
		}
	}

	private void BuildStaticUI()
	{
		var panel = new Widget( this )
		{
			Layout = Layout.Column()
		};
		panel.SetStyles( "background-color: rgba(25,25,25,0.65); border-radius: 6px; padding: 8px;" );
		Layout.Add( panel, 1 );

		panel.Layout.Spacing = 8;

		var header = AddCompactRow( panel.Layout, panel, HeaderHeight );

		_title = header.Add( new Label( "FMOD Event" ), 1 );
		_title.Alignment = TextFlag.LeftCenter;
		_title.SetStyles( "font-weight: 600;" );

		_refreshBanks = header.Add( new IconButton( "refresh" ) );
		_refreshBanks.Background = Color.Transparent;
		_refreshBanks.IconSize = 16;
		_refreshBanks.ToolTip = "Reload FMOD banks (editor preview)";
		_refreshBanks.OnClick += () =>
		{
			FMODEditorPreviewPlayer.EnsureInitialized( forceReloadBanks: true );
			Reload();
		};
		CompactSquare( _refreshBanks, HeaderHeight );

		_playStop = header.Add( new IconButton( "play_arrow" ) );
		_playStop.Background = Color.Transparent;
		_playStop.IconSize = 18;
		_playStop.ToolTip = "Play";
		_playStop.OnClick += TogglePlayStop;
		CompactSquare( _playStop, HeaderHeight );

		_path = panel.Layout.Add( new Label( "" ) );
		_path.SetStyles( "opacity: 0.75; font-size: 11px;" );

		_error = panel.Layout.Add( new Label( "" ) );
		_error.SetStyles( "opacity: 0.9; color: rgba(255,120,120,0.95); font-size: 11px;" );
		_error.Visible = false;

		AddDivider( panel );

		_timeline = panel.Layout.Add( new WaveformTimelineSlider( this, _timelineRms ) );
		_timeline.MinimumHeight = 64;
		_timeline.MaximumHeight = 64;
		_timeline.Minimum = 0;
		_timeline.Maximum = 1;
		_timeline.Step = 1f;
		_timeline.OnValueEdited = SeekTimeline;
		_timeline.Enabled = false;

		AddDivider( panel );

		_paramHeader = panel.Layout.Add( new Label( "Parameters" ) );
		_paramHeader.SetStyles( "opacity: 0.75; font-size: 10px; text-transform: uppercase; letter-spacing: 1px;" );

		_noParams = panel.Layout.Add( new Label( "No parameters" ) );
		_noParams.SetStyles( "opacity: 0.7;" );
		_noParams.Visible = false;

		_paramScroll = panel.Layout.Add( new ScrollArea( this ), 1 );
		_paramScroll.NoSystemBackground = true;
		_paramScroll.TranslucentBackground = true;
		_paramScroll.Canvas = _paramCanvas = new Widget( _paramScroll )
		{
			Layout = Layout.Column(),
			HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand,
			VerticalSizeMode = SizeMode.CanGrow
		};
		_paramCanvas.MaximumWidth = 4096;
		_paramCanvas.Layout.Spacing = 4;
	}

	private bool IsPlaying()
	{
		if ( !_instance.isValid() )
			return false;

		if ( _instance.getPlaybackState( out var state ) != FMOD.RESULT.OK )
			return false;

		return state == FMOD.Studio.PLAYBACK_STATE.PLAYING || state == FMOD.Studio.PLAYBACK_STATE.STARTING;
	}

	private void UpdatePlayStopButton()
	{
		var playing = IsPlaying();
		TrySetIcon( _playStop, playing ? "stop" : "play_arrow" );
		_playStop.ToolTip = playing ? "Stop" : "Play";
	}

	private void Reload()
	{
		Stop( immediate: true );

		_resource = null;
		_desc = default;
		_eventLengthMs = 0;

		if ( _asset == null )
		{
			_title.Text = "FMOD Event";
			_path.Text = "";
			_error.Visible = false;
			RebuildParameterUI( [] );
			return;
		}

		if ( _asset.TryLoadResource<FMODEventResource>( out var obj ) )
		{
			_resource = obj;
		}

		_title.Text = _asset.Name;

		if ( _resource == null || string.IsNullOrWhiteSpace( _resource ) )
		{
			_path.Text = "";
			_error.Text = "Could not load resource or missing EventPath.";
			_error.Visible = true;
			RebuildParameterUI( [] );
			return;
		}

		_path.Text = _resource;
		_error.Visible = false;

		_eventLengthMs = _resource.LengthMs;

		if ( FMODEditorPreviewPlayer.TryGetEventDescription( _resource, out var desc ) )
		{
			_desc = desc;

			if ( _eventLengthMs <= 0 && _desc.isValid() && _desc.getLength( out var len ) == FMOD.RESULT.OK )
				_eventLengthMs = len;
		}

		RebuildParameterUI( _resource.Parameters ?? [] );
		UpdateTimelineUI( enabled: true, lengthMs: _eventLengthMs, seedKey: _resource );
		UpdatePlayStopButton();
	}

	private void RebuildParameterUI( IEnumerable<FMODEventResource.FMODEventParameter> parameters )
	{
		_paramCanvas.Layout.Clear( true );
		_floatSliders.Clear();
		_labelCombos.Clear();

		var list = parameters?.ToList() ?? [];
		if ( list.Count == 0 )
		{
			_paramHeader.Visible = false;
			_paramScroll.Visible = false;
			_noParams.Visible = true;
			_paramCanvas.AdjustSize();
			return;
		}

		_paramHeader.Visible = true;
		_paramScroll.Visible = true;
		_noParams.Visible = false;

		foreach ( var p in list.OrderBy( x => x.Name ) )
		{
			if ( string.IsNullOrWhiteSpace( p?.Name ) )
				continue;

			var host = new Widget( _paramCanvas )
			{
				Layout = Layout.Row(),
				HorizontalSizeMode = SizeMode.Expand,
				MinimumHeight = RowHeight,
				MaximumHeight = RowHeight
			};
			host.Layout.Spacing = 8;
			host.Layout.Alignment = TextFlag.LeftCenter;
			_paramCanvas.Layout.Add( host );

			var row = host.Layout;

			var name = row.Add( new Label( p.Name ) );
			name.Alignment = TextFlag.LeftCenter;
			name.MinimumWidth = 120;
			name.MaximumWidth = 120;
			name.SetStyles( "opacity: 0.9;" );

			if ( p.IsLabeled )
			{
				var combo = row.Add( new ComboBox(), 1 );
				combo.MinimumWidth = 220;
				combo.Enabled = _desc.isValid();
				Compact( combo );

				if ( _desc.isValid() )
				{
					foreach ( var label in FMODEditorPreviewPlayer.GetParameterLabels( _desc, p.Name ) )
					{
						var captured = label;
						combo.AddItem( label, null, () =>
						{
							if ( _instance.isValid() )
								FMODEditorPreviewPlayer.SetParameter( _instance, p.Name, captured );
						} );
					}
				}

				_labelCombos[p.Name] = combo;
			}
			else
			{
				var min = MathF.Min( p.Min, p.Max );
				var max = MathF.Max( p.Min, p.Max );
				var def = Math.Clamp( p.Default, min, max );

				var rangeLabel = row.Add( new Label( $"{min:0.###}..{max:0.###}" ) );
				rangeLabel.Alignment = TextFlag.LeftCenter;
				rangeLabel.MinimumWidth = 92;
				rangeLabel.MaximumWidth = 92;
				rangeLabel.SetStyles( "opacity: 0.55; font-size: 10px;" );

				var slider = row.Add( new FloatSlider( this ), 1 );
				slider.Minimum = min;
				slider.Maximum = max;
				slider.Value = def;
				slider.Step = p.IsDiscrete ? 1f : MathF.Max( (max - min) / 200f, 0.001f );
				slider.OnValueEdited = () =>
				{
					if ( _instance.isValid() )
						FMODEditorPreviewPlayer.SetParameter( _instance, p.Name, slider.Value );
				};
				Compact( slider );

				var value = row.Add( new LineEdit() );
				value.Text = def.ToString( "0.###", CultureInfo.InvariantCulture );
				value.MinimumWidth = 64;
				value.MaximumWidth = 64;
				Compact( value );
				value.EditingFinished += () =>
				{
					if ( float.TryParse( value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v ) )
					{
						v = Math.Clamp( v, min, max );
						slider.Value = v;

						if ( _instance.isValid() )
							FMODEditorPreviewPlayer.SetParameter( _instance, p.Name, v );
					}

					value.Text = slider.Value.ToString( "0.###", CultureInfo.InvariantCulture );
				};

				slider.OnValueEdited += () =>
				{
					value.Text = slider.Value.ToString( "0.###", CultureInfo.InvariantCulture );
				};

				_floatSliders[p.Name] = slider;
			}
		}

		_paramCanvas.AdjustSize();
	}

	private void Play()
	{
		if ( _resource == null || string.IsNullOrWhiteSpace( _resource ) )
			return;

		_error.Visible = false;
		Stop( immediate: true );

		_instance = FMODEditorPreviewPlayer.Play( _resource, Vector3.Zero );
		if ( !_instance.isValid() )
		{
			_error.Text = $"Failed to play: {_resource}";
			_error.Visible = true;
			UpdatePlayStopButton();
			return;
		}

		if ( _eventLengthMs <= 0 && _desc.isValid() && _desc.getLength( out var len ) == FMOD.RESULT.OK )
			_eventLengthMs = len;

		UpdateTimelineUI( enabled: true, lengthMs: _eventLengthMs, seedKey: _resource );

		// Seek to the current scrub position (allows scrubbing before pressing play)
		if ( _eventLengthMs > 0 && _timeline.Value > 0 )
		{
			_instance.setTimelinePosition( (int)_timeline.Value );
		}

		// Apply slider and label defaults to the new instance

		foreach ( var kv in _floatSliders )
		{
			FMODEditorPreviewPlayer.SetParameter( _instance, kv.Key, kv.Value.Value );
		}

		foreach ( var kv in _labelCombos )
		{
			if ( !string.IsNullOrWhiteSpace( kv.Value.CurrentText ) )
				FMODEditorPreviewPlayer.SetParameter( _instance, kv.Key, kv.Value.CurrentText );
		}

		UpdatePlayStopButton();
	}

	private void TogglePlayStop()
	{
		if ( IsPlaying() )
			Stop();
		else
			Play();
	}

	public void Stop( bool immediate = false )
	{
		if ( _instance.isValid() )
			FMODEditorPreviewPlayer.Stop( _instance, immediate );

		_instance = default;
		UpdateTimelineUI( enabled: true, lengthMs: _eventLengthMs, seedKey: _resource?.EventPath );
		UpdatePlayStopButton();
	}

	private void UpdateTimelineUI( bool enabled, int lengthMs, string seedKey )
	{
		_timeline.Enabled = enabled;
		_timeline.LengthMs = lengthMs;
		_timeline.Minimum = 0f;
		_timeline.Maximum = Math.Max( 1, lengthMs );
		_timeline.Step = 1f;

		// Keep current scrub position when stopping/starting; just clamp to length.
		_timeline.Value = Math.Clamp( _timeline.Value, 0f, Math.Max( 1, lengthMs ) );
		_timeline.EnsureStaticWave( seedKey ?? string.Empty, lengthMs );
	}

	private void SeekTimeline()
	{
		if ( !_instance.isValid() || !_timeline.Enabled )
			return;

		_instance.setTimelinePosition( (int)_timeline.Value );
	}

	[EditorEvent.Frame]
	private void Tick()
	{
		UpdatePlayStopButton();

		if ( !_instance.isValid() || !_timeline.Enabled )
			return;

		if ( _instance.getTimelinePosition( out var posMs ) == FMOD.RESULT.OK )
		{
			_timeline.Value = posMs;
		}
	}
}

sealed class WaveformTimelineSlider : FloatSlider
{
	private readonly List<float> _samples;
	public int LengthMs { get; set; }
	public int MaxSamples { get; set; } = 512;
	public Color WaveColor { get; set; } = Color.Parse( "#8d368d" )!.Value;
	public Color BackgroundColor { get; set; } = Color.Parse( "#141414" )!.Value;

	private string _seedKey;
	private int _seedLengthMs;

	public WaveformTimelineSlider( Widget parent, List<float> samples ) : base( parent )
	{
		_samples = samples ?? [];
		Step = 1f;
		MinimumWidth = 100;
	}

	public void EnsureStaticWave( string seedKey, int lengthMs )
	{
		seedKey ??= string.Empty;
		lengthMs = Math.Max( 0, lengthMs );

		if ( _samples.Count > 0 && string.Equals( _seedKey, seedKey, StringComparison.Ordinal ) && _seedLengthMs == lengthMs )
			return;

		_seedKey = seedKey;
		_seedLengthMs = lengthMs;

		_samples.Clear();

		// Don't generate a waveform for empty/null events
		if ( lengthMs <= 0 || string.IsNullOrEmpty( seedKey ) )
			return;

		var seedLen = Math.Max( 1, lengthMs );
		var seed = HashSeed( seedKey, seedLen );
		var count = Math.Max( 64, Math.Min( MaxSamples, lengthMs / 2 ) );

		// Generate deterministic "fake waveform" that is stable per-event.
		var prev = 0.2f;
		for ( int i = 0; i < count; i++ )
		{
			seed = XorShift32( seed );
			var r = (seed & 0x00FFFFFF) / (float)0x01000000; // 0..~1

			// Shape it a bit
			var a = 0.05f + r * r * 0.95f;
			a = prev * 0.75f + a * 0.25f;
			prev = a;

			// fade at ends
			var t = i / (float)(count - 1);
			var fade = MathF.Min( t / 0.08f, (1f - t) / 0.08f );
			fade = Math.Clamp( fade, 0f, 1f );
			a *= 0.35f + 0.65f * fade;

			_samples.Add( Math.Clamp( a, 0f, 1f ) );
		}
	}

	private static uint HashSeed( string seedKey, int lengthMs )
	{
		unchecked
		{
			uint h = 2166136261;
			for ( int i = 0; i < seedKey.Length; i++ )
			{
				h ^= seedKey[i];
				h *= 16777619;
			}

			h ^= (uint)lengthMs;
			h *= 16777619;
			return h == 0 ? 1u : h;
		}
	}

	private static uint XorShift32( uint x )
	{
		x ^= x << 13;
		x ^= x >> 17;
		x ^= x << 5;
		return x;
	}

	protected override void OnPaint()
	{
		// Defensive: if we missed seeding, generate a static wave now so the control never appears empty.
		if ( _samples.Count <= 1 )
		{
			EnsureStaticWave( _seedKey ?? string.Empty, _seedLengthMs );
		}

		var rect = LocalRect;

		Paint.SetBrushAndPen( BackgroundColor, Theme.WidgetBackground, 1 );
		Paint.DrawRect( rect, 4 );

		var inner = rect.Shrink( 8, 10 );
		var midY = inner.Center.y;

		// Baseline
		Paint.SetPen( Color.White.WithAlpha( 0.08f ), 1 );
		Paint.DrawLine( new Vector2( inner.Left, midY ), new Vector2( inner.Right, midY ) );

		// Waveform (static per-event "shape")
		if ( _samples.Count > 1 )
		{
			Paint.SetPen( WaveColor.WithAlpha( 0.75f ), 1 );

			var count = _samples.Count;
			for ( int i = 0; i < count; i++ )
			{
				var x = inner.Left + (inner.Width * (i / (float)(count - 1)));
				var a = _samples[i];
				var h = a * (inner.Height * 0.48f);
				Paint.DrawLine( new Vector2( x, midY - h ), new Vector2( x, midY + h ) );
			}
		}

		// Playhead
		var denom = Math.Max( 1f, (Maximum - Minimum) );
		var t = (Value - Minimum) / denom;
		t = Math.Clamp( t, 0f, 1f );
		var px = inner.Left + inner.Width * t;

		Paint.SetPen( WaveColor.Lighten( 0.35f ).WithAlpha( 0.95f ), 2 );
		Paint.DrawLine( new Vector2( px, inner.Top ), new Vector2( px, inner.Bottom ) );

		// Time labels (top-right)
		if ( Enabled && LengthMs > 0 )
		{
			var cur = (Value / 1000f);
			var total = (LengthMs / 1000f);
			var text = $"{cur:0.00} / {total:0.00}s";
			Paint.SetPen( Color.White.WithAlpha( 0.75f ) );
			Paint.DrawText( rect.Shrink( 8, 6 ), text, TextFlag.RightTop );
		}
		else if ( Enabled )
		{
			Paint.SetPen( Color.White.WithAlpha( 0.5f ) );
			Paint.DrawText( rect.Shrink( 8, 6 ), "scrub", TextFlag.RightTop );
		}
	}
}
