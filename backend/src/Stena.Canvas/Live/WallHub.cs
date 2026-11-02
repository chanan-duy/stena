using Microsoft.AspNetCore.SignalR;

namespace Stena.Canvas.Live;

public class WallHub : Hub
{
	public static string Group(int wallId)
	{
		return $"wall:{wallId}";
	}

	public Task Watch(int wallId)
	{
		return Groups.AddToGroupAsync(Context.ConnectionId, Group(wallId));
	}

	public Task Unwatch(int wallId)
	{
		return Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(wallId));
	}
}
