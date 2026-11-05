using MassTransit;
using StackExchange.Redis;
using Stena.Contracts;

namespace Stena.Canvas.Messaging;

public class UserBannedConsumer(IConnectionMultiplexer redis) : IConsumer<UserBanned>
{
	public Task Consume(ConsumeContext<UserBanned> context)
	{
		return redis.GetDatabase().SetAddAsync(Keys.Bans, context.Message.UserId.ToString());
	}
}
