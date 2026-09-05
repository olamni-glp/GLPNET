using System.Net;
using Ynet.Transport.Link;

namespace Ynet.Transport.Listener;

/// <summary>
/// Binds and reports the QUIC listener for one named fleet service — <c>yng-broker</c>,
/// <c>yng-guardian</c>, the oracle, the admin interface, or any future name (FR-001).
/// </summary>
/// <remarks>
/// <para>
/// This is the piece <c>QuicProviderSeam</c> was built for and that nothing in the repo previously
/// supplied: before this, <c>BindListenerAsync</c> had no non-test caller outside the providers
/// themselves, so no service bound a listener at all.
/// </para>
/// <para>
/// 🔴 <b>Two rules, both learned by measurement:</b>
/// </para>
/// <list type="number">
/// <item><b>A bind is not a link.</b> The socket opening proves nothing about whether a peer can
/// reach it — a Windows per-binary inbound <c>Block</c> is invisible from inside the process and
/// beats a port <c>Allow</c>. So <see cref="BindAsync"/> alone never returns
/// <see cref="ListenerOutcome.Ok"/>; only <see cref="BindAndVerifyAsync"/> can.</item>
/// <item><b>A fallback is not a silence.</b> Every tier passed over is recorded in
/// <see cref="ListenerReport.SkippedTiers"/> with its measured reason, and printed (FR-008).</item>
/// </list>
/// <para>
/// FR-012: this type contains no election, no campaign, no vote and no leader. It binds a socket and
/// says what happened.
/// </para>
/// </remarks>
public sealed class YnetListenerService
{
    private readonly QuicProviderChain _chain;

    public YnetListenerService(QuicProviderChain? chain = null)
        => _chain = chain ?? QuicProviderChain.Default;

    /// <summary>
    /// Bind the service's listener on the first provider that both probes available AND actually
    /// binds. Providers that probe unavailable, or that probe available and then fail to bind, are
    /// both recorded as skipped tiers — the second case matters because a provider that reports
    /// health and then cannot serve is the failure this whole feature exists to make visible.
    /// </summary>
    public async Task<(ListenerReport Report, IQuicListenerHandle? Handle)> BindAsync(
        ListenerConfig config, CancellationToken ct = default)
    {
        var skipped = new List<(string, QuicProviderTier, QuicAvailability)>();
        var diagnoses = new List<(string, QuicProviderTier, QuicAvailability)>();

        foreach (var provider in _chain.Providers)
        {
            var availability = provider.Probe();
            diagnoses.Add((provider.Name, provider.Tier, availability));

            if (!availability.Supported)
            {
                skipped.Add((provider.Name, provider.Tier, availability));
                continue;
            }

            try
            {
                var handle = await provider.BindListenerAsync(config.EndPoint, ct).ConfigureAwait(false);

                // FR-003: the provider is READ OFF THE HANDLE, never taken from configuration or
                // from the loop variable — configuration is precisely what lies about this.
                return (new ListenerReport(
                    config.ServiceName,
                    ListenerOutcome.BoundUnreachable,
                    handle.LocalEndPoint,
                    handle.ProviderName,
                    skipped,
                    diagnoses,
                    "socket bound; inbound reachability NOT yet measured — a bind is not a link"),
                    handle);
            }
            catch (QuicUnavailableException ex)
            {
                // codexreview F5: a provider that was ATTEMPTED and failed was not "passed over".
                // Recording it as a skipped tier made Describe() print SKIPPED and FellBack=true for
                // a tier that did run, which misreports what happened. It belongs in Diagnoses only.
                diagnoses[^1] = (provider.Name, provider.Tier, QuicAvailability.No(
                    $"probed available but bind failed: {ex.GetBaseException().Message}"));
            }
            catch (Exception ex)
            {
                var why = QuicAvailability.No($"bind failed: {ex.GetBaseException().Message}");
                diagnoses[^1] = (provider.Name, provider.Tier, why);

                // A bind failure that is not a capability gap (port in use, permission denied) is
                // reported as BindFailed rather than swallowed into Refused.
                //
                // codexreview F4: Provider is null here ON PURPOSE. FR-003 requires the provider to
                // be OBSERVED FROM THE HANDLE, and there is no handle on this path — naming the loop
                // variable would report an unobserved provider as an observed one. The name goes in
                // Detail, where it reads as diagnosis rather than as evidence.
                if (ex is System.Net.Sockets.SocketException)
                {
                    return (new ListenerReport(
                        config.ServiceName, ListenerOutcome.BindFailed, null, null,
                        skipped, diagnoses,
                        $"bind attempted on {provider.Name} (tier {(int)provider.Tier}): {why.Detail}"), null);
                }
            }
        }

        // FR-011: no provider served. The service must not start, and the refusal names every tier.
        return (new ListenerReport(
            config.ServiceName, ListenerOutcome.Refused, null, null, skipped, diagnoses,
            "no registered QUIC provider can serve this host — service must not start deaf"), null);
    }

    /// <summary>
    /// Bind, then <b>prove</b> a peer can reach it: a full handshake plus a bidirectional byte
    /// exchange (FR-009). Only this path can return <see cref="ListenerOutcome.Ok"/>.
    /// </summary>
    /// <remarks>
    /// A completed handshake alone is not accepted as proof. A handshake that completes and then
    /// carries no bytes is exactly what a half-open path looks like, and accepting it would
    /// reintroduce the defect this method exists to detect.
    /// </remarks>
    public async Task<ListenerReport> BindAndVerifyAsync(
        ListenerConfig config, TimeSpan timeout, CancellationToken ct = default)
    {
        var (report, handle) = await BindAsync(config, ct).ConfigureAwait(false);
        if (handle is null) return report;

        await using (handle)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            try
            {
                var reachable = await ProbeReachabilityAsync(handle, cts.Token).ConfigureAwait(false);
                return report with
                {
                    Outcome = reachable ? ListenerOutcome.Ok : ListenerOutcome.BoundUnreachable,
                    Detail = reachable
                        ? "bound, handshake completed and bytes echoed both ways"
                        : "socket bound but NO peer could complete a handshake and byte exchange — "
                        + "on Windows check for auto-created per-binary inbound Block rules, which "
                        + "are invisible from inside the process and beat a port Allow",
                };
            }
            catch (OperationCanceledException)
            {
                return report with
                {
                    Outcome = ListenerOutcome.BoundUnreachable,
                    Detail = $"socket bound but reachability did not complete within {timeout.TotalSeconds:F1}s — "
                           + "a timeout is NOT a pass",
                };
            }
        }
    }

    /// <summary>
    /// Dial the bound endpoint through the same chain and require a completed handshake AND a
    /// round-tripped byte. Returns false rather than throwing: an unreachable listener is a
    /// measurement outcome, not an exception.
    /// </summary>
    /// <param name="dialer">
    /// codexreview F2: the provider that OWNS <paramref name="handle"/>. Verification must dial the
    /// same stack that bound the listener. Re-selecting from the chain could pick a different tier
    /// (e.g. one that probes available but cannot connect) and report a perfectly reachable listener
    /// as BoundUnreachable — a false RED, which is as wrong as a false green.
    /// When null, the owning provider is resolved from the handle's ProviderName.
    /// </param>
    public async Task<bool> ProbeReachabilityAsync(
        IQuicListenerHandle handle, CancellationToken ct, IQuicProvider? dialer = null)
    {
        dialer ??= _chain.Providers.FirstOrDefault(
            p => string.Equals(p.Name, handle.ProviderName, StringComparison.Ordinal));

        if (dialer is null) return false;   // cannot dial the owning stack -> not proven reachable

        var target = handle.LocalEndPoint;

        // 0.0.0.0 is not dialable; dial loopback on the same port. This is deliberately weaker than
        // a cross-host check and is labelled as such: it proves the local path, not the LAN path.
        if (target.Address.Equals(IPAddress.Any)) target = new IPEndPoint(IPAddress.Loopback, target.Port);
        if (target.Address.Equals(IPAddress.IPv6Any)) target = new IPEndPoint(IPAddress.IPv6Loopback, target.Port);

        var accept = handle.AcceptAsync(ct);

        IWireChannel? client = null;
        IWireChannel? server = null;
        try
        {
            client = await dialer.ConnectAsync(target, ct).ConfigureAwait(false);
            server = await accept.ConfigureAwait(false);

            var payload = new byte[] { 0x59, 0x4E, 0x45, 0x54 }; // "YNET"

            // Frame reads block, so run them off the calling thread and honour the deadline: a
            // reachability check that hangs is a check that never reports, which is the failure
            // mode this method exists to expose.
            client.WriteFrame(payload);
            // codexreview F3: WaitAsync is what makes the deadline real. Task.Run(..., ct) only
            // declines to START once cancelled; it cannot interrupt a ReadFrame already blocked,
            // so without WaitAsync a peer that never sends hangs BindAndVerifyAsync past its
            // timeout. The worker thread is left to drain, which is why the channel is disposed
            // in the finally block below.
            var got = await Task.Run(server.ReadFrame).WaitAsync(ct).ConfigureAwait(false);
            if (got is null || !got.AsSpan().SequenceEqual(payload)) return false;

            // and back — a one-way path is not a link
            server.WriteFrame(payload);
            var back = await Task.Run(client.ReadFrame).WaitAsync(ct).ConfigureAwait(false);
            return back is not null && back.AsSpan().SequenceEqual(payload);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            client?.Dispose();
            server?.Dispose();
        }
    }
}
