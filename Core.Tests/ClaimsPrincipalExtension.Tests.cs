using System.Security.Claims;

namespace Core.Tests;

public sealed class ClaimsPrincipalExtensionTests
{
    [Fact]
    public void GetUserId_ReturnsGuid_WhenNameIdentifierClaimIsPresent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        ClaimsPrincipal principal = TestHelpers.CreatePrincipal(userId);

        // Act
        Guid? result = principal.GetUserId();

        // Assert
        Assert.Equal(userId, result);
    }

    [Fact]
    public void GetUserId_ReturnsGuid_WhenSubClaimIsPresent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new List<Claim> { new("sub", userId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));

        // Act
        Guid? result = principal.GetUserId();

        // Assert
        Assert.Equal(userId, result);
    }

    [Fact]
    public void GetUserId_ReturnsGuid_WhenUserIdClaimIsPresent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new List<Claim> { new("user_id", userId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));

        // Act
        Guid? result = principal.GetUserId();

        // Assert
        Assert.Equal(userId, result);
    }

    [Fact]
    public void GetUserId_PrefersNameIdentifier_OverSubAndUserId()
    {
        // Arrange
        var nameIdentifierGuid = Guid.NewGuid();
        var subGuid = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, nameIdentifierGuid.ToString()),
            new("sub", subGuid.ToString()),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));

        // Act
        Guid? result = principal.GetUserId();

        // Assert
        Assert.Equal(nameIdentifierGuid, result);
    }

    [Fact]
    public void GetUserId_ReturnsNull_WhenNoRelevantClaimsPresent()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        Guid? result = principal.GetUserId();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetUserId_ReturnsNull_WhenClaimValueIsNotAValidGuid()
    {
        // Arrange
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "not-a-guid") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));

        // Act
        Guid? result = principal.GetUserId();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetUserId_ReturnsNull_WhenClaimValueIsEmpty()
    {
        // Arrange
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));

        // Act
        Guid? result = principal.GetUserId();

        // Assert
        Assert.Null(result);
    }
}
