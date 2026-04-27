using BookStore.ApiService.Events;
using BookStore.ApiService.Models.Ucp;

namespace BookStore.ApiService.Commands;

public record CreateCheckoutSession(
    Guid SessionId,
    string TenantId,
    string Currency,
    List<CheckoutLineItemData> LineItems);

public record UpdateCheckoutSession(
    Guid SessionId,
    string TenantId,
    List<CheckoutLineItemData> LineItems,
    UcpBuyer? Buyer);

public record CompleteCheckoutSession(
    Guid SessionId,
    string TenantId,
    UcpPaymentInstruments? Payment);

public record CancelCheckoutSession(
    Guid SessionId,
    string TenantId);
