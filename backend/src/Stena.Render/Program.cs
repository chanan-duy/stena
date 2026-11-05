using Microsoft.EntityFrameworkCore;
using Stena.Render.Api;
using Stena.Render.Data;
using Stena.Render.Drawing;
using Stena.ServiceDefaults;

namespace Stena.Render;

public class Program
{
	public static async Task Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);
		var db = builder.Configuration.GetConnectionString("db")!;
		builder.AddDefaults();
		builder.Services.AddHealthChecks()
			.AddNpgSql(db);
		builder.Services.AddDbContext<RenderDb>(o => o.UseNpgsql(db));
		builder.Services.AddHttpClient("canvas", c => c.BaseAddress = new Uri(builder.Configuration["Services:canvas"]!));
		builder.Services.AddHttpClient("history", c => c.BaseAddress = new Uri(builder.Configuration["Services:history"]!));
		builder.Services.AddSingleton<Sources>();
		builder.Services.AddSingleton(TimeProvider.System);
		builder.Services.AddHostedService<Timelapser>();

		var app = builder.Build();
		app.MapDefaults();
		app.MapImages();
		app.MapTimelapses();

		using (var scope = app.Services.CreateScope())
		{
			await scope.ServiceProvider.GetRequiredService<RenderDb>().Database.MigrateAsync();
		}

		await app.RunAsync();
	}
}
