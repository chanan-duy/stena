using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Stena.Contracts;
using Stena.History.Data;
using Stena.History.Domain;

namespace Stena.History.Redis;

public class Ingest(IServiceScopeFactory scopes, IConnectionMultiplexer redis, ILogger<Ingest> log) : BackgroundService
{
	private const int BatchSize = 1000;
	private static readonly TimeSpan Keep = TimeSpan.FromMinutes(1);

	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
		while (await timer.WaitForNextTickAsync(ct))
		{
			try
			{
				foreach (var wall in await redis.GetDatabase().SetMembersAsync(Keys.Walls))
				{
					while (await StepAsync((int)wall, ct) == BatchSize)
					{
					}
				}
			}
			catch (Exception e) when (e is not OperationCanceledException)
			{
				log.LogWarning(e, "Ingest failed");
			}
		}
	}

	private async Task<int> StepAsync(int wallId, CancellationToken ct)
	{
		using var scope = scopes.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<HistoryDb>();
		var rdb = redis.GetDatabase();
		var stream = Keys.Stream(wallId);

		var cursor = await db.Cursors.SingleOrDefaultAsync(c => c.WallId == wallId, ct);
		if (cursor is null)
		{
			cursor = new Cursor { WallId = wallId };
			db.Cursors.Add(cursor);
		}

		var entries = await rdb.StreamRangeAsync(stream, $"({cursor.StreamId}", "+", BatchSize);
		if (entries.Length == 0)
		{
			return 0;
		}

		db.Pixels.AddRange(entries.Select(e =>
		{
			var (ms, seq) = StreamIds.Parse(e.Id!);
			return new PixelEvent
			{
				WallId = wallId,
				Ms = ms,
				Seq = seq,
				X = (int)e["x"],
				Y = (int)e["y"],
				Color = (byte)(int)e["c"],
				UserId = Guid.Parse(e["u"].ToString()),
				UserName = e["n"].ToString(),
			};
		}));
		cursor.MoveTo(entries[^1].Id!);
		await db.SaveChangesAsync(ct);

		await rdb.ExecuteAsync("XTRIM", stream, "MINID", cursor.Ms - (long)Keep.TotalMilliseconds);
		return entries.Length;
	}
}
