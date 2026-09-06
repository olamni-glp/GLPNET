// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Net;
using System.Reflection;
using Ynet.Transport.Capability;
using Ynet.Transport.Listener;

namespace Ynet.Client;

/// <summary>
/// The one place the control surface may obtain a plane, and therefore the one place a plane can
/// be REACHED from.
///
/// <para>
/// 🔴 <b>Why this type exists at all.</b> On 2026-09-06 this repo measured its own defect:
/// <c>QuicCarrier.cs</c> was 400 lines realizing <see cref="IYnetInbound"/> and
/// <see cref="IYnetOutbound"/>, with 210 lines of passing tests, and <c>Program.cs</c> contained
/// <b>zero references to it</b>. The wire plane was reviewed, tested and merged with no consumer.
/// Nothing caught it: it compiled, and its own tests passed — because a capability's own tests
/// construct it directly, which is exactly the path a real consumer does not take.
/// </para>
///
/// <para>
/// The same class was then measured a second time the same day, independently:
/// <c>csharp/glp_supervisor</c> is a working, tested process supervisor that hosts
/// <c>glp_engine_host</c> and did <b>not</b> host this client — the one process the fleet had
/// declared MUST be kernel-managed.
/// </para>
///
/// <para>
/// 🔴 <b>A registry alone would have been a third instance of the same defect</b> — a declaration
/// with no consumer. What makes this one real is that <see cref="PlaneSelection"/> binds
/// <i>through</i> it and has no other way to construct a plane. Registration therefore
/// <b>implies</b> reachability by construction, rather than asserting it. The reachability test
/// (SC-004) checks the converse: that every realization in the assembly is registered here, or is
/// explicitly exempted <i>with a reason</i>.
/// </para>
/// </summary>
public static class PlaneCatalog
{
    /// <summary>The planes the control surface can be asked for.</summary>
    public enum Plane
    {
        /// <summary>The shared-volume file drop. The default, and the only fallback target.</summary>
        File,

        /// <summary>The authenticated QUIC session — the YNET wire.</summary>
        Wire,

        /// <summary>File and wire bound at once, de-duplicated by message id.</summary>
        Both,

        /// <summary>In-process only. Hears nothing but itself, and says so.</summary>
        Loopback,
    }

    /// <summary>
    /// Realizations that are deliberately NOT selectable, each with the reason.
    ///
    /// This is the escape hatch, and it is deliberately uncomfortable to use: the reachability test
    /// prints every exemption it honours, so an exemption is visible in test output forever rather
    /// than being a quiet way to re-open the hole this catalog closes. An exemption with an empty
    /// reason fails the test.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Exempt = new Dictionary<string, string>
    {
        // CompositeInbound is not a plane in its own right; it is how Plane.Both is realized, and
        // it is reached through that. Registering it as a selectable plane would let an operator
        // ask for "composite" with nothing inside it.
        ["CompositeInbound"] =
            "not a plane — the realization of Plane.Both, reached through that selection.",
    };

    /// <summary>Parse a plane request. Unknown text is refused rather than silently defaulted:
    /// a typo that silently selects the default is how a host ends up on a plane nobody chose.</summary>
    public static Plane Parse(string? requested) => (requested ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "" or "file" or "coop" => Plane.File,
        "wire" or "quic" or "ynet" => Plane.Wire,
        "both" or "composite" => Plane.Both,
        "loopback" or "memory" => Plane.Loopback,
        _ => throw new ArgumentException(
            $"unknown plane '{requested}'. Known: file, wire, both, loopback. Refusing to guess: " +
            "a typo that silently selects the default puts a host on a plane nobody chose.",
            nameof(requested)),
    };

    /// <summary>Everything a plane might need to be constructed. Assembled once by the control
    /// surface so that no verb can build a plane from a different set of inputs than another verb.</summary>
    public sealed record Binding
    {
        public string? CoopRoot { get; init; }
        public string? LaneDirectory { get; init; }
        public NodeIdentity? Self { get; init; }
        public ListenerConfig? Listener { get; init; }
    }

    /// <summary>
    /// Construct the inbound plane for <paramref name="plane"/>. Throws with a message naming what
    /// is missing rather than returning a plane that cannot work — an inbound that binds nothing
    /// and reports "running" is the defect this whole feature exists to close.
    /// </summary>
    public static IYnetInbound BindInbound(Plane plane, Binding b)
    {
        ArgumentNullException.ThrowIfNull(b);
        switch (plane)
        {
            case Plane.Loopback:
                return new LoopbackInbound();

            case Plane.File:
                return NewFile(b);

            case Plane.Wire:
                return NewWire(b);

            case Plane.Both:
                // Order matters only for reporting. Both are opened; neither is preferred.
                return new CompositeInbound("file+wire", NewFile(b), NewWire(b));

            default:
                throw new ArgumentOutOfRangeException(nameof(plane), plane, "unhandled plane");
        }
    }

    private static CoopFileInbound NewFile(Binding b)
    {
        if (string.IsNullOrWhiteSpace(b.CoopRoot))
            throw new InvalidOperationException(
                "the file plane needs a COOP root — pass --coop or set YNET_CLIENT_COOP. The carrier " +
                "refuses to guess a root: guessing one addresses nobody and reports success.");
        if (string.IsNullOrWhiteSpace(b.LaneDirectory))
            throw new InvalidOperationException(
                "the file plane needs this lane's mailbox — pass --self <node>/<actor>, or set " +
                "YNET_SELF, or set YNET_CLIENT_LANE. Refusing to invent one: an invented identity " +
                "binds a mailbox no peer addresses, and then reports 'running' forever.");
        return new CoopFileInbound(b.CoopRoot, b.LaneDirectory);
    }

    private static QuicInbound NewWire(Binding b)
    {
        if (b.Self is null)
            throw new InvalidOperationException(
                "the wire plane needs this node's identity. Without it there is no handshake, and " +
                "without a handshake this plane's whole advantage — a provable sender — is gone.");
        if (b.Listener is null)
            throw new InvalidOperationException(
                "the wire plane needs a listener address. Pass --listen <addr:port>.");
        return new QuicInbound(b.Self, b.Listener.Value);
    }

    /// <summary>
    /// Every concrete realization of <paramref name="contract"/> in this assembly. Used by the
    /// SC-004 reachability test.
    ///
    /// Deliberately reflected over the <b>assembly</b>, not grepped over source: a grep for
    /// <c>new QuicInbound</c> is satisfied by a dead code path, and a code path nobody reaches is
    /// precisely this defect.
    /// </summary>
    public static IEnumerable<Type> RealizationsOf(Type contract) =>
        typeof(PlaneCatalog).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsClass: true })
            .Where(contract.IsAssignableFrom)
            .OrderBy(t => t.Name, StringComparer.Ordinal);

    /// <summary>The inbound realizations this catalog can produce, by type. The reachability test
    /// compares this against <see cref="RealizationsOf"/>.</summary>
    public static IReadOnlySet<Type> SelectableInbound { get; } = new HashSet<Type>
    {
        typeof(LoopbackInbound),
        typeof(CoopFileInbound),
        typeof(QuicInbound),
    };

    /// <summary>The outbound realizations reachable from the control surface's <c>send</c> verb.</summary>
    public static IReadOnlySet<Type> SelectableOutbound { get; } = new HashSet<Type>
    {
        typeof(CoopFileOutbound),
        typeof(QuicOutbound),
    };

    /// <summary>Parse "host:port" into an endpoint, or null when nothing was supplied.</summary>
    public static ListenerConfig? ParseListen(string? serviceName, string? listen)
    {
        if (string.IsNullOrWhiteSpace(listen)) return null;
        var idx = listen.LastIndexOf(':');
        if (idx <= 0 || !int.TryParse(listen.AsSpan(idx + 1), out var port))
            throw new ArgumentException($"--listen '{listen}' is not <address>:<port>.", nameof(listen));
        var addrText = listen[..idx];
        if (!IPAddress.TryParse(addrText, out var addr))
            throw new ArgumentException($"--listen address '{addrText}' is not an IP literal.", nameof(listen));
        return new ListenerConfig(serviceName ?? "ynet-client", addr, port);
    }
}
