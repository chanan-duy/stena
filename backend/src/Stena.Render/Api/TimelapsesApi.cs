using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Stena.Render.Data;
using Stena.Render.Domain;
using Stena.Render.Drawing;
using Stena.ServiceDefaults;

namespace Stena.Render.Api;

public record TimelapseView(Guid Id, int WallId, TimelapseStatus Status, string? Error, string? Video);

public static class TimelapsesApi
{
	public static void MapTimelapses(this IEndpointRouteBuilder app)
	{
		var render = app.MapGroup("/render").WithTags("Timelapses");
		render.MapPost("/walls/{id:int}/timelapse", StartAsync).RequireAuthorization(Roles.Admin);
		render.MapGet("/walls/{id:int}/timelapse", LatestAsync);
		render.MapGet("/timelapses/{id:guid}.mp4", Video);
	}

	private static async Task<Results<Accepted<TimelapseView>, NotFound>> StartAsync(
		int id,
		RenderDb db,
		Sources sources,
		TimeProvider time,
		CancellationToken ct
	)
	{
		if (await sources.WallAsync(id, ct) is null)
		{
			return TypedResults.NotFound();
		}

		var job = new Timelapse { WallId = id, CreatedAt = time.GetUtcNow() };
		db.Timelapses.Add(job);
		await db.SaveChangesAsync(ct);
		return TypedResults.Accepted($"/render/walls/{id}/timelapse", View(job));
	}

	private static async Task<Results<Ok<TimelapseView>, NotFound>> LatestAsync(int id, RenderDb db, CancellationToken ct)
	{
		var job = await db.Timelapses.Where(t => t.WallId == id).OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync(ct);
		return job is null ? TypedResults.NotFound() : TypedResults.Ok(View(job));
	}

	private static Results<PhysicalFileHttpResult, NotFound> Video(Guid id, IConfiguration config)
	{
		var path = Timelapser.PathFor(config, id);
		return File.Exists(path) ? TypedResults.PhysicalFile(path, "video/mp4", enableRangeProcessing: true) : TypedResults.NotFound();
	}

	private static TimelapseView View(Timelapse job)
	{
		return new TimelapseView(job.Id, job.WallId, job.Status, job.Error, job.Status == TimelapseStatus.Done ? $"/render/timelapses/{job.Id}.mp4" : null);
	}
}
