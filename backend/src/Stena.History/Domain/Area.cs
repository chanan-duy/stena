namespace Stena.History.Domain;

public readonly record struct Area(int X, int Y, int Width, int Height)
{
	public bool Inside(int width, int height)
	{
		return X >= 0 && Y >= 0 && Width > 0 && Height > 0 && X < width && Y < height && Width <= width - X && Height <= height - Y;
	}

	public byte[] Crop(byte[] wall, int wallWidth)
	{
		var area = new byte[Width * Height];
		for (var row = 0; row < Height; row++)
		{
			wall.AsSpan((Y + row) * wallWidth + X, Width).CopyTo(area.AsSpan(row * Width));
		}

		return area;
	}

	public void Apply(byte[] area, int x, int y, byte color)
	{
		area[(y - Y) * Width + x - X] = color;
	}
}
