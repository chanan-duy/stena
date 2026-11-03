using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using Stena.Contracts;

namespace Stena.Canvas.Live;

public class Broadcaster(IConnectionMultiplexer redis, IHubContext<WallHub> hub, ILogger<Broadcaster> log) : BackgroundService
{
	private readonly Dictionary<int, RedisValue> _last = [];

	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
		while (await timer.WaitForNextTickAsync(ct))
		{
			try
			{
				await TickAsync(ct);
			}
			catch (Exception e) when (e is not OperationCanceledException)
			{
				log.LogWarning(e, "Broadcast failed");
			}
		}
	}

	private async Task TickAsync(CancellationToken ct)
	{
		var db = redis.GetDatabase();
		foreach (var member in await db.SetMembersAsync(Keys.Walls))
		{
			var wall = (int)member;
			var prev = _last.GetValueOrDefault(wall, "0-0");
			var entries = await db.StreamRangeAsync(Keys.Stream(wall), $"({prev}", "+", 10_000);
			if (entries.Length == 0)
			{
				continue;
			}

			var pixels = entries.Select(e => new Pixel((int)e["x"], (int)e["y"], (int)e["c"])).ToArray();
			var last = entries[^1].Id;
			await hub.Clients.Group(WallHub.Group(wall)).SendAsync("pixels", wall, prev.ToString(), last.ToString(), Batch.Encode(pixels), ct);
			_last[wall] = last;
		}
	}
}
