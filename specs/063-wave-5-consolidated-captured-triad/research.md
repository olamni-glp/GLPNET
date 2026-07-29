# Phase 0 Research — 063 wave-5 consolidated captured triad

Decisions resolving every open technical question. Sources: the 2026-07-02 036
fidelity-audit record (via the roadmap profile), the operator's intake brief
(`docs/roadmap-intake/durable-mesh-messaging-protocol.md`), the spec-025 link
contracts, and the constitution.

## R1 — US1 acceptance baseline is the audit record, not the current code

- **Decision**: US1's three gaps are exactly the audit findings: (a) the
  `--repl` flag is accepted but the live REPL process-I/O bridge is inert
  (envelope-only today); (b) the dup-id mesh eviction bug recorded at
  `Program.cs:253`; (c) the C# host dll is not built in-tree, so 9 integration
  tests skip. Acceptance = the three reversed, proven by scenario/test.
- **Rationale**: spec-first — the audit is the recorded defect source. Today's
  `Mesh.Remove` already carries an eviction guard comment of unverified
  provenance; the regression scenario decides whether the symptom is truly
  gone (Bug-Protocol: verify, then fix if it reproduces — never assume).
- **Alternatives**: trusting the in-code comment (rejected — code is never
  the source of truth); re-auditing from scratch (rejected — the recorded
  finding is the baseline).

## R2 — Live REPL bridge rides the spec-025 link-message interface

- **Decision**: complete the `--repl` path by bridging the glp_repl process's
  stdio to the link's message envelopes (one REPL line/result per message),
  reusing the exact link-message interface the envelope relay already uses.
  No new wire format; no REPL changes.
- **Rationale**: the audit names this interface as the intended completion;
  the envelope path is proven by the shipped rig.
- **Alternatives**: a bespoke REPL protocol (rejected — new wire surface for
  no requirement); embedding the engine in-process (rejected — 036's tool is
  a host, and the REPL exe is the shipped artifact).

## R3 — Signal/fetch carriage is transport-agnostic over the link layer

- **Decision**: (engineer-accepted in clarify) US2's signal, fetch, and
  friend-lookup messages are ground terms/JSON payloads carried over any
  spec-025 link transport; TCP evidence acceptable, QUIC leg exercised after
  US1 lands. Control (signal) and data (fetch) share the link; no separate
  control plane in the first hop.
- **Rationale**: decouples US1/US2 for the operator-directed parallel run;
  the link layer's ordering/fault guarantees are already proven.
- **Alternatives**: QUIC-only (rejected — couples US2 to US1); separate
  control/data split (deferred — a multi-hop concern, out of first-hop scope).

## R4 — WAL + message-file policy (per the intake brief, verbatim scope)

- **Decision**: append-only WAL records acceptance order and delivery state;
  content placement by configurable target file size — small messages share a
  message file, ~file-size messages get their own file, larger messages split
  across files. Recovery = WAL replay; a dense per-sender sequence
  (no gaps) is asserted at recovery and on fetch; a gap is a named loss event.
- **Rationale**: the intake brief specifies exactly this ("lose nothing",
  dense fully-serializable sequence, file policy by size).
- **Alternatives**: store-only durability without WAL (rejected — the brief
  names the WAL as the reliability core); per-message files always (rejected —
  the brief's shared-file policy exists for small-message efficiency).

## R5 — Sequence/metadata hot tier in the repo PGlite cluster (`msmesh` schema)

- **Decision**: per-message metadata (ids, mailbox/topic, per-sender seq,
  retention class, delivery/dedup state, DLQ reasons, friend cache) lives in
  a new additive `msmesh` schema in `<repo>/.pgdb/`, reached through the
  shared `codeconv.bridge_client` bridge; migration `0011_msmesh_schema.py`
  advances the single head 0010 → 0011.
- **Rationale**: constitution VI-a/VI-b — one working-data cluster, additive
  single-head migrations, shared bridge infrastructure.
- **Alternatives**: a per-tool standalone store (rejected — a second
  working-data cluster violates VI-b); sqlite (rejected — the intake brief
  names the PGlite→DuckLake tiering explicitly).

## R6 — DuckLake aging tier behind a seam, with a PGlite-only fallback

- **Decision**: periodic migration of aged metadata (≈ >1 day, configurable)
  from PGlite to DuckLake (DuckDB-over-parquet via the Python `duckdb`
  package) under a gitignored `ms_message/.data/lake/` dir; catch-up queries
  span hot+lake. The lake sits behind a narrow `lake.py` seam; if the
  dependency misbehaves on a host, the seam degrades LOUDLY to PGlite-only
  (a named warning, never silent) — the SC-004 drill remains valid because
  the drill window is < 1 day.
- **Rationale**: the intake brief names the tiering; the seam+fallback keeps
  the wave shippable if the lake dependency fights the host.
- **Alternatives**: lake-first (rejected — hot-path queries belong in the
  transactional tier); skipping the lake entirely (rejected — it is in the
  brief; the seam keeps it honest without betting the wave on it).

## R7 — Dedup + exactly-once observation

- **Decision**: message identity = (sender station id, per-sender seq); the
  recipient records the per-sender high-water mark + a sparse seen-set in
  `msmesh`, surviving restart; fetch is idempotent from any offset
  (Kafka-style consumer-side position).
- **Rationale**: dense sequences make the high-water mark sufficient almost
  always; the sparse set covers out-of-order replica fetches later.
- **Alternatives**: content-hash dedup (rejected — duplicate content is
  legitimate; identity is positional per the brief).

## R8 — Friend-lookup + DLQ minimal shapes

- **Decision**: friend-lookup is one request/response pair over an existing
  link ("do you know station X?" → address | unknown), consulting only the
  local known-hosts registry; no transitive search in the first hop. A target
  unresolvable after direct + friend lookup goes to the DLQ table with a
  reason; DLQ entries are listable and re-driveable from the CLI.
- **Rationale**: the brief scopes friend-lookup to "just enough to build test
  scenarios"; transitive search is the future separated service.
- **Alternatives**: recursive lookup with TTL (deferred — multi-hop era).

## R9 — US3 operationalization (no new runtime)

- **Decision**: US3 delivers (a) the formal protocol doc
  `docs/three-role-orchestration/PROTOCOL.md`, distilled from the recorded
  method-and-dogfood doc and the installed capability's contract; (b) two
  recorded engagements run through the installed 3-role capability on real
  wave-5 gates (the plan-review of this wave, and the US1 mesh-fix code
  review); (c) closure evidence linked into the roadmap item.
- **Rationale**: the migration already landed in the toolchain; the roadmap
  record scopes this feature to formalize-and-operationalize.
- **Alternatives**: GLP-native triads (rejected by the roadmap record);
  writing a new orchestration tool (rejected — duplicate of the installed
  capability).

## R10 — What is wave-4-dependent (FR-015 scan result)

- **Decision**: nothing currently in scope consumes wave-4 output: US1/US2
  touch csharp/glp_quick/ms_message surfaces; wave-4's §1.14 language pair
  and ZMQ base primitives are disjoint. The one watchpoint: if US2's QUIC leg
  wants the ZMQ transport as additional evidence, that task is sequenced last
  and board-flagged.
- **Rationale**: FR-015 requires the scan and the discipline, not drama.
