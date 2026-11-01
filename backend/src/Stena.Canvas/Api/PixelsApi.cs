using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Stena.Canvas.Redis;

namespace Stena.Canvas.Api;

public record PlaceRequest([property: Range(0, 1023)] int X, [property: Range(0, 1023)] int Y, [property: Range(0, 255)] int Color, Guid RequestId);

public record PlaceResponse(int Charges, long NextChargeMs, string StreamId);

public static class PixelsApi
{
	public static void MapPixels(this IEndpointRouteBuilder app)
	{
		var walls = app.MapGroup("/walls/{id:int}").WithTags("Pixels").RequireAuthorization();
		walls.MapPost("/pixels", PlaceAsync);
		walls.MapGet("/charges", ChargesAsync);
	}

	private static async Task<Results<Ok<PlaceResponse>, NotFound, ProblemHttpResult>> PlaceAsync(
		int id,
		PlaceRequest req,
		Placement placement,
		ClaimsPrincipal user,
		HttpResponse res
	)
	{
		if (req.RequestId == Guid.Empty)
		{
			return TypedResults.Problem("RequestId is required", statusCode: StatusCodes.Status400BadRequest);
		}

		var r = await placement.PlaceAsync(id, req.X, req.Y, req.Color, req.RequestId, user.FindFirstValue("sub")!, user.Identity!.Name!);
		switch (r.Status)
		{
			case PlaceStatus.Ok:
				return TypedResults.Ok(new PlaceResponse(r.Charges, r.NextChargeMs, r.StreamId!));
			case PlaceStatus.NotFound:
				return TypedResults.NotFound();
			case PlaceStatus.NoCharges:
				res.Headers.RetryAfter = Math.Ceiling(r.NextChargeMs / 1000.0).ToString("0");
				return TypedResults.Problem("No charges left", statusCode: StatusCodes.Status429TooManyRequests);
			case PlaceStatus.OutOfRange:
				return TypedResults.Problem("Pixel or color is out of range", statusCode: StatusCodes.Status400BadRequest);
			case PlaceStatus.Banned:
				return TypedResults.Problem("User is banned", statusCode: StatusCodes.Status403Forbidden);
			case PlaceStatus.Locked:
				return TypedResults.Problem("Wall is locked by a rollback", statusCode: StatusCodes.Status409Conflict);
			default:
				return TypedResults.Problem("Wall is not live", statusCode: StatusCodes.Status409Conflict);
		}
	}

	private static async Task<Results<Ok<Charges>, NotFound>> ChargesAsync(int id, Placement placement, ClaimsPrincipal user)
	{
		var charges = await placement.ChargesAsync(id, user.FindFirstValue("sub")!);
		return charges is null ? TypedResults.NotFound() : TypedResults.Ok(charges);
	}
}
