using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Stena.Canvas.Api;
using Stena.Canvas.Domain;
using Stena.ServiceDefaults;
using Xunit;

namespace Stena.Canvas.Tests;

public class LiveTests(CanvasApp app)
{
	private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

	[Fact]
	public async Task Place_ReachesWatcherInBatch()
	{
		var now = DateTimeOffset.UtcNow;
		var spec = new WallSpec("Live", 300, 2, ["#ffffff", "#000000", "#ff0000"], 60, 5, now.AddHours(-1), now.AddHours(1));
		var wall = (await (await app.As(Roles.Admin).PostAsJsonAsync("/walls", spec, _ct)).Content.ReadFromJsonAsync<WallView>(_ct))!.Id;

		await using var hub = new HubConnectionBuilder()
			.WithUrl(new Uri(app.Server.BaseAddress, "/hub"), o =>
			{
				o.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
				o.Transports = HttpTransportType.LongPolling;
			})
			.AddMessagePackProtocol()
			.Build();
		var batches = Channel.CreateUnbounded<(string Prev, string Last, byte[] Pixels)>();
		hub.On<int, string, string, byte[]>("pixels", (id, prev, last, pixels) =>
		{
			if (id == wall)
			{
				batches.Writer.TryWrite((prev, last, pixels));
			}
		});
		await hub.StartAsync(_ct);
		await hub.InvokeAsync("Watch", wall, _ct);

		var player = app.As(Roles.Player);
		await player.PostAsJsonAsync($"/walls/{wall}/pixels", new PlaceRequest(1, 0, 2, Guid.NewGuid()), _ct);
		var res = await player.PostAsJsonAsync($"/walls/{wall}/pixels", new PlaceRequest(258, 1, 1, Guid.NewGuid()), _ct);
		var placed = await res.Content.ReadFromJsonAsync<PlaceResponse>(_ct);

		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_ct);
		timeout.CancelAfter(TimeSpan.FromSeconds(5));
		var (prev, last, all) = await batches.Reader.ReadAsync(timeout.Token);
		while (last != placed!.StreamId)
		{
			var next = await batches.Reader.ReadAsync(timeout.Token);
			Assert.Equal(last, next.Prev);
			(last, all) = (next.Last, [..all, ..next.Pixels]);
		}

		Assert.Equal("0-0", prev);
		Assert.Equal([1, 0, 0, 0, 2, 2, 1, 1, 0, 1], all);
	}
}
