using System.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Tests;

public sealed class ApiFactoryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void Requests_GetTheAppsOwnClosedConnection_ToTheTestDatabase()
    {
        using IServiceScope first = factory.Services.CreateScope();
        using IServiceScope second = factory.Services.CreateScope();

        IDbConnection db = first.ServiceProvider.GetRequiredService<IDbConnection>();

        Assert.Equal(ConnectionState.Closed, db.State);
        Assert.Equal(factory.Connection.Database, db.Database);
        Assert.NotSame(db, second.ServiceProvider.GetRequiredService<IDbConnection>());
    }
}
