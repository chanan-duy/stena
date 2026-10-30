using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Stena.Identity.Domain;
using Stena.ServiceDefaults;

namespace Stena.Identity.Api;

public class Tokens(IConfiguration config)
{
	private static readonly JsonWebTokenHandler Handler = new();

	public string Issue(User user)
	{
		return Handler.CreateToken(new SecurityTokenDescriptor
		{
			Issuer = Jwt.Issuer,
			Audience = Jwt.Issuer,
			Expires = DateTime.UtcNow.AddDays(1),
			SigningCredentials = new SigningCredentials(Jwt.Key(config), SecurityAlgorithms.HmacSha256),
			Claims = new Dictionary<string, object>
			{
				["sub"] = user.Id.ToString(),
				["name"] = user.Name,
				["role"] = user.Role,
			},
		});
	}
}
