using Microsoft.EntityFrameworkCore;
using Stena.History.Domain;

namespace Stena.History.Data;

public class HistoryDb(DbContextOptions<HistoryDb> options) : DbContext(options)
{
	public DbSet<PixelEvent> Pixels => Set<PixelEvent>();
	public DbSet<Cursor> Cursors => Set<Cursor>();
	public DbSet<Snapshot> Snapshots => Set<Snapshot>();

	protected override void OnModelCreating(ModelBuilder b)
	{
		b.Entity<PixelEvent>(e =>
		{
			e.HasKey(p => new { p.WallId, p.Ms, p.Seq });
			e.HasIndex(p => new { p.WallId, p.X, p.Y, p.Ms });
			e.Property(p => p.UserName).HasMaxLength(32);
		});
		b.Entity<Cursor>(e =>
		{
			e.HasKey(c => c.WallId);
			e.Property(c => c.WallId).ValueGeneratedNever();
			e.Ignore(c => c.StreamId);
		});
		b.Entity<Snapshot>(e => e.HasIndex(s => new { s.WallId, s.Ms }));
	}
}
