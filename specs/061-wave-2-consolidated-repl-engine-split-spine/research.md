# Research — Wave 2: REPL Engine Split Spine (061)

Phase 0 output. Every Technical-Context unknown resolved as Decision /
Rationale / Alternatives. Guidance rule applied: simplest design that
satisfies the spec; constraints and rejected alternatives explicit.

## D1 — Wire transport & framing: reuse `glp_link` TcpTransport + FrameCodec

- **Decision**: the client↔engine wire is `TcpTransport` (loopback) framed by
  `FrameCodec` (`csharp/glp_link/reliability/FrameCodec.cs`), adding new TLV
  payload types for the request/response frames (see contracts/wire-protocol.md).
- **Rationale**: both are shipped, tested (`csharp/glp_link.tests/`), and
  already carry the link layer's traffic; the roadmap note pins "TCP-loopback
  FrameCodec". One framing stack on the wire, not two.
- **Alternatives rejected**: raw `NetworkStream` + hand framing (reinvents
  FrameCodec); HTTP/gRPC (new dependency, out of proportion for a
  single-client loopback MVP; would bypass the shipped codec conventions).

## D2 — Result payload: shipped 038 envelope, ground-only subset

- **Decision**: responses embed the 038 `ResultEnvelope` bytes
  (`csharp/glp_result_codec/ResultEnvelopeCodec.cs`) with the R6 ground-only
  subset; bindings pre-rendered engine-side; output as the R3 length-prefixed
  UTF-8 blob field.
- **Rationale**: 038 shipped exactly this seam (ED-1) for exactly this split;
  re-encoding results any other way would duplicate a source of truth (VIII).
- **Alternatives rejected**: plain-text responses (loses structure, kills
  SC-001 parity checking); full envelope field set now (DEF-C1 is a recorded
  deferral, not this wave's scope).

## D3 — Host layout: new `csharp/glp_engine_host/` project (R5, ratified)

- **Decision**: a new host project referencing `out/csharp/glp_runtime_net.csproj`;
  the single-process REPL (`out/csharp/glp_repl/`) is untouched (FR-005).
- **Rationale**: owner-ratified R5; clobber-safe additive dirs are the repo's
  satellite convention (029/038/041); forking the runtime would violate IV-b.
- **Alternatives rejected**: `--server-mode` flag on `glp_repl` (explicitly
  rejected by R5); moving the runtime under `csharp/` (churns the codeconv
  output tree for no functional gain).

## D4 — Client: new thin `csharp/glp_repl_client/` (R7, ratified)

- **Decision**: a minimal terminal client with no local `self.glp` context;
  the engine bootstraps the prelude (FR-003). The client distinguishes
  transport failure from goal failure (FR-007).
- **Alternatives rejected**: reusing the REPL binary in a client mode (drags
  the whole runtime into the "thin terminal"; violates R7's intent).

## D5 — Snapshot store: PGLite-primary via the 041 precedent + file fallback

- **Decision**: `ISnapshotStore` with `PgliteSnapshotStore` (Npgsql over the
  repo's single bridge-guarded `.pgdb/` cluster — the exact
  `csharp/glp_crdtmsg/store/PgliteOpWal.cs` pattern, additive tables) and
  `FileSnapshotStore` (gitignored dir, atomic write-rename, JSON manifest +
  binary blob). Monotonic `seq` per engine identity; write is
  blob-then-manifest so a torn write is never listed as complete (FR-013).
- **Rationale**: the roadmap note pins "MarathonStore-shaped: PGLite-primary +
  JSON-fallback, monotonic seq"; 041 proved the C#→PGLite path against the
  same cluster without a second deployment (VI-b).
- **Alternatives rejected**: a per-run cluster like marathon's (VI-b exemption
  exists but is unnecessary here — the engine is repo working state, and the
  bridge cluster is already up); SQLite (new dependency, second store tech).

## D6 — Snapshot blob: 029/038 byte conventions, verbatim cells

- **Decision**: the blob serializes heap cells verbatim (addresses preserved —
  FR-011/DEF-E2) using the established `ByteIo` conventions (LEB128 varints,
  LE int64, varint+UTF-8 strings) with a versioned header; content = the
  FR-010/DEF-D1 field set: heap, goal queue, suspended records, per-goal
  tables, `_goalId` counter, loaded IL units, `_waitReaders` (as remaining-time
  durations — FR-015), `InfrastructureGoalIds`, `GlpChannels`, link
  definitions (LinkIds + listen roles).
- **Rationale**: U-P3/U-P4 resolve to the cheapest correct path: verbatim
  addresses avoid a logical-id relocation layer; the byte conventions already
  have two shipped codecs and golden-test tooling.
- **Alternatives rejected**: logical-id relocation layer (recorded in DEF-E2
  as the fallback if verbatim proves insufficient — revisit trigger stays);
  .NET binary serialization (non-portable, opaque, no golden-parity story).

## D7 — Quiescence detection: scheduler-level, deferred-snapshot on busy

- **Decision**: quiescent ⇔ goal queue empty ∧ no reduction in flight ∧
  client transport drained (clarified FR-014); detection sits over the
  scheduler's drain state (`out/csharp/lib/runtime/scheduler.cs`); a snapshot
  request on a busy engine parks as pending and fires at the next quiescence,
  reporting `deferred` to the requester. Triggers: on-demand + graceful
  shutdown only.
- **Alternatives rejected**: stop-the-world snapshot mid-reduction (violates
  FR-014's consistency demand); periodic snapshots (explicitly out of scope).

## D8 — Supervisor: .NET BackgroundService, host-timer ping only (DEF-F1)

- **Decision**: `csharp/glp_supervisor/` runs the engine as a child process:
  liveness = periodic ping request over the wire + process-exit watch; crash
  → `CrashLog` record (exit code, timestamp) → restart via
  restore-and-resume; restart backoff + `UnrecoverableTaxonomy` (FR-023,
  DEF-F2: repeated immediate-crash-after-restore, corrupt-latest-snapshot,
  store-unavailable) stops the loop loudly. Windows-first (FR-025), contract
  portable. The self-prove GLP liveness goal is NOT implemented — a proposal
  memo to the language authority is a deliverable, nothing more (FR-021).
- **Alternatives rejected**: Windows Service–only packaging (BackgroundService
  hosts as console for tests and as service for deploy — both from one
  binary); implementing the self-prove goal "behind a flag" (violates §1.14).

## D9 — Restore + re-wire: `RewireHandle` beside `LinkEstablish` (DEF-E1)

- **Decision**: restore rebuilds the runtime from the blob, re-arms timers
  with remaining durations, then re-establishes links via
  `LinkRegistry.GetOrEstablish` and a new `RewireHandle` path that ADOPTS
  restored (possibly bound) cells — bypassing only the unbound-cell guards at
  `LinkEstablish.cs:38-43` while reusing the cursor/drainer/pump wiring;
  ingress/egress cursors resume from their snapshotted positions. In-flight
  work past the snapshot is discarded (at-most-once, clarified FR-032).
- **Rationale**: DEF-E1 names exactly this seam and sizes it (~30 lines);
  everything downstream of the address check is reusable because addresses
  are verbatim (D6).
- **Alternatives rejected**: relaxing the guards in `WireEstablishedLink`
  itself (weakens a shipped invariant for the normal path — II forbids
  masking); replaying in-flight requests (deferred, §10.9/DEF-X3).

## D10 — Verification: SPIN + TLA+ + UPPAAL (clarified R15 selection)

- **Decision**: three models under
  `docs/research/repl-engine-separation/models/`, each with the spike-proven
  WSL2 `run.sh` + `run.ps1` harness shape and a RESULT.md verdict:
  - **SPIN** (extends `spikes/spin/front_back.pml`): full wire protocol —
    LOAD/RUN/SNAPSHOT/STATUS/SHUTDOWN + ping, deadlock-freedom, no
    unspecified receptions, `request_eventually_answered` (R14/DEF-A3).
  - **TLA+** (TLC): crash/restore/resume state machine — at-most-once
    committed-stream consistency across crash points (FR-032/SC-002).
  - **UPPAAL** (verifyta): timed automata for ping interval, crash detection
    latency, restart backoff — detect-restart bound behind SC-003.
- **Rationale**: engineer-selected at clarify; each tool covers the property
  class the others model poorly. Tool availability: SPIN 6.5.1 already proven
  in WSL2; TLC (Java) and verifyta install into the same WSL2 distro — a
  tool-versions.txt per model dir records versions like the spike did.
- **Alternatives rejected**: SPIN-only (loses timed + refinement-style state
  properties the engineer explicitly asked to cover).

## D11 — Parity corpus: reuse the REPL test programs for SC-001

- **Decision**: SC-001's "representative program set" = the existing REPL
  suite programs (`test/run_all_tests.sh` Section A subset that runs on the
  C# runtime) executed via the split client, diffing rendered results against
  the single-process REPL output.
- **Alternatives rejected**: authoring a new corpus (duplicates a maintained
  one; VIII).
