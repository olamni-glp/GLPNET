# Phase 0 Research — glp_gleam subtree scaffold

All decisions follow the resolved-guidance rule: **prefer the simplest design that satisfies the
spec; call out constraints and rejected alternatives explicitly.** The spec had no open
`NEEDS CLARIFICATION` markers (its three 2026-06-24 clarifications resolved them at specify time);
the items below resolve the remaining *technical* choices the plan needs.

## R-001 — Project name & source namespace

- **Decision**: Gleam project `name = "glp_gleam"` in `gleam.toml`; subsystem modules under the
  `glp` source namespace, i.e. `src/glp/<subsystem>.gleam` → Gleam module path `glp/<subsystem>`.
- **Rationale**: dossier §6 prescribes "modules under `src/glp/`" and a repo-root subtree named for
  the port. `glp_gleam` is a legal Gleam project name (snake, lowercase-led) and disambiguates the
  *project/subtree* from the `glp` *module namespace*. Both `glp_gleam` and each segment `glp`,
  `analysis`, … satisfy the F2 segment rule `^[a-z][a-z0-9_]*$`, non-reserved (FR-006).
- **Alternatives rejected**: (a) project `name = "glp"` — collides conceptually with the `glp`
  module namespace and is a less descriptive subtree name; (b) flat modules `src/<subsystem>.gleam`
  (no `glp/` prefix) — contradicts dossier §6 and loses the namespace that keeps downstream ports
  1:1 and collision-free.

## R-002 — Dependency pinning & `gleam_otp` exclusion (FR-005, SC-004)

- **Decision**: `gleam.toml` carries the **same version ranges the F1 spike proved**
  (`gleam_stdlib` / `gleam_erlang` `>= 0.34.0 and < 2.0.0`; dev `gleeunit` `>= 1.0.0 and < 2.0.0`).
  The **committed `manifest.toml` is the pin**: it locks the exact F1-ratified versions
  `gleam_stdlib` 1.0.3, `gleam_erlang` 1.3.0, `gleeunit` 1.11.0 (with their checksums). `gleam_otp`
  is excluded simply by **never declaring it**; F1's manifest confirms none of the three declared
  deps pulls it transitively, so SC-004 ("0 occurrences in the committed lock") holds and is
  grep-verifiable.
- **Rationale**: in Gleam, `manifest.toml` is the lockfile (`gleam` respects an existing manifest
  and does not upgrade within-range unless asked) and is explicitly meant to be committed ("You
  should check this file into your source control repository" — manifest header). Ranges in
  `gleam.toml` + an exact committed manifest is the **proven** F1 shape; reusing it is the simplest
  design that satisfies "pinned to the F1-ratified versions" + "committed lock for reproducible
  builds".
- **Generation note**: `manifest.toml` is *managed by Gleam* — it is produced by running
  `gleam deps download` / `gleam build` **under WSL**, then committed. It is not hand-edited. The
  implement stage generates it under WSL (toolchain verified reachable) and commits the result.
- **Alternatives rejected**: (a) tightening `gleam.toml` to exact `==` versions — Gleam range syntax
  doesn't pin transitive deps anyway (the manifest does), and it diverges from the proven F1 shape
  for no added guarantee; (b) hand-writing `manifest.toml` — forbidden by its own header and
  fragile (checksums).

## R-003 — Smoke script shape & "wired into the local-gate convention" (FR-007, SC-005)

- **Decision**: a self-contained `glp_gleam/smoke.sh` (additive; lives in the new subtree) that,
  run **under WSL** from the subtree root: (1) **loudly checks the toolchain** — `gleam --version`
  is `1.17.0` and `erl` OTP release is `25`, failing with an actionable message naming the required
  versions if not (edge case "toolchain absent/wrong version"); (2) `gleam build --target erlang`;
  (3) `gleam test --target erlang`; exit non-zero on any red. It is "wired into the existing
  local-gate convention" by **being a peer gate that follows the same convention** (a runnable bash
  gate script, like `test/run_all_tests.sh`) and is **referenced from `quickstart.md`** and the
  subtree `README.md` as the F3 gate.
- **Rationale**: the repo has **no CI and no single master aggregator** — its gates
  (`test/run_all_tests.sh`, `codeconv` pytest, `buildkit` preflight) are invoked individually by
  convention. The convention is "a runnable local gate script", not "a line inside
  run_all_tests.sh". Providing such a script, under WSL, satisfies FR-007 with the least machinery.
- **Constraint / why NOT hook into `test/run_all_tests.sh`**: that suite is the **GLP REPL** suite,
  driven by a **Windows-native `dart.exe`** (per memory: hardcoded Linux dart path overridden on
  Windows). Gleam requires **WSL**. Literally calling `gleam` from inside `run_all_tests.sh` would
  (a) cross runtimes/OS boundaries mid-suite, (b) risk the additive-only invariant (FR-009: must not
  change any existing subtree's test behavior), and (c) fail on machines without WSL. So the smoke
  is a **separate** WSL gate. **This is the one judgment call worth owner awareness** — if the owner
  wants a literal call wired into a specific aggregator, that is a one-line follow-up, but it is
  architecturally cleaner kept separate. (Surfaced, not blocking.)
- **Alternatives rejected**: (a) a GitHub Actions / remote CI workflow — explicitly OUT OF SCOPE
  (FR-007; the repo has no CI today); (b) embedding the gleam build inside `run_all_tests.sh` — see
  constraint above.

## R-004 — FR-008 conversion recognition with zero stage-tool edits

- **Decision**: `glp_gleam/` is recognized by the Dart→Gleam conversion data flow **purely through
  existing configuration**, with **no** edit to any inventory/structure stage tool
  (`init`/`discover`/`scaffold`/`mirror`). The mechanism already exists from F2:
  - the `dart_gleam` langpair is **already registered**
    (`codeconv/src/codeconv/langpairs/dart_gleam/`);
  - the active pair and the conversion **roots are config values** read from
    `codeconv.workspace_settings` (written by `codeconv init`): `scaffold` reads `target_rel_root`,
    `mirror` reads `output_rel`, the pair is `resolve_workspace_pair(...)`. None of these is
    hardcoded in stage-tool source.
  So F3's obligation is met by **placement + (re-)`init` configuration**, mirroring how
  `glp_runtime_net` / `out/csharp` participate for the C# pipeline — not by code change. SC-006
  ("0 codeconv stage-tool source files changed") is therefore satisfiable by construction.
- **Clarified scope (spec Assumptions, narrowed 2026-06-24)**: `glp_gleam/` is **committed,
  hand-authored** source; the codeconv Dart→Gleam mirror's companion/tracker tree (the `.gleam`/
  `.ana`/… companions + `codeconv-gleam-tracker.json` from F2) sits **alongside** and does **not**
  generate the subtree. F3 establishes the subtree as *recognized* at the lightweight level the
  pipeline needs ("recognized + build/test green"); deeper pipeline integration is deferred to the
  heavy port features.
- **Rationale / boundary preserved**: this is exactly F2's plugin boundary (a new pair = a package +
  one registry line + config, zero stage-tool edits). Re-using it keeps F3 additive and stage-tool-
  silent.
- **Alternatives rejected**: teaching any stage tool about a "gleam subtree" in source — violates
  FR-008/SC-006 and F2's boundary; unnecessary because config already carries roots + pair.

## R-005 — Build/test target & environment

- **Decision**: build and test the **Erlang/BEAM target only** (`gleam build --target erlang`,
  `gleam test --target erlang`), under **WSL Ubuntu** with the pinned toolchain. AtomVM-specific
  build/run and the JavaScript target are **out of scope** (spec Assumptions; JS only partially
  viable per F1; AtomVM targeting lands with the heavy runtime features).
- **Rationale**: plain BEAM is F1's proven test runtime; the WSL toolchain is verified reachable
  from this repo (`gleam 1.17.0`, OTP `25`, `rebar3`), so the implement stage can actually produce
  the manifest and a green build/test — the SC-001/SC-002 gate is real, not aspirational.
- **Alternatives rejected**: targeting AtomVM or JS now — out of scope and unneeded for an
  empty-but-building skeleton.

## R-006 — Placeholder-module discipline (empty-but-building)

- **Decision**: each of the 8 placeholder modules is a minimal **but non-dangling** Gleam module:
  a module-doc comment (`////`) naming the subsystem and its Dart source-of-truth path, and **no
  exported definitions yet** (the port fills them in F4+). The single gleeunit smoke test
  (`test/glp_gleam_test.gleam`) provides the ≥1 passing test; it need not import the placeholders
  (they compile as part of the build regardless — edge case "placeholder imported but unused must
  still compile"). If a truly empty `.gleam` file is rejected by `gleam build` (to be confirmed at
  implement time under WSL), the module-doc comment alone makes it valid; if even that is
  insufficient, a single `pub const subsystem = "<name>"` marker is the fallback — decided at
  implement time against the **actual** compiler, not guessed here.
- **Rationale**: "empty-but-building, not empty-and-dangling" (spec edge case). A doc comment keeps
  the file self-describing and maps it to its Dart counterpart, aiding downstream ports, at zero
  semantic cost. Confirming emptiness behavior against the real compiler (not speculation) honors
  the read-first / no-guessing discipline.
- **Alternatives rejected**: (a) leaving files literally empty without first confirming `gleam`
  accepts them — risks a red build; (b) adding stub functions/types now — would be ported-semantics
  creep beyond "empty-but-building".

## Resolved unknowns summary

| Item | Resolution |
|------|------------|
| Project name / namespace | `glp_gleam` project; `src/glp/<subsystem>` modules (R-001) |
| Dependency pinning | committed `manifest.toml` locks 1.0.3 / 1.3.0 / 1.11.0; ranges from F1; `gleam_otp` omitted (R-002) |
| Smoke gate | self-contained WSL `glp_gleam/smoke.sh`; peer gate, referenced from quickstart/README (R-003) |
| FR-008 recognition | config-only via `workspace_settings` + existing `dart_gleam` pair; zero stage-tool edits (R-004) |
| Target / env | Erlang/BEAM only, under WSL; AtomVM/JS out of scope (R-005) |
| Placeholder discipline | doc-comment module per subsystem; emptiness confirmed at implement time (R-006) |

**Owner-awareness flag (non-blocking)**: R-003 — the smoke is a *separate* WSL gate rather than a
literal call inside `test/run_all_tests.sh` (cross-runtime/OS + additive-only reasons). Flagged for
visibility; revisit only if the owner wants an explicit aggregator hook.
