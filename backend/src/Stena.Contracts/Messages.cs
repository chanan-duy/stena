namespace Stena.Contracts;

public record UserBanned(Guid UserId);

public record RollbackRequested(
	Guid RollbackId,
	int WallId,
	int X,
	int Y,
	int Width,
	int Height,
	DateTimeOffset At,
	Guid? VandalId,
	Guid AdminId,
	string AdminName
);

public record ComputeArea(Guid RollbackId, int WallId, int X, int Y, int Width, int Height, DateTimeOffset At);

public record AreaComputed(Guid RollbackId, byte[] Pixels);

public record BanUser(Guid RollbackId, Guid UserId);

public record BanDone(Guid RollbackId);
