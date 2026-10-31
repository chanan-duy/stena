namespace Stena.Contracts;

public static class Keys
{
	public const string Walls = "walls";
	public const string Bans = "bans";

	public static string Wall(int id)
	{
		return $"wall:{id}";
	}

	public static string Pixels(int id)
	{
		return $"wall:{id}:px";
	}

	public static string Stream(int id)
	{
		return $"wall:{id}:stream";
	}

	public static string Lock(int id)
	{
		return $"wall:{id}:lock";
	}
}
