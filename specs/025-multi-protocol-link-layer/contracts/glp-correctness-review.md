# GLP-Correctness Review — Feature 025 Multi-Protocol Link Layer (Plan-Stage Exemplars)

**Scope:** Adversarial SRSW / mode / GLP-semantics review of every GLP clause in the
feature-025 plan-stage exemplars (six protocol tutorials + the three contract/dossier
files). Exemplars are ILLUSTRATIVE (PROPOSED primitives, not yet runnable) but MUST be
GLP-correct as written.

**Authorities checked against:** `docs/glp-cheat-sheet.md`, `docs/typed-glp-manual.md`
(§2A Channel mode-flip, §9.1 abandoned-input, §19.4 output-hole idiom),
`programs/self.glp:90-97` (`Channel` / `new_channel` / `send/3` / `receive/3`),
`contracts/link-primitives.md` (PROPOSED primitive signatures), `contracts/guards.md`
(PROPOSED guard signatures).

**Method:** Per-clause SRSW accounting (writer ≤1, reader ≤1, ground-implying-guard
relaxation, `_X` anon-writer legality, `_?` anon-reader illegality); mode verification
against procedure decls + the `Channel(In,Out) ::= ch(In, Out?)` flip rule; head-construction
of writer outputs; three-valued suspend-not-fail behavior; presence of type+procedure decls.
This is a **review report only — nothing is fixed here.**

---

## Overall Verdict

**ISSUES — not GLP-clean.** Seven files reviewed (six tutorials + one combined
contracts/dossier file). One file is clean on SRSW per-clause but carries high-severity
type/behavior defects (`coap.md`). The remaining six carry SRSW/mode compile-blockers.

| Severity | Count |
|---|---|
| **HIGH** | **17** (15 SRSW/mode compile-blockers + 2 type/behavior at the "GLP-correct as written" bar) |
| **MED** | **11** |
| **LOW** | **10** |
| **Total** | **38** |

**Total clauses checked:** 167 (file-loopback 23, websocket 20, https-http2-mtls 14,
mqtt 20, coap 31, ble-l2cap 12, contracts/dossier 47).

### Per-File Verdict

| File | Clauses | SRSW-clean | Verdict | High | Med | Low |
|---|---|---|---|---|---|---|
| `contracts/link-primitives.md` (+ `example-http-link.md` + `DESIGN-DOSSIER.md`) | 47 | no | **issues** | 3 | 2 | 2 |
| `tutorials/file-loopback.md` | 23 | no | **issues** | 4 | 1 | 1 |
| `tutorials/websocket.md` | 20 | no | **issues** | 3 | 3 | 1 |
| `tutorials/mqtt.md` | 20 | no | **issues** | 2 | 1 | 2 |
| `tutorials/ble-l2cap.md` | 12 | no | **issues** | 2 | 2 | 1 |
| `tutorials/https-http2-mtls.md` | 14 | no | **issues** | 1 | 0 | 1 |
| `tutorials/coap.md` | 31 | yes (SRSW) | **issues** (type/behavior) | 2 | 2 | 2 |

### The Single Systematic Root Cause (drives 13 of the 15 SRSW/mode HIGH defects)

Almost every high-severity defect is the **same mistake**: a consumed/produced *whole
channel* is destructured-and-bound with the reader/writer `?` marks on the **wrong `ch`
slots**, contradicting `Channel(In,Out) ::= ch(In, Out?)` and the canonical
`self.glp:94/97` `send/3`/`receive/3` shapes.

- The canonical consumed-channel head is **`ch(In, Out?)`** — *writer* `In` captures the
  inbound stream, *reader hole* `Out?` is the outbound hole; the body then reads `In?` and
  writes `Out`. (Verified: `self.glp:97` `receive(X?, ch([X|In], Out?), ch(In?, Out))`.)
- The exemplars repeatedly invert this to `ch(In?, Out)`, which produces a **double SRSW
  violation in one stroke**: the inbound stream becomes *2 readers / 0 writers* (never
  captured) and the outbound stream becomes *2 writers / 0 readers* (cannot be drained).
- The produce-mode *output channel arg* is repeatedly written as a bare **writer** in both
  head and body recursion, where the gold standard
  (`merge([X|Xs],Ys,[X?|Zs?]) :- merge(Ys?,Xs?,Zs)`, base case `merge([], Ys, Ys?)`)
  requires a **reader hole** `X?` in the head + writer in the body.
- The PROPOSED `link_send/3` clause in the contract — the very primitive the tutorials are
  told to thread through — itself has this bug (`ch(In?, ...)` where `send/3` has
  `ch(In, ...)`), and it is copied verbatim into `example-http-link.md` and
  `DESIGN-DOSSIER.md`. **Fix the contract primitive first; several tutorial defects are
  downstream of it.**

**The exemplars' own inline "SRSW hand-checks" cannot be trusted** — multiple checks
(`file-loopback` §3a/§3d, `websocket` 321-323, `https-http2-mtls` 370-373, `mqtt` 306-307/339,
`ble-l2cap` §3.2/§3.3) certify the buggy clauses as "Clean," each time by overlooking the
**second (body) occurrence** of the inverted variable.

---

## HIGH-SEVERITY ISSUES (14) — SRSW / mode errors; would not compile or would mis-execute

### `contracts/link-primitives.md` (+ `example-http-link.md` + `DESIGN-DOSSIER.md`)

**H1 — `link_send/3` consumed-channel arg2 inverts inbound mode (SRSW: 2 readers / 0 writers).**
*Location:* `link-primitives.md` §2.5 line 192; identically `DESIGN-DOSSIER.md` §2 line 80 and
quoted in `example-http-link.md` §3 line 134.
Clause: `link_send(Msg, ch(In?, [Msg?|Out?]), ch(In?, Out)) :- ground(Msg?) | true.`
Arg2 (`Link(In,Term)?`, consume ↓) must hold a **writer** `In` at the inbound `ch` slot to
capture the stream; the clause wrote reader `In?`. `In?` then appears as a reader in both
arg2 and arg3 → two readers, zero writers, wrong mode. Deviates from the `self.glp:94`
`send/3` it claims to mirror (`send(X, ch(In, [X?|Out?]), ch(In?, Out))`).
*Fix:* `link_send(Msg, ch(In, [Msg?|Out?]), ch(In?, Out)) :- ground(Msg?) | true.` Apply the
identical one-character fix in all three files. (Load-bearing — tutorials thread through this.)

**H2 — `link_setup/4` double-writes the outbound stream `Out`.**
*Location:* `link-primitives.md` §2.1 lines 97-99; identically `DESIGN-DOSSIER.md` §2 lines 72-73.
Head arg3 `Link(_,_)` (produce ↑) → `ch(In?, Out)` exposes writer `Out` (the caller fills it);
the body kernel `'_link_setup'(LinkId?, Role?, In, Out, Faults)` passes `Out` **again as a
writer**. Two writers, zero readers. The host EGRESS DRAINER *reads* the outbound stream, so
the kernel must hold its **reader**.
*Fix:* `'_link_setup'(LinkId?, Role?, In, Out?, Faults)` (keep head `ch(In?, Out)`); declare the
kernel's outbound arg as a reader. `In` is already correct.

**H3 — `request_link/4` and `accept_link/4` double-write the outbound stream `Link_Out`.**
*Location:* `link-primitives.md` §2.4 lines 157-160 & 165-168; `accept_link` also `DESIGN-DOSSIER.md`
§2 lines 75-78. Both kernels are passed `Link_Out` as a producer-writer **and** `Link_Out`
appears as a writer in `Link = ch(Link_In?, Link_Out)` → two writers, zero readers. Outbound
stream is drained (read) by the host.
*Fix:* `'_link_request'(LinkId?, ToPeer?, Link_In, Link_Out?, Faults), Link = ch(Link_In?, Link_Out).`
(and likewise `'_link_accept'(...)`). `Link_In` is already correct.

### `tutorials/file-loopback.md`

**H4 — `run_producer/2` double-writes `Out` (head + body) and uses writer-mode at a reader hole.**
*Location:* §3a `run_producer(ch(_In, Out), _Faults) :- produce([10, 20, 30], Out).`
`produce/2` arg2 is a writer; `Out` is a writer in the head **and** the body → two writers,
zero readers (no ground-implying guard can relax it — `Out` is a fresh output). Separately,
`Out` is in writer-form at the OUTBOUND slot of a CONSUMED (reader) channel, whose second slot
is the reader hole `Out?` per `Channel ::= ch(In, Out?)`; a producer cannot obtain a writer for
its outbound stream from a reader-channel's Out position.
*Fix:* Hold the channel so its outbound stream is the producer's own writer (writer-channel
polarity) and write it exactly once, threading via PROPOSED `link_send/3`'s returned channel
(e.g. `send_stream([10,20,30], Link, Link1)` loop), rather than destructuring and binding `Out`
in place. If destructuring is kept, `Out` must be a single writer: head reader-form `Out?` paired
with one body writer `Out`.

**H5 — `run_consumer/2` binds `[]` at the outbound reader hole (suspend-forever, not graceful close).**
*Location:* §3a `run_consumer(ch(In, []), _Faults) :- consume(In?).`
The second `ch` slot is the reader side of the consumer's outbound stream (`Out?`). Binding it to
`[]` in the head is a READ-match against a reader the consumer does not own the writer for — not
"Out := []." At runtime it suspends waiting for the *other* end to write the consumer's outbound
stream as `[]`, which never happens (the comment claiming a graceful close is wrong).
*Fix:* Bind the outbound `[]` at a *writer* position — hold the channel so Out is the consumer's
writer (writer-channel polarity / send idiom), not at the reader hole of a `Link?` reader-channel.

**H6 — `run_consumer_monitored/2` repeats the H5 outbound-reader-hole `[]` error.**
*Location:* §3d `run_consumer_monitored(ch(In, []), Faults) :- consume(In?), watch(Faults?).`
Same root cause as H5: `[]` at the outbound reader hole is a read-match the consumer cannot
satisfy → suspends forever. (The `In`/`In?` and `Faults`/`Faults?` pairs are clean.)
*Fix:* Identical to H5 — bind the outbound `[]` at a writer position obtained by writer-channel
polarity / send idiom.

**H7 — `run_producer/2` head arg-1 mode error (writer at a reader-channel's outbound slot).**
*Location:* §3a, head arg-1 of `run_producer`.
Arg-1 `Link(_,_)?` expands to a reader channel whose second slot is the reader hole `Out?`. The
producer is given a reader-channel that exposes Out only as a reader, so it cannot send through
this argument at all — the same root confusion as H4/H5/H6.
*Fix:* Give the producer the channel in the form where Out is its writer (writer-channel polarity,
or via `link_send/3`'s returned channel).

### `tutorials/websocket.md`

**H8 — `send_edits/3` clause 1 double-writes the output channel `LinkOut`.**
*Location:* §3 line 255 `send_edits([E|Es], Link, LinkOut) :- ground(E?) | link_send(E?, Link?, Link1), send_edits(Es?, Link1?, LinkOut).`
Arg3 `Link(In,Edit)` is produce ↑; a whole-channel var there must be the reader hole `LinkOut?`.
The clause writes writer `LinkOut` in the head AND passes writer `LinkOut` in the body recursion
→ two writers, zero readers.
*Fix:* `send_edits([E|Es], Link, LinkOut?) :- ground(E?) | link_send(E?, Link?, Link1), send_edits(Es?, Link1?, LinkOut).`

**H9 — `send_edits/3` clause 2 (base case) double-writes `Link`.**
*Location:* §3 line 259 `send_edits([], Link, Link).`
arg2 (consume ↓ → writer `Link`) and arg3 (produce ↑ → reader `Link?`) both written as the bare
writer `Link` → writer appears twice. `Link` is a channel/compound type, so constant-type SRSW
relaxation does not apply. Every analogous base case (`merge([], Ys, Ys?)`, `append([], Ys, Ys?)`)
has a reader output arg.
*Fix:* `send_edits([], Link, Link?).` (arg2 writer captures the channel; arg3 reader is the
pass-through hole).

**H10 — `ingest_edits/2` double-writes the output channel `LinkOut`.**
*Location:* §3 line 281 `ingest_edits(Link, LinkOut) :- link_recv(E, Link?, Link1), record_edit(E?), ingest_edits(Link1?, LinkOut).`
Arg2 `Link(Edit, Out)` is produce ↑; head var must be reader `LinkOut?`. Writes writer in head +
writer in body recursion → two writers, zero readers.
*Fix:* `ingest_edits(Link, LinkOut?) :- link_recv(E, Link?, Link1), record_edit(E?), ingest_edits(Link1?, LinkOut).`

### `tutorials/mqtt.md`

**H11 — `run_device/1` consumed-Link head `ch(CmdsIn?, TeleOut)` inverts both slots (double SRSW).**
*Location:* §3 exemplar lines 280-284. Arg1 is a consumed channel; per the flip rule it must be
`ch(In, Out?)` = writer `CmdsIn` + reader hole `TeleOut?`. As written, the body reads inbound via
`handle_commands(CmdsIn?)` and writes outbound via `send_telemetry(Samples?, TeleOut)`, so
`CmdsIn` = head-reader + body-reader (2R/0W) and `TeleOut` = head-writer + body-writer (2W). The
inline self-check (lines 306-307) wrongly certifies it clean.
*Fix:* `run_device(ch(CmdsIn, TeleOut?), _Faults)` — writer `CmdsIn` pairs with `CmdsIn?` reader,
reader hole `TeleOut?` pairs with `TeleOut` writer; then correct the inline note.

**H12 — `run_gateway/1` consumed-Link head `ch(TeleIn?, CmdsOut)` inverts both slots (double SRSW).**
*Location:* §3 exemplar lines 316-320. Identical to H11: `TeleIn` = 2 readers / 0 writers,
`CmdsOut` = 2 writers. Inline self-check (line 339) repeats the wrong claim.
*Fix:* `run_gateway(ch(TeleIn, CmdsOut?), _Faults)`.

### `tutorials/ble-l2cap.md`

**H13 — `run_band/2` consumed-Channel head `ch(Credits?, Data)` inverts both slots (double SRSW).**
*Location:* §3.2 `run_band(ch(Credits?, Data), _Faults) :- sensor_batch(Batch), produce(Batch?, Credits?, Data).`
Consumed channel must be `ch(Credits, Data?)`. As written, `Credits?` = head-reader + body-reader
(2R/0W) and `Data` = head-writer + body-writer (2W/0R). `Credits`/`Data` are streams, never
ground-gated → no relaxation.
*Fix:* `run_band(ch(Credits, Data?), _Faults) :- sensor_batch(Batch), produce(Batch?, Credits?, Data).`

**H14 — `run_phone/2` consumed-Channel head `ch(Data?, Credits)` inverts both slots (double SRSW).**
*Location:* §3.3 `run_phone(ch(Data?, Credits), _Faults) :- consume(Data?, Credits).`
Consumed channel must be `ch(Data, Credits?)`. As written, `Data?` = 2R/0W, `Credits` = 2W/0R.
*Fix:* `run_phone(ch(Data, Credits?), _Faults) :- consume(Data?, Credits).`

### `tutorials/https-http2-mtls.md`

**H15 — `run_fintech/2` clause 1 `ch(Credits?, Out)` inverts both slots (double SRSW).**
*Location:* §3.4 line 354 `run_fintech(ch(Credits?, Out), _Faults) :- produce_records([txn(1,500), txn(2,1200), txn(3,750)], Credits?, Out).`
Consumed channel must be `ch(Credits, Out?)`. As written, `Credits?` = head-reader + body-reader
(2R/0W; inbound credit stream never captured) and `Out` = head-writer + body-writer (2W/0R). The
sibling clause `run_bank` (line 310) gets the identical structure right (`ch(In, Out?)`), proving
the intended discipline; only `run_fintech` deviates. The §3.4 hand-check (lines 370-373) states
the modes backwards.
*Fix:* `run_fintech(ch(Credits, Out?), _Faults) :- produce_records([txn(1,500), txn(2,1200), txn(3,750)], Credits?, Out).`
and correct the hand-check prose.

> **Note on the HIGH count:** 17 HIGH defects total. H4/H7 are both in `file-loopback` §3a
> `run_producer` (H4 = the double-write SRSW; H7 = the head-arg-1 mode error), counted as part of
> that file's 4 HIGH alongside H5/H6. Per-file HIGH totals: contracts 3, file-loopback 4,
> websocket 3, mqtt 2, ble-l2cap 2, https-http2-mtls 1, coap 2 (type/behavior) = **17**. Of these,
> **15 are SRSW/mode compile-blockers** (H1–H15) and **2 are coap type/behavior items** (H16, H17)
> that are HIGH by the "GLP-correct as written" bar but are not SRSW (listed last in this section).

### `tutorials/coap.md` (HIGH type/behavior — SRSW per-clause is clean)

**H16 — `readings/1` places `[]` as a Stream data element (ill-typed + wrong output + breaks SC-001).**
*Location:* §3.0 line 226 `readings([21, 22, 23, []]).`
`[]` is the 4th *element* of a 4-element list, but `Stream(Integer) ::= [] ; [Integer | Stream(Integer)]`
— `[]` is not an Integer. `consume_unsplit([V|Vs])` matches `V=[]`, runs `use_reading([])` and
prints `[]`, so output is `21,22,23,[]`, contradicting the stated "prints 21, 22, 23" (line 239,
A1 SC-001) and diverging from §3.1 `sample_readings([21,22,23])`, breaking the byte-identical claim.
*Fix:* `readings([21, 22, 23]).` (the `[]` tail terminates the stream implicitly). If an explicit
EOS marker element is wanted, widen the type to a union (e.g. `Reading ::= Integer ; eos`) and add a
matching `consume_unsplit` clause.

**H17 — `use_or_stop/2` declares arg1 `Integer?` but clause 1 head matches `[]`.**
*Location:* §3.1 decl line 296 + clause 1 line 297 `use_or_stop([], _Link)`.
`[]` is not an Integer; the head literal is ill-typed against the declared `Integer?` domain. At
runtime `V` ranges over Integer (a reading) OR `[]` (the stream-end marker), so the declared type
is too narrow.
*Fix:* Widen arg1 to the actual value domain, e.g. `Reading ::= Integer ; []` and
`procedure use_or_stop(Reading?, Link(_, _)?).`

---

## MED-SEVERITY ISSUES (11) — questionable / type-level / cross-doc consistency / completeness

### `contracts/link-primitives.md` (+ dossier)

**M1 — Returned output holes `Faults`/`Link` are bare writers in heads (idiom violation).**
*Location:* §2.1/§2.2/§2.3/§2.7 + dossier §2/§4: `link_setup/4`, `server_listener/3`,
`client_connector/3`, `request_link/4`, `accept_link/4`, `link_monitor/2`, `link_close/*`.
An output produced by a body subgoal must carry the **reader hole** in the head per the
`test_double(X, Y?) :- double(X?, Y)` idiom (manual §19.4). Here `Faults`/`Link` are bare writers
in the head AND writers in the body → the writer half occurs twice, reader half never.
*Fix:* Mark head output holes as readers, e.g.
`link_monitor(LinkId, Faults?) :- ground(LinkId?) | '_link_monitor'(LinkId?, Faults).`;
`server_listener(LinkId, Link?, Faults?) :- ...`; same for `client_connector`, and the `Faults`
args of `link_setup`/`request_link`/`accept_link`.

**M2 — §5 role skeleton `main/2` carries a named-but-unread input `NetIn`.**
*Location:* §5 lines 306-314. Head writer `NetIn` (arg2 `Stream(_)?`) is never read in either
clause body — a named non-anonymous writer with no paired reader. Manual §9.1 requires an
abandoned input to be an anonymous writer `_NetIn`.
*Fix:* `main(Me, _NetIn) :- ...` in both clauses (or actually consume `NetIn?`).

### `tutorials/websocket.md`

**M3 — `closed/2` matched over a `Stream(Fault)` though `Fault` has no `closed/2` constructor.**
*Location:* §4.1 `on_fault/1` clause 3 line 381 `on_fault([closed(L, R)|_]) :- ground(L?) | handle_closed(L?, R?).`
(and the `closed(LinkId, …)` monitor terms in §2.6 / IT-WS-5 / IT-WS-7). The authoritative
`Fault ::= ok ; tempFail(LinkId, Reason) ; permFail(LinkId, Reason).` (link-primitives §1) has no
`closed/2` — a type error that contradicts B-WS-1's type-check claim. (SRSW is fine.)
*Fix:* Either add `closed(LinkId, Reason)` to the `Fault` union in link-primitives §1 (and reconcile
§2.7), or remove the `closed` clause/monitor terms. The two specs must agree on the fault vocabulary.

**M4 — Client inbound typed `Edit` but carries `Broadcast` values (type mismatch).**
*Location:* §3 `recv_feed/1` decl line 262 `procedure recv_feed(Link(Edit, _)?).` and
`apply_broadcast/1` decl line 268 `procedure apply_broadcast(Edit?).` The client In stream carries
server→client broadcasts (`feed_items` produces `ack(1..3)` of type `Broadcast`), but the channel
element is typed `Edit` and `apply_broadcast` takes `Edit?`. `ack/1` is a `Broadcast` constructor,
not an `Edit`.
*Fix:* `procedure recv_feed(Link(Broadcast, _)?).` and `procedure apply_broadcast(Broadcast?).`
(or a shared Edit/Broadcast union). SRSW otherwise clean.

**M5 — Receivers lack a stream-end base clause (suspend-forever, contradicting the prose).**
*Location:* §3 `recv_feed/1` line 263 and `ingest_edits/2` line 281. Both are single-clause infinite
recursions over `link_recv`, which only matches a cons; there is no clause matching the closed
inbound `[]`. The prose/trace describe a graceful close that the clauses cannot realize — the goal
suspends forever instead of terminating. (Completeness/liveness gap, not SRSW.)
*Fix:* Add an EOS base clause to each receiver matching the closed inbound stream, e.g.
`recv_feed(ch([], _Out)).` and `ingest_edits(ch([], _Out), LinkOut?).` (note the output arg must be a
reader `LinkOut?` per H10's fix).

### `tutorials/mqtt.md`

**M6 — `:=` placed in the GUARD zone of `drain_telemetry/2`.**
*Location:* §3 line 332 `drain_telemetry([S|In], N) :- N? > 0, N1 := N? - 1 | use_sample(S?), drain_telemetry(In?, N1?).`
`:=` is a body kernel that BINDS a fresh writer (`N1`); guards are pure three-valued tests. Every
instance of this countdown idiom in `programs/` puts the comparison in the guard and `:=` as the
first BODY goal. As written it is non-idiomatic and likely mis-compiles / is rejected. (SRSW
accounting itself is fine.)
*Fix:* `drain_telemetry([S|In], N) :- N? > 0 | N1 := N? - 1, use_sample(S?), drain_telemetry(In?, N1?).`

### `tutorials/file-loopback.md`

**M7 — Missing `procedure` declarations for `note_closed/2` and `note_temp/2`.**
*Location:* §3d `watch` clauses calling `note_closed(L?, R?)` / `note_temp(L?, R?)`. No
`procedure note_closed(...)` / `procedure note_temp(...)` appears in the file (only `give_up/2` is
declared). DECLARATIONS requirement unmet. (SRSW in these clauses is fine.)
*Fix:* Add `procedure note_closed(LinkId?, Reason?).` and `procedure note_temp(LinkId?, Reason?).`
matching the reader modes used, consistent with the declared `give_up(LinkId?, Reason?).`

### `tutorials/ble-l2cap.md`

**M8 — Tutorial `Link` type diverges from the authoritative contract type.**
*Location:* §3 comment `% Link(In, Out) ::= ch(In, Out?).` vs link-primitives §1
`Link(In, Out) ::= Channel(Stream(In), Stream(Out))` = `ch(Stream(In), Stream(Out)?)`. The tutorial
drops the `Stream` wrapping; the divergence is load-bearing for the head mode analysis (under the
contract type `run_band`'s arg would double-wrap to `ch(Stream(Stream(Credit)), …)`).
*Fix:* Align the tutorial to the contract (`Link(In, Out) ::= Channel(Stream(In), Stream(Out))`,
passing element types `Link(Credit, Integer)?` / `Link(Integer, Credit)?`), or state explicitly that
this tutorial uses a flattened `Link` alias and reconcile to a single authoritative definition.

**M9 — Test A-BLE-2 oracle's suspend rationale is wrong for a ground `[]` arg.**
*Location:* §4 Section A, goal `produce([99|_], [], Out).` with oracle "Suspended on the
[more|Credits] reader." Arg2 is a GROUND `[]`, not an unbound reader. Clause 1 head `[more|Credits]`
vs bound `[]` is a definite mismatch; clause 2 mismatches arg-1 → matches no clause; it does not
suspend on the credit reader. Three-valued suspend requires an UNBOUND reader, not a closed `[]`.
*Fix:* Use an open/unbound credit stream to illustrate credit-gated suspend, e.g.
`produce([99|_], Credits?, Out).` (Credits unbound) — clause 1 then suspends on the unbound
`[more|Credits]` head reader (intended FR-025 behavior).

### `tutorials/coap.md`

**M10 — `use_reading/1` declared+defined twice within one module (duplicate procedure).**
*Location:* §3.0 lines 228-229 and §3.1 lines 301-302, both inside `-module(coap_sensor_collector).`
(line 244). Test A1 (line 427) loads the module and runs `go_unsplit`, so both occurrences live in
the same module → duplicate-procedure error.
*Fix:* Declare/define `use_reading/1` exactly once (keep §3.1, drop the §3.0 duplicate, or factor
§3.0 into a separate baseline module).

**M11 — `closed/2` matched over `Stream(Fault)` though `Fault` has no `closed/2` constructor.**
*Location:* §3.4 `watch/1` clause 4 line 398 `watch([closed(L, R)|_]) :- ground(L?) | note_closed(L?, R?).`
The tutorial (§2.7, §5 IT-COAP-5) introduces clean-close terms `closed(LinkId, eos)` / `closed(L, R)`
but `Fault` (link-primitives §1) was never extended with `closed/2`, so matching it over a
`Stream(Fault)` is ill-typed. (Clause SRSW is clean.) Same root issue as M3 in websocket.
*Fix:* Add `closed(LinkId, Reason)` to the `Fault` union in link-primitives §1, or drop the `closed/2`
watch clause if clean-close is signalled differently. Keep type contract and exemplar in sync.

---

## LOW-SEVERITY ISSUES (9) — style / cosmetic / doc-wording

### `contracts/link-primitives.md` (+ dossier)

**L1 — Undeclared predicates in illustrations.**
*Location:* DESIGN-DOSSIER §3 `drain` calls `use/1` with no `procedure use(...).`; likewise the
undeclared `handle_perm/2`, `handle_temp/2`, `handle/2` in the §2.7 `on_fault` illustration and the
omitted `procedure` lines for `link_close/1,/2` in DESIGN-DOSSIER §2.
*Fix:* Add `procedure use(Item?).` etc., or note them as illustrative external sinks.

**L2 — Inconsistent role-selector constant form + undefined `Compound` type reference.**
*Location:* link-primitives §5 uses `Me? =?= "producer"`/`"consumer"` (quoted strings) while
example-http-link §1/§5 and DESIGN-DOSSIER §5 use bare atoms `producer`/`consumer`. Both are valid
GLP constants, but the parallel illustrations diverge. Also `Reason ::= String ; Compound` references
`Compound`, which is the guard `compound/1`, not a defined GLP type (the "any" primitive is `_`).
*Fix:* Pick one constant form (recommend bare atoms to match the `AgentId ::= String` alternative) and
use it in all three files; replace the `Compound` type reference with `_` or a concrete reason union.

### `tutorials/file-loopback.md`

**L3 — Inline hand-checks falsely certify the buggy producer/consumer clauses "Clean."**
*Location:* §3a note lines 263-264 and §3d note line 339. The narrative "the channel's Out is bound
to [] in the head (this end sends nothing back)" misstates that a reader-hole can be bound by the
consuming clause. (The hand-checks for `produce`, `consume`, `use_value`, the §3c bounded-pipe
clauses, the `watch` ok/permFail clauses, and `give_up` are accurate.)
*Fix:* Correct the hand-check to flag these clauses (or fix the clauses per H4–H7 and update the note).

### `tutorials/websocket.md`

**L4 — Role selectors use unquoted atoms not matching the AgentId `String` alternative.**
*Location:* §3 `main/1` lines 224/232 `Me? =?= editor_client` / `editor_server`. `AgentId ::= String ;
Integer ; peer(String,Integer)`; the §5 idiom uses string literals (`Me? =?= "producer"`). `=?=`
works on any ground constants so runtime/SRSW is fine, but the bare atoms don't match the declared
`String` alternative.
*Fix:* `Me? =?= "editor_client"` / `"editor_server"` (or extend AgentId to admit atom constants).

### `tutorials/mqtt.md`

**L5 — `on_fault/1` fragment: unread named writer `R` + missing decl/period.**
*Location:* §4 A-MQTT-6 inline fragment lines 388-389 `on_fault([permFail(L, R)|_]) :- ground(L?) | '_output'(permFail)`.
`R` is a named head writer never read (singleton-writer smell; anonymous `_R` is the SRSW-clean form);
no `procedure on_fault(...)` decl and no terminating period.
*Fix:* `on_fault([permFail(L, _R)|_]) :- ground(L?) | '_output'(permFail).` and add
`procedure on_fault(FaultStream?).`

**L6 — Type definition interleaved among procedure bodies (grouping nit).**
*Location:* §3 lines 296-297/269-270: `Command ::= setRate(Integer) ; ping.` declared AFTER
`procedure handle_commands(Stream(Command)?).` that uses it; mixing a type def in the middle of
procedure bodies is inconsistent with grouping type decls before procedures. Likely type-checks
(type defs are order-independent).
*Fix:* Hoist `Command ::= setRate(Integer) ; ping.` above `procedure handle_commands/1`, grouped with
the other type declarations near the module head.

### `tutorials/https-http2-mtls.md`

**L7 — Role selectors use bare atoms `fintech`/`bank` (cosmetic).**
*Location:* §3.2 `main/2` lines 276-288 `Me? =?= fintech` / `bank`. In glpnet a bare atom is a String
constant, so `=?=` accepts it and the `String` alternative covers it — no compile error. Using
`"fintech"`/`"bank"` would match the cited §5 skeleton.
*Fix:* Optional — quote the literals (`Me? =?= "fintech"` / `"bank"`), or leave as valid String
constants. Cosmetic only.

### `tutorials/ble-l2cap.md`

**L8 — Hand-check prose mislabels arity-2 `drain/2` as "drain/3."**
*Location:* §3.3 prose. The declaration and both clauses are arity 2; the SRSW hand-check refers to
"drain/3" twice. Documentation/arity-label inconsistency only; the code is arity-consistent and the
clauses are SRSW-clean.
*Fix:* Change the prose references from "drain/3" to "drain/2."

### `tutorials/coap.md`

**L9 — Role selectors use bare atoms `sensor`/`collector`; one hand-check comment misnames an anon writer.**
*Location:* §3.1 `main/1` lines 256-267 / §3.3 lines 371,377 `Me? =?= sensor` / `collector` vs the §5
skeleton's quoted strings — same AgentId `String`-alternative question as L4/L7 (SRSW fine, `=?=` is
ground-implying). Also §3.1 `run_sensor/2` hand-check (line 310) calls `_Faults` (a legal anonymous
**writer** capturing the fault stream at the ↓ FaultStream? position) an "anonymous reader" — wrong
term (an anon reader `_?` would be illegal); the code is correct.
*Fix:* Quote the role atoms (`Me? =?= "sensor"`) to match `String` (or confirm bare atoms are String
constants and note it); reword the hand-check note to "anonymous writer (unread fault stream — legal)."
No code change for the comment item.

---

## Cross-Cutting Recommendations for the Plan Gate

1. **Fix the PROPOSED contract primitives FIRST** (H1 `link_send/3`, H2 `link_setup/4`,
   H3 `request_link`/`accept_link`, M1 output holes). They are the source-of-truth the tutorials
   thread through; several tutorial defects are downstream of the buggy `link_send/3`.
2. **Apply the one rule everywhere a whole channel is destructured:** consumed channel head =
   `ch(In, Out?)` (writer In, reader hole Out?); produce-mode output arg = reader hole `X?` in the
   head + writer `X` in the body. This single discipline resolves H4–H15.
3. **Reconcile the `Fault` vocabulary** across link-primitives §1/§2.7 and the tutorials: decide
   whether `closed/2` is a `Fault` member (resolves M3, M11).
4. **Reconcile the `Link` type definition** (M8) to a single authoritative form.
5. **Distrust the inline hand-checks** (L3, and the wrong certifications noted under H4–H15) — they
   repeatedly certify the buggy clauses clean; regenerate them after the mode fixes.
6. **Verified-clean substrate:** the pure stream-processing clauses (producers/consumers/drains/
   bounded-pipe credit coupling, fault watchers' `ok`/`permFail`/`tempFail` clauses), the
   `link_recv/3` shape across all files (exact `self.glp:97` `receive/3`), ground-implying-guard SRSW
   relaxation, three-valued suspend-on-unbound-reader behavior, head-constructed writer outputs, and
   absence of any Prolog-isms (no cut/`->`/findall/assert) are all GLP-correct. The headline defects
   are localized mode flips on channel heads and the contract output holes — not a systemic rewrite.

---

*Review report only. No code or spec changes were made.*
