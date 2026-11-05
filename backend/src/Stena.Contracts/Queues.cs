namespace Stena.Contracts;

public static class Queues
{
	public static readonly Uri ComputeArea = new("queue:compute-area");
	public static readonly Uri BanUser = new("queue:ban-user");
}
