using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Stena.Contracts;
using Stena.History.Domain;

namespace Stena.History.Data;

public record WallSize(int Width, int Height);

public class Timeline(HistoryDb db, IConnectionMultiplexer redis)
{
	public async Task<WallSize?> SizeAsync(int wallId)
	{
		var size = await redis.GetDatabase().HashGetAsync(Keys.Wall(wallId), ["w", "h"]);
		return size[0].IsNull ? null : new WallSize((int)size[0], (int)size[1]);
	}

	public async Task<bool> CatchUpAsync(int wallId, DateTimeOffset at, CancellationToken ct)
	{
		var stream = Keys.Stream(wallId);
		var atMs = at.ToUnixTimeMilliseconds();
		for (var i = 0; i < 50; i++)
		{
			var cursor = await db.Cursors.AsNoTracking().SingleOrDefaultAsync(c => c.WallId == wallId, ct) ?? new Cursor { WallId = wallId };
			var rdb = redis.GetDatabase();
			if (cursor.Ms > atMs || !await rdb.KeyExistsAsync(stream) || !StreamIds.Before(cursor.StreamId, (await rdb.StreamInfoAsync(stream)).LastGeneratedId!))
			{
				return true;
			}

			await Task.Delay(100, ct);
		}

		return false;
	}

	public async Task<byte[]> AreaAsync(int wallId, Area area, DateTimeOffset at, CancellationToken ct)
	{
		var atMs = at.ToUnixTimeMilliseconds();
		var snapshot = await db.Snapshots.AsNoTracking()
			.Where(s => s.WallId == wallId && s.Ms <= atMs)
			.OrderByDescending(s => s.Ms)
			.ThenByDescending(s => s.Seq)
			.FirstOrDefaultAsync(ct);
		var pixels = snapshot is null ? new byte[area.Width * area.Height] : area.Crop(snapshot.Pixels, snapshot.Width);
		long ms = snapshot?.Ms ?? -1, seq = snapshot?.Seq ?? -1;
		var events = await db.Pixels.AsNoTracking()
			.Where(p => p.WallId == wallId && (p.Ms > ms || p.Ms == ms && p.Seq > seq) && p.Ms <= atMs)
			.Where(p => p.X >= area.X && p.X < area.X + area.Width && p.Y >= area.Y && p.Y < area.Y + area.Height)
			.OrderBy(p => p.Ms)
			.ThenBy(p => p.Seq)
			.Select(p => new { p.X, p.Y, p.Color })
			.ToListAsync(ct);
		foreach (var e in events)
		{
			area.Apply(pixels, e.X, e.Y, e.Color);
		}

		return pixels;
	}

	public Task<PixelEvent?> PixelAsync(int wallId, int x, int y, DateTimeOffset at, CancellationToken ct)
	{
		var atMs = at.ToUnixTimeMilliseconds();
		return db.Pixels.AsNoTracking()
			.Where(p => p.WallId == wallId && p.X == x && p.Y == y && p.Ms <= atMs)
			.OrderByDescending(p => p.Ms)
			.ThenByDescending(p => p.Seq)
			.FirstOrDefaultAsync(ct);
	}

	public Task<int> CountAsync(int wallId, CancellationToken ct) => db.Pixels.CountAsync(p => p.WallId == wallId, ct);
}
