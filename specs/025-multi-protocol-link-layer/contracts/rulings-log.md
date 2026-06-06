# Plan-gate rulings log — feature 025 multi-protocol-link-layer

Append-only record of Gabi's language-authority / co-design rulings at the plan gate.
The marathon gate is NOT recorded as fully approved until Gabi signals the gate complete;
this log captures item-by-item rulings as they are made so none are lost (save-to-disk rule).

| Date | Item | Ruling | Notes |
|---|---|---|---|
| 2026-06-06 | Clarify: peer-id ordering (FR-037) | **B — peer-ids may be non-numeric compound, totally ordered** → `@<`/`@>`/`@=<`/`@>=` in scope | encoded in spec.md, committed `1230c9e3` |
| 2026-06-06 | OQ-1 / OQ-3 sender kernel | **`'_link_send'/3` body-kernel (option a) APPROVED ("sound")** | option (b) `_send` index-0 reuse not taken; channel-face `link_send/3` remains the idiomatic data path, `'_link_send'/3` backs the LinkId-keyed `out_relay/3` face; receiver needs NO kernel (host ingress only) |
| 2026-06-06 | Close: 9th primitive `link_close` | **APPROVED — add `link_close/1` (or `/2`)**, a NEW host system-predicate (`'_link_close'`) | for ABRUPT teardown + to back the element-level `link_send`/`link_recv` sugar; graceful close stays stream-end `[]` (default). Base primitive count now **9**. |
| 2026-06-06 | Monitor vocab: clean-close term | **APPROVED — a clean close emits a terminal `closed`/`bye` term** on the monitor stream | proposed `closed(LinkId, Reason)` (clean, distinct from `tempFail`/`permFail`); exact name pending ratification |
| 2026-06-06 | CoAP reliability + DTLS | **ACCEPTED** | CoAP CON (confirmable) for transport ack/retransmit; our seq/dedup does ordering + dup-absorption on top (FR-020/021); DTLS = the TLS-equivalent secure variant (FR-029) |
| 2026-06-06 | MQTT framing (correction, Gabi) | **CLARIFIED — at THIS link level every link is peer-to-peer to the IMMEDIATE peer; the MQTT broker is at ANOTHER level, OUT OF SCOPE here** | pub↔broker is one P2P link, broker↔subscriber(s) another P2P link; broker fan-out/forwarding is a higher level (routing/glink-like), NOT the base primitives' concern. Supersedes the earlier "broker = relay under the link" wording: the base primitive just sees one bilateral P2P link to one peer (which may happen to be a broker). |

## Proposed concrete shapes (concept approved 2026-06-06; exact names/arities pending ratification)

**`link_close` — 9th base primitive.** Host kernel `'_link_close'(LinkId?, Reason?)` (NEW system-predicate); GLP wrappers:
```prolog
procedure link_close(LinkId?).
link_close(LinkId) :- ground(LinkId?) | '_link_close'(LinkId?, abrupt).

procedure link_close(LinkId?, Reason?).
link_close(LinkId, Reason) :- ground(LinkId?), ground(Reason?) | '_link_close'(LinkId?, Reason?).
```
ABRUPT teardown by ground LinkId regardless of stream state → maps to HTTP/2 RST_STREAM / WS close (non-1000) / CoAP RST. Runs per-link GC (FR-024). Emits a terminal `closed(LinkId, Reason)` on the monitor, then ends the monitor stream. Graceful close stays stream-end `[]`.

**Monitor vocabulary** (FR-043/045) — add the clean-close terminal (distinct from a fault):
```prolog
Fault ::= ok
        ; closed(LinkId, Reason)     %% NEW: intentional close. Reason = eos (graceful []) | <user reason> (link_close)
        ; tempFail(LinkId, Reason)
        ; permFail(LinkId, Reason).
```
`closed` = intentional (not a fault); `tempFail`/`permFail` unchanged. (`bye` is the alternative name if preferred.)

## PLAN GATE — RULED COMPLETE (Gabi, 2026-06-06)

| Item | Ruling |
|---|---|
| OQ-2 imported-reader fix (D-B2-3 / FR-035) | **Option 1 — wire `handleMadAssignment` -> `bindImportedReader`** (drain `VariableEntry.suspensions`); the `VariableEntry` path is KEPT (Preserve-Working-Code). Fixes the core hazard; forward-compatible with later glink. |
| OQ-G1 `@<` total order | **CONFIRMED** — Number < String < compound, then arity/functor/args; equality coincides with `=?=`; byte/behaviour-identical Dart<->C#. |
| OQ-G2 `@<` negatability | **NON-NEGATABLE** (complement `@>=`). |
| OQ-G3 `atom/1` | **YES** — exact synonym of runtime `string/1` (non-numeric atomic, excludes `[]`/`nil`). |
| OQ-A4/C1/C2 names+arities | **AS WRITTEN** — `'_link_setup'/5`, `'_link_request'/5`, `'_link_accept'/5`, `'_link_send'/3`, `'_link_monitor'/2`, `'_link_close'/2`; fault term `closed(LinkId, Reason)`; graceful reason `eos`. |
| OQ-A1 single bidirectional Link | **YES**. |
| OQ-A2 LinkId = `link_id(Scheme, Endpoint, Nonce)` | **YES**. |
| OQ-A3 rendezvous = in-band over the transport connect | **YES**. |
| OQ-F1 default window | **N = 8, scheme-overridable, below the seam**. |
| OQ-F2 MVP flow control | **below-seam backpressure only**; program-visible credit back-channel deferred to OQ-F3. |
| OQ-T1 BLE BIS true-multi-reader | **stays an open later co-design item** (not dropped). |
| Matrix follow-ups | fold in at tasks: SC-011 mqtt reconnect/stale-writer witness + SC-016 real-leaf `wss` reroute. |
| Everything else | **ALL AS RECOMMENDED**. |

Deferred to their own facets (post-gate): OQ-F3 credit-unification elaboration; SC-015 GEPA loop; OQ-G5 `=\=` prelude-target verification; OQ-T2 platform matrix (per-leaf).

**PLAN-APPROVAL GATE: COMPLETE.** The 9 base link primitives + the approved guard set + the three core fixes are approved-to-implement under language authority, with the signatures above. Proceeding to plan_task_analyze finalize.
