# Contract — Build, Test & Smoke Gate

Acceptance surface for FR-002, FR-003, FR-007, SC-001, SC-002, SC-005, and the toolchain edge case.
All commands run **under WSL** (Ubuntu, pinned toolchain) from `glp_gleam/`.

## Build (FR-002, SC-001)

```bash
gleam build --target erlang
```
- **Expected**: exit 0, zero errors, no ported runtime semantics present ("empty-but-building").
- A single documented command produces a successful Erlang/BEAM build. *(SC-001)*

## Test (FR-003, SC-002)

```bash
gleam test --target erlang
```
- **Expected**: ≥1 test, **0 failures** (gleeunit). A single documented command runs the suite
  green. *(SC-002)*

## Smoke script — `glp_gleam/smoke.sh` (FR-007, SC-005)

**Interface**
- **Invocation**: `bash glp_gleam/smoke.sh` under WSL (cwd-independent: the script resolves its own
  directory and `cd`s into the subtree).
- **Inputs**: none (reads the pinned toolchain from `PATH`).
- **Exit code**: `0` iff toolchain OK **and** build green **and** tests green; non-zero otherwise.
- **Output**: human-readable progress; on failure, an **actionable** message.

**Required steps (in order)**
1. **Toolchain check (loud)** — assert `gleam --version` reports `1.17.0` and `erl` OTP release is
   `25`. On mismatch/absence: print the required versions (Gleam 1.17.0 · Erlang/OTP 25.3.2.8 · WSL)
   and exit non-zero. *Never silently pass against an unexpected toolchain.* *(edge case "toolchain
   absent or wrong version")*
2. `gleam build --target erlang` — abort non-zero on failure.
3. `gleam test --target erlang` — abort non-zero on failure.
4. Print a clear PASS line and exit 0.

**Scope guards**
- Targets **only** Erlang/BEAM. MUST NOT build/test the JavaScript or AtomVM targets (out of scope —
  spec Assumptions).
- Additive: the script lives in the new subtree and changes no existing gate. *(FR-009)*

## Local-gate wiring (FR-007, SC-005)

- The smoke is a **peer gate** in the repo's existing convention (individually-invoked bash gates:
  `test/run_all_tests.sh`, `codeconv` pytest, `buildkit` preflight) — referenced as the F3 gate from
  `quickstart.md` and `glp_gleam/README.md`.
- It is **not** embedded inside `test/run_all_tests.sh` (that suite is the Windows-native dart REPL
  suite; Gleam requires WSL — see research.md R-003). *Owner-awareness flag, non-blocking.*

## Dependency resolution (SC-004; see dependency-lock.md)

```bash
gleam deps download    # resolves to the committed manifest.toml versions exactly
```
- **Expected**: resolves `gleam_stdlib` 1.0.3, `gleam_erlang` 1.3.0, `gleeunit` 1.11.0 and pulls in
  **no** `gleam_otp`. *(US1 AS-3, SC-004)*
