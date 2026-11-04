using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Stena.History.Data;
using Stena.History.Redis;
using Stena.ServiceDefaults;

namespace Stena.History;

public class Program
{
	public static async Task Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);
		var db = builder.Configuration.GetConnectionString("db")!;
		builder.AddDefaults();
		builder.Services.AddHealthChecks()
			.AddNpgSql(db)
			.AddRedis(sp => sp.GetRequiredService<IConnectionMultiplexer>());
		builder.Services.AddDbContext<HistoryDb>(o => o.UseNpgsql(db));
		builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
			ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("redis")!));
		builder.Services.AddHostedService<Ingest>();

		var app = builder.Build();
		app.MapDefaults();

		using (var scope = app.Services.CreateScope())
		{
			await scope.ServiceProvider.GetRequiredService<HistoryDb>().Database.MigrateAsync();
		}

		await app.RunAsync();
	}
}
