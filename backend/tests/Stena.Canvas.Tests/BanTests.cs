using System.Net;
using System.Net.Http.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Stena.Canvas.Api;
using Stena.Contracts;
using Stena.ServiceDefaults;
using Xunit;

namespace Stena.Canvas.Tests;

public class BanTests(CanvasApp app)
{
	private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

	[Fact]
	public async Task UserBanned_StopsPlacement()
	{
		var user = Guid.NewGuid();

		await app.Services.GetRequiredService<IBus>().Publish(new UserBanned(user), _ct);
		for (var i = 0; i < 100 && !await app.Redis.SetContainsAsync(Keys.Bans, user.ToString()); i++)
		{
			await Task.Delay(50, _ct);
		}

		var res = await app.As(Roles.Player, user).PostAsJsonAsync("/walls/1/pixels", new PlaceRequest(0, 0, 1, Guid.NewGuid()), _ct);

		Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
	}
}
