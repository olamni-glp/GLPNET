// ClientSession — per-client session state for the multi-client serve path
// (feature 064 US2, T016; data-model.md "Client session"). A PASSIVE data +
// lifecycle class: it owns the per-client channel pair and the
// active|draining|closed state machine, and enforces the reply-routing key —
// the EngineServer wiring that accepts N clients and drives these sessions is
// T017, not here.

using System.Threading.Channels;

using GlpRuntime.Link.Seam;
using GlpRuntime.SplitProtocol;

namespace GlpRuntime.EngineHost;

/// <summary>Client-session lifecycle states (data-model.md: active|draining|closed).</summary>
public enum ClientSessionState
{
    /// <summary>Serving: requests flow in, replies flow out.</summary>
    Active,

    /// <summary>No new requests accepted; pending replies still deliver.</summary>
    Draining,

    /// <summary>Terminal: both channels completed, pending replies discarded.</summary>
    Closed,
}

/// <summary>
/// Reply routing key + payload (data-model.md "RoutedReply"): a reply reaches
/// exactly its issuing session, matched on <paramref name="SessionId"/>;
/// <paramref name="GoalId"/> identifies the issuing goal within that session.
/// </summary>
/// <param name="SessionId">The issuing session (matches <see cref="ClientSession.SessionId"/>).</param>
/// <param name="GoalId">The issuing goal within the session.</param>
/// <param name="ResultEnvelope">The terminal response frame carrying the result envelope.</param>
public sealed record RoutedReply(ulong SessionId, ulong GoalId, ResponseFrame ResultEnvelope);

/// <summary>
/// One client's session: {session_id, transport connection, in/out channels,
/// state} (data-model.md "Client session"). Lifecycle: accept → register with
/// the control program (A31 merge tree, T017) → serve → close. Closing discards
/// pending replies rather than blocking on them, so a crashed client can never
/// wedge the merge loop; state transitions are checked loudly — an illegal
/// transition is a host bug, never silently absorbed (EngineSession convention).
/// </summary>
public sealed class ClientSession : IAsyncDisposable
{
    private readonly Channel<RequestFrame> _inbound =
        Channel.CreateUnbounded<RequestFrame>(new UnboundedChannelOptions { SingleReader = true });
    // SingleReader = false: the outbound queue has TWO readers — the serve loop
    // drains it for delivery (EngineServer.DeliverRoutedRepliesAsync) while
    // CloseAsync drains it from the recv-pump thread on disconnect. The
    // lock-free single-consumer channel is unsafe under that race, so the
    // multi-consumer implementation is the correct one here.
    private readonly Channel<RoutedReply> _outbound =
        Channel.CreateUnbounded<RoutedReply>(new UnboundedChannelOptions { SingleReader = false });
    private readonly object _lock = new();

    /// <summary>The session's stable identity — the reply-routing key (data-model.md).</summary>
    public ulong SessionId { get; }

    /// <summary>The client's established transport connection (one bilateral link).</summary>
    public ILinkEndpoint Connection { get; }

    /// <summary>Current lifecycle state.</summary>
    public ClientSessionState State { get; private set; }

    public ClientSession(ulong sessionId, ILinkEndpoint connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        SessionId = sessionId;
        Connection = connection;
        State = ClientSessionState.Active;
    }

    /// <summary>Client→engine requests: the serve loop reads; the recv pump writes.</summary>
    public ChannelReader<RequestFrame> Inbound => _inbound.Reader;

    /// <summary>Ingress side of <see cref="Inbound"/> (completed once draining begins).</summary>
    public ChannelWriter<RequestFrame> InboundWriter => _inbound.Writer;

    /// <summary>Engine→client replies: the per-session send pump reads; <see cref="TryRouteReply"/> writes.</summary>
    public ChannelReader<RoutedReply> Outbound => _outbound.Reader;

    /// <summary>
    /// Route one reply to this session. The routing key is checked loudly: a
    /// reply keyed to a DIFFERENT session here is a server routing bug (a reply
    /// reaches exactly its issuing session, data-model.md), never a discard.
    /// </summary>
    /// <returns>
    /// True when the reply was enqueued for delivery (active/draining); false when
    /// the session is closed — the reply is discarded, never blocked on (a closed
    /// client must not wedge the merge loop).
    /// </returns>
    public bool TryRouteReply(RoutedReply reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        if (reply.SessionId != SessionId)
            throw new InvalidOperationException(
                $"reply for session {reply.SessionId} routed to session {SessionId} (goal {reply.GoalId}) — server routing bug");
        lock (_lock)
        {
            if (State == ClientSessionState.Closed)
                return false; // discard: pending replies of a closed session are dropped
            return _outbound.Writer.TryWrite(reply); // unbounded channel — true unless completed
        }
    }

    /// <summary>
    /// active → draining: stop accepting new requests (the inbound writer is
    /// completed) while pending replies continue to deliver. Illegal from any
    /// other state.
    /// </summary>
    public void BeginDraining()
    {
        lock (_lock)
        {
            if (State != ClientSessionState.Active)
                throw new InvalidOperationException(
                    $"illegal ClientSession transition {StateWord} → draining (session {SessionId})");
            State = ClientSessionState.Draining;
            _inbound.Writer.TryComplete();
        }
    }

    /// <summary>
    /// active|draining → closed: complete both channels, discard every pending
    /// (undelivered) reply, and gracefully close the transport connection.
    /// Idempotent — a second close is a no-op returning 0.
    /// </summary>
    /// <returns>The number of pending replies discarded.</returns>
    public async Task<int> CloseAsync()
    {
        int discarded = 0;
        lock (_lock)
        {
            if (State == ClientSessionState.Closed)
                return 0;
            State = ClientSessionState.Closed;
            _inbound.Writer.TryComplete();
            _outbound.Writer.TryComplete();
            while (_outbound.Reader.TryRead(out _))
                discarded++; // discard, never deliver-after-close and never block
        }
        await Connection.CloseAsync().ConfigureAwait(false);
        return discarded;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        await Connection.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Lower-snake state word (data-model naming, EngineSession convention).</summary>
    public string StateWord => State switch
    {
        ClientSessionState.Active => "active",
        ClientSessionState.Draining => "draining",
        ClientSessionState.Closed => "closed",
        _ => throw new ArgumentOutOfRangeException(nameof(State)),
    };
}
