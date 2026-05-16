using Galileo.Chat.Client.App;
using Galileo.Chat.Client.UI;

namespace Galileo.Chat.Client.Commands;

public sealed class OnlineCommand : ICommand
{
    public string Name => "online";
    public string Description => "Lista quem está online na sala atual";

    private readonly ConnectionManager _conn;
    private readonly SessionState _state;
    private readonly MessageRenderer _renderer;
    private readonly NicknameResolver _resolver;

    public OnlineCommand(
        ConnectionManager conn,
        SessionState state,
        MessageRenderer renderer,
        NicknameResolver resolver)
    {
        _conn = conn;
        _state = state;
        _renderer = renderer;
        _resolver = resolver;
    }

    public async Task ExecuteAsync(string arguments, CancellationToken ct)
    {
        var users = await _conn.ListOnlineAsync(_state.RoomId, ct);
        foreach (var u in users)
            _resolver.Observe(u.UserId, u.Nickname);

        if (users.Count == 0)
        {
            _renderer.System("Ninguém mais aqui.");
            return;
        }

        _renderer.System($"Online em #{_state.RoomName} ({users.Count}):");
        foreach (var u in users)
        {
            // Show full GUID inline: /msg accepts nicknames, but on collision the
            // resolver asks for the GUID, so users need it visible.
            var self = u.UserId == _state.UserId ? "  (você)" : string.Empty;
            _renderer.System($"  • {u.Nickname,-20} {u.UserId:D}{self}");
        }
    }
}
