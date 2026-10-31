using Microsoft.EntityFrameworkCore;
using Stena.Canvas.Api;
using Stena.Canvas.Data;
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
			.AddRedis(builder.Configuration.GetConnectionString("redis")!);
		builder.Services.AddDbContext<CanvasDb>(o => o.UseNpgsql(db));
		builder.Services.AddSingleton(TimeProvider.System);

		var app = builder.Build();
		app.MapDefaults();
		app.MapWalls();

		await Setup.RunAsync(app.Services);
		await app.RunAsync();
	}
}
