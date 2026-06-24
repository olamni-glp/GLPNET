# glp_gleam

The Gleam/BEAM port skeleton for GLP — an **empty-but-building** subtree that is the
buildable home and known-good baseline the heavy downstream port features (F4+) land in.
It compiles to Erlang/BEAM and its test suite passes while containing **no ported GLP
runtime semantics yet**.

Feature 033 (`glp-gleam-subtree-scaffold`), epic `gleam-atomvm`. Source basis for the port
is the authoritative Dart runtime `glp_runtime/` (dossier §6).

## Layout

```
glp_gleam/
├── gleam.toml          # project metadata + dep ranges (no gleam_otp)
├── manifest.toml       # committed lock — gleam_stdlib 1.0.3, gleam_erlang 1.3.0, gleeunit 1.11.0
├── smoke.sh            # local WSL gate: toolchain check + build + test
├── src/glp/            # one placeholder module per Dart subsystem, 1:1 with glp_runtime/lib/
│   ├── analysis.gleam  bytecode.gleam  compiler.gleam  engine.gleam
│   └── link.gleam      lint.gleam      multiagent.gleam  runtime.gleam
└── test/glp_gleam_test.gleam   # gleeunit smoke (≥1 passing test)
```

Each `src/glp/<subsystem>.gleam` is a placeholder (module-doc only — no exported definitions, no
ported logic); a downstream feature fills it from its Dart source of truth at `glp_runtime/lib/<subsystem>/`.

## Build & test (under WSL only)

Gleam is **not** installed Windows-native here — run everything under WSL Ubuntu with the
pinned toolchain (Gleam **1.17.0** · Erlang/OTP **25.3.2.8** · rebar3 **3.19.0**):

```bash
wsl.exe -e bash -lc 'cd glp_gleam && gleam build --target erlang'   # SC-001: 0 errors
wsl.exe -e bash -lc 'cd glp_gleam && gleam test  --target erlang'   # SC-002: ≥1 test, 0 failures
```

Erlang/BEAM target only — the JavaScript and AtomVM-specific targets are out of scope for F3.

## Smoke gate

`smoke.sh` is the F3 local gate — a peer of `test/run_all_tests.sh`, `codeconv` pytest, and
`buildkit` preflight in the repo's individually-invoked local-gate convention. It loudly checks
the toolchain (Gleam 1.17.0 · OTP 25), then runs build + test, exiting non-zero on any red:

```bash
wsl.exe -e bash -lc 'cd glp_gleam && bash smoke.sh'   # exit 0 iff green (SC-005)
```

It is intentionally a **separate** WSL gate, not embedded in `test/run_all_tests.sh` (that suite
is the Windows-native dart REPL suite; Gleam requires WSL — research.md R-003).

## Conversion-pipeline recognition (FR-008, config-only)

The Dart→Gleam conversion data flow recognizes `glp_gleam/` **purely through existing
configuration** — no edit to any inventory/structure stage tool (`init`/`discover`/`scaffold`/
`mirror`). The mechanism already exists from F2 (`032-codeconv-gleam-langpair`):

- the `dart_gleam` langpair is already registered (`codeconv/src/codeconv/langpairs/dart_gleam/`);
- the active pair and the conversion roots are **config values** in `codeconv.workspace_settings`
  (written by `codeconv init`): `scaffold` reads `target_rel_root`, `mirror` reads `output_rel`,
  the pair is resolved via `resolve_workspace_pair(...)`.

So pointing the pipeline at this subtree is a placement + `codeconv init`/config action, mirroring
how `glp_runtime_net/` and `out/csharp/` participate for the C# pipeline — never a code change.
F3 delivers recognition at the lightweight level the pipeline needs ("recognized + build/test
green"); deeper pipeline runs land with the heavy port features. See
`specs/033-glp-gleam-subtree-scaffold/contracts/conversion-recognition.md`.

## Reference (do not duplicate)

The architecture, toolchain rationale, and `gleam_otp` exclusion live in the F1 dossier —
see `docs/research/gleam-atomvm/dossier.md` **§6 (Downstream handoff for F2/F3)**. This README
points to it rather than restating it (single source of truth).
