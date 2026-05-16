namespace Galileo.Chat.Infrastructure.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "DeepChat.Server";
    public string Audience { get; set; } = "DeepChat.Client";

    /// <summary>
    /// Base64-encoded HS256 secret (>= 32 bytes after decode).
    /// Production: read from env var DEEPCHAT_JWT_SECRET via Configuration.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    public int AccessTokenLifetimeHours { get; set; } = 8;
}
