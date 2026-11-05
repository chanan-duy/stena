namespace Stena.History.Domain;

public class PixelEvent
{
	public int WallId { get; init; }
	public long Ms { get; init; }
	public long Seq { get; init; }
	public int X { get; init; }
	public int Y { get; init; }
	public byte Color { get; init; }
	public Guid UserId { get; init; }
	public required string UserName { get; init; }
}
