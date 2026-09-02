# Quickstart — 101-gleam-capability-delivery

Toolchain (this host has no dev tools on the persisted PATH):

    export PATH="$HOME/.dotnet:$HOME/.local/bin:$HOME/erlang-otp-29/bin:$HOME/dart-sdk/bin:$PATH"

## Build the workstation (BEAM) ring
    cd glp_gleam && gleam build

## Run corpus parity (the existing instrument — reused, not rebuilt)
    bash test/parity/run_gleam_corpus.sh
    # expect: agree=N diverge=0, and a denominator on every line
    # measured 2026-09-02: agree=206 diverge=0 blocked=0 gap/fork=0

## SC-001 — prove independence from Dart
Run the corpus with the Dart toolchain absent from PATH. Any case that only passes with Dart
present is a refuter for Assumption 2.

## Build the app (AtomVM) ring
    # expect a BUILD-TIME refusal naming any construct outside the AtomVM subset (C3).
    # The MAUI Blazor Hybrid host is target-side and ABSENT here: host-side conformance
    # reports UNREAD with a named reason, never pass, never zero (C4-R / SC-006).
