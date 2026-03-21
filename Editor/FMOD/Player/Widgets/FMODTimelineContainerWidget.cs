namespace Editor.Assets;

using System;

sealed class TimelineContainerWidget ( Widget parent ) : Widget ( parent )
{
	private void EnsureChildrenFill()
	{
		// Ensure overlay children (waveform + slider) always match our size.
		// This also handles the "first frame" case where we may already have a size
		// before children are added (so OnResize doesn't fire after adding them).
		if ( Width <= 0 || Height <= 0 ) return;

		foreach ( var child in Children )
		{
			if ( child.Position.x != 0 || child.Position.y != 0 )
				child.Position = new Vector2( 0, 0 );

			child.MaximumWidth = Width;
			child.MaximumHeight = Height;

			if ( child.Width != Width )
				child.Width = Width;

			if ( child.Height != Height )
				child.Height = Height;
		}
	}

	protected override void OnResize()
	{
		EnsureChildrenFill();
    }

	protected override void OnPaint()
	{
		EnsureChildrenFill();
		base.OnPaint();
	}
}

sealed class WaveformBackgroundWidget : Widget
{
	private readonly List<float> _samples;
	public Color WaveColor { get; set; } = Color.Parse( "#8d368d" )!.Value;
	public Color BackgroundColor { get; set; } = Color.Parse( "#141414" )!.Value;
	public int LengthMs { get; set; }

	public WaveformBackgroundWidget( Widget parent, List<float> samples ) : base( parent )
	{
		_samples = samples;
		HorizontalSizeMode = SizeMode.CanGrow;
		VerticalSizeMode = SizeMode.CanGrow;
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( BackgroundColor, Theme.WidgetBackground, 1 );
		Paint.DrawRect( LocalRect, 4 );

		var inner = LocalRect.Shrink( 0, 0 );
		var midY = inner.Center.y;

		Paint.SetPen( Color.White.WithAlpha( 0.08f ), 1 );
		Paint.DrawLine( new Vector2( inner.Left, midY ), new Vector2( inner.Right, midY ) );

		if ( _samples.Count > 1 )
		{
			Paint.SetPen( WaveColor.WithAlpha( 0.75f ), 1 );
			var count = _samples.Count;
			for ( int i = 0; i < count; i++ )
			{
				var x = inner.Left + (inner.Width * (i / (float)(count - 1)));
				var a = _samples[i];
				var hh = a * (inner.Height * 0.48f);
				Paint.DrawLine( new Vector2( x, midY - hh ), new Vector2( x, midY + hh ) );
			}
		}

		if ( LengthMs > 0 )
		{
			Paint.SetPen( Color.White.WithAlpha( 0.75f ) );
			Paint.SetDefaultFont( 9f, 400 );
			Paint.DrawText( LocalRect.Shrink( 8, 6 ), $"{LengthMs / 1000f:0.00}s", TextFlag.RightTop );
		}
	}
}

sealed class WaveformTimelineSlider : FloatSlider
{
	private readonly List<float> _samples;
	private readonly WaveformBackgroundWidget _bg;

	public int LengthMs
	{
		get => _bg.LengthMs;
		set => _bg.LengthMs = value;
	}

	public int MaxSamples { get; set; } = 512;
	public Color WaveColor { get; set; } = Color.Parse( "#8d368d" )!.Value;

	private float _playheadPos;
	private string _seedKey;
	private int _seedLengthMs;

	public WaveformTimelineSlider( Widget parent, List<float> samples, WaveformBackgroundWidget bg ) : base( parent )
	{
		_samples = samples ?? [];
		_bg = bg;
		Step = 1f;
		MinimumWidth = 100;
		TranslucentBackground = true;
		NoSystemBackground = true;
	}

	public void EnsureStaticWave( string seedKey, int lengthMs )
	{
		seedKey ??= string.Empty;
		lengthMs = Math.Max( 0, lengthMs );

		// If we don't have a stable seed, keep whatever waveform we already have rather than blanking.
		if ( string.IsNullOrEmpty( seedKey ) )
			return;

		if ( _samples.Count > 0 && string.Equals( _seedKey, seedKey, StringComparison.Ordinal ) && _seedLengthMs == lengthMs )
			return;

		_seedKey = seedKey;
		_seedLengthMs = lengthMs;
		_samples.Clear();

		var seedLen = Math.Max( 1, lengthMs );
		var seed = HashSeed( seedKey, seedLen );
		var count = lengthMs > 0
			? Math.Max( 64, Math.Min( MaxSamples, lengthMs / 2 ) )
			: Math.Max( 64, Math.Min( MaxSamples, 256 ) );

		var prev = 0.2f;
		for ( int i = 0; i < count; i++ )
		{
			seed = XorShift32( seed );
			var r = (seed & 0x00FFFFFF) / (float)0x01000000;
			var a = 0.05f + r * r * 0.95f;
			a = prev * 0.75f + a * 0.25f;
			prev = a;
			var t = i / (float)(count - 1);
			var fade = MathF.Min( t / 0.08f, (1f - t) / 0.08f );
			fade = Math.Clamp( fade, 0f, 1f );
			a *= 0.35f + 0.65f * fade;
			_samples.Add( Math.Clamp( a, 0f, 1f ) );
		}

		_bg.Update();
	}

	public void SetPlayheadPos( float posMs )
	{
		var totalMs = Math.Max( 1f, Maximum - Minimum );
		var oldPx = (int)(_playheadPos / totalMs * Width);
		var newPx = (int)(posMs / totalMs * Width);
		if ( oldPx == newPx ) return;
		_playheadPos = posMs;
		Value = posMs;
		Update(); // only repaint the playhead line
	}

	private static uint HashSeed( string seedKey, int lengthMs )
	{
		unchecked
		{
			uint h = 2166136261;
			for ( int i = 0; i < seedKey.Length; i++ ) { h ^= seedKey[i]; h *= 16777619; }
			h ^= (uint)lengthMs;
			h *= 16777619;
			return h == 0 ? 1u : h;
		}
	}

	private static uint XorShift32( uint x )
	{
		x ^= x << 13; x ^= x >> 17; x ^= x << 5;
		return x;
	}

	protected override void OnPaint()
	{
		var inner = LocalRect.Shrink( 8, 0 );
		var denom = Math.Max( 1f, Maximum - Minimum );
		var t = Math.Clamp( (_playheadPos - Minimum) / denom, 0f, 1f );
		var px = inner.Left + inner.Width * t;
		Paint.SetPen( WaveColor.Lighten( 0.1f ).WithAlpha( 0.8f ), 3 );
		Paint.DrawLine( new Vector2( px, inner.Top ), new Vector2( px, inner.Bottom ) );
	}
}
