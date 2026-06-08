# Architecture Context — Multi-Protocol Peer-to-Peer Link Layer (Feature 025)

**Status:** PLAN-stage architecture facet. This document pins the **substrate** the
language primitives bind to; it proposes **no** GLP language primitives. Every
signature, term vocabulary, or core edit that *would* be a language extension is
flagged "PROPOSED — pending Gabi's language-authority approval" (CLAUDE.md
Language Authority; DISCIPLINE §1.14). It is coherent with, and subordinate to,
the binding contract: **"Decisions — RULED (Gabi, 2026-06-06)"** at the end of
`docs/research/multi-protocol-link-layer/B2-B3-G-decision.md`.

**Source precedence used throughout:** Tier-1 local specs / live code > Tier-2
Shapiro GLP/CGLP papers > Tier-3 (FCP/Logix/Oz/ISO). Tier-3 is mechanism
inspiration only; it never overrides a Tier-1 fact. Live-code citations are
`file:line` against the working tree at branch `025-multi-protocol-link-layer`.

---

## 0. What this facet fixes (and what it deliberately does not)

This is the **architecture context the primitive signatures must be coherent
with**, so the gate proposal is grounded in a real seam and a real runtime API.
It pins five things:

1. The **clobber-safe C#-first reference home** (RULED B3 / FR-055/056/057).
2. The uniform **`LinkTransport` seam** (FR-058).
3. The **reliability sublayer** that sits between the seam and the language
   primitives (FR-020..025, FR-060/061), and the live-code crash it must fix.
4. The **failure model** as data on a per-link monitor stream (FR-043..047).
5. The **cross-runtime parity gate** (FR-059/062).

It does **not** fix primitive names/arities/modes (FR-001 reserves those for the
language-authority gate). It grounds them: every proposed seam composes with the
real `Channel(In,Out)` type and the real `send`/`receive`/`new_channel` idioms in
`programs/self.glp:15,90-97`, and attaches to the two real runtime seams in
`mad_context.dart` (outbound `onMessageReady`, inbound `handleMadAssignment`).

---

## 1. The existing in-process substrate the link layer generalizes (grounded)

The link layer is **not greenfield**. madGLP already splits one shared
writer/reader pair across two runtime instances **in process**; this feature
generalizes the *transport* under that split while preserving every GLP invariant.

**The two narrow seams (the only attach points):**

- **Outbound seam** — `MadContext.onMessageReady` (`MessageDeliveryCallback`,
  `glp_runtime/lib/multiagent/mad_context.dart:19,45`), drained by
  `flushMessages()` (`mad_context.dart:87-105`). Every cross-instance assignment
  is an `OutboundMessage{destination, type, payload:List<int>}` appended to `mp`
  and pushed through this callback. Today it is wired to an in-process isolate
  coordinator (`isolate_manager.dart:293-295`, `ctx.onMessageReady = (dest,msg)
  => mainPort.send(NetworkMsg(...))`). **A transport replaces only this callback.**
- **Inbound seam** — `MadContext.handleMadAssignment({globalName, value,
  fromAgent})` (`mad_context.dart:229-246`). A transport's receive side decodes a
  wire frame back into `(GlobalName, Term, fromAgent)` and calls this one method.

Everything between those seams — globalize/localize (`mad_helpers.dart`), the
global writers table `W_p` (`global_writers_table.dart`), the `global_send`
registry, writer-binding (`heap_fcp.dart bindVariable:671`), and suspension
reactivation — is transport-agnostic and **must be preserved bit-for-bit** by any
link-layer scheme (corpus 07 §1).

**Open-structure transparency is already in the substrate (corpus 14).** The
madGLP `global_send`/globalize path guards on `known/1` (not `ground/1`,
`self.glp` guard family at `:39`), recurses over the whole term
(`mad_context.dart _extractTermVarsRecursive:180-196`), and mints a fresh global
name `_w(p,i)`/`_r(p,i)` per embedded variable on each hop. So a "ground-only"
transport (BLE GATT, fixed CoAP) can still carry an *open* term: openness is
encoded as **ground global-name placeholders + sibling sub-links**, never raw
unbound cells on the wire. **Architecture implication:** the new transport seam
sits on the globalize/localize seam (the `known/1` path), NOT on the ground-only
`_send_to_ui`/`ground/1` gate — wiring it to the ground gate would silently
collapse the link to ground RPC and lose stream/reply-variable transparency.

**Channel idiom the primitives must compose with (`programs/self.glp:15,90-97`):**

```prolog
% TYPE (self.glp:15)
Channel(In, Out) ::= ch(In, Out?).

% PROCEDURES (self.glp:90-97) — load-bearing idiom the link primitives mirror
procedure new_channel(Channel(X, Y), Channel(Y, X)).
new_channel(ch(Xs?, Ys), ch(Ys?, Xs)).

procedure send(X?, Channel(Y, Stream(X))?, Channel(Y, Stream(X))).
send(X, ch(In, [X?|Out?]), ch(In?, Out)).

procedure receive(X, Channel(Stream(X), Y)?, Channel(Stream(X), Y)).
receive(X?, ch([X|In], Out?), ch(In?, Out)).
```

A link is the cross-instance replacement for the shared `Channel` variable: any
proposed link-open primitive (gate item D-LA-1) SHOULD yield a `Channel(In,Out)`
so existing `send`/`receive` clauses compose unchanged above the seam — outputs
constructed in clause **heads** (writer-mode), never via `=` in the body
(`send/3`'s `[X?|Out?]` is the head writer-construction idiom). This is the
"replace the shared variable with a link, program logic unchanged" requirement
of FR-006/FR-010/FR-059.

---

## 2. C#-first reference placement + the clobber-safe home (RULED B3; FR-055/056/057)

### 2.1 What RULED inverts

The decision-doc *body* recommended Dart source-of-truth → codeconv → C#. The
**RULED** section inverts this for feature 025 (Gabi's language-authority call):
author the base link primitives + guards + failure model + reliability sublayer
**in C# FIRST** (`out/csharp/`, the mandated-default REPL); the Dart mirror is
authored **only after** the C# reference works fully and passes acceptance
(FR-055/056). `payload_serializer.cs` is already byte-parity with Dart (Assumption
in spec; verified present at `out/csharp/lib/multiagent/payload_serializer.cs`,
git-tracked).

### 2.2 Why a codeconv regen CAN reach the obvious C# locations (the hazard)

The clobber risk is **real and verified**, not hypothetical:

- **`out/csharp/lib/**/*.cs` is COMMITTED but is codeconv's *output*.** The
  `dart_csharp` langpair mirrors the Dart tree **verbatim** — one `.cs` per
  source `.dart`, directory structure preserved
  (`codeconv/src/codeconv/langpairs/dart_csharp/target_csharp.py:8-9,41-42`,
  "`lib/runtime/heap_fcp.dart` -> `lib/runtime/heap_fcp.cs`"). The mirror source
  is the Dart tree (`mirror_dart.py:85`, "the source is mirrored", empty target
  suffix = verbatim). So `codeconv mirror` / `codeconv scaffold` /
  `codeconv codegen` regenerate every `.cs` under `out/csharp/lib/**` whose path
  maps to a Dart source path. `out/csharp/lib/multiagent/mad_context.cs` and
  `payload_serializer.cs` are exactly such regenerated outputs (git-tracked
  product, but still regen targets). **Any hand-authored `.cs` placed at a path
  that maps to a Dart source file under the mirrored tree will be overwritten by
  the next regen** — that is the FR-057 hazard.
- **`glp_runtime_net/` is gitignored generated scratch** (`.gitignore:38-39`,
  "regenerable; do not commit"). Authoring there is worse: clobbered AND lost.

### 2.3 PROPOSED clobber-safe home (FR-057, decision D-B3-4)

**PROPOSED — pending Gabi approval:** author the hand-written C# reference
(language-layer primitives, guards, failure model, reliability sublayer, and the
non-regenerable transport leaves) in a **new top-level hand-authored C# package
that has NO Dart source counterpart** and is therefore **never a codeconv mirror
target**, e.g.:

```
linklayer/csharp/                 ← hand-authored, committed, NOT under out/csharp or glp_runtime_net
  GlpLinkLayer.csproj             ← references out/csharp's product assembly (payload_serializer.cs etc.)
  reliability/                    ← sequence/dedup, reorder buffer, framing, epoch/fence, GC
  transport/                      ← per-scheme LinkTransport leaves (native, per-platform)
  monitor/                        ← per-link monitor-stream plumbing
  link/                           ← base link primitives' C# bodies
linklayer/dart/                   ← the LATER Dart mirror (authored after C# reference works)
```

**Why a regen cannot reach it:** codeconv's mirror/scaffold/codegen walk is keyed
to the **Dart source tree** — it emits `.cs` only for paths that exist as `.dart`
under `glp_runtime/lib/**` (or `bin/`), mapped 1:1 by `target_csharp.py`. A path
like `linklayer/csharp/reliability/SequenceDedup.cs` has **no `.dart` preimage**,
so no mirror step ever names it as an output; it is invisible to the regen
oracle. (Contrast: putting it at `out/csharp/lib/multiagent/link_router.cs` would
require a `glp_runtime/lib/multiagent/link_router.dart` preimage, which the next
mirror/codegen would author/overwrite.) The package references the build-gated
`out/csharp` product assembly so it reuses the already-byte-parity
`PayloadSerializer`, `GlobalWritersTable`, `MadContext` rather than forking them.

**Coherence note for the gate:** the *base primitives and reliability sublayer*
are pure heap/value logic and are codeconv's high-fidelity bucket, so a later
Dart mirror is feasible; but during 025 they are **hand-authored C# first** per
RULED, hence the clobber-safe home. The **transport leaves** are codeconv's
escalate-don't-guess worst case (heavy async/native) and are FR-058-explicitly
"NOT required to be auto-converted" — they live natively in `linklayer/csharp/
transport/` permanently. The `out/csharp` tree (the REPL) stays the
codeconv-regenerated runtime; `linklayer/csharp` is the hand-authored link
overlay that depends on it.

> **Open question (gate):** confirm the exact package name/root for the
> clobber-safe home and whether it links the `out/csharp` product as a project
> reference or a built assembly reference. Either satisfies FR-057; the choice is
> a build-layout decision for Gabi.

---

## 3. The uniform `LinkTransport` seam (FR-058)

One uniform per-scheme interface — **open / send-bytes / recv-bytes / close +
fault** — behind which every per-protocol leaf lives, selected by scheme. The
leaves may be per-platform / native and are **not** auto-converted (FR-058);
acceptance is one of Windows OR Android per leaf (FR-063/T4). The seam is the one
contract both runtimes share, and it carries the *already byte-parity*
`PayloadSerializer` blob as an **opaque `byte[]`/`Uint8List`** — it knows nothing
about terms, globalize/localize, or SRSW (those live above it in `MadContext`).

**PROPOSED — host-API seam (NOT a GLP language primitive; this is the C#/Dart
runtime interface, not a guard/kernel/directive):**

```csharp
// linklayer/csharp/transport/ILinkTransport.cs  (PROPOSED; host interface, not GLP)
public interface ILinkTransport {
    // Establishment role is INDEPENDENT of data direction (FR-004):
    // a server-listener end may be the writer end and vice versa.
    Task<ILinkEndpoint> ListenAsync(LinkScheme scheme, LinkAddress local, LinkOptions opts);   // server-listener
    Task<ILinkEndpoint> ConnectAsync(LinkScheme scheme, LinkAddress remote, LinkOptions opts); // client-connector
}

public interface ILinkEndpoint {
    LinkId   Id            { get; }                 // stable, never-reused link identity (FR-007 idempotent setup)
    Task     SendBytesAsync(byte[] frame);          // one self-delimiting frame (corpus 13: opaque blob)
    Task<byte[]> RecvBytesAsync(CancellationToken ct); // one frame; per-link FIFO preserved by sublayer (FR-018)
    Task     CloseAsync();
    // Fault is delivered OUT-OF-BAND of the data path (FR-008): the reliability
    // sublayer turns this into ok/tempFail/permFail monitor-stream terms (§5).
    event Action<LinkFaultSignal> OnFault;
}
```

**Grounding against the live seam:** `ILinkTransport.Connect/Listen` is the
cross-machine rendezvous the in-process `IsolateManager` no longer provides
(`isolate_manager.dart:111-243` routes `NetworkMsg` over `SendPort`s); a
`LinkTransport` leaf replaces `IsolateManager`'s `SendPort` routing with real
bytes. `SendBytesAsync` is wired into `onMessageReady`
(`mad_context.dart:45,99`): the callback serializes the `OutboundMessage` header
(`createGlobalSendPayload`/`createSerializerPayload`, corpus 13 §3) plus the
serialized term into one `byte[]` and calls `SendBytesAsync`. `RecvBytesAsync`'s
decoded frame is dispatched to `handleMadAssignment` (`mad_context.dart:229`).
The async `recv` signature (Future/Stream) is exactly what convspec escalates;
expect a human-ratified interface mapping (precedent: `isolate_manager.dart`'s
"Option C ratified") then native bodies — this is why the leaves are hand-authored
not auto-converted.

**Bilateral invariant (FR-005):** the seam exposes exactly two logical ends; a
broker (MQTT, server-mediated XMPP) is a transport relay **under** one endpoint
pair, never a logical hub. The relay's FIFO/at-least-once is **not assumed of the
broker** — it is enforced end-to-end by the reliability sublayer (§4, FR-023).

---

## 4. The reliability sublayer (FR-020..025, FR-060/061) — the load-bearing net-new layer

Sits **between** `ILinkEndpoint` (raw bytes) and `MadContext`'s two seams
(terms). It is the real, unbuilt engineering; byte/behaviour-identical across Dart
and C# (FR-060/061). **Verified state today: zero such machinery exists**
(decision-doc verification note, "0 hits" for failure/dedup/sequence/crypto in
`mad_context.dart`).

### 4.1 The crash it MUST convert to a no-op (verified, FR-021 / SC-008)

**A duplicate frame crashes the agent today.** This is verified live, not
asserted:

- `_handleWriterAssignment` looks up the entry and **throws** `StateError('No
  GlobalizeEntry at index …')` if absent (`mad_context.dart:328-332`), then
  **removes** the entry one-shot (`removeGlobalizeEntry`, `mad_context.dart:364`).
- `_handleReaderAssignment` symmetrically **throws** `StateError('No
  LocalizeEntry …')` (`mad_context.dart:375-380`) and removes one-shot
  (`removeLocalizeEntry`, `mad_context.dart:411`).
- The underlying bind throws on an already-bound cell: `bindWriter` →
  `StateError('bindWriter called on non-writer cell …')` once the cell is
  `ValueTag` (`heap_fcp.dart:363-367`).

So the **second** delivery of the same `_w(p,i)`/`_r(p,i)` assignment hits a
removed entry and throws; the throw is swallowed by a print-and-continue catch up
the call chain (decision-doc), i.e. the agent is left inconsistent rather than
absorbing the duplicate. **FR-021/SC-008 require this become a verified no-op** —
no error thrown, no swallowed error, no re-bind, no goal re-enqueue. The
sublayer's **sequence + global-name dedup key** (FR-020) must short-circuit a
duplicate **before** it reaches `handleMadAssignment`, so the throw path is never
entered on a redelivery. This is the single sharpest correctness gate of the
feature and it is upstream of every transport leaf.

### 4.2 Components (each shared, behaviour-identical Dart↔C#)

| Component | Requirement | Grounding / what it wraps |
|---|---|---|
| **Sequence + dedup key** | FR-020, FR-021, FR-027 | per-link monotone seq# + the never-reused `(agent,index)` global name (corpus 13 §3.1); dedup table absorbs in-window redelivery as a no-op **before** `handleMadAssignment` (§4.1). |
| **Per-link FIFO + reorder buffer** | FR-018, FR-020, FR-053 | the madGLP correctness theorem's precondition (corpus 10/11); a single seq# **detects** but does not **restore** order, so a reorder buffer is required (decision-doc GRL residual). |
| **Idempotent redelivery** | FR-021, FR-027, SC-008 | sound under bind-once monotonicity (FR-052) **once dedup exists**; the cold-call index-0 serializer (`_handleSerializerAssignment`, `mad_context.dart:254-318`) extends the stream exactly once. |
| **Serializer framing** | FR-022, SC-012 | adds what corpus 13 §6 flags as **absent**: a **version byte** (no forward-compat today), an **outer length/CRC** (Dart relied on object integrity), **fragmentation/reassembly** for CoAP/BLE (≤MTU; corpus 13 §6 BLE GATT 20B, CoAP ~1KB). |
| **Cycle-guard (visited-set)** | FR-022, FR-028 | cyclic terms terminate serialization with a clean error; mirrors the runner's existing cycle-safe `_termsEqual` visited-set (`runner.dart:4699-4700`). |
| **Epoch / fencing token** | FR-047, SC-011 | split-brain defense **in addition to** global-name idempotency: a stale resumed writer cannot create a second conflicting binding; loser → `permFail` (§5), never silent overwrite, never `StateError`. |
| **Distributed GC** | FR-024, SC-014 | on link `permFail`, reclaim `W_p` entries (`global_writers_table.dart`), `GlobalSendRegistry` goals, heap `onBind` callbacks (`heap_fcp.dart` `_bindCallbacks`, fired/removed at `:383`), and reply-table entries (§6). Today removal is one-shot only and leaks on peer death. |
| **Reply-table / CorrId** | FR-024 edge "never-arriving reply", spec Key Entities | request/reply over the base ground-relay link uses a local `(V,V?)` pair + ground `CorrId` + reverse link; the table keyed by `CorrId` is reclaimed by distributed GC on `permFail`. |
| **Bounded backpressure** | FR-025, SC-013 | the outbound `mp` queue (`MessageQueue`, `message_queue.dart`) stays bounded — the producer **suspends** (FCP suspension, not a buffer), no OOM, no head-of-line blocking across independent links. |
| **Security** | FR-026..031, SC-007 | per-message origin auth (frame origin checked against the entry's owning peer — today `_lookupVariableForSerialization` is a "simplified version", `mad_context.dart:170-174`); replay window (FR-027); deserializer hardening within bounded memory/stack (FR-028); TLS-by-default for inter-host links (FR-029). The corpus-13 deserializer is a **parser-differential risk class** → fuzz + parity-test BOTH runtimes (FR-031). |

### 4.3 The two live correctness bugs the sublayer must NOT paper over

These are **fix-the-cause** items (CLAUDE.md Bug Protocol), already RULED
in-scope, not robustness workarounds:

- **Compound-operand suspend bug (FR-034, SC-009).** A guard whose operand is a
  `StructTerm` with a nested unbound reader passes the top-level gate, then
  `_termsEqual` returns false → the guard **FAILS instead of SUSPENDING** — a
  non-monotone wrong commit (`runner.dart` `_termsEqual:4699` does not recurse the
  deref into compound args before deciding). Over a link this maps an un-arrived
  remote value to a spurious FAIL, violating three-valued unification (FR-050).
- **Imported-reader reactivation gap (FR-035, SC-009).** A genuinely writerless
  imported reader stores its suspension in `VariableEntry.suspensions`
  (`heap_fcp.dart suspendOnReader:493-505`) and is reactivated **only** by
  `bindImportedReader` (`heap_fcp.dart:641-664`). But `handleMadAssignment` binds
  via `bindVariable`/`bindWriter` only (`mad_context.dart:306,355,402`), **never**
  `bindImportedReader`. So a guard suspended on an imported reader **never
  reactivates** when the value arrives — FR-051 ("reactivate exactly once") is
  violated on that path. **Decision D-B2-3** requires: keep the `VariableEntry`
  path (Preserve-Working-Code), wire the ingress to reactivate it, and test it
  (the spec/code divergence — madGLP §11.3 "local-pairs only" vs the live
  `VariableEntry` path — must be ruled, not silently chosen).

---

## 5. The failure model (FR-043..047) — faults are DATA, not a verdict

**RULED (D-F-1/2/3):** faults surface as **ordinary bound ground terms on a
per-link monitor stream**, read with **existing** guards. A fault is **NOT** a
fourth unification verdict and **NOT** a new guard outcome (FR-043). A disconnect
**never** maps to a logical Fail (FR-044/FR-050); a goal that does not read the
monitor stream stays **safely suspended** across a disconnect.

**Mechanism (grounded):** the monitor stream is an ordinary GLP `Stream(_)` — a
writer/reader pair the link layer extends by binding the tail (exactly the
`_handleSerializerAssignment` discipline, `mad_context.dart:303-318`: build
`[term | freshReader]`, `bindVariable(currentWriter, listCell)`, enqueue
reactivations). The program reads it with `receive/3` (`self.glp:96-97`) and
branches on the fault term with existing guards (`=?=`, `ground`, arithmetic). No
new core path is needed to *deliver* a fault — it is a normal stream bind, which
is why it cannot become a fourth verdict.

**The lattice (FR-045):** `ok` / `tempFail` / `permFail`.
- `tempFail` is the **default** classification for silence (recoverable via
  idempotent reconnect-redelivery — sound under monotonicity **once §4.1 dedup
  exists**).
- `permFail` is a **deliberate, possibly-wrong** give-up after a bounded,
  configurable silence interval (a tuning parameter, NOT a correctness condition;
  spec Assumptions). FLP/two-generals: detection is heuristic — the design bounds
  blast radius, it does not remove the wall.
- On peer disconnect mid-bind a `tempFail` term MUST appear within bounded time,
  then `permFail` on give-up, and a fault-guarded clause becomes reducible
  (FR-046, SC-010). The reader's *data* goal stays suspended-not-failed (FR-044).

**PROPOSED — monitor term vocabulary (language-authority surface; see
§language_authority below).** This is the one place this facet surfaces a
language-authority item, because the *term shapes* the program matches on are an
interface contract. PROPOSED ground term shapes (names/arity pending Gabi):
`ok(LinkId)`, `tempFail(LinkId, Reason)`, `permFail(LinkId, Reason)` — all ground
compounds carried by the existing serializer (corpus 13 §2). They compose with
existing guards; no new guard outcome is introduced.

---

## 6. Cross-runtime parity gate (FR-059/062, SC-001/002)

**RULED (D-B3-2): YES.** A Dart instance MUST connect to a C# instance over one
link. The serializer is already byte-parity (`payload_serializer.cs` present and
git-tracked; corpus 13 establishes the format is endian-independent big-endian
TLV, self-delimiting, schema-free). The gap is a **real transport + an executed
Dart↔C# round-trip test** — neither exists today.

**The gate (FR-062, a release gate):** one **role-parameterized** program
(branch-on-ground-`AgentId`, D-DEC-1/FR-011 — the existing `@`/boot idiom in
`agent_runtime.dart`/`boot_loader.dart`, NOT a two-version fork) split across a
Dart instance and a C# instance joined by one link, producing results
**equivalent to the unsplit single-instance run**. Sequenced per SC-001: first
Dart↔Dart over the simplest transport (loopback/file), then Dart↔C# as the
mandated parity gate.

**Parity obligations this imposes on §4 (FR-061):** the reliability sublayer
(sequence/dedup keys, framing, version byte, length/CRC, fragmentation,
epoch/fence, dedup-before-`handleMadAssignment`) must be **behaviour-identical**
across both runtimes so either runtime can be on either end of any link. The
deserializer + auth + dedup are the parser-differential risk class (FR-031,
SC-007): run the full adversarial corpus on BOTH REPLs with identical verdicts.
Because the C# reference is authored first (§2) and the Dart mirror after, parity
is established by mirroring the C# reference's *behaviour*, with the byte-parity
serializer as the fixed anchor on both ends.

---

## 7. Coherence summary — how the primitive signatures bind to this substrate

For the language-authority gate, the proposed primitive families (D-LA-1/2/3 in
the decision doc — `link*` / `_link_*` base set, the monitor primitive, the
`@<`/`@>`/`@=<`/`@>=` guard family FR-037) must be coherent with:

- **Outputs in clause heads, never `=` in body** — any link-open primitive
  returns a `Channel(In,Out)` (or writer/reader pair) **constructed in the head**,
  mirroring `send/3` (`self.glp:94`). No body unification, no cut, no
  if-then-else (GLP is not Prolog).
- **The two `MadContext` seams** — every base primitive ultimately drives
  `onMessageReady` (out) and `handleMadAssignment` (in) (`mad_context.dart:45,229`);
  it does not touch unification/suspension/globalize directly.
- **The `LinkTransport` seam** — primitives select a leaf by **scheme** (FR-013);
  the seam is `open/send-bytes/recv-bytes/close + fault` (§3), nothing
  protocol-specific leaks above it (FR-006).
- **Three-valued unification preserved** — an un-arrived remote value is an
  unbound **local reader** → Suspend (FR-017/FR-050); the §4.3 compound +
  imported-reader fixes are prerequisites for any guard over a remote operand to
  be sound.
- **SRSW never relaxed by a flag** (FR-048) — broadcast is N bilateral
  ground-copy links (FR-040); BLE BIS true-multi-reader stays an **open co-design
  item** (FR-041/T2 RULED keep), not a silently-dropped feature and not an SRSW
  relaxation.
- **glink is later** — these base primitives are the GRL-style ground-relay base
  discipline; the full writer/reader variable-distribution (`glink`) is a
  higher-level layer built ON TOP, base→glink only (FR-009, RULED B2).

---

## Appendix — file:line citations (live working tree)

- Seams: `glp_runtime/lib/multiagent/mad_context.dart:19,45,87-105` (outbound),
  `:229-246` (inbound), `:180-196` (recursive var extraction / open-structure),
  `:170-174` (simplified origin lookup), `:254-318` (cold-call serializer / stream
  extend by tail bind), `:303-318` (the monitor-stream delivery idiom),
  `:306,355,402` (binds via `bindVariable`, never `bindImportedReader`),
  `:328-332,364` / `:375-380,411` (the throw-and-one-shot-remove dup crash).
- Heap: `glp_runtime/lib/runtime/heap_fcp.dart:85-97` (allocateVariable pair),
  `:103-107` (allocateImportedReader), `:350-390` (bindWriter; throws on bound),
  `:493-514` (suspendOnReader → `VariableEntry.suspensions`), `:641-664`
  (bindImportedReader — the only reactivator of imported-reader suspensions),
  `:671-691` (bindVariable wrapper), `:383` (`_bindCallbacks` fire/remove).
- Runner guard: `glp_runtime/lib/bytecode/runner.dart:4669-4694` (`=?=`),
  `:4699-4700` (cycle-safe `_termsEqual` visited-set), `:4690-4692`
  (default WARN+fail — the `atom/1` analyzer↔runner inconsistency FR-033 lands
  here; `==`/`\==`/`@<`… absent).
- Prelude: `programs/self.glp:15` (`Channel`), `:39-41` (`ground`/`known`/
  `unknown`), `:90-97` (`new_channel`/`send`/`receive`).
- Transport: `glp_runtime/lib/multiagent/isolate_manager.dart:111-243`
  (`IsolateManager` + `NetworkMsg` over `SendPort` — the in-process transport the
  `LinkTransport` seam replaces), `:293-295` (`onMessageReady` wiring).
- codeconv clobber model: `codeconv/src/codeconv/langpairs/dart_csharp/
  target_csharp.py:8-9,41-42` (verbatim 1:1 `.dart`→`.cs` mirror),
  `mirror_dart.py:85` (source-mirrored), `.gitignore:38-39` (`glp_runtime_net/`
  ignored), `.gitignore:74` (`out/csharp/` committed product).
- C# reference present: `out/csharp/lib/multiagent/payload_serializer.cs`,
  `mad_context.cs` (git-tracked).
- Corpus: 07 (mad-context seams), 13 (wire format / version-byte/CRC/fragmentation
  gaps), 14 (globalize/localize open-structure transparency; `known/1` not
  `ground/1`), 10/11 (per-link FIFO is the correctness-theorem precondition).
