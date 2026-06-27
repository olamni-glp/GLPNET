---
name: "buildkit-opskit"
description: "BK-OpsKit: advisory entry point + integration document for porting OpsKit into buildkit. Explains what BK-OpsKit is and its do-first role in the OpsKit-into-buildkit epic, and resolves the on-disk path to the authoritative bk-opskit-integration.md (the component→target map, target layout + interface contracts + naming convention, the canonical-base-plus-rebase merge contract, and the recorded-but-open OD-001 resync decision). A stub: it ports no OpsKit behavior. Advisory & read-only — never switches branches, edits files, blocks a merge, or auto-invokes a buildkit-* command (FR-011)."
argument-hint: "[info|doc|where] or a question about the OpsKit→buildkit integration"
compatibility: "Requires a buildkit-initialised project (.specify/). Reads only the in-package bk-opskit-integration.md; no AWS, no network, no DB."
metadata:
  author: "buildkit"
  source: "templates/commands/buildkit-opskit.md"
user-invocable: true
disable-model-invocation: false
---


## User Input

```text
$ARGUMENTS
```

## What this does

`/bk-opskit` is the advisory entry point for **BK-OpsKit** — the buildkit-native home for the
OpsKit operator-session + AWS-VPC-operations toolkit. It is item #1 of the OpsKit-into-buildkit
epic: it stands up the skeleton and points at the single authoritative integration document,
`bk-opskit-integration.md`. It **ports no OpsKit behavior yet** — that is the two downstream
prelim-refactor features.

The integration document is the system of record. It carries:

- the **exhaustive component→target map** (every OpsKit public module, CLI subcommand, `opskit-*`
  skill, and public callable — mapped to a target or marked out-of-scope with a reason),
- the **target module + skill layout** and the **mechanical naming convention**,
- the **public interface contracts** down to the public-callable level,
- the **canonical-base-plus-rebase merge contract** (Gavri's contribution lands first; OLAMNIT/
  Marcelle rebases onto it), and
- the **recorded-but-unresolved OD-001 resync decision** (gated on GitHub-mechanism research).

`breenlake` is treated as an **external dependency boundary** — characterised, not ported.

## How to run it

```
buildkit-opskit info     # what BK-OpsKit is + its do-first role  (advisory; auto-invokes nothing)
buildkit-opskit doc      # resolve + print the path to bk-opskit-integration.md
buildkit-opskit doc --sections   # also print the document's section index
buildkit-opskit where    # skeleton package path + registered surfaces
```

All subcommands accept `--json`; `info`/`doc` accept `--project-root <path>` to override doc
resolution. From a coding agent, `/bk-opskit` reaches the same surface.

## Exit codes

- `0` — success (including the advisory "here is the doc / here is the role").
- `1` — usage error / invalid arguments.
- `2` — environment error (e.g. `bk-opskit-integration.md` cannot be located).

## Boundaries (do NOT) — FR-011, Constitution I & VII

- Advisory only: it auto-invokes **no** `/bk-*` or `buildkit-*` pipeline command.
- It switches **no** branch, edits/stages/commits **no** file, and blocks **no** merge.
- It performs **no** OpsKit behavior — no AWS calls, no environment mutation, no port. It is a stub.
- It writes **no** secret to a durable sink: any echoed text is secret-redacted first (FR-012).

**Registry upkeep (spec-028 FR-004)**: run
`python -m buildkit_cli.registry touch --tool buildkit-opskit` from the project root. It marks the
capability registry possibly-stale and **always exits 0** (fail-safe; never blocks this stage).
Ignore its output.
