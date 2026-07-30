# Reproduction (Windows) — UPPAAL timed model of the 061 supervision loop (T030, FR-040).
# Thin wrapper: real verifyta runs in WSL2 (Ubuntu). Forwards to the canonical run.sh.
# Pass the engineer's license key via $env:UPPAAL_KEY (see run.sh's license gate).
$ErrorActionPreference = "Stop"
$wslPath = "/mnt/d/bstdev/research/glp/glpnet/docs/research/repl-engine-separation/models/uppaal/run.sh"
$keyPrefix = if ($env:UPPAAL_KEY) { "UPPAAL_KEY='$env:UPPAAL_KEY' " } else { "" }
wsl.exe -d Ubuntu -- bash -lc "$keyPrefix bash '$wslPath'"
exit $LASTEXITCODE
