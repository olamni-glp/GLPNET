# MLIR/GLP-dialect round-trip spike — RESULT

**Status**: STUB — this is the acceptance artifact for US4, filled in **T020** against real MLIR (R13/R14, FR-043/071). Desk research does NOT satisfy this.

**Fragment under round-trip**: minimal GLP IL fragment (one clause touching each of the four primitives `HEAD-unify` / `GUARD-test` / `BODY-spawn` / `suspend-reactivate` once — ILFRAG-1, research §2).

To be recorded at T020:
- Pass/fail of `decode(encode(p)) == p` on the fragment (deterministic oracle decides)
- Confirmation Claude was restricted to structural generation only
- Reproduction: `spikes/mlir/run.sh` / `run.ps1`
- Tool versions: `spikes/mlir/tool-versions.txt`

(Acceptance: SC-007/009.)
