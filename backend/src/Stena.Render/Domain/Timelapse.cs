using System.Text.Json.Serialization;

namespace Stena.Render.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<TimelapseStatus>))]
public enum TimelapseStatus
{
	Queued,
	Running,
	Done,
	Failed,
}

public class Timelapse
{
	public Guid Id { get; init; } = Guid.CreateVersion7();
	public int WallId { get; init; }
	public TimelapseStatus Status { get; set; }
	public DateTimeOffset CreatedAt { get; init; }
	public string? Error { get; set; }
}
