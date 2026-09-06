// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The WaitForIdle contract (feature 106, 2026-09-05).
//
// THE DEFECT THESE TESTS FOLLOW FROM
//     WaitForIdle tested `_queue.Count == 0 && !_busy`. The pump sets _busy AFTER taking the item
//     off the queue, so between the take (Count -> 0) and the assignment there is a window in which
//     the notifier reports IDLE while the hook has not run. A caller sampling there is told the work
//     is done and then reads a null Last, which is where ReceiverAndSpoolTests threw a
//     NullReferenceException. The fix counts admitted-vs-completed, so an item is outstanding from
//     admission to completion with no interval where it is neither queued nor in flight.
//
// 🔴 WHAT THESE TESTS ARE, AND WHAT THEY ARE NOT — STATED PLAINLY
//     They pin the CONTRACT the fix establishes. They are NOT a reproduction of the race, and they
//     must not be cited as one. An earlier version of this file was a 400-iteration stress probe
//     written as a regression control; run against the PRE-FIX implementation it PASSED, so it
//     discriminated nothing and was removed rather than kept as a green decoration.
//
//     THE INSTRUMENT THAT ACTUALLY FINDS THIS DEFECT IS THE FULL SUITE RUN IN PARALLEL.
//     The window is a few instructions wide and only opens when the pump thread is preempted inside
//     it. Nothing in the suite ran concurrently with anything else, so 52 green tests never opened
//     it; adding ten new concurrently-running test methods opened it on the very first run. The
//     regression control for the race is therefore "the whole suite, run together" — which is how
//     it is run — not any single test in this file.

namespace Ynet.Client.Tests;

public sealed class HookNotifierIdleRaceTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "ynet_notifier_idle", Guid.NewGuid().ToString("N"));

    public HookNotifierIdleRaceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* a leftover temp dir is not a test failure */ }
    }

    [Fact]
    public void An_admitted_announcement_makes_the_notifier_non_idle_SYNCHRONOUSLY()
    {
        // This is the property the fix introduces and the old implementation could not offer:
        // Enqueue increments the outstanding count before it returns, so there is no instant at
        // which an admitted item is invisible to the idle test. Deterministic — no timing.
        using var notifier = new HookNotifier(new AgentHook(SpawningCommand));
        var spool = new PendingAlertSpool(_dir);

        notifier.Enqueue(spool.Raise("m-1", "gavriella.glpnet", "s"));

        Assert.False(notifier.WaitForIdle(TimeSpan.Zero));
        Assert.Equal(0, notifier.Attempted);
    }

    [Fact]
    public void When_it_reports_idle_every_admitted_announcement_has_completed()
    {
        using var notifier = new HookNotifier(new AgentHook(null));
        var spool = new PendingAlertSpool(_dir);

        const int count = 50;
        for (var i = 0; i < count; i++)
            notifier.Enqueue(spool.Raise($"m-{i}", "gavriella.glpnet", "s"));

        Assert.True(notifier.WaitForIdle(TimeSpan.FromSeconds(30)));

        // Idle is a statement about ALL admitted work, not about the queue being momentarily empty.
        Assert.Equal(count, notifier.Attempted);
        Assert.NotNull(notifier.Last);
        Assert.Equal(0, notifier.Dropped);
    }

    [Fact]
    public void A_dropped_announcement_is_not_counted_as_outstanding_and_cannot_hang_idle()
    {
        // Saturate a capacity-1 notifier whose hook blocks, so admissions are refused. A dropped
        // item was never admitted, so it must not keep the notifier permanently non-idle — that
        // would turn a survivable drop into a hang.
        using var notifier = new HookNotifier(new AgentHook(SpawningCommand), capacity: 1);
        var spool = new PendingAlertSpool(_dir);

        for (var i = 0; i < 40; i++)
            notifier.Enqueue(spool.Raise($"m-{i}", "gavriella.glpnet", "s"));

        Assert.True(notifier.Dropped > 0, "a capacity-1 notifier fed 40 items faster than a process spawn must refuse some");
        Assert.True(notifier.WaitForIdle(TimeSpan.FromSeconds(60)), "drops must not prevent reaching idle");
        Assert.Equal(40 - notifier.Dropped, notifier.Attempted);
    }

    /// <summary>
    /// A hook that really SPAWNS A PROCESS, so one notification costs tens of milliseconds rather
    /// than microseconds. AgentHook passes the whole string as ProcessStartInfo.FileName, so this
    /// must be a bare executable name: a command line with arguments is not a filename, fails
    /// instantly with a Win32Exception, and then the hook is fast rather than slow — which is
    /// exactly how the first version of this test silently exercised nothing.
    /// </summary>
    private static string SpawningCommand => OperatingSystem.IsWindows() ? "hostname.exe" : "true";
}
