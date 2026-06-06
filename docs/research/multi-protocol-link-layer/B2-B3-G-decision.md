# Multi-Protocol GLP Link Layer — Decision Document (B2 / B3 / G)

**Status:** decision-grade synthesis for Gabi's review. Nothing here is approved. Every new primitive and every new guard below is a **PROPOSAL pending your language-authority approval** (CLAUDE.md §Language Authority; DISCIPLINE §1.14).

**Verification note (live-code, this session).** Several proposals lean on claims I checked against the runtime rather than trusting them:
- The "idempotent redelivery is a verified no-op" claim that *every* B2 proposal banks on is **FALSE today**: `mad_context.dart` removes the W_p entry one-shot (`removeGlobalizeEntry` L364, `removeLocalizeEntry` L411) and **throws** `StateError('No GlobalizeEntry…')` / `('No LocalizeEntry…')` (L330, L377) on a second delivery; `bindWriter` throws on an already-`ValueTag` cell (`heap_fcp.dart`). A duplicate frame **crashes the agent** (swallowed by a print-and-continue catch), it is not absorbed.
- The "madGLP keeps all variables as local pairs; the `VariableEntry` imported-reader path is dead (corpus 12)" claim is **FALSE**: `heap_fcp.dart` has a *live* second representation — `allocateImportedReader` (L103), `bindImportedReader` (L641), and a separate suspension path `suspendOnReader → VariableEntry.suspensions` (L496-505) reactivated **only** by `bindImportedReader`. But `handleMadAssignment` calls **only** `bindVariable` (L306/355/402), never `bindImportedReader`. So a guard suspended on a genuine writerless imported reader **never reactivates**. This is a live correctness hazard and a spec/code divergence (madGLP §11.3 says local-pairs only; the code implements both).
- `atom/1` analyzer/runner inconsistency is **confirmed**: analyzer accepts `atom` in `_negatableGuards` (L608) and `typeCheckOps`/grounding (L671); runner `_evaluateGuard` has **no** `atom` case → default WARN+fail.
- Term-comparison guards (`==`, `\==`, `@<`, `@>`, `@=<`, `@>=`) and `\=` are **genuinely absent** from the runner; only `=?=` exists (L4669).
- **Zero** failure/dedup/sequence/crypto machinery exists in `mad_context.dart` (grep: 0 hits).

These facts drive the recommendations below.

---

## Decisions required

A checklist of precise rulings. Each is independent unless noted.

**B2 — variable-distribution model**
- [ ] **D-B2-1.** Choose the distribution model: **(A) glink** (generalize madGLP global links to N transports, max fidelity), **(B) GRL** (ground-relay, no unbound variable ever crosses), or **(C) bdu** (leased single-hop madGLP, non-transitive). *Recommendation: B for the first prototype, with A's wire/protocol as the eventual target — see §B2.*
- [ ] **D-B2-2.** Approve the **prerequisite hardening** as a gating sub-feature regardless of A/B/C: (i) idempotent redelivery (no crash on dup); (ii) per-link FIFO + sequence/dedup key; (iii) serializer cycle-guard + version byte + length/CRC + fragmentation; (iv) reconcile the `bindImportedReader`-vs-`bindVariable` ingress split (or rule the imported-reader path off-limits to the link layer in writing). *These are not optional polish; they are correctness gates.*
- [ ] **D-B2-3.** Rule on the `VariableEntry` imported-reader path: **keep it, wire `handleMadAssignment` to it, and test it**, OR **document in spec that the link layer must represent every remote reader as a local-pair writer and never use `allocateImportedReader`**. (Do not delete the path — CLAUDE.md Preserve-Working-Code.)

**Program-decomposition style**
- [ ] **D-DEC-1.** Confirm **one role-parameterized program** (branch-on-ground-`AgentId`, the existing `@`/boot idiom) as the default; two-version fork only as an escape hatch.
- [ ] **D-DEC-2.** Decide whether to invest later in **choreography + endpoint-projection** for a verified-transparency guarantee (higher build cost) or accept the informal SPMD claim.

**B3 — build target / authoring placement**
- [ ] **D-B3-1.** Confirm **language layer authored in Dart (source-of-truth) → codeconv-generated to C#**; per-transport leaves authored **per-platform, NOT auto-converted**, behind one Dart-defined `LinkTransport` seam. (Rejects C#-first; `glp_runtime_net` is gitignored generated output.)
- [ ] **D-B3-2.** Rule on the **cross-runtime parity bar**: must the *same* program split Dart-instance ↔ C#-instance over one link? If yes → wire format + protocol + the new reliability/dedup sublayer must be byte/behaviour-identical across both runtimes (a release gate). If no (intra-runtime links only) → far lighter obligation.
- [ ] **D-B3-3.** Confirm **GEPA-no-API**: production codegen runs on the Claude harness; the spec-019 litellm/OpenAI optimizer clause is a defect, not a constraint (per memory).
- [ ] **D-B3-4.** Decide the **home for the non-generated native C# transport leaves** so a `codeconv mirror`/scaffold regen cannot clobber them (a hand-authored C# package outside `out/csharp` and `glp_runtime_net`).

**G — guard set + comparison-guards fold-in**
- [ ] **D-G-1.** Confirm **`docs/guards-reference.md` is the single authoritative guard spec** and the standalone `comparison-guards` roadmap feature is **folded in here and cancelled** (no duplicate spec).
- [ ] **D-G-2.** Approve or decline the **standard-order term-ordering family** `@<`/`@>`/`@=<`/`@>=`. *Needed only if peer-ids are non-numeric compound terms requiring a total order (leader election / sorted peer sets). If peer-ids stay numeric/string, decline — `=?=` + arithmetic suffice.*
- [ ] **D-G-3.** Rule on `==` / `\==`: **decline as redundant** (alias `=?=` / `~(=?=)`) or implement.
- [ ] **D-G-4.** Rule on structural `\=`: *Recommendation: decline; standardize on `~(X =?= Y)` (GLP deliberately removed `\=`).*
- [ ] **D-G-5.** Rule on paper-kernel `atom/1`, `reader/1`, `writer/1`: **fix the `atom/1` inconsistency either way** (implement the runner case OR remove from analyzer); *recommend NOT adding `reader/1` (non-monotonic, unsound across a link).*
- [ ] **D-G-6.** Confirm **`=\=` stays untouched** (load-bearing in `self.glp` prelude) — no removal before a prelude migration to `~(=:=)`.

**T1 — broker vs strict bilateral**
- [ ] **D-T1-1.** Accept **broker-as-transport-relay under a logically-bilateral link** (MQTT via co-located in-process broker; XMPP via XEP-0174/0247; AMQP 1.0 is genuinely p2p), with the hard constraint that the broker hop **must preserve per-link FIFO + at-least-once** (enforced by the new sequence/dedup sublayer, not assumed).

**T2 — BLE broadcast vs SRSW**
- [ ] **D-T2-1.** Accept **N independent bilateral links each carrying a COPY of a GROUND value** as the SRSW-faithful broadcast model; **drop BIS true-multi-reader** (also infeasible: no app-data library on either platform); pin **DDS to 1:1 topics** or exclude. Confirm any true multi-reader/broadcast primitive is a **separate, non-SRSW language proposal**, out of scope here.

**Failure semantics**
- [ ] **D-F-1.** Confirm faults surface as **ordinary bound terms on a per-link monitor stream** read with existing guards — **NOT** a fourth unification verdict, **NOT** a new guard outcome; disconnect **never** maps to logical FAIL.
- [ ] **D-F-2.** Confirm the **lattice** (ok / tempFail / permFail; tempFail default for silence, permFail = deliberate possibly-wrong give-up) and **monitor-style** notification (deliver a term) over Erlang-link auto-propagation.
- [ ] **D-F-3.** Rule on **split-brain defense**: epoch/fencing token in addition to global-name idempotency, vs idempotency alone.

**Language-authority approvals (every item is a proposal)**
- [ ] **D-LA-1.** Link primitives — approve/revise names, arities, modes (the `glink_*` / `link*` / `_link_*` families below).
- [ ] **D-LA-2.** Guard additions — per D-G-2..5; note `@<` etc. touch core `runner._evaluateGuard` + the SRSW analyzer (multi-site edit), not just one switch arm.
- [ ] **D-LA-3.** Fault primitive (`link_monitor` / `glink_monitor`) and its term vocabulary.

---

## B2 — distributed unification (split-program)

### The problem in GLP terms

Normally a whole GLP program runs in **one scheduler thread**, with concurrency only via the scheduler; a writer `X` and reader `X?` communicate through **one shared logic variable** inside one heap. This feature **relaxes that assumption**: the system is genuinely multi-instance. The core transform is to take a program where `producer(X), consumer(X?)` share `X` inside one instance and **split it at the shared variable** across two (eventually N, unbounded) REPL instances — the **new link primitives replace the shared variable** and carry the binding across instances. madGLP already performs exactly this transform in-process (one shared pair → two local pairs joined by a global link); B2 is whether/how to generalize the *transport* under that seam while preserving every GLP/FCP invariant.

The hard constraints the split must preserve: the writer/reader **atomic pair**; **writer-MGU** (binds only local writers, never reader/reader, never writer/writer); **three-valued unification** (an un-arrived remote value = an unbound local reader → **Suspend**, never spurious Fail); **suspend-on-reader / reactivate-on-bind**; **bind-once monotonicity**; **SRSW per instance**; **per-link FIFO** (a precondition of the madGLP correctness theorem, corpus 10/11).

### Option A — `glink`: generalize madGLP global links to N transports (max fidelity)

- **Mechanism.** Keep the madGLP cell model verbatim: each link end is a real local two-cell pair joined by a Global-Writers-Table entry; a remote reader is an ordinary unbound local writer that suspends; reactivation is local `bindVariable` activations. Replace the in-process `MessageDeliveryCallback`/SendPort with a host `LinkTransport` seam carrying the same `PayloadSerializer` bytes. Open/partial terms cross (guard is `known/1`, not `ground/1`): `globalize` recurses and mints a fresh sub-link per embedded variable per hop (corpus 14).
- **Decomposition.** One role-parameterized program; N-instance mesh via pairwise links + both-ends-exported forwarding (friend-introduction §10.3), grassroots (no coordinator).
- **PROPOSED primitives** (pending approval): `glink_endpoint(+AgentId, -Descriptor)` (ground name→endpoint directory — the cross-machine rendezvous the in-process `IsolateManager` no longer provides); `glink_connect(+AgentId, -Channel)` (open/reuse a link, returns a `Channel(In,Out)` composing with `send`/`receive`); `glink_monitor(+AgentId, -FaultStream)`.
- **Surviving findings / residual risk.** The faithfulness lens **passes** A's "zero core change" claim *structurally* — but every operational, security, and implementability lens **fails** A *as written* because its load-bearing idempotency claim is contradicted by live code (duplicate frame → `StateError` crash, verified), there is **no** FIFO/dedup/epoch/fault/GC machinery, the serializer has no cycle-guard/version/CRC/fragmentation, remote-ref GC is one-shot-removal-only (leaks on peer death), forwarding mislabels imported-var creator (`_lookupVariableForSerialization` "simplified version"), and per-transport leaves are codeconv's worst case so the byte-identical-Dart invariant does **not** reach the leaf. A is the right *target* but is **multiple features of net-new reliability engineering**, not a thin overlay.

### Option B — `GRL`: ground-relay links (risk-minimal floor)

- **Mechanism.** Only **ground** terms cross the wire — no `_w`/`_r` global names, no distributed unbound variables ever. Each link is a one-directional ground-message stream; a per-link out-relay gates on `ground(X?)` (the existing `send_to_ui` discipline generalized) and an in-relay binds received ground terms into a local stream tail via local writer-MGU. A reply variable becomes a local `(V,V?)` pair + a ground `CorrId` + a reverse link. Eliminates by construction: distributed deref, distributed WxW, remote-ref GC, cross-node cycles, rational-tree marshalling.
- **Decomposition.** One role-parameterized program; each cross-cut channel is rewritten to a request-link + reply-link pair. **Not** behaviorally verbatim — open-structure streaming and reader/channel-end mobility are lost; the split program ≈ original *after* a mechanical request/reply rewrite.
- **PROPOSED primitives** (pending approval): `_link_open(+LinkId, +Direction, -Handle)`, `_link_send(+GroundTerm, +LinkId)` (statically rejects any variable node), `_link_recv(+LinkId, …)` (runtime-driven local bind), `_link_close(+LinkId)`; plus GLP wrappers `out_relay/2`, `in_relay/2`. **Note** the reviewed `out_relay` clause as written **fails SRSW** (`LinkId?` reader occurs twice with no ground guard on it) — the wrapper must ground-guard `LinkId` (mechanical fix), and `_link_recv` still needs the same `InputInjector` ingress machinery madGLP uses (GRL renames it, doesn't avoid it).
- **Surviving findings / residual risk.** Three of four lenses still **fail GRL as written**, but the breaks are *narrower and more contained* than A's: the distributed-GC leak **relocates** to a local reply-table keyed by `CorrId` (leaks on never-arriving answer), CorrId conflict/double-answer is a new (smaller) consensus surface, the single seq# detects but does not *restore* order (needs a reorder buffer), default is silent-suspend-forever, and the receive side must reject non-ground/forged frames (the ground gate is sender-side only). GRL does **not** inherit madGLP Theorem 5.7 — it needs its own smaller theorem. Crucially, GRL's safety claim shrinks under scrutiny to "no `_w`/`_r` on the wire" (corpus 16 shows madGLP already has no distributed deref/WxW), but it still genuinely removes per-hop link rebuild, global-table lifecycle, and rational-tree marshalling — a real reduction.

### Option C — `bdu`: leased single-hop madGLP

- madGLP success-path verbatim + a per-link **lease** + disconnect→**fault-term** + **single-hop** (forbids transitive forwarding, bounding coherence/GC/fault to one `(agent,index)` pair). Faithful on the success path (inherits Theorem 5.7) but **fails** the operational and security lenses for the *same* reason as A (duplicate→crash, no fencing, no auth), and additionally rests on an **unresolved spec/code conflict** (heap §10 `VariableEntry` vs madGLP §11.3 local-pairs) that this session **confirmed is live** — so its "writer-MGU unchanged ordinary local pair" claim is only true under one of two contradictory specs. Single-hop is a real expressiveness loss (friend-introduction becomes explicit re-cold-call).

### B2 disconnect / failure semantics (common to all options)

Perfect failure detection is impossible (FLP / two-generals). Recommended (D-F-1..3): disconnect surfaces as a bound ground **fault term** on a separate monitor stream; the unification trichotomy and guard purity stay intact; default **tempFail** (recoverable via idempotent reconnect-redelivery — sound under monotonicity *once dedup exists*), permFail is a deliberate give-up. Split-brain double-bind is the deepest risk: defend with at-most-once idempotency keyed by the unique never-reused `(agent,index)` global name **plus** an epoch/fencing token so a stale resumed writer cannot create a second conflicting binding.

### T1 / T2 answers (consistent across A/B/C)

- **T1.** Logically bilateral link; broker = transport relay only (grassroots property forbids it as a logical hub). AMQP 1.0 is genuinely p2p (false alarm); MQTT via co-located in-process broker in one node; XMPP via XEP-0174/0247. Hard requirement: broker hop must preserve **per-link FIFO + at-least-once** — supplied by the new sequence/dedup sublayer, not assumed.
- **T2.** SRSW forbids a multi-reader unbound variable. Model BLE BIS broadcast as **N independent bilateral links each carrying a ground-value copy** (legitimized by the ground-guard SRSW relaxation; the broadcast PHY is a transport optimization under a logical fan-out). Drop BIS true-multi-reader (also no app-data library on either platform). Pin DDS to 1:1 topics or exclude.

### B2 RECOMMENDATION

**Ship Option B (GRL) as the first feasibility prototype; adopt Option A (glink) as the eventual transparency target; require D-B2-2 hardening before *any* option touches a real wire.**

Rationale: every option fails the operational/security lenses *today* for the **same root cause** — the live runtime crashes on duplicate delivery and has no FIFO/dedup/fault/auth/GC sublayer. That sublayer is the real work and is **shared** across all three options. GRL minimizes the *additional* distributed-systems surface (no distributed variables, no remote-ref GC, no per-hop globalize), so it gets a working, testable 2-instance split soonest and exposes the reliability sublayer in isolation. A is strictly more transparent and inherits a correctness theorem, but only *after* the same hardening plus serializer/forwarding/GC fixes — sequence it second, reusing GRL's reliability sublayer. Reject C's single-hop-forwarding cut as the default unless transitive forwarding proves genuinely unneeded; its `VariableEntry`/local-pairs spec conflict must be resolved first regardless (D-B2-3).

---

## B3 — build target / authoring placement

### The problem

Dart `glp_runtime` is source-of-truth (byte-identical with the sibling GLP repo); C# `glp_runtime_net` is **codeconv-generated and gitignored** (`.gitignore` "regenerable; do not commit") and is currently all one-line TODO stubs; yet the **C# REPL is the mandated default** (failure = P1). Where do the link primitives, guards, and per-transport clients live?

### Option A — split by layer (Dart language layer → codeconv → C#; native transport leaves per platform)

- **Language layer** (link primitives, guards, globalize/localize/serialize glue) = pure heap-value logic → author in Dart, codeconv-generate to C# (the high-fidelity, low-escalation conversion bucket).
- **Transport leaf** (per-protocol clients: sockets/MQTT/QUIC/BLE) = heavy async/Stream/isolate host glue = codeconv's **escalate-don't-guess** worst case → author per-platform natively behind a tiny Dart-defined `LinkTransport` interface (open/send-bytes/recv-bytes/close + fault), registered by scheme. T4 (one platform per link) means each leaf needs only one impl.

### Option B — C#-first for the language layer

Rejected by every B3 lens and by repo invariants: authoring in the gitignored generated tree would be clobbered by the next scaffold, forks from the sibling repo, and inverts single-source-of-truth + codeconv direction.

### Surviving findings / residual risk for Option A

- **Codeconv fidelity is an ongoing obligation, not free.** The C# multiagent/runner layer is **not yet generated** (verified: stubs). The new files (`LinkRouter`, the reliability sublayer, guard switch arms, the term-comparison comparator with cycle-safe `_termsEqual` and Dart-`num`-vs-C#-numeric-tower ordering) must each pass codeconv + the C# build-gate; `runner.cs` already carries live `// TODO` ground-term-matching gaps. **Security-critical** code (deserializer bounds, auth, dedup) converted to C# is a **parser-differential** risk class — fuzz and parity-test both runtimes.
- **Transport leaves are non-regenerable** and need a home outside `out/csharp`/`glp_runtime_net` (D-B3-4); their reconnect/QoS/TLS behaviour diverges per platform/library, so failure-timing transparency holds for the **value**, not the timing, across runtimes.
- **The `LinkTransport` interface is the one seam that must round-trip codeconv**, and its async signature (`recv` Future/Stream) is exactly what convspec escalates — expect a human-ratified interface mapping (as `isolate_manager.dart`'s "Option C ratified"), then native bodies.
- **The "zero core change" framing is partly false even for A**: ingress integration touches the agent runtime / event loop (the `bindImportedReader`-vs-`bindVariable` split, D-B2-3) and the new guards touch `runner._evaluateGuard` + the SRSW analyzer + the parser (`@` tokenization) — get explicit approval for those core-adjacent edits.

### B3 RECOMMENDATION

**Adopt Option A: language layer Dart-authored → codeconv-generated to C#; transport leaves authored per-platform behind one Dart-defined `LinkTransport` seam, not auto-converted.** It is the only option consistent with single-source-of-truth + the gitignored generated tree + codeconv's real capability profile, and it satisfies the mandated-default C# REPL via generation. **Gate it on D-B3-2** (cross-runtime parity bar) and D-B3-3 (GEPA-no-API). Treat the codeconv conversion of the multiagent/runner layer + the security-critical reliability sublayer as **first-class scope with per-file C# build-gate + cross-runtime parity tests**, not a follow-up.

---

## G — guard constraints

### Full proposed guard set

| Guard | Signature (modes) | Three-valued ask-semantics | Already implemented? | Precedence source |
|---|---|---|---|---|
| `=?=` ground equality | `=?=(X?, Y?)` | succeed if both ground & equal; **suspend** on unbound reader; **fail** on unbound writer or ground-unequal | **Yes** (`runner` L4669, cycle-safe `_termsEqual`) | Tier-1 local + paper kernel |
| `<` `>` `=<` `>=` `=:=` `=\=` arithmetic | `<(X?,Y?)` … | succeed/fail on bound numbers; suspend on unbound reader; fail on unbound writer; ground-implying → SRSW relax | **Yes** (`runner` ~L4332-4394) | Tier-1 + paper kernel |
| `ground` `known` `unknown` `integer` `number` `string` `constant` `compound` `is_list` `no_readers` `module` `is_mutual_ref` `wait`/`wait_until` | type/instantiation | per `guards-reference.md`; `unknown/1` opts out of the suspend gate | **Yes** | Tier-1 |
| `~G` negation | `~(G)` | inverts success↔fail, leaves suspend; restricted to atomic builtins | **Yes** (`runner` L3148-3156) | Tier-1 |
| `atom/1` | `atom(X?)` | succeed iff non-numeric atomic constant; suspend/fail per rule; ground-implying | **INCONSISTENT** — analyzer accepts + grounds (L608/L671), **runner has no case** (defaults to WARN+fail) | paper kernel |
| `==` / `\==` term identity | `==(X?,Y?)` | on **ground** terms ≡ `=?=` / `~(=?=)`; suspend on unbound reader; fail on unbound writer | **No** (runner default→warn+fail) | Tier-3 (ISO/FCP), **not** GLP kernel |
| `@<` `@>` `@=<` `@>=` standard-order | `@<(X?,Y?)` | total order over **ground** terms; suspend until both ground; fail on unbound writer; ground-implying | **No** | Tier-3, **not** GLP kernel — GLP collapsed term comparison to `=?=` |
| `\=` structural disequality | `\=(X?,Y?)` | (ill-defined patiently over partial terms) | **No — REMOVED**; canonical form is `~(X =?= Y)` | Tier-3, deliberately dropped |
| `reader/1` `writer/1` | `reader(X?)` | succeed iff arg is unbound reader/writer; **non-monotonic** (truth flips as store grows) | **No** | paper kernel (flagged non-monotone) |

**Decisive correction to the task's premise:** GLP is **not** ISO-Prolog. `=` is a **body** unification predicate, not a guard (proposing it as a guard contradicts `glp-bytecode-v216`, where `guard_unify` was removed). `\=` was removed. `==`/`\==`/`@<`/… are Prolog-inherited and **deliberately absent** from the GLP paper kernel (arXiv:2510.15747 App.E). So most of the task's enumerated list is *not* canonical GLP and re-adding any of it is a **language extension** requiring approval, not a bug-fix.

### Link-layer-required subset

The link layer needs **almost no new guards**. Its functional requirement — peer-id/global-name **match & distinctness** (`=?=`, `~(=?=)`), **sequence-number** ordering/dedup (`integer`, `<`, `=:=`), **lease/deadline** comparison (`number`, arithmetic, `wait_until`), and **ground-broadcast** fan-out (`ground`) — is **already implemented**. The **only** genuinely link-relevant proposal is `@<`/`@>`, and **only if** peer-ids must be totally ordered as opaque non-numeric compound terms (canonical leader election / sorted peer sets). If peer-ids stay numeric/string, decline the whole term-ordering family.

### Interaction with distributed suspension

A guard reading a remote operand evaluates **locally** against the local reader cell; if unbound (value not yet arrived), it **suspends** via the universal gate and reactivates on the assignment bind — *provided* the runtime actually reactivates it.

⚠️ **Verified hazard (D-B2-3).** The universal gate's deref does **not** recurse into compound args (a `StructTerm` with a nested unbound reader passes the top-level gate, then `_termsEqual` returns false → the guard **FAILS instead of SUSPENDING** — a non-monotone wrong commit). And if a remote reader is represented via `allocateImportedReader` (live path), its suspension lives in `VariableEntry.suspensions` and is reactivated **only** by `bindImportedReader`, which `handleMadAssignment` **never calls**. So the "distributed suspension is free" claim is **false today** for (a) compound operands and (b) imported readers. Both must be fixed before any guard over a remote operand is sound. Non-monotone guards (`~(=?=)`, `\==`, `otherwise`) must additionally be gated *fully-known-across-the-link* before commit, else a late bind can falsify a committed verdict; `otherwise` is the sharpest (a withheld remote bind keeps a sibling suspended-not-failed, so `otherwise` never fires — a stealth deadlock and an attacker-controllable control-flow lever).

### G RECOMMENDATION

**Fold the standalone `comparison-guards` feature into this feature and cancel it; make `docs/guards-reference.md` the single authoritative guard spec (D-G-1).** Track G's deliverable is overwhelmingly **consolidation + test coverage + two real fixes**, not greenfield guard invention:
1. **Fix `atom/1`** (D-G-5): implement the runner case OR remove it from the analyzer sets — it is a live latent defect (compiles + relaxes SRSW, fails at runtime).
2. **Fix the compound-operand suspend bug and the `bindImportedReader` ingress gap** (D-B2-3) so existing guards (`=?=`, arithmetic) are actually sound over remote/compound operands.
3. **`@<`/`@>` only on demonstrated need** (D-G-2): prefer ordering a canonical ground encoding (bytes/string) via existing comparisons over inventing `@<`. **Decline `==`/`\==` as redundant aliases of `=?=`/`~(=?=)` (D-G-3); decline `\=` (D-G-4); decline `reader/1` (non-monotonic, unsound across a link).** Keep `=\=` untouched (D-G-6).

---

## Operational test plan

Any chosen B2/B3/G design must pass these (gating tests marked ⛔ currently fail against live code):

- **⛔ Duplicate-delivery idempotency.** Deliver the same `_w(p,i)`/`_r(p,i)` assignment twice (and a third after entry removal); MUST be a verified no-op — no `StateError`, no swallowed error, no re-bind, no goal re-enqueue. *(Today: throws.)* Repeat with the index-0 serializer cold-call: MUST extend the network input stream exactly once.
- **⛔ Conflicting double-bind / split-brain.** Two writers (stale + reconnected/fenced) deliver different values for one global name; exactly one wins (epoch/fence), the loser yields a `permfail` fault, never a silent overwrite and never a `StateError`. No downstream double-reduction.
- **Reorder / loss.** Deliver dependent frames and stream-tail binds out of order, dropped, and over a FIFO-disabled link; with the sublayer the reconstructed list/result equals the in-order single-instance run; without it the test must *detect corruption*, not silently build a wrong list.
- **⛔ Suspend-not-fail across the cut, incl. compound + imported-reader.** A guard (`ground`/`=?=`/`<`) on a remote reader whose value hasn't arrived SUSPENDS (not FAIL), incl. a **nested unbound reader inside a compound**, and incl. a reader allocated via `allocateImportedReader`; wakes exactly once on bind. *(Today: compound + imported-reader paths fail/never-wake.)*
- **Peer disconnect mid-bind → liveness.** Kill the writer node; the reader's suspended goal does NOT spuriously FAIL; a `fault(LinkId, tempfail)` then (on give-up) `permfail` term appears on the monitor stream within a bounded time; a fault-guarded clause becomes reducible. A goal not reading the monitor stays safely suspended.
- **Reconnect + idempotent redelivery.** Partition then heal; the same bind re-delivered is accepted exactly once; a stale-epoch writer is fenced.
- **Byzantine peer (security).** Forged `_w(victim,i):=T` from a non-owning peer MUST be rejected (origin authenticated against the entry's `remoteAgent`); index-enumeration/cold-call flooding MUST be quota-bounded; malformed/oversized/cyclic/huge-arity frames MUST fail-safe within bounded memory/stack (no OOM, no isolate crash); relayed stdin/stdout MUST require an explicit capability and sanitize control sequences; plain (non-TLS) inter-host links MUST be refused by default. Run the full adversarial corpus on **both** the Dart and codeconv-generated C# REPL with identical verdicts.
- **Distributed-GC.** Open then permFail N links; W_p entries, `GlobalSendRegistry` goals, heap `onBind` callbacks, and (for GRL) reply-table entries all return to baseline; forwarding-chain loop variant asserts no unreclaimable cycle (or documents the WEC requirement).
- **Slow-peer backpressure.** Fast producer + stalled consumer; outbound queue stays bounded (producer suspends), no OOM, no head-of-line blocking across independent links.
- **Serializer robustness.** Cyclic term serialization terminates with a clean error (visited-set); partial/open-term round-trip reconstructs nested placeholders + sub-links; bad-version/bad-CRC frame rejected; over-MTU frame fragments+reassembles (CoAP/BLE).
- **Guard three-valued behaviour (G).** For every new/changed guard: success / suspend-on-unbound-reader (then bind→reactivate) / fail-on-unbound-writer, as REPL Section-A runtime + Section-B/C type-check tests in `programs/tests/typed/`. SRSW: a clause reading a `@<`/`=?=`-grounded var multiply COMPILES; the same without a ground-implying guard is REJECTED. `atom/1` behaves consistently across compile and runtime.
- **Per-transport feasibility (T4).** Each shipped leaf opens a link and carries one bind writer→reader, reactivating the suspended reader, on **one** platform (Windows OR Android): file → WebSocket → QUIC, then one Bluetooth leaf (L2CAP CoC on Android).
- **2-instance split smoke test (the headline).** A single-instance `producer(X)/consumer(X?)` program, split role-parameterized across two REPL instances over the loopback/file transport, produces **byte-identical** observable results to the unsplit run — first Dart↔Dart, then Dart↔C# (the mandated-default parity gate).
- **Baseline regression.** `bash test/run_all_tests.sh` green before and after every core-touching change (heap bind guard, runner `_evaluateGuard`, analyzer); `self.glp` `=\=`-gated division/mod still loads.

---

## Open risks & what we still don't know

- **The reliability sublayer is the real, unbuilt feature.** All B2 options market robustness the code actively violates; sequencing/dedup/retransmit/reorder-buffer/epoch-fencing/fault/heartbeat/GC is substantial net-new code straddling the language/leaf boundary — under-scoped by every proposal as "a thin sublayer."
- **Spec/code divergence on the imported-reader representation is unresolved and live.** madGLP §11.3 (local-pairs only) vs `heap_fcp.dart` (`VariableEntry` path + a second suspension list) is a precedence-1-vs-precedence-1 conflict; `handleMadAssignment` wiring only `bindVariable` means the second path can silently leak suspensions. Must be ruled (D-B2-3) before any "faithful by construction" claim holds.
- **No security model exists.** madGLP §14 "Security Extensions" is a dangling reference to an archived, 0%-implemented crypto sketch; the live receive path does **zero** sender authentication. Moving frames onto a hostile network without per-message auth makes every distributed bind forgeable. Auth + AEAD + replay-window + deserializer hardening are prerequisites, not enhancements, and must be re-audited on the C# default.
- **Cross-runtime parity is unquantified.** The C# multiagent/runner layer is unbuilt (stubs); fidelity for the numeric tower, cycle-safe term equality, big-endian framing, and the security-critical deserializer is unproven and is a parser-differential risk.
- **FLP wall.** tempFail/permFail verdicts are heuristic and attacker-tunable; no design removes this — only bounds blast radius. Any program taking irreversible action on a fault remains exploitable.
- **Transparency ceiling.** Value + dataflow suspension are transparent; **cross-link ordering, latency, and partial failure are not** (only per-link FIFO holds). Non-confluent programs relying on global single-thread scheduling order will diverge after the split, and nothing checks confluence/projectability.
- **Transport reality.** At least one mandated transport (BLE LE-Audio BIS) cannot be honored under SRSW and has no app-data library; MQTT/core-XMPP need broker/server escape hatches; CoAP/BLE need a segmentation+integrity sublayer the wire format lacks. The faithful transport set is smaller than the 14+ enumerated.
- **GRL's correctness theorem is unwritten.** GRL does not inherit Theorem 5.7; its reply-table reclamation + CorrId-uniqueness obligations are not covered by "ordered channel + local single-assignment."
- **No deterministic distributed test harness exists.** Today's multiagent tests are in-process isolates (lossless, ordered); real-transport, partition-injecting, multi-instance tests are unbuilt — the very layer the feature adds is the one the current harness cannot test.

---

## Corpus & provenance

| # | Title | Authors / Year | Precedence class | Corpus path |
|---|---|---|---|---|
| 00 | madGLP Specification v5.3 | Claude, from CGLP §7 / 2026 | glp-current (Tier 1) | `…/corpus/00-madglp-spec-v5.3.md` |
| 01 | GLP Heap Storage — Pointer Architecture v3.4 | glpnet, after FCP (Shapiro et al.) / 2026 | glp-current | `…/corpus/01-glp-heap-pointer-architecture-v3-4.md` |
| 02 | GLP Runtime System Spec v2.19 | glpnet / 2026 | glp-current | `…/corpus/02-glp-runtime-spec-v2.19.md` |
| 03 | GLP Guards Quick Reference | glpnet | glp-current | `…/corpus/03-glp-guards-quick-reference.md` |
| 04 | Agent Execution Spec (event-driven drain-flush) | glpnet (Shapiro et al. paper) / 2026 | glp-current | `…/corpus/04-agent-execution-spec-event-driven-drain-flush.md` |
| 05 | Dart Runtime Spec for `@` Operator (Isolate Boot) v0.6 | glpnet / 2026 | glp-current | `…/corpus/05-dart-runtime-spec-at-operator-isolate-boot.md` |
| 06 | heap_fcp.dart live implementation | glpnet runtime / 2026 | glp-current | `…/corpus/06-heap-fcp-live-implementation.md` |
| 07 | mad_context.dart (MadContext) | glpnet, per CGLP §7 / 2026 | glp-current | `…/corpus/07-mad-context-dart.md` |
| 08 | glp_activation / `#` goal-routing seam | glpnet / 2026 | glp-current | `…/corpus/08-glp-activation-mhash-goal-routing-seam.md` |
| 09 | self.glp prelude + MutualRef merge idiom | GLP/glpnet (Shapiro lineage) / 2026 | glp-current | `…/corpus/09-self-glp-prelude-mutual-ref.md` |
| 10 | Implementing Grassroots Logic Programs with MTS and AI (CGLP/madGLP §) | Ehud Shapiro / 2026 (arXiv:2602.06934) | glp-paper (Tier 2) | `…/corpus/10-cglp-madglp-section-shapiro.md` |
| 11 | madGLP correctness theorem (N-agent) | Ehud Shapiro / 2026 | glp-paper | `…/corpus/11-madglp-correctness-theorem-n-agent.md` |
| 12 | Imported-variable heap model: madGLP local-pairs vs irma VariableEntry | glpnet specs + live code / 2026 | glp-current | `…/corpus/12-imported-variable-heap-model-madglp-vs-irma.md` |
| 13 | PayloadSerializer wire format | glpnet / 2026 | glp-current | `…/corpus/13-payload-serializer-wire-format.md` |
| 14 | Partial-term transmission over ground-only transports (per-hop globalization) | glpnet specs + code (Shapiro lineage) / 2026 | glp-current | `…/corpus/14-ground-only-transport-partial-term-globalization-per-hop.md` |
| 15 | `#` cross-module dispatch vs madGLP isolate boundary | glpnet maintainers (FCP precedent Shapiro et al.) / 2026 | glp-current | `…/corpus/15-hash-dispatch-vs-madglp-isolate-boundary.md` |
| 16 | Distributed deref / WxW / local pairs | glpnet / 2026 | glp-current | `…/corpus/16-distributed-deref-wxw-local-pairs.md` |
| 17 | Efficient Logic Variables for Distributed Computing | Haridi, Van Roy, Brand, Mehl, Scheidhauer, Smolka / 1999 | earlier-cl-paper (Tier 3) | `…/corpus/17-efficient-logic-variables-distributed-computing.md` |

*Corpus root: `D:/bstdev/research/glp/glpnet/docs/research/multi-protocol-link-layer/corpus/`. Tier-3 (FCP/Logix/Oz/ISO) is mechanism-inspiration only and never overrides Tier-1 local specs or Tier-2 GLP papers (SOURCE PRECEDENCE).*