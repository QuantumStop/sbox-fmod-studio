using System.IO;
using System;
using System.Linq;

namespace FMODSbox;

/// <summary>
/// Read-only representation of an FMOD Studio event for editor browsing.
/// Instances are generated from loaded bank metadata.
/// </summary>
[AssetType( Name = "FMOD Event", Extension = "fmevent", Category = "Sounds", Flags = AssetTypeFlags.NoEmbedding )]
public sealed class FMODEventResource : GameResource
{
	/// <summary>
	/// Implicitly converts this resource to its EventPath string.
	/// Allows passing FMODEventResource directly to FMODSound static methods.
	/// </summary>
	public static implicit operator string( FMODEventResource e ) => e?.EventPath ?? string.Empty;

	private static readonly Color TypePurple = "#8d368dff";
	private static Bitmap _iconLarge;
	private static Bitmap _iconSmall;

	/// <summary>
	/// FMOD event path, e.g. "event:/Weapons/Pistol/Fire".
	/// </summary>
	[Property, ReadOnly] public string EventPath { get; set; }

	/// <summary>
	/// Event GUID (string form).
	/// </summary>
	[Property, ReadOnly] public string Guid { get; set; }

	/// <summary>
	/// Event length in milliseconds (0 if unknown/looping).
	/// </summary>
	[Property, ReadOnly] public int LengthMs { get; set; }

	/// <summary>
	/// Whether this event is 3D.
	/// </summary>
	[Property, ReadOnly] public bool Is3D { get; set; }

	/// <summary>
	/// 3D min distance (only meaningful if <see cref="Is3D"/> is true).
	/// </summary>
	[Property, ReadOnly] public float MinDistance { get; set; }

	/// <summary>
	/// 3D max distance (only meaningful if <see cref="Is3D"/> is true).
	/// </summary>
	[Property, ReadOnly] public float MaxDistance { get; set; }

	/// <summary>
	/// Event parameters (basic metadata).
	/// </summary>
	[Property, ReadOnly] public List<FMODEventParameter> Parameters { get; set; } = [];

	public sealed class FMODEventParameter
	{
		[Property, ReadOnly] public string Name { get; set; }
		[Property, ReadOnly] public string Type { get; set; }
		[Property, ReadOnly] public float Min { get; set; }
		[Property, ReadOnly] public float Max { get; set; }
		[Property, ReadOnly] public float Default { get; set; }
		[Property, ReadOnly] public bool IsGlobal { get; set; }
		[Property, ReadOnly] public bool IsReadOnly { get; set; }
		[Property, ReadOnly] public bool IsLabeled { get; set; }
		[Property, ReadOnly] public bool IsDiscrete { get; set; }
	}

	public override Bitmap RenderThumbnail( ThumbnailOptions options )
	{
		var w = options.Width <= 0 ? 256 : options.Width;
		var h = options.Height <= 0 ? 256 : options.Height;

		var bitmap = new Bitmap( w, h );
		bitmap.Clear( new Color( 0, 0, 0, 0 ) );

		var bgTop = (Color)"#1b1b1bff";
		var bgBottom = (Color)"#0f0f0fff";
		bitmap.SetLinearGradient( new Vector2( 0, 0 ), new Vector2( 0, h ), Gradient.FromColors( bgTop, bgBottom ) );
		bitmap.DrawRoundRect( bitmap.Rect, 4 );

		var key = EventPath ?? ResourcePath ?? string.Empty;
		var samples = GenerateDeterministicWave( key, LengthMs, 256 );
		DrawWaveform( bitmap, samples, new Rect( w * 0.06f, h * 0.18f, w * 0.88f, h * 0.62f ), TypePurple.Lighten( 0.35f ) );

		var hasLabeled = Parameters?.Any( p => p?.IsLabeled == true ) == true;
		var hasFloat = Parameters?.Any( p => p?.IsLabeled != true ) == true;
		DrawParameterIcons( bitmap, w, h, hasFloat, hasLabeled );

		return bitmap;
	}

	private static void DrawParameterIcons( Bitmap bitmap, float w, float h, bool hasFloat, bool hasLabeled )
	{
		if ( bitmap is null )
			return;

		var size = MathX.Clamp( MathF.Min( w, h ), 14f, 28f );
		var gap = MathF.Max( 2f, size * 0.18f );
		var margin = MathF.Max( 6f, size * 0.35f );

		var rowW = size * 2 + gap;
		var x0 = w - margin - rowW;
		var y0 = margin;

		var floatRect = new Rect( x0, y0, size, size );
		var labeledRect = new Rect( x0 + size + gap, y0, size, size );

		var on = Color.White.WithAlpha( 0.85f );
		var off = Color.White.WithAlpha( 0.18f );

		var floatColor = hasFloat ? on : off;
		var labeledColor = hasLabeled ? on : off;

		bitmap.DrawText( new TextRendering.Scope( "tune", floatColor, size, "Material Icons" ), floatRect, TextFlag.Center | TextFlag.DontClip );
		bitmap.DrawText( new TextRendering.Scope( "label_important", labeledColor, size, "Material Icons" ), labeledRect, TextFlag.Center | TextFlag.DontClip );
	}

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		var bitmap = new Bitmap( width, height );
		bitmap.Clear( new Color( 0, 0, 0, 0 ) );

		bool useLarge = MathF.Max( width, height ) > 64;

		var icon = TryLoadIcon( large: useLarge );

		if ( icon != null )
		{
			var target = MathF.Min( width, height );

			var scale = MathF.Min(
				target / icon.Width,
				target / icon.Height
			);

			var w = icon.Width * scale;
			var h = icon.Height * scale;

			var x = MathF.Floor( (width - w) * 0.5f );
			var y = MathF.Floor( (height - h) * 0.5f );

			bitmap.DrawBitmap( icon, new Rect( x, y, w, h ) );
		}

		return bitmap;
	}

	private static Bitmap TryLoadIcon( bool large )
	{
		if ( large )
		{
			_iconLarge ??= TryLoadToolBitmap( "tools/images/assettypes/fmod_lg.tif" );
			return _iconLarge;
		}

		_iconSmall ??= TryLoadToolBitmap( "tools/images/assettypes/fmod_sm.tif" );
		return _iconSmall;
	}

	private static Bitmap TryLoadToolBitmap( string relativePath )
	{
		try
		{
			if ( string.IsNullOrWhiteSpace( relativePath ) )
				return null;

			var fullPath = FileSystem.Mounted.GetFullPath( relativePath );

			if ( string.IsNullOrWhiteSpace( fullPath ) || !File.Exists( fullPath ) )
				return null;

			return Bitmap.CreateFromBytes( File.ReadAllBytes( fullPath ) );
		}
		catch
		{
			return null;
		}
	}

	private static float[] GenerateDeterministicWave( string key, int lengthMs, int count )
	{
		count = Math.Max( 64, count );

		uint seed = 2166136261;
		unchecked
		{
			for ( int i = 0; i < key.Length; i++ )
			{
				seed ^= key[i];
				seed *= 16777619;
			}
			seed ^= (uint)Math.Max( 1, lengthMs );
			seed *= 16777619;
			if ( seed == 0 ) seed = 1;
		}

		static uint XorShift32( uint x )
		{
			x ^= x << 13;
			x ^= x >> 17;
			x ^= x << 5;
			return x;
		}

		var samples = new float[count];
		var prev = 0.25f;
		for ( int i = 0; i < count; i++ )
		{
			seed = XorShift32( seed );
			var r = (seed & 0x00FFFFFF) / (float)0x01000000;

			// Sound-file-like envelope: strong attack then decays.
			var t = i / (float)(count - 1);
			var env = MathF.Exp( -t * 3.0f );
			env = MathF.Max( env, 0.08f );

			var a = (0.08f + r * r * 0.92f) * env;
			a = prev * 0.65f + a * 0.35f;
			prev = a;
			samples[i] = Clamp01( a );
		}

		return samples;
	}

	private static void DrawWaveform( Bitmap bitmap, float[] samples, Rect rect, Color color )
	{
		if ( bitmap is null || samples is null || samples.Length < 2 )
			return;

		bitmap.SetAntialias( false );
		bitmap.SetPen( color.WithAlpha( 0.95f ), 1 );

		var midY = rect.Center.y;
		var halfH = rect.Height * 0.45f;
		var count = samples.Length;

		for ( int i = 0; i < count; i++ )
		{
			var x = rect.Left + (rect.Width * (i / (float)(count - 1)));
			var a = samples[i];
			var hh = a * halfH;
			bitmap.DrawLine( new Vector2( x, midY - hh ), new Vector2( x, midY + hh ) );
		}
	}

	private static float Clamp01( float v )
	{
		if ( v < 0f ) return 0f;
		if ( v > 1f ) return 1f;
		return v;
	}
}
