using System.Net;
using System.Net.Http.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Stena.Canvas.Api;
using Stena.Canvas.Domain;
using Stena.Contracts;
using Stena.ServiceDefaults;
using Xunit;

namespace Stena.Canvas.Tests;

public class RollbackTests(CanvasApp app)
{
	private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

	[Fact]
	public async Task Rollback_LocksWallThenWritesArea()
	{
		var now = DateTimeOffset.UtcNow;
		var admin = app.As(Roles.Admin);
		var player = app.As(Roles.Player);
		var spec = new WallSpec("Rollback", 3, 1, ["#ffffff", "#000000", "#ff0000"], 60, 10, now.AddHours(-1), now.AddHours(1));
		var wall = (await (await admin.PostAsJsonAsync("/walls", spec, _ct)).Content.ReadFromJsonAsync<WallView>(_ct))!.Id;
		await player.PostAsJsonAsync($"/walls/{wall}/pixels", new PlaceRequest(0, 0, 2, Guid.NewGuid()), _ct);
		await player.PostAsJsonAsync($"/walls/{wall}/pixels", new PlaceRequest(2, 0, 2, Guid.NewGuid()), _ct);

		var asked = new TaskCompletionSource<ComputeArea>(TaskCreationOptions.RunContinuationsAsynchronously);
		var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var history = app.Services.GetRequiredService<IBus>().ConnectReceiveEndpoint("compute-area", e => e.Handler<ComputeArea>(async ctx =>
		{
			if (ctx.Message.WallId == wall)
			{
				asked.TrySetResult(ctx.Message);
				await answer.Task;
				await ctx.Publish(new AreaComputed(ctx.Message.RollbackId, [1, 0]));
			}
		}));
		await history.Ready;

		var res = await admin.PostAsJsonAsync($"/walls/{wall}/rollbacks", new RollbackRequest(0, 0, 2, 1, now, null), _ct);
		var id = (await res.Content.ReadFromJsonAsync<RollbackView>(_ct))!.Id;
		await asked.Task.WaitAsync(TimeSpan.FromSeconds(10), _ct);
		var blocked = await player.PostAsJsonAsync($"/walls/{wall}/pixels", new PlaceRequest(1, 0, 1, Guid.NewGuid()), _ct);
		answer.SetResult();

		var view = await WaitDoneAsync(admin, wall, id);
		var bitmap = await app.Client.GetByteArrayAsync($"/walls/{wall}/bitmap", _ct);
		await history.StopAsync(_ct);

		Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
		Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
		Assert.Equal("Completed", view.State);
		Assert.Equal(1, view.Changed);
		Assert.Equal([1, 0, 2], bitmap);
		Assert.False(await app.Redis.KeyExistsAsync(Keys.Lock(wall)));
	}

	private async Task<RollbackView> WaitDoneAsync(HttpClient admin, int wall, Guid id)
	{
		for (var i = 0; i < 100; i++)
		{
			var view = (await admin.GetFromJsonAsync<RollbackView>($"/walls/{wall}/rollbacks/{id}", _ct))!;
			if (view.State is "Completed" or "Failed")
			{
				return view;
			}

			await Task.Delay(100, _ct);
		}

		throw new TimeoutException($"Rollback {id} did not finish");
	}
}
