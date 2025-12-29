using BookStore.ApiService.Helpers;
using BookStore.ApiService.Models;

namespace BookStore.ApiService.Tests.Helpers;

public class BookHelpersTests
{
    [Fact]
    public void IsPreRelease_WithNullPublicationDate_ReturnsFalse()
    {
        // Arrange
        PartialDate? publicationDate = null;

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPreRelease_WithPastCompleteDate_ReturnsFalse()
    {
        // Arrange
        var publicationDate = new PartialDate(2020, 1, 15);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPreRelease_WithFutureCompleteDate_ReturnsTrue()
    {
        // Arrange
        var futureYear = DateTimeOffset.UtcNow.Year + 2;
        var publicationDate = new PartialDate(futureYear, 6, 15);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPreRelease_WithPastYearOnly_ReturnsFalse()
    {
        // Arrange
        var publicationDate = new PartialDate(2020);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPreRelease_WithFutureYearOnly_ReturnsTrue()
    {
        // Arrange
        var futureYear = DateTimeOffset.UtcNow.Year + 1;
        var publicationDate = new PartialDate(futureYear);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPreRelease_WithPastYearMonth_ReturnsFalse()
    {
        // Arrange
        var publicationDate = new PartialDate(2020, 6);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPreRelease_WithFutureYearMonth_ReturnsTrue()
    {
        // Arrange
        var futureYear = DateTimeOffset.UtcNow.Year + 1;
        var publicationDate = new PartialDate(futureYear, 3);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPreRelease_WithCurrentYearButFutureMonth_ReturnsTrue()
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
        Assert.True(result);
    }

    [Fact]
    public void IsPreRelease_WithCurrentYearButPastMonth_ReturnsFalse()
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
        Assert.False(result);
    }

    [Fact]
    public void IsPreRelease_WithTodayDate_ReturnsFalse()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime);
        var publicationDate = new PartialDate(today.Year, today.Month, today.Day);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPreRelease_WithTomorrowDate_ReturnsTrue()
    {
        // Arrange
        var tomorrow = DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime).AddDays(1);
        var publicationDate = new PartialDate(tomorrow.Year, tomorrow.Month, tomorrow.Day);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPreRelease_WithYesterdayDate_ReturnsFalse()
    {
        // Arrange
        var yesterday = DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime).AddDays(-1);
        var publicationDate = new PartialDate(yesterday.Year, yesterday.Month, yesterday.Day);

        // Act
        var result = BookHelpers.IsPreRelease(publicationDate);

        // Assert
        Assert.False(result);
    }
}
