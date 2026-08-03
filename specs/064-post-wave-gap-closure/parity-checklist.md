# Parity checklist — C# link vs the 064 draft contracts (dist-unify, quiescence)

**T002 deliverable (D1/D2 evidence anchor).** Every claim below carries a file:line citation into
the C# reference (`csharp/`, `out/csharp/`). Paths are repo-relative to
`D:\bstdev\research\glp\glpnet\`.

---

## 1. VERDICT — read this first

### 🔴 The C# link does NOT implement distributed unification of non-ground terms. Verdict: **NO.**

The 064 draft `contracts/dist-unify.md` names the C# link as its parity target ("byte-for-byte on
the wire and rule-for-rule in semantics"). **There is no such protocol in the C# link to be at
parity with.** The base link layer is deliberately, loudly **ground-relay only**:

- `csharp/glp_link/primitives/LinkPump.cs:141` (doc of `RecvLoopAsync`): "The decoder rejects any
  embedded variable — the base layer is pure ground-relay (FR-010), so a non-ground payload is a
  wire-contract violation, not something to localize."
- `csharp/glp_link/seam/DefaultPayloadCodec.cs:22-25`: `Decode` passes
  `allocateImportedVar: _ => throw new InvalidOperationException("ground-relay base received a
  non-ground payload (embedded variable)")` — a variable on the wire is a hard error, never an
  import.
- `csharp/glp_link/primitives/LinkEgress.cs:69-88` (`ResolveGround`): an unbound cell at any depth
  throws `"non-ground term reached egress: unbound cell {addr} (ground-relay gate)"` — no
  `_w`/`_r` ever crosses (FR-010).
- Repo-wide search: `VAR_EXPORT`, `DIST_BIND`, `DIST_SUSPEND`, `DIST_FAULT`, `RemoteVarRef` — **zero
  hits** anywhere under `csharp/` or `out/csharp/` (grep over all non-`obj` `.cs`, 2026-08-03).
- The 050 port contract confirms this is by ruling, not omission:
  `specs/050-full-gleam-combined/contracts/link-primitives-port.md` D-4 — "The RULED base K2 is
  **ground-relay** (OQ-3 option (a), R-7): `ground(Msg?)` gate, no globalize, no `_w`/`_r` minting."

**Partial precedent exists, but NOT on the link:** the in-process madGLP layer
(`out/csharp/lib/multiagent/`) has a full variable-export/remote-bind mechanism (global names,
writer/reader halves, imported-reader binding, nested-var re-export — §4 below). It rides
**in-process isolate channels** (`isolate_manager.cs:495-499`: `ctx.OnMessageReady = (dest, msg) =>
config.MainPort.TryWrite(...)`), never `glp_link` frames. The two layers are explicitly not
composed: every link-side decode call refuses `allocateImportedVar`.

### 🔴 The C# link does NOT implement a distributed quiescence oracle. Verdict: **NO** (local-only quiescence exists — **PARTIAL** for single-instance semantics).

The 064 draft `contracts/quiescence.md` names "the C# link's quiescence algorithm (goal-state
census + in-flight accounting at the link seam)" as parity target. **There is no census, no
rounds, no cross-instance protocol.** Search for `census`/`CENSUS_REQ`/`inflight_acked`: zero hits.
What exists is a **single-engine, snapshot-gating** quiescence check:

- `csharp/glp_engine_host/Quiescence.cs:3-4`: "quiescent ⇔ goal queue empty ∧ no reduction in
  flight ∧ client transport drained" — and `:58` `public bool IsQuiescent =>
  _engine.Runtime.Gq.Length == 0;` (the other two conjuncts hold structurally, `:6-15`).
- It gates SNAPSHOT/SHUTDOWN only (`RequestDispatcher.cs:233-256`), never reports to a peer.

**Conclusion the 064 contracts must absorb:** both drafts describe **new protocol design**, not
parity with an existing C# implementation. "Parity target: the C# link" is factually wrong as
written and must be re-scoped (amendments in §6).

---

## 2. Message kinds / wire frames the C# link ACTUALLY uses

### 2.1 Frame layer (the only wire kinds `glp_link` itself defines)

| Kind | Where | Semantics |
|---|---|---|
| `FrameKind.Whole` = 0 | `csharp/glp_link/reliability/FrameCodec.cs:6-9` | whole payload in one frame (under MTU) |
| `FrameKind.Fragment` = 1 | `FrameCodec.cs:11-12` | one fragment of a payload split across frames (FR-022) |

Header (big-endian, 22 bytes, `FrameCodec.cs:54-63`): `Version(1)=0x01, Kind(1), MessageId(u32),
TotalLen(u32), FragIndex(u16), FragCount(u16), ChunkCrc(u32 CRC-32), ChunkLen(u32)`, then chunk.
`MaxPayloadBytes` = 64 MiB pre-allocation guard (`:47-52`). Bad version / bad CRC / inconsistent
header ⇒ `FrameException`, "never a silent mis-decode" (`:127-131`). **That is the entire frame
vocabulary — there is no per-payload "kind" field at the frame layer; semantics live in the payload
codec.**

### 2.2 Payload kinds carried inside frames

| Payload | Where | Semantics |
|---|---|---|
| Default ground-relay term blob | `csharp/glp_link/seam/DefaultPayloadCodec.cs:18-25`; term tags `TagConstant=1, TagVariable=2, TagStruct=3, TagList=4` at `out/csharp/lib/multiagent/payload_serializer.cs:84-88` | one ground GLP term per message (loopback/tcp and any unregistered scheme). Encode via `SerializeAgentMessage` which **throws on any VarRef** (`payload_serializer.cs:506-512`); decode refuses imported vars (§1). |
| Request token `request(LinkId, FromPeer)` | sent: `csharp/glp_link/primitives/LinkRequestKernel.cs:64-69` — "Raw out-of-band frame (messageId 0, NOT via the link Sequencer)"; consumed: `LinkListenKernel.cs:94-111` (`ReadOneGroundMessage`, throws on non-ground) | path-B pre-data handshake; consumed before the data pump engages so data sequence starts at 0 |
| CRDT message envelope | registry row `0x12` `crdt_message`, `csharp/glp_wire_registry/WireRegistry.cs:42-56, 72-87` | feature-041 envelope on a `"quic"` link (via `PayloadCodecRegistry`); still ground-side data, gated by macaroon capability (`LinkPump.cs:172-186` `CapabilityRefusedException` handling) |
| IL program / result envelope | `WireRegistry.cs:44-50` (`0x10 il_program`, `0x11 result_envelope`) | shipped 029/038 kinds (REPL/engine artifacts, not link-primitives traffic) |
| Split-protocol REQUEST/RESPONSE | `csharp/glp_split_protocol/WireProtocol.cs:22-29` (`0x40`/`0x41`), request kinds `LOAD_SOURCE 0x01, RUN_GOAL 0x02, SNAPSHOT 0x03, STATUS 0x04, SHUTDOWN 0x05, PING 0x06` (`:32-51`), response kinds `RESULT 0x81, ACK 0x82, DEFERRED 0x83, PROTOCOL_ERROR 0x84, ENGINE_BUSY 0x85` (`:54-70`) | 061 client↔engine split — control plane, not inter-engine unification |
| WS bootstrap lines | `csharp/glp_link/transports/ConnectBootstrap.cs:25-26` (`"GLPQUICK/1 CONNECT glp-link"` / `"GLPQUICK/1 101 SWITCHING"`) | 036 CONNECT-style bootstrap on a QUIC stream, pre-framing |

### 2.3 In-process madGLP message kinds (NOT on the link wire — cited as the only var-bearing precedent)

| Kind | Where | Semantics |
|---|---|---|
| `MessageType.Assignment` | `out/csharp/lib/multiagent/message_queue.cs:17-23` — "(G := T, destination). G is a global name (`_w(p,i)` or `_r(p,i)`)" | the remote-bind carrier: payload = GlobalName + serialized term (`payload_serializer.cs:193-220`) |
| `MessageType.AgentMessage` | `message_queue.cs:25-29` | structured friend-to-friend term |
| Variable wire form | `payload_serializer.cs:391-427` | `TagVariable` + `GlobalVarId(creator, localId)` + reader flag byte + (writers only) paired-reader local id |
| ReadRequest / Abandon payloads | `payload_serializer.cs:296-359`; **live callers only in** `out/csharp/lib/multiagent/archive-irma-2026-01-30/` (e.g. `irma_agent.cs:128,139`) | the pull-model (read-request) protocol — **archived 2026-01-30**, dead in the live tree |

Delivery transport: `isolate_manager.cs:495-499` — in-process `MainPort.TryWrite`. Flush cadence:
`mad_context.cs:117-141` `FlushMessages()`, called from the engine pump-driver loop
(`out/csharp/lib/engine/glp_engine.cs:619, 773`).

### 2.4 Ordering / reliability sublayer (matches draft dist-unify rule 2)

- Monotone per-link, per-direction sequence: `csharp/glp_link/reliability/LinkSequencer.cs` (whole
  file; seq becomes the frame `MessageId`, `LinkEgress.cs:54,59`).
- FIFO reconstruction + transport dedup: `InboundOrdering.cs:17-75` — at-or-below high-water = "an
  idempotent no-op — the transport-level half of at-least-once redelivery (FR-021/FR-027)" (`:8-10`);
  bounded reorder buffer, overflow throws (`:66-67`).
- Reassembly: `FrameReassembler.cs:36-59` (bounded in-flight partials).
- **No retransmission exists anywhere in `glp_link`** — consistent with draft rule 2's "MUST NOT
  add its own retransmission".
- Backpressure `SendWindow` is allocated per handle (`LinkHandle.cs:90`) but **NOT gated on the
  egress path**: `LinkEgress.cs:56-58` — "SendWindow backpressure (FR-025) is intentionally NOT
  gated here yet — credit release rides the inbound ack path". There is **no ack frame kind** on
  the wire today.
- `FencingRegistry` (epoch fencing of global-name writers, `reliability/FencingRegistry.cs:1-35`)
  is **not instantiated by any production code** (only `glp_link.tests/FencingTests.cs`) — a
  designed-but-unwired defense.

---

## 3. Writer-MGU rules as implemented (`out/csharp/lib/runtime/heap_fcp.cs`)

These are the LOCAL heap rules — the only binding rules the C# reference has; nothing applies them
across a wire (§1).

| Rule | Where | Behaviour |
|---|---|---|
| Only writers bind to values | `heap_fcp.cs:505-512` (`BindWriterWithCallbackControl`) | binding a non-`WrtTag` cell throws |
| Bind wakes suspensions then overwrites in place | `heap_fcp.cs:516-524` | suspensions walked into activations before `cell.Content = value; Tag = ValueTag` |
| Writer→reader chain (var-to-var) | `heap_fcp.cs:560-603` (`BindWriterToReader`) | stores `Pointer(readerAddr)`, tag stays `WrtTag`, suspensions forwarded to the target's writer, OnBind callback relocated (`:596-600`); imported readers are ILLEGAL targets (`:576-583`) |
| Writer-writer is forbidden, always throws | `heap_fcp.cs:606-609` (`BindWriterToWriter`) — "WxW violation ... always throws" | routed via `BindVariable` (`:883-886`) |
| Writer-points-to-writer chain = SRSW violation | `heap_fcp.cs:402-406` | dereference throws `"SRSW violation: writer at X points to writer at Y"` |
| Imported-reader bind (the madGLP remote-arrival path) | `heap_fcp.cs:838-867` (`BindImportedReader`) | a writer-less `RoTag` cell with `VariableEntry` gets a fresh `ValueTag` cell + `Pointer`; suspensions drained from `VariableEntry.Suspensions` |
| Single bind seam for value arrival | `heap_fcp.cs:905-918` (`BindAny`) | unbound imported reader → `BindImportedReader`; everything else → `BindVariable` — "the madGLP assignment ingress MUST route value arrivals through this single seam" (FR-035/SC-009, `:892-903`) |
| Bind observation (export trigger) | `heap_fcp.cs:792-801` (`OnBind`) — fires immediately if already bound; single callback per addr | used by madGLP to fire `global_send` on writer bind: `mad_context.cs:103-107` → `_fireGlobalSendGoalIfExists` (`:159-211`) |
| Nested-var re-export (chained bindings) | `mad_context.cs:183-210` | value is globalized (`GlobalizeTermWithResult`), an `Assignment` is queued, and NEW `global_send` goals + `OnBind` hooks are registered for each nested variable — the incremental-export shape the draft's "chained bindings" scenario assumes |
| Push model, no reader-side subscribe | `mad_context.cs:562-566` — "Only WRITERS get onBind callbacks here. Readers do NOT — the push model" | there is **no remote-suspension registration** anywhere in the live tree (the archived irma ReadRequest was the pull model) |
| Link ingress binds ONLY the link's own In-cursor | `csharp/glp_link/primitives/LinkPump.cs:112-132` | inbound term extends the `In` stream: fresh pair, cons `[value \| reader]`, `BindVariable(cursor, cons)`, reactivate, advance cursor; peer close binds `nil` (`:114-121`). A peer can never name an arbitrary heap cell. |

---

## 4. Quiescence as implemented (all local, engine-host scope)

1. **Definition**: quiescent ⇔ goal queue empty ∧ no reduction in flight ∧ transport drained;
   suspended goals and armed timers are PERMITTED in a quiescent engine
   (`csharp/glp_engine_host/Quiescence.cs:3-5`). Two conjuncts hold structurally because the serve
   loop is strictly sequential (`:7-15`); the check is `Gq.Length == 0` (`:58`).
2. **Use**: gates SNAPSHOT — busy ⇒ `DEFERRED` (0x83) + parked, fires at the next quiescent moment
   checked after every request (`RequestDispatcher.cs:124-137, 233-256`); pending link rewires also
   defer (`:235-239`). SHUTDOWN takes a final snapshot only if quiescent, else "skipped loudly"
   (`:259-288`).
3. **Timer consistency**: `DisarmTimersForCapture` disposes armed timers (waiting out in-flight
   callbacks), records remaining durations, then re-checks `Gq.Length != 0 ||
   rt.PendingTimers != disarmed.Count` — on disagreement it re-arms and returns null (defer, "never
   tear", `Quiescence.cs:82-114`).
4. **Lifecycle state**: `EngineState.Quiescing` exists in the session state machine
   (`EngineSession.cs:13, 32-33`) — serving→snapshotting only when quiescent (FR-014).
5. **Link-aware run loop** (the closest thing to "in-flight accounting at the link seam"): the
   engine drains, then services the pump while `HasPendingOrLive` (`inbox.Count > 0 || liveLinks >
   0`, `LinkPump.cs:91`), re-draining after each applied item; idle `TryApplyNext(wait)` returning
   false breaks the loop, leaving still-open links' readers suspended
   (`out/csharp/lib/engine/glp_engine.cs:613-627, 767-782`). The wait is `InboundPumpWait` — 30 s
   default, 100 ms in the request/response engine host (`glp_engine_host/Program.cs:189-195`).
6. **In-flight counters that exist but are never aggregated or reported**:
   `FrameReassembler.InFlightCount` (`FrameReassembler.cs:102`), `SendWindow.InFlight`
   (`SendWindow.cs:37`), `LinkPump.HasPendingOrLive`. No message kind carries any of them.

---

## 5. Draft-contract cross-check, item by item

### 5.1 `contracts/dist-unify.md`

| Draft item | C# reality | Match? |
|---|---|---|
| Header: "Parity target: the C# link's remote-binding protocol, byte-for-byte" | no remote-binding protocol exists (§1) | **DIFFERS — false premise** |
| `VAR_EXPORT` kind | nothing on the link; nearest precedent is madGLP globalize-on-send, in-process (`mad_context.cs:183-210`; wire form `payload_serializer.cs:391-427`) | **DIFFERS — does not exist** |
| `DIST_BIND {seq, var_id, term}` | nearest precedent: madGLP `Assignment` `(GlobalName := term)` (`message_queue.cs:17-23`), applied via `BindAny` (`heap_fcp.cs:905-918`); not on the link | **DIFFERS — does not exist on the link** |
| `DIST_SUSPEND` | nothing, live tree is push-only (`mad_context.cs:562-566`); pull model only in archived irma (`archive-irma-2026-01-30/`) | **DIFFERS — does not exist** |
| `DIST_FAULT {var_id, reason}` | faults are per-LINK lattice terms `ok\|closed\|tempFail\|permFail` delivered as bound ground terms on monitor streams (`LinkTerms.cs:11-13,156-162`; `LinkFaults.cs:52-59`; malformed payload → `LinkFaultKind.Permanent`, `LinkPump.cs:187-206`) — never var-scoped | **DIFFERS — no var-scoped fault** |
| Rule 1: writer-writer ⇒ fault, link faulted | local heap throws on WxW (`heap_fcp.cs:606-609`); no remote case exists to fault a link over | **PARTIAL — local analogue only** |
| Rule 2: per-link FIFO from existing sequencing/dedup, no own retransmission | matches: `LinkSequencer` + `InboundOrdering` dedup/reorder; no retransmission anywhere (§2.4) | **MATCHES (the one true parity anchor)** |
| Rule 3: reader suspension → one DIST_SUSPEND, local reactivation on bind | no remote suspension; local reactivation machinery exists (`BindImportedReader` drains `VariableEntry.Suspensions`, `heap_fcp.cs:852-858`) | **DIFFERS / PARTIAL** |
| Rule 4: term payloads reuse existing FrameCodec term encoding, byte parity proven | term encoding is `PayloadSerializer` (tags at `payload_serializer.cs:84-88`) inside `FrameCodec` frames — reusable, and it ALREADY has a variable wire form (`:391-427`); but the link-side ground gate rejects it (`DefaultPayloadCodec.cs:22-25`) | **PARTIAL — encoding exists, link refuses it** |
| Rule 5: malformed term / unknown var_id ⇒ DIST_FAULT, never silent drop | matches in spirit: malformed inbound payload/frame ⇒ recorded observable Permanent fault, link kept alive (`LinkPump.cs:156-206`); no var_id concept | **PARTIAL** |
| Parity matrix "equal the single-instance result" | single-instance rules are well-defined (§3) — usable as the semantic oracle | **MATCHES as oracle, not as wire parity** |

### 5.2 `contracts/quiescence.md`

| Draft item | C# reality | Match? |
|---|---|---|
| Header: "Parity target: the C# link's quiescence algorithm (census + in-flight accounting)" | no census, no cross-instance anything (§1, §4) | **DIFFERS — false premise** |
| `CENSUS_REQ`/`CENSUS_REP` with `{running, suspended, inflight_out, inflight_acked}` | no such kinds; counts partially exist un-aggregated (§4.6); `inflight_acked` cannot exist — there are no acks (`LinkEgress.cs:56-58`) | **DIFFERS — does not exist** |
| Counts "snapshotted atomically w.r.t. its scheduler step" | precedent: the dispatcher's strictly-sequential serve loop makes snapshots structurally atomic (`Quiescence.cs:7-15`) | **PARTIAL — local precedent** |
| Verdict rule (running=0 ∧ inflight_out==inflight_acked both ways) | local rule is `Gq.Length == 0` with suspended goals/timers permitted (`Quiescence.cs:54-58`, `:3-5`) | **PARTIAL — local analogue** |
| Fault during round ⇒ terminal `faulted` until re-establish + new round | no rounds; fault lattice + link re-establishment exist (`LinkFaults.cs`; restore-time `RewireHandle.cs:1-26`, `LinkRewirer` gating snapshots `RequestDispatcher.cs:235-239`) | **DIFFERS / PARTIAL** |
| Missing reply within "existing ≤30 s bound" ⇒ faulted, never a hang | the "existing bound" is real: `InboundPumpWait` default 30 s idle break (`Program.cs:189-191`; loop break at `glp_engine.cs:617-618`) — but it yields idle, not a `faulted` verdict | **PARTIAL — bound exists, verdict doesn't** |
| Safety/liveness/fault-honesty properties | untestable against C# — no oracle to compare | **N/A — new design obligations** |

---

## 6. DIVERGENCES — recommended contract amendments

**A-1 (both contracts, headline).** Replace "Parity target: the C# link's …" with an honest scope:
*"The C# reference implements NO distributed unification and NO distributed quiescence
(ground-relay ruling, 050 D-4/FR-010; local snapshot-quiescence only, engine-host FR-014). These
contracts define NEW protocol. C# parity applies only to the layers that exist: FrameCodec framing,
LinkSequencer/InboundOrdering FIFO+dedup, the fault lattice, the local writer-MGU rules (the
single-instance oracle), and the madGLP globalize/localize semantics as the interpretive model."*
Without this, the acceptance matrices demand committed `.out` parity runs against a C# behaviour
that cannot be produced.

**A-2 (dist-unify — message kinds).** Keep the four kinds as NEW payload kinds, but bind them to
the real extension point: a payload-type byte registered in the single-source wire registry
(`WireRegistry.cs:42-56` reserves `0x12+` for messaging kinds; SC-010 uniqueness is
build-checked). State explicitly that the frame layer stays untouched (Whole/Fragment only).

**A-3 (dist-unify — semantics source).** Point the semantic rules at the madGLP precedent instead
of "the C# link": DIST_BIND ≈ `Assignment` applied through the `BindAny` single seam
(`heap_fcp.cs:905-918`); var wire form ≈ `TagVariable` + `GlobalVarId` + reader flag + paired-reader
id (`payload_serializer.cs:391-427`); chained bindings ≈ globalize-spawns-new-goals
(`mad_context.cs:183-210`); export-at-most-once ≈ `GlobalWritersTable` duplicate-entry rejection
(`global_writers_table.cs:232-238`). Decide explicitly whether SRSW-across-the-link reuses the
GlobalName scheme or a new var_id space — the draft's bare `var_id` is neither.

**A-4 (dist-unify rule 1).** Align the writer-writer case with both existing behaviours: locally
WxW **throws** (`heap_fcp.cs:606-609`); the contract's "loud DIST_FAULT(writer_writer), link →
faulted" is a genuinely new remote rule — mark it NEW, and specify it surfaces on the existing
monitor-stream lattice (a `permFail(LinkId, Reason)` carrying the var context in `Reason`, or a new
lattice arm — the latter is GLP-visible surface and needs §1.14 approval; `Fault ::=` is ratified
in `self.glp:451`).

**A-5 (dist-unify rule 3).** The draft's DIST_SUSPEND is a pull-model element with NO live
precedent (live madGLP is push-only, `mad_context.cs:562-566`; pull existed only in archived irma).
Either justify reintroducing pull (cite FCP, §7) or drop DIST_SUSPEND and let the reader side
suspend purely locally (writer side pushes DIST_BIND unconditionally, as madGLP does) — that is the
smaller, precedent-backed design.

**A-6 (dist-unify rule 4).** Reword "byte parity already proven" — what is proven is the GROUND
subset (FR-060/061, `FrameCodec.cs:30-32`). The `TagVariable` encoding exists but has never crossed
the glp_link wire; its cross-runtime byte parity is a NEW obligation for Gleam.

**A-7 (quiescence).** Re-scope as a NEW census protocol layered on real anchors: `running` :=
`Gq.Length` (`Quiescence.cs:58`); `suspended` is informational (suspended goals are PERMITTED in
quiescence, `Quiescence.cs:3-5` — the draft's verdict rule correctly ignores it, keep that);
`inflight_out/inflight_acked` require introducing an ACK/credit-release path that TODAY DOES NOT
EXIST (`LinkEgress.cs:56-58` — SendWindow release unwired). Either add the ack kind to this
contract's scope or redefine link-drain via existing observables (pump inbox empty + egress
cursor at unbound tail). The ≤30 s bounded-silence window should cite `InboundPumpWait`
(`Program.cs:189-191`) as the existing bound it reuses.

**A-8 (quiescence — faulted verdict).** "Any fault-lattice event ⇒ round faulted" is fine, but
specify it reads the EXISTING per-link monitor streams (`LinkFaults.DeliverFault`,
`LinkFaults.cs:52-59`) rather than inventing a parallel fault channel; "re-establish" should cite
the restore-time rewire machinery as the precedent for what re-establishment means
(`RewireHandle.cs:1-26`, snapshot deferral on pending rewires `RequestDispatcher.cs:235-239`).

**A-9 (both).** The parity matrices should say: Gleam↔Gleam and Gleam↔C# **only after** the C# side
grows the same new protocol (a 064+ work item in `csharp/glp_link/`), or shrink the C# column to
the layers that exist today (framing, FIFO/dedup, fault delivery). Committed `.out` files cannot
predate the C# implementation they claim to capture.

---

## 7. FCP Savannah tie-breaker notes (only where C# is ambiguous or silent)

The C# code is UNAMBIGUOUS that no distributed unification exists, so FCP is not a tie-breaker for
"what C# does" — it is the interpretive source for the NEW rules where C# offers two precedents or
none. The Savannah tree is not on this host (sibling-repo asset: `/tmp/FCP/Savannah`, per
CLAUDE.md); the specific questions to settle against it:

- **F-1 (push vs pull — decides A-5).** C# offers BOTH precedents: live madGLP push
  (writer-side `OnBind` → `Assignment`; readers never subscribe, `mad_context.cs:562-566`) and
  archived irma pull (`ReadRequest`/`Abandon`, `archive-irma-2026-01-30/irma_agent.cs:128,139`).
  Check which model FCP's distributed unification actually used before ratifying DIST_SUSPEND.
- **F-2 (var-to-var across the link).** Locally, writer→reader stores a pointer and forwards
  suspensions without binding (`heap_fcp.cs:560-603`), and imported readers are illegal targets of
  writer-to-reader binding (`:576-583`). The draft is silent on a DIST_BIND whose term is itself a
  variable (var-to-var across instances). FCP's handling of remote var-var chains should decide
  whether that is legal wire content or a DIST_FAULT.
- **F-3 (writer-writer remote collision).** Local WxW throws (a programming error,
  `heap_fcp.cs:606-609`); the draft makes the remote case a link-level fault. FCP's treatment of a
  writer-writer encounter during distributed unification is the right authority for
  fault-vs-abort semantics (A-4).
- **F-4 (quiescence).** FCP/Logix distributed termination detection (if any is present in
  Savannah) is the only historical oracle for the census design; the C# tree contributes nothing
  beyond the local FR-014 definition.
