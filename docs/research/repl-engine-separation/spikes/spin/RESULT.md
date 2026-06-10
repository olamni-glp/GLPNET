# Promela/SPIN wire-protocol spike — RESULT

**Status**: ✅ **PASS** — recorded against **real SPIN 6.5.1** in **T024**, 2026-06-10. This is the
US5 acceptance artifact (R13/R14, FR-080/071, SC-011/009). Desk research does **not** satisfy this;
an executed real-tool model-check does.

## What was verified

`spikes/spin/front_back.pml` — a minimal front↔back request/response handshake (front sends one
REQUEST then awaits one RESPONSE; back awaits the REQUEST then sends the RESPONSE), over depth-1
channels with `xs`/`xr` exclusive ownership. Two verifier runs against real SPIN (HANDSHAKE-1):

| # | Check | SPIN configuration | Named properties | Verdict |
|---|---|---|---|---|
| 1 | **Liveness/progress** | LTL claim active, fairness (`./pan -a -f`) | `request_eventually_answered` = `[] (req_sent -> <> resp_seen)` | **errors: 0** |
| 2 | **Safety / deadlock** | LTL line removed → invalid-end-states + assertions ENABLED (`./pan`) | deadlock-freedom (no invalid end state); no unspecified receptions (`xs`/`xr`) | **errors: 0** |

> Run 2 removes the `ltl` line because an active never claim disables SPIN's invalid-end-state
> detection ("invalid end states − disabled by never claim"). Removing it lets SPIN check
> deadlock-freedom directly. The committed `front_back.pml` is unchanged; `run.sh` derives the
> claim-free variant in a temp dir.

## Result (deterministic model checker — FR-080)

```
(1) LIVENESS  — never claim request_eventually_answered, fairness enabled
    Full statespace search:  19 states stored, errors: 0
    acceptance cycles + (fairness enabled)  →  request_eventually_answered HOLDS

(2) SAFETY    — invalid end states +, assertion violations +, no never claim
    Full statespace search:  12 states stored, 0 unreached, errors: 0
    →  deadlock-free; no invalid end state; no unspecified reception (xs/xr)

run.sh exit code: 0   →   PASS
```

All three named properties hold: **deadlock-freedom**, **no unspecified receptions**, and the
**`request_eventually_answered`** progress property. No counterexample. All proctype states reached
(`0 of 7` / `0 of 4` unreached).

## No LM on the verification path (FR-073)

SPIN is a deterministic local model checker — model in, verdict/counterexample out. No language model
participates; the checker is the oracle (the no-API rule holds trivially; grep-gated at T010/T025).

## Reproduction

- Canonical (WSL2): `spikes/spin/run.sh` → both verifier runs, asserts `errors: 0` on each.
- Windows wrapper: `spikes/spin/run.ps1` → forwards to `run.sh` via `wsl.exe -d Ubuntu`.
- Model: `spikes/spin/front_back.pml` (HANDSHAKE-1).
- Tool versions: `spikes/spin/tool-versions.txt` — SPIN 6.5.1, gcc 13.3.0, WSL2 Ubuntu 24.04.

## Scope (minimal feasibility spike — FR-074/081)

ONE request, ONE response, two proctypes. The **complete** wire-protocol / result-envelope Promela
model is deferred to the wire-protocol seeds **#5/#6** (DEF-A3). The armoury that every wire seed
selects from is [`../../reconciliation/PROTOCOL-VERIFICATION-ARMOURY.md`](../../reconciliation/PROTOCOL-VERIFICATION-ARMOURY.md).

**Conclusion**: the "a minimal front↔back protocol is deadlock-free and makes progress" claim is
backed by a recorded real-SPIN model-check — not desk research.
