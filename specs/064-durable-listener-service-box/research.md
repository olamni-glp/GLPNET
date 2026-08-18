<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Phase 0 Research: durable-listener-service-box (064, gavri variant)

All decisions below were grounded in a code sweep of the live surfaces
(2026-08-03): `out/csharp/glp_repl/Program.cs`, `out/csharp/bin/glp_repl.cs`,
`csharp/glp_link/transports/QuicTransport.cs` + `TcpTransport.cs`,
`csharp/glp_link/primitives/LinkPump.cs` + `LinkSetupKernel.cs` + `LinkEstablish.cs`,
`csharp/glp_crdtmsg/store/` (IOpWal/OpWal/PgliteOpWal/Projection),
`csharp/glp_engine_host/store/` (061 snapshot seam), `programs/tests/quic/quic_chat.glp`.

## R1 — Resume-goal registration lives in a repo-root convention file, read by the shim

- **Decision**: registration = a JSON file `glpservice/resume.json` at the repo
  root, discovered by the same walk-up-from-`AppContext.BaseDirectory` idiom the
  cert dir already uses (`SharedCertMaterial.ResolveCertDir`,
  `SharedCertMaterial.cs:72-85`). Schema in `contracts/resume-registration.md`.
  It is read inside the existing `AfterEngineCreated` closure in the
  hand-authored shim `out/csharp/glp_repl/Program.cs:32-73` — the seam runs
  after prelude load and before the interactive loop (`glp_repl.cs:126`).
- **Rationale**: zero edits to the converted `out/csharp/bin/glp_repl.cs`
  (codeconv discipline guards converted files); absent file ⇒ zero behavior
  change (SC-005); a file (not env var) is inspectable/removable (FR-003) and
  survives reboots; the walk-up idiom is already proven for certs.
- **Alternatives considered**: (a) argv parsing — touches the converted file,
  rejected; (b) env var only — not inspectable/removable as an operator surface,
  rejected as primary (an env override may be added later); (c) `:boot` GLP boot
  files (`BootConfig`) — multi-isolate play machinery, interactive-only, wrong
  shape for a single service registration.

## R2 — Resume execution replays the operator's exact manual sequence, synchronously

- **Decision**: on launch with a registration present, the host performs exactly
  what the operator would type: load the registered program file (the
  `.glp`-load branch semantics of `glp_repl.cs:295-345`), then run the
  registered goal (the goal branch semantics of `:349-378`), before the
  interactive prompt. Failures at either step print a diagnostic naming the
  registration and the cause, then fall through to the normal prompt (FR-009,
  US1 scenario 2). No background-thread goal execution.
- **Rationale**: the engine's runner is single-threaded; running the resume goal
  concurrently with the interactive loop would invent a concurrency model this
  feature does not need. A service box's process is owned by its service goal —
  identical to today's manual operation (the operator's goal occupies the REPL
  the same way). SC-001's 10 s covers launch-to-accepting, not goal completion.
- **Alternatives considered**: background Task dispatch (prompt stays free) —
  rejected: unproven engine thread-safety, races the pump driver; restructuring
  the interactive loop — touches the converted file, rejected.

## R3 — The durable message log reuses the crdtmsg op-journal seam (IOpWal), PGlite-backed

- **Decision**: persistence = the existing append-only, idempotent, dot-keyed op
  journal `csharp/glp_crdtmsg/store/IOpWal.cs` with its two shipped backends —
  `PgliteOpWal` (primary; the repo's single `.pgdb/` cluster, conn from
  `COLAB_PG_CONN`) and `OpWal` (file fallback) — composed primary-then-loud-
  degrade like the 061 snapshot store. The REPL composition root wires a durable
  WAL where today's chat path is memory-only.
- **Rationale**: it is the crdtmsg-domain journal already in the assembly the
  REPL references; `Append` is idempotent (dot-keyed — exactly-once for SC-002
  falls out of the CRDT dedup), `Ops` is ordered, `Projection` already rebuilds
  state from the journal. Zero new storage seams; Constitution VI-b holds (the
  one `.pgdb/` cluster, no second cluster).
- **Alternatives considered**: (a) 061 `ISnapshotBackend` — blob/snapshot-shaped
  (one seq per engine identity, `Latest()`-centric, fork guard assumes one
  writer), bound to `glp_engine_host` not the REPL; usable but a worse fit —
  rejected; (b) a brand-new log table/seam — violates reuse-first, rejected;
  (c) GLP-side store_put/2/store_get/2 — the language-surface variant,
  explicitly rejected at intake (FR-006).

## R4 — Append point: once-per-delivered-term on the runner thread (LinkPump.TryApplyNext)

- **Decision**: `LinkPump` gains one additive, optional host hook — a delivery
  observer invoked in `TryApplyNext` immediately before the heap bind of each
  data item (`LinkPump.cs:124-132`), receiving the `LinkId` + decoded ground
  term. The composition root (shim) registers an appender that writes the WAL
  entry; durability precedes the bind, satisfying FR-004's crash-after-receipt
  guarantee. No hook registered ⇒ behavior byte-identical.
- **Rationale**: `TryApplyNext` is post-reassembly, post-ordering, post-dedup and
  runs on the runner thread — exactly once per message the program observes, in
  order. The alternative codec-decorator observes on the background receive
  thread, pre-dedup — wrong ordering/duplication semantics for SC-002.
- **Alternatives considered**: decorating `IPayloadCodec` at the composition
  root (no glp_link edit) — rejected on semantics (pre-ordering, background
  thread); patching the engine driver loop — touches converted engine code,
  rejected.

## R5 — Replay = seed the service's inbound stream from the WAL before live traffic

- **Decision**: on resume, after the program loads and before the listener goal
  arms, the host replays the WAL's ordered ops through the same delivery shape
  the program sees live (history first, live appends after — US2 scenario 3).
  Replayed items are NOT re-appended (replay reads, never writes — idempotence,
  US2 scenario 4). The concrete mechanism (a replay source feeding the goal's
  inbound stream ahead of link traffic) is pinned in
  `contracts/message-log-and-replay.md`; for the chat MVP the crdtmsg
  `Projection` provides the rebuilt state surface.
- **Rationale**: the program must not distinguish replay from live (US2), and
  replay-before-arm avoids interleaving replayed and live items.
- **Alternatives considered**: replay as plain host-side printout (history
  visible to the operator but not to the program) — fails US2's "service logic
  sees every message"; re-delivering through a live loopback link — heavier,
  invents infrastructure.

## R6 — QUIC connect-retry: port the TCP loop verbatim (role-order independence)

- **Decision**: `QuicTransport.ConnectAsync` (`QuicTransport.cs:147-175`, today
  single-shot, no catch) gains the same `while (true)` + catch-refused +
  `Task.Delay(100, ct)` back-off loop as `TcpTransport.cs:57-74` ("listener not
  up yet — back off and retry"), bounded by the kernel's existing 120 s connect
  ct (`LinkSetupKernel.cs:62-63`).
- **Rationale**: FR-008/US3 is literally TCP parity; the ct plumbing already
  exists, making this a near-drop-in port. Also closes the known cold-start
  window this fleet just debugged on the Gleam side (D-9 item 2).
- **Alternatives considered**: retry at the kernel layer (covers all transports)
  — larger blast radius mid-feature; deferred, noted for a future cross-
  transport refactor.

## R7 — Listener re-accept scope (MVP boundary)

- **Decision**: MVP keeps the existing one-accept-per-goal listener semantics
  (`QuicTransport.ListenAsync` accepts one connection per arm; the GLP service
  goal re-arms per session as today's chat does). The multi-accept listener
  (`CreateListenerAsync`/`QuicListenerHandle`, present but unreachable from the
  kernel path) is OUT of 064 — it is the roadmap's separate
  `multi-accept-transport-extension` feature.
- **Rationale**: restart-survival (this feature) is orthogonal to multi-accept;
  scope discipline keeps 064 shippable.
