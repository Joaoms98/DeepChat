using Galileo.Chat.Client.Commands;
using Galileo.Chat.Client.UI;
using Spectre.Console.Testing;

namespace Galileo.Chat.Client.Tests.Commands;

public sealed class CommandRouterTests
{
    private sealed class FakeCommand : ICommand
    {
        public string Name { get; }
        public string Description => "fake";
        public string? LastArgs { get; private set; }
        public int CallCount { get; private set; }

        public FakeCommand(string name) => Name = name;

        public Task ExecuteAsync(string arguments, CancellationToken ct)
        {
            LastArgs = arguments;
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private static (CommandRouter router, MessageRenderer renderer, TestConsole console) Build(params ICommand[] cmds)
    {
        var console = new TestConsole();
        var renderer = new MessageRenderer(console, "self");
        return (new CommandRouter(cmds, renderer), renderer, console);
    }

    [Fact]
    public void IsCommand_recognizes_slash_prefix()
    {
        var (router, _, _) = Build();
        router.IsCommand("/online").Should().BeTrue();
        router.IsCommand("hello").Should().BeFalse();
        router.IsCommand("").Should().BeFalse();
        router.IsCommand("//double").Should().BeTrue();
    }

    [Fact]
    public async Task DispatchAsync_calls_matching_command_with_args()
    {
        var fake = new FakeCommand("echo");
        var (router, _, _) = Build(fake);

        await router.DispatchAsync("/echo hello world", CancellationToken.None);

        fake.CallCount.Should().Be(1);
        fake.LastArgs.Should().Be("hello world");
    }

    [Fact]
    public async Task DispatchAsync_is_case_insensitive_on_verb()
    {
        var fake = new FakeCommand("online");
        var (router, _, _) = Build(fake);

        await router.DispatchAsync("/ONLINE", CancellationToken.None);

        fake.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task DispatchAsync_with_unknown_verb_warns_and_does_not_throw()
    {
        var fake = new FakeCommand("online");
        var (router, _, console) = Build(fake);

        await router.DispatchAsync("/nothing-here", CancellationToken.None);

        fake.CallCount.Should().Be(0);
        console.Output.Should().Contain("Comando desconhecido");
    }

    [Fact]
    public async Task DispatchAsync_help_lists_all_commands()
    {
        var (router, _, console) = Build(new FakeCommand("online"), new FakeCommand("clear"));

        await router.DispatchAsync("/help", CancellationToken.None);

        console.Output.Should().Contain("online");
        console.Output.Should().Contain("clear");
        console.Output.Should().Contain("help");
    }

    [Fact]
    public async Task DispatchAsync_swallows_command_exceptions_and_renders_error()
    {
        var failing = new FailingCommand();
        var (router, _, console) = Build(failing);

        await router.DispatchAsync("/boom", CancellationToken.None);

        console.Output.Should().Contain("falhou");
    }

    private sealed class FailingCommand : ICommand
    {
        public string Name => "boom";
        public string Description => "throws";
        public Task ExecuteAsync(string arguments, CancellationToken ct) =>
            throw new InvalidOperationException("nope");
    }
}
