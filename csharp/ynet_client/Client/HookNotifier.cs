// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;

namespace Ynet.Client;

/// <summary>
/// Runs agent notifications on their OWN thread (codex cycle 1, P1).
///
/// The receiver has a single dispatch thread, and the hook has a five-second bound. Calling the
/// hook inline meant a hanging agent stalled receipt for five seconds per alert, filled the bounded
/// mailbox, and lost messages — the exact failure the design claims to prevent, caused by the part
/// that was supposed to be best-effort.
///
/// Because the alert is durable BEFORE it is queued here, dropping a notification is survivable and
/// losing an alert is not: this queue is bounded and a refusal is COUNTED, and the pending alert is
/// still in the spool for the agent to find regardless.
/// </summary>
public sealed class HookNotifier : IDisposable
{
    private readonly AgentHook _hook;
    private readonly BlockingCollection<PendingAlert> _queue;
    private readonly Thread _thread;
    private long _dropped;

    public HookNotifier(AgentHook hook, int capacity = 256)
    {
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));
        _queue = new BlockingCollection<PendingAlert>(new ConcurrentQueue<PendingAlert>(), capacity);
        _thread = new Thread(Pump) { IsBackground = true, Name = "ynet-client-notifier" };
        _thread.Start();
    }

    /// <summary>The most recent attempt, whenever it completed.</summary>
    /// <remarks>
    /// Written on the pump thread and read by callers, so it is published with a release/acquire
    /// pair rather than a plain field write — an auto-property gave the reader no barrier at all.
    /// </remarks>
    public HookAttempt? Last => Volatile.Read(ref _last);

    /// <summary>Notifications dropped because the notifier was saturated. The alerts are not lost.</summary>
    public long Dropped => Interlocked.Read(ref _dropped);

    /// <summary>Notifications actually attempted.</summary>
    public long Attempted => Interlocked.Read(ref _completed);

    /// <summary>Queue one announcement. Never blocks the caller.</summary>
    public void Enqueue(PendingAlert alert)
    {
        // Counted as outstanding BEFORE the item can possibly be taken, then withdrawn if it is
        // refused.
        //
        // The obvious order - TryAdd, then increment - MOVED the false-idle window instead of
        // closing it: between a successful TryAdd and the increment, the pump can take the item,
        // run it, and increment _completed, so a caller sampling there sees admitted == completed
        // == 0 and is told the notifier is idle while the notification is still in flight. That is
        // the same defect one step earlier, and an adversarial review caught it in the fix for it.
        //
        // Over-counting for a few instructions is the SAFE direction: WaitForIdle waits slightly
        // too long. Under-counting reports work finished that has not started.
        Interlocked.Increment(ref _admitted);
        if (_queue.IsAddingCompleted || !_queue.TryAdd(alert))
        {
            Interlocked.Decrement(ref _admitted);
            Interlocked.Increment(ref _dropped);
        }
    }

    /// <summary>
    /// Block until every admitted announcement has been fully processed, or the timeout expires.
    ///
    /// THE RACE THIS EXISTS TO CLOSE (measured 2026-09-05, feature 106). The previous test was
    /// <c>_queue.Count == 0 &amp;&amp; !_busy</c>, and there is a window between the pump TAKING an
    /// item off the queue — which drops Count to 0 — and the pump setting <c>_busy</c>. A caller
    /// sampling inside that window is told the notifier is idle while the hook has not run, and
    /// then reads a null <c>Last</c>. The window is a few instructions wide, so 52 green tests
    /// never opened it; adding ten concurrently-running tests preempted the pump thread inside it
    /// on the first run.
    ///
    /// Counting admitted-vs-completed removes the window rather than narrowing it: an item is
    /// outstanding from admission until its <c>finally</c>, with no interval where it is neither
    /// queued nor in flight.
    /// </summary>
    public bool WaitForIdle(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (IsIdle) return true;
            Thread.Sleep(5);
        }
        return IsIdle;
    }

    private bool IsIdle => Interlocked.Read(ref _completed) == Interlocked.Read(ref _admitted);

    private HookAttempt? _last;
    private long _admitted;
    private long _completed;

    private void Pump()
    {
        try
        {
            foreach (var alert in _queue.GetConsumingEnumerable())
            {
                try
                {
                    Volatile.Write(ref _last, _hook.Notify(alert));
                }
                finally
                {
                    // Last is published BEFORE the item is marked complete, so a caller that
                    // observes idle can never then observe a stale or null Last.
                    Interlocked.Increment(ref _completed);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // shutdown
        }
        catch (InvalidOperationException)
        {
            // adding completed while enumerating; shutdown
        }
    }

    public void Dispose()
    {
        try { _queue.CompleteAdding(); } catch (ObjectDisposedException) { }
        _thread.Join(TimeSpan.FromSeconds(7));
        _queue.Dispose();
    }
}
