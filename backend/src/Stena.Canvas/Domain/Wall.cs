using System.Text.Json.Serialization;

namespace Stena.Canvas.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<WallStatus>))]
public enum WallStatus
{
	Upcoming,
	Live,
	Finished,
}

public class Wall
{
	public int Id { get; init; }
	public required string Name { get; set; }
	public int Width { get; set; }
	public int Height { get; set; }
	public required string[] Palette { get; set; }
	public int ChargeSeconds { get; set; }
	public int MaxCharges { get; set; }
	public DateTimeOffset StartsAt { get; set; }
	public DateTimeOffset EndsAt { get; set; }

	public WallStatus StatusAt(DateTimeOffset now)
	{
		return now < StartsAt ? WallStatus.Upcoming : now < EndsAt ? WallStatus.Live : WallStatus.Finished;
	}

	public static Wall Create(WallSpec spec)
	{
		var wall = new Wall { Name = spec.Name, Palette = spec.Palette };
		wall.Apply(spec);
		return wall;
	}

	public bool TryEdit(WallSpec spec, DateTimeOffset now)
	{
		var started = StatusAt(now) != WallStatus.Upcoming;
		if (started && (Utc(spec.StartsAt) != StartsAt || spec.Width != Width || spec.Height != Height || !spec.Palette.SequenceEqual(Palette)))
		{
			return false;
		}

		Apply(spec);
		return true;
	}

	private void Apply(WallSpec spec)
	{
		Name = spec.Name;
		Width = spec.Width;
		Height = spec.Height;
		Palette = spec.Palette;
		ChargeSeconds = spec.ChargeSeconds;
		MaxCharges = spec.MaxCharges;
		StartsAt = Utc(spec.StartsAt);
		EndsAt = Utc(spec.EndsAt);
	}

	// PostgreSQL keeps microseconds, so a start time sent back unchanged must compare equal to the stored one.
	private static DateTimeOffset Utc(DateTimeOffset time)
	{
		return new DateTimeOffset(time.UtcTicks - time.UtcTicks % 10, TimeSpan.Zero);
	}
}
