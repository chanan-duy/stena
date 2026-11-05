using Microsoft.AspNetCore.Http.HttpResults;
using Stena.History.Data;
using Stena.History.Domain;

namespace Stena.History.Api;

public record PixelInfo(int X, int Y, int Color, Guid UserId, string UserName, DateTimeOffset At);

public static class HistoryApi
{
	public static void MapHistory(this IEndpointRouteBuilder app)
	{
		var walls = app.MapGroup("/history/walls/{id:int}").WithTags("History");
		walls.MapGet("/pixels/{x:int}/{y:int}", PixelAsync);
		walls.MapGet("/area", AreaAsync);
	}

	private static async Task<Results<Ok<PixelInfo>, NotFound, ProblemHttpResult>> PixelAsync(
		int id,
		int x,
		int y,
		DateTimeOffset? at,
		Timeline timeline,
		TimeProvider time,
		CancellationToken ct
	)
	{
		var moment = at ?? time.GetUtcNow();
		if (!await timeline.CatchUpAsync(id, moment, ct))
		{
			return TypedResults.Problem("History is behind the wall", statusCode: StatusCodes.Status503ServiceUnavailable);
		}

		var p = await timeline.PixelAsync(id, x, y, moment, ct);
		return p is null
			? TypedResults.NotFound()
			: TypedResults.Ok(new PixelInfo(p.X, p.Y, p.Color, p.UserId, p.UserName, DateTimeOffset.FromUnixTimeMilliseconds(p.Ms)));
	}

	private static async Task<Results<FileContentHttpResult, NotFound, ProblemHttpResult>> AreaAsync(
		int id,
		int? x,
		int? y,
		int? w,
		int? h,
		DateTimeOffset? at,
		Timeline timeline,
		TimeProvider time,
		CancellationToken ct
	)
	{
		if (await timeline.SizeAsync(id) is not { } size)
		{
			return TypedResults.NotFound();
		}

		var area = new Area(x ?? 0, y ?? 0, w ?? size.Width, h ?? size.Height);
		if (!area.Inside(size.Width, size.Height))
		{
			return TypedResults.Problem("Area is outside the wall", statusCode: StatusCodes.Status400BadRequest);
		}

		var moment = at ?? time.GetUtcNow();
		if (!await timeline.CatchUpAsync(id, moment, ct))
		{
			return TypedResults.Problem("History is behind the wall", statusCode: StatusCodes.Status503ServiceUnavailable);
		}

		return TypedResults.File(await timeline.AreaAsync(id, area, moment, ct), "application/octet-stream");
	}
}
