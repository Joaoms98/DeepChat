using System.Net;
using System.Net.Sockets;

namespace Galileo.Chat.Server.Middleware;

/// <summary>
/// A parsed allow-rule: either a single IP or a CIDR block.
/// Match() runs in O(prefix bits) — fine for the LAN-sized rule sets we expect.
/// </summary>
internal sealed class IpRule
{
    private readonly byte[] _network;
    private readonly int _prefixLength;
    private readonly AddressFamily _family;

    private IpRule(byte[] network, int prefixLength, AddressFamily family)
    {
        _network = network;
        _prefixLength = prefixLength;
        _family = family;
    }

    public static IpRule Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("IP rule is empty.", nameof(raw));

        var slash = raw.IndexOf('/');
        var ipPart = slash >= 0 ? raw[..slash] : raw;

        if (!IPAddress.TryParse(ipPart, out var ip))
            throw new FormatException($"Invalid IP literal: '{raw}'.");

        var bytes = ip.GetAddressBytes();
        var maxBits = bytes.Length * 8;
        var prefix = maxBits;

        if (slash >= 0)
        {
            if (!int.TryParse(raw.AsSpan(slash + 1), out prefix) || prefix < 0 || prefix > maxBits)
                throw new FormatException($"Invalid CIDR prefix in '{raw}'.");
        }

        return new IpRule(bytes, prefix, ip.AddressFamily);
    }

    public bool Match(IPAddress address)
    {
        if (address.AddressFamily != _family) return false;

        var addrBytes = address.GetAddressBytes();
        var fullBytes = _prefixLength / 8;
        var remainingBits = _prefixLength % 8;

        for (var i = 0; i < fullBytes; i++)
        {
            if (addrBytes[i] != _network[i]) return false;
        }

        if (remainingBits == 0) return true;

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (addrBytes[fullBytes] & mask) == (_network[fullBytes] & mask);
    }
}
