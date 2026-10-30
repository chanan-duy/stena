using Stena.ServiceDefaults;

namespace Stena.Identity.Domain;

public class User
{
	public Guid Id { get; init; } = Guid.CreateVersion7();
	public required string Email { get; init; }
	public required string Name { get; init; }
	public string PasswordHash { get; set; } = "";
	public string Role { get; init; } = Roles.Player;
	public bool Banned { get; set; }
}
