using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Stena.Identity.Api;
using Xunit;

namespace Stena.Identity.Tests;

public class BanTests(IdentityApp app)
{
	private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

	[Fact]
	public async Task Ban_ByAdmin_BlocksLogin()
	{
		var player = await RegisterAsync("vandal");
		var admin = await LoginAsync("admin@stena.test", "admin123");

		var ban = await SendAsync(HttpMethod.Post, $"/auth/users/{Id(player)}/ban", admin);
		var login = await app.Client.PostAsJsonAsync("/auth/login", new LoginRequest("vandal@example.com", "vandalpassword"), _ct);

		Assert.Equal(HttpStatusCode.NoContent, ban.StatusCode);
		Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
	}

	[Fact]
	public async Task Ban_ByPlayer_IsForbidden()
	{
		var player = await RegisterAsync("someone");

		var res = await SendAsync(HttpMethod.Post, $"/auth/users/{Id(player)}/ban", player);

		Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
	}

	[Fact]
	public async Task Ban_Admin_IsConflict()
	{
		var admin = await LoginAsync("admin@stena.test", "admin123");

		var res = await SendAsync(HttpMethod.Post, $"/auth/users/{Id(admin)}/ban", admin);

		Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
	}

	private async Task<string> RegisterAsync(string name)
	{
		var res = await app.Client.PostAsJsonAsync("/auth/register", new RegisterRequest($"{name}@example.com", name, $"{name}password"), _ct);
		return (await res.Content.ReadFromJsonAsync<TokenResponse>(_ct))!.Token;
	}

	private async Task<string> LoginAsync(string email, string password)
	{
		var res = await app.Client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password), _ct);
		return (await res.Content.ReadFromJsonAsync<TokenResponse>(_ct))!.Token;
	}

	private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token)
	{
		var req = new HttpRequestMessage(method, url) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
		return app.Client.SendAsync(req, _ct);
	}

	private static string Id(string token)
	{
		return new JsonWebToken(token).Subject;
	}
}
