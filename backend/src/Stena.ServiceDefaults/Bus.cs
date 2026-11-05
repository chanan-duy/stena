using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Stena.ServiceDefaults;

public static class Bus
{
	public static void UseRetry(this IReceiveEndpointConfigurator cfg)
	{
		cfg.UseMessageRetry(r => r.Intervals(200, 1000, 3000));
	}

	public static void UseRabbitMq(this IBusRegistrationConfigurator bus, WebApplicationBuilder builder)
	{
		bus.SetKebabCaseEndpointNameFormatter();
		bus.UsingRabbitMq((ctx, cfg) =>
		{
			cfg.Host(new Uri(builder.Configuration.GetConnectionString("rabbitmq")!));
			cfg.ConfigureEndpoints(ctx);
		});
	}
}
