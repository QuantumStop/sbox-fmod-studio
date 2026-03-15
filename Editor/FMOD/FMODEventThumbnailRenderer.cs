namespace Editor;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Sandbox;

/// <summary>
/// High-priority thumbnail renderer for FMOD event resources.
/// Avoids AssetPreview-based thumbnails.
/// </summary>
public static class FMODEventThumbnailRenderer
{
	[Asset.ThumbnailRenderer( Priority = 1000 )]
	public static Task<Bitmap> RenderThumbnail( Asset asset )
	{
		var ext = asset?.AssetType?.FileExtension;
		if ( ext != "fmevent" && ext != "fmodevent" )
			return Task.FromResult<Bitmap>( null );

		var (eventPath, lengthMs) = TryReadMeta( asset.AbsolutePath );
		return Task.FromResult( RenderThumbBitmap( eventPath ?? asset.Name, lengthMs, 256, 256 ) );
	}

	private static (string EventPath, int LengthMs) TryReadMeta( string absolutePath )
	{
		try
		{
			if ( string.IsNullOrWhiteSpace( absolutePath ) || !File.Exists( absolutePath ) )
				return default;

			using var doc = JsonDocument.Parse( File.ReadAllText( absolutePath ) );
			var root = doc.RootElement;

			string path = null;
			int len = 0;

			if ( root.TryGetProperty( "EventPath", out var p ) && p.ValueKind == JsonValueKind.String )
				path = p.GetString();

			if ( root.TryGetProperty( "LengthMs", out var l ) && l.ValueKind == JsonValueKind.Number )
				len = l.GetInt32();

			return (path, len);
		}
		catch
		{
			return default;
		}
	}

	private static Bitmap RenderThumbBitmap( string key, int lengthMs, int w, int h )
	{
		var purple = (Color)"#8d368dff";
		var bgTop = (Color)"#1b1b1bff";
		var bgBottom = (Color)"#0f0f0fff";

		var bitmap = new Bitmap( w, h );
		bitmap.Clear( new Color( 0, 0, 0, 0 ) );

		bitmap.SetLinearGradient( new Vector2( 0, 0 ), new Vector2( 0, h ), Gradient.FromColors( bgTop, bgBottom ) );
		bitmap.DrawRoundRect( bitmap.Rect, 4 );

		var stripH = MathF.Max( 6f, h * 0.035f );
		bitmap.SetFill( purple.WithAlpha( 0.85f ) );
		bitmap.DrawRect( new Rect( 0, h - stripH, w, h ) );

		var samples = GenerateDeterministicWave( key ?? string.Empty, lengthMs, 256 );
		DrawWaveform( bitmap, samples, new Rect( w * 0.06f, h * 0.18f, w * 0.94f, h * 0.80f ), purple.Lighten( 0.35f ) );

		return bitmap;
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
