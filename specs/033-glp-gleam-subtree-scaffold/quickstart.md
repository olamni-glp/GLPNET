# Quickstart — Build & Test the `glp_gleam` Subtree

This is the maintainer-facing acceptance walkthrough for F3 (SC-001 / SC-002 / SC-004 / SC-005).
Everything runs **under WSL Ubuntu** with the F1-pinned toolchain.

## Prerequisites (verified reachable from this repo)

- Gleam **1.17.0** — `gleam --version` → `gleam 1.17.0`
- Erlang/OTP **25.3.2.8** — `erl -eval 'io:format("~s~n",[erlang:system_info(otp_release)]),halt().' -noshell` → `25`
- `rebar3` **3.19.0** — `which rebar3`

Enter WSL from the repo root:

```bash
wsl.exe -e bash -lc 'cd glp_gleam && <command>'
```

## 1. Resolve dependencies to the committed lock (SC-004)

```bash
cd glp_gleam
gleam deps download
grep -c gleam_otp manifest.toml      # → 0  (disallowed dep absent)
```
Expected: resolves `gleam_stdlib` 1.0.3, `gleam_erlang` 1.3.0, `gleeunit` 1.11.0; `gleam_otp` is
absent from the committed `manifest.toml`.

## 2. Build to Erlang/BEAM (SC-001)

```bash
gleam build --target erlang
```
Expected: exit 0, zero errors — the 8 placeholder modules under `src/glp/` and the test all compile,
with no ported runtime semantics present.

## 3. Run the test suite (SC-002)

```bash
gleam test --target erlang
```
Expected: ≥1 test, 0 failures.

## 4. Run the smoke gate (SC-005)

```bash
bash smoke.sh        # from glp_gleam/, under WSL
echo $?              # → 0 on green
```
`smoke.sh` loudly checks the toolchain (Gleam 1.17.0 · OTP 25), then runs the build + test, exiting
non-zero on any red. It is the F3 local gate — a peer of `test/run_all_tests.sh` / `codeconv` pytest
/ `buildkit` preflight in the repo's local-gate convention. (It is intentionally a *separate* WSL
gate, not embedded in `run_all_tests.sh` — see research.md R-003.)

## 5. Confirm the subsystem skeleton (SC-003)

```bash
for s in analysis bytecode compiler engine link lint multiagent runtime; do
  test -f src/glp/$s.gleam && echo "ok $s" || echo "MISSING $s"
done
```
Expected: 8 × `ok` — one placeholder per authoritative Dart subsystem (1:1 with `glp_runtime/lib/`).

## 6. Confirm additive-only & artifacts ignored (FR-009 / FR-010 / SC-006)

```bash
git status --porcelain glp_gleam/build      # → empty (build/ ignored)
git -C .. diff --name-only -- codeconv/src/codeconv/tools/    # → empty (no stage-tool edits)
```

## What this is NOT (scope guards)

- No ported GLP runtime semantics (those are F4–F9).
- No AtomVM-specific or JavaScript-target build (Erlang/BEAM only).
- No remote CI (the smoke is a local WSL gate).
- No codeconv stage-tool edits; conversion recognition is config-only (`workspace_settings` +
  existing `dart_gleam` pair).
