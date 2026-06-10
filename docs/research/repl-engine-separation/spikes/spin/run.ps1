# Reproduction (Windows) — Promela/SPIN wire-protocol spike (US5, 027, T024).
# Thin wrapper: real SPIN 6.5.1 + gcc run in WSL2 (Ubuntu). Forwards to the canonical run.sh.
$ErrorActionPreference = "Stop"
$wslPath = "/mnt/d/bstdev/research/GLP/GLPNET/docs/research/repl-engine-separation/spikes/spin/run.sh"
wsl.exe -d Ubuntu -- bash -lc "bash '$wslPath'"
exit $LASTEXITCODE
