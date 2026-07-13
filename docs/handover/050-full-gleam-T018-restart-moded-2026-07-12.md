# Feature 050-full-gleam-combined — T018 restart note (moded_term + moded_head)

**Date:** 2026-07-12
**Host:** Olamnit (192.168.0.136)
**Status:** In Progress — T018 chunked; next unit = `moded_term` → `moded_head`.
**Supersedes** the *Position* + *Test protocol/environment* sections of
`docs/handover/050-full-gleam-T018-handover-2026-07-12.md` (the porting-conventions
section of that note still applies verbatim).

---

## Session bootstrap (fresh session on Olamnit)

1. Mandatory reading per CLAUDE.md, in order: `CLAUDE.md`, `docs/DISCIPLINE.md`,
   `docs/typed-glp-manual.md`, `docs/glp-cheat-sheet.md`.
2. `git fetch origin && git checkout 050-full-gleam-combined && git rebase origin/050-full-gleam-combined`
   — a **concurrent QUIC session actively pushes** to this same branch; always
   fetch+rebase before working and before each push (our gleam commits are
   additive — new files only — so rebases are clean, no conflicts).
3. Read this note + `specs/050-full-gleam-combined/tasks.md`. Position truth =
   tasks.md checkboxes + the commit trail + the marathon trail.
4. Marathon run = **`mrun-56564f6cdca3`** (Olamnit-local). Resume/position via
   `D:/bstdev/research/buildkit/.venv313/Scripts/buildkit-marathon.exe position`
   (set `PYTHONUTF8=1`).

## Position

T018 (type checker) is being ported in dependency order. **Done:**
- chunk A — mode / TypeEnvironment / prelude — `c00547ee`
- chunk B — param_expansion — `2b56ad71`
- chunk C — program_dfa — `45a98603` (native gleam test 246/246)
- subtyping — `40417a00` (native gleam test 252/252)

**NEXT UNIT = moded_term + moded_head** (paired; moded_head builds on moded_term).
Then, still in T018 dep order: `well_typed_term` + `well_typed_clause` →
`type_environment_builder` → `clause_validation` → `type_checker` checkModule
entry (closes T018). Then T019–T030.

## The next unit — split plan

Port in two sub-chunks, commit + push + verify each:

1. **`moded_term.dart` (500 lines)** → `glp_gleam/src/glp/analysis/type_checker/moded_term.gleam` + tests.
   - Dart imports: only `mode.dart` → `glp/analysis/type_checker/mode` (ported).
2. **`moded_head.dart` (453 lines)** → `glp_gleam/src/glp/analysis/type_checker/moded_head.gleam` + tests.
   - Dart imports: `mode.dart`, `moded_term.dart` (do #1 first), `type_ast.dart`
     (→ `glp/analysis/type_ast`, ported), `../../compiler/ast.dart`
     (→ `glp/parser/ast`, present — same module param_expansion consumes).

All dependency modules already exist in the Gleam subtree.

## Porting conventions (recap — full detail in the base handover)

- **Error/diagnostic strings must be BYTE-IDENTICAL to the Dart oracle**
  (`dart run glp_runtime/bin/glp_repl.dart`; the prebuilt `glp_repl.exe` is
  stale/unbootable). Copy message strings verbatim from the Dart source.
- **Mode mapping:** Dart `Mode.produce`/`Mode.consume` = `mode.Output` (↑) /
  `mode.Input` (↓). Use `mode.flip`.
- **Name-clash trap:** `type_ast` exports `Output`/`Input` (TypeClassification)
  that collide with `mode`'s `Output`/`Input` — import only `mode`'s unqualified
  and qualify `type_ast` constructors (`type_ast.TypeRef(...)`).
- **Dart throw / StateError → Gleam `panic as {msg}`** for invariant violations
  (house style, matches `type_conversion.gleam`); genuine error channels use
  `Result`. Mutable-set/list accumulators are threaded functionally.
- **Reserved keyword:** `auto` is reserved in Gleam — don't use it as a name.
- Commit per sub-chunk, **staged by name** (never `-A`), single-line message.

## Test protocol / environment (Olamnit reality — differs from base handover)

🔴 **Olamnit has NO WSL distro** (`wsl -l -v` = none). Gabi APPROVED running the
suite with **native-Windows gleam 1.17.0 exclusively** (no platform to mix with,
so the base handover's "WSL-only" rule is moot here). `gleam test` recipe
(PowerShell):

```
$env:PATH = "C:\Users\smbuser\AppData\Local\Microsoft\WinGet\Packages\Gleam.Gleam_Microsoft.Winget.Source_8wekyb3d8bbwe;C:\Program Files\Erlang OTP\bin;" + $env:PATH
Set-Location D:\bstdev\research\glp\glpnet\glp_gleam
gleam test
```

Green baseline **before this unit = 252/252**. Pre-existing warnings about empty
placeholder modules (`glp/analysis`, `glp/bytecode`, …) are not ours — ignore.
NEVER run bare `gleam format` (base-handover rule still holds).

## Commit / push flow per sub-chunk

```
# (from glp_gleam) native gleam test → green
git add <the two new files by name>
git commit -m "impl(050): T018 moded_term - moded_term.gleam + tests port (native gleam test N/N)"
git fetch origin 050-full-gleam-combined && git rebase origin/050-full-gleam-combined
git push origin 050-full-gleam-combined
# marathon: trace + resolve the item; update position
```

## Marathon item ids (for resolve/trace)

- moded_term + moded_head: `mitem-019f559f6a68-f2fdbef3-b69d-4a89-85ee-9b0c7b88830f`
- well_typed_term/clause: `mitem-019f559f7270-98e8083c-30d9-4f15-b12a-34975dbeb4b0`
- type_environment_builder: `mitem-019f559f7a32-aa6305b0-3785-447d-b395-4f5cc16089bd`
- clause_validation: `mitem-019f559f8245-9e0ef3e7-0a5a-4708-b371-a2532c0ae160`
- type_checker checkModule: `mitem-019f559f8b71-1d101918-c60f-4f7d-acf0-83e5691853dd`
