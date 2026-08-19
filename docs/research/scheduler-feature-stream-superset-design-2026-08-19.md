# Durable superset fix — scheduler feature stream

**Date:** 2026-08-19 · **Host:** gavriella · **Marathon:** `mrun-20d9230f767b` step 4/13
**Evidence:** 3rtask run `20260819T162016Z-6e73`, verdict `budget_stop`, cycle 1
**Roadmap:** feature #13 `coordination-feature-stream-durable-superset-fix` (promoted, WSJF 4.25 / RICE 2625)

> Every requirement below cites the adjudicated claim(s) it answers. Nothing here is
> asserted without an anchor. Where the Critic escalated rather than confirmed, the
> requirement is marked **[ESCALATED]** and is *not* actionable until the engineer rules.

---

## 1. What the evidence licenses

Three blind Builders on file-disjoint corpora (substrate / channel / toolchain), independence
audit exercised post-output with **0 violations**, codex Critic adjudicating 72/72
(66 CONFIRM · 5 ESCALATE · 1 REFUTE) and deriving 5 DEFECTs + 5 CAUSAL_LINKs.

**The stream did not fail. It was never opened.** The M9 counterfactual is
**numerator 0, denominator 32** on root `D:/coop/glpnet/sched` — reached independently by
two Builders. Removing the capacity gate produces **no** gavriella allocation.

### Confirmed chain

| # | Finding | Evidence |
|---|---|---|
| C1 | `dispatch` admits only `state=='ready'`; **only 4 of 32 WPs ever transitioned**, all authored by ariellas | CAUSAL_LINK/high |
| C2 | Readiness has **no automatic writer** — `ingest --ready` and `cycle --refill` both off by default (R-B1: *"no cycle path writes it"*) | MECHANISM, S3 |
| C3 | Effort map S/M/L = 28800/144000/288000 s vs per-node capacity ≤ 86400 s → **96.2% of WPs exceed every node's capacity** | CAUSAL_LINK/high |
| C4 | Capacity filtering **does not explain gavriella's zero**: same sufficient horizon as ariellas on an exercised 28800 s WP, still not selected | CAUSAL_LINK/high |
| C5 | Supply is bursty: promotions ended ~117 h before window close; 26 WPs minted in **one 9-second burst**; 58 of 494 hourly cycles exist | CAUSAL_LINK/high |

### Confirmed defects

| # | Defect |
|---|---|
| D1 | Skill contract states verbatim *"There is no `distribute` and no `allocate` verb"*; live `2026.8.19.1` exposes `allocate` + 6 further omitted commands |
| D2 | `--help` advertises a sched-root default; `root --json` returns `null, exists=false, configured=false` — **at exit 0** |
| D3 | Proposal view and committed op assign the same WP, same effort, to **different actors** |
| D4 | A capacity rejection is **not enforced at commit** — a WP blocked on all nodes was committed, producing `cap_violations=1`, `remaining=-57600.0` |
| D5 | Mint records addressed to pseudo-actor `unassigned` are counted as **real allocated load** against a zero-capacity engineer |

### Refuted / escalated — do not build on these

- **REFUTED:** "the 26 batch allocate ops all carry `e_t_s: 0.0`". They total **5,212,800 s**.
- **[ESCALATED]** "availability was never it" as a categorical claim. The Critic: *"24h was
  neither sufficient nor universally binding. A controlled glpnet counterfactual is absent."*
  C4 (capacity is not *sufficient* to explain the zero) stands; the categorical does not.
- **[ESCALATED]** capability-name normalisation at the fail-closed R7 gate — glpnet candidates
  never exercised the capability gate (`required_capability` null 22/22, `edges_confirmed: 0`),
  so no glpnet defect is derivable.

---

## 2. The superset requirements

Ordered so each is independently shippable and each closes a *measured* gap.

### SR-1 — An explicit readiness writer *(closes C1+C2 — the dominant constraint)*
Readiness must have a named, auditable writer. Today the only path is an operator hand-running
a verb that is off by default, which is why 28 of 32 WPs were invisible to the allocator.
Either the refill cycle writes readiness under a declared policy, or the absence of a readiness
writer is itself reported loudly per run. **A board with 0 ready rows must refuse to report
"no candidate" and must instead report "no readiness writer configured".**

### SR-2 — Calibrated effort against a real denominator *(closes C3)*
S/M/L must be derived from measured actuals against the per-host calendar, not fixed seconds.
While 96.2% of WPs exceed every node's capacity, the allocator is arithmetically incapable of
placing work regardless of availability. Uncalibrated nodes must be flagged, never defaulted.

### SR-3 — Canonical board binding *(closes D2, and the 15-roots problem)*
Every invocation binds to one canonically resolved board identity. The same physical board is
currently reachable as `D:\coop\glpnet\sched`, `I:/coop/glpnet/sched` and
`\192.168.0.108\GAVRI_D\coop\glpnet\sched`; `olamnit/sched`, `ospark`, `buildkit`, `qhstate`,
`crucible`, `yngenios-research` are **different** boards. Join on the resolved path or the
merge manufactures false conflicts. `root` reporting `null` must not exit 0.

### SR-4 — One authoritative assignment surface *(closes D3+D4)*
The derived proposal view and the committed op log must not be independently authoritative.
Commit only allocator-approved assignments; a proposal blocked on every node must not be
force-committable. **[Partly ESCALATED —** whether a blocked proposal may be force-committed
is engineer escalation #2 and is not designed here.**]**

### SR-5 — Supply ingestion is continuous, not a burst *(closes C5)*
Tagged promoted supply ingests on a cadence. A 9-second burst 20.8 days into a 21-day window
is not a stream, and no downstream gate can compensate for it.

### SR-6 — Load accounting distinguishes minting from assignment *(closes D5)*
`unassigned` is a sentinel, not an engineer. It must never consume capacity as a real node.

### SR-7 — Contract/CLI conformance is machine-checked *(closes D1)*
The shipped `/bk-guards` template-contract check already exists for exactly this class
(documented CLI steps vs live-introspected contract). Wire the scheduler skill into it so a
contract claiming a verb does not exist while the binary exposes it fails a gate.

### SR-8 — Durable queued transport *(channel finding)*
Delivery/ACK state must not depend on gavriella's SMB share being mounted. The 5-minute ACK
SLA is structurally unsatisfiable on a file-drop channel with no delivery signal and no daemon
(measured latency ~9 min); restate it in hours or give ACK a real transport.

---

## 3. Cross-repo / cross-host rollout

The fix is **engine-side, not per-repo**. Every requirement above lives in the buildkit
scheduler engine, so:

1. **Implement once in buildkit.** No per-repo edit; every repo on this host inherits it
   through the deploy home.
2. **Pin, then converge.** `buildkit-deploy latest <repo>` advances a target to the newest
   installed version. Note the standing hazard: this host currently runs
   `BUILDKIT_ENGINE_OVERRIDE=ambient`, which overrides the deploy-home pin
   (`2026.08.18.4`) with `2026.8.19.1` — so *which subcommands exist is a function of the
   checked-out branch, not release state*. **Rollout must remove the override, not build on it.**
3. **Other hosts.** Same install path. Nothing in SR-1..SR-8 is host-specific; the only
   per-host input is the calendar (SR-2) and the resolved board (SR-3), both already
   per-host by design.

### Non-coverage — stated, per M0
- Does **not** cover the R7 capability-normalisation ruling (escalated; glpnet never exercised the gate).
- Does **not** cover the force-commit authority question (escalated).
- Does **not** establish that availability was never a factor on *any* board — only that
  capacity is insufficient to explain gavriella's zero on *this* board.
- Cycle 2 of the 3rtask run was not executed (budget: 307k remaining vs ~500k for a
  3-builder cycle). `min_cycles = 2` is UNMET and the run closed at `budget_stop`.
  The 6 open escalations are engineer-owned by the Critic's own ruling and were not
  closable by further blind reading.
