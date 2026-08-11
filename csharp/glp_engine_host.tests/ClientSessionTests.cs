// ClientSessionTests — T016 (feature 064 US2): the passive per-client session
// (data-model.md "Client session"): active|draining|closed lifecycle with loud
// illegal transitions, the per-client channel pair, and RoutedReply key
// semantics — a reply reaches exactly its issuing session, a mis-keyed reply is
// a loud routing bug, and a closed session discards (never wedges on) pending
// replies. No server wiring here — that is T017.

using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.SplitProtocol;

namespace GlpRuntime.EngineHost.Tests;

public class ClientSessionTests
{
    /// <summary>One established loopback link pair; the session wraps the server end.</summary>
    private static async Task<(ClientSession Session, ILinkEndpoint ClientEnd)> MakeSessionAsync(ulong sessionId)
    {
        var t = new LoopbackTransport();
        var addr = LinkAddress.Path($"sess{sessionId}");
        var listen = t.ListenAsync(LinkScheme.Loopback, addr, LinkOptions.Default);
        var clientEnd = await t.ConnectAsync(LinkScheme.Loopback, addr, LinkOptions.Default);
        return (new ClientSession(sessionId, await listen), clientEnd);
    }

    private static RoutedReply Reply(ulong sessionId, ulong goalId) =>
        new(sessionId, goalId, ResponseFrame.Text(goalId, ResponseKind.Result, $"goal {goalId}"));

    [Fact]
    public async Task New_session_is_active_and_carries_requests_and_replies()
    {
        var (session, clientEnd) = await MakeSessionAsync(1);

        Assert.Equal(ClientSessionState.Active, session.State);
        Assert.Equal("active", session.StateWord);
        Assert.Equal(1UL, session.SessionId);

        // In channel: a pumped request reaches the serve loop's reader.
        Assert.True(session.InboundWriter.TryWrite(RequestFrame.Text(7, RequestKind.RunGoal, "g.")));
        var request = await session.Inbound.ReadAsync();
        Assert.Equal(7UL, request.RequestId);

        // Out channel: a routed reply reaches the send pump's reader with its key intact.
        Assert.True(session.TryRouteReply(Reply(1, 42)));
        var reply = await session.Outbound.ReadAsync();
        Assert.Equal(1UL, reply.SessionId);
        Assert.Equal(42UL, reply.GoalId);
        Assert.Equal(ResponseKind.Result, reply.ResultEnvelope.Kind);

        await session.DisposeAsync();
        await clientEnd.DisposeAsync();
    }

    [Fact]
    public async Task Draining_stops_intake_but_still_delivers_replies()
    {
        var (session, clientEnd) = await MakeSessionAsync(2);

        session.BeginDraining();
        Assert.Equal(ClientSessionState.Draining, session.State);
        Assert.Equal("draining", session.StateWord);

        // No new requests…
        Assert.False(session.InboundWriter.TryWrite(RequestFrame.Empty(1, RequestKind.Ping)));
        // …but pending replies still deliver (drain = flush out, stop intake).
        Assert.True(session.TryRouteReply(Reply(2, 5)));
        Assert.Equal(5UL, (await session.Outbound.ReadAsync()).GoalId);

        await session.DisposeAsync();
        await clientEnd.DisposeAsync();
    }

    [Fact]
    public async Task Close_discards_pending_replies_and_is_terminal()
    {
        var (session, clientEnd) = await MakeSessionAsync(3);

        Assert.True(session.TryRouteReply(Reply(3, 1)));
        Assert.True(session.TryRouteReply(Reply(3, 2)));

        Assert.Equal(2, await session.CloseAsync()); // both pending replies discarded, not delivered
        Assert.Equal(ClientSessionState.Closed, session.State);
        Assert.Equal("closed", session.StateWord);
        Assert.False(session.Outbound.TryRead(out _)); // nothing delivered after close

        // A reply to a closed session is discarded — false, never a block or a throw
        // (a crashed client must not wedge the merge loop).
        Assert.False(session.TryRouteReply(Reply(3, 9)));

        // Idempotent second close; the client end sees the graceful end-of-stream.
        Assert.Equal(0, await session.CloseAsync());
        Assert.Null(await clientEnd.RecvBytesAsync());

        await session.DisposeAsync();
        await clientEnd.DisposeAsync();
    }

    [Fact]
    public async Task Draining_then_close_discards_what_was_not_flushed()
    {
        var (session, clientEnd) = await MakeSessionAsync(4);

        session.BeginDraining();
        Assert.True(session.TryRouteReply(Reply(4, 1)));
        Assert.True(session.TryRouteReply(Reply(4, 2)));
        Assert.Equal(1UL, (await session.Outbound.ReadAsync()).GoalId); // one flushed

        Assert.Equal(1, await session.CloseAsync()); // the unflushed one is discarded
        Assert.Equal(ClientSessionState.Closed, session.State);

        await session.DisposeAsync();
        await clientEnd.DisposeAsync();
    }

    [Fact]
    public async Task Illegal_transitions_throw_loudly()
    {
        var (session, clientEnd) = await MakeSessionAsync(5);

        session.BeginDraining();
        Assert.Throws<InvalidOperationException>(session.BeginDraining);   // draining → draining

        await session.CloseAsync();
        Assert.Throws<InvalidOperationException>(session.BeginDraining);   // closed → draining

        await clientEnd.DisposeAsync();
    }

    [Fact]
    public async Task Reply_keyed_to_another_session_is_a_loud_routing_bug()
    {
        var (session, clientEnd) = await MakeSessionAsync(6);

        // Key semantics: a reply reaches EXACTLY its issuing session — a mismatched
        // session_id is a server routing bug, thrown loudly, never silently dropped.
        var ex = Assert.Throws<InvalidOperationException>(() => session.TryRouteReply(Reply(99, 1)));
        Assert.Contains("routing bug", ex.Message);
        Assert.False(session.Outbound.TryRead(out _)); // nothing leaked into the wrong session

        await session.DisposeAsync();
        await clientEnd.DisposeAsync();
    }
}
