using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Testcontainers.PostgreSql;
using Xunit;

namespace Stena.Render.Tests;

public class ImageTests : IAsyncLifetime
{
	private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:18-alpine").Build();
	private WebApplicationFactory<Program> _factory = null!;

	public async ValueTask InitializeAsync()
	{
		await _db.StartAsync();
		var wall = """{"id":7,"width":2,"height":2,"palette":["#ffffff","#0000ff"],"startsAt":"2026-01-01T00:00:00Z","endsAt":"2027-01-01T00:00:00Z"}""";
		_factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
			.UseSetting("ConnectionStrings:db", _db.GetConnectionString())
			.UseSetting("Jwt:Key", "test-key-0123456789abcdef0123456789abcdef")
			.UseSetting("Services:canvas", "http://canvas")
			.UseSetting("Services:history", "http://history")
			.UseSetting("Storage:Path", Path.Combine(Path.GetTempPath(), "stena-render-tests"))
			.ConfigureTestServices(s =>
			{
				s.AddHttpClient("canvas").ConfigurePrimaryHttpMessageHandler(() => new Stub(req =>
					req.RequestUri!.AbsolutePath == "/walls/7" ? new StringContent(wall, Encoding.UTF8, "application/json") : null));
				s.AddHttpClient("history").ConfigurePrimaryHttpMessageHandler(() => new Stub(_ => new ByteArrayContent([0, 1, 1, 0])));
			}));
	}

	public async ValueTask DisposeAsync()
	{
		await _factory.DisposeAsync();
		await _db.DisposeAsync();
	}

	[Fact]
	public async Task Image_DrawsWallFromHistory()
	{
		var ct = TestContext.Current.CancellationToken;
		var client = _factory.CreateClient();

		var png = await client.GetByteArrayAsync("/render/walls/7/image.png?scale=3", ct);
		var missing = await client.GetAsync("/render/walls/8/image.png", ct);
		using var image = SKBitmap.Decode(png);

		Assert.Equal((6, 6), (image.Width, image.Height));
		Assert.Equal(SKColors.Blue, image.GetPixel(4, 1));
		Assert.Equal(SKColors.White, image.GetPixel(4, 4));
		Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
	}

	private class Stub(Func<HttpRequestMessage, HttpContent?> respond) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
		{
			var content = respond(request);
			return Task.FromResult(content is null
				? new HttpResponseMessage(HttpStatusCode.NotFound)
				: new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
		}
	}
}
