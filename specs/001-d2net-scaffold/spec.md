# Feature Specification: d2net-scaffold — Dart-to-.NET Conversion Scaffold

**Feature Branch**: `001-d2net-scaffold`
**Created**: 2026-04-30
**Status**: Draft
**Input**: User description: "create an MVP code conversion toolkit with d2net-scaffold creating a scaffold copy of GLPNET\glp_runtime in GLPNET\glp_runtime_net … copy all folders from source to target by traversing the folder tree in order and in each folder copy all files from source to target exactly except any dart file. each Dart file must be copied but you must add .src to its file name after the .dart file extension … then create 9 new files for each such .dart file where the .dart file extension is replaced with .cs, .ana, .tst, .con, .dep, .cgn, .iss, .sta, .ver and put a todo .cs comment into it. Also create a json based directory of all dart files and their new code conversion files (.cs, .dep, .ana, etc.) and their current status, with one tracking entry per .dart src file, in one file where these tracking records are in an array in a json tracker file in the root of glp_runtime_net."

## Clarifications

### Session 2026-04-30

- Q: Should the scaffold mirror the source tree literally (including `.dart_tool/`, `build/`, `.git/`, IDE folders) or exclude well-known build/cache/VCS directories? → A: Skip well-known build/cache/VCS directories by default: `.dart_tool`, `build`, `.git`, `.idea`, `.vscode`.
- Q: When a generated companion file (e.g. `runner.cs`) would collide with a pre-existing non-Dart file of the same name in the source folder, what should the tool do? → A: Pre-flight check — scan the whole source tree first, report all collisions, abort with non-zero exit, and create nothing in the target.
- Q: What should the tracker file be named, and what status values may a companion-file status field take? → A: Filename `d2net-tracker.json` at the root of the target tree; allowed status values are exactly `todo`, `in-progress`, `done`, `blocked` (a closed enum).
- Q: Where should the end-of-run summary report from FR-013 be written? → A: Human-readable summary on stdout only; no separate report file in the MVP.
- Q: When the toolkit is re-run with the override flag against an existing target, what is refreshed and what is preserved? → A: Refresh pruned-dir state, non-Dart files, and `.dart.src` files from the current source; never touch existing companion files (`.cs`, `.ana`, `.tst`, `.con`, `.dep`, `.cgn`, `.iss`, `.sta`, `.ver`) and never touch `d2net-tracker.json`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One-shot scaffold of glp_runtime into glp_runtime_net (Priority: P1)

A developer who is preparing to port the GLP Dart runtime to .NET runs `d2net-scaffold` once. The tool walks the entire `glp_runtime` source tree and produces a parallel `glp_runtime_net` tree where the directory layout is mirrored exactly, every non-Dart file is byte-identical to its source counterpart, every original `.dart` file is preserved alongside nine empty stub files (one per target conversion artifact), and a single JSON tracker at the root of the target tree lists every Dart file and the status of each of its nine companion artifacts. After this run, the developer has a complete working scaffold from which the .NET port can begin, file-by-file, with progress visible at a glance.

**Why this priority**: This is the entire MVP. Without this single user journey nothing else in the toolkit has any value. Every other feature listed below is an enhancement to this one workflow.

**Independent Test**: Point the tool at `GLPNET\glp_runtime` with target `GLPNET\glp_runtime_net`. Verify (a) every directory under the source has a matching directory in the target, (b) every non-Dart file in the source has a byte-identical copy at the corresponding target path, (c) every `.dart` file in the source has a `.dart.src` copy at the corresponding target path with identical content, (d) every `.dart` file in the source has nine stub companion files in the same target folder named with extensions `.cs`, `.ana`, `.tst`, `.con`, `.dep`, `.cgn`, `.iss`, `.sta`, `.ver`, each containing a TODO C-style comment, and (e) the JSON tracker file at the root of `glp_runtime_net` contains one record per Dart source file listing all nine companion files and their current status. Delivers value the moment the scaffold exists — porting work can begin against it.

**Acceptance Scenarios**:

1. **Given** `glp_runtime` exists with a mix of Dart and non-Dart files at multiple folder depths and `glp_runtime_net` does not exist, **When** the developer runs `d2net-scaffold` with source `glp_runtime` and target `glp_runtime_net`, **Then** `glp_runtime_net` is created with the same folder structure, every non-Dart file is copied verbatim, every `.dart` file is preserved as `<name>.dart.src`, nine companion stubs are created per Dart file with the specified extensions, and a JSON tracker file at the root of `glp_runtime_net` contains one entry per Dart source file.
2. **Given** a source folder contains a file named `runner.dart` plus a subfolder, **When** the scaffold completes, **Then** the corresponding target folder contains `runner.dart.src` plus `runner.cs`, `runner.ana`, `runner.tst`, `runner.con`, `runner.dep`, `runner.cgn`, `runner.iss`, `runner.sta`, `runner.ver` — each containing a TODO C-style comment — and the matching subfolder is recursively scaffolded the same way.
3. **Given** a source folder contains a file named `pubspec.yaml`, **When** the scaffold completes, **Then** the corresponding target folder contains `pubspec.yaml` with byte-identical content to the source.
4. **Given** the scaffold has run to completion, **When** the developer opens the JSON tracker file at the root of `glp_runtime_net`, **Then** they see a JSON document with an array of records, one per Dart source file, where each record identifies the Dart source path and lists all nine companion artifact files with a status of "todo".

---

### User Story 2 - JSON tracker as the single source of truth for porting progress (Priority: P2)

As the developer ports each Dart file to .NET, the JSON tracker at the root of `glp_runtime_net` provides a single, machine-readable inventory of every Dart source file and the status of each conversion artifact (`.cs`, `.ana`, `.tst`, `.con`, `.dep`, `.cgn`, `.iss`, `.sta`, `.ver`). The tracker is initialised by the scaffold step and is intended to be read and updated by later toolchain steps and dashboards.

**Why this priority**: The tracker is generated as part of the P1 scaffold run, but its real value (progress visibility, querying "which files still have .cs in todo state?") is realised in subsequent workflow steps. P2 reflects that the scaffold output must be well-formed enough to support those downstream uses, even though no downstream tool is in this MVP.

**Independent Test**: After P1 completes, validate the tracker file: it parses as JSON; the top level contains a single array of records; the array length equals the count of `.dart` files in the source tree; every record references a real `.dart.src` file in the target; every record lists exactly nine companion files with the correct extensions; every status field is a recognised value (initially "todo").

**Acceptance Scenarios**:

1. **Given** the scaffold has produced 200 `.dart.src` files in the target, **When** the developer reads the tracker file, **Then** the records array has exactly 200 entries.
2. **Given** the tracker file, **When** any record is inspected, **Then** it contains a stable identifier for the Dart source file (path relative to the target root), and a list of nine companion entries each with file name and status.

---

### User Story 3 - Re-running the scaffold against an existing target is safe and predictable (Priority: P3)

A developer who has already scaffolded `glp_runtime_net` and started porting work re-runs `d2net-scaffold`. The tool refuses to silently destroy in-progress conversion work and either aborts with a clear message or operates only in a documented additive mode.

**Why this priority**: Important for safety in real use, but not required for the first successful scaffold run that delivers MVP value. Without this safety net the tool is still useful; with it, the tool is safe to use repeatedly during ongoing porting work.

**Independent Test**: Run the scaffold once. Edit one of the generated `.cs` files to contain real C# code. Run the scaffold a second time. Confirm the edited `.cs` file is not overwritten without an explicit override, and the tool reports clearly what it would have done.

**Acceptance Scenarios**:

1. **Given** `glp_runtime_net` already exists from a prior run, **When** the developer re-runs the scaffold without any override flag, **Then** the tool reports that the target already exists and exits without modifying any file.
2. **Given** `glp_runtime_net` already exists with hand-edited companion files (e.g. real C# code in some `.cs` files) and the source has had a few `.dart` files edited and one new `.dart` file added, **When** the developer re-runs the scaffold with the `--refresh` flag, **Then** every `.dart.src` file is rewritten from the current source, every non-Dart file is rewritten verbatim, the new `.dart` file's nine companion stubs are created, every previously-existing companion file is left untouched (including the hand-edited `.cs` files), `d2net-tracker.json` is left untouched, and the run summary lists the newly-discovered Dart files so the developer can update the tracker.

---

### Edge Cases

- **Empty source folders**: An empty directory in `glp_runtime` produces an empty directory at the same relative path in `glp_runtime_net`.
- **Dart filenames with multiple dots** (e.g. `foo.bar.dart`): The preserved copy is `foo.bar.dart.src`; the nine companion files replace the trailing `.dart` only and are named `foo.bar.cs`, `foo.bar.ana`, etc.
- **Filename collisions on companion extensions**: If the source folder already contains a non-Dart file named, e.g., `runner.cs` next to `runner.dart`, the scaffold step would otherwise generate a stub `runner.cs` for the Dart file. The toolkit detects every such collision in a pre-flight pass and aborts with a non-zero exit before creating any target output (FR-012); the target tree is untouched on collision-failure.
- **Non-Dart files with unusual encodings or binary content** (images, PDFs, lockfiles): All non-Dart files are copied as raw bytes, regardless of content.
- **Symbolic links and OS metadata files** (`.DS_Store`, `Thumbs.db`): Treated like any other non-Dart file; the scaffold does not specially exclude or follow links beyond what a directory walk produces. The default-excluded directories (`.dart_tool`, `build`, `.git`, `.idea`, `.vscode`) are pruned at the directory level — see FR-002.
- **Source path does not exist** or **target path is inside source path**: The tool refuses to run and reports the misconfiguration.
- **Target path partially exists** (e.g. only the root folder): The tool treats it the same as User Story 3 — refuse without override.
- **Tracker file already exists** at the target root from a prior run: Treated identically to existing target (User Story 3).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The toolkit MUST accept a source directory path and a target directory path as inputs.
- **FR-002**: The toolkit MUST traverse the source directory recursively in deterministic order and reproduce the directory hierarchy at the target path, skipping any directory whose name matches one of the default-excluded names (`.dart_tool`, `build`, `.git`, `.idea`, `.vscode`) at any depth. Excluded directories MUST NOT appear in the target tree at all (no empty placeholder), and any files contained within them MUST NOT be processed.
- **FR-003**: For every non-Dart file in the source tree, the toolkit MUST copy that file to the corresponding relative path in the target tree with byte-for-byte identical content.
- **FR-004**: For every file in the source tree whose name ends in `.dart`, the toolkit MUST place a copy of that file at the corresponding relative path in the target tree, with the suffix `.src` appended after the `.dart` extension (so `foo.dart` becomes `foo.dart.src`), and with byte-for-byte identical content to the source file.
- **FR-005**: For every Dart source file processed under FR-004, the toolkit MUST create nine additional files in the same target folder, named by replacing the trailing `.dart` of the original filename with each of the following extensions: `.cs`, `.ana`, `.tst`, `.con`, `.dep`, `.cgn`, `.iss`, `.sta`, `.ver`.
- **FR-006**: Each of the nine companion files generated under FR-005 MUST contain a single C-style TODO comment line (the same comment style used in C# source — `// TODO: …`) identifying the file and its purpose category.
- **FR-007**: The toolkit MUST create a single JSON tracker file named `d2net-tracker.json` at the root of the target directory containing an array of tracking records.
- **FR-008**: The tracker array MUST contain exactly one record per Dart source file present in the source tree.
- **FR-009**: Each tracking record MUST identify the Dart source file by its path relative to the target root (i.e. the path of the `.dart.src` file under the target) and MUST list all nine companion files (each with its filename and a current status field).
- **FR-010**: The status field of every companion file in every record MUST be drawn from the closed enumeration `{"todo", "in-progress", "done", "blocked"}` and MUST be initialised to `"todo"` after a fresh scaffold run.
- **FR-011**: When the target directory already exists at invocation time, the toolkit MUST refuse to run by default and MUST report the conflict to the user, leaving the existing target untouched. The toolkit MUST also accept an explicit override flag (referred to as `--refresh`) that re-runs the scaffold against the existing target with the following semantics: (a) directory structure is brought back into agreement with the current (non-pruned) source tree — directories that no longer exist in the source MAY be left in place, but directories newly added in the source MUST be created; (b) every non-Dart file is rewritten from the current source byte-for-byte; (c) every `.dart.src` file is rewritten from the current source byte-for-byte; (d) every existing companion file (`.cs`, `.ana`, `.tst`, `.con`, `.dep`, `.cgn`, `.iss`, `.sta`, `.ver`) MUST be preserved untouched, regardless of its current content; (e) for any newly-discovered Dart source file in the current source that has no corresponding companion files in the target, the toolkit MUST create the nine companion stub files exactly as in a fresh run; (f) the existing `d2net-tracker.json` file MUST NOT be modified by `--refresh` — adding tracker entries for newly-discovered Dart files is out of scope for the MVP and is reported in the run summary so the user can update the tracker manually.
- **FR-012**: Before writing anything to the target, the toolkit MUST perform a pre-flight pass over the (non-pruned) source tree and detect every case where generating a companion file under FR-005 would collide with a pre-existing non-Dart file of the same name in the same folder. If any collision is detected, the toolkit MUST report the complete list of collisions, exit with a non-zero status, and leave the target tree unchanged (no folders, no files, no tracker written).
- **FR-013**: The toolkit MUST write a human-readable summary to standard output at the end of a successful run, including the counts of: directories created, non-Dart files copied, Dart source files preserved (`.dart.src` count), companion files generated, and tracking records written. No additional report file is produced in the MVP.
- **FR-014**: The toolkit MUST refuse to run when the target path is the same as, or nested inside, the source path.

### Key Entities *(include if feature involves data)*

- **Source tree**: The `GLPNET\glp_runtime` directory and everything below it. Treated as read-only by the toolkit.
- **Target tree**: The `GLPNET\glp_runtime_net` directory created by the toolkit, mirroring the source tree's folder structure.
- **Dart source file**: A file in the source tree whose name ends in `.dart`. Each one becomes the anchor for a 1-to-10 expansion in the target (1 preserved `.dart.src` + 9 companion stubs).
- **Preserved Dart source file**: The exact byte copy of a Dart source file placed in the target with `.src` appended (`foo.dart` → `foo.dart.src`). Acts as the read-only reference during conversion.
- **Companion artifact file**: One of nine stub files generated alongside each preserved Dart source file. The set of extensions `{.cs, .ana, .tst, .con, .dep, .cgn, .iss, .sta, .ver}` represents the conversion artifacts that will eventually be filled in by the .NET port workflow.
- **Non-Dart file**: Any file in the source tree whose name does not end in `.dart`. Copied verbatim.
- **Tracker file**: A single JSON file named `d2net-tracker.json` at the root of the target tree. Contains an array of tracking records — one per Dart source file — and serves as the inventory and progress index for the entire conversion effort.
- **Tracking record**: An entry in the tracker array. Identifies one Dart source file (by relative path) and carries the status of each of its nine companion artifact files. Each status is drawn from the closed enumeration `{todo, in-progress, done, blocked}`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After one invocation against the real `glp_runtime` tree, the count of folders under `glp_runtime_net` equals the count of folders under `glp_runtime` excluding the default-pruned directories (`.dart_tool`, `build`, `.git`, `.idea`, `.vscode`) and everything inside them. Empty folders that are not themselves pruned are still mirrored.
- **SC-002**: After one invocation, the count of non-Dart files under `glp_runtime_net` equals the count of non-Dart files under `glp_runtime` outside the pruned directories, and a byte-comparison of every non-Dart file pair returns zero differences.
- **SC-003**: After one invocation, the count of `.dart.src` files under `glp_runtime_net` equals the count of `.dart` files under `glp_runtime` outside the pruned directories.
- **SC-004**: After one invocation, for every `.dart.src` file in the target, the same target folder contains exactly nine companion files with extensions `.cs`, `.ana`, `.tst`, `.con`, `.dep`, `.cgn`, `.iss`, `.sta`, `.ver` and matching base name.
- **SC-005**: Every companion file generated by the run is non-empty and contains a TODO comment readable as a C# single-line comment.
- **SC-006**: The tracker file at `glp_runtime_net` root parses as valid JSON with a top-level array whose length equals the count of `.dart` files in the source tree outside the pruned directories.
- **SC-007**: A single end-to-end scaffold run completes in under 30 seconds for any source tree containing at most 500 `.dart` files and 2,000 non-Dart files distributed across at most 100 (non-pruned) directories, on a developer workstation with a modern multi-core CPU and an SSD.
- **SC-008**: Re-running the toolkit against an existing `glp_runtime_net` without any override flag produces zero changes to any file in the target tree.
- **SC-009**: Re-running the toolkit with `--refresh` against an existing `glp_runtime_net` (a) leaves byte-for-byte identical every existing companion file with one of the extensions `.cs`, `.ana`, `.tst`, `.con`, `.dep`, `.cgn`, `.iss`, `.sta`, `.ver`, (b) leaves `d2net-tracker.json` byte-for-byte identical, and (c) brings every `.dart.src` file and every non-Dart file into byte-for-byte agreement with the current source.

## Assumptions

- The source tree is `GLPNET\glp_runtime` relative to the repository root (i.e. `D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime`) and the target tree is its sibling `GLPNET\glp_runtime_net`. The toolkit accepts these as parameters but documentation defaults match this layout.
- "Dart file" means any file whose name ends in `.dart`, case-sensitive, that lives in a directory not pruned by FR-002. Files under `test/`, `lib/`, `bin/`, etc. are in scope; files under `.dart_tool/` or `build/` are not.
- The TODO comment placed in each of the nine companion files is a single line in C# `// TODO: …` form. The exact wording inside the comment is left to the implementation but should at least name the file's purpose category (e.g. `// TODO: cs — port from <name>.dart.src`). The user requested "a todo .cs comment", which is read here as "a C-#-style TODO comment", applied uniformly to all nine extensions even though only `.cs` is literally a C# file.
- The tracker file is fixed at `d2net-tracker.json` at the target root; the closed status enumeration is `{todo, in-progress, done, blocked}` initialised to `todo`. The exact JSON shape of each record (field names, ordering, indentation) is left to the implementation; the spec fixes only the requirements above (one record per Dart file, nine companion entries per record, statuses drawn from the enumeration and initialised to `todo`).
- Symbolic links, junctions, and OS-specific metadata files are copied through as ordinary files. Loop detection for symlink cycles is not in scope for the MVP.
- Existing target detection (FR-011) operates on the target directory's existence — not on whether it is "empty enough". An empty pre-existing target directory still triggers refuse-by-default.
- The toolkit is a one-shot CLI; it is not a long-running service and produces no persistent state outside the target tree.
