using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace Core.Utility
{
    public class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider schemeProvider) : IOpenApiDocumentTransformer
    {
        public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct = default)
        {
            var authenticationSchema = await schemeProvider.GetAllSchemesAsync();

            if (authenticationSchema.Any(authScheme => authScheme.Name == "Bearer"))
            {
                document.Components ??= new OpenApiComponents();
                var securitySchemeId = "Bearer";
                document.Components.SecuritySchemes.Add(securitySchemeId, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    In = ParameterLocation.Header,
                    BearerFormat = "Json Web Token",
                });

                document.SecurityRequirements.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Id = securitySchemeId,
                            Type = ReferenceType.SecurityScheme
                        }
                    }] = Array.Empty<string>()
                });
            }
        }
    }
}
