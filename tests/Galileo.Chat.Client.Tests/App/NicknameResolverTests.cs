using Galileo.Chat.Client.App;
using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Client.Tests.App;

public sealed class NicknameResolverTests
{
    [Fact]
    public void Resolve_returns_userId_when_token_is_a_known_nickname()
    {
        var resolver = new NicknameResolver();
        var id = Guid.NewGuid();
        resolver.Observe(id, "alice");

        var result = resolver.Resolve("alice", out var ambiguous);

        result.Should().Be(id);
        ambiguous.Should().BeFalse();
    }

    [Fact]
    public void Resolve_is_case_insensitive()
    {
        var resolver = new NicknameResolver();
        var id = Guid.NewGuid();
        resolver.Observe(id, "Maria");

        resolver.Resolve("MARIA", out _).Should().Be(id);
        resolver.Resolve("maria", out _).Should().Be(id);
    }

    [Fact]
    public void Resolve_parses_a_GUID_token_even_when_nickname_unknown()
    {
        var resolver = new NicknameResolver();
        var id = Guid.NewGuid();

        var result = resolver.Resolve(id.ToString("D"), out var ambiguous);

        result.Should().Be(id);
        ambiguous.Should().BeFalse();
    }

    [Fact]
    public void Resolve_returns_null_when_token_is_unknown_and_not_a_GUID()
    {
        var resolver = new NicknameResolver();

        var result = resolver.Resolve("ghost", out var ambiguous);

        result.Should().BeNull();
        ambiguous.Should().BeFalse();
    }

    [Fact]
    public void Resolve_flags_ambiguous_when_two_users_share_the_same_nickname()
    {
        var resolver = new NicknameResolver();
        resolver.Observe(Guid.NewGuid(), "alex");
        resolver.Observe(Guid.NewGuid(), "alex");

        var result = resolver.Resolve("alex", out var ambiguous);

        result.Should().BeNull();
        ambiguous.Should().BeTrue();
    }

    [Fact]
    public void Observe_ignores_empty_inputs_silently()
    {
        var resolver = new NicknameResolver();

        resolver.Observe(Guid.Empty, "x");
        resolver.Observe(Guid.NewGuid(), string.Empty);
        resolver.Observe(Guid.NewGuid(), "   ");

        resolver.Resolve("x", out _).Should().BeNull();
        resolver.Candidates("x").Should().BeEmpty();
    }

    [Fact]
    public void Observe_enumerable_populates_from_presence_list()
    {
        var resolver = new NicknameResolver();
        var a = new UserPresenceDto { UserId = Guid.NewGuid(), Nickname = "alice" };
        var b = new UserPresenceDto { UserId = Guid.NewGuid(), Nickname = "bob" };

        resolver.Observe(new[] { a, b });

        resolver.Resolve("alice", out _).Should().Be(a.UserId);
        resolver.Resolve("bob", out _).Should().Be(b.UserId);
    }

    [Fact]
    public void Candidates_returns_all_userIds_for_a_clashing_nickname()
    {
        var resolver = new NicknameResolver();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        resolver.Observe(a, "twin");
        resolver.Observe(b, "twin");

        resolver.Candidates("twin").Should().BeEquivalentTo(new[] { a, b });
    }

    [Fact]
    public void Resolve_rejects_empty_GUID()
    {
        var resolver = new NicknameResolver();

        var result = resolver.Resolve(Guid.Empty.ToString("D"), out var ambiguous);

        result.Should().BeNull();
        ambiguous.Should().BeFalse();
    }
}
