using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Stena.ServiceDefaults;

public class BearerScheme : IOpenApiDocumentTransformer
{
	public Task TransformAsync(OpenApiDocument doc, OpenApiDocumentTransformerContext context, CancellationToken ct)
	{
		doc.Components ??= new OpenApiComponents();
		doc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
		doc.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
		{
			Type = SecuritySchemeType.Http,
			Scheme = "bearer",
			BearerFormat = "JWT",
		};
		doc.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", doc)] = [] }];
		return Task.CompletedTask;
	}
}
