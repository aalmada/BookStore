namespace BookStore.ApiService.Infrastructure.UCP;

public sealed class UcpProfileOptions
{
    public const string SectionName = "Ucp";

    public string ServiceBaseUrl { get; init; } = string.Empty;
}
