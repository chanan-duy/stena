using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Stena.ServiceDefaults;

public static class Jwt
{
	public const string Issuer = "stena";

	public static SymmetricSecurityKey Key(IConfiguration config)
	{
		return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
	}
}
