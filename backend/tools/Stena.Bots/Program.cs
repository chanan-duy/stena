using System.Net.Http.Json;

namespace Stena.Bots;

public class Program
{
	public static async Task Main(string[] args)
	{
		var options = args.Chunk(2).ToDictionary(a => a[0].TrimStart('-'), a => a.ElementAtOrDefault(1) ?? "");
		var url = options.GetValueOrDefault("url", "http://localhost:8080");
		var wallId = int.Parse(options.GetValueOrDefault("wall", "1"));
		var count = int.Parse(options.GetValueOrDefault("bots", "10"));
		var image = options.GetValueOrDefault("image", Path.Combine(AppContext.BaseDirectory, "stena.png"));

		using var http = new HttpClient { BaseAddress = new Uri(url) };
		var wall = (await http.GetFromJsonAsync<Wall>($"/walls/{wallId}"))!;
		var target = Target.Load(image, wall, int.Parse(options.GetValueOrDefault("x", "0")), int.Parse(options.GetValueOrDefault("y", "0")));
		var bots = Enumerable.Range(1, count).Select(n => new Bot(http, wall, target, $"bot{n}")).ToArray();
		Console.WriteLine($"{count} bots draw {target.Count} pixels on {wall.Name}. Ctrl+C stops.");

		using var cts = new CancellationTokenSource();
		Console.CancelKeyPress += (_, e) =>
		{
			e.Cancel = true;
			cts.Cancel();
		};
		var run = Task.WhenAll(bots.Select(b => b.RunAsync(cts.Token)));
		while (!run.IsCompleted)
		{
			await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(5)));
			Console.WriteLine($"placed {bots.Sum(b => b.Placed)}");
		}

		try
		{
			await run;
		}
		catch (OperationCanceledException)
		{
		}
	}
}
