using Stena.ServiceDefaults;

namespace Stena.Canvas;

public class Program
{
	public static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);
		builder.AddDefaults();
		builder.Services.AddHealthChecks()
			.AddNpgSql(builder.Configuration.GetConnectionString("db")!)
			.AddRedis(builder.Configuration.GetConnectionString("redis")!);

		var app = builder.Build();
		app.MapDefaults();
		app.Run();
	}
}
