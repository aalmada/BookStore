using System.Text.Json.Serialization;

namespace BookStore.ApiService.Models.Ucp;

// ------------------------------------------------------------------
// Request / Response models for the UCP Checkout REST binding
// All property names follow the UCP spec's snake_case convention.
// ------------------------------------------------------------------

public record UcpLineItemReference(string Id);

public record UcpLineItemRequest(
    UcpLineItemReference Item,
    int Quantity,
    string? Id = null);

public record UcpAddress(
    [property: JsonPropertyName("street_address")] string StreetAddress,
    [property: JsonPropertyName("address_locality")] string AddressLocality,
    [property: JsonPropertyName("address_region")] string AddressRegion,
    [property: JsonPropertyName("postal_code")] string PostalCode,
    [property: JsonPropertyName("address_country")] string AddressCountry);

public record UcpBuyer(
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    string? Email,
    [property: JsonPropertyName("phone_number")] string? PhoneNumber);

public record UcpContext(
    [property: JsonPropertyName("address_country")] string? AddressCountry,
    string? Currency,
    string? Language);

public record UcpCreateCheckoutRequest(
    [property: JsonPropertyName("line_items")] List<UcpLineItemRequest> LineItems,
    UcpContext? Context);

public record UcpUpdateCheckoutRequest(
    [property: JsonPropertyName("line_items")] List<UcpLineItemRequest> LineItems,
    UcpBuyer? Buyer);

public record UcpCompleteCheckoutRequest(
    UcpPaymentInstruments? Payment);

public record UcpPaymentInstruments(
    List<UcpPaymentInstrument> Instruments);

public record UcpPaymentInstrument(
    string Id,
    [property: JsonPropertyName("handler_id")] string HandlerId,
    string Type,
    bool Selected,
    UcpPaymentDisplay? Display,
    UcpPaymentCredential? Credential);

public record UcpPaymentDisplay(
    string? Brand,
    [property: JsonPropertyName("last_digits")] string? LastDigits,
    string? Description);

public record UcpPaymentCredential(
    string Type,
    string Token);

// ------------------------------------------------------------------
// Session read model (returned in responses)
// ------------------------------------------------------------------

public record UcpLineItem(
    string Id,
    UcpLineItemProduct Item,
    int Quantity,
    List<UcpTotal> Totals);

public record UcpLineItemProduct(
    string Id,
    string Title,
    long Price);     // in minor currency units (e.g. cents)

public record UcpTotal(
    string Type,
    long Amount,
    string? Label = null);

public record UcpMessage(
    string Type,
    string Code,
    string? Path,
    string Content,
    string Severity);

public record UcpOrder(
    string Id,
    string Label,
    [property: JsonPropertyName("permalink_url")] string PermalinkUrl);

public record UcpMeta(
    string Version,
    string Status,
    [property: JsonPropertyName("payment_handlers")] Dictionary<string, List<UcpPaymentHandlerRef>>? PaymentHandlers = null);

public record UcpPaymentHandlerRef(string Id);

public record CheckoutSessionResponse(
    UcpMeta Ucp,
    string Id,
    string Status,
    string Currency,
    [property: JsonPropertyName("line_items")] List<UcpLineItem> LineItems,
    List<UcpTotal> Totals,
    List<UcpMessage> Messages,
    [property: JsonPropertyName("continue_url")] string? ContinueUrl,
    [property: JsonPropertyName("expires_at")] string ExpiresAt,
    UcpBuyer? Buyer,
    UcpOrder? Order);

