using Galileo.Chat.Shared.Constants;
using Galileo.Chat.Shared.Dto;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Galileo.Chat.Client.App;

/// <summary>Thin wrapper around SignalR HubConnection with automatic reconnect.</summary>
public sealed class ConnectionManager : IAsyncDisposable
{
    public HubConnection Connection { get; }

    private ConnectionManager(HubConnection conn) => Connection = conn;

    public static ConnectionManager Build(string serverUrl, Func<Task<string?>> tokenProvider, bool allowInsecureTls)
    {
        var conn = new HubConnectionBuilder()
            .WithUrl($"{serverUrl.TrimEnd('/')}{ProtocolConstants.ChatHubPath}", options =>
            {
                options.AccessTokenProvider = tokenProvider;
                if (allowInsecureTls)
                {
                    options.HttpMessageHandlerFactory = handler =>
                    {
                        if (handler is HttpClientHandler h)
                            h.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                        return handler;
                    };
                }
            })
            .AddMessagePackProtocol()
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();
        return new ConnectionManager(conn);
    }

    public Task StartAsync(CancellationToken ct = default) => Connection.StartAsync(ct);

    public Task PostMessageAsync(Guid roomId, EncryptedMessageDto envelope, CancellationToken ct = default) =>
        Connection.InvokeAsync("PostMessage", roomId.ToString("D"), envelope, ct);

    public Task PostPrivateMessageAsync(Guid targetUserId, EncryptedMessageDto envelope, CancellationToken ct = default) =>
        Connection.InvokeAsync("PostPrivateMessage", targetUserId.ToString("D"), envelope, ct);

    public Task JoinRoomAsync(Guid roomId, CancellationToken ct = default) =>
        Connection.InvokeAsync("JoinRoom", roomId.ToString("D"), ct);

    public Task LeaveRoomAsync(Guid roomId, CancellationToken ct = default) =>
        Connection.InvokeAsync("LeaveRoom", roomId.ToString("D"), ct);

    public Task<IReadOnlyList<UserPresenceDto>> ListOnlineAsync(Guid roomId, CancellationToken ct = default) =>
        Connection.InvokeAsync<IReadOnlyList<UserPresenceDto>>("ListOnline", roomId.ToString("D"), ct);

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
