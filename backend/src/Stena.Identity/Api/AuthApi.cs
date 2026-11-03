using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stena.Identity.Data;
using Stena.Identity.Domain;

namespace Stena.Identity.Api;

public record RegisterRequest(
	[property: Required, EmailAddress, MaxLength(256)] string Email,
	[property: Required, RegularExpression("^[A-Za-z0-9_]{3,32}$")] string Name,
	[property: Required, MinLength(8), MaxLength(128)] string Password
);

public record LoginRequest([property: Required] string Email, [property: Required] string Password);

public record TokenResponse(string Token);

public static class AuthApi
{
	public static void MapAuth(this IEndpointRouteBuilder app)
	{
		var auth = app.MapGroup("/auth").WithTags("Auth");
		auth.MapPost("/register", RegisterAsync);
		auth.MapPost("/login", LoginAsync);
	}

	private static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> RegisterAsync(
		RegisterRequest req,
		IdentityDb db,
		IPasswordHasher<User> hasher,
		Tokens tokens,
		CancellationToken ct
	)
	{
		var user = new User { Email = req.Email.Trim().ToLowerInvariant(), Name = req.Name };
		user.PasswordHash = hasher.HashPassword(user, req.Password);
		db.Users.Add(user);
		try
		{
			await db.SaveChangesAsync(ct);
		}
		catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
		{
			return TypedResults.Problem("Email or name is taken", statusCode: StatusCodes.Status409Conflict);
		}

		return TypedResults.Ok(new TokenResponse(tokens.Issue(user)));
	}

	private static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> LoginAsync(
		LoginRequest req,
		IdentityDb db,
		IPasswordHasher<User> hasher,
		Tokens tokens,
		CancellationToken ct
	)
	{
		var email = req.Email.Trim().ToLowerInvariant();
		var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
		if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
		{
			return TypedResults.Problem("Wrong email or password", statusCode: StatusCodes.Status401Unauthorized);
		}

		if (user.Banned)
		{
			return TypedResults.Problem("User is banned", statusCode: StatusCodes.Status403Forbidden);
		}

		return TypedResults.Ok(new TokenResponse(tokens.Issue(user)));
	}
}
