namespace GlpRuntime.Link.Seam;

/// <summary>
/// The transport scheme that selects which <see cref="ILinkTransport"/> leaf
/// serves a link (FR-013). Mirrors the GLP <c>Scheme ::= String</c> component of
/// <c>LinkId ::= link_id(Scheme, Endpoint, Nonce)</c>
/// (contracts/link-primitives.md §1, lines 53-54).
/// </summary>
/// <remarks>
/// A normalized, case-insensitive scheme token (e.g. "ws", "wss", "file",
/// "mqtt", "coap", "loopback"). Value equality so it can key the transport
/// registry. Nothing protocol-specific leaks above the seam (FR-006); the scheme
/// is the only protocol identity the language layer ever sees.
/// </remarks>
public readonly struct LinkScheme : IEquatable<LinkScheme>
{
    /// <summary>Deterministic in-memory transport for hermetic tests (T026).</summary>
    public static readonly LinkScheme Loopback = new("loopback");

    /// <summary>File endpoint (binary + text) — first headline-split transport (T041, FR-012).</summary>
    public static readonly LinkScheme File = new("file");

    /// <summary>Raw TCP over IPv4 localhost (127.0.0.1) — the FIRST real cross-process transport
    /// (feature 025 runtime-integration; ws/wss is the next iteration). One bidirectional duplex socket.</summary>
    public static readonly LinkScheme Tcp = new("tcp");

    public static readonly LinkScheme Ws = new("ws");
    public static readonly LinkScheme Wss = new("wss");
    public static readonly LinkScheme Https = new("https");
    public static readonly LinkScheme Mqtt = new("mqtt");
    public static readonly LinkScheme Coap = new("coap");
    public static readonly LinkScheme BleL2cap = new("ble-l2cap");

    /// <summary>The normalized (lower-case) scheme token. Never null/empty for a valid value.</summary>
    public string Name { get; }

    private LinkScheme(string normalized) => Name = normalized;

    /// <summary>
    /// Build a scheme from a raw GLP <c>Scheme</c> string, normalizing case so
    /// "WS" and "ws" denote the same transport. Throws on null/blank — an unbound
    /// or empty scheme is a caller bug (the GLP wrapper guards <c>ground/1</c>
    /// before the host ever sees it), not a value to tolerate.
    /// </summary>
    public static LinkScheme Of(string scheme)
    {
        if (string.IsNullOrWhiteSpace(scheme))
            throw new ArgumentException("LinkScheme requires a non-blank scheme token.", nameof(scheme));
        return new LinkScheme(scheme.Trim().ToLowerInvariant());
    }

    public bool Equals(LinkScheme other) =>
        string.Equals(Name, other.Name, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is LinkScheme s && Equals(s);

    public override int GetHashCode() => Name is null ? 0 : Name.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Name ?? "<uninitialized-scheme>";

    public static bool operator ==(LinkScheme a, LinkScheme b) => a.Equals(b);
    public static bool operator !=(LinkScheme a, LinkScheme b) => !a.Equals(b);
}
