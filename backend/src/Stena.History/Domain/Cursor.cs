using Stena.Contracts;

namespace Stena.History.Domain;

public class Cursor
{
	public int WallId { get; init; }
	public long Ms { get; set; }
	public long Seq { get; set; }

	public string StreamId => $"{Ms}-{Seq}";

	public void MoveTo(string streamId)
	{
		(Ms, Seq) = StreamIds.Parse(streamId);
	}
}
