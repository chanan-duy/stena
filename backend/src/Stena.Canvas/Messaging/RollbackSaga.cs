using MassTransit;
using Stena.Canvas.Redis;
using Stena.Contracts;

namespace Stena.Canvas.Messaging;

public class Rollback : SagaStateMachineInstance
{
	public Guid CorrelationId { get; set; }
	public string State { get; set; } = "";
	public int WallId { get; set; }
	public int X { get; set; }
	public int Y { get; set; }
	public int Width { get; set; }
	public int Height { get; set; }
	public DateTimeOffset At { get; set; }
	public Guid? VandalId { get; set; }
	public Guid AdminId { get; set; }
	public string AdminName { get; set; } = "";
	public int Changed { get; set; }
	public string? Error { get; set; }
}

public class RollbackSaga : MassTransitStateMachine<Rollback>
{
	public RollbackSaga()
	{
		InstanceState(r => r.State);
		Event(() => Requested, e => e.CorrelateById(m => m.Message.RollbackId));
		Event(() => Computed, e => e.CorrelateById(m => m.Message.RollbackId));
		Event(() => ComputeFailed, e => e.CorrelateById(m => m.Message.Message.RollbackId));
		Event(() => Banned, e => e.CorrelateById(m => m.Message.RollbackId));
		Event(() => BanFailed, e => e.CorrelateById(m => m.Message.Message.RollbackId));

		Initially(
			When(Requested)
				.Then(ctx =>
				{
					var m = ctx.Message;
					(ctx.Saga.WallId, ctx.Saga.X, ctx.Saga.Y, ctx.Saga.Width, ctx.Saga.Height) = (m.WallId, m.X, m.Y, m.Width, m.Height);
					(ctx.Saga.At, ctx.Saga.VandalId, ctx.Saga.AdminId, ctx.Saga.AdminName) = (m.At.ToUniversalTime(), m.VandalId, m.AdminId, m.AdminName);
				})
				.ThenAsync(async ctx =>
				{
					if (!await Store(ctx).LockAsync(ctx.Saga.WallId, ctx.Saga.CorrelationId))
					{
						ctx.Saga.Error = "Wall is locked by another rollback";
					}
				})
				.IfElse(ctx => ctx.Saga.Error is null,
					ok => ok
						.Send(Queues.ComputeArea, ctx => new ComputeArea(ctx.Saga.CorrelationId, ctx.Saga.WallId, ctx.Saga.X, ctx.Saga.Y, ctx.Saga.Width,
							ctx.Saga.Height, ctx.Saga.At))
						.TransitionTo(Computing),
					failed => failed.TransitionTo(Failed)));

		During(Computing,
			When(Computed)
				.ThenAsync(async ctx =>
				{
					var s = ctx.Saga;
					var changed = await Store(ctx).ApplyAsync(s.CorrelationId, s.WallId, s.X, s.Y, s.Width, s.Height, ctx.Message.Pixels, s.AdminId,
						s.AdminName);
					if (changed is null)
					{
						s.Error = "Wall lock expired before the area was written";
					}
					else
					{
						s.Changed = changed.Value;
					}
				})
				.IfElse(ctx => ctx.Saga.Error is null && ctx.Saga.VandalId is not null,
					ban => ban
						.Send(Queues.BanUser, ctx => new BanUser(ctx.Saga.CorrelationId, ctx.Saga.VandalId!.Value))
						.TransitionTo(Banning),
					done => done
						.ThenAsync(UnlockAsync)
						.IfElse(ctx => ctx.Saga.Error is null, ok => ok.TransitionTo(Completed), failed => failed.TransitionTo(Failed))),
			When(ComputeFailed)
				.Then(ctx => ctx.Saga.Error = "History could not compute the area")
				.ThenAsync(UnlockAsync)
				.TransitionTo(Failed));

		During(Banning,
			When(Banned)
				.ThenAsync(UnlockAsync)
				.TransitionTo(Completed),
			When(BanFailed)
				.Then(ctx => ctx.Saga.Error = "Area restored, but the vandal was not banned")
				.ThenAsync(UnlockAsync)
				.TransitionTo(Failed));

		During(Computing, Ignore(Requested));
		During(Banning, Ignore(Requested), Ignore(Computed), Ignore(ComputeFailed));
		During(Completed, Failed, Ignore(Requested), Ignore(Computed), Ignore(ComputeFailed), Ignore(Banned), Ignore(BanFailed));
	}

	public State Computing { get; private set; } = null!;
	public State Banning { get; private set; } = null!;
	public State Completed { get; private set; } = null!;
	public State Failed { get; private set; } = null!;

	public Event<RollbackRequested> Requested { get; private set; } = null!;
	public Event<AreaComputed> Computed { get; private set; } = null!;
	public Event<Fault<ComputeArea>> ComputeFailed { get; private set; } = null!;
	public Event<BanDone> Banned { get; private set; } = null!;
	public Event<Fault<BanUser>> BanFailed { get; private set; } = null!;

	private static Rollbacks Store(BehaviorContext<Rollback> ctx)
	{
		return ctx.GetServiceOrCreateInstance<Rollbacks>();
	}

	private static Task UnlockAsync<T>(BehaviorContext<Rollback, T> ctx) where T : class
	{
		return Store(ctx).UnlockAsync(ctx.Saga.WallId, ctx.Saga.CorrelationId);
	}
}
