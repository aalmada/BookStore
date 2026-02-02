using System.Text.Json.Serialization;

namespace BookStore.Client;

public partial class UpdateBookRequest
{
    [JsonPropertyName("prices")]
    public System.Collections.Generic.IDictionary<string, decimal>? Prices { get; set; }
}
