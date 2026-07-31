# Reproduction (Windows) — crash/restore/resume TLA+ model for the 061 split (T035, FR-040).
# Thin wrapper: OpenJDK + tla2tools.jar run in WSL2 (Ubuntu). Forwards to the canonical run.sh.
$ErrorActionPreference = "Stop"
$wslPath = "/mnt/d/bstdev/research/glp/glpnet/docs/research/repl-engine-separation/models/tla/run.sh"
wsl.exe -d Ubuntu -- bash -lc "bash '$wslPath'"
exit $LASTEXITCODE
