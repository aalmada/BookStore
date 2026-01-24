using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Events;
using BookStore.Shared.Models;

namespace BookStore.ApiService.UnitTests;

public class ValidationTests
{
    [Test]
    [Category("Unit")]
    public async Task BookAggregate_Validation_ReturnsResult()
    {
        // Arrange
        var id = Guid.NewGuid();
        var title = ""; // Invalid
        var isbn = "123"; // Invalid
        
        // Act
        var result = BookAggregate.CreateEvent(
            id, 
            title, 
            isbn, 
            "en", 
            [], 
            null, 
            null, 
            [], 
            [], 
            []);
        
        // Assert
        _ = await Assert.That(result.IsFailure).IsTrue();
        _ = await Assert.That(result.Error.Code).IsEqualTo(ErrorCodes.Books.TitleRequired);
    }

    [Test]
    [Category("Unit")]
    public async Task AuthorAggregate_Validation_ReturnsResult()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = ""; // Invalid
        
        // Act
        var result = AuthorAggregate.CreateEvent(id, name, []);
        
        // Assert
        _ = await Assert.That(result.IsFailure).IsTrue();
        _ = await Assert.That(result.Error.Code).IsEqualTo(ErrorCodes.Authors.NameRequired);
    }

    [Test]
    [Category("Unit")]
    public async Task CategoryAggregate_Validation_ReturnsResult()
    {
        // Arrange
        var id = Guid.NewGuid();
        
        // Act
        var result = CategoryAggregate.CreateEvent(id, []);
        
        // Assert
        _ = await Assert.That(result.IsFailure).IsTrue();
        _ = await Assert.That(result.Error.Code).IsEqualTo(ErrorCodes.Categories.TranslationsRequired);
    }
}
