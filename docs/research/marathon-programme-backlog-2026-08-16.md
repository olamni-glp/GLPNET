<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# Marathon programme backlog — captured 2026-08-16 ~2130Z

> 🔴 **Why this is a git-tracked file and not marathon backlog rows.**
> `buildkit-marathon capture` **failed 8 times and exited 0 every time**:
> `marathon: pgdb-runner bridge exited before reporting a port. Last log lines:
> '[bridge] BRIDGE_ERROR pglite_init_failed PGlite failed to initialize properly'`
> Root cause measured: the run's store catalog directory is **EMPTY** —
> `~/AppData/Local/buildkit/deploy-home/targets/85444bd44ab0/catalog/` contains **no cluster at all**.
> `marathon status` still works because it reads the `.md` mirror; **writes do not.**
> **This is the same false-green class this whole session has been cataloguing, now in the marathon
> itself: a write path that reports success while persisting nothing.**
> Until the store is repaired, **this file is the durable backlog**.

**Marathon run:** `mrun-20d9230f767b` · feature `078-verification-receipts` · status `open` · seq 20
**Discharge gate:** 11 items, **0 satisfied** · next action `/bk-clarify 078`
**Mirror:** `…/targets/85444bd44ab0/marathon-mrun-20d9230f767b.md`

---

## A · The 11 marathon discharge items (already durable in the run)

| # | item |
|---|---|
| 1 | `/bk-clarify 078` — resolve the default-resolved decisions before planning ⟵ **NEXT** |
| 2 | `/bk-plan 078` |
| 3 | `/bk-tasks 078` |
| 4 | `/bk-analyze 078` |
| 5 | `/bk-implement 078` |
| 6 | `/bk-codexreview 078` |
| 7 | `/bk-ship 078` — announce CalVer + re-check `ls-remote` at the cut |
| 8 | `/bk-close 078` post-ship |
| 9 | F1 gate: all 13 witnessed instances fault-injected and refusing loudly (SC-001) |
| 10 | F1 gate: adoption reported honestly per declared area incl. non-adoption (FR-017/FR-018) |
| 11 | protocol: publish roadmap sync round both legs before claiming the next item (W12) |

**Unblocked this session:** item 1 — the engineer ruled **phased FR-008** (a receipt binds only where a
site has declared adoption; unadopted sites keep working and emit a visible non-adoption marker).
That resolves the FR-008 ⟷ FR-017/018 contradiction that had 078 stalled since 14 Aug.

## B · WP-supply superset programme — owned by OTHER lanes, tracked not built here

| id | item | owner | state |
|---|---|---|---|
| **B1** | `allocate` op writer carrying `proposed_actor` + non-zero `e_t_s` | **ariellas/yngenios-windows** (P-core) | 🟢 **UNBLOCKED** — FA-15 ruled |
| **B2** | `e_t_s` surface: stop hardcoding `onboard.py:142` `0.0`; stop `plan.py:261/277/479` excluding `e_t_s<=0` | same — **one increment with B1, SERIAL** | 🟢 unblocked |
| **B3** | addressing refusal / the reader | **already written** — `bc203794` | 🟢 **DELIVER, do not re-author** |
| **B4** | calendar-projected capacity | — | 🔴 **WITHDRAWN** — engineer D2: keep shipped R2 |
| **B5** | T2 graft — absent calendar ≠ out of capacity | **gavriella/buildkit (W5)** | authoring |

**FA-15 ruling (the unblock):** `engineer_id` on an allocate op = **the assigned lane**; add a separate
`allocated_by` for the operator who ran the allocator. Existing ospark ops keep `"mvw"` — grow-only,
nobody rewrites another lane's ops.

## C · Wave-0 "honesty" fixes — assigned to **W5**, land FIRST so later waves are measurable

| | fix | evidence |
|---|---|---|
| a | `render.build_status` must not silently drop null-epic / orphaned rows | **12 of 27 not-closed features INVISIBLE to `roadmap status`**, incl. 078 (top WSJF) and the superset vehicle; `dedupe` inherits it (read 114 vs export's 115) |
| b | `note` must REFUSE a free-form `--wp` naming no existing WP | ~2 lines. A status note minted a phantom WP (board 6→7) and **D4 means it cannot be removed** |
| c | 3rtask `merge` keyed on the **E25 controlled vocabulary**, not free-text `claim_key` | measured: **144 claims, 84 keys, only 4 shared — all 4 per-slice by design**; `converged:false` is 0-by-construction |
| d | a re-issued 3rtask `verdict` must UPDATE the index row | `run.json` = `budget_stop`; `bk-3rtask list` still = `halted`; `index_recorded:false` reported silently |

## D · Repo-local defects found this session

| id | item | status |
|---|---|---|
| D-1 | **Section T (064) jKMV drills RED** — trust material destroyed, agent unidentified | 🔴 **only confirmed remaining red** |
| D-2 | **Suite total untrustworthy** — 3 overlapping runs contended on the same parity fixtures. `554/558` is from a contended run; U-3 `deep_acyclic`/`dag_shared` **do NOT reproduce in isolation** (both `✓ Loaded`) | 🔴 **RE-RUN ONE CLEAN SUITE before quoting any total** |
| D-3 | `run_all_tests.sh` Section U has **no build-staleness check** — a 37h-stale exe reported 077 red | open |
| D-4 | `row_version` is **not discoverable** (`status --json`, `brief`, export journal all omit it) so every editor must probe, and probing overwrites. **D10 hit twice today** | open — ruled: add it to a read surface |
| D-5 | **marathon store catalog is EMPTY**; `capture` fails and **exits 0** | 🔴 **blocks marathon as the vehicle** |
| D-6 | `advance` is **forward-only, no reverse verb anywhere** — an engineer ruling could not be executed | ruled: record-and-keep; gap filed against `multi-host-state-discipline-reversible-states-…` (WSJF 3.00) |

## E · 077 — resolved this session, with two corrections of my own

**Section U root cause was a STALE BUILD, not a code defect.** `glp_repl.exe` built `2026-08-12 08:50`
vs `term_traversal.cs` modified `2026-08-13 21:02` — **37 h older**, so the binary predated 077
entirely and the stack frame named `PartialEvaluator.ApplySubstitution`, a **pre-consolidation symbol
that no longer exists in source**. After `dotnet build` (0 errors) both cyclic fixtures emit
`Cyclic term detected` with **zero** stack overflows.

✅ **077's OVERSTATED stamp WITHDRAWN** — the feature was correct all along.
🔴 **064's stamp STANDS.**
⚠️ **I nearly patched already-correct code**, and stopped only because I compared binary mtime against
source before editing. **Third stale-artifact false conclusion in one session** (stale memory brief,
stale 3rtask index, stale binary).

## F · Standing rules adopted this session

1. **Verify by CONTENT, never by exit code** — an exit code is a claim about a process, not evidence
   about an artifact. *(Learned twice: a stale `$LASTEXITCODE` false green, and marathon `capture`
   exiting 0 on total failure.)*
2. **Date-stamp every measured claim; a claim older than the session is a HYPOTHESIS until re-run.**
   *(I published a stale brief claim twice today.)*
3. **Run the ancestry check yourself and publish the command + exit code** before relaying any route
   touching shared behaviour.
4. **Probe `--expect-version` only with the intended final value**, never a placeholder.
5. **Never `note --wp <free-form>`** — reserved id or an existing WP only.
6. **Never quote a 3rtask verdict from `list`** — read `run.json`.
7. **One clean suite run at a time** — concurrent runs contend on shared fixtures and fabricate
   failures.
