# Quickstart: /glptutorial-list

**Feature**: `022-glptutorial-list` | **Date**: 2026-06-03

A read-only browser for the vendored GLP tutorial corpus. Two equivalent
surfaces: the `codeconv tutorials list` CLI (engine) and the `/glptutorial-list`
skill (thin front-end).

## Prerequisites

- The codeconv venv (one-time):
  ```
  python -m venv codeconv/.venv
  codeconv/.venv/Scripts/python.exe -m pip install -e codeconv[dev]   # POSIX: codeconv/.venv/bin/python
  ```
- The vendored corpus at `tutorials/olamni/` (created by `codeconv tutorials sync`
  from the sibling `D:/bstdev/research/glp/GLP/olamni/tutorial/`). The list path
  never reads the sibling repo (FR-007).

## Browse the whole catalog (US1)

```
codeconv tutorials list
```
Prints every chapter, grouped, with each exercise's `.glp` scripts and a one-line
description. Empty (planned) chapters show `(no scripts)`.

## List one chapter (US2)

```
codeconv tutorials list ch03
codeconv tutorials list 3          # zero-pad normalized → ch03
codeconv tutorials list core       # title substring → ch03 "GLP Core"
```
Unknown id → "no match" + the available chapter ids (exit 3).

## Machine-readable / skill parity

```
codeconv tutorials list --json
codeconv tutorials list ch05 --json
```

## Via the skill

```
/glptutorial-list
/glptutorial-list ch03
```
Forwards verbatim to `codeconv tutorials list` and relays the output.

## Refresh the vendored snapshot (supporting, D3)

```
codeconv tutorials sync           # re-copy sibling → tutorials/olamni/ + rewrite SNAPSHOT.md
codeconv tutorials sync --check   # verify vendored tree vs manifest; non-zero on drift
```

## Run the tests

```
codeconv/.venv/Scripts/python.exe -m pytest codeconv/tests/test_tutorials_*.py
```
Pure filesystem tests against `codeconv/tests/fixtures/tutorials_corpus/` — no
bridge, no DBOS, no REPL.

## What this does NOT do

- Does not execute any tutorial script (that is the companion `/glptutorial-run`).
- Does not read the sibling GLP repo at list time (only `sync` does, at build time).
- Does not enrich descriptions with an LM — extraction is mechanical (D7).
