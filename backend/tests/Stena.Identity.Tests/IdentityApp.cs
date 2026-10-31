using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

[assembly: AssemblyFixture(typeof(Stena.Identity.Tests.IdentityApp))]

namespace Stena.Identity.Tests;

public class IdentityApp : IAsyncLifetime
{
	private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:18-alpine").Build();

	private WebApplicationFactory<Program> _factory = null!;

	public HttpClient Client { get; private set; } = null!;

	public async ValueTask InitializeAsync()
	{
		await _db.StartAsync();
		_factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
			.UseSetting("ConnectionStrings:db", _db.GetConnectionString())
			.UseSetting("Jwt:Key", "test-key-0123456789abcdef0123456789abcdef"));
		Client = _factory.CreateClient();
	}

	public async ValueTask DisposeAsync()
	{
		Client.Dispose();
		await _factory.DisposeAsync();
		await _db.DisposeAsync();
	}
}
