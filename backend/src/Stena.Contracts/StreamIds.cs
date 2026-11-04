namespace Stena.Contracts;

public static class StreamIds
{
	public static (long Ms, long Seq) Parse(string id)
	{
		var dash = id.IndexOf('-');
		return (long.Parse(id[..dash]), long.Parse(id[(dash + 1)..]));
	}

	public static bool Before(string a, string b)
	{
		return Parse(a).CompareTo(Parse(b)) < 0;
	}
}
