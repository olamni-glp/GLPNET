# GLP Correctness Review — Pass 2 (Re-review of FIXED feature-025 exemplars)

**Date:** 2026-06-06
**Authority:** `contracts/glp-canonical-forms.md` (REPL-verified, authoritative), `docs/glp-cheat-sheet.md`, `docs/typed-glp-manual.md` (§2A flip, §9 anon, §19.4 output holes), `programs/self.glp:90-97`, `contracts/link-primitives.md` + `contracts/guards.md` (PROPOSED signatures).
**Scope:** Re-review of all GLP clauses in the seven fixed exemplar/contract files for residual GLP-correctness defects (SRSW / mode / three-valued / type / missing-decl). Exemplars are ILLUSTRATIVE (PROPOSED primitives) but must be GLP-correct as written.
**Method:** Synthesis of seven independent per-file re-reviews, cross-checked against the canonical card. No fixes applied — findings only.

---

## Headline

The pass-1 fixes resolved the *structural* defect classes the prior adversarial review flagged: send-shape, output-hole idioms, double-inverted channel heads, producer double-writers, consumer-close form. **None of those re-appear.** The consumer-close `ch(In, [])` form is confirmed correct everywhere and was NOT re-raised (per the card's explicit certification).

**One residual defect class dominates pass 2:** a NAMED underscore variable (`_Faults`, `_Credits`, `_Link`, `_N`, `_Link1`) placed at an IGNORED position (top-level arg or body discard) where the canonical card requires a BARE `_`. The card's REPL-verified ANTI-form (`prod(ch(_, Out?), _Faults)` → `[codegen] Undefined variable: _Faults`) certifies these as hard load-blockers. This single class accounts for 13 of the 16 residual findings. A second, smaller class is missing procedure declarations for called primitives/sinks (3 low findings), plus one genuine head-construction mode defect (body-`=` channel build) in the contract file.

A recurring secondary problem: several files' OWN inline SRSW hand-checks actively MIS-CERTIFY these named underscores as "anonymous / exempt" — the root misconception that let the code defect survive the fix. The prose needs the same correction as the code.

---

## Per-file verdicts

| File | Clauses | Verdict | High | Med | Low |
|---|---|---|---|---|---|
| `tutorials/file-loopback.md` | 22 | **ISSUES** | 2 | 0 | 2 |
| `tutorials/websocket.md` | 22 | **ISSUES** | 2 | 0 | 1 |
| `tutorials/https-http2-mtls.md` | 17 | **ISSUES** | 2 | 0 | 1 |
| `tutorials/mqtt.md` | 20 | **ISSUES** | 1 | 0 | 0 |
| `tutorials/coap.md` | 30 | **ISSUES** | 3 | 1 | 0 |
| `tutorials/ble-l2cap.md` | 12 | **ISSUES** | 3 | 0 | 0 |
| `contracts/link-primitives.md` (+ `example-http-link.md` + `DESIGN-DOSSIER.md`) | 47 | **ISSUES** | 2 | 1 | 0 |
| **TOTAL** | **170** | **ISSUES** | **15** | **2** | **4** |

**Overall verdict: ISSUES (NOT pass-2-clean).** 7/7 files carry at least one residual defect. 15 high, 2 med, 4 low. Every high is the named-`_Foo`-at-ignored-slot codegen blocker except the one body-`=` channel-construction mode error in `link-primitives.md` (which the card certifies as an equivalent REPL failure: `writer requires ↑, got ↓`).

---

## Residual findings (detail)

### `tutorials/file-loopback.md` — 22 clauses, ISSUES (2H/2L)
- **HIGH** — §3c L308 `produce_bounded([], _Credits, []).` — named `_Credits` at ignored arg2 (credit-stream slot). Card ANTI-form → `[codegen] Undefined variable: _Credits`. Fix: bare `_`. The §3c SRSW hand-check (L319-324) wrongly asserts "Clean"; §3a's own note (L262-263) already states the correct rule.
- **HIGH** — §3d L343 `give_up(L, _R) :- ground(L?) | link_close(L?, abandoned).` — named `_R` at ignored arg2 (Reason?). Card ANTI-form → `[codegen] Undefined variable: _R`. Fix: bare `_`. §3d note (L346-353) wrongly asserts "Clean".
- **LOW** — §3d L328-344 `link_close/2` is CALLED but has no procedure/type declaration anywhere, and `link-primitives.md` declares only the 8 base primitives (link_close is prose-only, no signature). Fix: declare a PROPOSED `link_close(LinkId?, Reason?).` (preferably as the 9th primitive in the contract).
- **LOW** — §B unit-B-03 L413 `pick(P1, P2, P1) :- P1? @< P2? | true.` — inline illustrative clause has no procedure decl. The clause itself is SRSW-legal under the `@<` ground-implying relaxation (correct per PROPOSED guards.md); only the decl is missing. Belongs to the guards facet → low.

### `tutorials/websocket.md` — 22 clauses, ISSUES (2H/1L)
- **HIGH** — L243 `run_client(Link, _Faults)` — named `_Faults` at ignored top-level arg. Card ANTI-form L28 → `[codegen] Undefined variable: _Faults`. Fix: bare `_`. Line-318 hand-check mislabels `_Faults` "anon".
- **HIGH** — L276 `run_server(Link, _Faults)` — same defect. Fix: bare `_`.
- **LOW** — L393-399 `on_fault/1` watcher bodies call `handle_perm/2`, `handle_temp/2`, `handle_closed/2` with no procedure decls in the block, though `link-primitives.md` §2.7 (L252-253) DOES declare the first two as illustrative external sinks. Internal inconsistency. Fix: add the three sink decls mirroring the contract.

### `tutorials/https-http2-mtls.md` — 17 clauses, ISSUES (2H/1L)
- **HIGH** — §3.3 L310 `run_bank(ch(In, [more, more, more | Out?]), _Faults)` — named `_Faults` at ignored top-level arg. Card ANTI-form → `[codegen] Undefined variable: _Faults`. Fix: bare `_`. (The channel deconstruction/outbound construction itself is correct, forms #1/#4/#5.) Line-332 hand-check wrongly calls `_Faults` "anonymous".
- **HIGH** — §3.4 L354 `run_fintech(ch(Credits, Out?), _Faults)` — same defect. Fix: bare `_`. Line-375 prose to correct.
- **NOTE** (folded into the count as the file's LOW) — §3.4 L366 `produce_records([], _Credits, []).` is a THIRD instance of the same high class (`_Credits` at ignored arg). *Synthesizer note: the per-file reviewer scored this file 2H/1L, listing two `_Faults` highs plus one informational `main/1`-arity low. The L366 `_Credits` named-anon is the same REPL-certified high-severity class as the other tutorials' base-clause `_Credits` (cf. coap §3.2 L348, ble §3.2 L279, DESIGN-DOSSIER L117), and should be fixed to bare `_` together with them.* Fix: `produce_records([], _, []).`
- **LOW (informational)** — §3.2 L276 `main/1` drops the `NetIn` last arg that the boot/@-idiom skeleton (`link-primitives.md` §5; agent_runtime.dart:202-226) carries. Internally consistent and GLP-correct as written, but diverges from the boot-harness shape; may not be drivable by the real boot path. Optional fix: `procedure main(AgentId?, Stream(_)?).` + bare `_` NetIn arg.

### `tutorials/mqtt.md` — 20 clauses, ISSUES (1H/0/0)
- **HIGH** — §3 L334 `drain_telemetry([], _N).` — named `_N` at ignored arg2 (budget). Card ANTI-form → `[codegen] Undefined variable: _N`. Fix: bare `_`. The inline SRSW note (L340-347) audits only the recursive clause and skips the base clause.
- Everything else clean. `_More` (L270, output list-tail) and `_R` (L392, ignored arg inside a compound at a consume position) are NOT defects (manual §9.3 permits them) and were correctly NOT flagged.

### `tutorials/coap.md` — 30 clauses, ISSUES (3H/1M/0)
- **HIGH** — §3.1 L271 `run_sensor(Link, _Faults) :- ...` — named `_Faults` at unused top-level arg → `[codegen] Undefined variable: _Faults`. Fix: bare `_`. Hand-check L314-315 wrongly certifies it a "legal anonymous writer".
- **HIGH** — §3.1 L288 `run_collector(Link, _Faults) :- ...` — same defect. Fix: bare `_`.
- **HIGH** — §3.1 L301 `use_or_stop([], _Link).` — named `_Link` at unused top-level arg (unit clause) → `[codegen] Undefined variable: _Link`. Fix: bare `_`.
- **HIGH** — §3.2 L348 `produce([], _Credits, []).` — named `_Credits` at unused top-level arg → `[codegen] Undefined variable: _Credits`. Fix: bare `_`. §3.2 hand-check (L359-366) does not address it.
- **MED** — §3.1 L284 `link_send([], Link?, _Link1).` — named `_Link1` is a singly-occurring discarded BODY-subgoal output. Card rule: ignored positions use bare `_`. (Card's two REPL-verified failures are HEAD positions, so confidence is lower for a body discard → med.) Fix: bare `_`. Hand-check L321 wrongly defends `_Link1` as a "legal anonymous writer".

### `tutorials/ble-l2cap.md` — 12 clauses, ISSUES (3H/0/0)
- **HIGH** — §3.2 L271 `run_band(ch(Credits, Data?), _Faults) :- ...` — named `_Faults` at unused top-level arg → `[codegen] Undefined variable: _Faults`. Fix: bare `_`.
- **HIGH** — §3.2 L279 `produce([], _Credits, []).` — named `_Credits` at ignored top-level arg → `[codegen] Undefined variable: _Credits`. Fix: bare `_`. Hand-check L289-290 wrongly calls `_Credits` "anonymous (exempt)"; also fix the parenthetical in the A-BLE-4 row (L382).
- **HIGH** — §3.3 L305 `run_phone(ch(Data, Credits?), _Faults) :- consume(Data?, Credits).` — named `_Faults` at unused top-level arg → `[codegen] Undefined variable: _Faults`. Fix: bare `_`.

### `contracts/link-primitives.md` (+ `example-http-link.md` + `DESIGN-DOSSIER.md`) — 47 clauses, ISSUES (2H/1M/0)
- **HIGH** — `link-primitives.md:164` (request_link), `:172` (accept_link); `DESIGN-DOSSIER.md:78` (accept_link) — output channel built by a BODY unification `Link = ch(Link_In?, Link_Out).` while `Link?` is a reader-hole in the head. This is the card ANTI-form `cons(ch(In, Out?)) :- ..., Out = [].` → REPL verdict `writer requires ↑, got ↓`. Outputs must be constructed in HEADS (cheat-sheet Rule 1 / manual §6), not via body `=`. Fix: construct in the head via the output-hole idiom (the clean `link_setup/4` shape) and let a kernel subgoal fill the inner cells; remove the `Link = ch(...)` body line. (3 sites.)
- **HIGH** — `DESIGN-DOSSIER.md:117` `produce([], _Credits, []).` — named `_Credits` at ignored arg2. Card ANTI-form → `[codegen] Undefined variable: _Credits`. Fix: bare `_`.
- **MED** — `example-http-link.md:71` (run_producer), `:76` (run_consumer) — discarded `link_send`/`link_recv` output captured as named anon `_Link1` (writer never read). The file's own prose (L91-93) states the bare-`_` rule, then the code violates it. Fix: bare `_` in both (`link_send(V?, Link?, _).` / `link_recv(V, Link?, _),`). (2 sites.)

---

## Resolved prior findings (confirmed fixed, NOT re-raised)

The pass-1 round fixed the structural defects the original adversarial review (`contracts/glp-correctness-review.md`) raised. Re-review confirms these are RESOLVED across all files:

- **Send-shape (orig H1)** — `send(X, ch(In, [X?|Out?]), ch(In?, Out))` now matches card form #1 / self.glp:94 everywhere (link-primitives, all tutorials' send/out-relay clauses). Resolved.
- **Output-hole idioms (orig H2/H3, M1)** — reader-hole in head + writer in body subgoal (card form #7, manual §19.4) correctly applied: link_setup / server_listener / client_connector / link_monitor; tutorials' Link/Faults/Out/Credits/Data outputs; send_edits (websocket) arg3; ingest_edits arg2. Resolved.
- **Producer double-writer (orig H4/H7)** — producer heads now `ch(_, Out?)` with bare `_` inbound + `Out?` reader-hole + body writer (card form #2). Resolved (e.g. file-loopback run_producer, DESIGN-DOSSIER:204).
- **Double-inverted channel heads (orig H8–H15)** — consumed-channel heads are now the canonical `ch(In, Out?)` (card form #4): run_device/run_gateway (mqtt), run_band/run_phone (ble), run_bank/run_fintech (https), b2b/dev clauses, run_sensor (coap). All re-derived via the §2A flip rule against `Link(In,Out) ::= ch(Stream(In), Stream(Out)?)` and check out. Resolved.
- **Consumer-close `ch(In, [])` (orig H5/H6 — the review's WRONG finding)** — explicitly CONFIRMED CORRECT (card form #3) and deliberately NOT re-raised in any file: run_consumer / run_consumer_monitored (file-loopback), recv_feed / ingest_edits (websocket), drain_records (https), send_telemetry/send_commands `[]` (mqtt), send_all clause-2 / drain / produce `[]` base cases (coap), drain `[]` (ble), DESIGN-DOSSIER:210 run_consumer. The card-certified `[]` head-construct is the right idiom; the original review's proposed `Out = []` body-fix does NOT compile. Resolved (by NOT "fixing" it).
- **`Fault` needs `closed/2` (orig M3/M11), `Link` type (orig M8)** — type-side fixes from the prior round stand; no residual type defect observed in the watcher/fault clauses (watch/note_*, on_fault consume faults as ordinary data per FR-043, three-valued gates honored).
- **No illegal body `=`** — confirmed across all tutorials (the only `=`-class residual is the 3-site body-`=` channel construction in the CONTRACT file, newly surfaced this pass — see HIGH above; tutorials are clean).

---

## What is GLP-clean (verified, no residual issue)

- **SRSW reader/writer COUNTS** are correct throughout all 170 clauses. Every multiply-read head variable is correctly licensed by a ground-implying guard (`ground/1`, `=?=`, arithmetic `>`, or `@<`) per the ground-implying relaxation.
- **Three-valued suspend-not-fail** honored everywhere: `ground/1` and stream-head readers gate suspension, never map a suspend to a fail. Watchers consume faults as ordinary data (FR-043).
- **Head-construction** (no body `=` in tutorials): outbound streams head-constructed with reader-hole tails (`[V?|Out?]`, `[more, more, more|Out?]`); consumed-channel heads `ch(In, Out?)`; `[]` graceful-close head-construct. The lone body-`=` exception is the contract-file channel-construction HIGH.
- **Modes** re-derived via the §2A flip rule against the `Link(In,Out)` channel type; all check out.
- **Bare-`_` ignored positions** that ARE correct were not flagged (NetIn args, ignored list tails, channel slots with bare `_` inbound) — e.g. coap §3.4 watch/note_* correctly use bare `_` for ignored list tails, which is precisely why the §3.1/§3.2 named-`_Foo` slots are inconsistent.

---

## Recommendation (no fixes applied)

The dominant residual class is mechanical: convert every named `_Foo` at an ignored position to a bare `_` (13 of 16 findings; all but two are one-character head/clause edits). The contract-file body-`=` channel construction (1 HIGH, 3 sites) is a real mode defect requiring the output-hole rewrite. The 4 LOW/MED declaration gaps are additions, not rewrites. Critically, the inline SRSW hand-check prose in file-loopback (§3c/§3d), websocket (L318), https (L332/375/388), coap (L314-315/321), and ble (L289-290) actively mis-certifies named underscores as "anonymous/exempt" — that prose must be corrected alongside the code, or the same defect will recur, since the hand-checks are the mechanism that was supposed to catch it.
