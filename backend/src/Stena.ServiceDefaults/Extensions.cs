using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Stena.ServiceDefaults;

public static class Extensions
{
	public static void AddDefaults(this WebApplicationBuilder builder)
	{
		builder.Services.AddOpenApi();
		builder.Services.AddHealthChecks();
	}

	public static void MapDefaults(this WebApplication app)
	{
		app.MapOpenApi();
		app.MapHealthChecks("/health");
	}
}
