# Verification Report — 042-crdtmsg-verify-harden

> **Run date**: 2026-07-04 · **Pass executor**: Claude (feature `042-crdtmsg-verify-harden`, marathon run `mrun-5b9a5befaae8`; all verification labor Claude agents per Constitution V)
> **Verdict summary**: PENDING — populated at report assembly (T029).
>
> **Test-Protocol baseline note (T003)**: `bash test/run_all_tests.sh` is **environment-blocked on
> this Windows host** — the script hard-invokes `/home/user/dart-sdk/bin/dart` (absent here), a
> pre-existing harness/env mismatch recorded in `docs/known-issues.md` §"Feature 041 — GLP REPL
> baseline on Windows (T056)". Section A errors 198/204 with "No such file or directory" and the
> run aborts in Section B. This feature's diff is documentation-only (no code-test surface is
> touched), so the bracketing baseline is recorded as ENV-BLOCKED (identical state expected at
> re-test, T030), not green. Pre-change HEAD: `6ff3a8c9`.

## Verification baselines (FR-005/FR-015 hybrid ruling; plan.md table, scanner-C row resolved by T002)

| Baseline | Commit | Used for |
|---|---|---|
| F1/F2 delivery-time | `c20317ce` (2026-07-03 22:25 +0100) | F1/F2 method-conformance + F1 §12 / F2 §11 ledger re-derivation |
| F3 delivery-time | `6ecc975f` (2026-07-04 08:33 +0100; initial delivery `ee94a04f` 07:52, amended `3204bd1b` E1–E9 encoding, `6ecc975f` E1 store-side fix) | F3 method-conformance + §3/§4 ledger re-derivation |
| F3 scanner-C repo view | `d2689a71` (2026-07-04 07:44 +0100) — **resolved this pass (T002)**: the `037-virtual-3270-term` branch head immediately preceding F3's delivery commit `ee94a04f`. Candidates `c20317ce..d2689a71` differ only in `.specify/` roadmap-sync exports + codify notes (verified by `git diff --name-only c20317ce d2689a71`), so every scanned surface (docs/, specs/, csharp/, glp_runtime/) is identical across the candidate range — the residual ambiguity is immaterial. | re-deriving what scanner C could see |
| Current HEAD | `6ff3a8c9` (branch `042-crdtmsg-verify-harden`, execution-start HEAD) | hardening, PROVISIONAL closure, drift dispositions, evidence materialization |
| 041 ship evidence | tag `v2026.07.04.4` = `0945c29a` | PROVISIONAL trigger adjudication (US3) |

## 1. Method reconstruction (FR-001)

### 1.1 F1 — priorart-sibling-scan

*(pending T004)*

### 1.2 F2 — webresearch-corpus

*(pending T005)*

### 1.3 F3 — buildingblocks-synthesis

*(pending T006)*

## 2. Conformance ledgers (FR-002/FR-003, SC-001)

### 2.1 F1 conformance ledger (CF-F1-*)

*(pending T007)*

### 2.2 F2 conformance ledger (CF-F2-*)

*(pending T008)*

### 2.3 F3 conformance ledger (CF-F3-*)

*(pending T009)*

## 3. Singleton re-adjudication (FR-004/FR-014, SC-002)

*(pending T012 derivation + T017/T018 re-scans and verdicts)*

## 4. Coverage-ledger re-derivation (FR-005, SC-004)

### 4.1 F1 §12 signal×repo matrix

*(pending T013)*

### 4.2 F2 §11 gap-coverage table

*(pending T014)*

### 4.3 F3 §3 constraint matrix

*(pending T015)*

### 4.4 F3 §4 closure ledger (28/28)

*(pending T015)*

## 5. Drift dispositions (FR-006, SC-005)

*(pending T019)*

## 6. Ruling propagation (FR-007, SC-006)

*(pending T020)*

## 7. PROVISIONAL register closure (FR-008/FR-009, SC-003)

*(pending T021–T023)*

### 7a. Promotions for owner review

*(pending T023)*

### 7b. Escalations

*(pending T023)*

## 8. Evidence-pointer census (FR-010, SC-007)

*(pending T025–T028; full census: [evidence/evidence-index.md](evidence/evidence-index.md))*

## 9. Owner escalations (FR-013)

*(pending T029)*

## 10. Proposed roadmap follow-ups (FR-009)

*(pending T024/T029)*

## 11. Amendment index (FR-011, SC-008)

*(pending T029. Note: the three `SETUP-042-*` change-log rows logging each doc's change-log-section
addition (amendment-changelog contract rule 4) reference this report's baselines/setup header rather
than a §1–§8 finding — a documented exception: no §1–§8 finding existed yet at setup time.)*

## 12. Success-criteria checklist

*(pending T029/T031: SC-001..SC-009 with measured values)*
