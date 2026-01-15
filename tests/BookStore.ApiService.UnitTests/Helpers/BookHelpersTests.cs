using BookStore.ApiService.Helpers;
using BookStore.Shared.Models;

namespace BookStore.ApiService.UnitTests.Helpers;

public class BookHelpersTests
{
    [Test]
    public async Task IsPreRelease_WithNullPublicationDate_ReturnsFalse()
    {
        // Arrange
        PartialDate? publicationDate = null;

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        _ = await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsPreRelease_WithPastCompleteDate_ReturnsFalse()
    {
        // Arrange
        var publicationDate = new PartialDate(2020, 1, 15);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        _ = await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsPreRelease_WithFutureCompleteDate_ReturnsTrue()
    {
        // Arrange
        var futureYear = DateTimeOffset.UtcNow.Year + 2;
        var publicationDate = new PartialDate(futureYear, 6, 15);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        _ = await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsPreRelease_WithPastYearOnly_ReturnsFalse()
    {
        // Arrange
        var publicationDate = new PartialDate(2020);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        _ = await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsPreRelease_WithFutureYearOnly_ReturnsTrue()
    {
        // Arrange
        var futureYear = DateTimeOffset.UtcNow.Year + 1;
        var publicationDate = new PartialDate(futureYear);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        _ = await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsPreRelease_WithPastYearMonth_ReturnsFalse()
    {
        // Arrange
        var publicationDate = new PartialDate(2020, 6);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        _ = await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsPreRelease_WithFutureYearMonth_ReturnsTrue()
    {
        // Arrange
        var futureYear = DateTimeOffset.UtcNow.Year + 1;
        var publicationDate = new PartialDate(futureYear, 3);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        _ = await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsPreRelease_WithCurrentYearButFutureMonth_ReturnsTrue()
    {
        // Arrange
        var currentYear = DateTimeOffset.UtcNow.Year;
        var currentMonth = DateTimeOffset.UtcNow.Month;

        // Skip test if we're in December (no future month available)
        if (currentMonth == 12)
        {
            return;
        }

        var futureMonth = currentMonth + 1;
        var publicationDate = new PartialDate(currentYear, futureMonth);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        _ = await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsPreRelease_WithCurrentYearButPastMonth_ReturnsFalse()
    {
        // Arrange
        var currentYear = DateTimeOffset.UtcNow.Year;
        var currentMonth = DateTimeOffset.UtcNow.Month;

        // Skip test if we're in January (no past month available)
        if (currentMonth == 1)
        {
            return;
        }

        var pastMonth = currentMonth - 1;
        var publicationDate = new PartialDate(currentYear, pastMonth);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        _ = await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsPreRelease_WithTodayDate_ReturnsFalse()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime);
        var publicationDate = new PartialDate(today.Year, today.Month, today.Day);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        _ = await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsPreRelease_WithTomorrowDate_ReturnsTrue()
    {
        // Arrange
        var tomorrow = DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime).AddDays(1);
        var publicationDate = new PartialDate(tomorrow.Year, tomorrow.Month, tomorrow.Day);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        _ = await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsPreRelease_WithYesterdayDate_ReturnsFalse()
    {
        // Arrange
        var yesterday = DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime).AddDays(-1);
        var publicationDate = new PartialDate(yesterday.Year, yesterday.Month, yesterday.Day);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        _ = await Assert.That(result).IsFalse();
    }
}
