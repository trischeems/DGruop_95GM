using System.Text.Json.Serialization;

namespace DGroup.Server.Configuration;

/// <summary>
/// Khoi "server" trong config.json. Port de kieu string trong file cau hinh.
/// </summary>
public sealed class ServerOptions
{
    [JsonPropertyName("port")]
    public string Port { get; init; } = "8765";

    [JsonPropertyName("favicon")]
    public string Favicon { get; init; } = "";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "DGroup Manager";

    /// <summary>Port dang so de bind Kestrel.</summary>
    public int PortNumber => int.TryParse(Port, out var p) ? p : 8765;
}
