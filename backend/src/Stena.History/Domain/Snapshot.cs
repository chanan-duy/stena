namespace Stena.History.Domain;

public class Snapshot
{
	public int Id { get; init; }
	public int WallId { get; init; }
	public long Ms { get; init; }
	public long Seq { get; init; }
	public int Width { get; init; }
	public int Height { get; init; }
	public required byte[] Pixels { get; init; }
}
