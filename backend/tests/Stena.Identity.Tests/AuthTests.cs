using System.Net;
using System.Net.Http.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Stena.Identity.Api;
using Xunit;

namespace Stena.Identity.Tests;

public class AuthTests(IdentityApp app)
{
	private readonly HttpClient _client = app.Client;
	private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

	[Fact]
	public async Task Register_ThenLogin_IssuesPlayerToken()
	{
		await PostAsync("/auth/register", new RegisterRequest("Ann@Example.com", "ann", "annpassword"));

		var res = await PostAsync("/auth/login", new LoginRequest("ann@example.com", "annpassword"));
		var token = new JsonWebToken((await res.Content.ReadFromJsonAsync<TokenResponse>(_ct))!.Token);

		Assert.Equal(HttpStatusCode.OK, res.StatusCode);
		Assert.Equal("ann", token.GetClaim("name").Value);
		Assert.Equal("player", token.GetClaim("role").Value);
	}

	[Fact]
	public async Task Login_SeededAdmin_IssuesAdminToken()
	{
		var res = await PostAsync("/auth/login", new LoginRequest("admin@stena.test", "admin123"));
		var token = new JsonWebToken((await res.Content.ReadFromJsonAsync<TokenResponse>(_ct))!.Token);

		Assert.Equal("admin", token.GetClaim("role").Value);
	}

	[Fact]
	public async Task Login_WrongPassword_IsUnauthorized()
	{
		var res = await PostAsync("/auth/login", new LoginRequest("admin@stena.test", "wrong-password"));

		Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
	}

	[Fact]
	public async Task Register_TakenName_IsConflict()
	{
		var res = await PostAsync("/auth/register", new RegisterRequest("other@example.com", "admin", "password123"));

		Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
	}

	private Task<HttpResponseMessage> PostAsync<T>(string url, T body)
	{
		return _client.PostAsJsonAsync(url, body, _ct);
	}
}
