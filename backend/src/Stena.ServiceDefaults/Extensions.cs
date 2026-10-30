using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

namespace Stena.ServiceDefaults;

public static class Extensions
{
	public static void AddLogs(this WebApplicationBuilder builder)
	{
		builder.Services.AddSerilog(c => c
			.MinimumLevel.Information()
			.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
			.MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
			.MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
			.Enrich.FromLogContext()
			.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {RequestId} {Message:lj}{NewLine}{Exception}"));
	}

	public static void AddDefaults(this WebApplicationBuilder builder)
	{
		builder.AddLogs();
		builder.Services.AddOpenApi();
		builder.Services.AddHealthChecks();
	}

	public static void MapDefaults(this WebApplication app)
	{
		app.UseSerilogRequestLogging();
		app.MapOpenApi();
		app.MapHealthChecks("/health");
	}
}
