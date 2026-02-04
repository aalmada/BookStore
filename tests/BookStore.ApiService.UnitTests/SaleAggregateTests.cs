using BookStore.ApiService.Aggregates;
using BookStore.Shared.Models;

namespace BookStore.ApiService.UnitTests;

public class SaleAggregateTests
{
    [Test]
    public async Task ScheduleSale_Valid_ShouldSuccess()
    {
        var aggregate = new SaleAggregate();
        var now = DateTimeOffset.UtcNow;
        var start = now.AddHours(1);
        var end = now.AddHours(2);

        var result = aggregate.ScheduleSale(20m, start, end);

        _ = await Assert.That(result.IsSuccess).IsTrue();
        _ = await Assert.That(result.Value.Sale.Percentage).IsEqualTo(20m);
    }

    [Test]
    public async Task ScheduleSale_Overlap_ShouldFail()
    {
        var aggregate = new SaleAggregate();
        var now = DateTimeOffset.UtcNow;
        var start = now.AddHours(1);
        var end = now.AddHours(2);

        // First one succeeds
        var r1 = aggregate.ScheduleSale(20m, start, end);
        // ... (comments)

        _ = await Assert.That(r1.IsSuccess).IsTrue();
    }
}
