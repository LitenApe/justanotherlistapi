using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Core.Utility;

public class BearerSecuritySchemeTransformer(
    IAuthenticationSchemeProvider schemeProvider,
    IConfiguration configuration
) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var authenticationSchema = await schemeProvider.GetAllSchemesAsync();

        if (
            !authenticationSchema.Any(authScheme =>
                string.Equals(authScheme.Name, "Bearer", StringComparison.Ordinal)
            )
        )
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(
            StringComparer.Ordinal
        );
        document.Components.SecuritySchemes.Add(
            "Bearer",
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                In = ParameterLocation.Header,
                BearerFormat = "Json Web Token",
            }
        );

        string? authority = configuration["OAuth:Authority"];
        if (!string.IsNullOrEmpty(authority))
        {
            document.Components.SecuritySchemes.Add(
                "OAuth2",
                new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        ClientCredentials = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri($"{authority}/token"),
                            Scopes = new Dictionary<string, string>(StringComparer.Ordinal),
                        },
                    },
                }
            );
        }

        document.Security ??= [];
        document.Security.Add(
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
            }
        );
    }
}
