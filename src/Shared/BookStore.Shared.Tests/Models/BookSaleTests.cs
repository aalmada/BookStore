using BookStore.Shared.Models;

namespace BookStore.Shared.Tests.Models;

public class BookSaleTests
{
    [Test]
    [Category("Unit")]
    public async Task Constructor_WithValidArguments_SetsProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var end = now.AddDays(1);
        var sale = new BookSale(10m, now, end);

        using var scope = Assert.Multiple();
        _ = await Assert.That(sale.Percentage).IsEqualTo(10m);
        _ = await Assert.That(sale.Start).IsEqualTo(now);
        _ = await Assert.That(sale.End).IsEqualTo(end);
    }

    [Test]
    [Category("Unit")]
    [Arguments(0)]
    [Arguments(100)]
    [Arguments(-10)]
    [Arguments(150)]
    public async Task Constructor_WithInvalidPercentage_ThrowsArgumentException(decimal percentage) 
        => _ = await Assert.ThrowsAsync<ArgumentException>(() =>
            {
                _ = new BookSale(percentage, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
                return Task.CompletedTask;
            });

    [Test]
    [Category("Unit")]
    public async Task Constructor_WithInvalidDateRange_ThrowsArgumentException()
    {
        var now = DateTimeOffset.UtcNow;
        _ = await Assert.ThrowsAsync<ArgumentException>(() =>
        {
            _ = new BookSale(10m, now, now.AddDays(-1));
            return Task.CompletedTask;
        });

        _ = await Assert.ThrowsAsync<ArgumentException>(() =>
        {
            _ = new BookSale(10m, now, now); // Zero duration
            return Task.CompletedTask;
        });
    }

    [Test]
    [Category("Unit")]
    public async Task IsActive_ReturnsCorrectStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddHours(-1);
        var end = now.AddHours(1);
        var sale = new BookSale(10m, start, end);

        using var scope = Assert.Multiple();
        // Before sale
        _ = await Assert.That(sale.IsActive(start.AddMinutes(-1))).IsFalse();

        // At start (inclusive)
        _ = await Assert.That(sale.IsActive(start)).IsTrue();

        // During sale
        _ = await Assert.That(sale.IsActive(now)).IsTrue();

        // At end (exclusive)
        _ = await Assert.That(sale.IsActive(end)).IsFalse();

        // After sale
        _ = await Assert.That(sale.IsActive(end.AddMinutes(1))).IsFalse();
    }

    [Test]
    [Category("Unit")]
    public async Task CalculateDiscountedPrice_ReturnsCorrectValue()
    {
        var sale = new BookSale(10m, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        var discountedPrice = sale.CalculateDiscountedPrice(100m);

        _ = await Assert.That(discountedPrice).IsEqualTo(90m);
    }
}
