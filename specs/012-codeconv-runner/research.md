# Phase 0 Research: 012-codeconv-runner

This document resolves the technical unknowns of the plan. Each section is structured as Decision / Rationale / Alternatives considered. Every NEEDS CLARIFICATION raised by the plan template is closed below.

The spec itself was clarified in Session 2026-05-09 (15 questions, recorded in `spec.md` § Clarifications). Items already settled there are referenced, not re-litigated.

---

## R1. Cross-process bridge lock — `proper-lockfile`

**Decision**: Use `proper-lockfile@^4.1.2` (npm) inside `pglite_bridge.mjs` to acquire `.pgdb/.bridge.lock` at startup with `{ retries: 0, stale: 0 }`. Hold for the bridge process lifetime. On lock acquisition failure, exit immediately with code `5` (`EADDRINUSE` parity, per AIGRID's existing convention captured in `prereq-patterns/pglite/sources.md` row A11) plus a stderr line naming the holder PID read from `.pgdb/bridge.json`.

**Rationale**:
- Spec FR-002/FR-003 + Clarification Q3 fixes the choice of an OS-level lock with kernel-managed release. No PID + heartbeat heuristics.
- `proper-lockfile` is the de-facto npm wrapper for cross-platform `flock`-style file locking. On POSIX it uses advisory `flock`; on Windows it uses `LockFile` semantics inherited from the file handle. In both cases the kernel releases the lock when the process exits or the file handle is closed — which is exactly the Clarification Q3 invariant.
- The library is already widely used in the Node ecosystem (npm itself uses it for cache locking) — no exotic dependency.

**Alternatives considered**:
- `lockfile` (older, mtime-based, requires manual stale handling) — rejected: violates Clarification Q3.
- Hand-rolled `fs.openSync(..., 'wx')` + PID file — rejected: race window between open and write; Windows-vs-POSIX semantic divergence; spec wants kernel-managed.
- `node-fcntl-lock` — rejected: POSIX-only.

**Validation criterion**: SC-001 (parallel start race) + SC-002 (post-kill restart) pass on Windows 11. If `proper-lockfile` does NOT honour kernel release on Windows under the chosen call shape, escalate to Gabi per spec Assumptions and re-open this decision before implementation lands.

**Python-side equivalent**: `portalocker>=2.8` (cross-platform Python lock library; `fcntl` on POSIX, `LockFileEx` on Windows; same kernel-managed release semantics as `proper-lockfile`). Pinned in `codeconv/pyproject.toml` (T002). Used by `codeconv/src/codeconv/bridge_client.py` (T053).

**.NET-side equivalent**: `System.IO.FileStream` opened with `FileShare.None` on `.pgdb/.bridge.lock` (per research R13). Same kernel-managed release.

All three lock implementations target the SAME lock file at `.pgdb/.bridge.lock` and rely on kernel-level exclusion semantics; mixed-language clients (Python `codeconv` + .NET `D2Net.*` + Node bridge) interact correctly because they all go through the OS file-locking primitive, not through library-level state.

---

## R2. Bridge auto-spawn protocol — detached process model

**Decision**: Clients spawn the bridge with the language-appropriate detached-process shape, mirroring the existing reference at `D:/BREENDEV/aigrid/AWS-Infra/src/opskit/_vendor/opskit_pglite_sidecar.py` (`cmd_start`):

- Python: `subprocess.Popen([...], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, creationflags=subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.DETACHED_PROCESS)` on Windows; `start_new_session=True` on POSIX.
- .NET (`D2Net.BridgeClient`): `ProcessStartInfo { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true }` plus Windows-specific `CREATE_NEW_PROCESS_GROUP | DETACHED_PROCESS` flags via P/Invoke or via the `Microsoft.Windows.CsWin32` interop helper. POSIX equivalent uses `setsid()` via a small native shim or `nohup`-style wrapper.
- Both clients READ the `BRIDGE_READY port=<n> pid=<p>` token from the spawned process's piped stdout BEFORE detaching the pipe (per FR-030: token captured before detach so post-detach output never leaks to caller terminal).

**Rationale**:
- The opskit reference is already battle-tested for the same lifecycle. Spec FR-006 cites auto-spawn-on-demand verbatim. Spec FR-030 fixes the pre-detach token capture rule.
- Using language-native subprocess shapes avoids a launcher-shim layer; the bridge is plain `node pglite_bridge.mjs --data-dir .pgdb --port 0 --daemon`.
- After `BRIDGE_READY` is consumed, the spawning client closes its stdout/stderr pipe handles (or `subprocess.DEVNULL`-redirects them) so the bridge survives the parent's exit and writes to `.pgdb/bridge.log` from there on.

**Alternatives considered**:
- A standalone `node` daemon launcher script — rejected: extra moving part; spec FR-006 explicitly designates auto-spawn as the primary path with the manual launcher as escape hatch only.
- Windows Service / systemd unit — rejected: spec scope is per-checkout developer workstation, not host service.

---

## R3. Bridge port allocation

**Decision**: Bridge defaults to `--port 0` (let the OS allocate an ephemeral port). The bridge logs the resolved port on the `BRIDGE_READY port=<n> pid=<p>` line and writes it into `.pgdb/bridge.json`. Operators may override with an explicit `--port <n>` for debugging.

**Rationale**:
- Eliminates the entire class of "port already in use" startup failures.
- The bridge's existing CLI surface (`--port <int>`) already supports this (`createServer().listen(0)` resolves to an ephemeral port).
- Clients discover the port from the sidecar JSON, not from a hardcoded default.

**Alternatives considered**:
- Hardcoded default `54400` (per the existing investigation harness scripts) — rejected: collisions with other dev workloads on the same machine; hardcoded ports break parallel checkouts.
- Range scanning (`for port in 54400..54499: try`) — rejected: race-prone, no benefit over kernel allocation.

**Note**: This is a minor evolution of the existing bridge. The bridge already accepts `--port` per `prereq-patterns/pglite/sources.md`. The `--port 0` semantics may need a one-line bridge change to log the resolved port instead of the requested port — captured as a task input.

---

## R4. Sidecar JSON shape

**Decision**: `.pgdb/bridge.json` carries `{host: "127.0.0.1", port: <int>, pid: <int>, started_at: <ISO-8601 UTC>, data_dir: <absolute path>, role: "primary", managed_by: "auto-spawn" | "manual"}`. Written by the bridge AFTER `listen()` resolves, BEFORE the `BRIDGE_READY` token is emitted on stdout. Atomically written via `fs.writeFileSync(tmp, ...) + fs.renameSync(tmp, final)`.

**Rationale**:
- Mirrors the AIGRID `sidecar.json` shape for parity (see `opskit_pglite_sidecar.py` and `prereq-patterns/pglite/sources.md` row A13). Clients that already read AIGRID-shape sidecar JSON can read this without translation.
- Atomic rename prevents partial-read races (lost-the-lock client reading half a JSON file written by the spawning bridge).

**Alternatives considered**:
- Just `{port, pid}` — too thin; clients want `started_at` for staleness diagnostics and `data_dir` for sanity-check before connecting.
- TOML / YAML — JSON is the native Node format; no parser dependency on either side.

---

## R5. Python CLI framework — Typer

**Decision**: `typer` for the `codeconv` CLI (entry point: `codeconv` console script registered via `pyproject.toml`).

**Rationale**:
- Type-hint native, matches the prereq-pattern's "thin wrapper around source-of-truth CLI" shape from BREENDEV `/opskit-init` (which also shows a Python click-style CLI surface).
- Subcommand layout (`codeconv discover`, `codeconv list`, `codeconv <future>`) maps cleanly to Typer's `app.add_typer(...)` plug-in pattern, which fits FR-016 (file-system tool discovery): each `codeconv/tools/<name>/` exports a `typer.Typer()` app, the runner imports them all and adds them under `codeconv <name> ...`.
- Help output is auto-generated, free, and matches the "thin slash wrapper forwards `--help` verbatim" pattern.

**Alternatives considered**:
- `click` directly — rejected: more boilerplate; Typer is just a Click wrapper anyway.
- `argparse` — rejected: subcommand registration ergonomics are weaker; no type-hint-driven flag generation.
- `fire` — rejected: too magical; not deterministic enough for spec-driven contracts.

---

## R6. DBOS configuration on PGLite

**Decision**: Initialise DBOS at runner startup with `DBOSConfig(database_url=<unified-bridge-url>, db_engine_kwargs=pglite_engine_kwargs(application_name='codeconv'), schema='dbos')`. Apply `_apply_pglite_compat_patch()` BEFORE `dbos.launch()` (the prereq-pattern's helper from `applicability.md` § DBOS). Apply `apply_to_engine(engine)` to the SQLAlchemy engine returned by `dbos.app_db.engine` immediately after launch (so `timestamptz` reads from DBOS-managed tables don't crash).

**Rationale**:
- Spec FR-014 directly mandates this shape; this section just records the call sequence.
- Spec FR-015 fixes schema isolation: DBOS's tables go in `dbos`, codeconv's tables go in `codeconv`. DBOS supports `schema=` since 0.6+.
- Per applicability.md, the `_apply_pglite_compat_patch()` shim is required before DBOS's `migration_one` runs (or DBOS startup fails on `CREATE EXTENSION uuid-ossp`).

**Alternatives considered**:
- Skip DBOS, use plain SQLAlchemy + a hand-rolled progress table — rejected: spec FR-017 mandates durable workflows; DBOS is the named runtime.
- Run DBOS with `schema=public` and rely on table-name prefixing — rejected: spec FR-015 forbids `public` for any feature-introduced table.

---

## R7. Tombstone path encoding (Windows path separators)

**Decision**: Tombstone paths use forward slashes (`/`) in YAML frontmatter and on disk. The on-disk tombstone tree mirrors the `glp_runtime_net/` tree exactly; subdirectories use the OS path separator at filesystem level (Windows backslash on disk, forward-slash in YAML). Inventory `path` column uses forward slashes always.

**Rationale**:
- Cross-platform tombstone parity: the same checked-in tombstone tree must read identically on Windows and on the GLP repo's Mac/Linux sibling. YAML frontmatter must be path-separator-portable.
- `pathlib.PurePosixPath(rel).as_posix()` is the canonical normaliser. SQLAlchemy stores the same posix string.
- Discover writes tombstones using `pathlib.Path(...)`'s native separator (so files actually land in the right OS location), but encodes `path:` field in frontmatter using `.as_posix()`.

**Alternatives considered**:
- Backslash on Windows in YAML — rejected: tombstones diff across machines; SC-007 (rebuild bit-for-bit) breaks.
- URL-encoded paths — rejected: unreadable; engineers can't grep tombstones.

---

## R8. Migration idempotence (FR-009)

**Decision**: The migration command `D2Net.PgdbMigrate` (and its `/D2NET-pgdb-migrate` skill wrapper) implements a four-step state machine:

1. Read source (`.D2NET/pgdb/`) and target (`.pgdb/`) presence flags.
2. **(absent, *)**: exit 0 with "no-op: source already migrated or never present".
3. **(present, absent)**: take backup `.D2NET/pgdb.bak.<UTC-stamp>/` (recursive copy via `robocopy /MIR` on Windows, `cp -r` on POSIX), then atomic rename `.D2NET/pgdb` → `.pgdb`.
4. **(present, present-non-empty)**: refuse per FR-008 with explicit message naming both paths and listing source row counts vs target row counts to help operator decide.

After successful migration, write a `.pgdb/.migration-record.json` carrying `{from: ".D2NET/pgdb", backup_at: "...", at: "<UTC>"}`. Re-running the migration in state (absent, *) is a no-op regardless of whether the record exists — per FR-009, the migration does NOT depend on a flag file.

**Rationale**:
- The four cases are exhaustive; idempotence comes from case 2 being the natural retry-after-success path.
- Backup-then-rename is reversible: the operator can `rm -rf .pgdb && mv .D2NET/pgdb.bak.<stamp> .D2NET/pgdb` to undo.
- `robocopy /MIR` on Windows is atomic enough for this purpose (file-level mirroring; no partial-file states because PGLite is offline during migration). For PGLite cluster files (small), the brief window of dual-residency is acceptable.

**Alternatives considered**:
- Move source first, write backup from new location — rejected: if backup fails, source is already gone.
- File-by-file move with rollback log — rejected: PGLite cluster is small enough that whole-tree backup is cheap; complexity not worth it.

---

## R9. Bridge log rotation

**Decision**: Use Node's built-in `node:fs` via a tiny in-bridge rotator: when the current `.pgdb/bridge.log` exceeds 5 MiB, rename existing `.pgdb/bridge.log.2` → `.pgdb/bridge.log.3` (drop `.3` if exists), `.log.1` → `.log.2`, `.log` → `.log.1`, and open a new `.log`. Check on every batched write (cheap; one `statSync` per N=100 writes is fine). No external rotation library.

**Rationale**:
- Zero new dependencies for log rotation.
- Spec FR-030 specifies "size-based rotation, ~5MB × 3 retained" — exact policy is small enough to inline.
- Per FR-030, log-write failure must NOT block bridge work; the rotator catches and warns once per failure mode.

**Alternatives considered**:
- `winston` + `winston-daily-rotate-file` — rejected: heavyweight for one log file; introduces transitive deps.
- POSIX `logrotate` external tool — rejected: not cross-platform; not in spec.

---

## R10. Tool registration mechanism (FR-016)

**Decision**: `codeconv/src/codeconv/tools/__init__.py` runs `pkgutil.iter_modules(__path__)` at runner startup. For each subpackage, it imports `codeconv.tools.<name>` and looks up an `app` attribute (a `typer.Typer()` instance) plus optional `register_workflows(dbos_app)` callable. The runner's main `app` does `app.add_typer(tool.app, name=name)`. Adding a new tool means (a) creating a new `codeconv/src/codeconv/tools/<name>/__init__.py` exporting `app`, and (b) creating a new `.claude/skills/codeconv-<name>/SKILL.md` thin wrapper. No edits to `codeconv/cli.py` or `codeconv/runner.py`.

**Rationale**:
- Spec FR-016 explicitly: "Adding a new tool MUST require no edits to the runner's own code." File-system convention is the simplest delivery.
- `pkgutil.iter_modules` is stdlib; no plugin manager dependency.
- The Claude Code skill side is intentionally NOT auto-generated — each skill needs its own slash-help text per the BREENDEV/D2NET-init pattern. Spec mandates the skill exists; spec does not mandate it be generated.

**Alternatives considered**:
- Python entry-points (`importlib.metadata.entry_points`) — rejected: requires editing `pyproject.toml` per new tool, which is "edits to the runner's project metadata" — borderline FR-016 violation; file-system scan is more honest.
- Decorator registry (each tool calls `@register_tool('name')`) — rejected: requires the runner to import every tool eagerly to populate the registry; subtree scan is the honest version of that.

---

## R11. Doc-comment extraction for `purpose` / `key_idea`

**Decision**: For each `.dart` file, read up to the first 200 lines (cheap upper bound). Skip blank lines and shebang. If the next non-blank lines are a contiguous block of `///` Dart doc-comments OR a `/** ... */` block, capture the block verbatim (text without the `///` or `/* */` markers, with leading `//[/]` whitespace stripped per Dart convention). The captured block populates BOTH `purpose` AND `key_idea` (per Clarification Q9: "verbatim — same value as `purpose` when a single block, blank otherwise"). If no leading doc-comment exists, both fields are empty strings (FR-020).

**Rationale**:
- Spec FR-020 explicitly forbids heuristics or AI inference. Mechanical extraction only.
- 200-line read cap protects against pathological files; real Dart files have doc comments in the first ~30 lines.

**Alternatives considered**:
- Parse with `dart analyze --format=json` — rejected: requires Dart SDK on PATH; out of scope; pure regex extraction suffices for the leading block.
- Run `dartdoc` — rejected: heavy; same SDK requirement.

---

## R12. Import resolution for `dart_imports` edges

**Decision**: For each `.dart` file in scope, regex-extract `import\s+'([^']+)';` and `import\s+"([^"]+)";` directives. For each captured target:

1. If target starts with `package:`, `dart:`, `dart-ext:` — skip (external package or SDK; not an in-subtree edge).
2. If target is a relative path (`./foo.dart`, `../bar/baz.dart`, `foo.dart`), resolve relative to the from-file's directory.
3. If the resolved path lies inside `glp_runtime_net/`, record one row in `codeconv.dart_imports` with `(from_path, to_path)` both relative to `glp_runtime_net/`. Else: skip (not an in-subtree edge).

Duplicate `import` directives in a single file (same `(from_path, to_path)` pair) are deduplicated with a warning per FR-019.

**Rationale**:
- Regex extraction is sufficient because Dart `import` syntax is regular at the token level; no comment-eating edge cases inside string literals matter for `import` directives that must be at top of file.
- Dart's standard `package:` prefix is what surfaces external dependencies; `dart:` is SDK; both are out of subtree scope by definition (not in `glp_runtime_net/`).

**Alternatives considered**:
- AST parse — rejected: same SDK dependency as R11; not justified for what is functionally a top-of-file regex.
- Filesystem-ignorant resolution (treat `import 'foo.dart'` as a same-directory file) — rejected: Dart imports can be nested with `../`; need real path resolution.

---

## R13. .NET shared bridge-client library

**Decision**: New project `tools/d2net/src/D2Net.BridgeClient/D2Net.BridgeClient.csproj` (.NET 8 class library) exposing:

- `BridgeEndpoint AcquireOrDiscover(string repoRoot, TimeSpan readyTimeout)` — implements the FR-006 protocol.
- `Process? OwnedProcess { get; }` — non-null only on the lock winner; clients are responsible for NOT killing this process on their own exit (let the bridge run).
- `void Dispose()` — releases the lock if owned; does NOT terminate the bridge.

Both `D2Net.Init` and `D2Net.Scaffold` (and any future D2NET tool) reference this project. The lock implementation uses `System.IO.FileStream` opened with `FileShare.None` on `.pgdb/.bridge.lock` — Windows holds the share-mode exclusion at the kernel level; Linux/macOS use the same `flock`-style semantics via `RuntimeInformation.IsOSPlatform(...)` branching.

**Rationale**:
- Code reuse: the lock + sidecar discovery + spawn protocol must be identical across .NET clients to avoid divergence with the Python `bridge_client.py`. A shared library is the simplest enforcement.
- `FileShare.None` on a held FileStream is the .NET equivalent of `proper-lockfile`'s POSIX flock — kernel releases the share on process exit. No PID heuristics needed.

**Alternatives considered**:
- Each client implements its own protocol — rejected: drift inevitable; spec FR-001/FR-002 demand a single bridge.
- Use `Mutex` (named system mutex) — rejected: Windows-only abandoned-mutex semantics differ from POSIX; harder to align with Python's `proper-lockfile` behavior.

---

## R14. D2NET schema discovery (FR-015 schema isolation)

**Decision**: Inspect `D2Net.Init/Schema/` and `D2Net.Init/SchemaInitializer.cs` to determine which schema(s) D2NET currently uses against `.D2NET/pgdb/`. Document the finding in `data-model.md` § D2NET schemas. If D2NET currently uses `public` (likely default), this is preserved unchanged — spec FR-015 says "D2NET's tables MUST remain in whichever schema(s) D2NET currently uses; no D2NET schema rewrite is permitted as part of this feature."

**Rationale**:
- Spec is explicit: no D2NET schema rewrite. Whatever's there stays. This task is documentation, not code change.
- Coexistence works because PGLite supports multiple schemas in the same cluster; `search_path` per client controls visibility.

**Alternatives considered**:
- Move D2NET into a `d2net` schema — rejected: spec FR-015 explicitly forbids.

---

## R15. Performance plan for SC-013

**Decision**:
- **Fresh-checkout 60-second budget for 128 files**: ~470 ms per file in walltime. Discover is I/O-bound (read each file's first 200 lines + capture imports). DBOS workflow per-file checkpointing has overhead, so checkpoints are coarse-grained (one DBOS step per file, not per import). Parallelism is bounded by FR-017's durability requirement: each file is one DBOS step; up to 8 files in flight via DBOS's worker pool.
- **Idempotent re-run 5-second budget**: short-circuit on `mtime` + `sha256` match against the existing inventory row; skip parse + tombstone write entirely. The 5-second budget covers DBOS step bookkeeping for 128 no-op steps.

**Rationale**:
- The 60s SLO maps to ~250 ms per file with a 4× safety margin, well within Python's I/O latency on Windows for 200-line file reads.
- Idempotence short-circuit reduces per-file work to one `os.stat()` + one DB lookup.

**Alternatives considered**:
- Skip DBOS for the inner per-file loop (faster but breaks FR-017) — rejected.
- Use `asyncio` everywhere — rejected: DBOS Python is sync-first; mixing async into the workflow scaffolding adds complexity without buying I/O parallelism we don't need at 128-file scale.

---

## R16. Out of scope (explicit non-decisions)

The following are intentionally deferred by spec clarifications and are NOT resolved here:

- **Semantic enrichment** of `purpose` / `key_idea` (LLM-backed). Future codeconv tool. Spec FR-020.
- **Dart → C# translation logic**. Future codeconv tool. Spec FR-028.
- **Caller-graph extension to outside-subtree files**. Spec FR-023 (inside-only, warn on outside).
- **D2NET schema rewrite**. Spec FR-015 (D2NET schemas unchanged).

---

## Open questions for implementation

None. All NEEDS CLARIFICATION items raised by the plan template are resolved above. The 15 spec-side clarifications (Session 2026-05-09) plus R1–R15 above constitute the closed set.

If implementation discovers a `proper-lockfile` Windows behavioural surprise (R1 validation criterion), STOP and escalate per spec Assumptions before lowering the lock guarantee.
