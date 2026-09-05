using System.Net;

namespace Ynet.Transport.Listener;

/// <summary>
/// A declared bind for one named fleet service (FR-001). The name is not decoration: it is what a
/// report attributes a listener to, so an operator can tell "the guardian is deaf" from "the oracle
/// is deaf" without inspecting sockets.
/// </summary>
/// <param name="ServiceName">e.g. <c>yng-broker</c>, <c>yng-guardian</c>, <c>oracle</c>, <c>admin</c>.</param>
/// <param name="BindAddress">The address to bind. <c>0.0.0.0</c> for all interfaces.</param>
/// <param name="Port">The UDP port. 0 lets the kernel choose (used by tests).</param>
public readonly record struct ListenerConfig(string ServiceName, IPAddress BindAddress, int Port)
{
    /// <summary>The agreed fleet federation port. Unratified as of 2026-09-05 — see spec B4.</summary>
    public const int FederationPort = 47890;

    public IPEndPoint EndPoint => new(BindAddress, Port);

    /// <summary>Parse <c>name@address:port</c>, the one-line form an operator types.</summary>
    /// <exception cref="FormatException">When the form is not <c>name@address:port</c>.</exception>
    public static ListenerConfig Parse(string spec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spec);
        var at = spec.LastIndexOf('@');
        if (at <= 0 || at == spec.Length - 1)
            throw new FormatException($"listener spec must be 'name@address:port'; got '{spec}'");

        var name = spec[..at];
        var rest = spec[(at + 1)..];
        var colon = rest.LastIndexOf(':');
        if (colon <= 0 || colon == rest.Length - 1)
            throw new FormatException($"listener spec must be 'name@address:port'; got '{spec}'");

        if (!IPAddress.TryParse(rest[..colon], out var addr))
            throw new FormatException($"'{rest[..colon]}' is not an IP address (in '{spec}')");
        if (!int.TryParse(rest[(colon + 1)..], out var port) || port is < 0 or > 65535)
            throw new FormatException($"'{rest[(colon + 1)..]}' is not a port 0-65535 (in '{spec}')");

        return new ListenerConfig(name, addr, port);
    }

    public override string ToString() => $"{ServiceName}@{BindAddress}:{Port}";
}
