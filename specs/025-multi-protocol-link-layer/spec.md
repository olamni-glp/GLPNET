# Feature Specification: Multi-Protocol Peer-to-Peer Link Layer for Distributed GLP

**Feature Branch**: `025-multi-protocol-link-layer`
**Created**: 2026-06-06
**Status**: Draft
**Input**: User description: "Multi-protocol peer-to-peer link layer for distributed GLP"

## User Scenarios & Testing *(mandatory)*

### Overview

This feature adds a strictly peer-to-peer, multi-protocol **link layer** that lets a GLP program which today runs inside a single REPL instance be **split across two or more remote REPL instances over a real transport protocol**, while preserving GLP's one-writer/one-reader logic-variable semantics exactly. A producer and a consumer that today communicate through one shared logic variable inside one heap can instead run on separate machines or runtimes and still produce the identical observable result, with the link primitives carrying the binding across instances in place of the shared variable. The link layer can also reroute a REPL's `stdin`/`stdout`/`stderr` to a remote instance.

The link layer ships on the mandated-default C# REPL first as the reference implementation, with a Dart mirror authored afterward, and is proven correct by a cross-runtime Dart↔C# round trip. It establishes the base/current link primitives (request-link, accept-link, setup, sender, receiver, server-listener, client-connector, fault monitor) as the foundation on which a later, higher-level `glink` variable-distribution transparency layer will be built. Full `glink` transparency is out of scope for this feature's MVP; the dependency runs base → `glink`, never the reverse.

---

### User Story 1 - Split a single-instance producer/consumer program across two REPLs with byte-identical results (Priority: P1)

A developer has a GLP program in which `producer(X)` and `consumer(X?)` share one logic variable `X` inside one REPL instance. Using one role-parameterized version of that same program (the role chosen by a ground `AgentId`, not a forked second copy), the developer launches it on two REPL instances connected by the first real transport, so the producer runs on instance A and the consumer on instance B, the shared variable replaced by a link. The split run produces the **exact same observable output** as the unsplit single-instance run. This is first demonstrated Dart↔Dart, then promoted to the Dart↔C# cross-runtime parity gate.

**Why this priority**: This is the headline capability and the proof that the link layer preserves GLP semantics across a real wire. Until a real producer/consumer split yields byte-identical results to the unsplit baseline, nothing else in the feature has demonstrable value. It is the smallest end-to-end slice that exercises the writer/reader pair across instances, the reliability sublayer, and the GLP-invariant preservation in one go.

**Independent Test**: Run the program unsplit in one REPL and capture its observable output; run the same role-parameterized program split across two REPL instances over the first transport and capture its output; assert the two outputs are byte-identical. Run this first Dart↔Dart, then with one endpoint on the C# REPL and the other on the Dart REPL.

**Acceptance Scenarios**:

1. **Given** a single-instance `producer(X)/consumer(X?)` program and its captured unsplit observable output, **When** the same role-parameterized program is launched split across two REPL instances over the first real transport, **Then** the split run's observable output is byte-identical to the captured unsplit baseline.
2. **Given** the producer instance has not yet produced its value, **When** the consumer's goal reads the corresponding remote reader, **Then** the consumer goal **suspends** (it does not spuriously fail and does not deadlock), and it **reactivates exactly once** when the producer binds the value and it arrives over the link.
3. **Given** a passing Dart↔Dart split run, **When** the same split is re-run with one endpoint on the C# reference REPL and the other on the Dart REPL, **Then** the cross-runtime run produces output byte-identical to both the unsplit baseline and the Dart↔Dart split run.

---

### User Story 2 - Carry the same split across multiple real transport protocols (Priority: P2)

A developer needs the link to run not only over the first prototype transport but over a chosen set of real-world protocols. Each transport offers a symmetric sender + receiver and both a server-listener and a client-connector role, and is strictly bilateral (logically point-to-point). The developer selects a transport by scheme and runs the same split program over it without changing the program logic, only the link's transport binding.

**Why this priority**: Transport breadth is the product surface that makes the link layer usable in real deployments, but it is only meaningful once P1 has proven a single transport carries a faithful split. Each added transport is an independently testable increment that reuses the P1 acceptance harness.

**Independent Test**: For each shipped transport leaf, open a link using its server-listener on one instance and its client-connector on the other, run the P1 split program over it, and assert one writer→reader bind crosses the link and reactivates the suspended reader — on at least one accepted platform (Windows OR Android) per leaf.

**Acceptance Scenarios**:

1. **Given** a transport selected by scheme with a server-listener started on instance A, **When** instance B opens a client-connector to it, **Then** a bilateral link is established and a single writer→reader bind crosses it and reactivates the suspended reader on the far side.
2. **Given** two distinct transports from the lineup, **When** the same role-parameterized split program is run over each in turn, **Then** each run produces output byte-identical to the unsplit baseline, with only the link's transport binding changed between runs.
3. **Given** a transport offered in both plain and TLS variants, **When** an inter-host link is opened without TLS, **Then** the link is refused by default and the TLS variant succeeds — proving both variants are present and the secure default holds.

---

### User Story 3 - Reroute a REPL's stdin/stdout/stderr to a remote REPL (Priority: P3)

A developer wants one REPL instance's standard streams to be served by another instance over a link: input typed (or piped) at the remote end is delivered as the local REPL's `stdin`, and the local REPL's `stdout`/`stderr` are surfaced at the remote end. This lets a program running on one instance be driven and observed from another across the link layer.

**Why this priority**: Stream rerouting is a distinct, valuable capability but depends on a working bilateral link (P1/P2). It is the smallest useful interactive application of the link layer and can be added without changing the variable-distribution core.

**Independent Test**: Establish a link between two REPLs, enable stream rerouting under an explicit capability, send a known input sequence from the remote end, and assert the local program receives it as `stdin` and that its `stdout`/`stderr` appear at the remote end matching the locally-captured output.

**Acceptance Scenarios**:

1. **Given** two linked REPL instances with stream rerouting enabled under an explicit capability, **When** a known input sequence is supplied at the remote end, **Then** the local REPL consumes it as `stdin` and produces the same result it would have from local input.
2. **Given** a program writing to `stdout` and `stderr` on the local instance, **When** rerouting is active, **Then** that output is surfaced at the remote end, byte-equivalent to the locally-captured streams, with `stdout` and `stderr` kept distinct.
3. **Given** a relayed stream channel, **When** rerouting is requested without the explicit capability, **Then** the request is refused; and **When** control sequences appear in relayed input/output, **Then** they are sanitized rather than passed through unfiltered.

---

### User Story 4 - Observe and react to link faults via a per-link monitor stream (Priority: P4)

A developer needs to know when a link degrades or dies without that fault corrupting GLP's logic. Each link exposes a per-link **fault monitor** that delivers ordinary bound terms (`ok` / `tempFail` / `permFail`) on a monitor stream that the program reads with existing guards. A disconnect never becomes a logical failure of the program; a goal that does not read the monitor stream stays safely suspended.

**Why this priority**: Fault visibility is required for any real deployment but is layered on top of an established link (P1/P2). It is independently testable and load-bearing for correctness — a fault must never silently overwrite a binding or spuriously fail a goal.

**Independent Test**: Establish a link, kill the writer instance mid-bind, and assert that (a) the reader's suspended goal does not spuriously fail, (b) a `tempFail` then (on give-up) `permFail` term appears on the monitor stream within a bounded time, and (c) a fault-guarded clause becomes reducible.

**Acceptance Scenarios**:

1. **Given** a reader suspended on a value not yet arrived, **When** the writer instance disconnects, **Then** no spurious logical failure occurs and a `tempFail(LinkId, …)` term is delivered on that link's monitor stream within a bounded time, followed by `permFail` on deliberate give-up.
2. **Given** a duplicated or out-of-order delivery on a link, **When** the same bind frame is re-delivered, **Then** it is accepted exactly once (a verified no-op on the duplicate, not an agent crash), and the reconstructed result equals the in-order single-instance run.
3. **Given** two writers (one stale, one reconnected/fenced) deliver different values for the same global name, **When** both arrive, **Then** exactly one wins by epoch/fencing token, the loser yields a `permFail` fault, and there is never a silent overwrite.

---

### Decomposition Note

The split program is delivered as **one role-parameterized GLP program**, not a two-version fork. The instance's role (producer vs consumer, server-listener vs client-connector, sender vs receiver) is selected by branching on a **ground `AgentId`** supplied at boot — the existing `@`/boot idiom. A two-version fork is reserved only as an explicit escape hatch and is not the default decomposition. This keeps the unsplit baseline and the split deployment provably the same source, which is what makes the byte-identical P1 acceptance meaningful.

---

### Edge Cases

- **Duplicate delivery.** A frame (or an index-0 serializer cold-call) arrives twice or more. The system MUST treat repeats as a verified no-op (dedup by sequence + global-name), never re-binding, re-enqueuing, raising, or swallowing an error. (Baseline today: a duplicate frame crashes the agent; this case marks that closed.)
- **Split-brain double-bind.** A stale writer and a reconnected/fenced writer each deliver a different value for one global name. Exactly one binding wins via epoch/fencing; the loser becomes a `permFail` fault; no silent overwrite and no downstream double-reduction.
- **Reorder / loss.** Dependent frames and stream-tail binds arrive out of order or are dropped. With the reliability sublayer engaged the result equals the in-order run; without it, corruption is detected, never silently materialized as a wrong result.
- **Peer disconnect mid-bind (liveness).** A writer node dies while a reader's goal is suspended on it. The suspended goal MUST NOT spuriously fail; a `tempFail` then (on give-up) `permFail` term reaches the monitor stream within a bounded time so a fault-guarded clause can proceed; an unmonitored goal stays safely suspended.
- **Slow-peer backpressure.** A fast producer faces a stalled consumer. The outbound queue stays bounded (the producer suspends), with no out-of-memory and no head-of-line blocking across independent links.
- **Cyclic / oversized / forged frames (fail-safe).** Cyclic terms terminate serialization with a clean error (visited-set); over-MTU payloads fragment and reassemble (CoAP/BLE); bad-version / bad-CRC / huge-arity / oversized frames fail safe within bounded memory and stack; a frame whose claimed origin is not the entry's owning peer is rejected by origin authentication.
- **Byzantine peer.** A peer forges binds for a global name it does not own, floods cold-call index enumeration, or abuses relayed stdin/stdout. Forged binds are rejected; flooding is quota-bounded; relayed stdio requires an explicit capability and sanitizes control sequences; plain inter-host links are refused without TLS.
- **Compound / imported-reader suspension.** A guard over a remote operand encounters a nested unbound reader inside a compound term, or a reader on the imported-reader path. It MUST suspend (not fail) and wake exactly once on bind — closing the two verified live correctness hazards.
- **Never-arriving reply.** A request/reply exchange whose answer never comes leaves a correlation-keyed reply-table entry; distributed GC MUST reclaim it on link `permFail` rather than leak indefinitely.
- **Broker hop.** Where a transport requires a broker/server (MQTT, server-mediated XMPP), the relay MUST preserve per-link FIFO + at-least-once; otherwise the link's correctness contract is violated.
- **BLE broadcast vs SRSW (open co-design).** Broadcast is modeled as N bilateral ground-copy links while true BLE LE-Audio BIS multi-reader is kept in scope as a co-design goal; the SRSW tension on a true multi-reader is an explicitly open item, not a silently dropped feature.

## Clarifications

### Session 2026-06-06

- Q: Do this feature's peer-ids need a non-numeric total order — i.e., does the MVP include leader-election or sorted-peer-set use cases over **opaque compound** peer-ids? → A: **Yes (ruling B, Gabi).** Peer-ids MAY be non-numeric compound terms requiring a total order; leader-election / sorted-peer-set use cases are in scope. The standard-order term-ordering guards `@<` / `@>` (and the `@=<` / `@>=` companions) are therefore IN SCOPE and MUST be added (FR-037), under explicit language-authority approval. This resolves the lone open clarification flagged in the Dependencies section and removes the conditional gating in FR-037 / FR-039 / SC-006 / the Peer / AgentId entity.

## Requirements *(mandatory)*

### Functional Requirements

#### Base Link Primitives (behavioral)

- **FR-001**: System MUST provide a set of base link primitives covering, at the behavioral level: requesting a link, accepting a link, link setup, sending, receiving, listening as a server, connecting as a client, and a per-link fault monitor. Concrete signatures, arities, and modes are deliberately NOT fixed in this specification; they are co-designed at the plan gate under language-authority approval.
- **FR-002**: A link MUST be establishable by a pairing of a server-listener role and a client-connector role, and independently by a request-link / accept-link handshake; both paths MUST yield an equivalent established link.
- **FR-003**: Each base primitive MUST be symmetric across the two link ends: every shipped transport MUST expose both a sender capability and a receiver capability, and both a server-listener capability and a client-connector capability, such that either link end can both send and receive over the link without one end being privileged.
- **FR-004**: The connection-establishment role MUST be independent of subsequent data direction — a server-listener end MUST be able to act as the writer end and a client-connector end as the reader end, and vice versa. *(Which side listens is a deployment/NAT concern; which side writes is a program concern; the two must not be conflated.)*
- **FR-005**: Every link MUST be strictly bilateral (exactly two logical ends). No primitive may create a logical hub, shared bus, or N-way logical channel; any broker is a transport relay beneath a logically-bilateral link, never a logical participant. The broadcast model is governed by FR-040.
- **FR-006**: Establishing or accepting a link MUST be expressible through the base link primitives without exposing transport-specific connection details into GLP program logic, so one role-parameterized program drives any transport leaf through the same behavioral seam.
- **FR-007**: Link setup MUST be idempotent at the link-identity level: re-running setup or re-establishing an already-established link MUST reuse the existing link rather than create a conflicting duplicate.
- **FR-008**: Each established link MUST expose a per-link fault monitor that is independently observable from the link's data path (a goal may read data without reading faults, and vice versa).
- **FR-009**: The base link primitives are the FIRST deliverable; full writer/reader variable-distribution transparency (`glink`) is a strictly LATER, higher-level construct built ON the base primitives. The dependency direction is base→`glink` and MUST NOT be reversed; full `glink` transparency is OUT OF SCOPE for this feature's MVP.
- **FR-010**: The base discipline MUST carry the binding across instances as the replacement for the shared logic variable a single-instance program would use; this base layer relays values across the cut (ground-relay discipline) and is the substrate on which later `glink` transparency is built.

#### Program Decomposition

- **FR-011**: A program split across instances MUST be expressed as ONE role-parameterized program that branches on a ground agent identifier, NOT as a two-version fork. A two-version fork is permitted only as an explicit escape hatch and MUST be justified.

#### Transport Lineup & Connectivity Model

- **FR-012**: System MUST support the following transport leaves, each satisfying FR-003 through FR-008 (symmetric sender+receiver, server-listener+client-connector, strictly bilateral, per-link fault monitor):
  - **Messaging / pub-sub-as-relay**: MQTT, AMQP 1.0 (peer-to-peer use only), XMPP, DDS.
  - **Constrained / IoT**: CoAP.
  - **Web transports**: HTTP/2, HTTP/3, WebSocket — each in a PLAIN and a TLS variant (see FR-036, FR-037).
  - **Tunnelling / shell-style**: SSH (tunnelling).
  - **File-transfer transports**: FTP, SFTP.
  - **File endpoints**: binary and text file endpoints supporting read, write, and search.
  - **Bluetooth**: Bluetooth LE Audio (BIS/CIS), L2CAP CoC, BLE GATT, BR/EDR SPP.
- **FR-013**: Each listed transport MUST be selectable by the program through a stable scheme/identifier such that switching the carrying transport requires no change to the role-parameterized GLP program above the link seam.
- **FR-014**: DDS MUST be used as a 1:1 (single-reader/single-writer) topic link only; any DDS configuration that would create a true multi-reader topic is OUT OF SCOPE for a bilateral link and is governed instead by the broadcast model (FR-040, FR-041).
- **FR-015**: AMQP 1.0 MUST be used in its genuinely peer-to-peer mode (direct link between two AMQP peers), NOT through a logical broker hub.
- **FR-016**: For every transport leaf, the link layer MUST be able to open a link, carry exactly one bind (writer→reader) across that link, and reactivate the suspended reader on the receiving end, on at least one supported platform. This per-transport feasibility test MUST be an explicit, executed acceptance test for each shipped leaf, not an inferred capability.
- **FR-017**: An un-arrived value over any transport MUST present at the receiver as an unbound local reader that SUSPENDS (never a spurious FAIL); arrival of the value MUST reactivate the suspended reader exactly once.
- **FR-018**: Each transport leaf MUST preserve per-link FIFO: binds and frames delivered on one link MUST be observable at the other end in send order.
- **FR-019**: The set of transports that are feasible under SRSW and the platform matrix MAY be smaller than the full enumerated lineup; any enumerated transport found infeasible on both Windows and Android MUST be documented as such with rationale rather than silently omitted.

#### Reliability Sublayer

- **FR-020**: Each link MUST carry a sequence/dedup key and MUST reconstruct in-order delivery; out-of-order, dropped, or duplicated frames MUST yield a result equal to the in-order single-instance run, OR (when the sublayer is disabled) MUST DETECT corruption rather than silently build a wrong result.
- **FR-021**: Delivering the same binding twice (and again after entry removal) MUST be a verified no-op — no error thrown, no swallowed error, no re-bind, no goal re-enqueue. This is a correctness gate: a duplicate frame today crashes the agent and MUST become an absorbed no-op.
- **FR-022**: The serializer / wire format MUST include a cycle-guard (visited-set) so cyclic terms terminate with a clean error, a version byte, a length/CRC integrity check, and fragmentation/reassembly for under-MTU transports (e.g., CoAP, BLE). Bad-version and bad-CRC frames MUST be rejected; over-MTU frames MUST fragment and reassemble correctly; partial/open-term round-trips MUST reconstruct nested placeholders.
- **FR-023**: When a link is relayed through a broker or server, the relay MUST preserve per-link FIFO and at-least-once delivery; these properties MUST be enforced by the sequence/dedup sublayer end-to-end, NOT assumed of the broker. A relay that reorders or drops frames MUST be corrected by the sublayer such that the receiving end observes in-order, exactly-once-effective binds.
- **FR-024**: When links are opened and then `permFail`, all associated resources (registry entries, send-registry goals, bind callbacks, and any reply-table entries) MUST return to baseline; a forwarding-chain loop MUST NOT leave an unreclaimable cycle (or the cycle requirement MUST be explicitly documented).
- **FR-025**: Under a fast producer and a stalled consumer, the outbound queue MUST stay bounded (the producer suspends), with no out-of-memory and no head-of-line blocking across independent links.

#### Security

- **FR-026**: Every received binding MUST be authenticated against the owning peer; a forged binding for a victim's global name from a non-owning peer MUST be rejected.
- **FR-027**: The receive path MUST enforce a replay window so that replayed frames outside the window are rejected; combined with FR-021, in-window redelivery is an idempotent no-op.
- **FR-028**: Malformed, oversized, cyclic, or huge-arity frames MUST fail safe within bounded memory and stack — no out-of-memory, no runtime/isolate crash. Index-enumeration / cold-call flooding MUST be quota-bounded.
- **FR-029**: Inter-host links MUST be TLS-by-default; a plain (non-TLS) inter-host link MUST be refused by default and MUST require an explicit, deliberate opt-out. ("Inter-host" means the two link ends reside on different hosts; loopback and co-located in-process links are not inter-host and may use PLAIN variants.)
- **FR-030**: Rerouting `stdin`/`stdout`/`stderr` to a remote REPL MUST require an explicit capability and MUST sanitize control sequences before relaying.
- **FR-031**: The full adversarial security corpus (FR-026 through FR-030) MUST run on BOTH the Dart and the C# REPL and produce identical verdicts, because runtime-to-runtime parsing differences are a known differential-risk class.

#### Guards

- **FR-032**: System MUST keep and deliver the comparison-guards feature (it is folded into this feature, not cancelled). The full approved guard set MUST be specified against a single authoritative guard reference (`docs/guards-reference.md`) so there is no duplicate guard spec.
- **FR-033**: System MUST fix the `atom/1` analyzer↔runner inconsistency — `atom/1` MUST behave consistently at compile time (analyzer / grounding) and at runtime; today the analyzer accepts and grounds it while the runner has no case and fails at runtime.
- **FR-034**: A guard whose operand is a compound term containing a nested unbound reader MUST Suspend, NOT Fail; today such a compound passes the top-level gate and is then wrongly committed as a failure (a non-monotone wrong commit). System MUST fix this.
- **FR-035**: A guard suspended on a genuinely writerless imported reader MUST reactivate when that reader is later bound; today its suspension is never reactivated by the assignment-ingress path (a live correctness hazard and spec/code divergence). System MUST fix this.
- **FR-036**: System MUST DECLINE `==`, `\==`, `\=`, and `reader/1`. `==`/`\==` are declined as redundant aliases of `=?=` / `~(=?=)`; `\=` is declined (canonical form is `~(X =?= Y)`); `reader/1` is declined as non-monotonic and unsound across a link.
- **FR-037**: System MUST add the standard-order term-ordering guards `@<` / `@>` together with their `@=<` / `@>=` companions. The need for a non-numeric total order over peer-ids is confirmed in scope (leader-election / sorted-peer-set use cases over opaque compound peer-ids — Clarification 2026-06-06), so the family is required, NOT optional. Each MUST exhibit the three-valued ask-semantics of FR-039 (succeed on bound-and-satisfied, suspend on an unbound reader then reactivate on bind, fail on an unbound writer) over **ground** terms and be added under explicit language-authority approval (part of the approved guard set).
- **FR-038**: The arithmetic disequality guard `=\=` MUST remain untouched (it is load-bearing in the prelude); no removal before any prelude migration.
- **FR-039**: Every new or changed guard MUST exhibit the correct three-valued ask-semantics — succeed on bound-and-satisfied, Suspend on an unbound reader (then reactivate on bind), Fail on an unbound writer — verified as runtime tests AND as positive/negative type-check tests. Any non-monotone guard (e.g., `~(=?=)`, negation, `otherwise`) MUST be gated fully-known across the link before commit, so a late remote bind cannot falsify an already-committed verdict. Guard additions/changes that touch core evaluation and the SRSW analyzer (and parser tokenization for the now-in-scope `@<`/`@>` family — Clarification 2026-06-06) MUST be made under explicit language-authority approval and MUST keep the baseline REPL test suite green.

#### Broadcast Model & BLE Multi-Reader

- **FR-040**: Broadcast MUST be modeled as N independent bilateral links, each carrying a COPY of a GROUND value to one reader, rather than as a single multi-reader unbound variable (SRSW forbids one unbound logic variable with multiple readers).
- **FR-041**: Bluetooth LE Audio BIS true multi-reader MUST be KEPT IN SCOPE as an open co-design goal alongside the N-bilateral-ground-copy model. The tension between BIS true-multi-reader and SRSW is an explicit open co-design item to be resolved at a later stage gate; it MUST NOT be silently dropped, and any true-multi-reader semantics introduced for BIS MUST be reconciled with SRSW before acceptance.
- **FR-042**: BLE CIS (connection-oriented) MUST be usable as an ordinary bilateral link satisfying FR-003 through FR-008.

#### Failure-Monitor Model

- **FR-043**: Link faults MUST surface as ordinary bound terms on a per-link MONITOR STREAM, read with existing guards. A fault MUST NOT be a fourth unification verdict and MUST NOT be a new guard outcome.
- **FR-044**: A disconnect MUST NEVER map to a logical Fail; a goal that does not read the monitor stream MUST remain safely suspended across a disconnect, and fault notification MUST NOT auto-propagate failure to unrelated goals.
- **FR-045**: Faults MUST follow the lattice `ok` / `tempFail` / `permFail`. `tempFail` is the default classification for silence (recoverable via idempotent reconnect-redelivery). `permFail` is a deliberate, possibly-wrong give-up.
- **FR-046**: After a peer disconnect mid-bind, a `tempFail` term MUST appear on the link's monitor stream within a bounded time, and (on give-up) a `permFail` term MUST follow; a fault-guarded clause MUST then become reducible.
- **FR-047**: System MUST defend against split-brain double-binds with an epoch/fencing token in addition to global-name idempotency. When a stale writer and a reconnected/fenced writer deliver different values for one global name, exactly one MUST win (by epoch/fence) and the loser MUST yield a `permFail` fault — never a silent overwrite, never a crash, never a downstream double-reduction.

#### Preservation of GLP Invariants

- **FR-048**: The split MUST preserve Single-Reader/Single-Writer (SRSW) per instance; no link mechanism may introduce a second reader or second writer for a logic variable within an instance, and SRSW MUST NEVER be relaxed by an option flag.
- **FR-049**: Cross-link binding MUST bind only local writers (writer-MGU); it MUST NEVER bind reader-to-reader and MUST NEVER bind writer-to-writer.
- **FR-050**: A remote value that has not yet arrived MUST be treated as an unbound local reader and MUST yield Suspend, NEVER a spurious Fail. Disconnection, latency, or non-arrival MUST NEVER map to a logical Fail (three-valued unification).
- **FR-051**: A goal suspended on a remote reader MUST reactivate exactly once when the corresponding value arrives and binds the local cell; no remote operand may leave a goal permanently un-reactivated when a value does arrive.
- **FR-052**: Each distributed binding MUST be bind-once and monotonic; once bound, a cell MUST NOT be re-bound to a different value by any redelivery, retransmit, or reconnect.
- **FR-053**: Each link MUST preserve per-link FIFO ordering of deliveries, including when the underlying transport routes through a broker relay.
- **FR-054**: All of FR-048 through FR-053 MUST hold identically across the Dart mirror and the C# reference, and across a Dart↔C# link.

#### Cross-Runtime Parity & C#-First Delivery Constraint

- **FR-055**: The base link primitives, the guard deliverables, the failure model, and the reliability sublayer MUST be authored in C#/.NET FIRST as the priority REFERENCE implementation, targeting the mandated-default C# GLP REPL.
- **FR-056**: The Dart mirror of the same primitives MUST be authored only AFTER the C# reference works fully and passes its acceptance tests. Until the C# reference is complete, the Dart mirror is not a release dependency.
- **FR-057**: Hand-authored C# for this feature (including non-regenerable transport leaves) MUST live in a location that a codeconv regeneration / scaffold / mirror cannot clobber (outside the generated `out/csharp` and `glp_runtime_net` trees), so a regen run cannot silently overwrite it.
- **FR-058**: Per-transport leaves MAY be authored per-platform / native and are NOT required to be auto-converted; each transport leaf sits behind a single, uniform link-transport seam (open / send-bytes / recv-bytes / close + fault) selected by scheme.
- **FR-059**: Cross-runtime parity is REQUIRED: a single role-parameterized program MUST be splittable across one Dart instance and one C# instance joined by one link, and MUST produce results equivalent to the unsplit single-instance run.
- **FR-060**: The on-the-wire format MUST be byte-identical across the Dart and C# runtimes (the serializer is already byte-parity; the deliverable is a real transport plus an executed Dart↔C# round-trip test, neither of which exists today).
- **FR-061**: The reliability sublayer (sequence/dedup keys, framing, version byte, length/CRC, fragmentation) MUST be behaviour-identical across both runtimes, such that either runtime can be on either end of any link.
- **FR-062**: Cross-runtime parity is a release gate: an executed Dart↔C# round-trip test over a real transport MUST pass before the feature is shippable.

#### Platform Acceptance (T4)

- **FR-063**: Acceptance for each transport leaf is satisfied when the leaf runs (per the FR-016 feasibility test) on at least ONE of Windows OR Android. A leaf is NOT required to run on every platform; a leaf that is feasible on only a single platform MUST be accepted as single-platform and documented as such, and single-platform status MUST NOT block the leaf's acceptance.
- **FR-064**: Where a transport leaf is blocked on a platform (e.g., an OS-restricted Bluetooth or background-transport case), that block MUST be explicitly recorded as an accepted single-platform/blocked case with its rationale, and MUST NOT count as a feature failure provided the leaf meets FR-063 on at least one of Windows or Android.

#### GEPA / DSPy Verify-Loop

- **FR-065**: Each primitive (sender/receiver per protocol) MUST have a DEFINED and VERIFIED success metric — a round-trip equivalence / fidelity test — and an experiment→verify→refine loop baked in from the start.
- **FR-066**: The LM verify-loop MUST run in the Claude harness via Agent seams ONLY (HARD RULE). It MUST NEVER use OpenAI / litellm / `OPENAI_API_KEY` or any external LM API; any contract clause mandating such an API is a defect to remove, not a constraint to honor.

#### Baseline Regression Gate

- **FR-067**: The baseline REPL test suite (`bash test/run_all_tests.sh`) MUST be green before and after every change that touches core (heap bind/guard, runner guard evaluator, SRSW analyzer, parser), and the prelude's `=\=`-gated arithmetic MUST still load. No core-touching change may merge over a red baseline.

### Key Entities

- **Link**: A strictly bilateral (peer-to-peer) connection between exactly two GLP REPL instances over one transport, replacing a single shared writer/reader logic-variable pair across instances. Identified by a stable link identity; owns a data path, a fault-monitor stream, and reliability-sublayer state (sequence/dedup, epoch/fence); preserves per-link FIFO. A broker, where present, is a transport relay UNDER a Link, never a logical hub.
- **LinkId / global-name**: The unique, never-reused identifier of a Link end (the cross-instance analogue of the in-process global writer/reader name). The basis of at-most-once idempotency and of origin checks; distinctness and equality of LinkIds are tested with existing guards.
- **Peer / AgentId**: A ground identifier naming a participating REPL instance. The role-parameterized program branches on the ground `AgentId` to select sender vs receiver behavior (one program, not a fork). Peer-ids MAY be non-numeric compound terms requiring a total order; leader-election / sorted-peer-set use cases are in scope (Clarification 2026-06-06), so the standard-order `@<`/`@>`/`@=<`/`@>=` guards are required (FR-037).
- **Frame**: The on-the-wire unit crossing a Link. Carries a payload plus reliability metadata — a per-link sequence number (FIFO/reorder), an epoch / fencing token (split-brain defense), a version byte, and a length/CRC (integrity); large payloads fragment and reassemble. A duplicate Frame is absorbed as a no-op. Byte-identical across Dart and C#.
- **Monitor stream + fault term**: A per-link stream on which faults surface as ordinary bound ground terms read with existing guards — never a fourth unification verdict and never a new guard outcome. Fault terms form the lattice `ok` / `tempFail` / `permFail` (`tempFail` is the default for silence and is recoverable via idempotent redelivery; `permFail` is a deliberate, possibly-wrong give-up). A goal that does not read the monitor stays suspended.
- **Global Name + Epoch/Fence**: The unique, never-reused name of a distributed binding plus a fencing token that orders competing writers and defeats split-brain double-binds.
- **Transport leaf**: The per-protocol, per-platform implementation that opens a Link and moves bytes for one scheme (MQTT, AMQP 1.0 p2p, CoAP, HTTP/2, HTTP/3, XMPP, DDS, WebSocket, SSH tunnel, FTP, SFTP, file endpoints, BLE LE-Audio BIS/CIS, L2CAP CoC, BLE GATT, BR/EDR SPP; HTTP/2, HTTP/3 and WebSocket each in plain and TLS variants). Each leaf provides a symmetric sender+receiver and a server-listener+client-connector, sits behind one transport seam, and may be single-platform (T4: acceptance is one of Windows OR Android per leaf).
- **Link Transport Seam**: The uniform per-scheme interface (open / send-bytes / recv-bytes / close + fault) behind which each per-protocol leaf lives; the single seam both runtimes share.
- **Reliability sublayer**: The shared layer beneath every Transport leaf and above the language primitives that supplies per-link FIFO, sequence/dedup, idempotent redelivery, the serializer's cycle-guard + version byte + length/CRC + fragmentation, distributed-GC of dead links, and security (per-message origin authentication, replay window, deserializer hardening, TLS-by-default for inter-host links). Shared across all transports and byte/behaviour-identical across Dart and C#.
- **Reply / CorrId**: For request/reply over a ground-relay base link, a local writer/reader pair plus a ground correlation identifier and a reverse Link that route a remote answer back to the original requester. A reply table keyed by CorrId is tracked and reclaimed by distributed GC.
- **Base link primitives**: The behavioral set delivered FIRST — request-link, accept-link, setup, sender, receiver, server-listener, client-connector, plus a per-link fault monitor. Concrete signatures, arities, and modes are co-designed at the plan gate, not fixed here.
- **glink (out of MVP scope)**: A LATER higher-level construct, built strictly ON TOP of the base link primitives, that distributes the full writer/reader variable for transparency. The dependency runs base → `glink`, never the reverse; full `glink` transparency is out of scope for this feature's MVP.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001 (Headline split equivalence).** A single-instance `producer(X) / consumer(X?)` program, split role-parameterized across two REPL instances over the simplest available transport (loopback or file), produces byte-identical observable results to the unsplit single-instance run — verified first Dart↔Dart, then Dart↔C#. The Dart↔C# case is the mandated cross-runtime parity gate and MUST pass before the feature ships.
- **SC-002 (Cross-runtime link parity).** At least one transport leaf carries one complete writer→reader bind between a Dart REPL instance and a C# REPL instance, with the reconstructed value on the receiving side equal to the value sent. (Today neither a real transport nor an executed Dart↔C# round-trip exists; this SC marks both built.)
- **SC-003 (Per-transport bind reactivation, T4).** Each shipped transport leaf opens a bilateral link and carries at least one writer→reader bind that reactivates a previously-suspended reader exactly once, demonstrated on at least one platform (Windows OR Android) per leaf. A leaf is "shipped" only when this test passes for it.
- **SC-004 (Guard three-valued conformance).** Every new or changed guard passes three behavioral cases as REPL Section-A runtime tests plus Section-B/C type-check tests: (a) success on ground/satisfying operands; (b) suspend on an unbound reader, then reactivate exactly once when the reader is bound; (c) fail on an unbound writer (or definite mismatch). This holds for the same compound and remote-operand inputs that exercise the fixed compound-operand-suspend and imported-reader-reactivation paths.
- **SC-005 (`atom/1` consistency).** `atom/1` behaves identically across compile-time (analyzer/SRSW) and runtime (the runner guard evaluator) — no input that the analyzer accepts and grounds is allowed to fail at runtime, and vice versa.
- **SC-006 (SRSW preserved under comparison guards).** A clause that reads a variable grounded by a comparison guard (e.g., `=?=`, `@<`) compiles successfully; the same clause without a ground-implying guard on that variable is rejected by the SRSW analyzer. SRSW is never relaxed by an option flag.
- **SC-007 (Adversarial / security corpus parity).** The full adversarial corpus — forged-origin frames, index-enumeration / cold-call flooding, malformed / oversized / cyclic / huge-arity frames, bad-version / bad-CRC frames, relayed stdin/stdout abuse — produces identical verdicts on BOTH the Dart and the C# REPL, with every malformed input failing safe within bounded memory and stack (no OOM, no crash, no isolate kill). Plain (non-TLS) inter-host links are refused by default.
- **SC-008 (Idempotent redelivery is a verified no-op).** Delivering the same writer/reader assignment twice (and a third time after entry removal) is a verified no-op — no error raised, no error swallowed, no re-bind, no goal re-enqueue. (Baseline today: the second delivery crashes the agent; this SC marks that closed.)
- **SC-009 (Suspend-not-fail across the cut).** A guard reading a remote operand whose value has not yet arrived suspends rather than fails — including a nested unbound reader inside a compound term, and including a reader represented via the imported-reader path — and wakes exactly once on bind. No un-arrived remote value ever produces a spurious logical FAIL.
- **SC-010 (Fault liveness).** When a peer node is killed mid-bind, the reader's suspended goal does NOT spuriously fail; a `tempFail` fault term and, on give-up, a `permFail` fault term appear on the per-link monitor stream within a bounded time, and a fault-guarded clause becomes reducible. A goal not reading the monitor stream stays safely suspended.
- **SC-011 (Split-brain defense).** When two writers (one stale, one reconnected/fenced) deliver different values for one global name, exactly one wins (by epoch/fencing token), the loser yields a `permFail` fault, and there is never a silent overwrite and never a downstream double-reduction.
- **SC-012 (Reorder / loss recovery).** With the reliability sublayer engaged, dependent frames delivered out of order, dropped, or duplicated reconstruct a result equal to the in-order single-instance run; with the sublayer disabled, the test detects corruption rather than silently building a wrong result.
- **SC-013 (Backpressure bound).** Under a fast producer and a stalled consumer, the outbound queue stays bounded (the producer suspends), with no OOM and no head-of-line blocking across independent links.
- **SC-014 (Distributed GC).** After opening then permanently failing N links, all per-link resources (global-name entries, registry goals, heap bind callbacks, and reply-table entries where applicable) return to baseline, with no unreclaimable cycle.
- **SC-015 (GEPA round-trip fidelity per primitive).** Each transport primitive (sender/receiver per protocol) has a defined and verified success metric — a round-trip equivalence / fidelity test — and passes it through an experiment→verify→refine loop. This optimization loop runs exclusively in the Claude harness via Agent seams; no run touches OpenAI / litellm / `OPENAI_API_KEY`.
- **SC-016 (Stream reroute fidelity).** With stdio rerouting enabled under an explicit capability, a known input sequence supplied at the remote end is consumed as the local REPL's `stdin` and the local `stdout`/`stderr` are surfaced byte-equivalent at the remote end with the two streams kept distinct; rerouting requested without the capability is refused and relayed control sequences are sanitized.
- **SC-017 (Baseline regression gate).** `bash test/run_all_tests.sh` is green before and after every change that touches core (heap bind/guard, runner guard evaluator, SRSW analyzer, parser); the `=\=`-gated division/mod in `self.glp` still loads. No core-touching change merges over a red baseline.

## Assumptions

- **GLP semantics are preserved exactly** and are non-negotiable: SRSW (one reader / one writer per variable per clause, never relaxed by a flag); writer-MGU (binds only writers, never reader/reader or writer/writer); three-valued unification (an un-arrived remote value behaves as an unbound local reader → Suspend, never spurious Fail); suspend-on-reader / reactivate-on-bind; bind-once monotonicity; per-link FIFO.
- **The payload serializer is already byte-parity** between the Dart and C# runtimes; the open gap is a real transport plus an executed Dart↔C# round-trip test, not the serialization format.
- **C# is the priority + reference implementation** (`out/csharp`, the mandated-default GLP REPL, verified building and running GLP); the Dart mirror is authored only after the C# reference works fully. Hand-authored C# transport/primitive code lives in a home where a `codeconv mirror`/scaffold regeneration cannot clobber it (outside `out/csharp` and the gitignored generated tree).
- **One role-parameterized program** (branch on ground `AgentId`) is the decomposition; a two-version fork is not the default and is used only as a justified escape hatch.
- **Base link primitives are implemented first**; `glink` (full variable-distribution transparency) is a later layer built on top and is out of MVP scope. The required hardening and bug fixes (idempotent redelivery, per-link FIFO/dedup, serializer framing/version/CRC, imported-reader reactivation, compound-operand suspend) are part of building the primitives correctly — not a separate gate that blocks starting.
- **Faults are data, not control surprises**: they surface as ordinary bound terms on a per-link monitor stream over the `ok` / `tempFail` / `permFail` lattice; disconnect never maps to logical FAIL; epoch/fencing defends split-brain. Faults are not a fourth unification verdict and not a new guard outcome.
- **Concrete primitive signatures, arities, and modes** for request-link / accept-link / setup / sender / receiver / server-listener / client-connector and the per-link fault monitor are co-designed at the plan gate under language-authority approval; this specification fixes only behavior and constraints.
- **The GEPA/DSPy verify-loop runs in the Claude harness via Agent seams only** — never OpenAI / litellm / `OPENAI_API_KEY`. Each primitive carries a defined, verified round-trip fidelity metric inside an experiment→verify→refine loop from the start.
- **T4 acceptance is one platform per leaf**: each transport leaf need only run on at least one of Windows OR Android; not every leaf must be cross-platform.
- **"Inter-host" means the two link ends reside on different hosts**; loopback and co-located in-process links are not inter-host and may use PLAIN variants. Faults are deemed `permFail` only after a bounded, configurable silence/give-up interval (the default until then is `tempFail`); the exact bound is co-designed at the plan gate and is a tuning parameter, not a correctness condition.
- **BLE BIS true-multi-reader stays in scope as an open co-design goal**: this feature's MVP may ship broadcast via the N-bilateral-ground-copy model while the BIS-vs-SRSW reconciliation remains tracked and unresolved.

### Dependencies

- **`docs/guards-reference.md`** as the authoritative guard spec; the `comparison-guards` work is kept and implemented (not cancelled), folded against this reference.
- **`codeconv` toolchain** (Dart→C# conversion + per-file C# build-gate + cross-runtime parity tests) for keeping the C# reference and Dart mirror in correspondence; the new multiagent/runner and security-critical reliability code is first-class conversion scope, not a follow-up.
- **marathon-stage-harness** (feature 024, `codeconv.marathon`) for the durable cross-session checkpoint + compaction/crash-recovery that carries this multi-stage feature.
- **The shared reliability sublayer** is a prerequisite for every transport leaf and for both the base primitives and the later `glink` layer — it is the load-bearing net-new engineering, not optional polish.
- **Per-platform native transport leaves** sit behind one transport seam; each leaf depends on its platform's native protocol library and is registered by scheme.

**Resolved (Clarification 2026-06-06, ruling B):** Peer-ids MAY be non-numeric compound terms requiring a total order (leader-election / sorted-peer-set use cases are in scope). The `@<`/`@>`/`@=<`/`@>=` standard-order guards are therefore IN SCOPE and required (FR-037), added under language-authority approval — not declined.
