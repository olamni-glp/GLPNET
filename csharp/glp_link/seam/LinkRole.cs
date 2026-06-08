namespace GlpRuntime.Link.Seam;

/// <summary>
/// The transport establishment role. Mirrors the ground GLP <c>Role</c> term that
/// <c>link_setup/4</c> dispatches on — <c>listener</c> (via
/// <c>server_listener/3</c>) and <c>connector</c> (via <c>client_connector/3</c>)
/// (contracts/link-primitives.md §2.2-2.3, lines 121-145).
/// </summary>
/// <remarks>
/// Establishment role is INDEPENDENT of data direction (FR-004): the listener end
/// may be the writer end and vice versa. Which side listens is a deployment/NAT
/// concern only; both <see cref="Listener"/> and <see cref="Connector"/> paths
/// converge on the same established link (FR-002).
/// </remarks>
public enum LinkRole
{
    /// <summary>Bind this end as the transport server (<c>server_listener/3</c>).</summary>
    Listener,

    /// <summary>Initiate the transport connection (<c>client_connector/3</c>).</summary>
    Connector,
}
