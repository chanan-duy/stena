using Microsoft.EntityFrameworkCore;
using Stena.Canvas.Domain;
using Stena.Canvas.Redis;

namespace Stena.Canvas.Data;

public static class Setup
{
	private static readonly string[] Palette =
	[
		"#ffffff", "#e4e4e4", "#888888", "#222222", "#ffa7d1", "#e50000", "#e59500", "#a06a42",
		"#e5d900", "#94e044", "#02be01", "#00d3dd", "#0083c7", "#0000ea", "#cf6ee4", "#820080",
	];

	public static async Task RunAsync(IServiceProvider services)
	{
		using var scope = services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<CanvasDb>();
		var store = scope.ServiceProvider.GetRequiredService<WallStore>();
		var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

		await db.Database.MigrateAsync();
		if (!await db.Walls.AnyAsync())
		{
			db.Walls.Add(Wall.Create(new WallSpec("Main", 256, 256, Palette, 30, 10, now, now.AddDays(30))));
			await db.SaveChangesAsync();
		}

		foreach (var wall in await db.Walls.ToListAsync())
		{
			await store.SyncAsync(wall);
		}
	}
}
