# Data Model — d2net-scaffold

**Feature**: `001-d2net-scaffold` — see [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

The toolkit has no database. The "data model" here describes (a) the in-memory work-plan structure built during the pre-flight pass, and (b) the on-disk JSON tracker shape produced at the target root.

---

## In-memory entities

### `ScaffoldOptions` (record)

Parsed CLI inputs. Immutable after parsing.

| Field | Type | Source | Notes |
|-------|------|--------|-------|
| `SourceRoot` | `string` (absolute path) | positional arg 1 | Validated to exist, validated not to be ancestor of `TargetRoot` (FR-014) |
| `TargetRoot` | `string` (absolute path) | positional arg 2 | Validated to NOT exist (default mode) or to exist (refresh mode) |
| `Refresh` | `bool` | `--refresh` flag | Default `false`. Toggles `RefreshRunner` instead of `ScaffoldRunner` |

### `WorkPlan` (record)

Built by the pre-flight pass; consumed by the write pass. Holds the complete intent of a single scaffold run, derived purely from reading the source tree.

| Field | Type | Notes |
|-------|------|-------|
| `Directories` | `IReadOnlyList<RelDir>` | Every non-pruned directory under `SourceRoot`, in deterministic order |
| `NonDartFiles` | `IReadOnlyList<RelFile>` | Every non-Dart file outside pruned dirs, in deterministic order |
| `DartFiles` | `IReadOnlyList<RelFile>` | Every `.dart` file outside pruned dirs, in deterministic order |
| `Collisions` | `IReadOnlyList<Collision>` | FR-012 collisions detected during pre-flight |

A `WorkPlan` with non-empty `Collisions` is reported and the run aborts before any write (FR-012, R6).

### `RelFile` / `RelDir` (records)

| Field | Type | Notes |
|-------|------|-------|
| `RelPath` | `string` | Path relative to `SourceRoot`, using forward slashes |
| `AbsSourcePath` | `string` | Joined absolute path on disk (cached) |

### `Collision` (record)

| Field | Type | Notes |
|-------|------|-------|
| `DartFileRelPath` | `string` | The `.dart` file whose companion would collide |
| `CollidingExtension` | `string` | One of the nine companion extensions (`cs`, `ana`, …) |
| `ExistingFileRelPath` | `string` | The pre-existing source file the stub would have collided with |

### `RunSummary` (record)

Accumulated counters used by FR-013 stdout reporting. Mutable during the write pass, immutable when handed to the printer.

| Field | Type |
|-------|------|
| `DirectoriesCreated` | `int` |
| `NonDartFilesCopied` | `int` |
| `DartSrcFilesWritten` | `int` |
| `CompanionStubsWritten` | `int` |
| `TrackerRecordsWritten` | `int` |
| `NewlyDiscoveredDartFiles` | `IReadOnlyList<string>` *(refresh mode only — Dart files for which stubs were generated but no tracker entry was added; per FR-011 (f))* |

---

## On-disk tracker (`d2net-tracker.json`)

Located at `{TargetRoot}/d2net-tracker.json`. Top-level is a JSON array; each element is one record.

### Shape (informal)

```json
[
  {
    "source": "lib/runtime/runner.dart.src",
    "companions": {
      "cs":  "todo",
      "ana": "todo",
      "tst": "todo",
      "con": "todo",
      "dep": "todo",
      "cgn": "todo",
      "iss": "todo",
      "sta": "todo",
      "ver": "todo"
    }
  }
]
```

### Field rules

| Field | Type | Rules |
|-------|------|-------|
| `source` | string | Path of the `.dart.src` file relative to the target root, forward slashes. One per Dart file in source. Unique across the array. |
| `companions` | object | Exactly nine keys: `cs`, `ana`, `tst`, `con`, `dep`, `cgn`, `iss`, `sta`, `ver`. No other keys allowed. |
| `companions.<ext>` | string | One of the closed enumeration `{"todo", "in-progress", "done", "blocked"}`. Initialised to `"todo"` on a fresh run. |

### Invariants

- Array length = number of `.dart` files under non-pruned source directories.
- For every record `r`, the file at `{TargetRoot}/{r.source}` exists after a fresh run and is byte-identical to `{SourceRoot}/{r.source without ".src"}`.
- For every record `r` and every `<ext>` in the closed companion set, the file `{TargetRoot}/<dir>/<basename>.<ext>` exists, where `<dir>` and `<basename>` are derived from `r.source` by stripping `.dart.src`.

### Companion status state machine

```
   todo ─────► in-progress ─────► done
     │              │
     └──────────────┴──────► blocked ──────► in-progress (resume)
```

The scaffold step only writes `todo`. Transitions to other states are the responsibility of downstream conversion tools and humans editing the tracker.

---

## Relationships

- One **`ScaffoldOptions`** per invocation.
- One **`WorkPlan`** per invocation, derived from `ScaffoldOptions.SourceRoot`.
- One **tracker file** per fresh-run invocation (skipped on `--refresh`).
- N **tracker records** per tracker file = number of `.dart` files under non-pruned source dirs.
- 1:1 between a tracker record and its preserved `.dart.src` file in the target.
- 1:N (=9) between a `.dart` source file and its companion stub files.

---

## Validation rules (mapped to spec FRs)

| Rule | Spec | Enforcement point |
|------|------|------|
| `SourceRoot` exists & is a directory | FR-001 implicit | `ScaffoldOptions` parsing |
| `TargetRoot` is not equal to or nested inside `SourceRoot` | FR-014 | `ScaffoldOptions` parsing |
| `TargetRoot` does not exist (default mode) | FR-011 | `ScaffoldOptions` parsing |
| Pruned directory names match `{.dart_tool, build, .git, .idea, .vscode}` exactly | FR-002 | `DirectoryWalker` |
| No companion stub would overwrite a pre-existing source file | FR-012 | `PreflightChecker` over `WorkPlan.DartFiles` × companion extensions |
| Every Dart file gets exactly nine companion stubs | FR-005 | `CompanionFileWriter` |
| Every companion stub contains the TODO line | FR-006 | `CompanionFileWriter` |
| Tracker contains exactly one record per Dart file | FR-008 | `TrackerWriter` builds from `WorkPlan.DartFiles` |
| Companion statuses are drawn from the closed enum, initialised to `todo` | FR-010 | `TrackerWriter` constants |
