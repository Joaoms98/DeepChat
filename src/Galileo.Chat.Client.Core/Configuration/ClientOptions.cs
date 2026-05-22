namespace Galileo.Chat.Client.Configuration;

public sealed class ClientOptions
{
    public const string SectionName = "Client";

    /// <summary>Base URL of the server (https in prod). Example: "https://192.168.1.50:7001".</summary>
    public string ServerUrl { get; set; } = "http://localhost:5000";

    /// <summary>Default room name to join after login. Empty: ask user.</summary>
    public string DefaultRoom { get; set; } = "backend";

    /// <summary>If true, the client will skip TLS certificate validation (LAN dev only).</summary>
    public bool AllowInsecureTls { get; set; } = true;
}
