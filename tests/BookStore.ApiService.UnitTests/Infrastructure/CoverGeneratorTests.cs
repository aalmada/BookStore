using BookStore.ApiService.Infrastructure;

namespace BookStore.ApiService.UnitTests.Infrastructure;

public class CoverGeneratorTests
{
    [Test]
    public async Task GenerateCover_ShouldReturnBytes_WhenCalled()
    {
        // Act
        var bytes = CoverGenerator.GenerateCover("Test Title", "Test Author");

        // Assert
        await Assert.That(bytes).IsNotNull();
        await Assert.That(bytes).IsNotEmpty();
    }
}
