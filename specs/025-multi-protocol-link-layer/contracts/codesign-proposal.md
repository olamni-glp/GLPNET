# 025 Link Layer — Language-Authority Co-Design Proposal

**For Gabi's plan-approval gate. NOTHING here is decided.** Every primitive, guard, body-kernel and signature below is **PROPOSED pending your language-authority approval** (CLAUDE.md Language Authority; DISCIPLINE 1.14). Grounded in self.glp `Channel(In,Out) ::= ch(In, Out?)` + `send/3`/`receive/3`/`new_channel/2` (self.glp:15,90-97) and live runtime (cites below). RULED contract (B2-B3-G-decision.md "Decisions — RULED") is binding; this refines it into concrete shapes.

Full contracts: `contracts/link-primitives.md`, `contracts/guards.md`, `contracts/architecture-context.md`.

---

## 1. PROPOSED base link primitives (FR-001..010)

C#-first reference, Dart mirror after (RULED B3). All wrappers are pure GLP over existing guards + head unification (no body `=`); the **system-predicates / body-kernel** are the language-authority items.

| # | Name | Signature + modes | One-line semantics | Tag |
|---|---|---|---|---|
| 1 | `link_setup/4` | `link_setup(LinkId?, Role?, ch(In?,Out), Faults) :- ground(LinkId?), ground(Role?) \| '_link_setup'(LinkId?,Role?,In,Out,Faults).` | Establish-or-REUSE a link by ground LinkId in role `listener`/`connector`; idempotent at link-identity (FR-007); returns a bidirectional Link + per-link Faults stream. | **[NEW LANGUAGE — needs approval]** (`'_link_setup'/5` system-pred); wrapper composable |
| 2 | `server_listener/3` | `server_listener(LinkId?, Link, Faults) :- ground(LinkId?) \| link_setup(LinkId?, listener, Link, Faults).` | FR-002 path-A listen role; thin specialization of #1. | **[composable]** |
| 3 | `client_connector/3` | `client_connector(LinkId?, Link, Faults) :- ground(LinkId?) \| link_setup(LinkId?, connector, Link, Faults).` | FR-002 path-A connect role; thin specialization of #1. | **[composable]** |
| 4 | `request_link/4` | `request_link(LinkId?, ToPeer?, Link, Faults) :- ground(LinkId?), ground(ToPeer?) \| '_link_request'(LinkId?,ToPeer?,In,Out,Faults), Link = ch(In?,Out).` | FR-002 path-B initiate: send ground `request(LinkId)` to ToPeer, park until accept, yield established Link. | **[NEW LANGUAGE — needs approval]** (`'_link_request'/5`); wrapper composable |
| 5 | `accept_link/4` | `accept_link(LinkId?, [request(LinkId2,FromPeer)\|_], Link, Faults) :- ground(LinkId?), LinkId? =?= LinkId2? \| '_link_accept'(LinkId?,FromPeer?,In,Out,Faults), Link = ch(In?,Out).` | FR-002 path-B accept: consume a request token off the inbound stream; match by existing `=?=` (runner.dart:4669); establish equivalent link. SRSW-correct realization of the decision-doc's flagged double-read. | **[NEW LANGUAGE — needs approval]** (`'_link_accept'/5`); wrapper composable |
| 6 | `link_send/3` | `link_send(Msg, ch(In?,[Msg?\|Out?]), ch(In?,Out)) :- ground(Msg?) \| true.` (Channel face) AND `out_relay(Msg, LinkId, ToPeer) :- ground(Msg?), ground(LinkId?), ground(ToPeer?) \| '_link_send'(Msg?,LinkId?,ToPeer?).` (LinkId face) | Ground-relay sender (FR-010/FR-040): ship ONE ground term; `ground(Msg?)` certifies NO `_w`/`_r` placeholder + no embedded reader on the wire (sidesteps live FR-034/035 bugs for MVP). | **[NEW LANGUAGE — needs approval]** — see §correction; backing kernel is NOT composable |
| 7 | `link_recv/3` | `link_recv(Msg?, ch([Msg\|In], Out?), ch(In?, Out)).` | Receiver: self.glp:97 `receive/3` shape verbatim; SUSPEND on unbound head reader, reactivate once on inbound bind (FR-017/051). | **[composable]** — net-new work is the host ingress sublayer, not a language item |
| 8 | `link_monitor/2` + fault vocab | `link_monitor(LinkId?, Faults) :- ground(LinkId?) \| '_link_monitor'(LinkId?,Faults).` Fault ::= `ok` ; `tempFail(LinkId,Reason)` ; `permFail(LinkId,Reason)`. | Per-link fault monitor (FR-008/043-047): faults are ORDINARY BOUND GROUND TERMS read with existing guards; NOT a 4th verdict, NOT a new guard outcome; disconnect never maps to Fail (FR-044/050). | **[NEW LANGUAGE — needs approval]** (`'_link_monitor'/2` + functor/arity vocab); wrapper composable |

**LOAD-BEARING CORRECTION (verified live — affects #6).** The decision-doc lowered the sender to `'_send'(Msg?, LinkId?, peer_of(LinkId?))`. **This aborts at runtime**: `sendKernel` (body_kernels.dart:683-697) ABORTS unless arg-2 is a `_w/2` or `_r/2` struct, then runs the madGLP globalize path (`ctx.send`, :742). A LinkId is neither — it would (a) abort and (b) wrongly route the base into the open-structure glink path. **Hence the ground-relay sender needs a NEW body-kernel `'_link_send'/3`** (ground frame, no globalize — recommended), OR reuse of `_send`'s index-0 serializer cold-call (no new kernel but pins index-0 semantics) — see OQ-3. **The sender is a language-authority item, not purely composable.**

**Grounding cites:** allocateVariable→(writer,reader) heap_fcp.dart:85; bindWriter throws on already-ValueTag heap_fcp.dart:365-367 (= the FR-021 dup-crash); bindVariable :671; bindImportedReader :641; MadContext outbound onMessageReady/flushMessages mad_context.dart:19,45,87, inbound handleMadAssignment :229; stream-tail-bind delivery idiom :303-318. SRSW relaxation on #1/#4/#5/#6: multiple reader occurrences legal **only** because `ground/1` (or `=?=`) certifies groundness (ground-implying-guard relaxation) — SRSW itself never relaxed by a flag (FR-048).

---

## 2. PROPOSED guard set (FR-032..039)

`docs/guards-reference.md` is the single authoritative spec (FR-032) — deltas fold INTO it, no duplicate. Default-fall-through is WARN+fail (runner.dart:4690-4692), so today `atom`, `@<`, `==` all silently warn+fail.

### Additions
| Guard | Signature | One-line semantics | Edit-site | Tag |
|---|---|---|---|---|
| `@<` `@>` `@=<` `@>=` | `@<(X?,Y?)` … (infix→prefix; both readers) | Total order over GROUND terms (PROPOSED Number<String<compound, then arity/functor/args; equality = `=?=`); succeed/fail on ground; SUSPEND on unbound reader (incl. nested) reactivate once; FAIL on unbound writer; ground-implying ⇒ SRSW relax; PROPOSED non-negatable (complement `@>=`). | **Multi-site core edit (highest blast radius):** lexer.dart:61 (`@` lookahead vs `Goal@Agent`) + token.dart (4 tokens) + parser.dart:687-690 + runner.dart new arms + new `_compareTerms` by `_termsEqual`:4699 + analyzer.dart:616/727 + prelude.dart:33,82. | **[NEW — needs approval]** |

### Fixes (each = a real live defect)
| Item | One-line semantics | Edit-site | Tag |
|---|---|---|---|
| `atom/1` | Make runtime match the already-grounding analyzer+PE: implement runner arm ≈ `string/1` (non-numeric atomic, excludes `nil`); register as builtin. Today analyzer accepts+grounds (analyzer.dart:608,671; PE :1008) but runner has NO arm → warn+fail at runtime (SC-005). | runner.dart new arm beside `string`; prelude.dart:33,82. | **[FIX — needs approval]** |
| compound-operand-suspend | A guard whose operand is a compound with a NESTED unbound reader MUST SUSPEND, not FAIL (FR-034). Fix `_dereferenceWithTracking` to recurse into `StructTerm.args` (cycle-safe), mirroring the already-correct GroundEqual recursion (runner.dart:3630-3633). | runner.dart:4179-4182 (StructTerm branch) — hot path; full baseline must guard it. | **[FIX — needs approval]** |
| imported-reader-reactivation | A guard suspended on a writerless imported reader MUST wake once on bind (FR-035). Today `handleMadAssignment` calls only `bindVariable` (mad_context.dart:306/355/402), never `bindImportedReader` (heap_fcp.dart:641) → `VariableEntry.suspensions` never drain. Wire the 3 ingress handlers to route imported-reader cells through `bindImportedReader`. **Path is KEPT, never deleted** (Preserve-Working-Code). | mad_context.dart:306,355,402; heap bindImportedReader:641. **Alt (D-B2-3):** rule link layer to local-pair writers only + assert ingress never sees an imported reader — changes the fix entirely (OQ below). | **[FIX — needs approval]** |

### Declines (FR-036) — add NO edit sites; enforced by a negative Section-C test
| Guard | Why declined | Tag |
|---|---|---|
| `==` | Redundant alias of `=?=` over ground terms (Tier-3 Prolog/FCP, not GLP kernel). Canonical: `X =?= Y`. | **[DECLINE]** |
| `\==` | Redundant alias of `~(=?=)` (already negatable). Canonical: `~(X =?= Y)`. | **[DECLINE]** |
| `\=` | GLP deliberately removed it (ill-defined over partial terms). Canonical: `~(X =?= Y)`. | **[DECLINE]** |
| `reader/1` | Non-monotonic (truth flips as store grows) ⇒ unsound across a link (a late/withheld remote bind falsifies a committed verdict), violates FR-039 monotone-commit. | **[DECLINE]** |

`=\=` **untouched** (FR-038/SC-017, load-bearing arithmetic disequality) — preserve runner.dart:4387-4394, analyzer.dart:618,727, prelude.dart:58,107; baseline green before/after every change.

---

## 3. OPEN co-design questions (only what the corpus + RULED contract do NOT settle)

1. **Sender kernel (couples to the §1 correction).** `'_send'/3` is verified unusable (aborts unless `_w`/`_r`, body_kernels.dart:683-697). Approve **(a) NEW body-kernel `'_link_send'/3`** (ground frame, no globalize — recommended) or **(b) reuse `_send`'s index-0 serializer cold-call** (no new kernel but pins index-0 semantics)?
2. **Imported-reader fix shape (D-B2-3, changes the fix entirely).** **(1)** wire `handleMadAssignment`→`bindImportedReader` (drain `VariableEntry.suspensions`), or **(2)** rule the link layer to local-pair writers only + assert the ingress never receives an imported reader? With the ground-relay base, `'_link_setup'` mints ordinary local pairs, so (2) is viable for the MVP — but (1) is the only one that fixes the latent core hazard for later glink.
3. **`@<` total order + negatability.** Confirm PROPOSED order (Number<String<compound, then arity/functor/args) is the intended GLP standard order AND must be byte/behaviour-identical Dart↔C# (FR-060). Confirm PROPOSED **non-negatable** (membership in analyzer `_nonNegatableGuards`).
4. **`atom/1` exact semantics.** Exact synonym of runtime `string/1` (non-numeric string constant, excludes `[]`/`nil`), or should `atom` also accept `[]`? Corpus does not settle it; shipping `atom ≠ string` un-confirmed re-introduces a mismatch.
5. **LinkId identity.** Ground compound `link_id(Scheme, Endpoint, Nonce)` (so `=?=`/`@<` test it with no new machinery — recommended), or opaque host-minted handle with Scheme/Endpoint carried separately? Drives FR-007 idempotency + FR-026 origin-auth keying.
6. **request/accept rendezvous.** Token must reach the peer before the data link exists: (a) pre-established bootstrap link, (b) scheme-level discovery service, or (c) transport's own connect with an in-band request frame (recommended)?
7. **BLE BIS true-multi-reader (kept open per RULED T2 / FR-041).** The SRSW tension on a true multi-reader unbound variable is explicitly UNRESOLVED — flagged, not designed in this MVP (base ships N-bilateral ground-copy, FR-040). Confirm it stays a later stage-gate co-design item.

---

## 4. The ask

**Approve / revise / decline, item by item:**

- **Primitives:** the four NEW system-predicates `'_link_setup'/5`, `'_link_request'/5`, `'_link_accept'/5`, `'_link_monitor'/2`; the NEW body-kernel `'_link_send'/3` (or OQ-1(b)); the fault-term vocabulary `ok`/`tempFail`/`permFail`. (Wrappers #2,#3,#7 and the wrapper layer of #1,#4,#5,#6,#8 are composable — no approval needed beyond the predicates they call.)
- **Guards:** ADD `@< @> @=< @>=`; FIX `atom/1`, compound-operand-suspend, imported-reader-reactivation; DECLINE `== \== \= reader/1`; LEAVE `=\=` untouched.
- **Resolve OQ-1 and OQ-2** before implementation — both change what gets built.

All core / core-adjacent edits land only under your explicit language-authority approval, with `bash test/run_all_tests.sh` green before and after each change (FR-067/SC-017).

---

## Residual open questions (full list, for the gate)

1. OQ-1 (sender kernel, couples to the verified §1 correction): '_send'/3 aborts unless arg-2 is _w/2 or _r/2 (body_kernels.dart:683-697) and runs the globalize path — it CANNOT back a ground-relay LinkId sender. Approve (a) a NEW body-kernel '_link_send'/3 (ground frame, no globalize — recommended) or (b) reuse of '_send''s index-0 serializer cold-call (no new kernel but pins index-0 semantics)?
2. OQ-2 (imported-reader fix, D-B2-3 — changes the fix entirely): (1) wire handleMadAssignment->bindImportedReader to drain VariableEntry.suspensions (heap_fcp.dart:641; mad_context.dart:306/355/402), or (2) rule the link layer to local-pair writers only + assert the ingress never receives an imported reader? Option 2 is viable for the ground-relay MVP; only option 1 fixes the latent core hazard for later glink.
3. OQ-3 (@< total order + negatability): confirm PROPOSED standard order (Number<String<compound, then arity/functor/args; equality = =?=) is the intended GLP order AND must be byte/behaviour-identical Dart<->C# (FR-060); confirm PROPOSED non-negatable (complement @>=) for analyzer _nonNegatableGuards membership.
4. OQ-4 (atom/1 exact semantics): exact synonym of runtime string/1 (non-numeric string constant, excludes []/nil), or should atom also accept []? Corpus does not settle it; shipping atom != string un-confirmed re-introduces an analyzer<->runtime mismatch.
5. OQ-5 (LinkId identity): ground compound link_id(Scheme,Endpoint,Nonce) (so =?=/@< test it with no new machinery — recommended) or opaque host-minted handle with Scheme/Endpoint separate? Drives FR-007 idempotency + FR-026 origin-auth keying.
6. OQ-6 (request/accept rendezvous): how does the request token reach the peer before the data link exists — (a) pre-established bootstrap link, (b) scheme-level discovery service, or (c) transport's own connect with an in-band request frame (recommended)?
7. OQ-7 (BLE BIS true-multi-reader, kept open per RULED T2 / FR-041): the SRSW tension on a true multi-reader unbound variable is explicitly UNRESOLVED and out of the base MVP (base ships N-bilateral ground-copy per FR-040); confirm it remains a later stage-gate co-design item, not a drop.
8. OQ-8 (=\= guarantee target, SC-017): SC-017 says the '=\=-gated division/mod in self.glp still loads' but no =\= occurrence exists in programs/self.glp today — does the guarantee target the sibling GLP prelude or a planned glpnet prelude clause? (verification-only; does not block primitives/guards approval)
9. NON-LANGUAGE risks for the eng gate (not for the language-authority decision, but flagged): the reliability sublayer (sequence/dedup/epoch/FIFO-reorder/fault) is the real unbuilt work below the seam (0 hits today); the FR-021 duplicate-delivery crash is LIVE (bindWriter throws on already-ValueTag, heap_fcp.dart:365-367; _handle*Assignment throw on second delivery, mad_context.dart:330,377) and must be fixed in lockstep with link_recv; the C# host predicates must live outside out/csharp and glp_runtime_net (FR-057) so a codeconv regen cannot clobber them, and the LinkTransport seam's async recv signature is a codeconv-escalation item.
