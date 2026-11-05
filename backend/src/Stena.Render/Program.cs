using Stena.Render.Api;
using Stena.Render.Data;
using Stena.ServiceDefaults;

namespace Stena.Render;

public class Program
{
	public static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);
		builder.AddDefaults();
		builder.Services.AddHealthChecks()
			.AddNpgSql(builder.Configuration.GetConnectionString("db")!);
		builder.Services.AddHttpClient("canvas", c => c.BaseAddress = new Uri(builder.Configuration["Services:canvas"]!));
		builder.Services.AddHttpClient("history", c => c.BaseAddress = new Uri(builder.Configuration["Services:history"]!));
		builder.Services.AddSingleton<Sources>();
		builder.Services.AddSingleton(TimeProvider.System);

		var app = builder.Build();
		app.MapDefaults();
		app.MapImages();
		app.Run();
	}
}
