using MassTransit;
using Stena.Contracts;
using Stena.History.Data;
using Stena.History.Domain;

namespace Stena.History.Messaging;

public class ComputeAreaConsumer(Timeline timeline) : IConsumer<ComputeArea>
{
	public async Task Consume(ConsumeContext<ComputeArea> context)
	{
		var m = context.Message;
		if (!await timeline.CatchUpAsync(m.WallId, m.At, context.CancellationToken))
		{
			throw new InvalidOperationException($"History of wall {m.WallId} is behind {m.At}");
		}

		var pixels = await timeline.AreaAsync(m.WallId, new Area(m.X, m.Y, m.Width, m.Height), m.At, context.CancellationToken);
		await context.Publish(new AreaComputed(m.RollbackId, pixels));
	}
}
