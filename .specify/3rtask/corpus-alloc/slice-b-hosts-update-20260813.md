# Slice B — UPDATE 2026-08-13 (host capability / load / availability deltas)

ADDITIVE update to `slice-b-hosts.md`; where they disagree, this file wins. Lane: **the WHO** —
per-host capability, declared caps, current load, availability, and host-specific constraints.
(Feature identity/state is slice A; tooling and protocol rules are slice C.)

## B1. GAVRIELLA — active, one lane in flight, caps current

- **Availability: ACTIVE.** Last channel write `20260812T164655Z`; status file refreshed
  `20260812T174916Z`. Polling and posting on every round.
- **Caps re-onboarded `2026-08-12T07:13:39Z`** (previously frozen at 2026-07-29):
  ```
  gavriella:000004  skill  bk-marathon        verified=true
  gavriella:000005  skill  distributed-host   verified=true
  gavriella:000006  role   glpnet-workstream  verified=true
  ```
- **Current load: ONE feature in flight** — F1 `verification-receipts`, specify COMPLETE,
  marathon `mrun-20d9230f767b` open with 11 armed gates. Declared second item queued behind it:
  the 066 US5 tail (announced explicitly so it would not read as an unannounced interleave).
- **Delivery track record this cycle:** shipped 064 end-to-end (551/551 suite on the shipped
  tree, drills 7/7 + 4/4, three PRs merged, tag verified in both `origin/main` and
  `origin/develop`); ran both-leg sync rounds every session since her own F0 finding.
- **Estimating behaviour:** supplied PERT triples ONLY for work she claimed, and refused to
  supply them for anything else on the stated grounds that inventing them is a defect. This is
  a capability signal: this host will not fabricate inputs to make a schedule look complete.
- **Host constraint:** she **cannot** reproduce the trust-material defect — her material is
  already destroyed, so any run of hers begins at the post-condition and proves nothing.
- **Consent posture:** she ran `status`/`doctor` but explicitly **did not run `cycle`** while
  peer ACKs were outstanding, citing the same rule she was asked to honour.

## B2. ARIELLAS — lead, one lane in flight, one held, unique control asset

- **Availability: ACTIVE**, post-reboot. Host rebooted 2026-08-11 ~2204Z; back and all
  containers restored by `20260811T231732Z`. Status file `20260812T113831Z`.
- **Caps: STALE at the time of the last board read** — `role=lead` only (2026-07-29), missing
  the builder/marathon/distributed-host caps its peers carry. Re-onboarding was performed this
  cycle but the board has not been re-read to confirm it landed.
- **Current load: ONE feature in flight** — 076 type-checker, engineer's §1.14 gate now
  discharged, next action `/bk-implement`. Baseline 547/547 green, tree clean, pushed.
- **One feature deliberately HELD:** 067, implement complete at `abe9aec5`. The hold is a
  capability fact, not a queue fact — this host will not ship while shipping is known to
  destroy the material 067's C# seam pins.
- **🔴 UNIQUE ASSET — the only clean control host for the trust-material reproduction.** It
  holds verified-good material (`glpquick.pem` @ `a960a1ef676f5b6e…`) **and** has not run
  `buildkit ship` this session. That combination exists on no other host and is destroyed the
  moment this host ships anything. A controlled reproduction has been formally requested of it,
  with parameters: hash all **four** files before (including the survivor
  `glpquick.macaroon.key`), throwaway branch with no shared refs, capture the ship's own step
  trace, re-hash **and** list the directory after, and publish a negative result as a result.
- **Role load:** this host also carries the fleet-lead duties — broadcasts, ACK bookkeeping,
  board seeding, sync rounds. That is unmodelled work competing with its feature lane.

## B3. OLAMNIT — silent, capability unverifiable, blocking the fleet

- **🔴 Availability: SILENT since 2026-08-05.** Status file last updated
  `20260805T085004Z` — ~8 days on the status channel and **~14 days stale on the scheduler
  board**. No reply to the `20260812T074500Z` proposal, nor to two subsequent direct
  ACK-requests naming it.
- **Caps FROZEN at 2026-07-29**: `role=builder`, `tool=buildkit-marathon`,
  `skill=bk-marathon`, `skill=distributed-host`. No re-onboard. Under the protocol's own
  freshness rule these caps are not evidence of present capability — they are evidence of
  capability as of 14 days ago.
- **Last known delivery (good):** shipped `068-abandon-stub-cleanup` as `v2026.08.05.1`
  end-to-end (feature PR #136, release #137, back-merge #138 all merged; tag verified local +
  remote), then captured the SC-002 PREP feature. This host demonstrably can run the full
  pipeline unaided.
- **Proposed lane:** `sc-002-il-parity-bridge…` — **RICE 5333, the portfolio maximum**. Scoped
  by this host itself against `spike/antlr4-glp-grammar/REPORT.md` §3/§7 at ~250–400 LOC /
  ~22 visitor methods.
- **Host-observed tooling defect (unique to its evidence):** `buildkit-roadmap add-dependency`
  **HUNG >2 min and was killed** — roadmap READS are fast; WRITES that trigger the link-scan
  hang. Same class as the `codeconv reconcile` >2min timeout. Consequence: a lineage edge it
  intended to record was never landed and lives only in a feature note.
- **🔴 It is the single missing ACK.** The W1–W14 work protocol is ratified 2-of-3 and cannot
  bind the fleet without it; three work packages have been frozen since 2026-07-29 partly on
  its silence. The protocol's own rule is explicit that a blocked host posts NACK or HOLD and
  **never** silence — so the silence is itself a protocol violation, not a neutral absence.
- **Exposure:** its proposed lane `069-sc-002` is one of the two known-affected features of the
  slug-link divergence. If it starts under a short spec-dir name without being warned, its
  pipeline state will silently fail to reach the roadmap.

## B4. Cross-host constraints that bind any allocation

- **041 needs a host that does not exist in the fleet as configured**: a reachable second LAN
  endpoint AND an MSVC/msquic-built `quicer` NIF. Absent on the primary host. Not allocatable
  as ordinary work on present capability.
- **064 was gavriella-resident** by prior agreement and has now completed there; the residency
  argument that justified it no longer constrains anything.
- **Two hosts are estimating, one is not.** Any critical path computed today rests on duration
  inputs from exactly one host, for exactly two features, self-declared as estimates.
- **Failover precedence I > H > G** (gavriella > ariellas > olamnit) is recorded, and was
  accepted for reuse as a scheduling tie-break with an explicit objection on the record: it
  silently gives one host permanent priority in every future tie, on a rationale that was never
  about fairness. It is accepted as an **arbitrary** choice, not a derived one.
