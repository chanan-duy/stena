using MassTransit;
using Stena.Contracts;
using Stena.Identity.Data;

namespace Stena.Identity.Messaging;

public class BanUserConsumer(Bans bans) : IConsumer<BanUser>
{
	public async Task Consume(ConsumeContext<BanUser> context)
	{
		var result = await bans.BanAsync(context.Message.UserId, context.CancellationToken);
		if (result != BanResult.Banned)
		{
			throw new InvalidOperationException($"User {context.Message.UserId} was not banned: {result}");
		}

		await context.Publish(new BanDone(context.Message.RollbackId));
	}
}
