using System.Text;
using Galileo.Chat.Client.App;
using Galileo.Chat.Client.Crypto;
using Galileo.Chat.Client.UI;

namespace Galileo.Chat.Client.Commands;

/// <summary>
/// /msg &lt;nickname-or-guid&gt; &lt;text&gt; — private message routed by the server
/// only to the target user's connections. Ciphertext is produced with the
/// current room key (MVP); pair-wise ECDH is on the post-MVP roadmap.
/// </summary>
public sealed class MsgCommand : ICommand
{
    public string Name => "msg";
    public string Description => "Mensagem privada: /msg <nick|guid> <texto>";

    private readonly ConnectionManager _conn;
    private readonly ClientCryptoService _crypto;
    private readonly SessionState _state;
    private readonly MessageRenderer _renderer;
    private readonly NicknameResolver _resolver;

    public MsgCommand(
        ConnectionManager conn,
        ClientCryptoService crypto,
        SessionState state,
        MessageRenderer renderer,
        NicknameResolver resolver)
    {
        _conn = conn;
        _crypto = crypto;
        _state = state;
        _renderer = renderer;
        _resolver = resolver;
    }

    public async Task ExecuteAsync(string arguments, CancellationToken ct)
    {
        var trimmed = arguments?.Trim() ?? string.Empty;
        var space = trimmed.IndexOf(' ');
        if (space < 0)
        {
            _renderer.Warning("Uso: /msg <nick|guid> <texto>");
            return;
        }

        var targetRaw = trimmed[..space];
        var text = trimmed[(space + 1)..].Trim();

        var targetId = _resolver.Resolve(targetRaw, out var ambiguous);
        if (targetId is null)
        {
            if (ambiguous)
            {
                var candidates = string.Join(", ", _resolver.Candidates(targetRaw).Select(g => g.ToString("D")));
                _renderer.Warning($"Apelido '{targetRaw}' é ambíguo. Use o GUID: {candidates}");
            }
            else
            {
                _renderer.Warning($"Destinatário '{targetRaw}' desconhecido. Rode /online para atualizar a lista.");
            }
            return;
        }

        if (targetId == _state.UserId)
        {
            _renderer.Warning("Você não pode mandar DM pra você mesmo.");
            return;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            _renderer.Warning("Texto vazio.");
            return;
        }

        var dto = _crypto.EncryptForRoom(
            _state.RoomId,
            Encoding.UTF8.GetBytes(text),
            _state.UserId,
            _state.Nickname,
            DateTime.UtcNow);

        await _conn.PostPrivateMessageAsync(targetId.Value, dto, ct);

        _renderer.System($"→ DM enviada para {targetRaw}.");
    }
}
