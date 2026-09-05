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
        if (_queue.IsAddingCompleted || !_queue.TryAdd(alert))
            Interlocked.Increment(ref _dropped);
    }

    /// <summary>Block until the queue drains or the timeout expires. For tests and shutdown.</summary>
    public bool WaitForIdle(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_queue.Count == 0 && !_busy) return true;
            Thread.Sleep(5);
        }
        return _queue.Count == 0 && !_busy;
    }

    private volatile bool _busy;

    private void Pump()
    {
        try
        {
            foreach (var alert in _queue.GetConsumingEnumerable())
            {
                _busy = true;
                try
                {
                    Attempted++;
                    Last = _hook.Notify(alert);
                }
                finally
                {
                    _busy = false;
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
