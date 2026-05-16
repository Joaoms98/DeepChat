using System.Net;
using Galileo.Chat.Server.Middleware;

namespace Galileo.Chat.Server.Tests.Middleware;

public sealed class IpRuleTests
{
    [Theory]
    [InlineData("192.168.1.42", "192.168.1.42", true)]
    [InlineData("192.168.1.42", "192.168.1.43", false)]
    [InlineData("192.168.1.0/24", "192.168.1.0", true)]
    [InlineData("192.168.1.0/24", "192.168.1.255", true)]
    [InlineData("192.168.1.0/24", "192.168.2.0", false)]
    [InlineData("10.0.0.0/8", "10.255.255.255", true)]
    [InlineData("10.0.0.0/8", "11.0.0.0", false)]
    [InlineData("192.168.1.128/25", "192.168.1.128", true)]
    [InlineData("192.168.1.128/25", "192.168.1.255", true)]
    [InlineData("192.168.1.128/25", "192.168.1.127", false)]
    [InlineData("0.0.0.0/0", "8.8.8.8", true)]                  // catch-all v4
    public void Match_works_for_ipv4(string rule, string ip, bool expected)
    {
        IpRule.Parse(rule).Match(IPAddress.Parse(ip)).Should().Be(expected);
    }

    [Theory]
    [InlineData("::1", "::1", true)]
    [InlineData("::1", "::2", false)]
    [InlineData("fd00::/8", "fd12:3456::1", true)]
    [InlineData("fd00::/8", "fc00::1", false)]
    public void Match_works_for_ipv6(string rule, string ip, bool expected)
    {
        IpRule.Parse(rule).Match(IPAddress.Parse(ip)).Should().Be(expected);
    }

    [Fact]
    public void Match_does_not_cross_address_families()
    {
        var v4Rule = IpRule.Parse("0.0.0.0/0");
        v4Rule.Match(IPAddress.Parse("::1")).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-ip")]
    [InlineData("192.168.1.1/33")]   // out of range
    [InlineData("192.168.1.1/-1")]
    [InlineData("192.168.1.1/abc")]
    public void Parse_rejects_invalid_input(string input)
    {
        var act = () => IpRule.Parse(input);
        act.Should().Throw<Exception>();
    }
}
