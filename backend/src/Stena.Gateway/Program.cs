using Scalar.AspNetCore;

namespace Stena.Gateway;

public class Program
{
	public static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);
		builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
		builder.Services.AddHealthChecks();

		var app = builder.Build();
		app.MapScalarApiReference(o => o
			.AddDocuments("identity", "canvas", "history", "render")
			.HideDeveloperTools()
			.DisableAgent());
		app.MapHealthChecks("/health");
		app.MapReverseProxy();
		app.Run();
	}
}
