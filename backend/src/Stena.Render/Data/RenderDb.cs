using Microsoft.EntityFrameworkCore;
using Stena.Render.Domain;

namespace Stena.Render.Data;

public class RenderDb(DbContextOptions<RenderDb> options) : DbContext(options)
{
	public DbSet<Timelapse> Timelapses => Set<Timelapse>();

	protected override void OnModelCreating(ModelBuilder b)
	{
		b.Entity<Timelapse>(e =>
		{
			e.Property(t => t.Status).HasConversion<string>().HasMaxLength(16);
			e.HasIndex(t => new { t.Status, t.CreatedAt });
		});
	}
}
