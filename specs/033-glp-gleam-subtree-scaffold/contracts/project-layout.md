# Contract — Project Layout (`glp_gleam/`)

The scaffold MUST produce **exactly** this committed layout (build artifacts excluded by
`.gitignore`). This is the acceptance surface for FR-001, FR-004, FR-006, FR-010, SC-003, US2.

```text
glp_gleam/
├── gleam.toml                 # name = "glp_gleam"; deps gleam_stdlib, gleam_erlang; dev gleeunit
├── manifest.toml              # COMMITTED lock — exact pins; NO gleam_otp
├── .gitignore                 # *.beam, *.ez, /build, erl_crash.dump
├── README.md                  # purpose + commands + dossier §6 pointer
├── smoke.sh                   # local WSL gate (see build-test-smoke.md)
├── src/
│   └── glp/
│       ├── analysis.gleam
│       ├── bytecode.gleam
│       ├── compiler.gleam
│       ├── engine.gleam
│       ├── link.gleam
│       ├── lint.gleam
│       ├── multiagent.gleam
│       └── runtime.gleam
└── test/
    └── glp_gleam_test.gleam
```

## Invariants

1. **Sibling placement** — `glp_gleam/` is a direct child of the repo root, beside `glp_runtime/`
   and `glp_runtime_net/`. *(FR-001, US2 AS-3)*
2. **Subsystem set is exactly the 8** — `{analysis, bytecode, compiler, engine, link, lint,
   multiagent, runtime}`, 1:1 with `glp_runtime/lib/` (verified present). No 9th module; none
   missing. *(FR-004, SC-003)*
3. **Module path = `glp/<subsystem>`** — every placeholder lives under `src/glp/`. *(dossier §6)*
4. **All segments legal Gleam** — every directory + file stem matches `^[a-z][a-z0-9_]*$` and is
   non-reserved. The skeleton itself contains **no** illegal segment (it never needs F2's
   normalization escape — the names are already legal). *(FR-006)*
5. **Tracked vs ignored** — `gleam.toml`, `manifest.toml`, `.gitignore`, `README.md`, `smoke.sh`,
   `src/**`, `test/**` are git-tracked; `build/`, `*.beam`, `*.ez`, `erl_crash.dump` are ignored.
   *(FR-010)*

## Verification

- `find glp_gleam -type f -not -path '*/build/*'` lists exactly the tracked files above.
- For S in the 8 subsystems: `test -f glp_gleam/src/glp/$S.gleam`.
- `comm`-style diff of `{src/glp/*.gleam stems}` against `{glp_runtime/lib/* dirnames}` is empty
  both ways (set equality). *(SC-003)*
- `git status --porcelain glp_gleam/build` is empty after a build (ignored). *(FR-010)*
