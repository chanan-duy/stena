using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Stena.Canvas.Data;
using Stena.Canvas.Domain;
using Stena.Canvas.Redis;
using Stena.ServiceDefaults;

namespace Stena.Canvas.Api;

public record WallView(
	int Id,
	string Name,
	int Width,
	int Height,
	string[] Palette,
	int ChargeSeconds,
	int MaxCharges,
	DateTimeOffset StartsAt,
	DateTimeOffset EndsAt,
	WallStatus Status
)
{
	public static WallView From(Wall w, DateTimeOffset now)
	{
		return new WallView(w.Id, w.Name, w.Width, w.Height, w.Palette, w.ChargeSeconds, w.MaxCharges, w.StartsAt, w.EndsAt, w.StatusAt(now));
	}
}

public static class WallsApi
{
	// Two edits interleaving their save and Redis sync would leave Redis with the older rules.
	private static readonly SemaphoreSlim Edits = new(1, 1);

	public static void MapWalls(this IEndpointRouteBuilder app)
	{
		var walls = app.MapGroup("/walls").WithTags("Walls");
		walls.MapGet("", ListAsync);
		walls.MapGet("/{id:int}", GetAsync);
		walls.MapPost("", CreateAsync).RequireAuthorization(Roles.Admin);
		walls.MapPut("/{id:int}", EditAsync).RequireAuthorization(Roles.Admin);
		walls.MapGet("/{id:int}/bitmap", BitmapAsync);
	}

	private static async Task<Ok<WallView[]>> ListAsync(CanvasDb db, TimeProvider time, CancellationToken ct)
	{
		var walls = await db.Walls.OrderBy(w => w.Id).ToListAsync(ct);
		var now = time.GetUtcNow();
		return TypedResults.Ok(walls.Select(w => WallView.From(w, now)).ToArray());
	}

	private static async Task<Results<Ok<WallView>, NotFound>> GetAsync(int id, CanvasDb db, TimeProvider time, CancellationToken ct)
	{
		var wall = await db.Walls.FindAsync([id], ct);
		return wall is null ? TypedResults.NotFound() : TypedResults.Ok(WallView.From(wall, time.GetUtcNow()));
	}

	private static async Task<Created<WallView>> CreateAsync(
		WallSpec spec,
		CanvasDb db,
		WallStore store,
		TimeProvider time,
		CancellationToken ct
	)
	{
		var wall = Wall.Create(spec);
		db.Walls.Add(wall);
		await db.SaveChangesAsync(ct);
		await store.SyncAsync(wall);
		return TypedResults.Created($"/walls/{wall.Id}", WallView.From(wall, time.GetUtcNow()));
	}

	private static async Task<Results<Ok<WallView>, NotFound, ProblemHttpResult>> EditAsync(
		int id,
		WallSpec spec,
		CanvasDb db,
		WallStore store,
		TimeProvider time,
		CancellationToken ct
	)
	{
		await Edits.WaitAsync(ct);
		try
		{
			var wall = await db.Walls.FindAsync([id], ct);
			if (wall is null)
			{
				return TypedResults.NotFound();
			}

			var now = time.GetUtcNow();
			if (!wall.TryEdit(spec, now))
			{
				return TypedResults.Problem("Start, size and palette are frozen once the wall starts", statusCode: StatusCodes.Status409Conflict);
			}

			await db.SaveChangesAsync(ct);
			await store.SyncAsync(wall);
			return TypedResults.Ok(WallView.From(wall, now));
		}
		finally
		{
			Edits.Release();
		}
	}

	private static async Task<Results<FileContentHttpResult, NotFound>> BitmapAsync(int id, WallStore store, HttpResponse res)
	{
		if (await store.BitmapAsync(id) is not var (pixels, streamId))
		{
			return TypedResults.NotFound();
		}

		res.Headers["X-Stream-Id"] = streamId;
		return TypedResults.File(pixels, "application/octet-stream");
	}
}
