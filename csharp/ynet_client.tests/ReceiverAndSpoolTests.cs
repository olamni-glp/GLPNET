// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The M6 claims, each pinned by a test that could fail:
//   M6-b  the client works with no agent attached
//   M6-c  receipt never waits for the agent
//   M6-f  an alert survives the agent being absent, busy or restarted, and is drained by choice
//
// The load-bearing one is "a failing hook must not lose the message". It is easy to build a client
// that looks correct while the agent is up and silently loses traffic while it is down, so the
// tests below drive the down case directly rather than the happy path.

using Ynet.Client.Machine;

namespace Ynet.Client.Tests;

public sealed class ReceiverAndSpoolTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "ynet_client_tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* a leftover temp dir is not a test failure */ }
    }

    private static YnetMessage Msg(string id, string origin = "gavriella.glpnet", string summary = "hello") =>
        new(id, origin, summary, ReadOnlyMemory<byte>.Empty);

    // ---- the spool --------------------------------------------------------------------------

    [Fact]
    public void An_alert_survives_the_process_that_raised_it()
    {
        var first = new PendingAlertSpool(_dir);
        first.Raise("m-1", "shiras.glpnet", "roster update");

        // A different instance over the same directory is what a restart looks like from here.
        var afterRestart = new PendingAlertSpool(_dir);

        var pending = afterRestart.Undrained();
        Assert.Single(pending);
        Assert.Equal("m-1", pending[0].MessageId);
        Assert.Equal(1, pending[0].Presentations);
    }

    [Fact]
    public void Re_raising_the_same_message_re_presents_it_rather_than_duplicating_it()
    {
        var spool = new PendingAlertSpool(_dir);
        spool.Raise("m-1", "o", "s");
        var second = spool.Raise("m-1", "o", "s");

        Assert.Equal(2, second.Presentations);
        Assert.Single(spool.Undrained());       // one alert, presented twice — not two alerts
    }

    [Fact]
    public void Draining_is_explicit_and_idempotent()
    {
        var spool = new PendingAlertSpool(_dir);
        var a = spool.Raise("m-1", "o", "s");

        Assert.True(spool.Drain(a.AlertId));    // first drain removed it
        Assert.False(spool.Drain(a.AlertId));   // second is a recorded no-op, not an error
        Assert.Empty(spool.Undrained());
    }

    [Fact]
    public void Undrained_alerts_come_back_oldest_first()
    {
        var spool = new PendingAlertSpool(_dir);
        var t = DateTimeOffset.UtcNow;
        spool.Raise("m-late", "o", "s", t.AddMinutes(5));
        spool.Raise("m-early", "o", "s", t);

        var order = spool.Undrained().Select(a => a.MessageId).ToArray();
        Assert.Equal(new[] { "m-early", "m-late" }, order);
    }

    [Fact]
    public void An_unreadable_spool_entry_is_quarantined_never_deleted()
    {
        var spool = new PendingAlertSpool(_dir);
        spool.Raise("m-good", "o", "s");
        File.WriteAllText(Path.Combine(_dir, "corrupt.json"), "{ this is not json");

        var pending = spool.Undrained();

        Assert.Single(pending);                                        // the good one still reads
        Assert.False(File.Exists(Path.Combine(_dir, "corrupt.json")));
        Assert.True(File.Exists(Path.Combine(_dir, "corrupt.json.unreadable")));  // kept, not lost
    }

    // ---- the receiver -----------------------------------------------------------------------

    private (YnetReceiverMachine machine, LoopbackInbound plane, PendingAlertSpool spool) Build(string? hook = null)
    {
        var plane = new LoopbackInbound();
        var spool = new PendingAlertSpool(_dir);
        var machine = new YnetReceiverMachine(plane, spool, new AgentHook(hook));
        machine.Start();
        machine.PumpOnce();       // Booting -> Operational -> Idle
        return (machine, plane, spool);
    }

    [Fact]
    public void The_client_boots_to_idle_without_any_agent()
    {
        var (machine, _, _) = Build();
        using var _m = machine;

        Assert.True(machine.IsIdle);
        Assert.False(machine.IsDegraded);
    }

    [Fact]
    public void A_message_received_with_NO_agent_configured_still_lands_durably()
    {
        var (machine, plane, spool) = Build(hook: null);
        using var _m = machine;

        Assert.True(plane.Deliver(Msg("m-1")));
        machine.PumpOnce();
        Assert.True(machine.WaitForNotifications(TimeSpan.FromSeconds(10)));

        Assert.Equal(1, machine.MessagesReceived);
        Assert.Equal(1, machine.AlertsRaised);
        Assert.Single(spool.Undrained());
        Assert.Equal(HookOutcome.NotConfigured, machine.LastHookAttempt!.Outcome);
        Assert.True(machine.IsIdle);              // and it returned to service
    }

    [Fact]
    public void A_FAILING_hook_does_not_lose_the_message()
    {
        // The agent is "down": the hook command cannot possibly run.
        var (machine, plane, spool) = Build(hook: "this-command-does-not-exist-ynet-client-test");
        using var _m = machine;

        plane.Deliver(Msg("m-1"));
        machine.PumpOnce();
        Assert.True(machine.WaitForNotifications(TimeSpan.FromSeconds(20)));

        Assert.Equal(HookOutcome.Failed, machine.LastHookAttempt!.Outcome);
        Assert.NotNull(machine.LastHookAttempt.Detail);      // the failure is recorded, not swallowed
        Assert.Single(spool.Undrained());                    // and the alert is still there
        Assert.True(machine.IsIdle);                         // and the client kept running
    }

    [Fact]
    public void Receipt_walks_idle_receiving_alerting_and_back_to_idle_in_that_order()
    {
        var (machine, plane, _) = Build();
        using var _m = machine;

        plane.Deliver(Msg("m-1"));
        machine.PumpOnce();

        var cycle = machine.Trace.SkipWhile(t => t != "Idle:exit").ToArray();
        Assert.Equal(
            new[] { "Idle:exit", "Receiving:entry", "Receiving:exit", "Alerting:entry", "Alerting:exit", "Idle:entry" },
            cycle);
    }

    [Fact]
    public void Several_messages_each_produce_their_own_pending_alert()
    {
        var (machine, plane, spool) = Build();
        using var _m = machine;

        foreach (var id in new[] { "m-1", "m-2", "m-3" })
        {
            plane.Deliver(Msg(id));
            machine.PumpOnce();
        }

        Assert.Equal(3, machine.MessagesReceived);
        Assert.Equal(3, spool.Undrained().Count);
    }

    [Fact]
    public void The_agent_drains_on_its_own_schedule_and_the_rest_stay_pending()
    {
        var (machine, plane, spool) = Build();
        using var _m = machine;

        foreach (var id in new[] { "m-1", "m-2" })
        {
            plane.Deliver(Msg(id));
            machine.PumpOnce();
        }

        var first = machine.Pending()[0];
        Assert.True(machine.DrainAlert(first.AlertId));

        Assert.Single(spool.Undrained());     // the other one is untouched — nothing auto-drains
    }

    [Fact]
    public void A_fault_degrades_the_client_and_retry_brings_it_back()
    {
        var (machine, _, _) = Build();
        using var _m = machine;

        machine.Post(new QEvt(YnetSignal.Fault, "carrier closed"));
        machine.PumpOnce();
        Assert.True(machine.IsDegraded);
        Assert.Equal("carrier closed", machine.FaultReason);

        machine.Post(new QEvt(YnetSignal.Retry));
        machine.PumpOnce();
        Assert.True(machine.IsIdle);
        Assert.Null(machine.FaultReason);
    }

    [Fact]
    public void A_message_delivered_while_degraded_is_refused_by_the_plane_not_silently_accepted()
    {
        var (machine, plane, spool) = Build();
        using var _m = machine;

        machine.Post(new QEvt(YnetSignal.Fault, "carrier closed"));
        machine.PumpOnce();

        Assert.False(plane.Deliver(Msg("m-1")));   // the plane says no; it does not pretend
        machine.PumpOnce();
        Assert.Empty(spool.Undrained());
    }

    [Fact]
    public void Stop_is_terminal_and_closes_the_plane()
    {
        var (machine, plane, _) = Build();
        using var _m = machine;

        machine.Post(new QEvt(YnetSignal.Stop));
        machine.PumpOnce();

        Assert.True(machine.IsStopped);
        Assert.False(plane.Deliver(Msg("m-1")));
    }


    [Fact]
    public void A_write_leaves_no_temp_file_behind_and_uses_a_unique_temp_name()
    {
        // Adopted from @shiras-glpnet's TOCTOU finding: a FIXED temp name is shared by every
        // concurrent writer. This asserts the observable consequence — the directory holds exactly
        // the alert, with no ".tmp" sibling for a second writer to collide with or a reader to trip on.
        var spool = new PendingAlertSpool(_dir);
        spool.Raise("m-1", "o", "s");
        spool.Raise("m-1", "o", "s");

        var files = Directory.GetFiles(_dir).Select(Path.GetFileName).ToArray();
        Assert.Single(files);
        Assert.DoesNotContain(files, f => f!.Contains(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void Concurrent_writers_to_one_spool_never_produce_a_torn_read()
    {
        var spool = new PendingAlertSpool(_dir);
        Parallel.For(0, 40, i => spool.Raise($"m-{i % 8}", "o", $"summary {i}"));

        // Every surviving file must still parse: a torn write would be quarantined as unreadable.
        var pending = spool.Undrained();
        Assert.Equal(8, pending.Count);
        Assert.DoesNotContain(Directory.GetFiles(_dir), f => f.EndsWith(".unreadable", StringComparison.Ordinal));
    }


    // ---- codex cycle 1 regressions: seven P1 findings, each with a test that fails without the fix

    [Fact]
    public void CODEX_P1_overflow_is_recorded_durably_and_the_machine_degrades()
    {
        // Before the fix: a refused MessageArrived posted Fault into the SAME full mailbox, which
        // was also refused, so the machine never degraded and the message vanished while the
        // carrier believed it delivered.
        var plane = new LoopbackInbound();
        var spool = new PendingAlertSpool(_dir);
        using var machine = new YnetReceiverMachine(plane, spool, new AgentHook(null), capacity: 2);
        machine.Start();
        machine.PumpOnce();

        for (var i = 0; i < 12; i++) plane.Deliver(Msg($"burst-{i}"));

        Assert.True(machine.Overflowed > 0);                 // counted
        Assert.Null(machine.OverflowSpoolError);
        Assert.Contains(spool.Undrained(), a => a.MessageId.StartsWith("OVERFLOW-", StringComparison.Ordinal));

        machine.PumpOnce();
        Assert.True(machine.IsDegraded);                     // and it actually degraded
    }

    [Fact]
    public void CODEX_P1_internal_completion_events_are_never_refused_by_a_full_mailbox()
    {
        // Before the fix: a carrier burst during Receiving:entry could fill the mailbox before
        // AlertRaised was posted, wedging the machine in Receiving forever.
        var plane = new LoopbackInbound();
        var spool = new PendingAlertSpool(_dir);
        using var machine = new YnetReceiverMachine(plane, spool, new AgentHook(null), capacity: 1);
        machine.Start();
        machine.PumpOnce();

        for (var i = 0; i < 8; i++) { plane.Deliver(Msg($"m-{i}")); machine.PumpOnce(); }

        // Whatever else happened, the machine must not be stuck in a transient state.
        Assert.True(machine.IsIdle || machine.IsDegraded);
        Assert.True(machine.AlertsRaised >= 1);
    }

    [Fact]
    public void CODEX_P1_a_failed_durable_write_closes_the_plane_instead_of_losing_the_message()
    {
        // Before the fix: the exception escaped to DispatchGuarded, the plane stayed open, and the
        // carrier kept delivering into a hole.
        var plane = new LoopbackInbound();
        var unwritable = Path.Combine(_dir, "spool");
        var spool = new PendingAlertSpool(unwritable);
        using var machine = new YnetReceiverMachine(plane, spool, new AgentHook(null));
        machine.Start();
        machine.PumpOnce();

        Directory.Delete(unwritable, recursive: true);
        File.WriteAllText(unwritable, "now a FILE, so every spool write must fail");

        plane.Deliver(Msg("m-1"));
        machine.PumpOnce();

        Assert.NotNull(machine.SpoolError);
        Assert.True(machine.IsDegraded);
        Assert.False(plane.Deliver(Msg("m-2")));   // backpressure: the carrier is refused, not fed
    }

    [Fact]
    public void CODEX_P1_a_hanging_hook_does_not_stall_receipt()
    {
        // Before the fix: _hook.Notify ran on the single dispatch thread with a 5s bound, so a
        // hanging agent stalled receipt. The hook below sleeps well past the machine's work.
        var hookCmd = OperatingSystem.IsWindows() ? "timeout" : "sleep";
        var plane = new LoopbackInbound();
        var spool = new PendingAlertSpool(_dir);
        using var machine = new YnetReceiverMachine(plane, spool, new AgentHook(hookCmd));
        machine.Start();
        machine.PumpOnce();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 5; i++) { plane.Deliver(Msg($"m-{i}")); machine.PumpOnce(); }
        sw.Stop();

        // Five alerts durable, and the dispatch thread never waited on the hook.
        Assert.Equal(5, spool.Undrained().Count);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"receipt took {sw.Elapsed.TotalSeconds:0.0}s — the hook is back on the dispatch thread");
    }

    [Fact]
    public void CODEX_P1_two_message_ids_sharing_a_sanitized_prefix_do_not_overwrite_each_other()
    {
        // Before the fix: the id was a timestamp plus the first 48 sanitized chars, so these two
        // collided within one millisecond and the second silently destroyed the first.
        var spool = new PendingAlertSpool(_dir);
        var stem = new string('a', 48);
        var a = spool.Raise(stem + "-ONE", "o", "first");
        var b = spool.Raise(stem + "-TWO", "o", "second");

        Assert.NotEqual(a.AlertId, b.AlertId);
        Assert.Equal(2, spool.Undrained().Count);
    }

    [Theory]
    [InlineData("../../escape")]
    [InlineData(@"..\..\escape")]
    [InlineData(@"C:\Windows\Temp\anything")]
    [InlineData("/etc/passwd")]
    [InlineData("not-an-alert-id")]
    public void CODEX_P1_drain_refuses_an_id_that_is_not_ours(string bad)
    {
        // Before the fix: Path.Combine resolved outside the spool and `drain` deleted an arbitrary
        // reachable .json. The drain verb is a CLI surface, so its argument is untrusted input.
        var spool = new PendingAlertSpool(_dir);
        var victim = Path.Combine(_dir, "..", "victim.json");
        File.WriteAllText(victim, "{}");

        Assert.False(spool.Drain(bad));      // refused, and reported as "was not pending"
        Assert.True(File.Exists(victim));    // and nothing outside the spool was touched

        File.Delete(victim);
    }

    [Fact]
    public void CODEX_P1_two_spool_instances_over_one_directory_do_not_lose_a_presentation()
    {
        // Before the fix: _gate serialised threads in ONE process; `run` and `inject` are two.
        // Two independent instances stand in for two processes over the same directory.
        var a = new PendingAlertSpool(_dir);
        var b = new PendingAlertSpool(_dir);

        a.Raise("m-1", "o", "s");
        var second = b.Raise("m-1", "o", "s");

        Assert.Equal(2, second.Presentations);
        Assert.Single(a.Undrained());
    }

    // ---- the bounded mailbox ------------------------------------------------------------------

    [Fact]
    public void Capacity_is_signalled_never_a_silent_drop()
    {
        var plane = new LoopbackInbound();
        using var machine = new YnetReceiverMachine(plane, new PendingAlertSpool(_dir), new AgentHook(null), capacity: 2);
        machine.Start();     // NOT pumped: the mailbox stays full on purpose

        var outcomes = new List<AppendOutcome>();
        for (var i = 0; i < 6; i++) outcomes.Add(machine.Post(new QEvt(YnetSignal.Retry)));

        Assert.Contains(AppendOutcome.Closed, outcomes);        // refusal is visible to the caller
        Assert.True(machine.Refused > 0);                       // and counted, so it is reportable
        Assert.Equal(machine.Capacity, machine.Depth);          // the bound is enforced, not advisory
    }

    [Fact]
    public void Sending_works_with_no_agent_and_reports_a_refusal_honestly()
    {
        var plane = new LoopbackInbound();
        var sent = new List<YnetMessage>();
        using var machine = new YnetReceiverMachine(
            plane, new PendingAlertSpool(_dir), new AgentHook(null),
            outbound: new FakeOutbound(sent, accept: true));
        machine.Start();

        Assert.True(machine.Send(Msg("out-1")));
        Assert.Single(sent);

        using var noOutbound = new YnetReceiverMachine(new LoopbackInbound(), new PendingAlertSpool(_dir), new AgentHook(null));
        noOutbound.Start();
        Assert.False(noOutbound.Send(Msg("out-2")));   // no plane: false, not a pretend success
    }

    private sealed class FakeOutbound(List<YnetMessage> sink, bool accept) : IYnetOutbound
    {
        public bool Send(YnetMessage message)
        {
            if (!accept) return false;
            sink.Add(message);
            return true;
        }
    }
}
