using System.Net;
using Xunit;

namespace Stena.Identity.Tests;

public class HealthTests(IdentityApp app)
{
	[Fact]
	public async Task Health_WithDatabase_IsHealthy()
	{
		var res = await app.Client.GetAsync("/health", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, res.StatusCode);
	}
}
