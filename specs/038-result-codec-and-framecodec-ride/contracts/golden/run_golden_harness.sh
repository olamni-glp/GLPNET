#!/usr/bin/env bash
# T031 — cross-runtime golden byte-parity harness (SC-002).
#
# Each runtime's golden test asserts `encode(corpus) == the pinned corpus.hex`
# (Dart authors it, T026; C# + Gleam reproduce it, T027). If all three pass then
# Dart == C# == Gleam == golden byte-for-byte on the NON-gated corpus — the SC-002
# byte-parity criterion, proven transitively through the single pinned artifact.
#
# The dev-host toolchains are not on PATH; override via env (defaults below match the
# documented dev box — see specs/036-.../ env notes).
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
DART="${DART:-/c/src/flutter/bin/cache/dart-sdk/bin/dart.exe}"
DOTNET="${DOTNET:-/c/Users/smbuser/AppData/Local/Microsoft/dotnet/dotnet.exe}"
GLEAM="${GLEAM:-/c/Users/smbuser/AppData/Local/Microsoft/WinGet/Packages/Gleam.Gleam_Microsoft.Winget.Source_8wekyb3d8bbwe/gleam.exe}"
ERLANG_BIN="${ERLANG_BIN:-/c/Program Files/Erlang OTP/bin}"

fail=0

echo "== [1/3] Dart golden (source of truth authors corpus.hex) =="
( cd "$REPO/glp_runtime" && "$DART" test test/codec/golden_corpus_test.dart ) || fail=1

echo "== [2/3] C# golden (reproduces corpus.hex) =="
( cd "$REPO" && "$DOTNET" test csharp/glp_result_codec/tests/ \
    --filter "FullyQualifiedName~GoldenByteIdentity" --nologo ) || fail=1

echo "== [3/3] Gleam golden (reproduces corpus.hex) =="
( cd "$REPO/glp_gleam" && PATH="$ERLANG_BIN:$PATH" "$GLEAM" test ) || fail=1

echo
if [ "$fail" -eq 0 ]; then
  echo "GOLDEN HARNESS: PASS — Dart == C# == Gleam == corpus.hex (non-gated, SC-002)"
else
  echo "GOLDEN HARNESS: FAIL — a runtime did not reproduce corpus.hex (see output above)"
fi
exit "$fail"
