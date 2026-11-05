using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Stena.Render.Data;
using Stena.Render.Domain;

namespace Stena.Render.Drawing;

public class Timelapser(IServiceScopeFactory scopes, IConfiguration config, TimeProvider time, ILogger<Timelapser> log) : BackgroundService
{
	private const int Frames = 60;
	private const int Side = 512;

	public static string PathFor(IConfiguration config, Guid id)
	{
		return Path.Combine(config["Storage:Path"]!, $"{id}.mp4");
	}

	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		Directory.CreateDirectory(config["Storage:Path"]!);
		using (var scope = scopes.CreateScope())
		{
			await scope.ServiceProvider.GetRequiredService<RenderDb>().Timelapses
				.Where(t => t.Status == TimelapseStatus.Running)
				.ExecuteUpdateAsync(t => t.SetProperty(j => j.Status, TimelapseStatus.Queued), ct);
		}

		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
		while (await timer.WaitForNextTickAsync(ct))
		{
			using var scope = scopes.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<RenderDb>();
			var job = await db.Timelapses.OrderBy(t => t.CreatedAt).FirstOrDefaultAsync(t => t.Status == TimelapseStatus.Queued, ct);
			if (job is null)
			{
				continue;
			}

			job.Status = TimelapseStatus.Running;
			await db.SaveChangesAsync(ct);
			try
			{
				await MakeAsync(job, scope.ServiceProvider.GetRequiredService<Sources>(), ct);
				job.Status = TimelapseStatus.Done;
			}
			catch (Exception e) when (e is not OperationCanceledException)
			{
				log.LogWarning(e, "Timelapse {Id} failed", job.Id);
				job.Status = TimelapseStatus.Failed;
				job.Error = e.Message;
			}

			await db.SaveChangesAsync(CancellationToken.None);
		}
	}

	private async Task MakeAsync(Timelapse job, Sources sources, CancellationToken ct)
	{
		var wall = await sources.WallAsync(job.WallId, ct) ?? throw new InvalidOperationException("Wall not found");
		var end = new[] { time.GetUtcNow(), wall.EndsAt }.Min();
		if (end <= wall.StartsAt)
		{
			throw new InvalidOperationException("Wall has not started");
		}

		var path = PathFor(config, job.Id);
		var part = path + ".part";
		using var ffmpeg = Process.Start(new ProcessStartInfo("ffmpeg")
		{
			ArgumentList =
			{
				"-y", "-loglevel", "error", "-f", "image2pipe", "-framerate", "10", "-i", "-",
				"-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-f", "mp4", part,
			},
			RedirectStandardInput = true,
			RedirectStandardError = true,
		})!;
		var errors = ffmpeg.StandardError.ReadToEndAsync(ct);

		try
		{
			var scale = Math.Max(1, Side / Math.Max(wall.Width, wall.Height));
			var span = (end - wall.StartsAt).Ticks;
			for (var i = 0; i < Frames; i++)
			{
				var at = wall.StartsAt.AddTicks((long)((Int128)span * i / (Frames - 1)));
				using var frame = Painter.Draw(wall.Width, wall.Height, wall.Palette, await sources.PixelsAsync(job.WallId, at, ct), scale);
				await ffmpeg.StandardInput.BaseStream.WriteAsync(Painter.Png(frame), ct);
			}

			ffmpeg.StandardInput.Close();
			await ffmpeg.WaitForExitAsync(ct);
			if (ffmpeg.ExitCode != 0)
			{
				throw new InvalidOperationException($"ffmpeg failed: {(await errors).Trim()}");
			}

			File.Move(part, path, true);
		}
		catch
		{
			if (!ffmpeg.HasExited)
			{
				ffmpeg.Kill();
				await ffmpeg.WaitForExitAsync(CancellationToken.None);
			}

			File.Delete(part);
			throw;
		}
	}
}
