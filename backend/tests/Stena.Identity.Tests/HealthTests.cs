using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Stena.Identity.Tests;

public class HealthTests : IAsyncLifetime
{
	private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:18-alpine").Build();

	public async ValueTask InitializeAsync()
	{
		await _db.StartAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _db.DisposeAsync();
	}

	[Fact]
	public async Task Health_WithDatabase_IsHealthy()
	{
		await using var factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:db", _db.GetConnectionString()));

		var res = await factory.CreateClient().GetAsync("/health", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, res.StatusCode);
	}
}
