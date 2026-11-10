using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Stena.Bots;

public record TokenResponse(string Token);

public record Placed(int Charges, long NextChargeMs);

public class Bot(HttpClient http, Wall wall, Dictionary<(int X, int Y), byte> target, string name)
{
	public int Placed { get; private set; }

	public async Task RunAsync(CancellationToken ct)
	{
		var token = await LoginAsync(ct);
		while (!ct.IsCancellationRequested)
		{
			var bitmap = await http.GetByteArrayAsync($"/walls/{wall.Id}/bitmap", ct);
			var todo = target.Where(t => bitmap[t.Key.Y * wall.Width + t.Key.X] != t.Value).ToArray();
			if (todo.Length == 0)
			{
				await Task.Delay(TimeSpan.FromSeconds(5), ct);
				continue;
			}

			var ((x, y), color) = todo[Random.Shared.Next(todo.Length)];
			var req = new HttpRequestMessage(HttpMethod.Post, $"/walls/{wall.Id}/pixels")
			{
				Content = JsonContent.Create(new { x, y, color, requestId = Guid.NewGuid() }),
				Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
			};
			using var res = await http.SendAsync(req, ct);
			switch (res.StatusCode)
			{
				case HttpStatusCode.OK:
					Placed++;
					var placed = (await res.Content.ReadFromJsonAsync<Placed>(ct))!;
					await Task.Delay(placed.Charges > 0 ? TimeSpan.FromMilliseconds(Random.Shared.Next(300, 1500)) : TimeSpan.FromMilliseconds(placed.NextChargeMs), ct);
					break;
				case HttpStatusCode.TooManyRequests:
					await Task.Delay(res.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5), ct);
					break;
				case HttpStatusCode.Unauthorized:
					token = await LoginAsync(ct);
					break;
				case HttpStatusCode.Forbidden:
					Console.WriteLine($"{name} is banned");
					return;
				default:
					await Task.Delay(TimeSpan.FromSeconds(2), ct);
					break;
			}
		}
	}

	private async Task<string> LoginAsync(CancellationToken ct)
	{
		var email = $"{name}@bots.test";
		var password = $"{name}-password";
		var res = await http.PostAsJsonAsync("/auth/login", new { email, password }, ct);
		if (res.StatusCode == HttpStatusCode.Unauthorized)
		{
			res = await http.PostAsJsonAsync("/auth/register", new { email, name, password }, ct);
		}

		res.EnsureSuccessStatusCode();
		return (await res.Content.ReadFromJsonAsync<TokenResponse>(ct))!.Token;
	}
}
