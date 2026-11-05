using MassTransit;
using Microsoft.EntityFrameworkCore;
using Stena.Canvas.Domain;
using Stena.Canvas.Messaging;

namespace Stena.Canvas.Data;

public class CanvasDb(DbContextOptions<CanvasDb> options) : DbContext(options)
{
	public DbSet<Wall> Walls => Set<Wall>();
	public DbSet<Rollback> Rollbacks => Set<Rollback>();

	protected override void OnModelCreating(ModelBuilder b)
	{
		b.Entity<Wall>(e => e.Property(w => w.Name).HasMaxLength(64));
		b.Entity<Rollback>(e =>
		{
			e.HasKey(r => r.CorrelationId);
			e.Property(r => r.State).HasMaxLength(32);
			e.Property(r => r.AdminName).HasMaxLength(32);
		});
		b.AddInboxStateEntity();
		b.AddOutboxMessageEntity();
		b.AddOutboxStateEntity();
	}
}
