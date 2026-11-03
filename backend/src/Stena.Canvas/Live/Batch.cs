using System.Buffers.Binary;

namespace Stena.Canvas.Live;

public readonly record struct Pixel(int X, int Y, int Color);

public static class Batch
{
	public const int PixelSize = 5;

	public static byte[] Encode(IReadOnlyList<Pixel> pixels)
	{
		var bytes = new byte[pixels.Count * PixelSize];
		for (var i = 0; i < pixels.Count; i++)
		{
			var span = bytes.AsSpan(i * PixelSize);
			BinaryPrimitives.WriteUInt16LittleEndian(span, (ushort)pixels[i].X);
			BinaryPrimitives.WriteUInt16LittleEndian(span[2..], (ushort)pixels[i].Y);
			span[4] = (byte)pixels[i].Color;
		}

		return bytes;
	}
}
