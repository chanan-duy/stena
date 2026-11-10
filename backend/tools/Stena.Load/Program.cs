using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NBomber.CSharp;

namespace Stena.Load;

public record TokenResponse(string Token);

public record WallView(int Id);

public class Program
{
	private const int Size = 256;

	public static async Task Main(string[] args)
	{
		var options = args.Chunk(2).ToDictionary(a => a[0].TrimStart('-'), a => a.ElementAtOrDefault(1) ?? "");
		var url = options.GetValueOrDefault("url", "http://localhost:8080");
		var users = int.Parse(options.GetValueOrDefault("users", "1000"));
		var rate = int.Parse(options.GetValueOrDefault("rate", "1000"));
		var seconds = int.Parse(options.GetValueOrDefault("seconds", "30"));

		using var http = new HttpClient { BaseAddress = new Uri(url) };
		var wall = await CreateWallAsync(http);
		var tokens = await RegisterAsync(http, users);
		Console.WriteLine($"Wall {wall}, {tokens.Length} users, {rate} pixels/s for {seconds} s, then {rate * 2}/s for 10 s");

		var scenario = Scenario.Create("place", async _ =>
			{
				var req = new HttpRequestMessage(HttpMethod.Post, $"/walls/{wall}/pixels")
				{
					Content = JsonContent.Create(new
					{
						x = Random.Shared.Next(Size),
						y = Random.Shared.Next(Size),
						color = Random.Shared.Next(16),
						requestId = Guid.NewGuid(),
					}),
					Headers = { Authorization = new AuthenticationHeaderValue("Bearer", tokens[Random.Shared.Next(tokens.Length)]) },
				};
				using var res = await http.SendAsync(req);
				return res.StatusCode == HttpStatusCode.OK ? Response.Ok() : Response.Fail(statusCode: ((int)res.StatusCode).ToString());
			})
			.WithoutWarmUp()
			.WithLoadSimulations(
				Simulation.Inject(rate, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(seconds)),
				Simulation.Inject(rate * 2, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)));

		var result = NBomberRunner.RegisterScenarios(scenario)
			.WithReportFolder("load-report")
			.WithReportFileName("load")
			.Run();

		var placed = result.ScenarioStats[0].Ok.Request.Count;
		Console.WriteLine($"Placed {placed} pixels, {result.ScenarioStats[0].Fail.Request.Count} refused");
		Console.WriteLine(await SameAsync(http, wall) ? "History matches the wall" : "History DIFFERS from the wall");
	}

	private static async Task<int> CreateWallAsync(HttpClient http)
	{
		var login = await http.PostAsJsonAsync("/auth/login", new { email = "admin@stena.test", password = "admin123" });
		var admin = (await login.Content.ReadFromJsonAsync<TokenResponse>())!.Token;
		var now = DateTimeOffset.UtcNow;
		var palette = Enumerable.Range(0, 16).Select(i => $"#{i * 16:x2}{255 - i * 16:x2}{i * 8:x2}").ToArray();
		var req = new HttpRequestMessage(HttpMethod.Post, "/walls")
		{
			Content = JsonContent.Create(new
			{
				name = $"Load {now:HH:mm}",
				width = Size,
				height = Size,
				palette,
				chargeSeconds = 1,
				maxCharges = 100,
				startsAt = now.AddMinutes(-1),
				endsAt = now.AddHours(1),
			}),
			Headers = { Authorization = new AuthenticationHeaderValue("Bearer", admin) },
		};
		var res = await http.SendAsync(req);
		res.EnsureSuccessStatusCode();
		return (await res.Content.ReadFromJsonAsync<WallView>())!.Id;
	}

	private static async Task<string[]> RegisterAsync(HttpClient http, int count)
	{
		var tokens = new string[count];
		await Parallel.ForAsync(0, count, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (i, ct) =>
		{
			var name = $"load{i}";
			var body = new { email = $"{name}@load.test", name, password = $"{name}-password" };
			var res = await http.PostAsJsonAsync("/auth/login", body, ct);
			if (res.StatusCode == HttpStatusCode.Unauthorized)
			{
				res = await http.PostAsJsonAsync("/auth/register", body, ct);
			}

			res.EnsureSuccessStatusCode();
			tokens[i] = (await res.Content.ReadFromJsonAsync<TokenResponse>(ct))!.Token;
		});
		return tokens;
	}

	private static async Task<bool> SameAsync(HttpClient http, int wall)
	{
		var bitmap = await http.GetByteArrayAsync($"/walls/{wall}/bitmap");
		var history = await http.GetByteArrayAsync($"/history/walls/{wall}/area");
		return bitmap.AsSpan().SequenceEqual(history);
	}
}
