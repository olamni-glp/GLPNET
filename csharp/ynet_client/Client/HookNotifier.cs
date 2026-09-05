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
    public HookAttempt? Last { get; private set; }

    /// <summary>Notifications dropped because the notifier was saturated. The alerts are not lost.</summary>
    public long Dropped => Interlocked.Read(ref _dropped);

    /// <summary>Notifications actually attempted.</summary>
    public long Attempted { get; private set; }

    /// <summary>Queue one announcement. Never blocks the caller.</summary>
    public void Enqueue(PendingAlert alert)
    {
        // Count the item OUTSTANDING before it is queued. Counting after would reopen the very gap
        // this counter exists to close (see WaitForIdle).
        Interlocked.Increment(ref _outstanding);

        // 🔴 EVERY failure path after that increment must release it, INCLUDING a throw.
        // IsAddingCompleted is a check, not a lock: Dispose() can complete the queue between that
        // check and TryAdd, and TryAdd then THROWS InvalidOperationException rather than returning
        // false. The first version only handled the `false` case, so a shutdown racing an enqueue
        // left _outstanding positive forever and WaitForIdle blocked to its timeout on work that was
        // never queued (codexreview 2026-09-05, P2).
        var queued = false;
        try
        {
            queued = !_queue.IsAddingCompleted && _queue.TryAdd(alert);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            queued = false; // the queue closed underneath us; treated as a drop, same as a refusal
        }
        finally
        {
            if (!queued)
            {
                Interlocked.Decrement(ref _outstanding);
                Interlocked.Increment(ref _dropped);
            }
        }
    }

    /// <summary>
    /// Block until every queued announcement has been ATTEMPTED, or the timeout expires.
    ///
    /// 🔴 This used to test <c>_queue.Count == 0 &amp;&amp; !_busy</c>, and that has a window in which it
    /// returns true having waited for nothing: the pump takes an item (Count drops to 0) and has not
    /// yet set <c>_busy</c>. An observer sampling between those two statements sees an idle notifier
    /// while the notification has not been attempted at all, so <see cref="Last"/> is still null or
    /// stale when the caller reads it. Measured as an intermittent failure of
    /// <c>A_FAILING_hook_does_not_lose_the_message</c> — roughly one run in three, 2026-09-05.
    ///
    /// A single outstanding counter, incremented BEFORE the enqueue and decremented only AFTER the
    /// handler returns, has no such window: the item is counted continuously from before it is
    /// visible to the pump until after its effect is published. A "done" from this method is now
    /// evidence that the work happened.
    /// </summary>
    public bool WaitForIdle(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Interlocked.Read(ref _outstanding) == 0) return true;
            Thread.Sleep(5);
        }
        return Interlocked.Read(ref _outstanding) == 0;
    }

    /// <summary>Announcements queued but not yet attempted. Zero is the only idle state.</summary>
    public long Outstanding => Interlocked.Read(ref _outstanding);

    private long _outstanding;

    private void Pump()
    {
        try
        {
            foreach (var alert in _queue.GetConsumingEnumerable())
            {
                try
                {
                    Attempted++;
                    Last = _hook.Notify(alert);
                }
                finally
                {
                    // AFTER Last is published, so an observer that sees zero outstanding can read it.
                    Interlocked.Decrement(ref _outstanding);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            AbandonOutstanding(); // shutdown
        }
        catch (InvalidOperationException)
        {
            AbandonOutstanding(); // adding completed while enumerating; shutdown
        }
    }

    /// <summary>
    /// The pump is gone, so anything still counted will never be attempted. Release the counter and
    /// count those announcements as dropped — a waiter must not block on work that cannot happen,
    /// and the drop must appear in the number that reports drops. The ALERTS remain in the spool.
    /// </summary>
    private void AbandonOutstanding()
    {
        var abandoned = Interlocked.Exchange(ref _outstanding, 0);
        if (abandoned > 0) Interlocked.Add(ref _dropped, abandoned);
    }

    public void Dispose()
    {
        try { _queue.CompleteAdding(); } catch (ObjectDisposedException) { }
        _thread.Join(TimeSpan.FromSeconds(7));
        _queue.Dispose();
    }
}
