using MassTransit;
using Microsoft.EntityFrameworkCore;
using Stena.Identity.Domain;

namespace Stena.Identity.Data;

public class IdentityDb(DbContextOptions<IdentityDb> options) : DbContext(options)
{
	public DbSet<User> Users => Set<User>();

	protected override void OnModelCreating(ModelBuilder b)
	{
		b.Entity<User>(e =>
		{
			e.Property(u => u.Email).HasMaxLength(256);
			e.Property(u => u.Name).HasMaxLength(32);
			e.Property(u => u.Role).HasMaxLength(16);
			e.HasIndex(u => u.Email).IsUnique();
			e.HasIndex(u => u.Name).IsUnique();
		});
		b.AddInboxStateEntity();
		b.AddOutboxMessageEntity();
		b.AddOutboxStateEntity();
	}
}
