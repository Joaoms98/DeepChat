using System.Collections.Concurrent;
using System.Globalization;
using Galileo.Chat.Shared.Dto;

namespace Galileo.Chat.Client.App;

/// <summary>
/// Local cache of nickname → userId, fed by presence events. No server lookup,
/// which keeps the directory private to room members. Nicknames are not unique;
/// callers get a candidate list when there's ambiguity.
/// </summary>
public sealed class NicknameResolver
{
    private readonly ConcurrentDictionary<string, HashSet<Guid>> _byNick =
        new(StringComparer.OrdinalIgnoreCase);

    public void Observe(Guid userId, string nickname)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(nickname)) return;
        var bucket = _byNick.GetOrAdd(nickname, _ => new HashSet<Guid>());
        lock (bucket) bucket.Add(userId);
    }

    public void Observe(IEnumerable<UserPresenceDto> users)
    {
        foreach (var u in users) Observe(u.UserId, u.Nickname);
    }

    /// <summary>
    /// Resolves a nickname or stringified GUID to a user. Returns null when
    /// unknown or ambiguous — the <paramref name="ambiguous"/> flag disambiguates.
    /// </summary>
    public Guid? Resolve(string token, out bool ambiguous)
    {
        ambiguous = false;
        if (string.IsNullOrWhiteSpace(token)) return null;

        if (Guid.TryParse(token, CultureInfo.InvariantCulture, out var guid) && guid != Guid.Empty)
            return guid;

        if (!_byNick.TryGetValue(token, out var bucket)) return null;

        lock (bucket)
        {
            if (bucket.Count == 0) return null;
            if (bucket.Count > 1) { ambiguous = true; return null; }
            return bucket.First();
        }
    }

    public IReadOnlyCollection<Guid> Candidates(string nickname)
    {
        if (!_byNick.TryGetValue(nickname, out var bucket)) return Array.Empty<Guid>();
        lock (bucket) return bucket.ToArray();
    }
}
