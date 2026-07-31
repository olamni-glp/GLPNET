# Reproduction (Windows) — FULL wire-protocol SPIN model for the 061 split (T015, FR-040).
# Thin wrapper: real SPIN + gcc run in WSL2 (Ubuntu). Forwards to the canonical run.sh.
$ErrorActionPreference = "Stop"
$wslPath = "/mnt/d/bstdev/research/glp/glpnet/docs/research/repl-engine-separation/models/spin/run.sh"
wsl.exe -d Ubuntu -- bash -lc "bash '$wslPath'"
exit $LASTEXITCODE
