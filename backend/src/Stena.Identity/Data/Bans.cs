using MassTransit;
using Stena.Contracts;
using Stena.ServiceDefaults;

namespace Stena.Identity.Data;

public enum BanResult
{
	Banned,
	NotFound,
	Admin,
}

public class Bans(IdentityDb db, IPublishEndpoint bus)
{
	public async Task<BanResult> BanAsync(Guid userId, CancellationToken ct)
	{
		var user = await db.Users.FindAsync([userId], ct);
		if (user is null)
		{
			return BanResult.NotFound;
		}

		if (user.Role == Roles.Admin)
		{
			return BanResult.Admin;
		}

		if (!user.Banned)
		{
			user.Banned = true;
			await bus.Publish(new UserBanned(userId), ct);
			await db.SaveChangesAsync(ct);
		}

		return BanResult.Banned;
	}
}
