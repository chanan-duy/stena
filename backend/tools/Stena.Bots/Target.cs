using System.Globalization;
using SkiaSharp;

namespace Stena.Bots;

public record Wall(int Id, string Name, int Width, int Height, string[] Palette, string Status);

public static class Target
{
	public static Dictionary<(int X, int Y), byte> Load(string path, Wall wall, int left, int top)
	{
		using var image = SKBitmap.Decode(path) ?? throw new InvalidOperationException($"Cannot read {path}");
		var palette = wall.Palette.Select(c => uint.Parse(c[1..], NumberStyles.HexNumber)).ToArray();
		var target = new Dictionary<(int, int), byte>();
		for (var y = 0; y < image.Height && top + y < wall.Height; y++)
		{
			for (var x = 0; x < image.Width && left + x < wall.Width; x++)
			{
				var c = image.GetPixel(x, y);
				if (c.Alpha >= 128 && left + x >= 0 && top + y >= 0)
				{
					target[(left + x, top + y)] = Nearest(palette, c);
				}
			}
		}

		return target;
	}

	private static byte Nearest(uint[] palette, SKColor c)
	{
		var best = 0;
		var bestDistance = int.MaxValue;
		for (var i = 0; i < palette.Length; i++)
		{
			int r = (int)(palette[i] >> 16) - c.Red, g = (int)(palette[i] >> 8 & 0xff) - c.Green, b = (int)(palette[i] & 0xff) - c.Blue;
			var distance = r * r + g * g + b * b;
			if (distance < bestDistance)
			{
				(best, bestDistance) = (i, distance);
			}
		}

		return (byte)best;
	}
}
