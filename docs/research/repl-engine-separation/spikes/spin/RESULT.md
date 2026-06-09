# Promela/SPIN wire-protocol spike — RESULT

**Status**: STUB — this is the acceptance artifact for US5, filled in **T024** against real SPIN (R13/R14, FR-080/071). Desk research does NOT satisfy this.

**Model under verification**: `spikes/spin/front_back.pml` — a minimal front↔back request/response handshake (front sends request → awaits response; back awaits request → sends response) with named safety (deadlock-freedom, no unspecified receptions) + a named progress/liveness property. Minimal handshake ONLY; full model deferred to #5/#6 (DEF-A3, FR-081 — HANDSHAKE-1).

To be recorded at T024:
- Deadlock-freedom + progress verdict (or counterexample trace)
- The named safety + liveness properties checked
- Reproduction: `spikes/spin/run.sh` / `run.ps1`
- Tool versions: `spikes/spin/tool-versions.txt`

(Acceptance: SC-011/009.)
