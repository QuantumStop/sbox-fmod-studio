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
		if ( ext != "fmevent" )
			return Task.FromResult<Bitmap>( null );

		var (eventPath, lengthMs, hasFloat, hasLabeled) = TryReadMeta( asset.AbsolutePath );
		return Task.FromResult( RenderThumbBitmap( eventPath ?? asset.Name, lengthMs, hasFloat, hasLabeled, 256, 256 ) );
	}

	private static (string EventPath, int LengthMs, bool HasFloat, bool HasLabeled) TryReadMeta( string absolutePath )
	{
		try
		{
			if ( string.IsNullOrWhiteSpace( absolutePath ) || !File.Exists( absolutePath ) )
				return default;

			using var doc = JsonDocument.Parse( File.ReadAllText( absolutePath ) );
			var root = doc.RootElement;

			string path = null;
			int len = 0;
			var hasFloat = false;
			var hasLabeled = false;

			if ( root.TryGetProperty( "EventPath", out var p ) && p.ValueKind == JsonValueKind.String )
				path = p.GetString();

			if ( root.TryGetProperty( "LengthMs", out var l ) && l.ValueKind == JsonValueKind.Number )
				len = l.GetInt32();

			if ( root.TryGetProperty( "Parameters", out var parms ) && parms.ValueKind == JsonValueKind.Array )
			{
				foreach ( var item in parms.EnumerateArray() )
				{
					if ( item.ValueKind != JsonValueKind.Object )
						continue;

					var isLabeled = false;
					if ( item.TryGetProperty( "IsLabeled", out var il ) && il.ValueKind == JsonValueKind.True )
						isLabeled = true;

					if ( isLabeled ) hasLabeled = true;
					else hasFloat = true;

					if ( hasFloat && hasLabeled )
						break;
				}
			}

			return (path, len, hasFloat, hasLabeled);
		}
		catch
		{
			return default;
		}
	}

	private static Bitmap RenderThumbBitmap( string key, int lengthMs, bool hasFloat, bool hasLabeled, int w, int h )
	{
		var purple = (Color)"#8d368dff";
		var bgTop = (Color)"#1b1b1bff";
		var bgBottom = (Color)"#0f0f0fff";

		var bitmap = new Bitmap( w, h );
		bitmap.Clear( new Color( 0, 0, 0, 0 ) );

		bitmap.SetLinearGradient( new Vector2( 0, 0 ), new Vector2( 0, h ), Gradient.FromColors( bgTop, bgBottom ) );
		bitmap.DrawRoundRect( bitmap.Rect, 4 );

		var samples = GenerateDeterministicWave( key ?? string.Empty, lengthMs, 256 );
		DrawWaveform( bitmap, samples, new Rect( w * 0.06f, h * 0.18f, w * 0.88f, h * 0.62f ), purple.Lighten( 0.35f ) );

		DrawParameterIcons( bitmap, w, h, hasFloat, hasLabeled );

		return bitmap;
	}

	private static void DrawParameterIcons( Bitmap bitmap, float w, float h, bool hasFloat, bool hasLabeled )
	{
		if ( !bitmap.IsValid() )
			return;

		var size = MathX.Clamp( MathF.Min( w, h ) * 0.085f, 12f, 18f );
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
		bitmap.DrawText( new TextRendering.Scope( "label", labeledColor, size, "Material Icons" ), labeledRect, TextFlag.Center | TextFlag.DontClip );
	}

	private static float[] GenerateDeterministicWave( string key, int lengthMs, int count )
	{
		key ??= string.Empty;
		lengthMs = Math.Max( 0, lengthMs );
		count = Math.Max( 64, count );

		static uint XorShift32( uint x )
		{
			x ^= x << 13;
			x ^= x >> 17;
			x ^= x << 5;
			return x;
		}

		static uint HashSeed( string seedKey, int seedLen )
		{
			unchecked
			{
				uint h = 2166136261;
				for ( int i = 0; i < seedKey.Length; i++ ) { h ^= seedKey[i]; h *= 16777619; }
				h ^= (uint)seedLen;
				h *= 16777619;
				return h == 0 ? 1u : h;
			}
		}

		var seedLen = Math.Max( 1, lengthMs );
		var seed = HashSeed( key, seedLen );

		var samples = new float[count];
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
