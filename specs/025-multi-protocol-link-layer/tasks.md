# Tasks: Multi-Protocol Peer-to-Peer Link Layer for Distributed GLP

**Feature**: `025-multi-protocol-link-layer` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Gate**: plan-approval COMPLETE (rulings in [contracts/rulings-log.md](contracts/rulings-log.md)).

Conventions: `[P]` = parallelizable with siblings (different files, no dep). Each task lists FR/SC refs + file targets. **C#-first** (the `out/csharp` reference + the clobber-safe `csharp/glp_link/`); the Dart mirror is Phase 8. **Baseline gate (FR-067/SC-017): `bash test/run_all_tests.sh` green before AND after every core-touching task** — listed once here, applies throughout. All exemplar GLP must clear [contracts/glp-correctness-review.md](contracts/glp-correctness-review.md) before it is promoted to a runnable test.

---

## Phase 0 — Baseline + the three live core fixes (no link layer yet)

- [ ] **T001** Record green baseline: `bash test/run_all_tests.sh`; capture counts. (SC-017)
- [ ] **T002** Fix **FR-021 duplicate-delivery crash**: add the dedup gate to the inbound ingress so a redelivered (seq + global-name) frame is a **verified no-op** — no `StateError`, no re-bind, no re-enqueue (today `mad_context` `_handle*Assignment` throw; `bindWriter` throws on a bound cell). Files: `csharp/glp_link/reliability/` + ingress; mirror later. (FR-021/SC-008)
- [ ] **T003** Fix **FR-034 compound-operand-suspend**: `_dereferenceWithTracking` must recurse into `StructTerm.args` (cycle-safe), mirroring the correct `GroundEqual.collectUnbound`, so a nested unbound reader SUSPENDS not FAILs. Files: runner guard path. (FR-034/SC-009)
- [ ] **T004** Fix **FR-035 imported-reader reactivation (OQ-2 = option 1)**: wire `handleMadAssignment` → `bindImportedReader` (drain `VariableEntry.suspensions`); KEEP the `VariableEntry` path (Preserve-Working-Code). Files: `mad_context` ingress + heap dispatch. (FR-035/SC-009)
- [ ] **T005** Regression after each of T002–T004: baseline green; add the new no-op/suspend/reactivate cases to Section A. (SC-008/009/017)

## Phase 1 — Guards (language authority; approved)

- [x] **T010** `atom/1`: add the runner `_evaluateGuard` arm = runtime `string/1` (non-numeric atomic, excludes `[]`/`nil`); register in prelude. Aligns analyzer (already grounds it) ↔ runtime. (FR-033/SC-005)
- [x] **T011** `@< @> @=< @>=` (non-negatable; total order Number<String<compound then arity/functor/args, equality = `=?=`): lexer `@`-lookahead (must not break `Goal@Agent`) + 4 tokens + parser + runner arms + new `_compareTerms` (cycle-safe, byte/behaviour-identical Dart↔C#) + SRSW analyzer (ground-implying; `_nonNegatableGuards`) + prelude. (FR-037/SC-004/006) — **largest core edit**.
- [x] **T012** `[P]` Guard tests: Section A three-valued (succeed / suspend-then-reactivate-once / fail) for `@<` family + `atom/1`; Section B positive SRSW-relaxation; Section C negatives incl. **declines** `== \== \= reader/1` (FR-036) reject; `=\=` untouched regression (FR-038). Files: `programs/tests/typed/`. (SC-004/006)
- [ ] **T013** `[P]` Consolidate `docs/guards-reference.md` as the single authoritative guard spec (fold the additions/fixes/declines in; no duplicate spec). (FR-032)

## Phase 2 — Reliability sublayer + seam (C# reference, clobber-safe `csharp/glp_link/`)

- [ ] **T020** `LinkTransport` seam: `ILinkTransport`/`ILinkEndpoint` (open / send-bytes / recv-bytes / close + fault), scheme-selected. (FR-058)
- [ ] **T021** Wire format: version byte + length/CRC + cycle-guard (visited-set) + fragmentation/reassembly for under-MTU leaves; bad-version/bad-CRC rejected; over-MTU fragments. (FR-022)
- [ ] **T022** Per-link sequence/dedup + FIFO + reorder buffer (in-order reconstruct; corruption detected when sublayer off); broker-relay FIFO+at-least-once enforced end-to-end. (FR-020/023/053)
- [ ] **T023** Epoch/fencing token (split-brain): competing writers for one global name → exactly one wins, loser `permFail`. (FR-047)
- [ ] **T024** Distributed GC: on `permFail`/close, registry + send-registry goals + heap bind callbacks + reply-table return to baseline; no unreclaimable cycle. (FR-024)
- [ ] **T025** Bounded backpressure: default window **N=8** (scheme-overridable, below the seam); producer suspends; no OOM; no head-of-line block across independent links. (FR-025)
- [ ] **T026** Deterministic seeded **loopback transport** (FIFO/in-order/exactly-once by default; deviates only on injected fault) — substrate for hermetic tests.

## Phase 3 — Base link primitives (C# reference)

- [ ] **T030** `'_link_setup'/5` + `link_setup/4` + `server_listener/3` + `client_connector/3`; per-instance LinkId→handle registry (idempotent at identity). (FR-001/002/003/004/007)
- [ ] **T031** `'_link_send'/3` + `link_send/3` (channel face) + `out_relay/3` (LinkId face); ground-relay (`ground(Msg?)` gate; no `_w`/`_r`/embedded reader on the wire); host egress drainer on `Out`. (FR-010/040)
- [ ] **T032** `link_recv/3` + the per-link host ingress (fills `In`; routes through the T002 dedup gate; reactivate-exactly-once). (FR-017/051)
- [ ] **T033** `'_link_request'/5` + `'_link_accept'/5` + `request_link/4` + `accept_link/4`; in-band request frame over the transport connect (OQ-A3); both paths converge on the T030 registry → equivalent link. (FR-002)
- [ ] **T034** `'_link_monitor'/2` + `link_monitor/2` + fault vocab `ok` / `closed(LinkId,Reason)` / `tempFail(LinkId,Reason)` / `permFail(LinkId,Reason)` on a per-link monitor stream (data, not a verdict). (FR-008/043-046)
- [ ] **T035** `'_link_close'/2` + `link_close/1`+`/2` (abrupt teardown → RST_STREAM-equiv) + graceful stream-end `[]` close; both run T024 GC and emit `closed(LinkId, Reason)` (`eos` for graceful). (FR-024)
- [ ] **T036** `programs/lib/link.glp`: types `Link(In,Out)` / `LinkId=link_id(Scheme,Endpoint,Nonce)` / `Fault` + the GLP wrappers. (FR-006/013)
- [ ] **T037** Role-parameterized boot: branch on ground `AgentId` (the `@`/boot idiom), one program not a fork. (FR-011)

## Phase 4 — Headline split (SC-001/002) over file/loopback

- [ ] **T040** `producer(X)/consumer(X?)` split **Dart↔Dart** over loopback → byte-identical to unsplit baseline. (SC-001)
- [ ] **T041** `[P]` File endpoints (binary + text; read/write/search) split. (FR-012)
- [ ] **T042** Same split **Dart↔C#** over loopback — the cross-runtime parity **release gate**. (SC-002/059/062)
- [ ] **T043** `[P]` request/accept (path B) split over loopback. (FR-002)

## Phase 5 — Integration-test harness (net-new)

- [ ] **T050** Implement the harness per [tests/integration-harness-design.md](tests/integration-harness-design.md): `start_instances / open_link / inject / drive / capture / assert_equiv / close / stop`; Dart↔Dart then Dart↔C# rigs.
- [ ] **T051** Wire **Section R** into `test/run_all_tests.sh` (skip-until-implemented; flips to run as each primitive/leaf lands; keeps baseline green). (SC-017)
- [ ] **T052** Fault injection: drop / reorder / duplicate / delay / partition / peer-kill / clock-jitter (seeded, replayable). (SC-008/010/011/012/013/014)

## Phase 6 — Transport leaves (each: SC-003 feasibility + close + TLS-default) `[P]` across leaves

- [ ] **T060** `ws`/`wss` leaf (native full-duplex; server→client back-channel). (SC-003) — [tutorials/websocket.md](tutorials/websocket.md)
- [ ] **T061** `https`/HTTP-2 + **mTLS** leaf (bidirectional stream; mutual-cert origin auth). (SC-002/003/007) — [tutorials/https-http2-mtls.md](tutorials/https-http2-mtls.md)
- [ ] **T062** `mqtt` leaf — **P2P to the immediate peer** (broker out of scope); request/accept rendezvous; QoS-duplicate dedup. (SC-003/008) — [tutorials/mqtt.md](tutorials/mqtt.md)
- [ ] **T063** `coap` leaf — OBSERVE back-channel; blockwise fragmentation; CON ack; DTLS. (SC-003/012) — [tutorials/coap.md](tutorials/coap.md)
- [ ] **T064** `ble-l2cap` leaf (Android; CoC bilateral stream; BIS multi-reader stays OUT/open). (SC-003) — [tutorials/ble-l2cap.md](tutorials/ble-l2cap.md)
- [ ] **T065** Per-leaf: FR-016 one-bind feasibility test, graceful/abrupt close, plain inter-host refused (TLS/DTLS default). (FR-029)

## Phase 7 — Failure model, security, advanced SCs

- [ ] **T070** Fault liveness: peer-kill mid-bind → reader does NOT fail; `tempFail`→`permFail` in bounded time; fault-guarded clause reducible. (SC-010)
- [ ] **T071** Split-brain defense (epoch/fence; loser `permFail`; no silent overwrite) **+ the `mqtt` reconnect/stale-writer witness** (matrix follow-up). (SC-011)
- [ ] **T072** `[P]` Reorder/loss recovery: sublayer-on reconstructs in-order; sublayer-off detects corruption. (SC-012)
- [ ] **T073** `[P]` Backpressure bound test (producer suspends; bounded; no HoL block across links). (SC-013)
- [ ] **T074** `[P]` Distributed GC to baseline after N `permFail`s (SnapshotResources probe). (SC-014)
- [ ] **T075** Adversarial corpus on **both** REPLs, verdict-by-verdict: forged origin (FR-026), index/cold-call flooding quota (FR-028), malformed/oversized/cyclic/huge-arity fail-safe, bad-version/bad-CRC; plain inter-host refused (FR-029). (SC-007/FR-031)
- [ ] **T076** Stream reroute `stdin`/`stdout`/`stderr` (explicit capability; control-seq sanitized; streams distinct) **+ a real-leaf `wss` reroute run** (matrix follow-up). (SC-016/FR-030)
- [ ] **T077** GEPA round-trip fidelity per primitive via **Claude Agent seams only** (never OpenAI/litellm/`OPENAI_API_KEY`). (SC-015/FR-065-066)

## Phase 8 — Dart mirror + cross-runtime parity + ship

- [ ] **T080** Dart mirror of `csharp/glp_link/` (only AFTER the C# reference passes its acceptance). (FR-056)
- [ ] **T081** Executed **Dart↔C# round-trip over a real transport** — the release gate (must pass before ship). (SC-002/062)
- [ ] **T082** `codeconv` correspondence of the language-layer edits (guards/heap fixes) Dart↔C#; byte/behaviour-identical serializer + comparator. (FR-054/060/061)
- [ ] **T083** Full regression green on both REPLs; the `=\=`-gated prelude still loads. (SC-017)

---

## Dependencies (summary)

- Phase 0 fixes (T002–T004) are prerequisites for Phase 3 (`link_recv` routes through the T002 dedup gate; remote operands rely on T003/T004).
- Phase 1 guards (T011 `@<`) are independent of the link primitives but share the SRSW-analyzer/parser edits — sequence the parser `@` change carefully against `Goal@Agent`.
- Phase 2 sublayer + seam (T020–T026) underlie all of Phase 3 + Phase 6.
- Phase 4 headline split needs Phase 3 + the loopback (T026) + harness (T050).
- Phase 6 leaves each depend on the seam (T020) + the headline split working on loopback (Phase 4).
- Phase 8 (Dart mirror + parity) is LAST and gates ship (T081).

## Deferred (post-MVP / own facet — tracked, not in this task set)

- `glink` full variable-distribution transparency (base→glink; out of MVP).
- OQ-F3 credit/back-channel-unification elaboration (program-visible credit back-channel).
- BLE LE-Audio **BIS true-multi-reader** vs SRSW (open co-design).
- The remaining FR-012 transport leaves beyond the priority 6 (AMQP, XMPP, DDS, HTTP/3, SSH, FTP, SFTP, BLE GATT, BR/EDR SPP).
- OQ-G5 `=\=`-gated-prelude target verification (SC-017 wording).
