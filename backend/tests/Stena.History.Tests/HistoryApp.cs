using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Stena.Contracts;
using Stena.History.Data;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

[assembly: AssemblyFixture(typeof(Stena.History.Tests.HistoryApp))]

namespace Stena.History.Tests;

public class HistoryApp : IAsyncLifetime
{
	private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:18-alpine").Build();
	private readonly RedisContainer _redis = new RedisBuilder("redis:8-alpine").Build();
	private WebApplicationFactory<Program> _factory = null!;
	private ConnectionMultiplexer _redisConnection = null!;
	private int _walls;

	public HttpClient Client { get; private set; } = null!;
	public IDatabase Redis => _redisConnection.GetDatabase();

	public async ValueTask InitializeAsync()
	{
		await Task.WhenAll(_db.StartAsync(), _redis.StartAsync());
		_factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
			.UseSetting("ConnectionStrings:db", _db.GetConnectionString())
			.UseSetting("ConnectionStrings:redis", _redis.GetConnectionString())
			.UseSetting("Jwt:Key", "test-key-0123456789abcdef0123456789abcdef"));
		Client = _factory.CreateClient();
		_redisConnection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
	}

	public async Task<int> WallAsync(int width, int height)
	{
		var id = Interlocked.Increment(ref _walls);
		await Redis.HashSetAsync(Keys.Wall(id), [new HashEntry("w", width), new HashEntry("h", height)]);
		await Redis.SetAddAsync(Keys.Walls, id);
		return id;
	}

	public async Task<string> AddAsync(int wallId, string streamId, int x, int y, int color)
	{
		return (await Redis.StreamAddAsync(Keys.Stream(wallId), [
			new NameValueEntry("x", x),
			new NameValueEntry("y", y),
			new NameValueEntry("c", color),
			new NameValueEntry("u", Guid.Empty.ToString()),
			new NameValueEntry("n", "tester"),
		], streamId)).ToString();
	}

	public async Task WaitIngestedAsync(int wallId, string streamId)
	{
		for (var i = 0; i < 100; i++)
		{
			using var scope = _factory.Services.CreateScope();
			var cursor = await scope.ServiceProvider.GetRequiredService<HistoryDb>().Cursors.FindAsync(wallId);
			if (cursor is not null && !StreamIds.Before(cursor.StreamId, streamId))
			{
				return;
			}

			await Task.Delay(50);
		}

		throw new TimeoutException($"Wall {wallId} not ingested up to {streamId}");
	}

	public async Task WithDbAsync(Func<HistoryDb, Task> action)
	{
		using var scope = _factory.Services.CreateScope();
		await action(scope.ServiceProvider.GetRequiredService<HistoryDb>());
	}

	public async ValueTask DisposeAsync()
	{
		Client.Dispose();
		await _redisConnection.DisposeAsync();
		await _factory.DisposeAsync();
		await Task.WhenAll(_db.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
	}
}
