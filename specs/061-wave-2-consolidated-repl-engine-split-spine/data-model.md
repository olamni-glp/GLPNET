# Data Model — Wave 2: REPL Engine Split Spine (061)

Phase 1 output. Entities, fields, relationships, state transitions.

## RequestFrame (client → engine)

TLV frame over FrameCodec. `kind` discriminates:

| kind | payload | notes |
|---|---|---|
| LOAD_SOURCE | UTF-8 program source text | full pipeline runs engine-side |
| RUN_GOAL | UTF-8 goal text | one goal per request (MVP) |
| SNAPSHOT | — | on-demand trigger (FR-014) |
| STATUS | — | engine + restore/pending-snapshot state |
| SHUTDOWN | — | graceful: final snapshot then exit 0 |
| PING | — | supervisor liveness probe |

- `request_id`: uint64, client-monotonic; echoed in the response.
- Validation: unknown kind ⇒ structured protocol-error response; engine stays up (FR-006).

## ResponseFrame (engine → client)

- `request_id`: echo.
- `kind`: RESULT (038 ResultEnvelope bytes, ground-only subset + output blob) |
  ACK (SNAPSHOT/SHUTDOWN/PING: status + seq where relevant) |
  DEFERRED (snapshot parked pending quiescence) |
  PROTOCOL_ERROR | ENGINE_BUSY (restore in progress — FR "hold during restore").
- Invariant: every RequestFrame gets exactly one terminal ResponseFrame or the
  transport drops — the client renders transport failure distinctly (FR-007).

## EngineSession

- `engine_identity`: stable id (store scoping + snapshot seq namespace).
- `state`: `starting | restoring | serving | quiescing | snapshotting | shutting_down`.
- Transitions: starting→(restoring|serving); restoring→serving (only after full
  restore — FR-030); serving→snapshotting (only when quiescent — FR-014);
  snapshotting→serving; serving→shutting_down (graceful = final snapshot).
- One client connection at a time (FR-002); a second accept is refused loudly.

## Snapshot

- `seq`: uint64, monotonic per engine identity (FR-012).
- `created_utc`, `engine_identity`, `format_version`.
- `blob`: versioned byte layout (ByteIo conventions) containing the FR-010 set:
  - heap cells — verbatim addresses (FR-011);
  - goal queue (empty at quiescence, recorded for integrity checks);
  - suspended-goal records + per-goal tables;
  - `_goalId` next-allocation counter;
  - loaded IL units;
  - `_waitReaders` timers as **remaining-duration** entries (FR-015);
  - `InfrastructureGoalIds`, `GlpChannels`;
  - link definitions: LinkId, role (listen/connect), endpoint parameters,
    ingress/egress cursor positions.
- `complete`: a snapshot exists only when blob + manifest are both durable;
  torn write ⇒ not listed (FR-013).

## SnapshotStore (ISnapshotStore)

- Operations: `Write(snapshot) → seq` · `Latest() → snapshot?` ·
  `BySeq(seq) → snapshot?` · `List() → [meta]`.
- Implementations: `PgliteSnapshotStore` (primary — additive tables on the
  single repo cluster, 041 precedent) · `FileSnapshotStore` (fallback —
  atomic write-rename). Fallback engagement is reported loudly (US2/AS-4).

## CrashRecord

- `timestamp_utc`, `engine_identity`, `exit_code?`, `detection` (`exit | ping_timeout`),
  `restart_outcome` (`restored(seq) | unrecoverable(reason)`), `backoff_applied`.
- Queryable history (FR-024); append-only.

## UnrecoverableClassification (DEF-F2 taxonomy)

- `repeated_immediate_crash` (N crashes within window after restore),
  `corrupt_latest_snapshot` (restore fails integrity), `store_unavailable`
  (both backends down), `explicit_poison` (engine self-reported fatal state).
- Effect: supervisor stops restarting, surfaces to operator (FR-023).

## SupervisorConfig

- `ping_interval`, `ping_timeout`, `restart_backoff` (initial/max/multiplier),
  `crash_window`/`crash_threshold` (taxonomy), `store_root`, `engine_binary`.

## RewireHandle (DEF-E1)

- Input: restored LinkHandle definition + restored (possibly bound) In/Out/Faults
  cell addresses + cursor positions.
- Behaviour: registers the handle idempotently, adopts the existing cells
  (no unbound-guard — that guard stays for the normal `WireEstablishedLink`
  path), wires cursors at their restored positions, arms drainer/pump.
- Relationship: sibling of `LinkEstablish.WireEstablishedLink`; both converge
  on the same registry (`LinkRegistry.GetOrEstablish`).

## Relationships

- EngineSession 1—N Snapshot (by engine_identity, ordered by seq).
- Supervisor 1—1 EngineSession (MVP); 1—N CrashRecord.
- Snapshot 1—N link definitions; restore maps each to a RewireHandle.
- ResponseFrame RESULT payloads reuse the 038 ResultEnvelope entity unchanged.
