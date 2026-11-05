using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Stena.Canvas.Data;
using Stena.Contracts;
using Stena.ServiceDefaults;

namespace Stena.Canvas.Api;

public record RollbackRequest(
	[property: Range(0, 1023)] int X,
	[property: Range(0, 1023)] int Y,
	[property: Range(1, 128)] int Width,
	[property: Range(1, 128)] int Height,
	DateTimeOffset At,
	Guid? VandalId
);

public record RollbackView(Guid Id, int WallId, string State, int Changed, string? Error);

public static class RollbacksApi
{
	public static void MapRollbacks(this IEndpointRouteBuilder app)
	{
		var rollbacks = app.MapGroup("/walls/{id:int}/rollbacks").WithTags("Rollbacks").RequireAuthorization(Roles.Admin);
		rollbacks.MapPost("", StartAsync);
		rollbacks.MapGet("/{rollbackId:guid}", GetAsync);
	}

	private static async Task<Results<Accepted<RollbackView>, NotFound, ProblemHttpResult>> StartAsync(
		int id,
		RollbackRequest req,
		CanvasDb db,
		IPublishEndpoint bus,
		ClaimsPrincipal user,
		CancellationToken ct
	)
	{
		var wall = await db.Walls.FindAsync([id], ct);
		if (wall is null)
		{
			return TypedResults.NotFound();
		}

		if (req.X + req.Width > wall.Width || req.Y + req.Height > wall.Height)
		{
			return TypedResults.Problem("Area is outside the wall", statusCode: StatusCodes.Status400BadRequest);
		}

		var rollbackId = NewId.NextGuid();
		await bus.Publish(new RollbackRequested(rollbackId, id, req.X, req.Y, req.Width, req.Height, req.At, req.VandalId,
			Guid.Parse(user.FindFirstValue("sub")!), user.Identity!.Name!), ct);
		await db.SaveChangesAsync(ct);
		return TypedResults.Accepted($"/walls/{id}/rollbacks/{rollbackId}", new RollbackView(rollbackId, id, "Requested", 0, null));
	}

	private static async Task<Results<Ok<RollbackView>, NotFound>> GetAsync(int id, Guid rollbackId, CanvasDb db, CancellationToken ct)
	{
		var r = await db.Rollbacks.FindAsync([rollbackId], ct);
		return r is null || r.WallId != id
			? TypedResults.NotFound()
			: TypedResults.Ok(new RollbackView(r.CorrelationId, r.WallId, r.State, r.Changed, r.Error));
	}
}
