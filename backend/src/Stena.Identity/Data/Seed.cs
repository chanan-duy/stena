using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stena.Identity.Domain;
using Stena.ServiceDefaults;

namespace Stena.Identity.Data;

public static class Seed
{
	public static async Task RunAsync(IServiceProvider services)
	{
		using var scope = services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<IdentityDb>();
		var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

		await db.Database.MigrateAsync();
		if (await db.Users.AnyAsync())
		{
			return;
		}

		db.Users.AddRange(
			Create(hasher, "admin@stena.test", "admin", Roles.Admin, "admin123"),
			Create(hasher, "player@stena.test", "player", Roles.Player, "player123"));
		await db.SaveChangesAsync();
	}

	private static User Create(IPasswordHasher<User> hasher, string email, string name, string role, string password)
	{
		var user = new User { Email = email, Name = name, Role = role };
		user.PasswordHash = hasher.HashPassword(user, password);
		return user;
	}
}
