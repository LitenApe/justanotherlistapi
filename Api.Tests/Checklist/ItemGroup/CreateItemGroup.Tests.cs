using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.ItemGroupTests;

public sealed class CreateItemGroupTests
{
    [Fact]
    public async Task Execute_CreatesTheList_WithTheCallerAsItsOnlyMember()
    {
        var userId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();

        ItemGroup itemGroup = await new CreateItemGroup(new SqlItemGroupStore(db)).Execute(
            new Caller(userId),
            TestHelpers.ValidName("Groceries"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("Groceries", itemGroup.Name);
        Assert.Equal(userId, Assert.Single(itemGroup.Members));
        Assert.Empty(itemGroup.Items);
        Assert.Equal(new ItemGroupSettings(), itemGroup.Settings);
        Assert.Equal("Groceries", await db.LoadItemGroupName(itemGroup.Id));
        Assert.Equal(userId, Assert.Single(await db.LoadMembers(itemGroup.Id)));
    }
}
