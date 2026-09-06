// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

namespace Ynet.Client;

/// <summary>
/// Several planes bound at once, presenting as one plane, de-duplicated by message id.
///
/// <para>
/// <b>Why this exists</b> (engineer ruling Q-G34-03, 2026-09-06). The fleet is mid-migration: some
/// peers deliver on the shared volume today and will deliver on the wire later. An exclusive plane
/// choice makes that a flag-day. The alternative considered and rejected was to let a lane run two
/// client processes — which would give one lane two mailboxes, a worse defect than the one being
/// fixed.
/// </para>
///
/// <para>
/// 🔴 <b>De-duplication is the dangerous part, and it fails in two opposite directions.</b> Too
/// little and the same message alerts twice. Too much and a message is silently lost — and every
/// "exactly one alert" test still passes, because one is what an over-eager de-duplicator produces
/// for the *first* message and zero is what it produces for every one after. That is why
/// <c>CompositeInboundTests</c> carries a negative control (one plane, one delivery, exactly one
/// alert) as well as the duplicate case: the positive test alone cannot tell a working
/// de-duplicator from a suppressor.
/// </para>
///
/// <para>
/// 🔴 <b>The seen-set is BOUNDED.</b> An unbounded one is a memory-exhaustion primitive available
/// to anyone who can complete a handshake and send distinct ids — the same reasoning that put a
/// ceiling on frame size in <see cref="QuicInbound.MaxFrameBytes"/>. Eviction is by insertion
/// order, so the ids most likely to still be in flight are the ones retained.
/// </para>
/// </summary>
public sealed class CompositeInbound : IYnetInbound, IDisposable
{
    /// <summary>How many recently-seen message ids are remembered.
    ///
    /// The consequence of eviction is honest and worth stating: if the same id arrives on the two
    /// planes further apart than this many distinct messages, it will alert twice. That is the
    /// correct trade — a duplicate alert is visible and recoverable, whereas an unbounded set is a
    /// remote memory-exhaustion primitive, and a lost message is invisible.</summary>
    public const int SeenCapacity = 8192;

    private readonly IYnetInbound[] _planes;
    private readonly LinkedList<string> _order = new();
    private readonly Dictionary<string, LinkedListNode<string>> _seen = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private bool _open;
    private long _suppressed;

    public CompositeInbound(string planeName, params IYnetInbound[] planes)
    {
        ArgumentNullException.ThrowIfNull(planes);
        if (planes.Length == 0)
            throw new ArgumentException(
                "a composite with no inner planes receives nothing while reporting that it is a " +
                "plane. Refusing to build one.", nameof(planes));
        if (Array.IndexOf(planes, null) >= 0)
            throw new ArgumentException("a null inner plane cannot be opened.", nameof(planes));

        PlaneName = planeName;
        _planes = planes;
        foreach (var p in _planes) p.Received += OnInner;
    }

    /// <inheritdoc/>
    public string PlaneName { get; }

    /// <summary>The inner planes, in the order they were given. Exposed so status output can name
    /// what is actually live rather than printing the composite's own label and stopping there.</summary>
    public IReadOnlyList<IYnetInbound> Planes => _planes;

    /// <summary>How many arrivals were suppressed as duplicates. Exposed because a de-duplicator
    /// that silently suppresses is indistinguishable from one that is doing nothing — and, worse,
    /// from one that is suppressing everything.</summary>
    public long Suppressed => Interlocked.Read(ref _suppressed);

    /// <inheritdoc/>
    public event Action<YnetMessage>? Received;

    /// <inheritdoc/>
    public void Open()
    {
        lock (_gate)
        {
            if (_open) return;
            _open = true;
        }
        // Opened OUTSIDE the lock: an inner Open may block (a listener binds, a poller starts), and
        // holding the gate across that would serialize every arrival behind a bind.
        foreach (var p in _planes) p.Open();
    }

    /// <inheritdoc/>
    public void Close()
    {
        lock (_gate)
        {
            if (!_open) return;
            _open = false;
        }
        foreach (var p in _planes) p.Close();
    }

    private void OnInner(YnetMessage message)
    {
        if (message is null) return;
        if (!IsFirstSighting(message.MessageId)) return;
        Received?.Invoke(message);
    }

    /// <summary>
    /// True the first time an id is seen, false afterwards.
    ///
    /// Kept as a named, internal, individually-testable method rather than being inlined into
    /// <see cref="OnInner"/> so that FR-023a's mutation proof has something to neuter: the test
    /// that proves de-duplication works is the test that fails when this returns <c>true</c>
    /// unconditionally.
    /// </summary>
    internal bool IsFirstSighting(string messageId)
    {
        if (string.IsNullOrEmpty(messageId))
            // An id-less message cannot be de-duplicated. Delivering it is the safe direction:
            // a duplicate alert is visible, a dropped message is not.
            return true;

        lock (_gate)
        {
            if (_seen.ContainsKey(messageId))
            {
                Interlocked.Increment(ref _suppressed);
                return false;
            }

            var node = _order.AddLast(messageId);
            _seen[messageId] = node;

            while (_seen.Count > SeenCapacity)
            {
                var oldest = _order.First;
                if (oldest is null) break;
                _order.RemoveFirst();
                _seen.Remove(oldest.Value);
            }
            return true;
        }
    }

    public void Dispose()
    {
        Close();
        foreach (var p in _planes)
        {
            p.Received -= OnInner;
            if (p is IDisposable d) d.Dispose();
        }
    }
}
