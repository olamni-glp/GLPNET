# Reconciliation Memo — #8 liveness-crash-restart-host

**Feature id:** liveness-crash-restart-host  
**Dossier kind (§11 #8):** MVP  
**Date:** 2026-06-09  
**Seed state:** captured; WSJF=1.62, RICE=750  
**Author:** sub-agent reconciliation pass (026-engine-review-dossier)

---

## Dossier cross-references

| Anchor | Content |
|---|---|
| §5 (full) | Liveness / crash-signal / restart model — the complete design source for this seed |
| §0.4 row "OS-liveness / crash-signal / watchdog" | Classification: net-new; as-built: zero; substrate note |
| §8.2 | Slice B = Slice A + persistence + liveness/crash/restart host + restore-and-resume |
| §11 #8 | Seed entry: depends_on #7; §ref §5 |
| §11 #7 | engine-state-snapshot-and-persistence-api (hard prerequisite) |
| §11 #9 | restore-and-resume-with-link-reestablish (immediate dependent) |
| §6.4 | Bootstrap + restore-and-resume (the behaviour this host must invoke on restart) |
| §10.7 | Where the snapshot/resume driver lives (FR-057 fork) |
| §12 risk 2 | Heap snapshot scale/cost |
| §12 risk 3 | Ephemeral OS file/FFI handles — no re-establish path |

---

## Seed-vs-dossier-vs-code

### Stored seed profile (from `buildkit-roadmap brief`)

```
Notes: MVP. BackgroundService/Windows-Service/systemd host: liveness ping (timer + optional
self-prove goal), crash exit code, supervised restart calling restore-and-resume.
depends-on: #7. (§7 #8)
```

Problem/need, target-user, value/outcome, and risk fields are blank. The effort field says L.

### Dossier §11 #8 entry

```
| 8 | liveness-crash-restart-host | MVP | BackgroundService/Windows-Service/systemd host:
  liveness ping, crash exit code, supervised restart→restore-and-resume | the §5 success
  theme | 7 | §5 |
```

### Divergences

1. **Seed note says `§7 #8` — should be `§5 #8`.** The dossier §ref is §5; §7 is the mailbox
   decision. This is a copy-paste error in the seed note (the depends_on `#7` is correct;
   the section reference `§7` is wrong — it should read `§5`).

2. **Seed says "liveness ping (timer + optional self-prove goal)"** — dossier §5 elaborates
   this as: (a) host `BackgroundService` timer independent of the GLP scheduler as the
   robust source, and (b) a deeper "engine-internal self-prove-no-op goal that must reduce
   within a bound" to distinguish a live process from a live computation. The seed captures
   both paths; the dossier adds the design rationale (analogous to the bridge-daemon-coordination
   "end-to-end SQL roundtrip" signal).

3. **Scope note "calls restore-and-resume"** — this is intentionally a call-out rather than
   owned by this seed. Dossier §11 #9 (`restore-and-resume-with-link-reestablish`) is the
   separate feature that implements full restore-and-resume. Seed #8 owns: (a) the
   `BackgroundService`/SCM/systemd wrapping, (b) the liveness ping/watchdog, (c) the
   distinguished crash exit code, and (d) the trigger to restart+resume. The RESUME
   behaviour itself is #9's responsibility. This decomposition is correct and matches the
   dossier.

4. **No profile fields filled** (problem/need, target-user, value/outcome, risk) — the
   seed is under-profiled for a `/buildkit-specify` handoff. This is expected at the
   `captured` state but must be filled before the specify gate.

### As-built code confirmation

`out/csharp` and `csharp/glp_link` were searched for:
- `IHostedService`, `BackgroundService`, `sd_notify`, `ServiceBase`, `WindowsService`,
  `Environment.Exit`, heartbeat, watchdog, liveness, WATCHDOG, READY=1
- `SaveSnapshot`, `LoadLatestSnapshot`, `RestoreAndResume`, `restore_and_resume`
- `Process.GetCurrentProcess`, `AppDomain.CurrentDomain`

All searches return zero matches (confirmed absent from `out/csharp` and `csharp/glp_link`).

The single liveness-related hit in `out/csharp` is:

```
out/csharp/lib/engine/glp_engine.cs:178-181
/// Feature 025 (Option B): how long the inbound-pump driver blocks for the next
/// link frame before giving up and reporting the run as suspended. A liveness
/// tuning knob, not a correctness bound.
public TimeSpan InboundPumpWait { get; set; } = TimeSpan.FromSeconds(30);
```

This is a per-link timeout, explicitly labelled "not a correctness bound" — not OS liveness.

The single liveness hit in `csharp/glp_link` is:

```
csharp/glp_link/seam/LinkOptions.cs:39-41
/// Bounded silence after which the sublayer surfaces tempFail (FR-045, SC-010).
/// A liveness tuning knob, not a correctness bound.
public TimeSpan TempFailAfter { get; init; } = TimeSpan.FromSeconds(5);
```

Again a per-link timeout, not OS liveness.

**Dossier claim confirmed:** zero OS liveness/crash/watchdog infrastructure exists in `out/csharp`
or `csharp/glp_link`. The whole capability is net-new.

The composition root at `out/csharp/glp_repl/Program.cs:24-38` is the thin `Task Main` entry
point that delegates to `GlpRuntime.Repl.Program.Main(args)`. It has no
`BackgroundService`/`IHostedService` wrapping and no crash exit-code logic — consistent with
the dossier claim.

---

## Classification check

**Kind: MVP** — correct. This is genuinely user-visible OS-integration work: the feature
delivers a supervised-restart-capable host, which directly satisfies the §5 success theme
(durable long-running engine).

**net-new classification** — confirmed: `out/csharp/glp_repl/Program.cs:24-38` (the current
entry-point) has no `BackgroundService`/`IHostedService` wrapping, no `sd_notify`, no SCM
integration, no crash exit-code logic, and no heartbeat timer. Substrate is identified in
§0.4: `Microsoft.Extensions.Hosting BackgroundService`; `sd_notify` (Linux); Windows SCM.

**Scope supported?** Yes. The scope "BackgroundService/Windows-Service/systemd host: liveness
ping, crash exit code, supervised restart calling restore-and-resume" is entirely net-new
implementation above the engine library (FR-057: in the host/exe layer, not the engine
library). The engine does not acquire this capability; the host wrapping does.

**Dependency on #7 correct?** Yes. The restart action calls restore-and-resume, which depends
on a persistence API that does not exist today. Seed #7 (`engine-state-snapshot-and-persistence-api`)
must deliver the snapshot API before #8's restart action has anything to restore.

---

## Tensions

### T1: liveness-ping scope creep vs. clean MVP boundary

**Summary:** The "optional self-prove goal" liveness check (a GLP no-op goal that must
reduce within a bound) is a non-trivial addition. It couples the host liveness mechanism
to the GLP scheduler and to the engine's computation health — well beyond a simple OS
heartbeat.

**Evidence:** Dossier §5 distinguishes "host BackgroundService timer (independent of the GLP
scheduler) — robust source for OS liveness" from "engine-internal self-prove goal — deeper
signal distinguishing live process from live computation." The `InboundPumpWait` at
`glp_engine.cs:178-181` = 30s default is the only engine-side tuning knob related to idling.
There is no existing mechanism for the engine to run a self-directed no-op probe.

**Options:**
1. **MVP scope = host timer only.** Deliver only the `BackgroundService` timer-based liveness
   (sd_notify WATCHDOG=1 / SCM ping). The self-prove goal is deferred to a follow-up. Cleanest
   MVP boundary.
2. **Both in scope.** Implement both the timer and the engine-internal self-prove-goal, with a
   configurable enable flag. Higher cost; risks scope creep.
3. **Timer first, engine probe as a separate subsequent task within the same feature.** Ship
   timer path first; only add the engine-probe path if the feature is still in flight and cost
   permits.

### T2: crash exit code — what constitutes "unrecoverable state"?

**Summary:** The dossier states "a distinguished non-zero exit code on unrecoverable state so
the supervisor restarts it. Recoverable faults stay GLP terms; only cannot-live-with state
exits." But "unrecoverable" is not defined. The fault lattice
(`self.glp:451`: `ok ; closed(LinkId,Reason) ; tempFail(LinkId,Reason) ; permFail(LinkId,Reason)`)
handles link faults as GLP terms — these do NOT escalate to OS. What does escalate?

**Evidence:** `out/csharp/lib/engine/glp_engine.cs:362-364` catches all `Exception` and
returns `ExecutionResult(Failed, error: e.ToString())` — errors are absorbed as GLP-level
failures, not propagated to the OS. There is no documented "cannot-live-with" criterion.
`csharp/glp_link/seam/LinkOptions.cs:48` (`PermFailAfter = 30s`) stays GLP-side.

**Options:**
1. **Explicit list.** Enumerate exactly which C# exception types or conditions are
   unrecoverable (e.g. heap corruption, snapshot write failure, catastrophic out-of-memory)
   vs. GLP-level failures. Define this list in the spec before building.
2. **Generic catch-all.** Any unhandled exception from the `BackgroundService.ExecuteAsync`
   loop becomes a crash exit. Simpler; risks false positives.
3. **Keep all errors GLP-term.** Do not crash-exit; rely on an external watchdog timer
   (timeout-based restart). Avoids defining unrecoverable; requires external liveness timeout.

### T3: FR-057 placement of the restart/resume trigger

**Summary:** FR-057 states `glp_link → out/csharp` only; the engine library NEVER references
the link layer. Restart triggers link re-establishment (via `GetOrEstablish`/`WireEstablishedLink`
at `csharp/glp_link/primitives/LinkEstablish.cs:51`). Where exactly does the restart/resume
driver live — in the composition root, in a new engine resume-hook, or in `BackgroundService`?

**Evidence:** Dossier §10.7 fork: Opt 1 = top-level supervisor/composition root drives resume
(engine exposes heap-snapshot only; link re-establish above it, evidence `Program.cs:30-35`
and FR-057); Opt 2 = new engine resume-hook seam analogous to `rt.InboundPump`
(`runtime.cs:129`). This fork is NOT settled (§10.7 `settled = false`).

**Options:**
1. **Composition root (§10.7 Opt 1).** `BackgroundService.OnStart` calls: load snapshot from
   #7 API → re-register kernels → restore heap → re-establish links via
   `LinkEstablish.WireEstablishedLink` → resume drain. All link-touching code stays in the
   host. Clean FR-057 compliance.
2. **Engine resume-hook (§10.7 Opt 2).** Add a `ResumeFromSnapshot(blob)` method on
   `GlpEngine` that includes a hook for link re-establishment (injected callback), keeping the
   resume logic co-located with the heap.
3. **Staged.** Composition root for MVP (Opt 1); refactor to engine resume-hook only if the
   composition root becomes too large/complex in #9.

---

## Under-specifications

### U1: No "unrecoverable state" taxonomy

**Question:** What C# exception types / runtime conditions constitute "unrecoverable state"
that should produce a non-zero exit code vs. GLP-level failures that stay as `ExecutionResult`?

**Why it matters:** Without this, either the `BackgroundService` catches everything and never
crashes (defeating the restart mechanism) or it crashes on recoverable errors (triggering
unnecessary restarts). The supervisor (systemd / SCM) needs a reliable signal.

**Options:**
A. Enumerate a closed set (heap OOM, snapshot write failure, failed CancellationToken on
   shutdown) before building.
B. Unhandled exception from `BackgroundService.ExecuteAsync` = crash (open-ended; risks
   false positives from transient errors).
C. No crash-exit at all in MVP; use watchdog timer timeout only.

### U2: Cross-platform liveness mechanism selection

**Question:** Is the target platform Windows only, or must liveness work on Linux (systemd)
and cross-platform (fallback heartbeat file/socket)? Which mechanisms are in scope for this
feature vs. later?

**Why it matters:** `sd_notify WATCHDOG=1` (Linux), Windows SCM heartbeat, and a
portable heartbeat-file fallback are three different implementations. The code to select
between them (platform detection + conditional compilation or runtime dispatch) adds scope.

**Options:**
A. Windows-only (`Microsoft.Extensions.Hosting` Windows Service + SCM) for MVP; add
   Linux/cross-platform in a follow-up.
B. Cross-platform from day one: `Microsoft.Extensions.Hosting` with both
   `UseWindowsService()` and `UseSystemd()` and a fallback timer.
C. Portable heartbeat-file/socket only (no platform-native mechanisms), deferred to
   platform integration as a follow-up.

### U3: Self-prove goal — no GLP program defined for it

**Question:** If the optional self-prove goal is in scope, what GLP predicate does the host
call as the liveness probe, and where does it live?

**Why it matters:** The probe must reduce within a bounded cycle count. There is no existing
"no-op-that-must-reduce" predicate in `programs/self.glp` (the prelude). Defining one is
GLP language work (must follow the language-authority rule in CLAUDE.md §Language Authority).

**Options:**
A. Define a new system predicate `_liveness_probe/0` in `programs/self.glp` (language work,
   needs approval per DISCIPLINE.md §1.14).
B. Use an existing trivially-reducible goal (e.g. `true.` or `1 =:= 1`) as the probe. No
   new predicate needed; may not exercise enough of the engine.
C. Skip the self-prove goal in this feature; cover it as part of a monitoring/observability
   follow-up.

### U4: Kill-and-restart correctness test boundary

**Question:** Seed #8 is described as triggering restore-and-resume (which seed #9 owns).
But seed #8 must itself have a correctness test. What does #8's test cover vs. what is left
to #9?

**Why it matters:** If #8's test reaches into heap restore + link re-establish (both owned by
#9), there is a test-boundary overlap. Without clarity, #8 may ship untestable.

**Options:**
A. #8 tests only: process starts under `BackgroundService`; liveness pings are sent/logged;
   a crash produces the expected exit code; supervisor restarts the process. No heap/link
   correctness in #8's test.
B. #8 tests include a minimal restart round-trip (no goals in flight — empty state restart),
   deferring the link-reestablish correctness to #9.
C. Merge the correctness test entirely into #9; #8 has only unit tests for the host wrapper.

---

## GEPA/DSPy refinement

### Applicability: methodological

This seed is entirely OS/C# host integration work. There is no LM-generated program being
optimized, no codegen DSPy pipeline, and no text-to-code DSPy module that GEPA would
directly optimize. Applicability is `methodological` — GEPA/DSPy serves as the
iterate-against-a-metric discipline: host design → candidate implementation → evaluate
against the metric combination → reflect/mutate → repeat.

### Seed definition

A C# `BackgroundService` / Windows Service / systemd host wrapper for `GlpEngine` that:
(a) sends OS liveness signals on a timer (sd_notify WATCHDOG=1 on Linux; SCM heartbeat on
Windows; optionally exercises an engine self-prove goal as a deeper health signal);
(b) on unrecoverable exception emits a distinguished non-zero exit code that the OS
supervisor interprets as a restart request;
(c) on supervisor-triggered restart, calls the restore-and-resume API delivered by seed #7
(heap snapshot reload + link re-establishment via `LinkEstablish.WireEstablishedLink`),
re-establishes monitored links from persisted `LinkId` definitions, and resumes the
GLP drain.

### Metrics combination

| Name | Kind | Tool / Harness | Threshold |
|---|---|---|---|
| Host wraps engine without leaking link-layer refs into engine lib | pragmatic | `dotnet build` + `dotnet test` on engine library in isolation (FR-057 check: no `GlpLink` reference from `glp_runtime_net.csproj`) | zero cross-references; build clean |
| OS liveness signal delivered on schedule | pragmatic | Integration test: spawn host process → assert sd_notify/SCM heartbeat fires within 2× the configured interval; kill host → assert restart within supervisor timeout | 100% of test scenarios in a repeatable local integration harness |
| Crash exit code is non-zero and supervisor restarts | pragmatic | `dotnet test` with a `BackgroundService` that injects a fault → assert `Environment.ExitCode != 0`; integration test: supervisor (mock) observes restart count | exit code check 100%; restart observed in all injected-fault cases |
| Restart restores engine and resumes drain correctly | pragmatic | Kill-and-restart correctness test: run a suspending goal → crash host → restart → verify goal eventually resolves from restored snapshot | pass for all smoke scenarios; goal-resolution before-and-after identical |
| Liveness-ping does not interfere with GLP committed-choice concurrency | pragmatic (Shapiro criteria) | existing REPL test suite (`test/run_all_tests.sh`) passes unchanged after host wrapping | 384/384 REPL tests green |
| FR-057 preserved (engine library has no link-layer refs) | formal (type/dependency) | `csproj` reference graph check + `dotnet build --no-dependencies` on engine library | zero `GlpLink` assembly references in `glp_runtime_net` build output |
| Crash-exit invariant: unrecoverable state defined and exhaustive | formal (lightweight) | SMT/Z3 check on the exception-type taxonomy: for each C# exception type, exactly one of {crash-exit, GLP-result-failed, retry} is reachable; no gap | Z3 UNSAT on the negation (all cases covered) — or a human-readable exhaustiveness proof in the spec |

Formal note: this seed does not touch the GLP language, grammar, or bytecode wire format.
No mechanized GLP semantics proof or IL verification (MLIR/TWAM) is required. The formal
metric is the FR-057 dependency check (a type-system / csproj-reference property, statically
verifiable) plus the exception-taxonomy completeness check (lightweight SMT). Neither
requires Lean or Rocq.

### Interactive spec step

At the start of `/buildkit-specify` for this seed, the owner confirms:

1. **Platform scope:** Windows only, cross-platform, or Linux-primary for MVP? (U2)
2. **Unrecoverable state taxonomy:** closed set enumerated in spec, or generic unhandled
   exception catch? (U1, T2)
3. **Self-prove goal:** in scope for this feature or deferred? (U3)
4. **FR-057 placement of restart/resume driver:** composition root (§10.7 Opt 1) or new
   engine resume-hook (§10.7 Opt 2)? (T3)
5. **Metric confirmation:** owner confirms the pragmatic + FR-057-formal + exception-taxonomy
   metric set above, or adjusts.

### Refinement loop (Claude-run, no API)

Epoch structure:

1. **Seed → candidate design doc.** Claude produces a host-architecture design (class diagram:
   `GlpEngineHost : BackgroundService`, `ILivenessStrategy` (Windows/Linux/file), crash-exit
   discriminator, restart/resume call sequence).
2. **Evaluate against metrics.** (a) Does the design preserve FR-057 (no `GlpLink` refs in
   engine lib)? Check `csproj` reference graph. (b) Is the exception taxonomy closed? Check
   by exhaustive case analysis. (c) Does the restart sequence call `SaveSnapshot`/`LoadLatestSnapshot`
   from #7 API correctly? Verify against #7's contract.
3. **GEPA reflective mutation.** If FR-057 violated: move link re-establish code up to
   composition root. If exception taxonomy incomplete: extend the discriminator. If restart
   sequence incorrect: realign with #9 boundary.
4. **Candidate implementation.** Claude generates the `BackgroundService` class, liveness timer,
   crash-exit discriminator, and the restart/resume call site.
5. **Run metrics.** `dotnet build` (FR-057 check); REPL test suite (Shapiro criteria);
   kill-and-restart integration test (pragmatic correctness).
6. **Terminate** when all metric thresholds hold AND roadmap-sequence fit holds (depends_on #7
   delivered; #9 unblocked).

---

## Formal tooling

### Lean 4 vs Rocq evaluation

**Lean 4 fit:** Poor fit for this seed. The deliverable is a C# `BackgroundService` + OS
integration, not a mathematical structure or type-theoretic property. Lean 4's strength is
mechanized proofs of semantic properties (type safety, operational semantics). The only
formal property this seed introduces is the FR-057 dependency invariant (a csproj-reference
graph property) and the exception-taxonomy completeness. Neither is a theorem suited to Lean 4
tactics. A Lean 4 proof of "no `GlpLink` reference exists in `glp_runtime_net`" would be an
overcomplicated encoding of a `dotnet build` graph check.

**Rocq fit:** Same conclusion. Rocq is a better fit for compiler-correctness and operational
semantics proofs (Vellvm, verified WAM). The exception-taxonomy completeness might in principle
be stated as a Rocq inductive type exhaustiveness property, but the effort is disproportionate
to the semantic content — it is a finite enumeration checked trivially by code review or Z3.

**Primary:** `n/a` — this seed has no mechanized proof requirement. The formal checks are
(a) a static dependency graph check (tooling: `dotnet build` reference analysis) and (b) an
exception-taxonomy completeness argument (lightweight Z3 or code-review enumeration). No ITP
(Lean/Rocq) is appropriate here.

**Alternative when:** n/a. If a future version of this seed introduces a formal liveness
property (e.g. "the engine resumes all suspended goals within K steps after restart" — a
quantitative liveness theorem), then Lean 4 with Iris/Trillium (concurrent/distributed
separation logic) would become appropriate. That is not in the current MVP scope.

**IL verification:** n/a — this seed does not touch the GLP bytecode format, the wire codec,
or the IL. No MLIR dialect / byte-parity / round-trip verification applies.

---

## Shapiro criteria preserved

This seed must preserve the following original GLP/Shapiro design criteria, framed for the
embedded-switch purpose:

1. **Committed-choice concurrency.** The host `BackgroundService` timer fires on a separate
   thread. It MUST NOT touch the GLP heap or scheduler directly. All GLP execution remains
   single-owner/single-threaded (`heap_fcp.cs:136-141`). The timer only signals the OS
   supervisor (sd_notify / SCM); the engine's drain loop is not interrupted mid-reduction.
   The liveness probe (if the self-prove goal option is chosen) must be submitted to the
   engine's goal queue, not injected mid-reduction.

2. **SRSW (Single-Reader/Single-Writer).** Restart and resume must restore heap `Cells` +
   `Hp` atomically at a quiescence boundary (between reductions, never mid-reduction). A
   partial-snapshot resume would violate SRSW by re-introducing a writer–writer or
   reader–writer aliasing that the original execution would never have produced. The
   quiescence boundary (`glp_engine.cs:545 DrainAsyncWithStatus`) is the only correct
   snapshot point.

3. **Suspension correctness.** After restart, suspended goals must re-suspend on the same
   reader heap addresses they were suspended on before the crash. The restart must rebuild
   the `Suspended` index (from the persisted snapshot delivered by #7) before resuming the
   drain, so that when a writer is later bound, `heap_fcp.cs:730-742`'s activation walk
   finds the correct goal set.

4. **Monotone variable binding.** The crash-exit and restart path must not allow a variable
   that was bound in the pre-crash state to appear unbound post-restart. The heap snapshot
   is the mechanism that preserves this: bound cells in `Cells` are restored verbatim. Any
   partial restore (e.g. restoring only the goal queue but not the heap) would violate
   monotone binding.

5. **Three-valued unification (embedded-switch framing).** The embedded-switch role — routing
   external connectivity events and internal OS actions — means that a restarted engine must
   re-attach to the same `LinkId` rendezvous definitions. The `GetOrEstablish`/`WireEstablishedLink`
   re-establishment path (`LinkEstablish.cs:51`) preserves this: replaying a persisted `LinkId`
   yields an indistinguishable fresh link, so the GLP unification state (In/Out/Faults cursor
   heap addresses) is consistent before the drain resumes.

---

## Recommendation

**Alignment: aligned.** The seed matches the dossier §5 scope and classification. Kind = MVP
is correct (OS-visible, user-observable durability). Depends_on #7 is correct. The §ref
should be §5 not §7 (minor note error in seed storage).

Recommended owner action for this seed before `/buildkit-specify`:

1. Resolve T2/U1 (unrecoverable state taxonomy) and T3/U2 (platform scope and FR-057
   placement) — these are the two blockers for a well-specified `buildkit-specify` pass.
2. Decide U3 (self-prove goal) — keep it deferred from this MVP or include.
3. Fill the missing profile fields (problem/need, target-user, value/outcome, risk) in the
   roadmap seed.
4. Confirm the metrics combination at the start of `/buildkit-specify` (the interactive spec
   step above).

For the MVP, §8.2 advisory recommendation applies: deliver seed #8 as part of Slice B only
after Slice A (#6) is shipped. The liveness/crash/restart machinery should not gate the
process-split MVP.

---

## Options for owner

| Label | Consequence |
|---|---|
| Accept MVP scope as-is (host timer + crash exit code + restart trigger, no self-prove goal) | Smallest deliverable; self-prove goal deferred; liveness is process-level only |
| Add self-prove goal in scope | Deeper health signal; requires new GLP predicate (language authority approval needed per DISCIPLINE.md §1.14) |
| Windows-only platform for MVP | Simpler codebase; Linux/cross-platform deferred |
| Cross-platform from day one | More initial work; eliminates a future migration |
| Composition root owns restart/resume (§10.7 Opt 1) | FR-057-clean; restart logic in host layer |
| New engine resume-hook (§10.7 Opt 2) | Co-locates resume with heap; adds engine surface |

---

## Open questions

1. Is the § reference in the stored seed note (`§7 #8`) a typo for `§5 #8` — should it be
   corrected in the roadmap record?

2. What is the minimum viable liveness signal for the embedded-switch use case: OS process
   liveness alone (host timer), computation liveness (self-prove goal), or both? The choice
   determines whether this seed touches the GLP language (language-authority gate).

3. For the restart correctness test (U4): does seed #8's acceptance test cover the full
   kill-and-restart round trip with a suspending goal, or is that entirely seed #9's test?
   The boundary between #8 and #9 needs explicit agreement to avoid a gap in test coverage.

4. The dossier cites the bridge-daemon-coordination "end-to-end SQL roundtrip" liveness
   analogy for the self-prove goal. Is this analogy load-bearing (i.e. should the self-prove
   goal be a GLP query that exercises the same execution path as production goals), or is it
   only illustrative?

5. Does the owner want `UseSystemd()` + `UseWindowsService()` from `Microsoft.Extensions.Hosting`
   as the implementation path (established .NET pattern, low risk), or is there a preference
   for a more lightweight custom approach?

---

## External refs

- [Microsoft.Extensions.Hosting — BackgroundService](https://learn.microsoft.com/en-us/dotnet/core/extensions/background-service)
- [Microsoft.Extensions.Hosting — Windows Service](https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service)
- [Microsoft.Extensions.Hosting — systemd (UseSystemd)](https://learn.microsoft.com/en-us/dotnet/core/extensions/systemd)
- [APOLLO — model-agnostic agentic Lean proving (2505.05758)](https://arxiv.org/abs/2505.05758)
- Dossier §5 (`docs/research/repl-engine-separation/design-dossier.md`)
- Dossier §10.7 (FR-057 / resume driver placement fork)
- `csharp/glp_link/primitives/LinkEstablish.cs:51` (WireEstablishedLink / GetOrEstablish)
- `out/csharp/lib/engine/glp_engine.cs:178-181` (InboundPumpWait — the only existing liveness-adjacent knob)
- `out/csharp/lib/engine/glp_engine.cs:545` (DrainAsyncWithStatus — the quiescence boundary)
- `out/csharp/lib/runtime/runtime.cs:22-152` (GlpRuntimeEngine — full live state to serialize)
- `out/csharp/glp_repl/Program.cs:24-38` (current composition root — the host insertion point)
- `codeconv/src/codeconv/marathon/store.py:96` (MarathonStore — the persistence API template)
