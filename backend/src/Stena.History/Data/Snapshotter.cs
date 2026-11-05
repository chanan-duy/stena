using Microsoft.EntityFrameworkCore;
using Stena.Contracts;
using Stena.History.Domain;

namespace Stena.History.Data;

public class Snapshotter(IServiceScopeFactory scopes, ILogger<Snapshotter> log) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
		while (await timer.WaitForNextTickAsync(ct))
		{
			try
			{
				await TakeAllAsync(ct);
			}
			catch (Exception e) when (e is not OperationCanceledException)
			{
				log.LogWarning(e, "Snapshot failed");
			}
		}
	}

	private async Task TakeAllAsync(CancellationToken ct)
	{
		using var scope = scopes.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<HistoryDb>();
		var timeline = scope.ServiceProvider.GetRequiredService<Timeline>();

		foreach (var cursor in await db.Cursors.AsNoTracking().ToListAsync(ct))
		{
			var last = await db.Snapshots
				.Where(s => s.WallId == cursor.WallId)
				.OrderByDescending(s => s.Ms)
				.ThenByDescending(s => s.Seq)
				.FirstOrDefaultAsync(ct);
			if (last is not null && !StreamIds.Before($"{last.Ms}-{last.Seq}", cursor.StreamId))
			{
				continue;
			}

			if (await timeline.SizeAsync(cursor.WallId) is not { } size)
			{
				continue;
			}

			var at = DateTimeOffset.FromUnixTimeMilliseconds(cursor.Ms);
			var pixels = await timeline.AreaAsync(cursor.WallId, new Area(0, 0, size.Width, size.Height), at, ct);
			db.Snapshots.Add(new Snapshot
			{
				WallId = cursor.WallId,
				Ms = cursor.Ms,
				Seq = cursor.Seq,
				Width = size.Width,
				Height = size.Height,
				Pixels = pixels,
			});
			await db.SaveChangesAsync(ct);
		}
	}
}
