---
title: "Shared Integration-Test Harness — Design (PRE-IMPLEMENTATION, SPEC-LEVEL)"
subtitle: "Feature 025 multi-protocol-link-layer — the cross-instance test rig the per-transport integration tests target"
date: "2026-06-06"
status: "PLAN-stage. The harness is DESIGNED here, NOT implemented. The link primitives it drives are PROPOSED, pending Gabi's language-authority approval; all exemplar GLP is ILLUSTRATIVE (not runnable yet) and every test below is SPEC-LEVEL (scenario + exemplar GLP + expected observable outcome + pass/fail oracle), made runnable once implementation lands."
---

# 0. Status, framing, and what this document is

This is the **plan-stage design for the shared integration-test harness** that feature
025 needs and that **does not exist today** (verified: there is in-process multi-isolate
machinery — `IsolateManager` + `mad_*` Dart unit tests — but **no harness that spins up
2+ REPL instances and connects them over a transport leaf with fault injection and
byte-identical baseline comparison**; see §1.2).

Hard constraints honored throughout (CLAUDE.md Language Authority; DISCIPLINE §1.14;
DESIGN-DOSSIER §0):

- The base link primitives (`link_setup`/`server_listener`/`client_connector`/
  `request_link`/`accept_link`/`link_send`/`link_recv`/`link_monitor`/`link_close`)
  are **PROPOSED, pending approval, NOT YET IMPLEMENTED**. Every GLP fragment here is
  **ILLUSTRATIVE**, hand-checked for SRSW/modes, and will become runnable only after the
  primitives land. **No runnable test code is written against non-existent primitives.**
- Every test is **SPEC-LEVEL**: a realistic scenario + exemplar GLP + an expected
  **observable** outcome + a pass/fail **oracle**. The harness is the seam that makes
  these runnable later without rewriting the tests.
- **GLP semantics are preserved exactly** and the harness must never tempt a violation:
  SRSW (one reader / one writer per variable per clause, never relaxed by a flag),
  writer-MGU (binds only writers), three-valued unification (an un-arrived remote value
  is an unbound **local reader** ⇒ **Suspend**, never a spurious **Fail**),
  suspend-on-reader / reactivate-on-bind, bind-once, per-link FIFO, three-phase
  HEAD→GUARD→BODY. GLP is **not** Prolog: writer-mode outputs are built in clause **heads**,
  never via `=` in a body.
- **Every link is peer-to-peer to the IMMEDIATE peer.** Any broker/relay (e.g. an MQTT
  broker) is at another level and **OUT OF SCOPE** for the harness; the harness only ever
  stands up two logical link ends and the one transport leaf between them.

Source precedence: **local `docs/`/spec GLP > Shapiro GLP papers > earlier
concurrent-logic papers > external transport RFCs / tooling** (the last used only to
ground the deterministic-transport emulation and the fault taxonomy, never to override a
Tier-1 fact).

---

# 1. Why a new harness, and what it generalizes

## 1.1 Realistic real-world scenario this rig must support (web-grounded)

The headline transports for the MVP are loopback/file (SC-001's "simplest available
transport") and then the **WebSocket** / **HTTP/2** web leaves (spec FR-012). The
real-world shape the harness must reproduce faithfully is a **producer running on one
host streaming values to a consumer on another host over a long-lived full-duplex
WebSocket** — the canonical "arriving out of order or not at all is unacceptable" case
WebSocket exists for (chat, trade confirmations, live game/telemetry state), per
RFC 6455's design rationale ([RFC 6455 §1.1, §1.5](https://www.rfc-editor.org/rfc/rfc6455.html)).
The harness must:

- carry a **bidirectional** stream over **one** logical connection (RFC 6455 §1.2 —
  "each side can, independently from the other, send data at will"), which is exactly how
  the GLP `Link(In, Out)` couples a forward data stream to a reverse credit/back-channel
  stream (DESIGN-DOSSIER §3 KEY INSIGHT);
- preserve **per-link FIFO and reliable in-order delivery** by default (WebSocket inherits
  TCP's in-order reliable delivery, RFC 6455 §1.7), so a *hermetic* test transport must
  default to in-order, lossless, exactly-once delivery — and only **inject** drop/reorder/
  duplicate/delay when a fault is explicitly requested;
- respect **message boundaries** (a GLP frame is one logical message; WebSocket messages
  may fragment into continuation frames in order, RFC 6455 §5.4) so the harness exercises
  the reliability sublayer's fragmentation/reassembly (FR-022) without the GLP program ever
  seeing a partial term;
- model **graceful close** as the WebSocket close handshake (RFC 6455 §5.5.1, close
  control frame + code) ↔ GLP graceful stream-end `[]`, and **abrupt close** as a dropped
  TCP connection ↔ GLP `link_close/1` + a `permFail` on the monitor (DESIGN-DOSSIER §4);
- model **TLS-by-default for inter-host** as the `wss`/`https` scheme variants (RFC 6455
  §3 `wss`, §4.1 TLS-before-data) so FR-029's "plain inter-host refused by default" is a
  testable, deterministic decision in the loopback transport (no real sockets needed).

The deterministic fault taxonomy the harness injects (drop / reorder / duplicate / delay /
partition / peer-kill / clock) is the standard one used by deterministic-simulation and
fault-injection systems: seeded, reproducible network faults (drop, reorder, latency,
partition) plus node-level faults (hang, terminate/restart, clock jitter), as catalogued
by deterministic-simulation harnesses
([Antithesis fault injection](https://antithesis.com/docs/environment/fault_injection/))
and network fault-injection surveys
([NEAT / Toxiproxy-style partition + drop tooling](https://oneuptime.com/blog/post/2026-01-30-network-failure-testing/view)).
The crucial property both sources stress and the harness MUST inherit is **deterministic
reproducibility via a seed**: every fault decision is driven by one seeded PRNG so a
failing run replays byte-for-byte.

## 1.2 What exists today, and the gap (verified)

| Capability | Today | Gap for 025 |
|---|---|---|
| Split one shared writer/reader pair across two runtime instances | YES — in-process, `IsolateManager` routes `NetworkMsg` over `SendPort`s (`glp_runtime/lib/multiagent/isolate_manager.dart:111-243,293-295,374-392`) | not over a real/loopback transport; not 2 REPL processes |
| The two attach seams a transport replaces | YES — outbound `MadContext.onMessageReady` (`mad_context.dart:45,99`), inbound `handleMadAssignment` (`mad_context.dart:229`) | a transport leaf must replace ONLY the `SendPort` routing on these two seams |
| Multi-isolate Dart unit tests | YES — `glp_runtime/test/multiagent/*` (`isolate_manager_test.dart`, `mad_scenarios_test.dart`, …) | unit-level, single process, no fault injection, no baseline byte-diff, Dart-only |
| REPL regression suite | YES — `test/run_all_tests.sh` Sections A–Q incl. M/O multi-isolate | no cross-instance/cross-transport section; no Dart↔C# parity |
| Spin up 2+ REPLs over a transport, inject faults, diff against unsplit baseline | **NO** | **this harness** |
| Same adversarial corpus on BOTH Dart and C# REPLs | **NO** | **this harness (the parity rig)** |

The harness **generalizes the in-process split**: the only thing it changes about the
proven madGLP machinery is the wire under `onMessageReady` / `handleMadAssignment`. The
deterministic **loopback** transport is, in effect, today's `SendPort` route hardened into
a seeded, fault-injectable, FIFO-by-default channel that lives behind the `LinkTransport`
seam (architecture-context §3) instead of behind `IsolateManager`.

---

# 2. Harness architecture (spec-level)

```
                         ┌─────────────────────────────────────────────────────────┐
                         │                 IntegrationHarness                        │
                         │  (one test process; drives the whole rig; owns the seed)  │
                         └─────────────────────────────────────────────────────────┘
                              │ start_instances / open_link / drive / inject / capture / assert_equiv
            ┌─────────────────┴──────────────────┐
            ▼                                     ▼
   ┌──────────────────┐                  ┌──────────────────┐
   │  Instance A      │                  │  Instance B      │
   │  (GLP REPL:      │                  │  (GLP REPL:      │
   │   Dart OR C#)    │                  │   Dart OR C#)    │
   │  role=producer   │                  │  role=consumer   │
   │  AgentId ground  │                  │  AgentId ground  │
   │                  │                  │                  │
   │ onMessageReady ──┼───►┐        ┌────┼─► handleMadAssign│
   │ handleMadAssign ◄┼────┤        ├───◄┼── onMessageReady │
   │  OutputCapture   │    │        │    │  OutputCapture   │
   └──────────────────┘    ▼        ▼    └──────────────────┘
                       ┌─────────────────────────────┐
                       │  LinkTransport (selected by  │   ◄── open_link(scheme)
                       │  scheme):                    │
                       │  • loopback  (DETERMINISTIC, │   ◄── inject(fault) acts HERE
                       │     in-mem, seeded, hermetic)│        (drop/reorder/dup/delay/
                       │  • file      (hermetic)      │         partition); peer-kill acts
                       │  • ws/wss/http2/coap/... (real)        on the Instance (TaskStop)
                       └─────────────────────────────┘
```

Key design choices:

1. **The harness drives REAL REPL instances**, not mocks. An "instance" is one GLP REPL
   (Dart `bin/glp_repl.dart` or the C# mandated-default REPL) booted with a **ground
   `AgentId`** that selects its role in the **one role-parameterized program** (FR-011 —
   never a fork). Dart↔Dart first; either endpoint swappable to C# for the cross-runtime
   parity rig (SC-002/059/062).
2. **Fault injection lives in the transport, not in the GLP layer.** The harness perturbs
   bytes/frames in the loopback transport (drop/reorder/dup/delay/partition) and perturbs
   *instances* for peer-kill — it NEVER reaches into the heap, never relaxes SRSW, never
   forges a binding from the GLP side. A fault is something the **reliability sublayer**
   (the code under test) must absorb or surface as a monitor-stream term — exactly the
   contract under test.
3. **The loopback transport is the deterministic, hermetic default** and is the SC-001
   "simplest available transport". It is FIFO-by-default, lossless-by-default, exactly-once
   by default (mirroring TCP/WebSocket guarantees, RFC 6455 §1.7) and only deviates when a
   fault is injected. All non-determinism is funneled through **one seeded PRNG** so a
   failing case replays deterministically.
4. **Output capture + byte-identical comparison** is first-class: each instance's
   observable output (everything via `'_output'/1` → stdout, plus stderr kept distinct) is
   captured into a normalized transcript; SC-001 asserts the merged split transcript is
   byte-identical to the captured unsplit baseline.
5. **The same harness binary/driver runs the adversarial corpus on BOTH the Dart and the
   C# REPL** and diffs verdicts (SC-007), because runtime-to-runtime parsing differences
   are a known differential-risk class (FR-031).

The harness itself is a **test driver** (the natural home is the Python `codeconv` test
process, or a small Dart/C# test driver invoked by `test/run_all_tests.sh`'s new section —
see §9). It is NOT GLP code and NOT a language item; it is purely below-the-seam tooling.

---

# 3. The harness INTERFACE (the seam per-transport integration tests target)

This is the contract every per-transport integration test calls. It is presented as an
abstract host-language interface (the C# reference signature first, since C# is the
mandated reference per FR-055; a behaviour-identical Dart mirror follows per FR-056). It is
**spec-level**: names/shapes are the proposal the transport authors target; the *bodies*
are built when the primitives land. **Nothing in this interface is a GLP primitive.**

The seven verbs the task names — `start_instances`, `open_link`, `inject`, `drive`,
`capture`, `assert_equiv` — plus the small set of supporting types:

```csharp
// ===== Harness interface (PROPOSED, host-language; NOT GLP). C# reference shape. =====
// A behaviour-identical Dart mirror exists for the Dart↔Dart rig (FR-056).

public interface IIntegrationHarness {

    // ---- lifecycle ----------------------------------------------------------
    // Spin up N>=2 GLP REPL instances of the SAME role-parameterized program,
    // each booted with its own ground AgentId (FR-011). Runtime per instance is
    // Dart or Csharp so the SAME call site builds Dart<->Dart and Dart<->C# rigs.
    InstanceSet StartInstances(ProgramSource program, IReadOnlyList<InstanceSpec> specs, long seed);

    // Open ONE bilateral peer-to-peer link between exactly two instances over the
    // transport named by `scheme` (loopback|file|ws|wss|http2|coap|...). Returns a
    // LinkHandle the test uses for inject()/close(). The two ends are wired into
    // each instance's onMessageReady (out) / handleMadAssignment (in) seams; the
    // GLP program opens its end via the PROPOSED primitives (server_listener /
    // client_connector or request_link / accept_link). IMMEDIATE-peer only: no
    // broker is ever modelled here (out of scope).
    LinkHandle OpenLink(InstanceRef a, InstanceRef b, string scheme, LinkOptions opts);

    // Inject a deterministic fault. For wire faults (drop/reorder/duplicate/delay/
    // partition) the harness perturbs the loopback transport for `link`; for
    // peerKill it terminates `target`'s instance process. All randomness is drawn
    // from the run seed so the fault sequence is reproducible. Returns a token the
    // test may use to heal a partition (clearFault).
    FaultToken Inject(FaultSpec fault);

    // Submit a goal (or boot directive) to an instance's REPL and let event-driven
    // execution run to quiescence-or-deadline. Returns when the instance has
    // suspended (no reducible goals) or the deadline elapses. Quiescence (not a
    // fixed sleep) is the completion signal, so suspend-not-fail is observable.
    DriveResult Drive(InstanceRef inst, GlpGoal goal, Deadline deadline);

    // Snapshot one instance's observable output so far: stdout ('_output'/1),
    // stderr (kept DISTINCT, FR-030/SC-016), and the per-link monitor terms it has
    // read. Output is normalized (see §6) before comparison.
    Capture Capture(InstanceRef inst);

    // Oracle: assert the SPLIT run's merged observable output equals the captured
    // unsplit single-instance BASELINE, byte-for-byte after normalization (SC-001).
    // `mode` selects byte-identical (SC-001/002) or multiset/causal-equivalence
    // (used by reorder/loss SC-012 where only the in-order *result* must match).
    void AssertEquiv(Capture splitMerged, Capture baseline, EquivMode mode);

    // ---- teardown -----------------------------------------------------------
    void CloseLink(LinkHandle link, CloseKind kind);   // graceful([]) | abrupt(link_close)
    void Stop(InstanceSet instances);                  // kills REPLs, asserts GC-to-baseline (SC-014)
}

// ---- supporting value types (PROPOSED) -------------------------------------
public sealed record InstanceSpec(string AgentId, Runtime runtime);     // runtime: Dart | Csharp
public enum   Runtime { Dart, Csharp }
public sealed record LinkOptions(bool InterHost, bool Tls, int? Window); // InterHost+!Tls => refused (FR-029)
public enum   FaultKind { Drop, Reorder, Duplicate, Delay, Partition, PeerKill, ClockJitter }
public sealed record FaultSpec(FaultKind kind, LinkHandle link, FaultParams p); // p carries count/ratio/ms/target
public enum   EquivMode { ByteIdentical, MultisetEqual, CausalInOrder }
public enum   CloseKind { Graceful, Abrupt }
public sealed record Capture(byte[] Stdout, byte[] Stderr, IReadOnlyList<MonitorTerm> Faults);
```

The **transport-author seam** (what a new per-protocol leaf must satisfy so the harness can
drive it) is the already-specified `ILinkTransport` / `ILinkEndpoint`
(architecture-context §3: `open / send-bytes / recv-bytes / close + fault`). The harness's
`OpenLink(scheme=...)` selects a leaf by scheme; a leaf is "harness-ready" when (a) it
implements that seam and (b) for the deterministic-test story it provides an in-memory,
seeded variant (loopback) OR is exercised live with the harness's instance-level faults
only. The fault hooks (drop/reorder/dup/delay/partition) are **only required of the
loopback (and any in-memory) transport** — real leaves (ws/coap/…) are tested for the
**bind-reactivation feasibility** (SC-003) and for graceful/abrupt close, with wire faults
covered hermetically on loopback. This keeps T4 (one platform per leaf, FR-063) cheap.

---

# 4. The deterministic loopback transport (the core enabler)

A faithful, hermetic stand-in for a real bilateral transport. It implements the
`ILinkTransport` seam so the GLP primitives and the reliability sublayer above it are the
**identical code path** used with a real leaf — only the bytes move in-memory.

Default behaviour (no faults) — chosen to match TCP/WebSocket guarantees so a passing
hermetic test means something on a real wire:

- **FIFO, in-order, exactly-once, lossless** per link (RFC 6455 §1.7 inherits TCP). Two
  in-memory ordered queues (A→B, B→A) = the two WebSocket directions (RFC 6455 §1.2).
- **Frame = one logical message.** A `byte[]` frame handed to `SendBytesAsync` arrives whole
  at `RecvBytesAsync` (message-boundary preserving, RFC 6455 §5.4). Fragmentation/reassembly
  (FR-022) is exercised by a `maxFrame` option that forces the sublayer to split/rejoin,
  with the loopback delivering the fragments **in order** (RFC 6455 §5.4: fragments
  delivered in send order) — so the GLP program never sees a partial term.
- **Deterministic scheduling.** Delivery order across the two directions and across multiple
  links is decided by the **single seeded PRNG**; with no faults the schedule is a fixed
  round-robin so runs are repeatable and byte-diffable.
- **Inter-host/TLS modelling without sockets.** `LinkOptions{InterHost=true, Tls=false}`
  ⇒ `OpenLink` is **refused** deterministically (FR-029); `Tls=true` (scheme `wss`/`https`)
  ⇒ succeeds. No certificates needed — the policy decision is what SC-007's "plain
  inter-host refused" asserts.

Fault behaviour (only when `Inject` requests it), all seeded:

| FaultKind | Loopback action | Exercises |
|---|---|---|
| `Drop(ratio\|nth)` | discard selected frame(s) on a direction | FR-020/FR-025; sublayer must redeliver or surface `tempFail` |
| `Reorder(window)` | hold + permute frames within a window before release | FR-018/FR-020/SC-012; reorder buffer must restore order |
| `Duplicate(nth)` | deliver a frame twice (and again after entry removal) | FR-021/SC-008; **the live duplicate-delivery crash must become a no-op** |
| `Delay(ms\|nth)` | hold a frame for a (seeded) interval | SC-010/SC-013; suspend-not-fail; backpressure |
| `Partition(set)` | sever a direction/link until `clearFault` | FR-044/SC-011; split-brain; unmonitored goal stays suspended |
| `PeerKill(target)` | (instance-level) terminate the REPL process | SC-010/SC-014; `tempFail`→`permFail`; GC-to-baseline |
| `ClockJitter` | advance the harness logical clock (give-up timer) | SC-010 bounded-time `permFail` without real wall-clock waits |

Crucially, the loopback transport gives the harness control of the **give-up clock** so
"`tempFail` within a bounded time, then `permFail`" (SC-010) is tested by advancing a
**logical** clock, not by sleeping — keeping the suite fast and deterministic (the spec's
silence-interval is "a tuning parameter, not a correctness condition" — spec Assumptions).

---

# 5. How the harness wires into the proven seams (no GLP-semantics risk)

The harness attaches a transport leaf to **exactly** the two seams the in-process split
already uses, so nothing about unification/suspension/globalize/SRSW changes:

- **Outbound:** the harness sets each instance's `MadContext.onMessageReady`
  (`mad_context.dart:45`) to hand the serialized `OutboundMessage` bytes to the leaf's
  `SendBytesAsync` — replacing the `IsolateManager` `SendPort` route
  (`isolate_manager.dart:293-295`). This is the ONLY outbound change.
- **Inbound:** the leaf's `RecvBytesAsync` frame, after the reliability sublayer's
  dedup/reorder/auth gate, is decoded to `(globalName, value, fromAgent)` and dispatched to
  `MadContext.handleMadAssignment` (`mad_context.dart:229`) — replacing the inbound
  `NetworkMsg` route (`isolate_manager.dart:374-392`). This is the ONLY inbound change.

Because the harness only swaps the wire, every invariant the in-process split already
upholds (writer-MGU, suspend-on-reader, reactivate-once, per-link FIFO) is upheld
identically; the harness's job is to **prove** that under adverse wires, not to provide it.
The harness MUST NOT call `bindWriter`/`bindVariable` directly, MUST NOT inject a binding
that did not originate from a peer's `link_send`, and MUST NOT relax SRSW — any such
shortcut would test the harness, not the link layer.

---

# 6. Output capture and byte-identical comparison (SC-001)

The headline oracle. Design:

1. **Capture surface.** Per instance, the harness tees the REPL's stdout (the sink of
   `'_output'/1`, `self.glp:73`) and stderr into separate in-memory buffers, kept
   **distinct** (FR-030/SC-016). It also records the ordered monitor-stream terms the
   program read (so fault tests can assert the exact `ok`/`tempFail`/`permFail`/`closed`
   sequence — DESIGN-DOSSIER §1 monitor lattice).
2. **Baseline.** Run the program **unsplit** in one instance (no link opened; the shared
   variable stays in one heap) and capture its output → the **baseline transcript**.
3. **Split run.** Run the **same** role-parameterized source split across instances over the
   selected transport; capture each instance's output.
4. **Merge rule.** Observable output in the split run is partitioned by instance. For the
   producer/consumer headline (SC-001) the *consumer* instance is the one that prints, so
   the merged split transcript = the printing instance's stdout. For programs where both
   ends print, the merge is **causal**: outputs are ordered by the per-link FIFO + the
   program's own data dependencies (the same order the unsplit run would produce), which is
   well-defined because there is exactly one logical execution (one program, FR-011). The
   merge rule is fixed per scenario and documented in that scenario's test.
5. **Normalization.** Before diffing, strip instance-local nondeterminism that is NOT
   part of observable GLP output: REPL prompt/banner lines, timing/trace lines, and any
   AgentId-tagged framing the REPL adds. Normalization is a fixed, documented transform
   (the same one for baseline and split) so it cannot mask a real divergence. GLP
   `'_output'` content is **never** normalized.
6. **Oracle.** `AssertEquiv(splitMerged, baseline, ByteIdentical)` — byte-for-byte equal
   ⇒ pass; any difference ⇒ fail with a unified diff. For SC-012 (reorder/loss) the mode is
   `CausalInOrder`/`MultisetEqual`: the *result* must equal the in-order run even though the
   wire delivered out of order; with the sublayer disabled, the oracle instead asserts
   **corruption is detected** (a fault term / clean error), never a silently-wrong transcript.

The Dart↔C# parity gate (SC-002/059/062) reuses this exact oracle: the split run has one
endpoint on each runtime; its merged transcript must be byte-identical to BOTH the unsplit
baseline AND the Dart↔Dart split transcript.

---

# 7. The adversarial-corpus runner (SC-007) — same corpus, both REPLs

A single corpus of crafted **wire inputs** (not GLP programs) plus an expected **verdict**
per input. The runner feeds each input into the reliability sublayer / deserializer of a
running instance via the loopback transport's frame-injection hook, then reads the verdict.

Corpus categories (FR-026..031, Edge "Cyclic/oversized/forged", "Byzantine peer"):

- forged-origin frame (claimed origin ≠ entry's owning peer) → **rejected** (FR-026);
- index-enumeration / cold-call flooding → **quota-bounded**, no unbounded work (FR-028);
- malformed / oversized / cyclic / huge-arity frame → **fail-safe within bounded memory and
  stack** (no OOM, no crash, no isolate kill) (FR-022/FR-028);
- bad-version byte / bad-CRC frame → **rejected** (FR-022);
- replayed frame outside the replay window → **rejected**; in-window redelivery →
  **idempotent no-op** (FR-027 + FR-021);
- relayed-stdio abuse (control sequences, no capability) → **refused/sanitized** (FR-030);
- plain inter-host open → **refused by default** (FR-029).

**Parity requirement (the rig's whole point):** the runner is **runtime-parameterized** and
runs the **identical corpus** on the Dart REPL and the C# REPL, then asserts **verdict-by-
verdict equality** (FR-031/SC-007). A divergence (one runtime crashes where the other
rejects cleanly) is a parity failure, not just a bug — surfaced as a per-input table:

```
input#  category            Dart verdict   C# verdict   parity
  07    huge-arity frame     reject(bounded) reject(bounded)  OK
  12    cyclic term          reject(clean)   CRASH            FAIL  ◄── differential
```

Each verdict also asserts **bounded** resource use (a memory/stack ceiling the harness
enforces around each input) so "fail safe within bounded memory and stack" is measured, not
assumed.

---

# 8. SPEC-LEVEL test catalogue (scenario + exemplar GLP + observable + oracle)

Each entry is the SPEC-LEVEL test the harness will run once the primitives land. Exemplar
GLP is ILLUSTRATIVE and hand-checked for SRSW/modes; it uses the PROPOSED primitives. The
**oracle** is the harness call that decides pass/fail.

> SRSW hand-check convention used below: a writer occurs once (in a head), its reader once,
> UNLESS a ground-implying guard (`ground/1`, `=?=`, `@<`) certifies groundness and thus
> permits multiple reader occurrences (guards-reference "Guards That Imply Groundness").

### T-01 Headline split equivalence (SC-001, US1) — Dart↔Dart then Dart↔C#

Scenario: split `producer(X)/consumer(X?)` across two REPLs over loopback (then file, then
ws), one role-parameterized program (the §0 / example-http-link program with `scheme` =
`"loopback"`). Observable: the consumer prints `42`. Oracle:

```
base   = Drive(unsplit, go_unsplit, d);  bc = Capture(unsplit)          // prints 42
A,B    = StartInstances(prog, [(producer,Dart),(consumer,Dart)], seed)
link   = OpenLink(A, B, "loopback", noFaults)
Drive(A, main(producer), d);  Drive(B, main(consumer), d)
AssertEquiv(merge(Capture(A),Capture(B)), bc, ByteIdentical)            // SC-001 Dart<->Dart
// then repeat StartInstances with [(producer,Dart),(consumer,Csharp)]  // SC-001/SC-002 Dart<->C# gate
```

Illustrative GLP (the role selector; full bodies in example-http-link.md):

```prolog
procedure main(AgentId?).
main(Me) :- Me? =?= producer | demo_link(L), client_connector(L?, Link, Faults), run_producer(Link?, Faults?).
main(Me) :- Me? =?= consumer | demo_link(L), server_listener(L?, Link, Faults), run_consumer(Link?, Faults?).
```

SRSW: `Me` writer→`Me?` reader once under `=?=` (ground-implying); `L` writer→`L?` reader
once; `Link`/`Faults` writers read once each. Clean.

### T-02 Suspend-not-fail / reactivate-exactly-once across the cut (SC-001 AS2, SC-009, FR-017/051)

Scenario: drive the consumer **before** the producer sends. Observable: the consumer goal
is **suspended** (appears in no result as failed; quiescent-suspended), then after the
producer sends, it **reactivates once** and prints `42`. Oracle: `Drive(B,…)` returns
`Suspended` (not `Failed`, not `Deadlock`); after `Drive(A,…)`, `Capture(B)` shows exactly
one `42` and no second reactivation. This is the three-valued guarantee the whole feature
rests on — the harness must distinguish **Suspended** from **Failed** from **Deadlock** in
`DriveResult`.

### T-03 Per-transport bind reactivation, T4 (SC-003, FR-016, US2)

Scenario: for each shipped leaf (`ws`, `wss`, `coap`, …), `OpenLink(scheme)`, carry one
writer→reader bind, assert the suspended reader reactivates once — on at least one platform
(Windows OR Android). Oracle: same as T-02 but over the real leaf; a leaf is "shipped" only
when this passes. Wire faults are NOT required here (covered hermetically on loopback);
only feasibility + graceful/abrupt close.

### T-04 TLS-by-default (SC-001 AS3 / FR-029)

Scenario: open an **inter-host** link with `Tls=false` → refused; with `Tls=true`
(`wss`/`https`) → succeeds. Oracle: `OpenLink(InterHost=true,Tls=false)` throws
`LinkRefused`; `OpenLink(InterHost=true,Tls=true)` returns a usable link that passes T-01.
On loopback this is a deterministic policy decision (no real certs; §4).

### T-05 Idempotent redelivery is a verified no-op (SC-008, FR-021)

Scenario: `Inject(Duplicate(nth=1))` then a third delivery after entry removal. Observable:
exactly one `42`, no crash, no error printed, no second reactivation. Oracle: `Capture(B)`
identical to the no-fault T-01 capture; harness asserts **no exception/StateError** crossed
the instance boundary (today the second delivery throws — `mad_context.dart:330,377`;
`heap_fcp.dart:365`). This is the sharpest regression gate: the harness must observe the
**absence** of the live crash.

### T-06 Reorder / loss recovery (SC-012, FR-020/018)

Scenario: stream `[10,20,30]` with `Inject(Reorder(window=3))` and `Inject(Drop(nth=2))`.
Observable (sublayer ON): consumer prints `10 20 30` in order = the in-order run. Oracle:
`AssertEquiv(Capture(B), inorderBaseline, CausalInOrder)`. Sublayer-OFF variant: oracle
asserts **corruption detected** (a fault term or clean error), never a wrong transcript.

Illustrative producer (stream, ground-relay; graceful close via `[]`):

```prolog
procedure produce(Stream(Integer)?, Stream(Integer)).
produce([V|Vs], [V?|Out?]) :- ground(V?) | produce(Vs?, Out).
produce([], []).
```
SRSW: `V` writer→`V?` reader twice but both under `ground(V?)` (ground-implying) — legal;
`Vs`/`Out` thread once each. Clean.

### T-07 Fault liveness on peer-kill (SC-010, FR-044/046, US4)

Scenario: consumer suspended on an un-arrived value; `Inject(PeerKill(producer))`;
`Inject(ClockJitter)` to advance the give-up timer. Observable: the consumer's **data** goal
does **not** fail; the monitor stream yields `tempFail(LinkId,_)` within bounded (logical)
time, then `permFail(LinkId,_)` on give-up; a fault-guarded clause becomes reducible; an
**unmonitored** consumer stays suspended. Oracle: `Capture(B).Faults` = `[tempFail(L,_),
permFail(L,_)]` in order; `DriveResult` for the data goal is `Suspended`, never `Failed`.

Illustrative monitor reader (existing guards only — faults are DATA, FR-043):

```prolog
procedure on_fault(FaultStream?).
on_fault([permFail(L, R)|_]) :- ground(L?) | handle_perm(L?, R?).
on_fault([tempFail(L, R)|_]) :- ground(L?) | handle_temp(L?, R?).
on_fault([ok|Rest])          :- on_fault(Rest?).
```
SRSW: `L`/`R` writers from the head list cell, read once each under `ground(L?)`; `Rest`
once. Clean. No fourth verdict — `on_fault` is ordinary stream consumption.

### T-08 Split-brain defense (SC-011, FR-047)

Scenario: two writers (one stale, one fenced/reconnected) deliver different values for one
global name; harness `Inject(Partition)` then heals and reconnects the fenced writer.
Observable: exactly one value wins (by epoch/fence), the loser yields `permFail`, no silent
overwrite, no crash, no downstream double-reduction. Oracle: the bound value equals the
winning epoch's value AND `Capture` shows exactly one `permFail` for the loser AND the
consumer printed the winning value exactly once.

### T-09 Backpressure bound (SC-013, FR-025)

Scenario: fast producer, stalled consumer (`Inject(Delay)` on the consumer direction).
Observable: the producer **suspends** (outbound queue bounded), no OOM, and an independent
second link is **not** head-of-line blocked. Oracle: harness asserts the producer's
`DriveResult` reaches `Suspended` with the outbound queue depth ≤ the window, the process
memory ceiling is not breached, and a concurrent T-01 on a second link still passes. The
program-visible credit back-channel (DESIGN-DOSSIER §3) is the GLP-level mechanism; below
the seam the bounded queue + delay is what the harness perturbs.

### T-10 Distributed GC (SC-014, FR-024)

Scenario: open then `permFail` N links (`Inject(PeerKill)` ×N). Observable: all per-link
resources (global-name entries, send-registry goals, heap `onBind` callbacks, reply-table
entries) return to baseline, no unreclaimable cycle. Oracle: `Stop(...)` (or a
`SnapshotResources` probe) asserts the post-`permFail` resource census == the pre-open
baseline census.

### T-11 Stream reroute fidelity (SC-016, US3, FR-030)

Scenario: enable stdio reroute under an explicit capability; supply a known input sequence
at the remote end. Observable: the local REPL consumes it as `stdin` and its `stdout`/
`stderr` surface at the remote end, **distinct**, byte-equivalent to the locally-captured
streams; without the capability the request is **refused**; control sequences are
**sanitized**. Oracle: `AssertEquiv` on stdout and on stderr separately; a no-capability
attempt returns `Refused`; an injected control sequence appears sanitized in the relayed
capture.

### T-12 Guard three-valued + SRSW under comparison guards (SC-004/005/006/009)

Scenario (NOT a transport test but it rides this harness's capture/oracle for the
remote-operand cases): the new/changed guards (`@<` family, `atom/1` fix, compound-suspend,
imported-reader) exercised over a **remote** operand carried by the loopback link, so the
same suspend/reactivate-once behaviour is shown across the cut. Observable: succeed on
ground/satisfying; suspend on an unbound (incl. nested-in-compound, incl. imported-reader)
then reactivate once on bind; fail on an unbound writer. Oracle: per-case verdict equals the
expected three-valued outcome; the in-process Section-A/B/C tests (guards.md §1-§4) are the
local mirror, this is the across-the-cut mirror. SRSW positive/negative compile cases stay
in Sections B/C of `run_all_tests.sh`.

### T-13 Adversarial-corpus parity (SC-007) — see §7

Oracle: verdict-by-verdict equality Dart REPL vs C# REPL, every malformed input fail-safe
within bounded memory/stack, plain inter-host refused.

---

# 9. Plugging into the existing REPL suite (FR-067 / SC-017)

The harness adds **one new section** to `test/run_all_tests.sh` (proposed letter **R —
"Cross-Instance Link Integration"**), sequenced AFTER the existing multi-isolate sections
M/O so a failure there is attributable. Design:

- **R is gated on the primitives existing.** Until the PROPOSED primitives land, Section R
  is a **documented skip** (prints `SKIP: link layer not yet implemented (feature 025)`),
  so the baseline stays green and SC-017 holds before any core change. As primitives land,
  individual T-xx tests flip from skip to run.
- **R runs the hermetic loopback tests by default** (fast, deterministic, no network), and
  the real-leaf tests (T-03) behind an opt-in env flag / per-platform guard (T4: Windows OR
  Android) so CI on one platform is not blocked by a leaf feasible only on the other
  (FR-063/064).
- **The Dart↔C# parity rig (T-01 Dart↔C#, T-13)** is a separate invocation the harness
  driver runs when both REPLs are available; it is the **release gate** (FR-062) and is NOT
  on the default fast path (it needs the C# REPL built).
- **FR-067/SC-017 standing assertion:** the WHOLE suite (Sections A–R) must be green before
  and after every core-touching change (heap bind/guard, runner guard evaluator, SRSW
  analyzer, parser), and `self.glp` (incl. any `=\=`-gated arithmetic) must still load. The
  harness MUST NOT mutate `self.glp` or any prelude.

The harness driver itself lives in a clobber-safe home (architecture-context §2.3 /
FR-057): the C# reference driver under `linklayer/csharp/test/`, the Dart mirror under
`linklayer/dart/test/` — NEITHER under `out/csharp` nor `glp_runtime_net`, so a codeconv
regen cannot overwrite it. The Python `codeconv` test process may orchestrate the two-REPL
spawn (it already owns cross-process orchestration), or a thin Dart/C# driver invoked by
Section R does — that choice is OQ-H1 below.

---

# 10. Determinism, seeds, and reproducibility (the property that makes failures actionable)

- **One seed per run** drives every nondeterministic choice: cross-direction delivery
  interleaving, fault selection (which frame to drop/dup/reorder, partition timing), and the
  logical give-up clock. A failing run is reported with its seed and replays byte-for-byte
  (the deterministic-simulation discipline, Antithesis / seeded-PRNG network simulators).
- **No real wall-clock sleeps** in hermetic tests: time is the harness's **logical** clock
  advanced by `ClockJitter`/`Drive` deadlines, so "within a bounded time" (SC-010) is tested
  in microseconds and is reproducible.
- **Quiescence, not sleep, is the completion signal.** `Drive` returns when the instance has
  no reducible goals (suspended) or the deadline elapses — so `Suspended` vs `Failed` vs
  `Deadlock` is observable and the suspend-not-fail invariant is directly assertable.
- **The seed sweep** (run the corpus under N seeds) is the cheap way to surface
  order-dependent reliability bugs (reorder/dup races) without nondeterministic flakiness:
  each seed is itself deterministic.

---

# 11. What the harness must NEVER do (semantic-faithfulness guardrails)

- NEVER bind a heap cell directly, forge a binding, or call `bindWriter`/`bindVariable`/
  `bindImportedReader` from the harness — bindings cross the cut ONLY via a peer's
  `link_send` through the two seams (§5). The harness perturbs the **wire**, not the heap.
- NEVER relax SRSW, never add a second reader/writer, never pass a `skipSRSW`-style flag
  (no such flag exists; inventing one is forbidden).
- NEVER convert a disconnect/non-arrival into a logical **Fail** — the harness asserts the
  opposite (Suspend + monitor-stream fault term). A test that "passes" by observing a Fail
  on disconnect is itself a defect.
- NEVER put a `_w`/`_r` placeholder or an embedded reader on the wire in a base-layer test
  (the base is ground-relay, FR-010; open-structure transport is `glink`, out of scope).
- NEVER mutate `self.glp` / the prelude (FR-067).
- NEVER model a broker as a logical participant — the harness only ever stands up two
  immediate peers and one transport leaf between them (brokers are another level, out of
  scope).

---

# 12. Open questions (for the gate / eng review)

- **OQ-H1 (driver host).** Does the two-REPL spawn + fault orchestration live in the Python
  `codeconv` test process (reuses existing cross-process orchestration) or in a thin
  Dart/C# driver invoked by Section R? (Recommendation: a small C#-first driver in
  `linklayer/csharp/test/` for the parity rig, with a Python wrapper for `run_all_tests.sh`
  integration.)
- **OQ-H2 (loopback fidelity ceiling).** How faithfully must loopback emulate HTTP/2
  flow-control (`WINDOW_UPDATE`) vs WS socket backpressure for the credit/back-channel
  (DESIGN-DOSSIER OQ-F3)? (Recommendation: model **logical** credits only in loopback;
  byte-window fidelity is a real-leaf concern tested live on T-03/T-09.)
- **OQ-H3 (merge rule for both-ends-print programs).** Confirm the causal merge rule (§6.4)
  is sufficient, or whether such programs are out of the byte-identical oracle and use
  `CausalInOrder` only. (Recommendation: byte-identical for single-printer scenarios;
  `CausalInOrder` otherwise.)
- **OQ-H4 (C# REPL spawn API).** The C# mandated-default REPL's programmatic boot + stdio
  capture surface for `StartInstances`/`Capture` must be confirmed (the Dart side mirrors
  `bin/glp_repl.dart` + the multiagent isolate boot). HOST-interface item, not language.
- **OQ-H5 (resource census probe).** SC-014's GC-to-baseline needs a `SnapshotResources`
  probe into `W_p` / `GlobalSendRegistry` / heap `_bindCallbacks` / reply-table. Confirm
  this read-only probe is acceptable (it touches runtime internals but mutates nothing).
- **OQ-H6 (real-leaf fault coverage).** Confirm wire faults (drop/reorder/dup) are required
  ONLY on loopback, with real leaves covered for feasibility + close only (keeps T4 cheap).
  (Recommendation: yes — hermetic wire-fault coverage on loopback is the contract; real
  leaves prove the seam + reactivation.)

---

# 13. Coverage map — harness capability → SC / FR

| Harness capability | Satisfies |
|---|---|
| `StartInstances` (Dart/Csharp, ground AgentId, one program) | FR-011, SC-001/002, SC-059/062 |
| `OpenLink(scheme)` deterministic loopback + file + real leaves | SC-001 (loopback/file), SC-003 (real), FR-012/013/016 |
| `Inject(Duplicate)` | SC-008, FR-021 |
| `Inject(Reorder)` / `Inject(Drop)` | SC-012, FR-018/020 |
| `Inject(Delay)` + bounded-queue probe | SC-013, FR-025 |
| `Inject(Partition)` + epoch/fence | SC-011, FR-047 |
| `Inject(PeerKill)` + `Inject(ClockJitter)` logical clock | SC-010, FR-044/046 |
| `Capture` (stdout/stderr distinct + monitor terms) + `AssertEquiv` | SC-001, SC-016, FR-060 |
| Adversarial-corpus runner, both REPLs, verdict-diff | SC-007, FR-026..031 |
| Across-the-cut guard three-valued cases | SC-004/005/006/009, FR-017/034/035/037/050/051 |
| `Stop` + resource census | SC-014, FR-024 |
| Section R skip-until-implemented + full-suite-green gate | SC-017, FR-067 |
| GEPA round-trip fidelity metric hook (Agent seams only) | SC-015, FR-065/066 |

---

# 14. Sources

Transport semantics and the deterministic-fault taxonomy were web-grounded; all GLP
semantics, seams, and file:line facts are from the Tier-1 local specs and live code cited
inline (DESIGN-DOSSIER.md, spec.md, contracts/{link-primitives,guards,architecture-context,
example-http-link}.md; `mad_context.dart`, `isolate_manager.dart`, `heap_fcp.dart`,
`self.glp`, `test/run_all_tests.sh`).

- WebSocket Protocol — RFC 6455 (full-duplex over one TCP connection §1.2; framing &
  message boundaries / fragmentation §5.1, §5.4; TCP-inherited in-order reliability §1.7;
  close handshake §5.5.1; `wss`/TLS §3, §4.1; origin/masking security §1.6, §5.3):
  https://www.rfc-editor.org/rfc/rfc6455.html
- WebSocket protocol overview (framing, full-duplex, ordering inherited from TCP):
  https://websocket.org/guides/websocket-protocol/
- Deterministic fault-injection taxonomy (latency, packet loss/drop, congestion+reorder,
  partitions, bad nodes, node hang/termination, clock jitter) and perfect reproducibility:
  https://antithesis.com/docs/environment/fault_injection/
- Network failure / fault-injection testing techniques (partition types, packet drop via
  proxy/tc, deterministic seeded simulation):
  https://oneuptime.com/blog/post/2026-01-30-network-failure-testing/view
