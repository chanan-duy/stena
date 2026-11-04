using Microsoft.AspNetCore.Http.HttpResults;
using Stena.Identity.Data;
using Stena.ServiceDefaults;

namespace Stena.Identity.Api;

public static class UsersApi
{
	public static void MapUsers(this IEndpointRouteBuilder app)
	{
		var users = app.MapGroup("/auth/users").WithTags("Users").RequireAuthorization(Roles.Admin);
		users.MapPost("/{id:guid}/ban", BanAsync);
	}

	private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> BanAsync(Guid id, Bans bans, CancellationToken ct)
	{
		return await bans.BanAsync(id, ct) switch
		{
			BanResult.Banned => TypedResults.NoContent(),
			BanResult.NotFound => TypedResults.NotFound(),
			_ => TypedResults.Problem("Admins cannot be banned", statusCode: StatusCodes.Status409Conflict),
		};
	}
}
