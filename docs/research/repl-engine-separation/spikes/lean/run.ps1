# Reproduction (Windows) — Lean 4 tactic-loop spike (US3, 027, T015).
# Thin wrapper: real Lean 4.30.0 runs in WSL2 (R10). Forwards to the canonical run.sh.
$ErrorActionPreference = "Stop"
$wslPath = "/mnt/d/bstdev/research/GLP/GLPNET/docs/research/repl-engine-separation/spikes/lean/run.sh"
wsl.exe -d Ubuntu -- bash -lc "bash '$wslPath'"
exit $LASTEXITCODE
