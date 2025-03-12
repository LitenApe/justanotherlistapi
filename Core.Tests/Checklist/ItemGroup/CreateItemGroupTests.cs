using JustAnotherListApi;
using JustAnotherListApi.Checklist;
using Microsoft.EntityFrameworkCore;

namespace Core.Tests.Checklist.ItemGroup;

public class CreateItemGroupTests
{
    [Fact]
    public async Task CreateItemGroup_returnCreatedItemGroup()
    {
        var request = new CreateItemGroup.Request() { Name = "name" };
        var dbOptions = new DbContextOptionsBuilder<DatabaseContext>().UseInMemoryDatabase("JustAnotherList").Options;
        var dbContext = new DatabaseContext(dbOptions);
        var response = await CreateItemGroup.Execute(request, dbContext);

        var requestBody = response.Value;

        Assert.NotNull(requestBody);
        Assert.Multiple(
            () => Assert.Equal(request.Name, requestBody.Name),
            () => Assert.Empty(requestBody.Items),
            () => Assert.Single(requestBody.Members),
            () => Assert.NotEqual(Guid.Empty, requestBody.Id));
    }

    [Fact]
    public async Task CreateItemGroup_writesToDatabase()
    {
        var request = new CreateItemGroup.Request() { Name = "name" };
        var dbOptions = new DbContextOptionsBuilder<DatabaseContext>().UseInMemoryDatabase("JustAnotherList").Options;
        var dbContext = new DatabaseContext(dbOptions);
        var response = await CreateItemGroup.Execute(request, dbContext);

        Assert.NotNull(response.Value);
        var itemGroup = dbContext.ItemGroups.Find(response.Value.Id);

        Assert.NotNull(itemGroup);
        Assert.Equal(request.Name, itemGroup.Name);

        var members = dbContext.Members.Where(m => m.ItemGroupId == response.Value.Id);

        Assert.NotNull(members);
        Assert.Single(members);
    }
}
