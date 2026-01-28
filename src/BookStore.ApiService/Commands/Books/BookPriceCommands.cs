namespace BookStore.ApiService.Commands;

/// <summary>
/// Internal command to apply a discount to a book (triggered by scheduled job)
/// </summary>
public record ApplyBookDiscount(Guid BookId, decimal Percentage, string TenantId);

/// <summary>
/// Internal command to remove a discount from a book (triggered by scheduled job)
/// </summary>
public record RemoveBookDiscount(Guid BookId, string TenantId);
