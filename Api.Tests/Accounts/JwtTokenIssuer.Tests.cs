using Api.Accounts;
using Checklist.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Api.Tests.Accounts;

public sealed class JwtTokenIssuerTests
{
    private static readonly User kim = new()
    {
        Id = Guid.NewGuid(),
        Name = "Kim",
        Email = "kim@example.com",
    };

    private readonly AuthOptions settings = new() { SigningKey = ApiFactory.SigningKey };

    [Fact]
    public async Task Issue_SignsATokenForTheUser_ThatTheSameSettingsAccept()
    {
        AccessToken token = new JwtTokenIssuer(Options.Create(settings), TimeProvider.System).Issue(
            kim
        );

        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(
            token.Value,
            new TokenValidationParameters
            {
                ValidIssuer = settings.Issuer,
                ValidAudience = settings.Audience,
                IssuerSigningKey = settings.CreateSigningKey(),
            }
        );

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(kim.Id.ToString(), result.Claims[JwtRegisteredClaimNames.Sub]);
    }

    [Fact]
    public async Task Issue_RefusesToBeCheckedWithAnotherKey()
    {
        AccessToken token = new JwtTokenIssuer(Options.Create(settings), TimeProvider.System).Issue(
            kim
        );

        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(
            token.Value,
            new TokenValidationParameters
            {
                ValidIssuer = settings.Issuer,
                ValidAudience = settings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(new byte[AuthOptions.MinKeyBytes]),
            }
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Issue_ExpiresAfterTheLifetime_InWholeSecondsLikeTheToken()
    {
        var now = new DateTimeOffset(2026, 11, 21, 9, 30, 15, 750, TimeSpan.Zero);
        settings.TokenLifetime = TimeSpan.FromHours(8);

        AccessToken token = new JwtTokenIssuer(
            Options.Create(settings),
            new FixedTimeProvider(now)
        ).Issue(kim);

        var expected = new DateTimeOffset(2026, 11, 21, 17, 30, 15, TimeSpan.Zero);
        Assert.Equal(expected, token.ExpiresAt);
        Assert.Equal(expected.UtcDateTime, new JsonWebToken(token.Value).ValidTo);
    }
}
