using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
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
		builder.Services.AddProblemDetails();
		builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerScheme>());
		builder.Services.AddHealthChecks();
		builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
			.AddJwtBearer(o =>
			{
				o.MapInboundClaims = false;
				o.TokenValidationParameters = new TokenValidationParameters
				{
					ValidIssuer = Jwt.Issuer,
					ValidAudience = Jwt.Issuer,
					IssuerSigningKey = Jwt.Key(builder.Configuration),
					NameClaimType = "name",
					RoleClaimType = "role",
				};
			});
		builder.Services.AddAuthorizationBuilder()
			.AddPolicy(Roles.Admin, p => p.RequireRole(Roles.Admin));
	}

	public static void MapDefaults(this WebApplication app)
	{
		app.UseSerilogRequestLogging();
		app.UseExceptionHandler();
		app.UseStatusCodePages();
		app.UseAuthentication();
		app.UseAuthorization();
		app.MapOpenApi();
		app.MapHealthChecks("/health");
	}
}
