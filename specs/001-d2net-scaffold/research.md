# Phase 0 Research — d2net-scaffold

**Feature**: `001-d2net-scaffold` — see [spec.md](spec.md) and [plan.md](plan.md)

All Technical Context items in `plan.md` are concrete (no `NEEDS CLARIFICATION` markers remain after `/speckit-clarify`). This document records the rationale behind the technology choices so they can be revisited when sibling d2net tools are added.

---

## R1 — Implementation language for the toolkit

**Decision**: C# on .NET 8 (LTS).

**Rationale**:
- The toolkit's purpose is to *bootstrap a .NET port*. Implementing the toolkit itself in .NET aligns the toolchain with the target ecosystem and lets future d2net tools (porter, verifier, status dashboard) share code.
- .NET 8 is the current LTS, supported through 2026-11. It ships first-class cross-platform `System.IO`, `System.Text.Json`, and `System.CommandLine`, covering every capability the spec demands without third-party packages.
- The host environment (this repo) is Windows 11 with PowerShell, but .NET 8 runs cross-platform; no Windows-only APIs are introduced.

**Alternatives considered**:
- **Dart**: would dovetail with the existing `glp_runtime` build, but the tool is throw-away meta-tooling for the .NET port — the dependency would point the wrong direction (Dart depending on the port effort instead of the port effort standing on its own).
- **PowerShell script**: zero install on the host but poor portability and no static typing for the tracker schema. Rejected as too brittle for a tool whose output every downstream tool will consume.
- **Python**: portable and quick to write but adds a third runtime to the project (Dart + .NET + Python). Rejected on YAGNI grounds.

---

## R2 — CLI parsing library

**Decision**: `System.CommandLine` (Microsoft, currently in pre-release but widely used; pin a stable beta).

**Rationale**:
- First-party Microsoft library, lightweight, supports POSIX-style flags (`--refresh`), positional arguments (`<source>`, `<target>`), automatic `--help`, and clean exit-code handling.
- No third-party dependency footprint.

**Alternatives considered**:
- **CommandLineParser** (third-party NuGet): mature but adds a dep that pre-release `System.CommandLine` does not need.
- **Manual `args[]` parsing**: works for two positional args plus one flag, but fragile and ugly for `--help`/`--version`. Not worth the saving for a tool people will actually invoke.

---

## R3 — Directory walking & pruning

**Decision**: `System.IO.Directory.EnumerateDirectories` / `EnumerateFiles` with manual recursion. Pruning is by directory **name** match against a hard-coded set `{".dart_tool", "build", ".git", ".idea", ".vscode"}` at any depth (per FR-002 and the Q1 clarification answer).

**Rationale**:
- Manual recursion lets us short-circuit pruned directories cleanly; built-in `EnumerationOptions.RecurseSubdirectories = true` would still descend into excluded folders before filtering.
- Name-based match (not path-based, not glob) matches the spec exactly and is trivial to reason about.

**Alternatives considered**:
- **Microsoft.Extensions.FileSystemGlobbing**: glob support is overkill for a five-name closed list and adds complexity to error reporting.
- **Reading `.gitignore`**: was option E in the Q1 clarification — rejected by the user, who chose the explicit closed list.

---

## R4 — Tracker JSON schema & writer

**Decision**: `System.Text.Json.JsonSerializer` with `WriteIndented = true` and a typed record:

```csharp
public sealed record TrackerRecord(
    string SourceRelPath,                       // forward-slash relative path of the .dart.src in the target tree
    Dictionary<string, string> Companions);     // key = extension without dot ("cs","ana",...), value = status ("todo")
```

The top-level document is a JSON array of `TrackerRecord` (no wrapper object), per FR-007 and FR-008.

**Rationale**:
- `System.Text.Json` is in the BCL (no NuGet), supports records natively, and produces stable indented output that diffs cleanly under git.
- A flat array of records is the smallest shape that satisfies "one record per Dart source file" without inviting incidental metadata fields that would later need to be specified.
- Forward-slash paths in the JSON are platform-portable; the implementation converts native `Path.DirectorySeparatorChar` to `'/'` on write.

**Alternatives considered**:
- **Newtonsoft.Json**: extra dep, not needed.
- **Wrapper object** (`{"version":1,"records":[…]}`): future-proofs the file but adds shape that no consumer asked for in the MVP. Rejected; can be added later in a v2 with a backwards-compatible reader.
- **Per-companion array of objects** instead of a dictionary: more verbose, no concrete benefit. Rejected.

---

## R5 — Companion stub TODO comment format

**Decision**: Each of the nine companion files receives a single line:
```
// TODO: d2net — port from <basename>.dart.src (artifact: <ext>)
```
where `<basename>` is the original Dart filename without `.dart` and `<ext>` is one of `cs`, `ana`, `tst`, `con`, `dep`, `cgn`, `iss`, `sta`, `ver`. This is a valid C-style single-line comment, consistent with the user's request for "a todo .cs comment", applied uniformly even though only `.cs` is literally a C# file.

**Rationale**:
- One line keeps each stub minimal; readers can grep for `// TODO: d2net` to find unstarted work.
- Naming the source file inside the stub means the stub is self-explanatory if someone opens it without the tracker open.
- `//` comments are accepted by C#, JS, and most C-family ecosystems; even the non-source extensions (`.ana`, `.dep`, etc.) will be opened in editors that highlight `//` as a comment line.

**Alternatives considered**:
- **Empty file**: no signal that the file is intentional scaffolding; risks being mistaken for an accidental file.
- **Different per-extension comment**: tempting but fights uniformity; the spec explicitly allowed one comment style across all nine.

---

## R6 — Atomicity on collision-failure (FR-012)

**Decision**: Two-phase execution. Phase 1 (pre-flight) is read-only over the source tree, accumulates collisions and any other refusals, and either returns a list of problems or proceeds. Phase 2 (write) is invoked only after Phase 1 returns clean, and only then are directories created and files written. This satisfies "leaves the target tree unchanged on collision-failure" without needing rollback logic.

**Rationale**:
- Pre-flight is cheap: a single recursive scan over file names that, in passing, also builds the work plan for Phase 2.
- Avoids the need for any reversal or temp-staging.

**Alternatives considered**:
- **Write to a temp sibling and atomic rename on success**: solid but heavier than necessary for an MVP and makes `--refresh` (which writes into an existing target) awkward.
- **Write-and-rollback**: complex to implement correctly across crashes; explicitly rejected.

---

## R7 — `--refresh` semantics

**Decision**: As clarified in spec Q5 (option C). Implementation reuses the same DirectoryWalker and FileCopier; the only behavioural differences are:
- `CompanionFileWriter` checks `File.Exists(target)` and skips if true (never overwrites).
- `TrackerWriter` is bypassed entirely when `--refresh` is set.
- A separate counter tracks "newly-discovered Dart files for which a companion stub set was created but no tracker entry was added"; this is reported in the run summary so the user can update the tracker manually (per FR-011 (f)).

**Rationale**:
- Reusing the same components keeps `--refresh` from drifting in behaviour from the fresh path.
- Out-of-scope tracker updates are surfaced loudly so they don't go unnoticed.

**Alternatives considered**:
- **Auto-extending the tracker on `--refresh`**: was option D in the Q5 clarification — rejected by the user.

---

## R8 — Test approach

**Decision**: Integration-test-heavy with thin unit tests. Each integration test in `D2Net.Scaffold.Tests`:
1. Builds a small fixture source tree under `Path.GetTempPath()/d2net-scaffold-tests/<guid>/source`.
2. Runs `ScaffoldRunner` against `…/<guid>/target`.
3. Asserts on the resulting target tree (file existence, content, byte-for-byte equality, tracker JSON shape).
4. Cleans up in `IDisposable.Dispose`.

**Rationale**:
- The behaviour the spec describes is observable filesystem outcomes; testing those directly is more meaningful than mocking `IFileSystem`.
- Fixtures are small (10–30 files each) and run in milliseconds; full suite stays under a second.

**Alternatives considered**:
- **Mock filesystem with `System.IO.Abstractions`**: extra dep, less faithful to the real I/O paths the tool will exercise. Rejected for this scope.
- **Property-based tests** (FsCheck): overkill for the deterministic behaviours we're verifying.

---

## R9 — Stdout summary format (FR-013)

**Decision**: Plain human-readable English on stdout, one summary section, no escape codes. Example:

```
d2net-scaffold: scaffold complete.
  Source: D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime
  Target: D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net
  Directories created : 142
  Non-Dart files copied: 318
  .dart.src files     : 207
  Companion stubs     : 1863  (= 207 × 9)
  Tracker records     : 207
  Tracker file         : D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net\d2net-tracker.json
  Pruned directories   : .dart_tool, build, .git, .idea, .vscode
```

**Rationale**:
- Matches Q4's clarification answer (stdout, no separate report file).
- Keeps numeric fields aligned for visual scan; CI logs render fine.

**Alternatives considered**:
- **JSON on stdout**: was option B in Q4 — rejected by the user.
- **Quiet on success**: was option E in Q4 — rejected.

---

## Summary of resolved unknowns

| ID | Topic | Status |
|----|-------|--------|
| R1 | Implementation language | Resolved → C#/.NET 8 |
| R2 | CLI library | Resolved → `System.CommandLine` |
| R3 | Directory walk & pruning | Resolved → manual recursion + name set |
| R4 | Tracker schema & serializer | Resolved → `System.Text.Json` + flat array of records |
| R5 | Stub TODO comment format | Resolved → single-line `// TODO: d2net …` |
| R6 | Atomicity on failure | Resolved → two-phase (pre-flight then write) |
| R7 | `--refresh` semantics | Resolved → reuse pipeline, skip-existing companions, no tracker write |
| R8 | Test approach | Resolved → xUnit integration tests over temp fixtures |
| R9 | Stdout summary format | Resolved → human-readable, aligned columns |

No `NEEDS CLARIFICATION` items remain.
