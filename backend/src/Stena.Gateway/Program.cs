using Scalar.AspNetCore;
using Serilog;
using Stena.ServiceDefaults;

namespace Stena.Gateway;

public class Program
{
	public static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);
		builder.AddLogs();
		builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
		builder.Services.AddHealthChecks();

		var app = builder.Build();
		app.UseSerilogRequestLogging();
		app.MapScalarApiReference(o => o
			.AddDocuments("identity", "canvas", "history", "render")
			.HideDeveloperTools()
			.DisableAgent());
		app.MapHealthChecks("/health");
		app.MapReverseProxy();
		app.Run();
	}
}
