using SkiaSharp;
using Stena.Render.Drawing;
using Xunit;

namespace Stena.Render.Tests;

public class PainterTests
{
	private static readonly string[] Palette = ["#ffffff", "#ff0000"];

	[Fact]
	public void Draw_ScalesPixelsWithPalette()
	{
		using var bitmap = Painter.Draw(2, 1, Palette, [1, 0], 2);

		Assert.Equal((4, 2), (bitmap.Width, bitmap.Height));
		Assert.Equal(SKColors.Red, bitmap.GetPixel(1, 1));
		Assert.Equal(SKColors.White, bitmap.GetPixel(2, 0));
	}

	[Fact]
	public void Fit_ShrinksLargeWallToSide()
	{
		using var bitmap = Painter.Fit(300, 150, Palette, new byte[300 * 150], 128);

		Assert.Equal((128, 64), (bitmap.Width, bitmap.Height));
	}
}
