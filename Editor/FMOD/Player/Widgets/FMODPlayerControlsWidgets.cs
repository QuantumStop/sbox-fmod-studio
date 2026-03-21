namespace Editor.Assets;

using System;

sealed class TimerWidget : Widget
{
	private string _text = "00:00.000";
	private int _lastMs = -1;

	public TimerWidget( Widget parent ) : base( parent )
	{
		MinimumWidth = 80;
		MaximumWidth = 80;
	}

	public void SetMs( int ms )
	{
		if ( ms == _lastMs ) return;
		_lastMs = ms;
		var ts = TimeSpan.FromMilliseconds( ms );
		_text = $"{(int)ts.TotalMinutes:00}:{ts.Seconds:00}.{ts.Milliseconds:000}";
		Update();
	}

	public void Reset()
	{
		_lastMs = -1;
		_text = "00:00.000";
		Update();
	}

	protected override void OnPaint()
	{
		Paint.SetPen( Color.White.WithAlpha( 0.9f ) );
		Paint.SetFont( "monospace", 10f, 400 );
		Paint.DrawText( LocalRect, _text, TextFlag.Center );
	}
}

sealed class VolumeSliderWidget : Widget
{
	private sealed class MuteButton : Widget
	{
		public Func<string> IconName { get; set; }
		public Func<Color> IconColor { get; set; }
		public Action Pressed { get; set; }
		public float IconSize { get; set; } = 18f;

		public MuteButton( Widget parent ) : base( parent )
		{
			Cursor = CursorShape.Finger;
		}

		protected override void OnMousePress( MouseEvent e )
		{
			if ( !e.LeftMouseButton )
				return;

			e.Accepted = true;
			Pressed?.Invoke();
		}

		protected override void OnPaint()
		{
			var icon = IconName?.Invoke();
			if ( string.IsNullOrWhiteSpace( icon ) )
				return;

			var color = IconColor?.Invoke() ?? Color.White.WithAlpha( 0.85f );
			Paint.SetPen( color );
			Paint.DrawIcon( LocalRect, icon, IconSize, TextFlag.Center );
		}
	}

	public float Volume
	{
		get => _volume;
		set => _volume = Math.Clamp( value, 0f, 1f );
	}

	public bool Muted
	{
		get => _muted;
		set
		{
			_muted = value;
			Update();
		}
	}

	public Action OnVolumeChanged { get; set; }
	public Action OnMuteToggled { get; set; }

	private float _volume = 1f;
	private bool _muted = false;
	private bool _dragging = false;
	private readonly MuteButton _muteIcon;

	private static readonly Color ColLow = Color.Parse( "#4caf50" )!.Value;
	private static readonly Color ColMid = Color.Parse( "#ffeb3b" )!.Value;
	private static readonly Color ColHigh = Color.Parse( "#f44336" )!.Value;
	private static readonly Color ColBg = Color.Parse( "#1a1a1a" )!.Value;
	private static readonly Color ColBorder = Color.Parse( "#3a3a3a" )!.Value;

	private const float IconSize = 18f;
	private const float IconGap = 8f;

	public VolumeSliderWidget( Widget parent ) : base( parent )
	{
		MinimumHeight = 22f;
		MaximumHeight = 22f;
		Cursor = CursorShape.None;

		_muteIcon = new MuteButton( this )
		{
			IconSize = IconSize,
			IconName = () => _muted ? "volume_off" : _volume < 0.01f ? "volume_mute" : _volume < 0.5f ? "volume_down" : "volume_up",
			IconColor = () => _muted ? Color.Parse( "#888" )!.Value : Color.White.WithAlpha( 0.85f ),
			Pressed = () =>
			{
				_muted = !_muted;
				OnMuteToggled?.Invoke();
				Update();
			}
		};
	}

	private Rect GetIconRect() => new Rect( 0, 0, IconSize, Height );
	private Rect GetSliderRect() => new Rect( IconSize + IconGap, 2, Width - IconSize - IconGap, Height - 4 );

	private static float RectToVolume( Rect r, float x ) => Math.Clamp( (x - r.Left) / r.Width, 0f, 1f );

	private void UpdateCursor( Vector2 localPos )
	{
		if ( _dragging )
		{
			Cursor = CursorShape.SizeH;
			return;
		}

		Cursor = GetSliderRect().IsInside( localPos ) ? CursorShape.SizeH : CursorShape.None;
	}

	private void LayoutChildren()
	{
		var r = GetIconRect();
		_muteIcon.Position = new Vector2( r.Left, r.Top );
		_muteIcon.Size = new Vector2( r.Width, r.Height );
	}

	protected override void OnMousePress( MouseEvent e )
	{
		LayoutChildren();
		UpdateCursor( e.LocalPosition );

		var r = GetSliderRect();
		if ( e.LeftMouseButton && r.IsInside( e.LocalPosition ) )
		{
			_dragging = true;
			UpdateCursor( e.LocalPosition );
			_volume = RectToVolume( r, e.LocalPosition.x );
			e.Accepted = true;
			OnVolumeChanged?.Invoke();
			Update();
		}
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		LayoutChildren();
		UpdateCursor( e.LocalPosition );
		if ( !_dragging ) return;
		_volume = RectToVolume( GetSliderRect(), e.LocalPosition.x );
		OnVolumeChanged?.Invoke();
		Update();
	}

	protected override void OnMouseReleased( MouseEvent e )
	{
		_dragging = false;
		LayoutChildren();
		UpdateCursor( e.LocalPosition );
	}

	protected override void OnPaint()
	{
		var sliderRect = GetSliderRect();
		LayoutChildren();

		// Background
		Paint.SetBrushAndPen( ColBg, ColBorder, 2 );
		Paint.DrawRect( sliderRect, 3 );

		// Fill
		if ( !_muted && _volume > 0f )
		{
			var fillRect = sliderRect.Shrink( 1 );
			fillRect.Width *= _volume;

			// Gradient: green/yellow/red based on volume
			var t = _volume;
			Color fillColor;
			if ( t < 0.5f )
				fillColor = Color.Lerp( ColLow, ColMid, t * 2f );
			else
				fillColor = Color.Lerp( ColMid, ColHigh, (t - 0.5f) * 2f );

			Paint.SetBrushAndPen( fillColor.WithAlpha( 0.85f ), Color.Transparent );
			Paint.DrawRect( fillRect, 2 );

			// Highlight stripe
			var highlight = fillRect;
			highlight.Height *= 0.35f;
			Paint.SetBrushAndPen( Color.White.WithAlpha( 0.1f ), Color.Transparent );
			Paint.DrawRect( highlight, 2 );
		}

		// Percentage label
		var pct = _muted ? "MUTE" : $"{(int)Math.Round( _volume * 100f )}%";
		Paint.SetPen( Color.White.WithAlpha( 0.9f ) );
		Paint.SetDefaultFont( 8f, 600 );
		Paint.DrawText( sliderRect, pct, TextFlag.Center );
	}
}

sealed class GradientLabel : Widget
{
	private string _text = "";
	public string Text
	{
		get => _text;
		set { if ( _text != value ) { _text = value; Update(); } }
	}

	private Color _accentColor = Color.Parse( "#2c6c2c" )!.Value;
	public Color AccentColor
	{
		get => _accentColor;
		set { _accentColor = value; Update(); }
	}

	public GradientLabel( Widget parent ) : base( parent )
	{
		SetStyles( "text-transform: uppercase; letter-spacing: 1px;" );
	}

	protected override void OnPaint()
	{
		var rect = LocalRect;
		var w = (int)rect.Width;
		var h = (int)rect.Height;
		if ( w <= 0 || h <= 0 ) return;

		var from = _accentColor.WithAlpha( 0.6f );
		var mid = _accentColor.WithAlpha( 0.15f );
		var to = _accentColor.WithAlpha( 0f );

		for ( int i = 0; i < w; i++ )
		{
			var t = i / (float)w;
			var adjustedT = t - 0.15f;
			Color col = adjustedT < 0.43f
				? Color.Lerp( from, mid, Math.Clamp( adjustedT / 0.4f, 0f, 1f ) )
				: Color.Lerp( mid, to, Math.Clamp( (adjustedT - 0.5f) / 0.4f, 0f, 1f ) );
			Paint.SetPen( col, 1 );
			Paint.DrawLine( new Vector2( rect.Left + i, rect.Top ), new Vector2( rect.Left + i, rect.Bottom ) );

			var sheenAlpha = Math.Clamp( (1f - t * 2f) * 0.25f, 0f, 0.25f );
			if ( sheenAlpha > 0f )
			{
				Paint.SetPen( Color.White.WithAlpha( sheenAlpha ), 1 );
				Paint.DrawLine( new Vector2( rect.Left + i, rect.Top ), new Vector2( rect.Left + i + 1, rect.Top ) );
			}
		}

		Paint.SetPen( Color.White.WithAlpha( 0.1f ), 2 );
		Paint.DrawLine( new Vector2( rect.Left, rect.Top ), new Vector2( rect.Left, rect.Bottom ) );

		Paint.SetPen( Color.White.WithAlpha( 0.85f ) );
		Paint.SetDefaultFont( 7f, 600 );
		Paint.DrawText( rect.Shrink( 6, 0 ), _text, TextFlag.LeftCenter );
	}
}

sealed class ThemedFloatSlider : Widget
{
	public float Value
	{
		get => _value;
		set
		{
			_value = Math.Clamp( value, Minimum, Maximum );
			Update();
		}
	}

	public float Minimum { get; set; } = 0f;
	public float Maximum { get; set; } = 1f;
	public float Step { get; set; } = 0.01f;
	public float TrackPadding { get; set; } = 2f;
	public Action OnValueEdited { get; set; }

	private float _value;
	private bool _dragging;

	private static readonly Color ColBg = Color.Parse( "#1a1a1a" )!.Value;
	private static readonly Color ColBorder = Color.Parse( "#3a3a3a" )!.Value;
	private static readonly Color ColFill = Color.Parse( "#644e64" )!.Value;
	private static readonly Color ColHandle = Color.Parse( "#cac4ca" )!.Value;

	public ThemedFloatSlider( Widget parent ) : base( parent )
	{
		MinimumHeight = 16f;
		MaximumHeight = 16f;
		Cursor = CursorShape.SizeH;
	}

	private float LocalToValue( float x )
	{
		var innerLeft = TrackPadding;
		var innerWidth = Width - TrackPadding * 2f;
		var t = Math.Clamp( (x - innerLeft) / innerWidth, 0f, 1f );
		var raw = Minimum + t * (Maximum - Minimum);
		if ( Step > 0f )
			raw = MathF.Round( raw / Step ) * Step;
		return Math.Clamp( raw, Minimum, Maximum );
	}

	protected override void OnMousePress( MouseEvent e )
	{
		if ( !e.LeftMouseButton ) return;
		_dragging = true;
		Value = LocalToValue( e.LocalPosition.x );
		OnValueEdited?.Invoke();
		Update();
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		if ( !_dragging ) return;
		Value = LocalToValue( e.LocalPosition.x );
		OnValueEdited?.Invoke();
		Update();
	}

	protected override void OnMouseReleased( MouseEvent e )
	{
		_dragging = false;
	}

	protected override void OnPaint()
	{
		var rect = LocalRect.Shrink( 0, 3 );
		var innerLeft = rect.Left + TrackPadding;
		var innerRight = rect.Right - TrackPadding;
		var innerWidth = innerRight - innerLeft;

		var t = Maximum > Minimum ? (Value - Minimum) / (Maximum - Minimum) : 0f;
		t = Math.Clamp( t, 0f, 1f );
		var handleX = innerLeft + innerWidth * t;

		// Background
		Paint.SetBrushAndPen( ColBg, ColBorder, 2 );
		Paint.DrawRect( rect, 2 );

		// Gradient fill
		if ( t > 0f )
		{
			var fillWidth = (int)(innerWidth * t);
			var sheenH = (rect.Height - 2f) * 0.4f;
			for ( int i = 0; i < fillWidth; i++ )
			{
				var ft = i / (float)Math.Max( 1, fillWidth );
				Paint.SetPen( Color.Lerp( ColFill.WithAlpha( 0.6f ), ColFill.WithAlpha( 0.25f ), ft ), 1 );
				Paint.DrawLine( new Vector2( innerLeft + i, rect.Top + 1f ), new Vector2( innerLeft + i, rect.Bottom - 1f ) );
				Paint.SetPen( Color.White.WithAlpha( (1f - ft) * 0.12f ), 1 );
				Paint.DrawLine( new Vector2( innerLeft + i, rect.Top + 1f ), new Vector2( innerLeft + i, rect.Top + 1f + sheenH ) );
			}
		}

		// Handle line
		Paint.SetPen( ColHandle.WithAlpha( 0.9f ), 2 );
		Paint.DrawLine( new Vector2( handleX, rect.Top + 2f ), new Vector2( handleX, rect.Bottom - 2f ) );
	}
}
