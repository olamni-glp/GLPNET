namespace GlpRuntime.Link.Seam;

/// <summary>
/// The host/path (and optional port) a transport leaf opens against. Mirrors the
/// GLP <c>Endpoint ::= String ; ep(String, Integer)</c> component of
/// <c>link_id(Scheme, Endpoint, Nonce)</c>
/// (contracts/link-primitives.md §1, line 55).
/// </summary>
/// <param name="Host">
/// The host or path. For <c>file</c> a filesystem path; for <c>ws</c>/<c>mqtt</c>
/// a host (or full URL path); for <c>loopback</c> a logical channel name.
/// </param>
/// <param name="Port">
/// The port for the <c>ep(Host, Port)</c> form, or <c>null</c> for the bare-string
/// <c>Endpoint ::= String</c> form (path/host with no port).
/// </param>
public sealed record LinkAddress(string Host, int? Port = null)
{
    /// <summary>The bare-string <c>Endpoint ::= String</c> form (path or host, no port).</summary>
    public static LinkAddress Path(string hostOrPath)
    {
        if (string.IsNullOrWhiteSpace(hostOrPath))
            throw new ArgumentException("LinkAddress requires a non-blank host/path.", nameof(hostOrPath));
        return new LinkAddress(hostOrPath, null);
    }

    /// <summary>The <c>ep(Host, Port)</c> form (host + port).</summary>
    public static LinkAddress Endpoint(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("LinkAddress requires a non-blank host.", nameof(host));
        if (port is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be in [0, 65535].");
        return new LinkAddress(host, port);
    }

    public override string ToString() => Port is { } p ? $"{Host}:{p}" : Host;
}
