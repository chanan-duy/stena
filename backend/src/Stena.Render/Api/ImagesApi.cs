using Microsoft.AspNetCore.Http.HttpResults;
using SkiaSharp;
using Stena.Render.Data;
using Stena.Render.Drawing;

namespace Stena.Render.Api;

public static class ImagesApi
{
	private const int ThumbnailSide = 128;

	public static void MapImages(this IEndpointRouteBuilder app)
	{
		var walls = app.MapGroup("/render/walls/{id:int}").WithTags("Images");
		walls.MapGet("/image.png", ImageAsync);
		walls.MapGet("/thumbnail.png", ThumbnailAsync);
	}

	private static async Task<Results<FileContentHttpResult, NotFound>> ImageAsync(
		int id,
		DateTimeOffset? at,
		int? scale,
		Sources sources,
		TimeProvider time,
		CancellationToken ct
	)
	{
		return await DrawAsync(id, at ?? time.GetUtcNow(), (w, p) => Painter.Draw(w.Width, w.Height, w.Palette, p, Math.Clamp(scale ?? 1, 1, 16)),
			sources, ct);
	}

	private static async Task<Results<FileContentHttpResult, NotFound>> ThumbnailAsync(int id, Sources sources, TimeProvider time, CancellationToken ct)
	{
		return await DrawAsync(id, time.GetUtcNow(), (w, p) => Painter.Fit(w.Width, w.Height, w.Palette, p, ThumbnailSide), sources, ct);
	}

	private static async Task<Results<FileContentHttpResult, NotFound>> DrawAsync(
		int id,
		DateTimeOffset at,
		Func<WallInfo, byte[], SKBitmap> draw,
		Sources sources,
		CancellationToken ct
	)
	{
		var wall = await sources.WallAsync(id, ct);
		if (wall is null)
		{
			return TypedResults.NotFound();
		}

		using var bitmap = draw(wall, await sources.PixelsAsync(id, at, ct));
		return TypedResults.File(Painter.Png(bitmap), "image/png");
	}
}
