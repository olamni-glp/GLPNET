# Contract: Base Link Primitives (PROPOSED) — feature 025 multi-protocol-link-layer

> 🔴 **SUPERSEDED 2026-07-20 by `contracts/rulings-log.md` — THE GATE IS CLOSED AND THIS SURFACE IS RATIFIED.**
> The "Status: PROPOSAL … NOTHING here is decided" paragraph below is **STALE** and must not be read as
> current. `rulings-log.md` records **"PLAN-APPROVAL GATE: COMPLETE. The 9 base link primitives + the
> approved guard set + the three core fixes are approved-to-implement under language authority"**
> (Gabi, 2026-06-06), extended by the T033 establishment-path-B ratification (Gabi, 2026-06-07) which
> adds the 10th primitive `request_listener/2` + `'_link_listen'/3`. Names and arities are ratified
> **AS WRITTEN**. **Work on this surface is NOT §1.14-gate-blocked.**
>
> This stale header has already cost one near-false escalation. **Authority order:** shipped surface
> (`programs/self.glp`) + `rulings-log.md` **>** this document's body **>** proposal docs
> (`architecture-context.md`) **>** the C# reference **>** the Dart reference.
>
> Note also: the entire GLP-side surface designed here **already ships** in `programs/self.glp`
> (declarations + wrappers, relocated Gabi-approved in `6c21281e`). Remaining work is host-side
> kernels only — see `specs/050-full-gleam-combined/contracts/link-primitives-port.md`.

**Status (STALE — see the superseding banner above):** PROPOSAL pending Gabi's language-authority approval (CLAUDE.md §Language Authority; DISCIPLINE §1.14). **NOTHING here is decided.** Every name, arity, mode, guard, system-predicate, body-kernel, and directive below is marked **PROPOSED** and is presented as input to the language-authority co-design gate. Approve / revise / decline each item; do not treat any as ratified.

**Scope.** This contract designs ONLY the **BASE link primitives** named in FR-001 / Key Entities "Base link primitives": request-link, accept-link, link setup, sender, receiver, server-listener, client-connector, per-link fault monitor. Per the RULED contract (`docs/research/multi-protocol-link-layer/B2-B3-G-decision.md` §"Decisions — RULED (Gabi, 2026-06-06)") and FR-009/FR-010, the base set is the **first deliverable**, the base discipline is a **GRL-style ground-relay** (FR-010, "ground-relay base carries the binding across the cut"), and **`glink` (full writer/reader variable distribution) is a LATER higher-level layer built ON these — explicitly OUT OF MVP SCOPE and NOT designed here.** Dependency direction is base → glink, never the reverse.

**Source precedence honored.** Tier-1 local specs/code (self.glp, heap_fcp.dart, mad_context.dart, guards-reference.md) ground every signature; the corpus (Tier-1 glp-current and Tier-2 GLP papers) provides the model; Tier-3 (FCP/Oz/ISO) is mechanism-inspiration only and never overrides Tier-1.

---

## 0. Grounding — the real GLP idioms and runtime APIs these primitives sit on

The base primitives are deliberately a **thin GLP surface over the already-present madGLP ground-relay machinery**, so existing `self.glp` composition holds ABOVE the seam and the conversion-to-C# surface stays small.

Real GLP idioms reused verbatim (cited):
- `Channel(In, Out) ::= ch(In, Out?).` — `programs/self.glp:16`. A link end is PROPOSED to present as a `Channel(In,Out)` so `send/3` / `receive/3` / `mwm/2` compose above the seam.
- `procedure new_channel(Channel(X, Y), Channel(Y, X)). new_channel(ch(Xs?, Ys), ch(Ys?, Xs)).` — `self.glp:90-91`.
- `procedure send(X?, Channel(Y, Stream(X))?, Channel(Y, Stream(X))). send(X, ch(In, [X?|Out?]), ch(In?, Out)).` — `self.glp:93-94`.
- `procedure receive(X, Channel(Stream(X), Y)?, Channel(Stream(X), Y)). receive(X?, ch([X|In], Out?), ch(In?, Out)).` — `self.glp:96-97`.
- `procedure _send(_?, _?, _?).` — the madGLP network primitive — `self.glp:69`; body-kernel registered `body_kernels.dart:99`, body `sendKernel` at `body_kernels.dart:658-745`, dispatching to `MadContext.send(term, isWriter, gnAgent, gnIndex, destAgent)` (`body_kernels.dart:742`; runtime body `mad_context.dart:501`). **Load-bearing constraint (verified):** `'_send'/3` ABORTS unless its second argument `G` is a `_w/2` or `_r/2` struct (`body_kernels.dart:683-697`) and it runs the madGLP **globalize** path — it is NOT a pure ground-relay. A pure ground-relay base that puts no `_w`/`_r` placeholder on the wire therefore CANNOT simply call `'_send'/3` with a `LinkId`; the base needs either (a) a NEW ground-relay body-kernel, or (b) reuse of `'_send'`'s index-0 serializer cold-call form `_w(Q,0) := [T↑ | _w(Q,0)]` (which already carries a ground term as a list head into the peer's network-input stream — `mad_context.dart:254-318`). See OQ-3 and the §3 correction.
- `procedure _output(_?).` — `self.glp:73`.
- Ground-relay output discipline (`send_to_ui`/`'_send_to_ui'` guards `ground/1`, never globalizes) — corpus `14-ground-only-transport-...md` §4. **The base layer adopts exactly this ground gate** (no `_w`/`_r` placeholders on the wire) per FR-010 ground-relay.

Real runtime APIs the C#-first reference (and Dart mirror) bind to (cited):
- `heap.allocateVariable() -> (writerAddr, readerAddr)` — `heap_fcp.dart:85`.
- `heap.bindWriter(writerAddr, value) -> List<GoalRef>` activations — `heap_fcp.dart:350`; `bindVariable` is used by `handleMadAssignment` — `mad_context.dart:306/355/402`.
- `heap.onBind(writerAddr, cb)` — fires when a writer is bound — `mad_context.dart:163,208,463`.
- `enqueueReactivatedGoal(act)` — re-schedules a woken goal — `mad_context.dart:316`.
- `MadContext.onMessageReady` / `flushMessages()` / `MessageQueue mp` — the outbound seam — `mad_context.dart:46,87,160`.
- `handleMadAssignment({globalName, value, fromAgent})` — the inbound ingress — `mad_context.dart:229`.
- Role/boot seam: arg-0 is a ground `AgentId` constant, last arg is `NetIn` — `agent_runtime.dart:202-226`. **Role selection is branch-on-ground-AgentId** (FR-011), one program, the existing `@`/boot idiom.
- Guard evaluator `_evaluateGuard(predicateName, args, cx)` — `runner.dart:4249`; `=?=` arm at `runner.dart:4669`; `@<`/`@>`/`==`/`\==` genuinely absent (default WARN+fail at `runner.dart:4690`).

**Verified-live correctness caveats that constrain these primitives (from the decision doc, re-confirmed against code):**
1. Duplicate inbound delivery **crashes today**: `_handleWriterAssignment`/`_handleReaderAssignment` throw `StateError` on a second delivery after one-shot entry removal (`mad_context.dart:330,377`); `bindWriter` throws on an already-`ValueTag` cell (`heap_fcp.dart:365`). FR-021 idempotency is therefore a **net-new gate the primitives must route through**, not an existing property.
2. A guard suspended on a genuine writerless **imported reader** never reactivates: `handleMadAssignment` calls only `bindVariable`, never `bindImportedReader` (`heap_fcp.dart:641`); suspension lives on `VariableEntry.suspensions` (`heap_fcp.dart:496`). FR-035.
3. A guard over a **compound operand with a nested unbound reader** wrongly FAILS instead of SUSPENDing (top-level gate passes, `_termsEqual` returns false). FR-034.

The base primitives are specified so that the **ground gate sidesteps (1)–(3) for the MVP**: ground terms carry no embedded readers, so the base relay never crosses an open structure. The fixes are still required (FR-021/034/035), but base correctness does not depend on the open-structure path — that dependency belongs to `glink` (later).

---

## 1. Shared types and the transport seam (PROPOSED)

These type declarations are PROPOSED additions to a new prelude module (e.g. `programs/lib/link.glp`), NOT to `self.glp` itself, so the prelude stays untouched (FR-067 baseline gate).

```prolog
% --- PROPOSED link-layer types ---

% A stable, ground link identity. Cross-instance analogue of the in-process
% global writer/reader name (Key Entities: LinkId / global-name). Never reused.
% Compound so it can carry scheme + endpoint + a uniqueness nonce, all ground.
LinkId ::= link_id(Scheme, Endpoint, Nonce).
Scheme   ::= String.                  % e.g. "ws", "wss", "file", "mqtt", "coap"
Endpoint ::= String ; ep(String, Integer).   % host/path, or host+port
Nonce    ::= Integer ; String.        % per-establishment uniqueness (idempotency basis)

% A ground peer/agent identifier. MAY be a non-numeric compound term requiring a
% total order (leader-election / sorted-peer-set — FR-037 / Clarification 2026-06-06).
AgentId ::= String ; Integer ; peer(String, Integer).

% Establishment role — a deployment/NAT concern, INDEPENDENT of data direction (FR-004).
LinkRole ::= listener ; connector.

% Fault lattice carried as ORDINARY BOUND GROUND TERMS on the monitor stream
% (FR-043: never a 4th unification verdict, never a new guard outcome). FR-045.
% closed/2 is a Fault member (Gabi ruling 2026-06-06): a clean/intentional close
% emits a terminal closed(LinkId, Reason) term (Reason = eos for graceful), distinct
% from tempFail/permFail.
Fault ::= ok ; closed(LinkId, Reason)
        ; tempFail(LinkId, Reason) ; permFail(LinkId, Reason).
Reason ::= String ; _.

% A live link end presented to GLP logic as a Channel so self.glp send/receive/mwm
% compose ABOVE the seam (FR-006: no transport detail leaks into logic).
% In = inbound ground-term stream from the peer; Out = outbound ground-term stream.
Link(In, Out) ::= Channel(Stream(In), Stream(Out)).   % i.e. ch(Stream(In), Stream(Out)?)
```

**The Link Transport Seam (Key Entities: "Link Transport Seam").** PROPOSED to live BELOW GLP entirely — a host-language interface (C# first, Dart mirror), NOT a GLP primitive — exactly the uniform `open / send-bytes / recv-bytes / close + fault` seam selected by `Scheme` (FR-058). It is named here only so the GLP primitives below can reference it; its concrete C#/Dart signature is a host-interface co-design item (see Open Questions), not a language-authority item.

---

## 2. The eight base primitives (PROPOSED)

For each: PROPOSED name; full signature with modes; HEAD/GUARD/BODY sketch in valid GLP with type + procedure declarations; semantics; invariants touched + how preserved; language-authority status; base tag.

Mode notation: `X?` reader (input), `X` writer (output), `+`/`-`/`?` annotate intended instantiation in prose.

---

### 2.1 PROPOSED `link_setup/4` — link setup (idempotent at link-identity) `[base:setup]`

```prolog
% PROPOSED. Establish-or-reuse a link by its ground LinkId, in a given role,
% over the transport named inside LinkId's Scheme. Idempotent at link-identity (FR-007).
% Role (listener|connector) is independent of who later writes (FR-004).
procedure link_setup(LinkId?, LinkRole?, Link(_, _), FaultStream?).
FaultStream ::= Stream(Fault).

link_setup(LinkId, Role, ch(In?, Out), Faults?) :-
    ground(LinkId?), ground(Role?) |
    '_link_setup'(LinkId?, Role?, In, Out?, Faults).
```

- **Semantics.** Given a ground `LinkId` and a ground establishment `Role`, open (or, if already established under the same `LinkId`, REUSE) the underlying transport leaf, and hand back (a) a `Link` channel `ch(In?, Out)` whose `In` is the inbound ground-term stream and `Out` the outbound ground-term stream, and (b) a per-link `Faults` monitor stream. Re-invoking `link_setup` with the same ground `LinkId` returns the already-established link rather than a conflicting duplicate (FR-007 idempotent-at-identity); the underlying `'_link_setup'` consults a per-instance LinkId→handle registry keyed by the ground `LinkId`.
- **Invariants touched & preserved.**
  - *SRSW (FR-048):* `LinkId?`/`Role?` appear once each as readers under a `ground/1` guard (ground-implying ⇒ SRSW relaxation per guards-reference §"Ground Guards"). The three produced outputs follow the output-hole idiom (manual §19.4): `In` is a reader hole `In?` in the head paired with the body writer `In`; `Out` is the head writer (the caller fills it) paired with the body reader `Out?`; `Faults` is a reader hole `Faults?` in the head paired with the body writer `Faults`. Each cell has exactly one reader and one writer — fresh local pairs minted by `'_link_setup'`.
  - *Three-valued / suspend-not-fail (FR-017/FR-050):* an unbound `LinkId?` or `Role?` SUSPENDS on the `ground/1` gate (patient), never spuriously fails.
  - *Idempotency (FR-007/FR-052):* registry keyed by the never-reused ground `LinkId` makes re-setup a no-op-returning-the-same-handle; bind-once preserved because the `In`/`Out`/`Faults` writers are bound exactly once at first establishment.
  - *Bilateral (FR-005):* `'_link_setup'` opens exactly one transport leaf to exactly one peer; a broker, if the scheme needs one, is a relay UNDER this seam, never a logical hub.
- **Language-authority status: NEW system-predicate** `'_link_setup'/5` (host-implemented, no GLP clauses), PLUS the GLP wrapper `link_setup/4` (composable from `ground/1` + the new predicate). Approve the predicate; the wrapper is ordinary GLP.

---

### 2.2 PROPOSED `server_listener/4` — server-listener establishment `[base:listen]`

```prolog
% PROPOSED. Establish a link by LISTENING (accepting an inbound transport connection).
% Thin role-specialization of link_setup with Role = listener. (FR-002 path A, this end.)
procedure server_listener(LinkId?, Link(_, _), FaultStream?).
server_listener(LinkId, Link?, Faults?) :-
    ground(LinkId?) |
    link_setup(LinkId?, listener, Link, Faults).
```

- **Semantics.** Bind this end as the transport server for `LinkId`; on a peer connecting, the SAME established link results as `client_connector` produces on the other end (FR-002: both paths yield an equivalent established link). Which side listens is a deployment/NAT concern only; this end may still be the data writer OR reader afterward (FR-004).
- **Invariants.** Inherits all of `link_setup/4`'s. Symmetric data capability (FR-003) is preserved because the returned `Link` exposes both `In` (receiver capability) and `Out` (sender capability) regardless of establishment role.
- **Language-authority status: composable** — a single-unit-ish GLP wrapper over `link_setup/4`. No new language item. (Listed because it is one of the eight named behavioral primitives.)
- **Base tag:** `[base:listen]`.

---

### 2.3 PROPOSED `client_connector/4` — client-connector establishment `[base:connect]`

```prolog
% PROPOSED. Establish a link by CONNECTING (initiating the transport connection).
% Thin role-specialization of link_setup with Role = connector. (FR-002 path A, other end.)
procedure client_connector(LinkId?, Link(_, _), FaultStream?).
client_connector(LinkId, Link?, Faults?) :-
    ground(LinkId?) |
    link_setup(LinkId?, connector, Link, Faults).
```

- **Semantics.** Symmetric counterpart to `server_listener/3`: initiate the transport connection for `LinkId`. Pairing a `server_listener` on instance A with a `client_connector` on instance B yields one established bilateral link (FR-002).
- **Invariants.** Same as `server_listener/3`. Establishment role independent of data direction (FR-004): a connector end may be the writer.
- **Language-authority status: composable** over `link_setup/4`. No new language item.
- **Base tag:** `[base:connect]`.

---

### 2.4 PROPOSED `request_link/4` + `accept_link/4` — the second establishment path (handshake) `[base:request]` / `[base:accept]`

FR-002 mandates a SECOND establishment path — a `request-link` / `accept-link` handshake — yielding an equivalent established link. This is a logical (in-band) handshake over an already-reachable rendezvous, distinct from the transport listen/connect pairing.

```prolog
% PROPOSED. Initiate an in-band link-request handshake to a peer over a rendezvous link.
% Sends a ground request token carrying the desired LinkId; on the peer's accept,
% both ends hold the equivalent established Link (FR-002).
procedure request_link(LinkId?, AgentId?, Link(_, _), FaultStream?).
request_link(LinkId, ToPeer, ch(Link_In?, Link_Out), Faults?) :-
    ground(LinkId?), ground(ToPeer?) |
    '_link_request'(LinkId?, ToPeer?, Link_In, Link_Out?, Faults).

% PROPOSED. Accept an inbound link-request for a LinkId this end is willing to serve.
% Reads a request token off RequestStream; on a match, establishes the equivalent Link.
procedure accept_link(LinkId?, Stream(request(LinkId, AgentId))?, Link(_, _), FaultStream?).
accept_link(LinkId, [request(LinkId2, FromPeer)|_], ch(Link_In?, Link_Out), Faults?) :-
    ground(LinkId?), LinkId? =?= LinkId2? |
    '_link_accept'(LinkId?, FromPeer?, Link_In, Link_Out?, Faults).
```

- **Semantics.** `request_link` sends a ground `request(LinkId)` token to `ToPeer` and parks until the peer accepts, then yields the established `Link` + `Faults`. `accept_link` consumes a `request(LinkId2, FromPeer)` token from its inbound `RequestStream`; when the requested `LinkId2` matches a `LinkId` this end will serve (tested by `=?=` over ground terms), it establishes the equivalent link. The two establishment paths (2.2/2.3 listen/connect, and 2.4 request/accept) MUST produce an indistinguishable established `Link` (FR-002) — both ultimately route through the same `'_link_setup'` registry, so downstream data/fault behavior is identical.
- **Invariants touched & preserved.**
  - *SRSW (FR-048):* `LinkId?` ground-guarded (relaxation permits its two reader occurrences in `accept_link`: once in `ground/1`, once in `=?=`); `LinkId2?`/`FromPeer?` read once from the head list cell. The produced `Link` is **head-constructed** as `ch(Link_In?, Link_Out)` (no body `=`; outputs are built in clause heads — cheat-sheet Rule 1): `Link_In` = kernel writer + `Link_In?` reader-hole in the head `ch`; `Link_Out` = `Link_Out` writer in the head `ch` + kernel reader `Link_Out?`; `Faults` = head reader-hole `Faults?` + kernel writer. Each cell has exactly one reader and one writer. **Note:** this clause is the SRSW-correct rewrite of the decision doc's FLAGGED `out_relay` clause (which read `LinkId?` twice with no ground guard) — the fix is exactly the `ground(LinkId?)` gate (see §3).
  - *Three-valued (FR-017/FR-050):* an unbound `LinkId?`/`ToPeer?` SUSPENDS on `ground/1`; an unarrived `request` token leaves `accept_link` SUSPENDED on the stream head reader, never failed.
  - *`=?=` ask-semantics (FR-039):* `LinkId? =?= LinkId2?` succeeds on ground-equal, suspends on an unbound reader, fails on an unbound writer / ground-unequal — already-implemented behavior (`runner.dart:4669`), used here unchanged.
  - *Bilateral (FR-005):* one request ↔ one accept ↔ one link.
- **Language-authority status: NEW system-predicates** `'_link_request'/5` and `'_link_accept'/5` (host-implemented). The GLP wrappers are composable from `ground/1`, `=?=`, and head unification. Approve the two predicates.
- **Base tags:** `[base:request]`, `[base:accept]`.

---

### 2.5 PROPOSED `link_send/3` — sender (the ground-relay out-relay) `[base:send]`

This is the corrected `out_relay` from the decision doc. It is the **ground-relay discipline** that carries the binding across the cut (FR-010): only GROUND terms cross; nothing with an embedded reader is ever placed on the wire (corpus 14 §4 ground gate).

```prolog
% PROPOSED. Send one GROUND term out over the link end's Out stream (ground-relay).
% Symmetric: either end may call it (FR-003). The corrected, SRSW-clean out_relay:
% LinkId is ground-guarded so its (single) reader use is legal under the relaxation,
% and crucially the PAYLOAD is gated GROUND so no _w/_r placeholder ever crosses.
procedure link_send(Term?, Link(In, Term)?, Link(In, Term)).
link_send(Msg, ch(In, [Msg?|Out?]), ch(In?, Out)) :-
    ground(Msg?) | true.
```

- **Semantics.** Append a ground `Msg` to the link's `Out` stream tail; the transport leaf serializes and ships it. Pure stream-cons in the head (the `self.glp:94` `send/3` shape), gated `ground(Msg?)` so the relay never crosses an open structure. The far end's `link_recv/3` binds the value into a local cell (writer-MGU), reactivating any suspended reader exactly once (FR-017/FR-051).
- **Invariants touched & preserved.**
  - *SRSW (FR-048):* `Msg?` appears twice (guard + head cons) — legal ONLY because `ground(Msg?)` certifies groundness (guards-reference §"Ground Guards - SRSW Relaxation": a ground-implying guard permits multiple reader occurrences). This is the precise fix for the decision doc's flagged SRSW violation. `In`, `Out` thread through once each.
  - *Ground-relay / no distributed variable (FR-010, FR-040):* `ground(Msg?)` guarantees no `_w`/`_r` placeholder, no embedded reader, ever reaches the wire — so the base layer carries a COPY of a ground value (FR-040 broadcast model is N of these), never a shared unbound variable. This is what keeps the base independent of the (still-buggy) open-structure path and defers transparency to `glink`.
  - *Three-valued (FR-017/FR-050):* an unbound `Msg?` SUSPENDS on `ground/1` until the producer binds it — never a spurious fail.
  - *Per-link FIFO (FR-018/FR-053):* stream-cons order = send order; the transport leaf + reliability sublayer preserve it on the wire.
  - *Writer-MGU (FR-049):* `link_send` itself only reads `Msg?` and writes the stream tail; the cross-link bind happens at the receiver's local writer (see `link_recv`), binding only a local writer.
- **Language-authority status: composable GLP wrapper, but it leans on a send seam that needs a ruling (OQ-3).** The wrapper `link_send/3` is pure GLP over `ground/1` + head unification (the `self.glp:94` `send/3` shape); the only thing it leans on is `'_link_setup'` having produced the `Out` stream, whose tail-bind is drained by the transport leaf. **It does NOT lower to the existing `'_send'/3`:** that kernel aborts unless `G` is a `_w/2`/`_r/2` struct and runs globalize (`body_kernels.dart:683-697,742`) — the madGLP global-link path, not a ground-relay. So the base ground-relay send requires either **(a) a NEW body-kernel** `'_link_send'(GroundTerm?, LinkId?, AgentId?)` that ships a ground frame with no globalize, or **(b) reuse of `'_send'`'s index-0 serializer cold-call** form `_w(ToPeer,0) := [Msg↑ | _w(ToPeer,0)]` (already ground-friendly, `mad_context.dart:254-318`). Recommendation: (a) — a tiny ground-only kernel keeps the base discipline explicit and avoids overloading the index-0 serializer. This makes the SENDER one of the language-authority items either way (NEW kernel under (a); under (b) it is composable but pins index-0 semantics). See OQ-3.
- **Base tag:** `[base:send]`.

---

### 2.6 PROPOSED `link_recv/3` — receiver (the in-relay) `[base:recv]`

```prolog
% PROPOSED. Receive the next term arriving on the link end's In stream (in-relay).
% Symmetric: either end may call it (FR-003). Pure stream-decons (the self.glp receive/3
% shape); the actual cross-link bind into In's tail is performed by the inbound ingress
% machinery (handleMadAssignment) when a frame arrives.
procedure link_recv(Term, Link(Term, Out)?, Link(Term, Out)).
link_recv(Msg?, ch([Msg|In], Out?), ch(In?, Out)).
```

- **Semantics.** Read one element `Msg` off the link's `In` stream head and return the advanced channel. The `In` stream tail is extended by the inbound ingress (`handleMadAssignment` → `bindVariable` → activations → `enqueueReactivatedGoal`, `mad_context.dart:306-318`) as frames arrive over the transport. A `link_recv` whose element has not yet arrived SUSPENDS on the unbound stream-head reader and REACTIVATES exactly once when the value binds (FR-017/FR-051).
- **Invariants touched & preserved.**
  - *SRSW (FR-048):* the `self.glp:97` `receive/3` shape verbatim — `Msg?` (writer in head returned to caller), `In`/`Out` thread once each; no extra reader/writer.
  - *Three-valued / suspend-not-fail (FR-017/FR-050/SC-009):* an unarrived value = unbound local reader at the stream head ⇒ SUSPEND, never FAIL. **Depends on FR-035 fix** (imported-reader reactivation) only if the In-stream tail is ever represented via `allocateImportedReader`; with the ground-relay base, `'_link_setup'` mints ordinary local pairs (`allocateVariable`), so the standard `bindVariable` activation path applies and the FR-035 hazard is avoided for the base MVP.
  - *Bind-once monotonic (FR-052):* the stream tail cell is bound exactly once per frame; idempotent redelivery (FR-021) must make a duplicate frame a verified no-op at the ingress, NOT re-bind this cell (the gate the primitives route through; see §4).
  - *Writer-MGU (FR-049):* ingress binds only the local writer of the In-stream tail pair; never reader/reader, never writer/writer.
  - *Per-link FIFO (FR-018):* the reliability sublayer reconstructs in-order before extending the stream (FR-020).
- **Language-authority status: composable** — pure GLP (the prelude `receive/3` shape). The net-new work is in the inbound ingress + reliability sublayer (host code), not the GLP surface. No new guard/kernel.
- **Base tag:** `[base:recv]`.

---

### 2.7 PROPOSED `link_monitor/2` — per-link fault monitor (independently observable) `[base:monitor]`

```prolog
% PROPOSED. Obtain the per-link fault MONITOR STREAM for an established LinkId.
% Faults are ORDINARY BOUND GROUND TERMS over the ok/tempFail/permFail lattice,
% read with EXISTING guards (=?=, compound, etc.) — NOT a 4th unification verdict,
% NOT a new guard outcome (FR-043). Independently observable from the data path (FR-008):
% reading Faults does not touch In/Out, and vice versa.
procedure link_monitor(LinkId?, FaultStream?).
link_monitor(LinkId, Faults?) :-
    ground(LinkId?) |
    '_link_monitor'(LinkId?, Faults).
```

- **Semantics.** Return the monitor stream `Faults` for the established `LinkId`. The transport leaf + reliability sublayer push `ok`, then on silence `tempFail(LinkId, Reason)` within a bounded interval, then on deliberate give-up `permFail(LinkId, Reason)` (FR-045/FR-046). A program reads it with ordinary guards, e.g.:
  ```prolog
  procedure on_fault(FaultStream?).
  procedure handle_perm(LinkId?, Reason?).   % illustrative external sinks (host/app handlers)
  procedure handle_temp(LinkId?, Reason?).
  on_fault([permFail(L, R)|_]) :- ground(L?) | handle_perm(L?, R?).
  on_fault([tempFail(L, R)|_]) :- ground(L?) | handle_temp(L?, R?).
  on_fault([ok|Rest])          :- on_fault(Rest?).
  ```
  A goal that does NOT read `Faults` stays safely suspended across a disconnect (FR-044); a fault NEVER maps to a logical Fail (FR-044/FR-050).
- **Invariants touched & preserved.**
  - *No 4th verdict (FR-043):* faults are data on a stream — they never enter `_evaluateGuard`'s success/suspend/fail trichotomy.
  - *Independent observability (FR-008):* `Faults` is a separate stream from `In`/`Out`; `link_monitor` shares only the ground `LinkId` with the data primitives (registry lookup), no cell aliasing ⇒ no SRSW interference.
  - *Three-valued (FR-050):* disconnect → a bound `tempFail`/`permFail` TERM, never a logical fail; an unmonitored suspended goal is undisturbed.
  - *SRSW (FR-048):* `LinkId?` ground-guarded (single relaxed reader use); `Faults` follows the output-hole idiom — reader hole `Faults?` in the head paired with the single body writer `Faults` from `'_link_monitor'` (manual §19.4).
- **Language-authority status: NEW system-predicate** `'_link_monitor'/2` (host-implemented; emits the fault lattice terms). The lattice atoms `ok`/`tempFail/2`/`permFail/2` are ordinary GROUND terms — **NOT new language items** (no new guard, no new verdict), but the fault-term VOCABULARY (functors/arities) is an approval item (decision doc D-LA-3). Approve the predicate + the term vocabulary.
- **Base tag:** `[base:monitor]`.

---

## 3. The corrected `out_relay` (decision-doc SRSW fix) — explicit before/after

The decision doc flagged its reviewed `out_relay` clause as **failing SRSW**: `LinkId?` (a reader) occurred twice with no ground guard on it. The corrected form is `link_send/3` (§2.5) — the fix is to **ground-guard the LinkId** (and, in the GRL out-relay variant that names LinkId explicitly, to gate the payload ground). Shown explicitly for the gate record:

```prolog
% BEFORE (decision doc, FLAGGED — SRSW VIOLATION: LinkId? read twice, no ground guard):
%   out_relay(Msg, LinkId) :- known(Msg?) | '_send'(Msg?, LinkId?, peer_of(LinkId?)).
%                                                          ^^^^^^^        ^^^^^^^ LinkId? twice, ungrounded

% AFTER (PROPOSED, SRSW-clean): ground-guard LinkId so its multiple reader uses are
% permitted under the ground-implying-guard relaxation, and gate the payload GROUND
% (ground-relay base, FR-010) rather than known/1 (which would admit open structures
% that belong to glink, not the base). The body calls a NEW ground-relay kernel
% '_link_send'/3 (NOT '_send'/3 — see below).
procedure out_relay(Term?, LinkId?, AgentId?).
out_relay(Msg, LinkId, ToPeer) :-
    ground(Msg?), ground(LinkId?), ground(ToPeer?) |
    '_link_send'(Msg?, LinkId?, ToPeer?).
```

- This is the lower-level GRL wrapper that the Channel-shaped `link_send/3` may lower to; either may be the public face (OQ-3). The **GLP wrappers are composable** (no new guard) over `ground/1` and head unification, but the body cannot be the existing `'_send'/3`.
- **Why NOT `'_send'/3` (correction to the decision-doc sketch):** verified live, `'_send'/3` ABORTS unless its global name `G` is a `_w/2`/`_r/2` struct and it runs the madGLP **globalize** path (`body_kernels.dart:683-697,742`). A `LinkId` is neither `_w` nor `_r`, and the base must NOT globalize (that is the open-structure `glink` path). So the corrected ground-relay sender requires a **NEW body-kernel** `'_link_send'(GroundTerm?, LinkId?, AgentId?)` (ground frame, no globalize) — OR, as a fallback, reuse of `'_send'`'s index-0 serializer cold-call form `_w(ToPeer,0) := [Msg↑ | _w(ToPeer,0)]` (`mad_context.dart:254-318`). The decision doc's `'_send'(Msg?, LinkId?, peer_of(LinkId?))` sketch would abort at runtime as written. This is a genuine correction the gate must rule on (OQ-3).
- **Why `ground/1` not `known/1`:** the base is ground-relay (FR-010/FR-040). `known/1` would pass `[add|Xs1?]` with an embedded reader, routing into the open-structure globalize path (the buggy FR-034/FR-035 territory) — that is `glink`, deferred. `ground/1` keeps the base to ground copies and sidesteps both live bugs for the MVP.

---

## 4. Idempotent-redelivery gate (the primitives route THROUGH it) — base correctness note

FR-021/SC-008: a duplicate frame today CRASHES (verified: `mad_context.dart:330,377` throw; `heap_fcp.dart:365` throws on re-bind). None of the eight primitives above bind cells directly on the inbound path — they all route the cross-link bind through the inbound ingress (`handleMadAssignment`). The base contract therefore REQUIRES (as part of building the primitives correctly, per the RULED contract, NOT as a separate blocking gate) that the ingress:
1. Carry a per-link **sequence + global-name** dedup key (FR-020) so a redelivered frame is recognized.
2. Make a recognized duplicate a **verified no-op** (return without throw, no re-bind, no re-enqueue) — replacing the current one-shot-remove-then-throw.
3. Defend split-brain with an **epoch/fencing token** (FR-047): two writers for one global name ⇒ exactly one wins by epoch, the loser yields `permFail` on the monitor stream.

This is host-side reliability-sublayer work (C# reference first, Dart mirror), NOT a GLP-surface change — so it carries **no language-authority item**. It is listed because the primitives' FR-017/FR-051/FR-052 guarantees depend on it.

---

## 5. Role-parameterized decomposition (FR-011) — one program, branch on ground AgentId

Per FR-011 and the RULED D-DEC-1, the split is ONE role-parameterized program selecting role by a ground `AgentId` (arg-0, the `@`/boot idiom — `agent_runtime.dart:202-205`), not a fork:

```prolog
% PROPOSED skeleton — one program, role chosen by ground AgentId (FR-011).
procedure main(AgentId?, Stream(_)?).        % arg-0 ground AgentId, last arg NetIn (unread here)
main(Me, _) :-
    Me? =?= producer |
    client_connector(link_id("ws", ep("hostB", 9001), 1), Link, Faults),
    produce(Link?, Faults?).
main(Me, _) :-
    Me? =?= consumer |
    server_listener(link_id("ws", ep("hostB", 9001), 1), Link, Faults),
    consume(Link?, Faults?).
```

- `Me? =?= producer` is the branch-on-ground-AgentId selector (existing `=?=`, three-valued: ground-equal → that clause; unbound reader → suspend; mismatch → next clause; the bare atom `producer` is a `String` constant per the `AgentId ::= String` alternative). Establishment role (`client_connector` vs `server_listener`) is chosen here but is INDEPENDENT of data direction (FR-004) — the producer could equally be the listener.
- This keeps the unsplit baseline and the split deployment provably the same source (what makes SC-001 byte-identical meaningful).

---

## 6. Summary table — eight primitives, status, base tags

| # | Primitive (PROPOSED) | Signature (modes) | Establishment / data | LA status | Base tag |
|---|---|---|---|---|---|
| 1 | `link_setup/4` | `(LinkId?, LinkRole?, Link, FaultStream)` | setup (idempotent) | NEW pred `'_link_setup'/5` | `base:setup` |
| 2 | `server_listener/3` | `(LinkId?, Link, FaultStream)` | establish: listen | composable | `base:listen` |
| 3 | `client_connector/3` | `(LinkId?, Link, FaultStream)` | establish: connect | composable | `base:connect` |
| 4a | `request_link/4` | `(LinkId?, AgentId?, Link, FaultStream)` | establish: request | NEW pred `'_link_request'/5` | `base:request` |
| 4b | `accept_link/4` | `(LinkId?, Stream(request)?, Link, FaultStream)` | establish: accept | NEW pred `'_link_accept'/5` | `base:accept` |
| 5 | `link_send/3` (+ `out_relay/3`) | `(Term?, Link?, Link)` / `(Term?, LinkId?, AgentId?)` | data: send (ground-relay) | GLP wrapper composable; body needs NEW kernel `'_link_send'/3` (OQ-3) | `base:send` |
| 6 | `link_recv/3` | `(Term, Link?, Link)` | data: receive | composable | `base:recv` |
| 7 | `link_monitor/2` | `(LinkId?, FaultStream)` | fault monitor | NEW pred `'_link_monitor'/2` + fault vocab | `base:monitor` |

The GLP **wrappers** are composable from existing guards (`ground/1`, `=?=`) and head unification (the `self.glp` `send/receive/new_channel` idioms). The language-authority approval surface is: three **NEW host system-predicates** for establishment/fault (`'_link_setup'/5`, `'_link_request'/5`, `'_link_accept'/5`, `'_link_monitor'/2`), **one NEW body-kernel** for the ground-relay sender (`'_link_send'/3` — because the existing `'_send'/3` aborts on a non-`_w`/`_r` global name and globalizes; OQ-3), plus the **fault-term vocabulary** (`ok`/`tempFail/2`/`permFail/2`). No new **GUARD** and no new **DIRECTIVE** are proposed by the base set (the guard work — `@<`/`@>` family, `atom/1` fix, compound-suspend / imported-reader fixes — is a SEPARATE facet under FR-032..FR-039, not part of these eight primitives). The receiver carries no new language item — its net-new work is the inbound ingress + reliability sublayer (host code below the seam, §4).

---

## 7. Open co-design questions (flagged for the gate)

- **OQ-1 (establishment-vs-data symmetry surface).** FR-003 requires every primitive symmetric (both ends send AND receive). The design achieves this by returning a `Link(In,Out)` exposing both directions regardless of establishment role. Confirm this is the intended symmetry, vs. separate `link_sender`/`link_receiver` handle types. (Recommendation: single bidirectional `Link` — composes with `self.glp` `new_channel/2`'s dual-end shape.)
- **OQ-2 (LinkId structure).** Is the ground `link_id(Scheme, Endpoint, Nonce)` triple the right identity, or should LinkId be opaque (host-minted handle) with Scheme/Endpoint carried separately? Identity drives FR-007 idempotency and FR-026 origin-auth keying. (Recommendation: ground compound, so `=?=`/`@<` can test it without new machinery.)
- **OQ-3 (public sender face AND its kernel).** Two coupled questions. (i) Public face: Channel-shaped `link_send/3` (composes with `self.glp` `send/3`) OR LinkId-keyed `out_relay/3` (closer to the `'_send'/3` shape)? One should be canonical to avoid two ways to do one thing. (ii) Underlying kernel: the existing `'_send'/3` is NOT usable for the ground-relay base — verified, it aborts unless `G` is `_w/2`/`_r/2` and it globalizes (`body_kernels.dart:683-697`). So the base sender requires a ruling between **(a) a NEW body-kernel `'_link_send'/3`** (ground frame, no globalize — recommended, keeps the base discipline explicit) and **(b) reuse of `'_send'`'s index-0 serializer cold-call** (`_w(ToPeer,0) := [Msg↑|_w(ToPeer,0)]`, no new kernel but pins index-0 semantics to the base relay). The decision-doc's `'_send'(Msg?, LinkId?, peer_of(LinkId?))` sketch would abort as written — this is a correction the gate must adopt either way.
- **OQ-4 (request/accept rendezvous).** `request_link`/`accept_link` presume an already-reachable rendezvous to carry the request token before the data link exists. Is that rendezvous (a) a pre-established bootstrap link, (b) a scheme-level discovery service, or (c) the transport's own connect with an in-band request frame? FR-002 only requires the path exists and yields an equivalent link. (Recommendation: (c) — in-band request frame over the transport connect, so no extra channel concept.)
- **OQ-5 (Link Transport Seam host signature).** The `open/send-bytes/recv-bytes/close+fault` seam (FR-058) is host-language (C# first per FR-055) — its async signature (recv as a Future/Stream) is exactly what codeconv escalates. This is a HOST-interface co-design item, not a language-authority item, but the gate should ratify the seam shape before transport leaves are authored.
- **OQ-6 (idempotency-key placement).** The dedup sequence/epoch key (§4) — is it (a) inside the `Frame` only (invisible to GLP), or (b) also surfaced in a `LinkId`/global-name the GLP layer can `=?=`-test? FR-021 is satisfiable with (a); (b) would let GLP-level dedup-aware code exist. (Recommendation: (a) for the base — keep reliability below the seam.)
- **OQ-7 (`ground/1` vs `no_readers/1` for the out-relay gate).** §2.5/§3 gate the payload `ground/1`. For the stdio-reroute facet (US3) and writer-carrying query/response, `no_readers/1` (writers OK, readers not — corpus 14 §4 / guards-reference) might be wanted. Is the base strictly `ground/1`, or does it admit a `no_readers/1` variant? (Recommendation: base = `ground/1`; defer `no_readers/1` to the stdio facet.)
- **OQ-8 (true-multi-reader BIS — kept open per RULED T2).** FR-041 keeps BLE BIS true-multi-reader in scope as an open co-design goal alongside the N-bilateral-ground-copy model (FR-040, expressed as N `link_send`s of a ground copy). The SRSW tension on a true multi-reader unbound variable is explicitly UNRESOLVED and out of the base MVP — flagged, not designed here.

---

## 8. Risks

- **R-1 (reliability sublayer is the real unbuilt work).** All eight primitives' FR-017/021/051/052 guarantees depend on the net-new host-side sequence/dedup/epoch/FIFO-reorder/fault sublayer that does NOT exist today (decision-doc verification: 0 hits). The GLP surface is thin; the engineering is below the seam. Under-scoping it as "a thin sublayer" is the headline risk.
- **R-2 (duplicate-delivery crash is live).** Until §4's idempotency gate lands, even a correct `link_recv` can crash on a redelivered frame (verified throws). The base primitives are spec'd to route through the ingress, but the ingress must be fixed in lockstep.
- **R-3 (compound-suspend / imported-reader bugs lurk one layer up).** The base ground-relay sidesteps FR-034/FR-035 by never crossing open structures. The moment `glink` (later) crosses an embedded reader, both bugs become load-bearing. The base contract must NOT be read as fixing them.
- **R-4 (C#-first authoring of host predicates).** `'_link_setup'`/`'_link_request'`/`'_link_accept'`/`'_link_monitor'` are authored in C# FIRST (FR-055) and must live outside `out/csharp`/`glp_runtime_net` (FR-057) so a codeconv regen cannot clobber them; the Dart mirror follows (FR-056). The seam's async signature is a codeconv-escalation risk (OQ-5).
- **R-5 (two establishment paths, one registry).** FR-002 "equivalent established link" requires listen/connect AND request/accept to converge on the same `'_link_setup'` registry/handle. If they diverge (e.g., different LinkId normalization), idempotency (FR-007) and origin-auth (FR-026) keying break. Single canonical registry keyed by ground LinkId is the mitigation.
- **R-6 (peer-of derivation).** `out_relay/3` needs `ToPeer` ground; deriving the peer from a LinkId (the flagged `peer_of(LinkId?)`) must itself be ground and single-use. Carrying `ToPeer` explicitly (as in §3 AFTER) avoids a hidden second LinkId read.
- **R-7 (`'_send'/3` is not a ground-relay kernel — corrected here).** The decision-doc `out_relay` sketch lowered to `'_send'(Msg?, LinkId?, peer_of(LinkId?))`. Verified live, `'_send'/3` ABORTS unless `G` is a `_w/2`/`_r/2` struct and runs globalize (`body_kernels.dart:683-697,742`); a `LinkId` is neither, so that body would abort at runtime AND the base must not globalize. The base ground-relay sender therefore needs a NEW body-kernel `'_link_send'/3` (recommended) or the index-0 serializer cold-call form (OQ-3). Mis-reading `'_send'/3` as a ground-relay would silently route the base into the open-structure `glink` path (the buggy FR-034/FR-035 territory). This is now reflected in §2.5/§3/OQ-3 and the summary table.
