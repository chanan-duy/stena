using Microsoft.EntityFrameworkCore;
using Stena.Canvas.Api;
using Stena.Canvas.Data;
using Stena.Canvas.Live;
using Stena.Canvas.Redis;
using StackExchange.Redis;
using Stena.ServiceDefaults;

namespace Stena.Canvas;

public class Program
{
	public static async Task Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);
		var db = builder.Configuration.GetConnectionString("db")!;
		builder.AddDefaults();
		builder.Services.AddValidation();
		builder.Services.AddHealthChecks()
			.AddNpgSql(db)
			.AddRedis(sp => sp.GetRequiredService<IConnectionMultiplexer>());
		builder.Services.AddDbContext<CanvasDb>(o => o.UseNpgsql(db));
		builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
			ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("redis")!));
		builder.Services.AddSingleton<WallStore>();
		builder.Services.AddSingleton<Placement>();
		builder.Services.AddSingleton(TimeProvider.System);
		builder.Services.AddSignalR().AddMessagePackProtocol();
		builder.Services.AddHostedService<Broadcaster>();

		var app = builder.Build();
		app.MapDefaults();
		app.MapWalls();
		app.MapPixels();
		app.MapHub<WallHub>("/hub");

		await Setup.RunAsync(app.Services);
		await app.RunAsync();
	}
}
