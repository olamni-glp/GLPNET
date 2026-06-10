# Reproduction (Windows) — MLIR/GLP-dialect round-trip spike (US4, 027, T020).
# Thin wrapper: the real compiled-LLVM mlir.ir wheel is Linux-only, so the spike
# runs in WSL2 (Ubuntu). This forwards to the canonical run.sh inside WSL.
$ErrorActionPreference = "Stop"
$wslPath = "/mnt/d/bstdev/research/GLP/GLPNET/docs/research/repl-engine-separation/spikes/mlir/run.sh"
wsl.exe -d Ubuntu -- bash -lc "bash '$wslPath'"
exit $LASTEXITCODE
