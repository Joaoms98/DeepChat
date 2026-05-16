using System.Net;
using System.Net.Http.Json;
using Galileo.Chat.Domain.Abstractions;
using Galileo.Chat.Domain.UseCases.Auth;
using Galileo.Chat.Shared.Dto;
using Microsoft.Extensions.DependencyInjection;

namespace Galileo.Chat.Server.Tests.Integration;

public sealed class AuthEndpointsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory _factory;
    public AuthEndpointsTests(ServerFactory factory) => _factory = factory;

    private async Task SeedUserAsync(string username, string nickname, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var register = scope.ServiceProvider.GetRequiredService<RegisterUserHandler>();
        await register.HandleAsync(new RegisterUserCommand(username, nickname, password));
    }

    [Fact]
    public async Task POST_login_with_valid_credentials_returns_200_and_token()
    {
        await SeedUserAsync("alice", "Alice", "secret123");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "alice", Password = "secret123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
        body.Nickname.Should().Be("Alice");
        body.UserId.Should().NotBe(Guid.Empty);
        body.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task POST_login_with_wrong_password_returns_401()
    {
        await SeedUserAsync("bob", "Bob", "secret123");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "bob", Password = "WRONG" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_login_with_unknown_user_returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "ghost", Password = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_health_returns_200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
