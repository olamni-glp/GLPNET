# Instructions for Claude Code — glpnet

## Repo Identity

- **Project**: glpnet — Windows-side workstreams (D2NET, PGLite, Flutter multiagent app) sharing the GLP language with the sibling Mac/Linux GLP repo.
- **Working directory**: `D:\BSTDEV\RESEARCH\glp\glpnet`
- **User**: Gabi (`vonwenm` / `mvonwen@gmail.com`)
- **Branching/versioning**: see `docs/BRANCHING.md` and `docs/VERSIONING.md` (CalVer `vYYYY.MM.DD[-N]` on `main`; feature branches `NNN-short-name`).
- **Sibling repo**: GLP language implementation at `/Users/udi/Grassroots/GLP/` (Mac) or `/home/user/GLP/` (Linux). See appendix at the end of this file for sibling-repo-specific commands and paths.

## 🔴 PGLite data-dir — use the canonical cluster `--data-dir C:/pglite/research/glpnet`

**2026-05-17 (verified, Gabi-approved cleanup):** D: is **NTFS** (`Get-Volume D` → `FileSystem: NTFS`, label `GAVRI_VOL_D`; the prior Lexar/exFAT drive was replaced). The old "the repo is on exFAT, PGLite cannot run here, the bridge crashes mid-migration on `<repo>/.pgdb/`" premise is **VOID** — `<repo>/.pgdb/` on D: now passes the CLI filesystem guard. `docs/known-issues.md` Issue 8 is historical.

For **consistency and to reuse the already-running shared bridge**, every `codeconv` invocation that talks to the bridge SHOULD still pass:

```
codeconv --data-dir C:/pglite/research/glpnet <subcommand> ...
```

`C:\pglite\research\glpnet\` is the canonical shared PGLite cluster for this repo (a healthy bridge runs there — `codeconv doctor` → OVERALL OK). This is now a **convention for consistency, not a filesystem necessity**. The CLI guard (`codeconv.bridge_client._check_data_dir_filesystem`) still refuses non-NTFS/ReFS data-dirs (exit 64) — a safety net that no longer triggers on D:.

---

## 🔴 Start of Every Conversation — Mandatory Reading

Complete these IN ORDER before any other action:

1. **READ** this file (`CLAUDE.md`) to completion → acknowledge "I have read CLAUDE.md completely"
2. **READ** `docs/DISCIPLINE.md` → acknowledge
3. **READ** `docs/typed-glp-manual.md` → acknowledge
4. **READ** `docs/glp-cheat-sheet.md` (compact GLP reference; GLP is **not** Prolog — study the wrong-vs-right examples) → acknowledge
5. **STOP AND WAIT.** Do not read other files until Gabi gives direction.

🔴 **Never program based on ignorance of GLP and its type system.** If the manual and cheat sheet do not answer a question, STOP and report the gap; do not speculate or grope in the dark.

After Gabi gives direction:
- Identify mode (Discussion vs Implementation)
- Ask for current state if unclear
- Read additional specs only as the task demands

---

## 🔴 After Context Compaction

When emerging from compaction (you see a session summary replacing the original conversation), do NOT silently continue. Stop, tell Gabi you have emerged from compaction, summarise where things stand, and ask how to proceed. Never assume the summary is complete or that prior agreements still hold.

---

## 🔴 Working Modes

### Discussion Mode (DEFAULT)

- **NO ACTIONS** — no coding, testing, running commands, or git operations until Gabi explicitly ends the discussion ("discussion over", "let's implement", "go ahead", or similar).
- **"stop" / "wait" means stop immediately.** Do not finish the current action, do not clean up. Just stop. Gabi's direct command overrides hook feedback.
- **Never leave a discussion before Gabi confirms it is finished.** A discussion ends only when Gabi says so, or when you ask "is the discussion finished?" and Gabi says "yes".
- Stay on topic; brief responses; ask clarifying questions; point out inconsistencies; don't agree too quickly — Gabi often refines the design mid-discussion.

### Implementation Mode

- Enter only after explicit signal. Confirm: "Moving to implementation mode."
- Test immediately after each change. Report exactly what changed.
- Complete solutions, not partial victories — fix all related bugs; don't stop at the first successful case.

---

## 🔴 Spec-First Development — No Implementation Without Spec

Before writing **any** code (including actor scripts and demo plays):

1. **Identify** which spec(s) cover this area.
2. **Read and quote** the relevant section verbatim — don't paraphrase.
3. **Verify** the spec is clear and consistent with all other specs covering the area.
4. **If unclear, missing, conflicting, or seemingly incorrect**: STOP. Discuss with Gabi. Clarify or revise the spec FIRST.
5. **Only then implement** — and the implementation must match the spec exactly.

Three possibilities when reviewing code against a spec:
1. Spec is clear → revise code to match spec.
2. Spec is unclear → clarify spec first.
3. Spec seems incorrect → discuss and possibly revise spec before any code work.

**Stop signals — if you find yourself doing any of these, STOP and report:**
- Making code "work" without spec backing
- Adding logic that isn't in the spec
- Fixing something by guessing what behavior should be
- Using try/catch or null checks to "handle" cases the spec doesn't address
- Adding interleaving/race-condition workarounds without a protocol spec

**"Robustness" is often a workaround in disguise.** If a function receives invalid input, the bug is in the caller — fix the caller, don't make the function tolerate bad input.

**Single source of truth.** Each subsystem has ONE authoritative spec; other docs reference it, not duplicate it. Example: `docs/heap/heap-pointer-architecture-spec.md` is authoritative for heap design.

### Reading Specs Correctly

- Quote exactly. Answer only what the spec says. Don't add inferences.
- "Spec section X.Y says: '<verbatim quote>'" — not "the spec is clear, basically what it means is …"
- If the spec is silent on the case: say so, don't fill in.

---

## 🔴 Bug Protocol

When a bug is discovered:

1. **STOP.** No fixes, no workarounds, no alternative approaches.
2. **Identify clearly.** What was expected, what happened, where.
3. **Check the spec.** Is the code violating the spec? Is the spec unclear? Is the spec wrong?
4. **Report and discuss.** Wait for agreement before any action.

### GLP Bug Reporting Format

When a suspected GLP bug is found, report in THIS EXACT FORMAT with no intervening text:

**Failing Goal:**
```
<the goal that fails>
```

**Type and Procedure Declarations:**
```prolog
<relevant type definitions>
<procedure declaration>
```

**Suspected Clause(s):**
```prolog
<the clause(s) that should match but don't>
```

Then STOP and wait. Do not attempt a fix.

### When GLP Behaves Unexpectedly

1. Is the behavior consistent with the spec?
2. If yes → is the spec clear? (If not, improve spec / docs.)
3. If no → it's a bug. Report and discuss; choose between fix-spec, fix-code, or improve-docs.

### Mandatory debugging protocol

For any GLP-program debugging session, read and follow `docs/Mandatory protocol for debugging the GLP implementation with GLP programs.txt`. Do not skip steps; stop and report if any step fails.

---

## 🔴 Code Modification Protocol

- **`.glp` files written by Gabi**: NEVER modify without prior discussion and explicit approval. `.glp` files Claude wrote in the current session can be modified freely.
- **Dart files**: may be modified, but tell Gabi what is changing and why before or as you do it.
- **Before running or tracing GLP code in the REPL**: show which file will be loaded, show the goal, wait for approval (or use pre-approved commands from `.claude/settings.local.json`).
- **When Gabi pastes code/instructions from Claude Web**: review first; raise concerns; never blindly execute; do not exceed scope.
- **Save Claude-Web-provided code exactly as given** — no modifications. Test immediately. If it fails: ask "revert, or consult Claude Web?".

### Language Authority

The GLP language definition — guards, system predicates, body kernels, directives, type-system features, primitive types — **cannot be revised, extended, or added to without explicit discussion with Gabi and his express approval.** Propose first, wait for approval, then implement. See `docs/DISCIPLINE.md` §1.14.

### Preserve Working Code

NEVER remove without explicit approval:
- `_ClauseVar` (HEAD-phase unresolved variables — CRITICAL)
- `_TentativeStruct` (HEAD structure building)
- Fallback cases / edge-condition branches
- Any code you don't fully understand

The current implementation may differ from textbook WAM — respect existing patterns.

### Do Exactly What Is Asked

- Do exactly the task; nothing more. No extra analysis, refactoring, or "while I'm at it" cleanup.
- Never decide on your own not to implement an instructed change.
- Never revert an instructed change without explicit permission.
- Never divert from the instructed task. If blocked: STOP, report, wait.

---

## 🔴 File Verification

Before referencing any file, path, or fact:
1. Verify the file exists (Glob / Read / `ls`)
2. Verify the location — never trust paths from memory
3. Verify the contents — read the actual file
4. List directories before assuming what's in them
5. If you can't verify, say so — never hallucinate paths

### Binary / non-text files

When a file isn't readable in your context window (PDFs, PPTX, large binaries, paths with spaces): **ask Gabi to upload it.** Do not waste time with multiple tools, copy commands, or workarounds.

---

## Communication Style

- **Terse, direct.** No long explanations, no verbose politeness, no apologies-and-promises after mistakes — just acknowledge and move on.
- **Questions to Gabi: at most two sentences. Be concise.**
- **Never ask closed-form questions** (multiple choice, yes/no, pick-from-list, AskUserQuestion). Free-text only when clarification is genuinely needed.
- **One-liner shell commands** when handing commands to Gabi to run — single line, no comments, copy-paste ready.
- **Don't use the word "pattern"** in any paper or document except in the technical sense of pattern-matching. Use a more precise alternative.
- **Showing GLP code**: always include type declarations, procedure declarations, and full clauses; group related definitions in one block; no intervening text between related code blocks.

---

## Test Protocol

🔴 **Always baseline before changing. Always re-test after.**

1. Before any change: run the suite, confirm green, commit & push as a baseline checkpoint.
2. Make the change.
3. Re-run the suite. When green, commit & push.

This gives a known-good baseline, attributes failures correctly, and makes revert trivial.

Local invocation (Windows / glpnet):
```
bash test/run_all_tests.sh
```

REPL test suite is the primary signal — run it before unit tests. If a bug is found and fixed, add a regression test to `test/run_all_tests.sh` (Section A for runtime, B/C for type-check). New typed test programs go in `programs/tests/typed/` and must have `procedure` declarations.

If unified tests fail unexpectedly, common causes:
- **Stale REPL kernel snapshot** at `glp_runtime/.dart_tool/repl.dill` — delete it and re-run.
- Wrong working directory — must run from repo root.

For sibling-repo (Mac/Linux) test invocation, see appendix.

### REPL usage (the unified GLP tool)

There is exactly **one** way to compile, typecheck, and run GLP code: the REPL. Loading a `.glp` file runs the full pipeline (SRSW → partial eval → type check → compile → execute). If it loads, it passed every stage. There are no separate tools — old standalone tools (`check_types.dart`, `glp_pe.dart`, `glpc.dart`) are archived under `glp_runtime/bin/archive/` and must NOT be executed.

Preferred invocation (Gabi's request — remember this):
```
dart run glp_repl.dart
```

For scripted / approval-free use, pipe `echo -e` (NOT heredoc `<<<`, which prompts each time):
```
echo -e 'load programs/path/file.glp\ngoal.' | dart run bin/glp_repl.dart
```

REPL commands: `:trace`, `:debug`, `:limit <n>`, `:quit`. Load file first, then run goals.

Windows pre-built executable: `glp_runtime/glp_repl.exe`.

---

## Git Workflow

### Commit scope and revert discipline

Multiple Claude Code sessions may be working concurrently:

1. **Commit only files you worked on this session.** No `git add -A` / `git add .`. Stage by name to avoid sweeping in another session's WIP.
2. **Never revert, reset, or undo commits without Gabi's express permission.** No `git reset`, `git revert`, `git checkout -- <file>`, or `git restore` on files you didn't modify this session. Undoing your own current-session change is fine.
3. **Merge conflicts or unexpected changes from other sessions** → STOP and report. Do not resolve silently.

### Single-line commit messages

Multi-line `-m` arguments confuse the shell. Always:
```
git commit -m "Fix Channel definition to match prelude"
```

### Branch rules

- `main` is the source of truth — only Gabi merges into it.
- Each Claude Code session works on its own branch (`claude/...-<session-id>` or feature branch like `006-d2net-init-skill`).
- Each session can only push to its own branch (HTTP 403 otherwise) but can pull from any branch.

### Baseline checkpoint before risky work

```
git status
git log -1 --oneline
bash test/run_all_tests.sh
git add -A && git commit -m "Checkpoint: before attempting X"
```

### End-of-task: always offer fetch / merge / push

When a task is committed and pushed, give Gabi the merge template with **the actual branch name substituted** and the `cd` step included:

```
cd D:\BSTDEV\RESEARCH\glp\glpnet
git checkout main
git pull origin main
git fetch origin <ACTUAL-BRANCH-NAME>
git merge -m "Merge <ACTUAL-BRANCH-NAME> into main" origin/<ACTUAL-BRANCH-NAME>
git push origin main
```

Common issues:
- *"not something we can merge"* → run the `git fetch` step first.
- *"refusing to merge unrelated histories"* → add `--allow-unrelated-histories`.
- Merge conflicts → resolve, `git add -A`, commit, push.
- Divergent branches → `git pull origin main --no-rebase`.

For sibling-repo (Mac) merge paths, see appendix.

---

## Multi-Stage Task Persistence

When starting a 3+ step effort, write the plan to `docs/current_plan.md`:

```markdown
# Current Plan: <Task Name>

Started: YYYY-MM-DD

## Steps
- [x] 1. Done step
- [ ] 2. Current step ← CURRENT
- [ ] 3. Next step

## Context
<brief description of what & why>
```

Update as you go. Delete when done. **At session start, check whether `docs/current_plan.md` exists** and, if so, resume from the current step.

---

## maGLP Development Constraints

When working on maGLP (multi-agent GLP) code:
- Modify only `glp_runtime/lib/multiagent/` and `glp_runtime/test/multiagent/`.
- **Do not modify core GLP files** (`runner.dart`, `heap_fcp.dart`, `compiler/`, etc.) without explicit discussion and approval.
- If a core-GLP bug is blocking maGLP work, STOP and report — no workarounds.

---

## Flutter Multiagent App Build Process

When changes to `glp_runtime` need to land in `glp_multiagent` (path-dependency in `pubspec.yaml`), do a clean rebuild — caches will otherwise miss the changes:

```
cd glp_multiagent
flutter clean
flutter pub get
flutter build windows   # or build macos on Mac
```

Verify the build timestamp matches your changes. Logs go to a per-platform trace file (on Mac: `/private/tmp/glp_multiagent_trace.log`).

**Common mistake**: `flutter build` without `flutter clean` uses cached deps and silently misses `glp_runtime` changes.

---

## #remember Directive

When Gabi says `#remember <something>`, add that information to this file so it persists across sessions. When you figure something out after multiple tries (paths, commands, environment quirks), add it here too — future sessions shouldn't repeat the trial-and-error. Pre-approved commands accumulated during a session go in `.claude/settings.local.json`.

---

## GLP Quick Reference

### Core constraints

- **SRSW** (Single-Reader / Single-Writer): each variable occurs at most once per clause. **Mandatory** — never invent or use a `skipSRSW` option.
- **Anonymous `_`**: a writer that nobody reads. Exempt from SRSW. Use in abort clauses where the result is never bound.
- **Three-phase execution**: HEAD (tentative unification) → GUARDS (pure tests) → BODY (mutations).
- **Suspension**: goals suspend on unbound readers, reactivate when writers bind.
- **Writer MGU**: only binds writers, never readers; never binds writer to writer.
- **Three-valued unification**: Success (σ̂w extended/verified) | Suspend (unbound reader, add to Si/U) | Fail (mismatch).

### Runtime architecture

- `RunnerContext`: holds `clauseVars`, `sigmaHat`, `si`, `U`.
- `BytecodeRunner`: executes bytecode.
- `_TentativeStruct`: HEAD-phase structure building.
- `_ClauseVar`: HEAD-phase unresolved variables (**critical — do not remove**).
- Structure completion tracked by `argsProcessed >= structureArity`.

### Bytecode disassembler

`udi/dump_bytecode.dart` (in sibling GLP repo on Mac) compiles a `.glp` file and emits opcode disassembly with PC addresses. Useful for debugging compilation, opcode sequences, mode conversions, HEAD/GUARD/BODY placement.

### Known limitations

- **`=..` not allowed in clause bodies** (parser bug). Works in clause heads only.
- **Structs inside lists in REPL goals fail**: `distribute_indexed([send(1,a), send(2,b)], Y, Z).` errors with "Unsupported list head type: StructTerm". Simple lists, nested lists, and variables-in-lists work; struct elements don't. Location: `glp_repl.dart` `_buildListTermForConj` / `_buildListTerm`.

See `docs/known-issues.md` for the full list.

---

## Directory Structure (glpnet)

```
D:\BSTDEV\RESEARCH\glp\glpnet\
├── CLAUDE.md                     # ← This file
├── docs/                         # Specs, manuals, handovers, BRANCHING/VERSIONING
│   ├── DISCIPLINE.md             # ← MANDATORY reading
│   ├── typed-glp-manual.md       # ← MANDATORY reading
│   ├── glp-cheat-sheet.md        # ← MANDATORY reading
│   ├── glp-bytecode-v216-complete.md
│   ├── glp-runtime-spec.txt
│   ├── current_plan.md           # ← Multi-stage task plans
│   ├── known-issues.md
│   └── research/                 # Deep-dive investigations & reference artifacts
├── glp_runtime/                  # Dart project (REPL + runtime)
│   ├── lib/{bytecode,compiler,runtime,multiagent}/
│   ├── bin/glp_repl.dart
│   └── glp_repl.exe              # Windows pre-built REPL
├── glp_multiagent/               # Flutter multi-agent app
├── glp_runtime_net/              # Dart subtree codeconv inventories (FR-018)
├── programs/                     # All `.glp` source (single source of truth)
│   ├── self.glp                  # Root prelude
│   ├── book/, tests/, lib/, archive/, misc/
├── prereq-patterns/pglite/       # Canonical PGLite bridge — source of truth
│   ├── pglite_bridge.mjs         #   live deployment file (FR-012)
│   ├── package.json              #   node deps (proper-lockfile, pg, pglite)
│   └── tests/*.test.mjs          #   node --test cross-process lock tests
├── codeconv/                     # Python harness (DBOS-on-PGLite over .pgdb/) — the ONE toolchain
│   ├── pyproject.toml
│   ├── src/codeconv/{cli,runner,bridge_client,db}/
│   ├── src/codeconv/langpairs/{__init__,base,dart_csharp}/   # pluggable source→target pairs (feature 016)
│   ├── src/codeconv/tools/{discover,depgraph,init,scaffold}/ # auto-discovered tool subpackages
│   └── tests/                    # pytest — bridge + engine + discover + depgraph + init + scaffold
├── .codeconv/tombstones/         # Inventoried .dart files (checked in, FR-029)
├── .pgdb/                        # Unified PGLite cluster (gitignored)
├── .pgdb.bridge.lock/            # Bridge OS lock (sibling to .pgdb/, gitignored)
├── test/run_all_tests.sh         # Unified REPL test suite (GLP)
└── specs/                        # Feature workstreams (012-codeconv-runner et al.)
```

**GLP code location policy**: all `.glp` source lives under `programs/`. Paper repos (SGLP, CGLP, etc.) may reference paths but must not contain copies — single source of truth.

### Migration to unified bridge (feature 012, 2026-05)

The repo now has ONE PGLite deployment at `<repo>/.pgdb/`, guarded by an OS-level cross-process lock at the sibling path `<repo>/.pgdb.bridge.lock/`. Every PGLite consumer — the Python `codeconv` tools (`discover`, `depgraph`, `init`, `scaffold`) and future tools — auto-spawns or discovers the bridge via the protocol in `specs/012-codeconv-runner/contracts/bridge_lifecycle.md`. The bridge script `prereq-patterns/pglite/pglite_bridge.mjs` is the live deployment, not a template; do not copy it into a feature working tree (former behaviour from feature 011). Schemas inside `.pgdb/`: `public` (legacy D2NET tables, left in place, unconsulted), `dbos` (DBOS runtime), `codeconv` (the inventory + the de-branded workspace tables — `workspace_settings`, `excluded_directories`, `phase_sequence`, `phase_status` — added by feature 016 migration `0003`).

### One toolchain (feature 016, 2026-05)

The legacy D2NET .NET toolchain (`tools/d2net/` — `D2Net.Init`, `D2Net.Scaffold`, `D2Net.PgdbMigrate`, `D2Net.BridgeClient`, `D2Net.sln`) and the `D2NET-init` / `D2NET-scaffold` / `D2NET-pgdb-migrate` skills were **removed** (not forked — git history is the archive). Their load-bearing functionality is now `codeconv init` / `codeconv scaffold` (skills `/codeconv-init` / `/codeconv-scaffold`), behind a pluggable language-pair registry (`codeconv/src/codeconv/langpairs/`; production pair Dart→C#). `D2Net.BridgeClient` is retired — every tool reuses the shared `codeconv.bridge_client`. The one-shot legacy `.D2NET/pgdb/` → `.pgdb/` migration (formerly `D2Net.PgdbMigrate`) is historically complete and intentionally **not** ported (a no-op after first success; D1/D2). There is exactly one conversion toolchain — the `codeconv` CLI.

---

## Error Response Template

When something fails:

```
The operation failed:

[Complete error message]

Test status: X/<total> REPL tests, Y/<total> Dart tests

Likely cause: [brief]

Options:
1. Revert (recommended if tests were green before)
2. Consult Claude Web for design
3. Minimal targeted fix (only if cause is clear)

What would you like?
```

---

## Efficiency

- Don't create temp `.dart` files to inspect bytecode when reading code suffices.
- Don't write throwaway test files when an existing test suite or REPL invocation does the job.
- Make forward progress autonomously when the path is clear; don't ask "should I continue?" on obvious next steps.
- Only create files that are permanent additions.

---

## Appendix: Reference — Sibling GLP Repo (Mac/Linux)

When working in `/Users/udi/Grassroots/GLP/` (Mac) or `/home/user/GLP/` (Linux). Paths and commands here are **specific to that repo**, not glpnet.

### Environments

| Environment | GLP path | Dart binary |
|---|---|---|
| Mac | `/Users/udi/Grassroots/GLP` | `/opt/homebrew/bin/dart` |
| Linux | `/home/user/GLP` | `/home/user/dart-sdk/bin/dart` |

Detect by checking whether `/Users/udi/Grassroots/GLP` exists. Then `export PATH="…:$PATH"` and `dart --version` to verify.

### Dart install (Linux only, when needed)

Project requires Dart SDK ^3.9.4; use 3.10.1 or later:

```
cd /home/user && curl -L -o dart-sdk.zip "https://storage.googleapis.com/dart-archive/channels/stable/release/3.10.1/sdk/dartsdk-linux-x64-release.zip" && unzip -o dart-sdk.zip && rm dart-sdk.zip
export PATH="/home/user/dart-sdk/bin:$PATH"
dart --version
```

Doesn't work on this Linux environment: `curl … | sh` (403), `apt-get install dart`, `busybox unzip`, Dart ≤ 3.2.0. Also missing: `timeout`, `tail`, `head`, `grep`.

### Reference repos cloned on session start

- **FCP** (Flat Concurrent Prolog): `git clone --depth 1 https://github.com/EShapiro2/FCP.git /tmp/FCP`. Authoritative release at `/tmp/FCP/Savannah`. Term-syntax docs at `/tmp/FCP/Savannah/efcp/Logix/CONSTANTS.txt`.
- **Art-of-GLP-2025**: `git clone --depth 1 https://github.com/EShapiro2/Art-of-GLP-2025.git /tmp/Art-of-GLP-2025`. Main file `/tmp/Art-of-GLP-2025/main_AofGLP.tex`.

GitHub directory zip URL template:
```
https://download-directory.github.io/?url=https://github.com/EShapiro2/GLP/tree/<BRANCH>/<path>
```

### Sibling-repo test suites

| Suite | Location | Tests | Purpose |
|---|---|---|---|
| Unified | `test/run_all_tests.sh` | 384 | All REPL-based (runtime + type-check + negative + modules) |
| Book | `test/run_book_tests.sh` | 141 | Book-example compile check |
| Dart | `glp_runtime/test/` | 374 | Unit tests (14 known failures, 5 skipped) |

Unified suite sections: A=runtime, B=positive type-check, C=negative type, D=SRSW violations, E=invalid guard, F=CSSG modules, G=Social Graph, H=CSSN plays 1–12.

Expected: Unified 384/384, Book 84/141 (57 SRSW failures in book code), Dart 374 + 14 known fails + 5 skipped.

### Sibling-repo merge instructions (Mac)

```
cd /Users/udi/Grassroots/GLP
git checkout main
git pull origin main
git fetch origin claude/<ACTUAL-BRANCH-NAME>
git merge -m "Merge claude/<ACTUAL-BRANCH-NAME> into main" origin/claude/<ACTUAL-BRANCH-NAME>
git push origin main
```

### Bonds plays (sibling repo)

`programs/typed_book/bonds/` — NOT in `run_all_tests.sh`. Plays are `fplay1-6, fplay4b, fplay8-12` (no `fplay7`). Play 12 needs `play12/` actor sub-modules and `:limit 5000000`.

```
BONDS=/Users/udi/Grassroots/GLP/programs/typed_book/bonds
printf 'load $BONDS/agent.glp\nload $BONDS/mediator.glp\nload $BONDS/actors.glp\nload $BONDS/boot.glp\n:limit 1000000\nfplay1.\n' | dart run bin/glp_repl.dart
```

Expected outcomes: `→ succeeds` or `→ suspended` (suspended is normal for plays with escrow timers: 3, 4, 4b, 12). Do NOT load the bonds directory as a project — load files individually.

### GrassrootsApp testing framework

See `docs/grassroots-testing-framework.md`. Theater-style: agents (from the GLP paper) + actors (scripted users) + plays (test scenarios in `GrassrootsApp/plays/`). Key files: `GrassrootsApp/glp/agent.glp`, `GrassrootsApp/glp/network.glp`, `GrassrootsApp/plays/play01_cold_call/`.

### Research sources (sibling repo)

1. `docs/glp-bytecode-v216-complete.md` — instruction set (NORMATIVE)
2. `docs/glp-runtime-spec.txt` — runtime architecture (NORMATIVE)
3. `docs/typed-glp-manual.md` — programming patterns (MANDATORY)
4. CSSN Group spec: `/Users/udi/Grassroots/SGLP/docs/group-glp-implementation-spec.md`
5. WAM paper: `docs/wam.pdf`
6. GLP paper source: `/tmp/GLP-2025/main GLP 2025.tex`
7. FCP paper: `docs/1-s2.0-0743106689900113-main.pdf`. FCP source: `/Users/udi/Dropbox/Concurrent Prolog/FCP/Savannah`. Mirror: https://github.com/EShapiro2/FCP

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan:
- specs/018-codeconv-builder/plan.md
<!-- SPECKIT END -->
