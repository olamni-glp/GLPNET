# Quickstart: codeconv Gleam langpair (Dart→Gleam)

Verifies User Stories 1–3. Pure-unit checks need no bridge; the end-to-end
scaffold/mirror run uses the repo-local cluster (`PYTHONUTF8=1`, `--data-dir
D:/bstdev/research/glp/glpnet/.pgdb`). Python: `codeconv/.venv`.

## 0. Baseline (Principle VII)

```
cd codeconv && .venv/Scripts/python.exe -m pytest -q --test-concurrency=1
```
Confirm green before any change (regression oracle = `test_langpair_registry.py`).

## 1. Registry presence & selectability (US1 / US3)

```
.venv/Scripts/python.exe -c "from codeconv import langpairs; print(langpairs.list_pairs()); print(langpairs.get('dart','gleam').key())"
```
Expect: `[('dart','csharp'), ('dart','gleam')]` and `('dart','gleam')`.

## 2. Target conventions (US2)

```
.venv/Scripts/python.exe -c "from codeconv import langpairs; p=langpairs.get('dart','gleam'); print(p.target_extension()); print(p.target_for('lib/runtime/heap_fcp.dart')); print(p.target_for('lib/Foo.dart')); print(p.target_for('lib/type.dart')); print(p.tracker_filename())"
```
Expect: `.gleam` / `lib/runtime/heap_fcp.gleam` / `lib/foo.gleam` /
`lib/type_.gleam` / `codeconv-gleam-tracker.json`.

## 3. New unit suite

```
.venv/Scripts/python.exe -m pytest tests/test_langpair_dart_gleam.py -q --test-concurrency=1
```
All green (target+mirror hooks, source parity, normalization, registry, SC-003
proxy, corpus no-collision).

## 4. End-to-end structure run (US1 acceptance) — optional smoke

Bind a throwaway workspace to `(dart, gleam)` over a **small** Dart subtree and
run `scaffold` then `mirror`; confirm every non-excluded `.dart` yields a
`.gleam` target + companion set under the mirrored structure, and that the root
tracker is `codeconv-gleam-tracker.json`. Then confirm the default `(dart,
csharp)` path is unchanged when the pair is not selected (US1 scenario 2 /
SC-002). Exact CLI invocation per the 016 scaffold/mirror CLI contracts.

## 5. Extensibility proof (US3 / SC-003)

```
git diff --name-only <feature-015-base>..HEAD
```
Expect only: `codeconv/src/codeconv/langpairs/dart_gleam/**`, one line in
`codeconv/src/codeconv/langpairs/__init__.py`, `codeconv/tests/test_langpair_dart_gleam.py`,
and `specs/032-codeconv-gleam-langpair/**`. **Zero** `tools/` files
(unless R3-b is owner-approved → exactly one generic line in
`tools/scaffold/planner.py`).
