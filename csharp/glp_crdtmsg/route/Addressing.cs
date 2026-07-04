// @name resolution + loud-fail addressing (feature 041-crdtmsg-mvp, T049).
//
// Contract C21 / FR-012 / SC-007: a directed @name resolves against the AUTHENTICATED peer set and
// delivers to those peers ONLY. An unknown name → a REPORTED ERROR, never a silent default-fallback.
// This is a language-level invariant for every addressing form, born from the 037 silent-fallback defect.

using GlpRuntime.CrdtMsg.Envelope;

namespace GlpRuntime.CrdtMsg.Route;

public sealed class AddressBook
{
    private readonly IReadOnlySet<string> _authenticatedPeers;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _groups;

    public AddressBook(IReadOnlySet<string> authenticatedPeers,
                       IReadOnlyDictionary<string, IReadOnlyList<string>>? groups = null)
    {
        _authenticatedPeers = authenticatedPeers;
        _groups = groups ?? new Dictionary<string, IReadOnlyList<string>>();
    }

    /// <summary>
    /// Resolve a destination to concrete authenticated peers. A '@'-prefixed group name resolves to its
    /// members; a bare name resolves to that peer. An unknown name or a group member that is not
    /// authenticated is a LOUD FAILURE — never a fallback (SC-007).
    /// </summary>
    public IReadOnlyList<string> Resolve(string to)
    {
        if (to.StartsWith('@'))
        {
            if (!_groups.TryGetValue(to, out var members))
                throw new CrdtMsgException($"unknown @name '{to}' — reported error, no fallback (SC-007)");
            foreach (var m in members)
                if (!_authenticatedPeers.Contains(m))
                    throw new CrdtMsgException($"@name '{to}' member '{m}' is not an authenticated peer");
            return members;
        }

        if (!_authenticatedPeers.Contains(to))
            throw new CrdtMsgException($"unknown peer '{to}' — reported error, no fallback (SC-007)");
        return new[] { to };
    }
}
