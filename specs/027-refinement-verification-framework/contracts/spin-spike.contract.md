# Contract: Promela/SPIN Wire-Protocol Validation Spike + Armoury  (FR-076–081, R14/R15)

**Artifacts**: `PROTOCOL-VERIFICATION-ARMOURY.md` (matrix) + `docs/research/repl-engine-separation/spikes/spin/`
(runnable spike).

## Provides
An empirical demonstration that a minimal front↔back request/response protocol is deadlock-free and makes
progress, checked with **real SPIN**; plus a documented tool armoury with seed-type selection guidance.

## Acceptance (must all hold)
1. **Armoury (FR-078–079, SC-012)**: a tool matrix of ≥7 tools — **SPIN/Promela** (default), **TLA+/PlusCal**,
   **UPPAAL**, **NuSMV/nuXMV**, **mCRL2**, **FDR4**, **CADP** — each with modeling paradigm, verification engine,
   primary strength, best-for use case; plus seed-type selection guidance (SPIN default; TLA+ consensus/multi-client;
   UPPAAL timed; nuXMV symbolic/large; mCRL2/FDR4/CADP process-algebra/asynchronous).
2. **SPIN adoption (FR-076/077)**: SPIN named the REQUIRED pragmatic-tier default; mandatory in the metric
   table of #2/#5/#6; each such seed names its specific safety + liveness properties.
3. **Spike (FR-080, runnable)**: a minimal Promela model of the front↔back request/response handshake checked
   with real SPIN for deadlock-freedom, no unspecified receptions, and a named progress/liveness property;
   verdict recorded (or counterexample surfaced).
4. **Scope (FR-081, DEF-A3)**: the spike covers a **minimal handshake only**; the full envelope/protocol model
   is deferred to #5/#6.
5. **Reproducible (FR-071)**: committed `run.sh`/`run.ps1` (`spin -a` → compile `pan.c` → run), `tool-versions.txt`,
   recorded `RESULT.md`.

## Verification
- Run real SPIN on the model → deadlock-freedom + progress reported (or counterexample) with named properties
  (US5-AC1); reproducible (US5-AC3, SC-009).
- **Closes**: SC-011, SC-012, SC-010 (protocol limb). Desk argument does NOT satisfy this contract (FR-080).
