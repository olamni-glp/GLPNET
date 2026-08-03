using System.Net.Sockets;

using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;

namespace GlpQuick.Host;

/// <summary>
/// 064 US1 T012 (research D3, FR-004): the Gleam-facing plain-TCP acceptor on the mesh server.
/// Gleam peers dial this port with the existing Gleam TCP transport (4-byte big-endian
/// length-prefixed frames — byte-identical to <see cref="TcpTransport"/>'s framing;
/// <c>glp_gleam/src/glp/link/transports/bridge_client.gleam</c> is the dial helper), and each
/// accepted link joins the QUIC-WS mesh EXACTLY like a QUIC client: the same
/// <see cref="Program.ClientPumpAsync"/>, the same <see cref="Mesh"/> routing, the same shared
/// capacity gate. The bridge is a RELAY, not a protocol translator — frame bytes pass through
/// the router unchanged in both directions (byte parity is proven at the codec seam).
/// </summary>
internal static class BridgeAcceptor
{
    /// <summary>
    /// Accept Gleam peers on <paramref name="local"/> continuously until <paramref name="ct"/>
    /// cancels (reusing <see cref="TcpTransport.AcceptLoopAsync"/>, 064 T015 — none dropped).
    /// <c>BRIDGE_READY</c> is advisory: the OS bind happens on the loop's first advance, and
    /// dialers follow the fleet dial-retry norm (D-9), so READY-before-bind is not a race.
    /// A bind failure is an explicit <c>ERR bridge_bind_failed</c>, never a silent absence.
    /// </summary>
    public static async Task RunAsync(Mesh mesh, LinkAddress local, ClientCapacity capacity, CancellationToken ct)
    {
        var transport = new TcpTransport();
        Console.Error.WriteLine($"BRIDGE_READY {local}");
        try
        {
            await foreach (var link in transport.AcceptLoopAsync(LinkScheme.Tcp, local, LinkOptions.Default, ct).ConfigureAwait(false))
            {
                if (!capacity.TryAdmit())
                {
                    Console.Error.WriteLine($"REJECT over_capacity {link.Id}"); // same clear reject as the QUIC path
                    _ = Program.RejectOverCapacityAsync(link);
                    continue;
                }
                Console.Error.WriteLine($"BRIDGE_UP {link.Id} ({capacity.Active}/{capacity.Max})");
                _ = Program.ClientPumpAsync(mesh, link, capacity.Release, ct);
            }
        }
        catch (SocketException ex)
        {
            Console.Error.WriteLine($"ERR bridge_bind_failed {ex.SocketErrorCode}: {ex.Message}");
        }
    }
}

/// <summary>
/// The shared admission gate (T012): QUIC clients and bridge peers count against ONE
/// <c>--max-clients</c> budget, so the mesh's capacity semantics are uniform across legs.
/// </summary>
internal sealed class ClientCapacity
{
    private int _active;

    public ClientCapacity(int max) => Max = max;

    public int Max { get; }

    public int Active => Volatile.Read(ref _active);

    /// <summary>Admit one client if the budget allows; over-capacity leaves the count untouched.</summary>
    public bool TryAdmit()
    {
        if (Interlocked.Increment(ref _active) <= Max)
            return true;
        Interlocked.Decrement(ref _active);
        return false;
    }

    public void Release() => Interlocked.Decrement(ref _active);
}
