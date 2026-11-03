using Microsoft.EntityFrameworkCore;
using Stena.Canvas.Domain;

namespace Stena.Canvas.Data;

public class CanvasDb(DbContextOptions<CanvasDb> options) : DbContext(options)
{
	public DbSet<Wall> Walls => Set<Wall>();

	protected override void OnModelCreating(ModelBuilder b)
	{
		b.Entity<Wall>(e => e.Property(w => w.Name).HasMaxLength(64));
	}
}
