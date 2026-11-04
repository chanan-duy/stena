using System.Net;
using System.Net.Http.Json;
using Stena.Contracts;
using Stena.History.Api;
using Stena.History.Domain;
using Xunit;

namespace Stena.History.Tests;

public class HistoryTests(HistoryApp app)
{
	private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

	[Fact]
	public async Task Pixel_ReturnsLastPlacementBeforeMoment()
	{
		var wall = await app.WallAsync(4, 4);
		await app.AddAsync(wall, "1000-0", 1, 1, 1);
		await app.WaitIngestedAsync(wall, await app.AddAsync(wall, "2000-0", 1, 1, 2));

		var early = await app.Client.GetFromJsonAsync<PixelInfo>($"/history/walls/{wall}/pixels/1/1?at={At(1500)}", _ct);
		var late = await app.Client.GetFromJsonAsync<PixelInfo>($"/history/walls/{wall}/pixels/1/1", _ct);
		var before = await app.Client.GetAsync($"/history/walls/{wall}/pixels/1/1?at={At(500)}", _ct);

		Assert.Equal(1, early!.Color);
		Assert.Equal("tester", early.UserName);
		Assert.Equal(2, late!.Color);
		Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
	}

	[Fact]
	public async Task Area_StartsFromSnapshotThenReplays()
	{
		var wall = await app.WallAsync(3, 2);
		await app.AddAsync(wall, "1000-0", 0, 0, 1);
		await app.WaitIngestedAsync(wall, await app.AddAsync(wall, "3000-0", 2, 1, 4));
		await app.WithDbAsync(async db =>
		{
			db.Snapshots.Add(new Snapshot { WallId = wall, Ms = 2000, Seq = 0, Width = 3, Height = 2, Pixels = [7, 7, 7, 7, 7, 7] });
			await db.SaveChangesAsync(_ct);
		});

		var beforeSnapshot = await app.Client.GetByteArrayAsync($"/history/walls/{wall}/area?at={At(1500)}", _ct);
		var afterSnapshot = await app.Client.GetByteArrayAsync($"/history/walls/{wall}/area?at={At(3500)}", _ct);
		var part = await app.Client.GetByteArrayAsync($"/history/walls/{wall}/area?x=1&y=1&w=2&h=1&at={At(3500)}", _ct);

		Assert.Equal([1, 0, 0, 0, 0, 0], beforeSnapshot);
		Assert.Equal([7, 7, 7, 7, 7, 4], afterSnapshot);
		Assert.Equal([7, 4], part);
	}

	[Fact]
	public async Task Ingest_TrimsStreamOlderThanAMinute()
	{
		var wall = await app.WallAsync(2, 2);
		await app.AddAsync(wall, "1000-0", 0, 0, 1);
		await app.WaitIngestedAsync(wall, await app.AddAsync(wall, "*", 1, 1, 1));

		var length = 2L;
		for (var i = 0; i < 50 && length > 1; i++)
		{
			await Task.Delay(20, _ct);
			length = await app.Redis.StreamLengthAsync(Keys.Stream(wall));
		}

		Assert.Equal(1, length);
	}

	private static string At(long ms)
	{
		return Uri.EscapeDataString(DateTimeOffset.FromUnixTimeMilliseconds(ms).ToString("O"));
	}
}
