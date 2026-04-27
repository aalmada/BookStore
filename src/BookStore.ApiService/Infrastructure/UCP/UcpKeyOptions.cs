namespace BookStore.ApiService.Infrastructure.UCP;

public sealed class UcpKeyOptions
{
    public const string SectionName = "Ucp";

    public string SigningKeyId { get; init; } = "bookstore-key-2026";
    public string? SigningPublicKeyBase64 { get; init; }
    public string? SigningPrivateKeyBase64 { get; init; }
    public bool RequireSignatures { get; init; }
}
