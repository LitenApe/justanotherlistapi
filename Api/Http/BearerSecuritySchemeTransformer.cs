using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.Http;

/// <summary>
/// Tells the OpenAPI document that the API takes bearer tokens: the ones
/// <c>POST /api/auth/sign-in</c> and <c>POST /api/auth/sign-up</c> answer with.
/// </summary>
public sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider schemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        IEnumerable<AuthenticationScheme> authenticationSchema =
            await schemeProvider.GetAllSchemesAsync();

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

        document.Security ??= [];
        document.Security.Add(
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
            }
        );
    }
}
