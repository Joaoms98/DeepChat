using System.Net;

namespace Galileo.Chat.Server.Tests.Integration;

public sealed class IpWhitelistIntegrationTests
{
    [Fact]
    public async Task Loopback_request_succeeds_when_AllowLoopback_is_true()
    {
        // ServerFactory configures AllowLoopback=true and an empty IP list.
        // Requests via TestServer hit the in-process pipeline as 127.0.0.1.
        await using var factory = new ServerFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
