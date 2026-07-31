using System.Collections.Concurrent;
using System.Text;

using GlpQuick.Host;
using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Seam;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// 063 US1 T009 — the <c>mesh_dup_id</c> regression scenario (contract C2; the defect the
/// 2026-07-02 036 fidelity audit recorded at <c>Program.cs:253</c>). Three instances A, B, C:
/// C announces the same endpoint id as a LIVE B. REQUIRED: the incumbent B's routing entry
/// survives (no eviction/hijack by the newcomer), traffic to that id keeps flowing to B, and
/// the rejection/refresh is visible in the mesh's own output. The audited symptom — the
/// newcomer's DEATH evicting the live incumbent's route (an unconditional id-keyed remove) —
/// is exactly what <see cref="Mesh.Remove"/> must not do. These tests drive the internal
/// <see cref="Mesh"/> router directly over fake endpoints (deterministic; no wire), selected
/// by <c>dotnet test --filter mesh_dup_id</c> (quickstart.md US1).
/// </summary>
public class MeshDupIdRegressionTests
{
    /// <summary>A leaf that records every frame the mesh sends it; recv is never used by Mesh.</summary>
    private sealed class FakeEndpoint : ILinkEndpoint
    {
        public LinkId Id { get; }
        public readonly ConcurrentQueue<byte[]> Sent = new();
#pragma warning disable CS0067 // OnFault is part of the seam; the mesh scenario exercises routing, not faults.
        public event Action<LinkFaultSignal>? OnFault;
#pragma warning restore CS0067
        public FakeEndpoint(string chan) =>
            Id = new LinkId(LinkScheme.Of("fake"), LinkAddress.Path(chan), LinkNonce.Int(1));

        public Task SendBytesAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            Sent.Enqueue(frame.ToArray());
            return Task.CompletedTask;
        }

        public async Task<byte[]?> RecvBytesAsync(CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            return null;
        }

        public Task CloseAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static int _msgNo;

    private static byte[] Env(string from, string to) =>
        Encoding.UTF8.GetBytes(
            $"{{\"msg_id\":\"m{Interlocked.Increment(ref _msgNo)}\",\"from\":\"{from}\",\"to\":\"{to}\",\"seq\":null,\"payload\":\"p\"}}");

    /// <summary>Route one frame from a client link (registers its announced id on first sight).</summary>
    private static Task Announce(Mesh mesh, FakeEndpoint link, string id) =>
        mesh.RouteAsync(Env(id, "server"), fromSelf: false, srcLink: link, CancellationToken.None);

    /// <summary>Route one server-originated frame to <paramref name="to"/>.</summary>
    private static Task SendTo(Mesh mesh, string to) =>
        mesh.RouteAsync(Env("server", to), fromSelf: true, srcLink: null, CancellationToken.None);

    [Fact]
    public async Task mesh_dup_id_incumbent_keeps_route_and_rejection_is_visible()
    {
        var stdout = new StringWriter();
        var mesh = new Mesh("server", stdout);
        var b = new FakeEndpoint("b");
        var c = new FakeEndpoint("c");
        await Announce(mesh, b, "bob");

        // Capture the mesh's own diagnostic output for the dup announcement.
        var prevErr = Console.Error;
        var err = new StringWriter();
        Console.SetError(err);
        try { await Announce(mesh, c, "bob"); }
        finally { Console.SetError(prevErr); }

        Assert.Contains("dup-id", err.ToString());          // the rejection is visible (C2)
        Assert.Contains("from=bob", err.ToString());

        await SendTo(mesh, "bob");
        Assert.Single(b.Sent);                              // traffic keeps flowing to the incumbent
        Assert.Empty(c.Sent);                               // the newcomer did not hijack the route
    }

    [Fact]
    public async Task mesh_dup_id_newcomer_death_never_evicts_the_live_incumbent()
    {
        // THE audited defect (Program.cs:253): after a duplicate announce, the duplicate's
        // cleanup must not tear out the live incumbent's id→link route.
        var mesh = new Mesh("server", new StringWriter());
        var b = new FakeEndpoint("b");
        var c = new FakeEndpoint("c");
        await Announce(mesh, b, "bob");

        var prevErr = Console.Error;
        Console.SetError(new StringWriter());
        try { await Announce(mesh, c, "bob"); }
        finally { Console.SetError(prevErr); }

        mesh.Remove(c);                                     // the duplicate drops (ClientPump finally)

        var prev2 = Console.Error;
        var err2 = new StringWriter();
        Console.SetError(err2);
        try { await SendTo(mesh, "bob"); }
        finally { Console.SetError(prev2); }

        Assert.Single(b.Sent);                              // the incumbent still owns the route
        Assert.DoesNotContain("no-route", err2.ToString()); // no routing loss after the dup's death
    }

    [Fact]
    public async Task mesh_dup_id_incumbent_departure_after_dup_leaves_no_stale_route()
    {
        // Clean-departure complement: when BOTH the duplicate and then the incumbent leave,
        // the id is fully released — no stale entry keeps routing to a gone link.
        var mesh = new Mesh("server", new StringWriter());
        var b = new FakeEndpoint("b");
        var c = new FakeEndpoint("c");
        await Announce(mesh, b, "bob");

        var prevErr = Console.Error;
        Console.SetError(new StringWriter());
        try { await Announce(mesh, c, "bob"); }
        finally { Console.SetError(prevErr); }

        mesh.Remove(c);
        mesh.Remove(b);

        var prev2 = Console.Error;
        var err2 = new StringWriter();
        Console.SetError(err2);
        try { await SendTo(mesh, "bob"); }
        finally { Console.SetError(prev2); }

        Assert.Empty(b.Sent);
        Assert.Empty(c.Sent);
        Assert.Contains("no-route", err2.ToString());       // honest drop, not a phantom delivery
    }

    [Fact]
    public async Task mesh_dup_id_rejected_newcomer_becomes_addressable_under_a_fresh_id()
    {
        // The refresh path: the rejected newcomer re-announces under its own (free) id and
        // becomes addressable there, while the incumbent keeps the contested id.
        var mesh = new Mesh("server", new StringWriter());
        var b = new FakeEndpoint("b");
        var c = new FakeEndpoint("c");
        await Announce(mesh, b, "bob");

        var prevErr = Console.Error;
        Console.SetError(new StringWriter());
        try { await Announce(mesh, c, "bob"); }
        finally { Console.SetError(prevErr); }

        await Announce(mesh, c, "carol");                   // id refresh from the same link

        await SendTo(mesh, "bob");
        await SendTo(mesh, "carol");
        Assert.Single(b.Sent);                              // bob still routes to the incumbent
        Assert.Single(c.Sent);                              // carol routes to the (renamed) newcomer
    }
}
