namespace Galileo.Chat.Server.Configuration;

public sealed class NetworkOptions
{
    public const string SectionName = "Network";

    /// <summary>
    /// Allowed IPs and CIDR blocks (IPv4 and IPv6). Examples:
    ///   "192.168.1.42"
    ///   "192.168.1.0/24"
    ///   "::1"
    ///   "fd00::/8"
    /// Empty list means deny everyone.
    /// </summary>
    public List<string> AllowedIps { get; set; } = new();

    /// <summary>
    /// Trust X-Forwarded-For (only enable behind a reverse proxy you control).
    /// Default OFF: a malicious client could otherwise spoof their source IP.
    /// </summary>
    public bool TrustForwardedHeaders { get; set; }

    /// <summary>
    /// When true, accepts loopback (127.0.0.0/8 and ::1) regardless of AllowedIps.
    /// Useful for local dev and health checks. Set to false in production.
    /// </summary>
    public bool AllowLoopback { get; set; } = true;
}
