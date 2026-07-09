# Ship-gate audit (T031 / SC-010 — Clarifications hard gate, Option B)

**Date**: 2026-07-09 · **Auditor session**: gavri (per Gabi's /bk-implement directive completing
US1 + US3 records + close-out on this host) · **Branch**: `049a-gavri-us2-us3` (PR to the
canonical feature branch)

## The hard gate: ALL FOUR user stories pass their acceptance scenarios

| Story | SC | Verdict | Evidence |
|---|---|---|---|
| US1 GLP policy-guard (staged (a)→(b)) | SC-001, SC-002, SC-003, SC-009 | **PASS** | `guard/form-a.md`, `guard/form-b.md`, `guard/audit.md` |
| US2 Profile C in-process BEAM QUIC | SC-005 | **PASS** | `gavri/us2-verdict.md`, `gavri/10-profile-c.md` |
| US3 Two-host LAN acceptance | SC-006 | **PASS** | `two-host/us3-verdict.md`, `two-host/run.md`, `gavri/20-two-host.md` |
| US4 Marathon durability | SC-007 | **PASS** | `marathon/run.md`, `marathon/kill-resume.md`, `marathon/redrive.md` |

Supporting: SC-004 baselines PASS (`final-baselines.md`); FR-015 carried fixes T025–T028 done
(regressions added); US5/FR-017..019 bounded control channel delivered (`5f696c9e`, loopback-proven,
184 pytest green — suite now 181+6skip after environment-dependent skips on this host).

**US1 genuineness note (the go-condition that was open at handoff)**: US1 is no longer the flagged
shadow layer — the guard genuinely evaluates in the runtime: form (a) via the ruled (a1)
runtime-defined-guard machinery over the user program, form (b) as a native system guard primitive
callable with zero user clauses; both proven three-valued (Success/Suspend/Fail) on all 12 vectors
+ 4 worked examples with identical outcome maps (SC-009), 100% matcher parity on shared vectors
(SC-003), and the §1.14 gate audit clean (SC-001).

## BLOCKED-record scan

One BLOCKED record exists: the **MSVC-native quicer build** (upstream quicer C source is
unix-only; same in 0.2.15 and 0.4.3) — recorded + escalated per FR-010 in
`gavri/10-profile-c.md`/`90-summary.md` and carried in the Gabi-directed SHIP-HANDOFF. It does
**not** block any acceptance scenario: SC-005 (US2) PASSED with the documented, reproducible WSL
provisioning path, and the PASS verdict was recorded with the MSVC note relayed (`523047d3`).
Surfaced here again so the ship decision is made with it in view; an express Gabi nod in the ship
message closes T031's letter ("any BLOCKED record ⇒ pending express re-ruling").

## Deviations recorded (not gate items)

- T020 on-wire packet capture staged-not-taken; non-loopback proven by two-machine consoles
  (`two-host/run.md`).
- T028 fix #7 realized as noeol/eol reassembly (outcome-equivalent; recorded for Gabi).
- NEW compiler observations from US1 (recorded in `guard/form-a.md`): named-anonymous variables in
  head structures fail codegen (`Undefined variable`), and constant-operand `=?=` does not parse in
  guard position — both pre-existing, flagged for a follow-up ruling/fix, not worked around
  silently (equivalent legal forms used, deviations documented).

## Verdict

**Gate condition met: all four user stories PASS with evidence; zero deferred gate items.**
Remaining before release: Gabi's ship decision (T032) — `buildkit ship` must run on the canonical
branch `049-wave1-guard-link-acceptance` after merging this branch's PR, per
`gavri/SHIP-HANDOFF.md` (this host's buildkit is the minimal build without `--skip-preflight`).
