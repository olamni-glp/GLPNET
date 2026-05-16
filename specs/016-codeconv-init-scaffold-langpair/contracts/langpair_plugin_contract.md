# Contract: language-pair plugin & registry

Source: spec FR-001..FR-005, D6. The implementation in `codeconv/src/codeconv/langpairs/` follows this exactly; any deviation is a bug.

## Registry surface (`codeconv.langpairs`)

```python
def register(pair: LangPair) -> None          # idempotent on identical key; error on conflicting re-register
def get(source: str, target: str) -> LangPair # raises UnknownLangPair (actionable, lists known) if absent
def list_pairs() -> list[tuple[str, str]]     # sorted (source, target) ids
def resolve_workspace_pair(engine) -> LangPair # reads codeconv.workspace_settings; see "Stage enforcement"
```

`langpairs/__init__.py` MUST auto-import every production pair package so `list_pairs()`/`get()` work without the caller importing the pair. The only production pair registered by this feature is `("dart","csharp")`.

## `LangPair` protocol (`langpairs/base.py`)

```python
class LangPair(Protocol):
    def key(self) -> tuple[str, str]: ...                       # (source_id, target_id)
    # --- source side (init / discover) ---
    def source_extensions(self) -> tuple[str, ...]: ...          # e.g. (".dart",)
    def tool_exclusion_globs(self) -> tuple[str, ...]: ...        # e.g. (".dart_tool/", "build/", "*.g.dart", ...)
    def read_package_name(self, subtree_root: Path) -> tuple[str | None, dict | None]: ...
    def extract_imports(self, file_path: Path, subtree_root: Path,
                        package_name: str | None) -> list[str]: ...
    def extract_leading_doc(self, file_path: Path) -> str: ...
    # --- target side (scaffold) ---
    def target_extension(self) -> str: ...                       # e.g. ".cs"
    def target_for(self, source_rel: str) -> str: ...            # source POSIX rel → target POSIX rel
    def workdir_name(self, source_rel: str) -> str | None: ...   # per-file working dir, or None
```

Behavioural requirements:

1. **Source hooks are byte-faithful to the pre-016 Dart path.** `dart_csharp`'s `extract_imports` / `extract_leading_doc` / `read_package_name` / `tool_exclusion_globs` MUST produce results identical to today's `tools/discover/{parse,pubspec,walker}.py` so the feature-012/014/015 discover suites stay green (FR-023/SC-005). They are the regression oracle.
2. **`target_for`** maps a source-relative POSIX path to the target-relative POSIX path by swapping the source extension for `target_extension()` (and only that — directory structure is mirrored). For `dart_csharp`: `lib/runtime/heap_fcp.dart` → `lib/runtime/heap_fcp.cs`.
3. **`workdir_name`** returns the per-source-file working-directory name (D2NET parity: `__<basename>` adjacent to each source file) or `None` if the pair has no working-dir convention. `dart_csharp` returns `__<basename-without-ext>`.
4. **Pure & side-effect-free.** Hooks read the filesystem at most (no DB, no bridge, no network) so they are unit-testable without `@needs_bridge`.

## Stage enforcement (FR-004 / FR-018 / SC-008)

- `resolve_workspace_pair(engine)` reads `workspace_settings.source_lang`/`target_lang`.
  - Unset AND stage requires a workspace (scaffold) → raise actionable error, non-zero exit, no output.
  - Unset AND stage tolerates no-workspace (discover) → default `("dart","csharp")` (preserves pre-016 behaviour).
  - Set but not a registered pair → actionable error (lists known pairs), non-zero exit, no mutation.
  - Set and a `--source/--target` override disagrees → refuse (no mixed-pair output).
- A stage MUST resolve the pair **once** at entry and use it for the whole run.

## Extensibility proof obligation (SC-003)

Adding a pair = adding `langpairs/<source>_<target>/` (+ its source/target modules + master `register()`), and one auto-import line in `langpairs/__init__.py`. **Zero edits** to `tools/init`, `tools/discover`, `tools/depgraph`, `tools/scaffold`. A test-only second pair in `test_langpair_registry.py` MUST demonstrate this (registered, `list_pairs()` shows it, selectable, no stage-tool diff).

## Test obligations

`test_langpair_registry.py` (pure unit, no bridge):
1. `register`/`get`/`list_pairs` happy path; duplicate-identical register is a no-op; conflicting re-register errors.
2. `get`/`resolve_workspace_pair` on an unregistered pair → `UnknownLangPair`, message names known pairs.
3. `dart_csharp.source_extensions/tool_exclusion_globs/target_extension/target_for/workdir_name` exact-value asserts (positive) + negative controls (non-Dart path, escaping `..`).
4. `dart_csharp.extract_imports`/`extract_leading_doc` parity vs the legacy `tools/discover` implementation on a fixture (same output).
5. A registered test-only second pair proves SC-003 (no stage-tool source edited).
