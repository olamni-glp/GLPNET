// glp_repl executable entrypoint — feature 020 (T017).
//
// The REAL REPL is the converted `GlpRuntime.Repl.Program` (from
// glp_runtime/bin/glp_repl.dart), compiled into the glp_runtime_net library
// (out/csharp/bin/glp_repl.cs). This file is the executable's thin startup
// shim: it delegates to that converted entrypoint so `glp_repl.exe` runs the
// converted runtime instead of the former placeholder.
//
// The structured trace instrumentation the differential equivalence oracle
// (feature 020) consumes is added candidate-side inside the converted runtime
// (see specs/020-trace-equivalence-fidelity/contracts/trace_normalization.md);
// the Dart golden under glp_runtime/ is never modified (R10 / HARD GATE 6).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using GlpRuntime.CrdtMsg.Bridge;
using GlpRuntime.Engine;
using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Runtime;

namespace GlpRuntime.Repl.Host;

internal static class EntryPoint
{
    // Task-returning async entry point; forwards argv to the converted REPL.
    private static Task Main(string[] args)
    {
        // feature 025 — composition root: wire the hand-authored link layer into the REPL
        // engine the converted Program builds. This is the ONLY place that may reference both
        // glp_runtime_net and GlpLink (the library can't, without a reference cycle). The hook
        // runs once, right after engine construction (out/csharp/bin/glp_repl.cs).
        GlpRuntime.Repl.Program.AfterEngineCreated = engine =>
        {
            var link = LinkKernels.Install(engine.Runtime);
            link.Transports.Register(new TcpTransport());        // first real cross-process leaf (127.0.0.1)
            link.Transports.Register(new LoopbackTransport());   // in-process hermetic substrate
            // feature 050 — register the genuine QUIC+WS leaf (036) so a GLP goal over a "quic"
            // link_id reaches it through the unchanged 025 kernels. The PERMANENT shared trust
            // material (cert + SPKI pin) is loaded from glpquick-cert/ fail-closed if absent
            // (FR-010/FR-011). The load is DEFERRED to first actual QUIC use (LazyQuicTransport):
            // a host without glpquick-cert/ still starts the REPL for non-QUIC (loopback/tcp) links,
            // and a QUIC link still fails closed LOUDLY the moment it is opened (P1 fix 2026-07-13,
            // codexreview 20260713T070618Z). No TCP/loopback fallback — QuicTransport refuses loudly
            // on a host without QUIC (FR-002).
            link.Transports.Register(new LazyQuicTransport(() =>
            {
                var (quicCert, quicPin) = SharedCertMaterial.LoadFromRepo();
                return new QuicTransport(quicCert, quicPin);
            }));
            // feature 050 US3 (T026-T028) — macaroon capability gate, verify-before-act (FR-008/9).
            // The static-macaroon root key is loaded out-of-band alongside the cert (beacon model,
            // fail-closed if absent) and this endpoint's presented static macaroon minted — DEFERRED
            // to first gated action (LazyMacaroonLinkGate), same rationale as the transport above.
            // The gate runs in LinkEstablish BEFORE any "quic" endpoint is opened; every outbound
            // envelope carries the macaroon in the capability slot (section 0x20, envelope v2);
            // inbound gated actions re-verify it. Refusals are recorded (ProvenanceOutcome.Refused)
            // and fail closed — never a crash, never a silent drop.
            var quicGate = new LazyMacaroonLinkGate(() =>
            {
                var (macaroonRootKey, staticMacaroon) = StaticMacaroonMaterial.LoadFromRepo();
                return new MacaroonLinkGate(macaroonRootKey, staticMacaroon);
            });
            link.CapabilityGates.Register(LinkScheme.Quic, quicGate);
            // feature 050 US2 (T018) — the "quic" link's L5 wire payload is a 041 crdtmsg envelope
            // (FR-005). Inject the CrdtMsgPayloadCodec for the Quic scheme; loopback/tcp keep the
            // default ground-relay blob (byte-for-byte unchanged). The kernels stay codec-agnostic —
            // LinkEstablish selects the per-link codec from this registry at establishment.
            // US3: the codec is capability-gated — it attaches/verifies the macaroon slot. Deferred
            // (LazyCrdtMsgPayloadCodec) so the gate's macaroon load happens on first wire use, not
            // startup; quicGate.Value materializes the real gate at that point.
            link.PayloadCodecs.Register(LinkScheme.Quic,
                new LazyCrdtMsgPayloadCodec(() => new CrdtMsgPayloadCodec(quicGate.Value)));

            // feature 064 — durable listener service box (gavri variant), T004/T007/T008:
            // the resume-goal hook. Runs AFTER the link wiring above (the registered goal
            // may arm a QUIC listener) and BEFORE the interactive loop the converted
            // Main enters. With no registration file this is a single silent probe
            // (SC-005). Contract: specs/064-durable-listener-service-box/contracts/
            // resume-registration.md.
            RunResumeGoal(engine, link);
        };
        return GlpRuntime.Repl.Program.Main(args);
    }

    // Replays the operator's exact manual sequence for the registered service:
    // load the program (interactive-load semantics), then run the goal
    // (typed-input semantics), synchronously — the engine's runner is
    // single-threaded and a background dispatch would race the interactive
    // loop (research.md R2). Every failure is a named diagnostic and falls
    // through to the normal prompt (FR-009).
    private static void RunResumeGoal(GlpEngine engine, LinkRuntime link)
    {
        if (!ResumeConfig.TryLoad(Console.WriteLine, out var reg) || reg is null)
            return;
        if (!reg.Enabled)
        {
            Console.WriteLine("resume: registration present but disabled");
            return;
        }

        Console.WriteLine($"resume: arming {reg.Goal} from {reg.Program}");

        // T007/T008 — durable history. The WAL is opened once; its ops are handed to the pump
        // as the per-link REPLAY SOURCE (drained into the inbox before that link's receive loop
        // starts, so history strictly precedes live traffic), and every LIVE delivered term is
        // appended before the program can act on it. Replayed items never reach the observer,
        // so replay is idempotent across restarts (contracts/message-log-and-replay.md).
        var wal = ServiceWal.Open(reg.RepoRoot, Console.WriteLine);
        if (wal is not null && reg.Replay)
        {
            // Boot-time visibility: the log's depth is known now, but the replay itself can only
            // happen when the service's link establishes — that is when the program's In stream
            // exists to receive it (contracts/message-log-and-replay.md). History still strictly
            // precedes live traffic on that link.
            Console.WriteLine($"resume: {wal.Ops.Count} message(s) in the log ({wal.BackendName}); "
                + "replayed to the service when its link establishes");
            var replayed = 0;
            link.Pump.ReplaySource = _ =>
            {
                var terms = new List<Term>();
                foreach (var op in wal.Ops)
                {
                    try
                    {
                        terms.Add(wal.Decode(op, _ => throw new InvalidOperationException(
                            "resume replay received a non-ground payload (embedded variable)")));
                    }
                    catch (Exception e)
                    {
                        // Named diagnostic, then arm with the prefix that did replay (FR-009).
                        Console.WriteLine($"resume: replay failed: {e.Message}");
                        break;
                    }
                }
                replayed = terms.Count;
                Console.WriteLine($"resume: replayed {replayed} message(s)");
                return terms;
            };
        }
        else if (wal is not null)
        {
            Console.WriteLine("resume: replay disabled by registration");
        }
        if (wal is not null)
            link.Pump.OnDelivered = (_, term) => wal.Append(term, Console.WriteLine);

        // The hooks belong to a SUCCESSFULLY armed service only. Every failure path below
        // falls through to the interactive REPL (FR-009) — with the hooks still live, a
        // manual link opened there would receive the failed service's replay and append its
        // own traffic into the service's WAL. Uninstall them on failure, then release the
        // WAL backend (its pglite connection would otherwise leak for the process lifetime).
        // Hooks first: AddLink drains ReplaySource synchronously before its receive loop
        // starts, so once both are null no new drain can reach the disposed backend.
        void Disarm() { link.Pump.ReplaySource = null; link.Pump.OnDelivered = null; wal?.Dispose(); }

        var programPath = Path.Combine(reg.RepoRoot, reg.Program);
        if (!File.Exists(programPath))
        {
            Console.WriteLine($"resume: program load failed: {programPath}: File not found");
            Disarm();
            return;
        }
        try
        {
            if (!engine.LoadFile(programPath))
            {
                Console.WriteLine($"resume: program load failed: {reg.Program} (rejected by the load pipeline)");
                Disarm();
                return;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"resume: program load failed: {e.Message}");
            Disarm();
            return;
        }

        try
        {
            var result = engine.RunGoalAsync(reg.Goal).GetAwaiter().GetResult();
            Console.WriteLine($"resume: goal finished ({result.Status})");
            if (result.Error is not null)
                Console.WriteLine($"resume: goal error: {result.Error}");
            // Suspended is the SOLE armed state: a suspended listener's hooks must stay
            // live. Every other terminal state (Failed, errored, or a Success whose goal
            // ran to completion) is not an armed service — uninstall the hooks just like
            // a thrown goal, so a later interactive link cannot see the service's replay
            // or append into its WAL.
            if (!result.Suspended)
                Disarm();
        }
        catch (Exception e)
        {
            Console.WriteLine($"resume: goal failed: {e.Message}");
            Disarm();
        }
    }
}
