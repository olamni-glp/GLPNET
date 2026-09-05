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
