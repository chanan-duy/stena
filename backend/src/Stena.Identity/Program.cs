using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stena.Identity.Api;
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
		builder.Services.AddValidation();
		builder.Services.AddHealthChecks()
			.AddNpgSql(db);
		builder.Services.AddDbContext<IdentityDb>(o => o.UseNpgsql(db));
		builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
		builder.Services.AddSingleton<Tokens>();

		var app = builder.Build();
		app.MapDefaults();
		app.MapAuth();

		await Seed.RunAsync(app.Services);
		await app.RunAsync();
	}
}
