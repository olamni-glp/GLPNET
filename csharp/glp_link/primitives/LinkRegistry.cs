using GlpRuntime.Link.Seam;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The per-instance LinkId→<see cref="LinkHandle"/> registry that makes
/// <c>'_link_setup'</c> idempotent at link-identity (FR-007): re-invoking setup
/// with the same ground <see cref="LinkId"/> returns the already-established handle
/// rather than opening a conflicting duplicate. Keyed by the never-reused ground
/// <see cref="LinkId"/> (value equality). One registry per instance; not
/// thread-safe (the runner is single-threaded).
/// </summary>
public sealed class LinkRegistry
{
    private readonly Dictionary<LinkId, LinkHandle> _handles = new();

    /// <summary>Look up an established link by identity.</summary>
    public bool TryGet(LinkId id, out LinkHandle handle) => _handles.TryGetValue(id, out handle!);

    /// <summary>
    /// Return the existing handle for <paramref name="id"/> (idempotent reuse,
    /// FR-007), or create + store one via <paramref name="establish"/> on first
    /// setup. <paramref name="establish"/> runs only when no handle exists yet.
    /// </summary>
    public LinkHandle GetOrEstablish(LinkId id, Func<LinkHandle> establish)
    {
        if (_handles.TryGetValue(id, out var existing))
            return existing;
        var handle = establish();
        if (!handle.Id.Equals(id))
            throw new InvalidOperationException($"established handle id {handle.Id} != requested {id}");
        _handles[id] = handle;
        return handle;
    }

    /// <summary>True if this exact handle was newly stored by the last <see cref="GetOrEstablish"/> call for an id.</summary>
    public bool Contains(LinkId id) => _handles.ContainsKey(id);

    /// <summary>Remove a link's handle (distributed GC on permFail/close, T024).</summary>
    public bool Remove(LinkId id) => _handles.Remove(id);

    /// <summary>Number of established links.</summary>
    public int Count => _handles.Count;
}
