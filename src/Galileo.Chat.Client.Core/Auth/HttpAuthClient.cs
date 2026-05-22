using System.Net.Http.Json;
using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Client.Auth;

public sealed class HttpAuthClient
{
    private readonly HttpClient _http;

    public HttpAuthClient(HttpClient http) => _http = http;

    /// <summary>Returns the LoginResponse on success, null on 401, throws on transport errors.</summary>
    public async Task<LoginResponse?> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = username, Password = password }, ct);

        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return null;

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct);
    }
}
