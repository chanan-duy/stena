using System.Net;
using System.Net.Http.Json;
using Stena.Canvas.Api;
using Stena.Canvas.Domain;
using Stena.ServiceDefaults;
using Xunit;

namespace Stena.Canvas.Tests;

public class WallTests(CanvasApp app)
{
	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
	private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

	[Fact]
	public async Task Create_AsAdmin_MakesBlankBitmap()
	{
		var res = await app.As(Roles.Admin).PostAsJsonAsync("/walls", Spec(8, 4), _ct);
		var wall = await res.Content.ReadFromJsonAsync<WallView>(_ct);

		var bitmap = await app.Client.GetAsync($"/walls/{wall!.Id}/bitmap", _ct);

		Assert.Equal(HttpStatusCode.Created, res.StatusCode);
		Assert.Equal(new byte[32], await bitmap.Content.ReadAsByteArrayAsync(_ct));
		Assert.Equal("0-0", bitmap.Headers.GetValues("X-Stream-Id").Single());
	}

	[Fact]
	public async Task Create_AsPlayer_IsForbidden()
	{
		var res = await app.As(Roles.Player).PostAsJsonAsync("/walls", Spec(8, 4), _ct);

		Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
	}

	[Fact]
	public void Edit_LiveWall_KeepsSizeFrozen()
	{
		var wall = Wall.Create(Spec(8, 4) with { StartsAt = Now.AddHours(-1) });

		Assert.False(wall.TryEdit(Spec(16, 4) with { StartsAt = Now.AddHours(-1) }, Now));
		Assert.False(wall.TryEdit(Spec(8, 4) with { StartsAt = Now.AddHours(1) }, Now));
		Assert.True(wall.TryEdit(Spec(8, 4) with { Name = "Renamed", StartsAt = Now.AddHours(-1) }, Now));
		Assert.Equal("Renamed", wall.Name);
	}

	[Fact]
	public void Edit_UpcomingWall_AllowsResize()
	{
		var wall = Wall.Create(Spec(8, 4));

		Assert.True(wall.TryEdit(Spec(16, 4), Now));
		Assert.Equal(16, wall.Width);
	}

	private static WallSpec Spec(int width, int height)
	{
		return new WallSpec("Test", width, height, ["#ffffff", "#000000"], 1, 3, Now.AddDays(1), Now.AddDays(2));
	}
}
