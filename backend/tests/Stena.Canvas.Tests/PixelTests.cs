using System.Net;
using System.Net.Http.Json;
using Stena.Canvas.Api;
using Stena.Canvas.Domain;
using Stena.Canvas.Redis;
using Stena.Contracts;
using Stena.ServiceDefaults;
using Xunit;

namespace Stena.Canvas.Tests;

public class PixelTests(CanvasApp app) : IAsyncLifetime
{
	private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
	private readonly Guid _user = Guid.NewGuid();
	private HttpClient _player = null!;
	private int _wall;

	public async ValueTask InitializeAsync()
	{
		var now = DateTimeOffset.UtcNow;
		var spec = new WallSpec("Pixels", 4, 4, ["#ffffff", "#000000"], 60, 2, now.AddHours(-1), now.AddHours(1));
		var res = await app.As(Roles.Admin).PostAsJsonAsync("/walls", spec, _ct);
		_wall = (await res.Content.ReadFromJsonAsync<WallView>(_ct))!.Id;
		_player = app.As(Roles.Player, _user);
	}

	public ValueTask DisposeAsync()
	{
		_player.Dispose();
		return ValueTask.CompletedTask;
	}

	[Fact]
	public async Task Place_WritesPixelAndStream()
	{
		var res = await PlaceAsync(1, 2, 1);
		var body = await res.Content.ReadFromJsonAsync<PlaceResponse>(_ct);
		var bitmap = await app.Client.GetByteArrayAsync($"/walls/{_wall}/bitmap", _ct);
		var entry = (await app.Redis.StreamRangeAsync(Keys.Stream(_wall))).Single();

		Assert.Equal(HttpStatusCode.OK, res.StatusCode);
		Assert.Equal(1, body!.Charges);
		Assert.Equal(1, bitmap[2 * 4 + 1]);
		Assert.Equal(body.StreamId, entry.Id.ToString());
		Assert.Equal(_user.ToString(), entry["u"].ToString());
	}

	[Fact]
	public async Task Place_SameRequestId_CountsOnce()
	{
		var id = Guid.NewGuid();
		await PlaceAsync(0, 0, 1, id);
		await PlaceAsync(0, 0, 1, id);

		Assert.Equal(1, await app.Redis.StreamLengthAsync(Keys.Stream(_wall)));
		Assert.Equal(1, (await ChargesAsync()).Count);
	}

	[Fact]
	public async Task Place_WithoutCharges_IsTooManyRequests()
	{
		await PlaceAsync(0, 0, 1);
		await PlaceAsync(1, 0, 1);
		var res = await PlaceAsync(2, 0, 1);

		Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
		Assert.True(res.Headers.RetryAfter!.Delta > TimeSpan.Zero);
	}

	[Fact]
	public async Task Place_OutOfRange_KeepsCharges()
	{
		var res = await PlaceAsync(4, 0, 1);

		Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
		Assert.Equal(2, (await ChargesAsync()).Count);
	}

	[Fact]
	public async Task Place_WithoutRequestId_IsBadRequest()
	{
		var res = await PlaceAsync(0, 0, 1, Guid.Empty);

		Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
		Assert.Equal(0, await app.Redis.StreamLengthAsync(Keys.Stream(_wall)));
	}

	[Fact]
	public async Task Place_Banned_IsForbidden()
	{
		await app.Redis.SetAddAsync(Keys.Bans, _user.ToString());

		var res = await PlaceAsync(0, 0, 1);

		Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
	}

	[Fact]
	public async Task Place_LockedWall_IsConflict()
	{
		await app.Redis.StringSetAsync(Keys.Lock(_wall), "rollback", TimeSpan.FromMinutes(1));

		var res = await PlaceAsync(0, 0, 1);

		Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
		Assert.Equal(0, await app.Redis.StreamLengthAsync(Keys.Stream(_wall)));
	}

	private Task<HttpResponseMessage> PlaceAsync(int x, int y, int color, Guid? requestId = null)
	{
		return _player.PostAsJsonAsync($"/walls/{_wall}/pixels", new PlaceRequest(x, y, color, requestId ?? Guid.NewGuid()), _ct);
	}

	private async Task<Charges> ChargesAsync()
	{
		return (await _player.GetFromJsonAsync<Charges>($"/walls/{_wall}/charges", _ct))!;
	}
}
