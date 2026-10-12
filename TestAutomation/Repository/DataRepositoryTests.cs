using Automation.Repository;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace TestAutomation.Repository;

public class DataRepositoryTests
{
    private static DataRepository CreateRepository() =>
        new(Path.Combine(Path.GetTempPath(), $"repository-test-{Guid.NewGuid()}"), Substitute.For<ILogger>());

    [Fact]
    public void Get_ReturnsACopy_SoChangingItDoesNotChangeTheStoredData()
    {
        // Arrange - the repository is shared by all apps
        var repository = CreateRepository();
        repository.Save("list", new List<string> { "a" });

        // Act - one app changes the list it got, without saving it
        repository.Get<List<string>>("list")!.Add("b");

        // Assert - another app still gets the stored data
        Assert.Equal(["a"], repository.Get<List<string>>("list")!);
    }

    [Fact]
    public void Save_FromManyThreadsAtOnce_DoesNotFail()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"repository-test-{Guid.NewGuid()}");
        var repository = new DataRepository(path, Substitute.For<ILogger>());

        // Act
        Parallel.For(0, 200, i => repository.Save("value", new List<int> { i }));

        // Assert - the stored data is one complete value, readable from disk by a fresh repository (no cache)
        var fromDisk = new DataRepository(path, Substitute.For<ILogger>()).Get<List<int>>("value");
        Assert.Single(fromDisk!);
    }
}
