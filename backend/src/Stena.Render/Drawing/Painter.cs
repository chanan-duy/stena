using System.Globalization;
using SkiaSharp;

namespace Stena.Render.Drawing;

public static class Painter
{
	public static SKBitmap Draw(int width, int height, string[] palette, byte[] pixels, int scale)
	{
		var colors = palette.Select(c => new SKColor(uint.Parse(c[1..], NumberStyles.HexNumber) | 0xff000000)).ToArray();
		var bitmap = new SKBitmap(width * scale, height * scale, SKColorType.Rgba8888, SKAlphaType.Opaque);
		for (var y = 0; y < bitmap.Height; y++)
		{
			for (var x = 0; x < bitmap.Width; x++)
			{
				bitmap.SetPixel(x, y, colors[pixels[y / scale * width + x / scale]]);
			}
		}

		return bitmap;
	}

	public static SKBitmap Fit(int width, int height, string[] palette, byte[] pixels, int side)
	{
		var longest = Math.Max(width, height);
		if (longest <= side)
		{
			return Draw(width, height, palette, pixels, side / longest);
		}

		using var full = Draw(width, height, palette, pixels, 1);
		var info = new SKImageInfo(Math.Max(1, width * side / longest), Math.Max(1, height * side / longest), SKColorType.Rgba8888, SKAlphaType.Opaque);
		return full.Resize(info, new SKSamplingOptions(SKFilterMode.Nearest));
	}

	public static byte[] Png(SKBitmap bitmap)
	{
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}
}
