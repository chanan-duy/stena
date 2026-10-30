using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stena.Identity.Data;
using Stena.Identity.Domain;
using Stena.ServiceDefaults;

namespace Stena.Identity;

public class Program
{
	public static async Task Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);
		var db = builder.Configuration.GetConnectionString("db")!;
		builder.AddDefaults();
		builder.Services.AddHealthChecks()
			.AddNpgSql(db);
		builder.Services.AddDbContext<IdentityDb>(o => o.UseNpgsql(db));
		builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

		var app = builder.Build();
		app.MapDefaults();

		await Seed.RunAsync(app.Services);
		await app.RunAsync();
	}
}
