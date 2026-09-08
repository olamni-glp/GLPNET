// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Feature 108, T025 — the WAL-replay-clobbers-ack disclosure (spec instance 8, FR-012).
//
// 🔴 THESE TESTS ARE EXPECTED TO FAIL, AND THAT IS THE DELIVERABLE (Constitution II).
//     They are the disclosure mechanism for a defect this lane does NOT own and MUST NOT patch:
//     Q-glpnetshiras-50 rules YngeniOS.Ynet.Client (@ariellas-qhstate) canonical, so a defect found
//     here is disclosed upstream, never fixed locally. A test that goes green by working around the
//     defect would delete the disclosure.
//
//     They carry Trait("category","disclosure") so a gate can exclude them deliberately
//     (`--filter category!=disclosure`) rather than by forgetting they exist. The suite's honest
//     count is stated in specs/108-evidence-signal-ordering/tasks.md T025.
//
// WHY THIS SHAPE — the engineer ruled "2 and 3 then 1" on 2026-09-07:
//     (2) drive the REAL canonical binary  -> T025_REAL_BINARY_...
//     (3) model the defect locally         -> T025_MODEL_...      + T025_CONTROL_...
//     (1) record the discharge             -> tasks.md T025
//     The model alone would prove only that a deliberately-broken double can be broken. The real
//     binary alone would leave a lane with no qhstate checkout unable to see the defect at all.
//
// MEASURED BY HAND FIRST, on SHIRAS 2026-09-08T05:26Z, against the live Release build:
//     ack               -> acknowledged: true,  arrived_utc: 05:26:36.9682372
//     run --once        -> "effects=0 duplicates=1 replayed_on_start=1"   (NO new frame arrived)
//     re-observe        -> acknowledged: FALSE, arrived_utc: 05:26:52.1628301   (RE-STAMPED)
//     That is spec instance 8 confirmed on a third host and on today's build. The fix is not
//     "make ack durable" — the ack IS durable and survives the process dying. The fix is that the
//     startup replay must MERGE BY message_id and never clobber a record already present.

using System.Diagnostics;
using System.Text.Json;

namespace Ynet.Client.Tests;

[Trait("category", "disclosure")]
public sealed class AckDurabilityDisclosureTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "ynet_ack_disclosure", Guid.NewGuid().ToString("N"));

    public AckDurabilityDisclosureTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* a leftover temp dir is not a test failure */ }
    }

    /// <summary>Where the canonical client lives. Overridable so this is not a shiras-only path.</summary>
    private static string? CanonicalClientDll
    {
        get
        {
            var fromEnv = Environment.GetEnvironmentVariable("YNET_CANONICAL_CLIENT_DLL");
            if (!string.IsNullOrWhiteSpace(fromEnv)) return File.Exists(fromEnv) ? fromEnv : null;
            const string known = "/mnt/biwin/D_DRIVE/BSTDEV/research/qhstate/Csharp/yngenios/"
                               + "YngeniOS.Ynet.Client.Cli/bin/Release/net11.0/ynet-client.dll";
            return File.Exists(known) ? known : null;
        }
    }

    // ------------------------------------------------------------------ (2) the real binary

    [Fact]
    public void T025_REAL_BINARY_an_ack_survives_a_restart_of_the_canonical_client()
    {
        var dll = CanonicalClientDll;
        if (dll is null)
        {
            // 🔴 C-20: "I could not check" is NEVER folded into "it is not there". This is a
            // distinct outcome from the defect being present, and it says so in the message rather
            // than passing quietly — a silent skip here would report a clean run for a check that
            // never executed, which is measured instance 2 of this very feature.
            Assert.Fail(
                "UNVERIFIABLE — not a defect verdict. The canonical YngeniOS.Ynet.Client build was "
              + "not found. Set YNET_CANONICAL_CLIENT_DLL to its ynet-client.dll to run this probe. "
              + "This says nothing about whether the defect is present.");
        }

        var wal = Path.Combine(_dir, "wal");
        var alerts = Path.Combine(_dir, "alerts");
        var coop = Path.Combine(_dir, "coop");
        foreach (var d in new[] { wal, alerts, coop }) Directory.CreateDirectory(d);

        const string target = "t025target";
        const string peer = "t025peer";

        // The target must announce its inbox before a peer can address it — the client refuses to
        // invent a peer inbox, which is correct and is why this ordering is not incidental.
        Run(dll!, "run", "--lane", target, "--node", "shiras", "--wal", wal, "--alerts", alerts,
                  "--coop", coop, "--once");
        Run(dll!, "send", "--lane", peer, "--node", "shiras", "--to", $"shiras/{target}",
                  "--signal", "probe", "--body", "t025 disclosure probe", "--coop", coop);
        Run(dll!, "run", "--lane", target, "--node", "shiras", "--wal", wal, "--alerts", alerts,
                  "--coop", coop, "--once");

        var raised = Alerts(dll!, target, alerts);
        Assert.True(raised.Count == 1,
            $"probe setup did not raise exactly one alert (got {raised.Count}); the disclosure cannot run");
        var id = raised[0].GetProperty("message_id").GetString()!;

        Run(dll!, "ack", id, "--lane", target, "--alerts", alerts);

        var afterAck = Alerts(dll!, target, alerts).Single();
        Assert.True(afterAck.GetProperty("acknowledged").GetBoolean(),
            "the ack did not take effect at all, so the durability question cannot be asked");
        var arrivedBefore = afterAck.GetProperty("arrived_utc").GetString();

        // THE RESTART. A fresh process replays the retained WAL entry on start.
        Run(dll!, "run", "--lane", target, "--node", "shiras", "--wal", wal, "--alerts", alerts,
                  "--coop", coop, "--once");

        var afterRestart = Alerts(dll!, target, alerts).Single();
        var ackAfter = afterRestart.GetProperty("acknowledged").GetBoolean();
        var arrivedAfter = afterRestart.GetProperty("arrived_utc").GetString();

        Assert.True(ackAfter,
            "DISCLOSED DEFECT (FR-012, spec instance 8): the startup WAL replay re-raised an "
          + "already-acknowledged alert and CLOBBERED the flag. No new frame arrived — the client "
          + "itself reports effects=0. Completion that a restart undoes was never completion. "
          + "Owner @ariellas-qhstate; this lane discloses and does not patch (Q-glpnetshiras-50).");
        Assert.True(arrivedBefore == arrivedAfter,
            $"DISCLOSED DEFECT (FR-012): arrived_utc was RE-STAMPED by the replay "
          + $"({arrivedBefore} -> {arrivedAfter}), so the record now claims to have arrived at the "
          + "restart time. Replay must merge by message_id, never overwrite.");
    }

    // ------------------------------------------------------------------ (3) the local model

    /// <summary>
    /// The replay path as the canonical client implements it: re-raise unconditionally.
    /// This is a MODEL of the measured behaviour, not a copy of its source.
    /// </summary>
    private sealed class ClobberingReplayStore : IReplayStore
    {
        private readonly Dictionary<string, (bool Acked, string Arrived)> _rows = new();
        public void Raise(string id, string arrivedUtc) => _rows[id] = (false, arrivedUtc);
        public void Ack(string id) => _rows[id] = (true, _rows[id].Arrived);
        public void ReplayOnStart(string id, string nowUtc) => _rows[id] = (false, nowUtc); // ← the defect
        public (bool Acked, string Arrived) Observe(string id) => _rows[id];
    }

    /// <summary>The same path done correctly: a replay MERGES by message_id and never overwrites.</summary>
    private sealed class MergingReplayStore : IReplayStore
    {
        private readonly Dictionary<string, (bool Acked, string Arrived)> _rows = new();
        public void Raise(string id, string arrivedUtc) => _rows[id] = (false, arrivedUtc);
        public void Ack(string id) => _rows[id] = (true, _rows[id].Arrived);
        public void ReplayOnStart(string id, string nowUtc)
        {
            if (!_rows.ContainsKey(id)) _rows[id] = (false, nowUtc);   // only a record we do not have
        }
        public (bool Acked, string Arrived) Observe(string id) => _rows[id];
    }

    private interface IReplayStore
    {
        void Raise(string id, string arrivedUtc);
        void Ack(string id);
        void ReplayOnStart(string id, string nowUtc);
        (bool Acked, string Arrived) Observe(string id);
    }

    private static (bool Acked, string Arrived, string ArrivedBefore) ObserveRestartObserve(IReplayStore store)
    {
        store.Raise("m-1", "2026-09-08T05:26:36Z");
        store.Ack("m-1");
        var before = store.Observe("m-1");
        store.ReplayOnStart("m-1", "2026-09-08T05:26:52Z");   // the restart
        var after = store.Observe("m-1");
        return (after.Acked, after.Arrived, before.Arrived);
    }

    [Fact]
    public void T025_MODEL_the_clobbering_replay_loses_the_ack()
    {
        var (acked, arrived, arrivedBefore) = ObserveRestartObserve(new ClobberingReplayStore());

        Assert.True(acked,
            "DISCLOSED DEFECT (FR-012) reproduced in-repo without the canonical binary: a replay "
          + "that re-raises unconditionally destroys an ack that was already durable.");
        Assert.True(arrived == arrivedBefore,
            $"DISCLOSED DEFECT (FR-012): arrived_utc re-stamped {arrivedBefore} -> {arrived}.");
    }

    [Fact]
    public void T025_CONTROL_a_replay_that_merges_by_message_id_keeps_the_ack()
    {
        // 🔴 WITHOUT THIS, THE TWO FAILURES ABOVE PROVE NOTHING. They would be equally red against
        // assertions that no implementation could satisfy. This runs the SAME observe/restart/
        // re-observe sequence and the SAME assertions against the merging path, and it is GREEN —
        // so the failures are a statement about the implementation, not about the test.
        var (acked, arrived, arrivedBefore) = ObserveRestartObserve(new MergingReplayStore());

        Assert.True(acked, "the merging replay must preserve the ack");
        Assert.Equal(arrivedBefore, arrived);
    }

    // ------------------------------------------------------------------ helpers

    private static void Run(string dll, params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add(dll);
        foreach (var a in args) psi.ArgumentList.Add(a);
        // The client's own probe measured cp1252 consoles mangling its output; the harness does not
        // depend on the text, only on the side effects in --alerts.
        using var p = Process.Start(psi)!;
        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit(120_000);
    }

    private static List<JsonElement> Alerts(string dll, string lane, string alertsDir)
    {
        var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add(dll);
        foreach (var a in new[] { "alerts", "--lane", lane, "--alerts", alertsDir, "--json", "--all" })
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var json = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit(120_000);
        return JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();
    }
}
