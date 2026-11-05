using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stena.Identity.Api;
using Stena.Identity.Data;
using Stena.Identity.Domain;
using Stena.Identity.Messaging;
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
		builder.Services.AddScoped<Bans>();
		builder.Services.AddMassTransit(x =>
		{
			x.AddEntityFrameworkOutbox<IdentityDb>(o =>
			{
				o.UsePostgres();
				o.UseBusOutbox();
			});
			x.AddConfigureEndpointsCallback((ctx, _, cfg) =>
			{
				cfg.UseRetry();
				cfg.UseEntityFrameworkOutbox<IdentityDb>(ctx);
			});
			x.AddConsumer<BanUserConsumer>();
			x.UseRabbitMq(builder);
		});

		var app = builder.Build();
		app.MapDefaults();
		app.MapAuth();
		app.MapUsers();

		await Seed.RunAsync(app.Services);
		await app.RunAsync();
	}
}
