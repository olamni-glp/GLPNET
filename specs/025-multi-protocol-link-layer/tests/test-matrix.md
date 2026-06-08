---
title: "Consolidated Coverage Matrix — feature 025 multi-protocol link layer"
subtitle: "Every test spec (harness substrate + per-transport unit + integration) × the success criteria SC-001..SC-017, with the gaps called out."
date: "2026-06-06"
status: "PLAN-stage / PRE-IMPLEMENTATION. The link primitives are PROPOSED (pending Gabi's language-authority approval) and NOT YET IMPLEMENTED. Every test referenced here is SPEC-LEVEL (scenario + exemplar GLP + observable outcome + pass/fail oracle); none is runnable until the primitives + the reliability sublayer + the named leaf land. This matrix records DECLARED coverage from each leaf's own SC tags; it is not a record of passing tests."
---

# 0. What this matrix is, and how to read it

This is the **single consolidated coverage view** across feature 025's whole test plan:
the shared integration-test **harness** (the substrate every leaf rides), and the **unit**
+ **integration** test specs of all six transport tutorials. Rows are test specs; columns
are the success criteria **SC-001 .. SC-017** from [`../spec.md`](../spec.md). A mark means
that test spec **declares** it satisfies that SC (the SC tag is taken verbatim from the
test's own "Satisfies"/"SC" field in its source file — this matrix does not re-derive
coverage, it aggregates it).

Legend:

- **●** — primary coverage: the test's headline SC (its reason to exist).
- **○** — secondary / supporting coverage: the test exercises the SC but it is not the
  headline (e.g. a split test that incidentally reactivates a suspended reader, SC-003; a
  close test that supports GC-to-baseline, SC-014).
- *(blank)* — not claimed by that test.

Cross-cutting framing (carried from every source doc): all primitives are **PROPOSED / not
implemented**; all GLP is **ILLUSTRATIVE**; all tests are **SPEC-LEVEL**; the base link is
**peer-to-peer to the immediate peer** (any broker is at another level, out of scope);
**GLP semantics are preserved exactly** (SRSW never relaxed by a flag; writer-MGU;
three-valued unification ⇒ Suspend not Fail; bind-once; per-link FIFO).

The success criteria (abbreviated; authoritative text in [`../spec.md`](../spec.md) §Success Criteria):

| SC | One-line |
|---|---|
| SC-001 | Headline split equivalence — byte-identical split vs unsplit (Dart↔Dart then Dart↔C#). |
| SC-002 | Cross-runtime link parity — one writer→reader bind reconstructed equal Dart↔C#. |
| SC-003 | Per-transport bind reactivation (T4) — each shipped leaf reactivates a suspended reader once, on ≥1 platform. |
| SC-004 | Guard three-valued conformance — succeed / suspend-then-reactivate-once / fail. |
| SC-005 | `atom/1` analyzer↔runner consistency. |
| SC-006 | SRSW preserved under comparison guards (ground-implying relaxation positive; un-guarded negative). |
| SC-007 | Adversarial / security corpus parity (Dart vs C#); plain inter-host refused by default. |
| SC-008 | Idempotent redelivery is a verified no-op (the live duplicate-crash closed). |
| SC-009 | Suspend-not-fail across the cut (incl. nested-in-compound + imported-reader). |
| SC-010 | Fault liveness — peer-kill ⇒ `tempFail`→`permFail` in bounded time, data goal not failed. |
| SC-011 | Split-brain defense — epoch/fence, loser `permFail`, no silent overwrite. |
| SC-012 | Reorder / loss recovery — sublayer-on reconstructs in-order; sublayer-off detects corruption. |
| SC-013 | Backpressure bound — producer suspends, queue bounded, no head-of-line block across links. |
| SC-014 | Distributed GC — per-link resources return to baseline after N permFails. |
| SC-015 | GEPA round-trip fidelity per primitive — Agent-seams-only (never OpenAI/litellm). |
| SC-016 | Stream-reroute fidelity — stdin/stdout/stderr, distinct, capability-gated, sanitized. |
| SC-017 | Baseline regression gate — `run_all_tests.sh` green before/after every core change. |

---

# 1. Substrate row group — the shared harness (T-01..T-13)

The harness is the **substrate** every transport integration test below targets. Its 13
spec-level catalogue tests (T-01..T-13, [`integration-harness-design.md`](integration-harness-design.md)
§8) plus its two standing gates (Section-R skip-until-implemented for SC-017; the
GEPA-metric hook for SC-015) are the **only** rows that cover several SCs no individual
transport leaf claims (SC-005, SC-011, SC-015, SC-016 — see §4 Gaps).

| Harness test (substrate) | 001 | 002 | 003 | 004 | 005 | 006 | 007 | 008 | 009 | 010 | 011 | 012 | 013 | 014 | 015 | 016 | 017 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **T-01** Headline split (Dart↔Dart then Dart↔C#) | ● | ● | ○ | | | | | | | | | | | | | | |
| **T-02** Suspend-not-fail / reactivate-once across the cut | ○ | | | | | | | | ● | | | | | | | | |
| **T-03** Per-transport bind reactivation (T4) | | | ● | | | | | | | | | | | | | | |
| **T-04** TLS-by-default (inter-host plain refused) | ○ | | | | | | ● | | | | | | | | | | |
| **T-05** Idempotent redelivery no-op | | | | | | | | ● | | | | | | | | | |
| **T-06** Reorder / loss recovery | | | | | | | | ○ | | | | ● | | | | | |
| **T-07** Fault liveness on peer-kill | | | | | | | | | ○ | ● | | | | | | | |
| **T-08** Split-brain defense | | | | | | | | | | | ● | | | | | | |
| **T-09** Backpressure bound | | | | | | | | | | | | | ● | | | | |
| **T-10** Distributed GC | | | | | | | | | | | | | | ● | | | |
| **T-11** Stream reroute fidelity | | | | | | | | | | | | | | | | ● | |
| **T-12** Guard three-valued + SRSW under guards (across the cut) | | | | ● | ● | ● | | | ● | | | | | | | | |
| **T-13** Adversarial-corpus parity (both REPLs) | | | | | | | ● | | | | | | | | | | |
| **Section R** skip-until-implemented + full-suite-green gate | | | | | | | | | | | | | | | | | ● |
| **GEPA** round-trip fidelity hook (Agent seams only) | | | | | | | | | | | | | | | ● | | |

Notes: T-12 is the harness's across-the-cut mirror of the guard facet
([`../contracts/guards.md`](../contracts/guards.md) §1–§4); the in-process Section-A/B/C
guard tests are the local mirror. SC-005 (`atom/1`) and SC-006 (SRSW under guards) are
**owned by the guard facet**, surfaced through T-12 here.

---

# 2. Per-transport UNIT test groups (single-REPL Section A/B/C)

Unit tests are single-instance, no transport; they pin the GLP-surface contracts each leaf
sits on. The recurring coverage is **SC-006** (SRSW relaxation positive in Section B,
negative in Section C — the declined-guard rejects also land here as the FR-036 slice of
SC-006/SC-004), **SC-009** (suspend-not-fail on an unbound/compound/imported reader, in
Section A), **SC-013** (the bounded-pipe/credit-window suspension *surface*, seeded in
Section A but proven at integration level), and the new-guard three-valued **SC-004** where
a leaf exercises the `@<` family. Per-leaf rows are collapsed to the Section level (the
individual A/B/C IDs are in each tutorial); the SC tags below are the union the section's
tests declare.

| Unit group (file) | 001 | 002 | 003 | 004 | 005 | 006 | 007 | 008 | 009 | 010 | 011 | 012 | 013 | 014 | 015 | 016 | 017 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **file-loopback** `unit-A-01..07` (runtime) | | | | | | ○ | | | ● | | | | ○ | | | | |
| **file-loopback** `unit-B-01..03` (pos type-check) | | | | ○ | | ● | | | | | | | | | | | |
| **file-loopback** `unit-C-01..03` (neg type-check) | | | | ○ | | ● | | | | | | | | | | | |
| **websocket** `A-WS-1..7` (runtime) | | | | | | ○ | | | ● | ○ | | | ○ | | | | |
| **websocket** `B-WS-1..3` (pos type-check) | | | | | | ● | | | | | | | | | | | |
| **websocket** `C-WS-1..3` (neg type-check) | | | | ○ | | ● | | | | | | | | | | | |
| **https** `A-https-01..06` (runtime) | ○ | | | | | ○ | | | ● | ○ | | | ○ | | | | |
| **https** `B-https-01..03` (pos type-check) | | | | | | ● | | | | | | | | | | | |
| **https** `C-https-01..03` (neg type-check) | | | | ○ | | ● | | | | | | | | | | | |
| **mqtt** `A-MQTT-1..6` (runtime) | | | | ● | | ○ | | | ● | ○ | | | ○ | | | | |
| **mqtt** `B-MQTT-1..3` (pos type-check) | | | | ○ | | ● | | | | | | | | | | | |
| **mqtt** `C-MQTT-1..3` (neg type-check) | | | | ○ | | ● | | | | | | | | | | | |
| **coap** `A1..A11` (runtime) | ○ | | | | | ○ | | ○ | ● | ○ | | | ○ | | | | |
| **coap** `B1..B3` (pos type-check) | | | | ○ | | ● | | | | | | | | | | | |
| **coap** `C1..C3` (neg type-check) | | | | ○ | | ● | | | | | | | | | | | |
| **ble-l2cap** `A-BLE-1..10` (runtime) | ○ | | | | | ○ | | | ● | | | | ● | | | | |
| **ble-l2cap** `B-BLE-1..3` (pos type-check) | | | | | | ● | | | | | | | | | | | |
| **ble-l2cap** `C-BLE-1..3` (neg type-check) | | | | | | ● | | | | | | | | | | | |

Notes:
- **mqtt A-MQTT-5** and **B-MQTT-2 / C-MQTT-2** exercise the **`@<`** peer-id ordering guard,
  so the mqtt unit group is the one that directly carries **SC-004** for the new guard family
  at the leaf level (the guard facet owns the full SC-004/SC-005 conformance; the other
  leaves reference it rather than re-specify it).
- Section-A "runtime" rows tag **SC-013** as ○: they assert the bounded-pipe/credit-window
  *suspension surface* structurally (e.g. file `unit-A-05`, ws `A-WS-5`, https `A-https-03`,
  mqtt `A-MQTT-4`, coap `A8`, ble `A-BLE-2/3`); the full backpressure bound is the
  integration SC-013 row in §3.
- The Section-A "runtime" SC-010 ○ marks are the monitor-lattice term reads (faults are
  ordinary data); the full fault-liveness SC-010 is the integration peer-kill row in §3.
- **SC-005** is never claimed by a leaf unit group — it is the guard facet's `atom/1` item,
  carried by the harness T-12 row. See §4.

---

# 3. Per-transport INTEGRATION test rows (cross-instance over the harness)

| Integration test (file) | 001 | 002 | 003 | 004 | 005 | 006 | 007 | 008 | 009 | 010 | 011 | 012 | 013 | 014 | 015 | 016 | 017 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **unit-INT-01** loopback split Dart↔Dart | ● | | ○ | | | | | | | | | | | | | | |
| **unit-INT-02** loopback split Dart↔C# (release gate) | ● | ● | ○ | | | | | | | | | | | | | | |
| **unit-INT-03** `file` append/replay split | ● | | ○ | | | | | | | | | | | | | | |
| **unit-INT-04** loopback Duplicate ⇒ dedup no-op | | | | | | | | ● | | | | | | | | | |
| **unit-INT-05** loopback Reorder+Drop recovery | | | ○ | | | | | | | | | ● | | | | | |
| **unit-INT-06** request/accept path B (loopback) | ● | | ○ | | | | | | | | | | | | | | |
| **unit-INT-07** graceful `[]` vs abrupt close (`file`) | | | | | | | | | | ● | | | | ○ | | | |
| **unit-INT-08** TLS-by-default guard wired | | | | | | | ● | | | | | | | | | | |
| **IT-WS-1** `wss` split (Dart↔Dart then Dart↔C#) | ● | ● | ○ | | | | | | | | | | | | | | |
| **IT-WS-2** `wss` per-transport bind reactivation | | | ● | | | | | | | | | | | | | | |
| **IT-WS-3** fault liveness on socket drop | | | | | | | | | ○ | ● | | | | | | | |
| **IT-WS-4** backpressure bound | | | | | | | | | | | | | ● | | | | |
| **IT-WS-5** graceful vs abrupt close | | | ○ | | | | | | | | | | | ○ | | | |
| **IT-WS-6** TLS-by-default refusal | | | | | | | ● | | | | | | | | | | |
| **IT-WS-7** reorder/loss recovery (hermetic loopback) | | | | | | | | ○ | | | | ● | | | | | |
| **I-https-01** `https` split Dart↔Dart then Dart↔C# | ● | ● | ○ | | | | | | | | | | | | | | |
| **I-https-02** suspend-not-fail / reactivate-once | ○ | | | | | | | | ● | | | | | | | | |
| **I-https-03** per-transport bind reactivation (T4) | | | ● | | | | | | | | | | | | | | |
| **I-https-04** adversarial corpus parity + mTLS origin auth | | | | | | | ● | | | | | | | | | | |
| **I-https-05** backpressure over HTTP/2 flow control | | | | | | | | | | | | | ● | | | | |
| **I-https-06** graceful vs abrupt close | ○ | | | | | | | | | ● | | | | | | | |
| **I-https-07** idempotent redelivery no-op (hermetic) | | | | | | | | ● | | | | | | | | | |
| **I-https-08** reorder/loss recovery (hermetic) | | | | | | | | | | | | ● | | | | | |
| **IT-MQTT-1** `mqtt` split Dart↔Dart then Dart↔C# | ● | ● | | | | | | | | | | | | | | | |
| **IT-MQTT-2** `mqtts` per-transport bind reactivation | | | ● | | | | | | | | | | | | | | |
| **IT-MQTT-3** dedup over QoS-1 duplicates (no-op) | | | | | | | | ● | | | | | | | | | |
| **IT-MQTT-4** reorder recovery | | | | | | | | | | | | ● | | | | | |
| **IT-MQTT-5** graceful close `[]` ⇒ `closed(eos)` | | | | | | | | | | | | | | ○ | | | |
| **IT-MQTT-6** abrupt/partition ⇒ `tempFail`→`permFail` | | | | | | | | | ○ | ● | | | | ○ | | | |
| **IT-MQTT-7** distributed GC to baseline | | | | | | | | | | | | | | ● | | | |
| **IT-COAP-1** `coap` split Dart↔Dart then Dart↔C# | ● | ● | | | | | | | | | | | | | | | |
| **IT-COAP-2** real-CoAP per-transport bind reactivation | | ○ | ● | | | | | ○ | | | | | | | | | |
| **IT-COAP-3** reorder/loss over UDP (hermetic) | | | | | | | | ○ | | | | ● | | | | | |
| **IT-COAP-4** backpressure to a slow constrained peer | | | | | | | | | | | | | ● | | | | |
| **IT-COAP-5** graceful vs abrupt close | | | | | | | | | | ● | | | | ● | | | |
| **IT-COAP-6** inter-host DTLS-by-default refusal | | | | | | | ● | | | | | | | | | | |
| **I-BLE-1** loopback-CoC split Dart↔Dart | ● | | | | | | | | | | | | | | | | |
| **I-BLE-2** loopback-CoC split Dart↔C# (release gate) | ● | ● | | | | | | | | | | | | | | | |
| **I-BLE-3** real Android CoC per-transport bind reactivation | | ● | ● | | | | | | | | | | | | | | |
| **I-BLE-4** backpressure bound (credit window) | | | | | | | | | | | | | ● | | | | |
| **I-BLE-5** reorder/loss recovery (hermetic) | | | | | | | | | | | | ● | | | | | |
| **I-BLE-6** graceful close = stream-end `[]` | ○ | | ○ | | | | | | | | | | | | | | |
| **I-BLE-7** abrupt close + permFail / fault liveness | | | ○ | | | | | | ○ | ● | | | | | | | |
| **I-BLE-8** inter-device TLS-by-default refusal | | | | | | | ● | | | | | | | | | | |

Notes:
- The **Dart↔C# arm** of each leaf's headline split test (unit-INT-02, IT-WS-1, I-https-01,
  IT-MQTT-1, IT-COAP-1, I-BLE-2) is the **cross-runtime parity release gate** (FR-062): it
  carries both SC-001 (Dart↔C#) and SC-002. This is the single most-replicated coverage in
  the matrix — by design, every leaf must clear it.
- **Wire-fault** tests (Duplicate/Reorder/Drop ⇒ SC-008/SC-012) run **hermetically on the
  deterministic loopback** transport, even in a "ws"/"coap"/"https"/"ble" tutorial
  (IT-WS-7, I-https-07/08, IT-COAP-3, I-BLE-5, unit-INT-04/05). This is the transport-author
  seam rule: the shared reliability sublayer is validated once on loopback; each real leaf
  only proves bind-reactivation (SC-003) + close. So a leaf "covering" SC-008/SC-012 does so
  through its loopback-backed sibling test, not a live-wire fault.
- **SC-011 (split-brain)** is claimed by **no transport leaf** — only by the harness T-08
  substrate row. **SC-014 (distributed GC)** is claimed at leaf level only by **IT-MQTT-7**
  (●) with several close tests marking it ○; the harness T-10 is the primary. **SC-016
  (stream reroute)** and **SC-015 (GEPA)** are claimed by no leaf at all. See §4.

---

# 4. Coverage summary and GAPS

## 4.1 SC coverage roll-up (across substrate + all leaves)

| SC | Covered by | Status |
|---|---|---|
| SC-001 | Harness T-01/T-02; **every leaf's headline split** (unit-INT-01/02/03/06, IT-WS-1, I-https-01/02/06, IT-MQTT-1, IT-COAP-1, I-BLE-1/2/6) | **Strong** — every transport. |
| SC-002 | Harness T-01; every leaf's Dart↔C# split arm (unit-INT-02, IT-WS-1, I-https-01, IT-MQTT-1, IT-COAP-1/2, I-BLE-2/3) | **Strong** — the release gate, every transport. |
| SC-003 | Harness T-03; **every leaf** (unit-INT-01..03/05/06 ○, IT-WS-2, I-https-03, IT-MQTT-2, IT-COAP-2, I-BLE-3 ●) | **Strong** — the "leaf is shipped" gate, one platform each. |
| SC-004 | Harness T-12; mqtt `A-MQTT-5`/`B-MQTT-2`/`C-MQTT-2` (the `@<` family); guard facet (`../contracts/guards.md`) | **Adequate** — owned by the guard facet; mqtt is the leaf carrier. |
| SC-005 | **Harness T-12 only** (the guard facet's `atom/1` item) | **THIN — see Gap G1.** |
| SC-006 | Harness T-12; **every leaf's Section-B/C** unit tests | **Strong** — SRSW-under-guards positive+negative on every leaf. |
| SC-007 | Harness T-04/T-13; unit-INT-08, IT-WS-6, I-https-04, IT-COAP-6, I-BLE-8 (TLS-default + corpus parity) | **Strong** — but corpus *parity* depth varies (see Gap G4). |
| SC-008 | Harness T-05; unit-INT-04, IT-WS-7, I-https-07, IT-MQTT-3, IT-COAP-2/3 ○ (loopback-backed) | **Strong** — hermetic on loopback; MQTT QoS-1 is the showcase. |
| SC-009 | Harness T-02/T-12; **every leaf's Section-A** + I-https-02 | **Strong** — suspend-not-fail across the cut. |
| SC-010 | Harness T-07; IT-WS-3, I-https-06, IT-MQTT-6, IT-COAP-5, I-BLE-7, unit-INT-07 | **Strong** — fault liveness on most leaves. |
| SC-011 | **Harness T-08 only** | **GAP — see Gap G2.** |
| SC-012 | Harness T-06; unit-INT-05, IT-WS-7, I-https-08, IT-MQTT-4, IT-COAP-3, I-BLE-5 (loopback-backed) | **Strong** — hermetic on loopback, every leaf. |
| SC-013 | Harness T-09; IT-WS-4, I-https-05, IT-MQTT-4(unit) /A-rows, IT-COAP-4, I-BLE-4 | **Strong** — backpressure on most leaves. |
| SC-014 | Harness T-10; IT-MQTT-7 (●); IT-COAP-5, IT-WS-5, unit-INT-07, IT-MQTT-5/6 (○) | **Adequate — but only MQTT has a dedicated leaf-level GC test; see Gap G3.** |
| SC-015 | **Harness GEPA-hook row only** | **GAP — see Gap G5.** |
| SC-016 | **Harness T-11 only** | **GAP — see Gap G6.** |
| SC-017 | Harness Section-R gate row; **every leaf's Regression section** ties its set to `run_all_tests.sh` | **Strong** — standing gate, every leaf. |

## 4.2 Declared GAPS (SCs no transport leaf covers, and why)

- **G1 — SC-005 (`atom/1` analyzer↔runner consistency): no transport leaf, harness T-12 only.**
  This is a pure language/guard-facet item (`atom/1` must behave identically at compile time
  and runtime, FR-033). It is correctly owned by [`../contracts/guards.md`](../contracts/guards.md)
  and surfaced through the harness's across-the-cut guard row (T-12). **Why it's a gap at the
  leaf level:** no transport exercises `atom/1` specifically. **Disposition:** acceptable —
  this belongs to the guard facet, not a transport; the matrix flags it so the guard facet's
  Section-A/B/C `atom/1` tests are not forgotten when leaves are the focus.

- **G2 — SC-011 (split-brain double-bind defense): harness T-08 only, no transport leaf.**
  Epoch/fencing under two competing writers for one global name (FR-047) is a
  **reliability-sublayer** property, not a per-transport one — it is validated hermetically on
  loopback where a `Partition`-then-heal can be injected deterministically (harness T-08). **Why
  no leaf:** the leaves' wire-fault coverage is delegated to loopback by the transport-author
  seam rule, and split-brain is the sharpest such fault. **Disposition:** acceptable by design,
  **but** the matrix recommends at least one *named* leaf integration test (e.g. an `mqtt`
  reconnect-with-stale-writer case, the natural real-world split-brain) be added so SC-011 has a
  transport-flavored witness, not only the abstract loopback one. Tracked against DESIGN-DOSSIER
  OQ (epoch/fencing) and the failure-model facet.

- **G3 — SC-014 (distributed GC to baseline): only IT-MQTT-7 is a dedicated leaf test.**
  Every other leaf marks SC-014 only ○ (via a close/teardown test that "supports" GC) and relies
  on the harness `Stop`/`SnapshotResources` substrate (T-10). **Why:** GC-to-baseline is a
  resource-census property of the shared registry/heap, transport-independent. **Disposition:**
  adequate (the substrate T-10 is the real gate; MQTT-7 is the leaf witness), but the matrix notes
  the GC census probe (`SnapshotResources`, harness OQ-H5) must exist for ANY of these to run.

- **G4 — SC-007 corpus-parity depth is uneven across leaves.** Only `https` (I-https-04) and the
  harness (T-13) run the **full** adversarial corpus verdict-by-verdict on both REPLs; the other
  leaves cover only the **TLS-by-default refusal** slice of SC-007 (IT-WS-6, IT-COAP-6, I-BLE-8,
  unit-INT-08). **Why:** the corpus is transport-independent (it targets the deserializer /
  reliability sublayer), so running it once on the parity rig is sufficient for the *security*
  verdicts; each leaf only needs the *policy* slice (plain inter-host refused). **Disposition:**
  acceptable — the full corpus is the harness's job (FR-031), not each leaf's; flagged so this is a
  conscious decision, not an omission.

- **G5 — SC-015 (GEPA round-trip fidelity per primitive): harness GEPA-hook row only; no leaf
  spec.** The experiment→verify→refine fidelity loop per sender/receiver primitive (FR-065/066)
  runs **exclusively in the Claude harness via Agent seams** — never OpenAI/litellm/`OPENAI_API_KEY`.
  **Why no leaf:** the tutorials specify *correctness* tests (round-trip equivalence), which IS the
  GEPA success metric, but none wires the GEPA *optimization loop* explicitly. **Disposition:**
  flagged gap — the GEPA facet must define, per primitive, the metric + the Agent-seam loop; the
  per-leaf round-trip equivalence tests (SC-001/002/003) supply the metric, the loop is deferred to
  the GEPA facet (harness coverage-map note; SC-015 design detail deferred).

- **G6 — SC-016 (stream-reroute fidelity): harness T-11 only; no transport leaf.** Rerouting a
  REPL's stdin/stdout/stderr to a remote REPL under an explicit capability with sanitization
  (US3, FR-030) is a **capability layered on a working bilateral link** (P1/P2), independent of
  which transport carries it. **Why no leaf:** it is correctly a single substrate capability, not
  per-protocol. **Disposition:** acceptable — but the matrix recommends the reroute test (T-11) be
  exercised over at least one *real* leaf (e.g. `wss`) in addition to loopback, since control-
  sequence sanitization over a real socket is the realistic threat surface; this is currently
  loopback-only.

## 4.3 Reading the gaps together

The five thin/gap SCs (G1 SC-005, G2 SC-011, G3 SC-014, G5 SC-015, G6 SC-016) are all
**substrate-level or facet-level** properties — guard semantics, sublayer reliability,
resource GC, the GEPA loop, and stdio rerouting — that are deliberately **NOT** per-transport.
The transport leaves correctly concentrate on what IS per-transport: split equivalence
(SC-001/002), bind reactivation (SC-003), close fidelity, fault liveness (SC-010),
backpressure (SC-013), TLS-default (SC-007 policy slice), with wire-fault correctness
(SC-008/012) delegated hermetically to loopback. The matrix's recommendations are to add (a)
one transport-flavored SC-011 witness (mqtt reconnect/stale-writer), and (b) one real-leaf
SC-016 reroute run — neither blocking, both improving realism. Everything else is covered.

---

# 5. Dependency note — nothing runs until the primitives land

Every row in this matrix is **SPEC-LEVEL**. The entire test plan is exercisable only once:
(1) the **PROPOSED base link primitives** are approved under language authority and
implemented; (2) the **reliability sublayer** (per-link seq/dedup, FIFO+reorder buffer, the
FR-021 duplicate-delivery fix that today **crashes** the agent, serializer
version/CRC/fragmentation, epoch/fence, distributed GC) is built below the seam; (3) the
named **transport leaf** exists. Until then, the harness's Section **R** of
`test/run_all_tests.sh` is a documented `SKIP` so the baseline stays green (SC-017 / FR-067),
and each leaf's tests flip from skip to run as its primitives + leaf land. The Dart↔C#
parity arms (SC-001 Dart↔C#, SC-002) are the **release gate** (FR-062) and require both REPLs
built; they are not on the default fast path.
