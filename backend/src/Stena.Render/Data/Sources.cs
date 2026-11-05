using System.Net;

namespace Stena.Render.Data;

public record WallInfo(int Id, int Width, int Height, string[] Palette, DateTimeOffset StartsAt, DateTimeOffset EndsAt);

public class Sources(IHttpClientFactory http)
{
	public async Task<WallInfo?> WallAsync(int wallId, CancellationToken ct)
	{
		var res = await http.CreateClient("canvas").GetAsync($"/walls/{wallId}", ct);
		if (res.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}

		res.EnsureSuccessStatusCode();
		return await res.Content.ReadFromJsonAsync<WallInfo>(ct);
	}

	public Task<byte[]> PixelsAsync(int wallId, DateTimeOffset at, CancellationToken ct)
	{
		return http.CreateClient("history").GetByteArrayAsync($"/history/walls/{wallId}/area?at={Uri.EscapeDataString(at.ToString("O"))}", ct);
	}
}
