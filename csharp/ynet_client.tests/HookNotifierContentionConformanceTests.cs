// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Feature 108, T012 + T013 — the 40-iteration contention conformance check for
// `HookNotifier.WaitForIdle`, and the NEGATIVE CONTROL that proves the check can fail.
//
// 🔴 PATH DEVIATION, STATED RATHER THAN HIDDEN
//     tasks.md T012 names `csharp/ynet_transport.tests/`. The surface under test — `HookNotifier` —
//     lives in `csharp/ynet_client`, and `ynet_transport.tests` does not reference that project.
//     Putting the check where the code is not would have required a new project reference whose only
//     purpose was to satisfy a path in a task line. The check lives next to its subject; the manifest
//     entry `hook-notifier-wait-for-idle` is updated to point here.
//
// 🔴 WHY A HARNESS AND NOT TWO HAND-WRITTEN TESTS
//     FR-018a: a check never shown capable of failing scores zero. The only way to show THIS check
//     can fail is to run THE SAME HARNESS against an implementation with the defect. So the harness
//     is a method, the subject is an interface, and T012 and T013 differ in exactly one thing: which
//     implementation they hand it. If someone weakens the harness to make T012 pass, T013 goes green
//     too and the pair breaks loudly.
//
// 🔴 WHAT THE NEGATIVE CONTROL IS, HONESTLY
//     `PreFixIdleProbe` is the PRE-FIX ORDERING, not the pre-fix source: it tests
//     `queue.Count == 0 && !busy`, and the pump sets `busy` AFTER taking the item off the queue.
//     That window is real and is what was measured on 2026-09-05 (~1 run in 3). It is a few
//     instructions wide, so the probe WIDENS it with an explicit delay between the take and the
//     `busy` assignment. The widening does not invent the defect; it makes a real, narrow window
//     observable deterministically instead of one run in three. Stated here because a control whose
//     mechanism is not written down is a control nobody can audit — and the earlier 400-iteration
//     stress probe in this suite PASSED against the pre-fix code and was deleted for exactly that.

using System.Collections.Concurrent;

namespace Ynet.Client.Tests;

/// <summary>The signal under test, reduced to what the harness needs. Two implementations exist:
/// the real <see cref="HookNotifier"/> and the pre-fix ordering it replaced.</summary>
internal interface IIdleProbe : IDisposable
{
    void Enqueue(PendingAlert alert);
    bool WaitForIdle(TimeSpan timeout);
    HookAttempt? Last { get; }
    long Attempted { get; }
    long Dropped { get; }
}

internal sealed class RealNotifierProbe : IIdleProbe
{
    private readonly HookNotifier _inner;
    public RealNotifierProbe(AgentHook hook) => _inner = new HookNotifier(hook);
    public void Enqueue(PendingAlert alert) => _inner.Enqueue(alert);
    public bool WaitForIdle(TimeSpan timeout) => _inner.WaitForIdle(timeout);
    public HookAttempt? Last => _inner.Last;
    public long Attempted => _inner.Attempted;
    public long Dropped => _inner.Dropped;
    public void Dispose() => _inner.Dispose();
}

/// <summary>
/// The ordering `HookNotifier` had before feature 106: idle is `queue empty AND not busy`, and the
/// pump marks itself busy only AFTER the item has left the queue. Between those two statements the
/// item is in neither place, so an observer sampling there is told the work is done while the hook
/// has not been called and <see cref="Last"/> is still null.
/// </summary>
internal sealed class PreFixIdleProbe : IIdleProbe
{
    private readonly AgentHook _hook;
    private readonly BlockingCollection<PendingAlert> _queue =
        new(new ConcurrentQueue<PendingAlert>(), 256);
    private readonly Thread _thread;
    private readonly TimeSpan _windowWidening;
    private volatile bool _busy;

    public PreFixIdleProbe(AgentHook hook, TimeSpan windowWidening)
    {
        _hook = hook;
        _windowWidening = windowWidening;
        _thread = new Thread(Pump) { IsBackground = true, Name = "prefix-idle-probe" };
        _thread.Start();
    }

    public HookAttempt? Last { get; private set; }
    public long Attempted { get; private set; }
    public long Dropped { get; private set; }

    public void Enqueue(PendingAlert alert)
    {
        if (_queue.IsAddingCompleted || !_queue.TryAdd(alert)) Dropped++;
    }

    public bool WaitForIdle(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_queue.Count == 0 && !_busy) return true;   // ← the defect, verbatim
            Thread.Sleep(1);
        }
        return _queue.Count == 0 && !_busy;
    }

    private void Pump()
    {
        try
        {
            foreach (var alert in _queue.GetConsumingEnumerable())
            {
                // The item has left the queue here and `_busy` is still false: this is the window.
                if (_windowWidening > TimeSpan.Zero) Thread.Sleep(_windowWidening);
                _busy = true;
                try { Attempted++; Last = _hook.Notify(alert); }
                finally { _busy = false; }
            }
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    public void Dispose()
    {
        try { _queue.CompleteAdding(); } catch (ObjectDisposedException) { }
        _thread.Join(TimeSpan.FromSeconds(5));
        try { _queue.Dispose(); } catch (ObjectDisposedException) { }
    }
}

/// <summary>One iteration's verdict, kept so a failure can say WHICH invariant broke.</summary>
internal sealed record TrialOutcome(int Iteration, bool Correct, string Detail);

public sealed class HookNotifierContentionConformanceTests : IDisposable
{
    /// <summary>FR-004's declared iteration count for `hook-notifier-wait-for-idle`. The manifest
    /// says 40; this constant is what actually runs, and the two must not drift.</summary>
    private const int DeclaredIterations = 40;

    /// <summary>Announcements admitted per iteration, half of them from a competing thread —
    /// the manifest's declared contention, "concurrent enqueue during drain".</summary>
    private const int PerIteration = 24;

    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "ynet_notifier_contention", Guid.NewGuid().ToString("N"));

    public HookNotifierContentionConformanceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* a leftover temp dir is not a test failure */ }
    }

    // ---------------------------------------------------------------- T012

    [Fact]
    public void T012_WaitForIdle_is_correct_on_all_40_contended_iterations()
    {
        var outcomes = RunHarness(_ => new RealNotifierProbe(new AgentHook(null)));

        var wrong = outcomes.Where(o => !o.Correct).ToList();
        Assert.True(
            wrong.Count == 0,
            $"FR-004/FR-005: {outcomes.Count - wrong.Count}/{outcomes.Count} iterations correct. " +
            $"First failure: {wrong.FirstOrDefault()?.Detail ?? "-"}");
        Assert.Equal(DeclaredIterations, outcomes.Count);
    }

    // ---------------------------------------------------------------- T013

    [Fact]
    public void T013_the_same_harness_FAILS_the_pre_fix_ordering()
    {
        // Identical harness, identical contention, identical verdict rule. The ONLY difference is
        // the implementation handed to it. If this passes, T012 proves nothing (FR-018a).
        var outcomes = RunHarness(
            _ => new PreFixIdleProbe(new AgentHook(null), TimeSpan.FromMilliseconds(4)));

        var wrong = outcomes.Where(o => !o.Correct).ToList();
        Assert.True(
            wrong.Count > 0,
            "NEGATIVE CONTROL DID NOT FIRE: the harness reported 40/40 against the pre-fix ordering, " +
            "so it discriminates nothing and T012's green is decoration. Do not weaken this " +
            "assertion — fix the harness.");

        // 🔴 A TIMEOUT IS NOT THE DEFECT. If every failure here were a timeout, this control would
        // prove only that the harness notices a slow machine — and T012 would still be unproven
        // against the ordering it claims to govern. At least one failure must be the EARLY WAIT:
        // idle reported while admitted work had not been attempted.
        var earlyWaits = wrong.Where(o => o.Detail.Contains("idle reported")).ToList();
        Assert.True(
            earlyWaits.Count > 0,
            $"NEGATIVE CONTROL FIRED FOR THE WRONG REASON: {wrong.Count} failures, none of them an " +
            $"early wait. First: {wrong[0].Detail}");
    }

    // ---------------------------------------------------------------- the harness

    /// <summary>
    /// Drive one probe per iteration under concurrent enqueue-during-drain, and decide each
    /// iteration by ONE rule: <b>if WaitForIdle returned true, every admitted announcement must
    /// already have been attempted and its effect published.</b> A timeout is also incorrect — the
    /// signal must be reachable — but it is reported distinctly so a slow machine is not confused
    /// with an early wait.
    /// </summary>
    private List<TrialOutcome> RunHarness(Func<int, IIdleProbe> factory)
    {
        var outcomes = new List<TrialOutcome>(DeclaredIterations);

        for (var i = 0; i < DeclaredIterations; i++)
        {
            var spool = new PendingAlertSpool(Path.Combine(_dir, $"i{i}"));
            using var probe = factory(i);

            var admitted = 0;
            var half = PerIteration / 2;

            // A competing producer, so the wait is sampled while the pump is draining.
            var competitor = new Thread(() =>
            {
                for (var k = 0; k < half; k++)
                    probe.Enqueue(spool.Raise($"c-{i}-{k}", "gavriella.glpnet", "s"));
            });
            competitor.Start();

            for (var k = 0; k < half; k++)
            {
                probe.Enqueue(spool.Raise($"m-{i}-{k}", "gavriella.glpnet", "s"));
                admitted++;
            }
            competitor.Join();
            admitted += half;

            var reportedIdle = probe.WaitForIdle(TimeSpan.FromSeconds(20));
            var expectedAttempts = admitted - probe.Dropped;

            if (!reportedIdle)
            {
                outcomes.Add(new TrialOutcome(i, false,
                    $"iteration {i}: WaitForIdle TIMED OUT with {probe.Attempted}/{expectedAttempts} attempted"));
                continue;
            }

            // Idle was reported. It is evidence only if the work it reports has happened.
            if (probe.Attempted < expectedAttempts)
            {
                outcomes.Add(new TrialOutcome(i, false,
                    $"iteration {i}: idle reported with only {probe.Attempted}/{expectedAttempts} attempted " +
                    "— the signal was observable between accept and begin"));
                continue;
            }

            if (expectedAttempts > 0 && probe.Last is null)
            {
                outcomes.Add(new TrialOutcome(i, false,
                    $"iteration {i}: idle reported and Attempted={probe.Attempted}, but Last is still null " +
                    "— the effect was not published before the signal"));
                continue;
            }

            outcomes.Add(new TrialOutcome(i, true, "ok"));
        }

        return outcomes;
    }
}
