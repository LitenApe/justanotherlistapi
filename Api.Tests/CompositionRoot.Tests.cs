using Checklist.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// The app's own container can build every use case, so <c>AddChecklist()</c> misses none. The
/// endpoints' tests would catch most gaps, but <c>AutoDeleteItemGroups</c> only runs from the
/// cleanup job, which the tests switch off.
/// </summary>
public sealed class CompositionRootTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void EveryUseCase_ResolvesFromTheAppsContainer()
    {
        using IServiceScope scope = factory.Services.CreateScope();

        string[] missing =
        [
            .. typeof(Caller)
                .Assembly.GetTypes()
                .Where(type =>
                    string.Equals(type.Namespace, "Checklist.UseCases", StringComparison.Ordinal)
                    && !type.IsNested
                    && scope.ServiceProvider.GetService(type) is null
                )
                .Select(type => type.Name),
        ];

        Assert.Empty(missing);
    }
}
