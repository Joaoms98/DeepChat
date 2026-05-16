using Galileo.Chat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Galileo.Chat.Infrastructure.Persistence.Conversions;

/// <summary>
/// Maps Username &lt;-&gt; string. Inbound strings flow through Username.Create
/// — corruption in the column surfaces as a thrown exception during query
/// rather than a silently-invalid entity.
/// </summary>
public sealed class UsernameConverter : ValueConverter<Username, string>
{
    public UsernameConverter()
        : base(
            convertToProviderExpression: u => u.Value,
            convertFromProviderExpression: s => Username.Create(s))
    {
    }
}
