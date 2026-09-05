// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;

namespace Ynet.Client.Machine;

/// <summary>Outcome of appending to a bounded mailbox. Capacity is SIGNALLED, never a silent drop.</summary>
public enum AppendOutcome
{
    /// <summary>Queued for dispatch.</summary>
    Accepted,

    /// <summary>Refused because the mailbox is at capacity. The caller is told; nothing is lost silently.</summary>
    Closed,
}

/// <summary>
/// An active object: a <see cref="Qhsm"/> with its own bounded mailbox and its own thread, so the
/// machine runs to completion on one event at a time (M6-b: it keeps running whether or not any
/// agent is attached).
///
/// The full-capacity contract here is the SIGNALLED one. The two in-memory paths in the estate
/// disagree on this and the disagreement was published rather than assumed (broadcast
/// 2026-09-05T10:50Z, section 3.4a): the four measured QActive copies return a bare bool from
/// Post, while YngeniOS.Mailbox.Unified's IUnifiedMailbox.Append returns AppendOutcome.Closed and
/// documents capacity as "signalled, never silently dropped". This class follows the unified
/// contract because it is the one that is written down as a contract, and the question of which
/// governs is with @qhstate / @yngcor.
/// </summary>
public abstract class QActiveLite : Qhsm, IDisposable
{
    private readonly BlockingCollection<QEvt> _mailbox;

    /// <summary>
    /// Internal completion events (codex cycle 1, P1). A machine's own transition events —
    /// "the plane opened", "the alert is recorded", "the agent was told" — must NEVER compete for
    /// the bounded PUBLIC mailbox: if a carrier burst fills it during an entry action, the machine
    /// wedges in a transient state and every later message goes unhandled. This queue is internal,
    /// drained with priority, and bounded in practice by run-to-completion (a dispatch posts at
    /// most a small constant number of internal events before it returns).
    /// </summary>
    private readonly ConcurrentQueue<QEvt> _internal = new();

    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _stopping = new();
    private Thread? _thread;
    private long _accepted;
    private long _refused;
    private long _dispatched;

    protected QActiveLite(int capacity = 1024)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _mailbox = new BlockingCollection<QEvt>(new ConcurrentQueue<QEvt>(), capacity);
    }

    /// <summary>The enforced bound.</summary>
    public int Capacity { get; }

    /// <summary>Events currently queued.</summary>
    public int Depth => _mailbox.Count;

    /// <summary>Events accepted since construction.</summary>
    public long Accepted => Interlocked.Read(ref _accepted);

    /// <summary>Events refused at capacity since construction. A non-zero value is a reportable fact.</summary>
    public long Refused => Interlocked.Read(ref _refused);

    /// <summary>Events dispatched to the machine since construction.</summary>
    public long Dispatched => Interlocked.Read(ref _dispatched);

    /// <summary>Raised when a dispatch throws, so a machine fault is never swallowed.</summary>
    public event Action<Exception>? Faulted;

    /// <summary>Post an event. Never blocks the caller; refusal at capacity is returned, not thrown.</summary>
    public AppendOutcome Post(QEvt e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (_stopping.IsCancellationRequested) return AppendOutcome.Closed;

        if (_mailbox.TryAdd(e))
        {
            Interlocked.Increment(ref _accepted);
            _signal.Release();
            return AppendOutcome.Accepted;
        }

        Interlocked.Increment(ref _refused);
        return AppendOutcome.Closed;
    }

    /// <summary>
    /// Post one of the machine's OWN completion events. Cannot be refused, and is dispatched ahead
    /// of anything in the public mailbox, so a machine can always finish the transition it started.
    /// Only a state handler may call this — it is not a back door around the public bound.
    /// </summary>
    protected void PostInternal(QEvt e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _internal.Enqueue(e);
        _signal.Release();
    }

    /// <summary>Start the machine and its dispatch thread.</summary>
    public void Launch(string threadName)
    {
        if (_thread is not null) throw new InvalidOperationException("already launched");
        Start();
        _thread = new Thread(Pump) { IsBackground = true, Name = threadName };
        _thread.Start();
    }

    /// <summary>Dispatch queued events on the calling thread until both queues are empty (tests).</summary>
    public int PumpOnce()
    {
        var n = 0;
        while (TryNext(out var e))
        {
            DispatchGuarded(e!);
            n++;
        }
        return n;
    }

    /// <summary>Internal completion events first, then the public mailbox.</summary>
    private bool TryNext(out QEvt? e)
    {
        if (_internal.TryDequeue(out var i)) { e = i; return true; }
        if (_mailbox.TryTake(out var m)) { e = m; return true; }
        e = null;
        return false;
    }

    private void Pump()
    {
        try
        {
            while (!_stopping.IsCancellationRequested)
            {
                _signal.Wait(_stopping.Token);
                while (TryNext(out var e)) DispatchGuarded(e!);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (ObjectDisposedException)
        {
            // shutdown raced the dispose; nothing is in flight by then
        }
    }

    private void DispatchGuarded(QEvt e)
    {
        try
        {
            Dispatch(e);
            Interlocked.Increment(ref _dispatched);
        }
        catch (Exception ex)
        {
            Faulted?.Invoke(ex);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        if (!_stopping.IsCancellationRequested) _stopping.Cancel();
        _mailbox.CompleteAdding();
        _thread?.Join(TimeSpan.FromSeconds(5));
        _stopping.Dispose();
        _signal.Dispose();
        _mailbox.Dispose();
    }
}
