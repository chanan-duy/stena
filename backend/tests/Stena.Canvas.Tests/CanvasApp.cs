using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Stena.ServiceDefaults;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

[assembly: AssemblyFixture(typeof(Stena.Canvas.Tests.CanvasApp))]

namespace Stena.Canvas.Tests;

public class CanvasApp : IAsyncLifetime
{
	private const string JwtKey = "test-key-0123456789abcdef0123456789abcdef";

	private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:18-alpine").Build();
	private readonly RedisContainer _redis = new RedisBuilder("redis:8-alpine").Build();
	private readonly RabbitMqContainer _mq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
	private WebApplicationFactory<Program> _factory = null!;
	private ConnectionMultiplexer _redisConnection = null!;

	public HttpClient Client { get; private set; } = null!;
	public IDatabase Redis => _redisConnection.GetDatabase();
	public TestServer Server => _factory.Server;
	public IServiceProvider Services => _factory.Services;

	public async ValueTask InitializeAsync()
	{
		await Task.WhenAll(_db.StartAsync(), _redis.StartAsync(), _mq.StartAsync());
		_factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
			.UseSetting("ConnectionStrings:db", _db.GetConnectionString())
			.UseSetting("ConnectionStrings:redis", _redis.GetConnectionString())
			.UseSetting("ConnectionStrings:rabbitmq", _mq.GetConnectionString())
			.UseSetting("Jwt:Key", JwtKey));
		Client = _factory.CreateClient();
		_redisConnection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
	}

	public HttpClient As(string role, Guid? user = null, string name = "tester")
	{
		var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
		{
			Issuer = Jwt.Issuer,
			Audience = Jwt.Issuer,
			Expires = DateTime.UtcNow.AddHours(1),
			SigningCredentials = new SigningCredentials(
				Jwt.Key(new ConfigurationBuilder().AddInMemoryCollection([new("Jwt:Key", JwtKey)]).Build()),
				SecurityAlgorithms.HmacSha256),
			Claims = new Dictionary<string, object>
			{
				["sub"] = (user ?? Guid.NewGuid()).ToString(),
				["name"] = name,
				["role"] = role,
			},
		});
		var client = _factory.CreateClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return client;
	}

	public async ValueTask DisposeAsync()
	{
		Client.Dispose();
		await _redisConnection.DisposeAsync();
		await _factory.DisposeAsync();
		await Task.WhenAll(_db.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask(), _mq.DisposeAsync().AsTask());
	}
}
