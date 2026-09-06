<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract — `scripts/evidence_signal_audit.py`

## Invocation

    python scripts/evidence_signal_audit.py [--repo PATH] [--manifest PATH] [--report PATH] [--json]

All paths default to repo-relative conventions. `--repo` defaults to the script's own repo root, so
the audit is never dependent on the caller's working directory — a hard-coded sibling-host path has
already cost this fleet a 16/16 false failure.

## Exit codes — the contract this tool must not itself violate

| code | meaning |
|---|---|
| `0` | report generated, **and** it is clean: zero errors, zero non-conforming, zero unproven |
| `1` | report generated, and it contains at least one **non-conforming** or **unproven** surface |
| `2` | usage error |
| `3` | **manifest/scan disagreement** (FR-014b): a scan-only hit or a manifest-only entry |
| `4` | the audit could not examine part of the repo it was asked to examine (FR-020) |

**Exit 0 is reserved for a clean report.** The tool must never exit 0 while reporting a problem —
that is measured instance 4 (`buildkit-scheduler reject` exiting 0 while refusing), and an audit for
that class committing that class would be worthless. Codes are distinct per failure class so a
wrapper can act without parsing prose (FR-009).

**Piping changes `$?` to the pipe's status.** The tool prints a reminder to stderr when it detects it
is not attached to a terminal, mirroring the canonical YNET client's own banner, because this is a
measured way callers lose the exit code.

## Output

Human-readable summary on stdout; `--json` emits the `ConformanceReport` (see `data-model.md`).
The report file is written in both modes — a run that produced no artefact cannot be reconciled
later.

## Receipt (FR-017)

Every run emits a feature-078-conforming receipt recording the resolved repo root **as resolved**,
the manifest sha256, counts examined and skipped with reasons, the outcome classification, and the
timestamp. The audit is subject to the invariant it audits: a run that did not happen must be as
loud as a run that failed.

## Refusals

- A manifest that fails schema validation ⇒ exit 2, naming the field. Never a default, never a skip.
- A manifest entry with `governed_by` containing `FR-004` and no `negative_control` ⇒ exit 2. The
  manifest is asserting a contention property with no way to be wrong, and that is worse than an
  absent entry.
- An unreadable region ⇒ recorded in `regions_unexamined` **and** exit 4. It is never silently
  dropped from the denominator.
