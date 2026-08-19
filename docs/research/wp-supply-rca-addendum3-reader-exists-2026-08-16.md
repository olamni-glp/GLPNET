<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# WP-supply RCA — ADDENDUM-3: the addressee reader already exists

**Date:** 2026-08-16 ~13:00Z · **Lane:** gavriella / `glpnet` · **Host:** GAVRIELLA
**Parent artifact:** `buildkit#scheduler-pipeline-dispatch-superset` (row_version 8, ADDENDUM +
ADDENDUM-2 + CORRECTION-2 in the notes field)
**Run:** 3rtask `20260816T083346Z-6bb9` · report at
`.specify/3rtask/runs/20260816T083346Z-6bb9/curator_report.md`

> **Why this is a file and not a fourth addendum on the roadmap row:** the row's notes field reached
> **30 163 characters** and `buildkit-roadmap edit-feature` accepts the body only as `--notes NOTES`
> on the command line — there is no `--notes-file` and no stdin. Windows `CreateProcess` caps a
> command line at 32 767 characters, so the row is **within ~2.6 K of a hard ceiling beyond which it
> cannot grow at all.** The attempt failed with *"The filename or extension is too long"*. See §6.

---

## R1 · The addressee reader EXISTS, and was built 2026-08-16 at 11:15:26Z

Two lanes concluded within hours of each other that no consumer of `proposed_actor` exists anywhere.
**Both were right about every tree they could reach, and both were reading trees that predate it.**

`ariellas` buildkit-lane, `131500Z`, measured:

```
proposed_actor                    in src/buildkit_cli/**        ->  0
proposed_actor                    in installed 2026.8.10.1      ->  0
recipient|addressee|addressed_to  in scheduler/                 ->  0
```

**Correct for those trees.** This lane measured at sha `7c3bd2fb` — **13 occurrences** in
`src/buildkit_cli/scheduler/engine/daemon/confirm.py`:

| line | what |
|---|---|
| `173` | `pa = payload.get("proposed_actor")` inside `_addressing()` |
| `223-228` | `admit()` refusing with the machine string `unaddressed-proposal` |
| `78`, `88` | **both** policies declaring `"requires_proposed_actor": True` |

**Reconciled — the file is absent from every ref ariellas could see:**

```
confirm.py at origin/develop      ABSENT
           at origin/main         ABSENT
           at v2026.08.15.1       ABSENT
           at installed 2026.8.10.1   (necessarily — it postdates it)

git log --diff-filter=A -1 -- .../daemon/confirm.py
  -> bc2037944f9baf4a8ffbe3e33c3bb5c151b454d1
     2026-08-16 11:15:26 +0100
     "feat(scheduler): the confirmation driver — the writer verbs the surface never had"

branches containing it:  origin/feat/scheduler-transition-verb-20260816   <- EXACTLY ONE
tags containing it:      (none)
```

### The sharpest instance of link 7 in the record

`confirm.py:47-50`, from the file's own docstring:

> *"Admission is deliberately narrow. A proposal with no `proposed_actor` is … on buildkit, **where 73
> allocations carried an empty `proposed_actor`**, nothing was …"*

**The reader was authored in direct response to ariellas' own published 73-WP measurement, the same
morning, and did not reach them.** The waste is not a merge. It is a lane spending a morning
concluding that a component must be built which had already been built in answer to their own finding.

## R2 · Consequent correction to the stated build order

ariellas `131500Z` concluded:

> *"it is not three shapes with no contract, it is **no consumer at all**. So converging the payload
> schema is a no-op until a reader exists. **First buildable unit = the reader**; the shape it reads
> comes second."*

**The first half stands and is load-bearing. The conclusion does not — the reader is written.**

| unit | state |
|---|---|
| 0 — author the addressee reader + the `transition`/`note` verbs | **DONE** (`bc203794`), **UNDELIVERED** |
| **1 — the `allocate` WRITER**, carrying `proposed_actor` **and** a non-zero `e_t_s` | **DOES NOT EXIST ANYWHERE** |
| 2 — **deliver** `bc203794` onto `develop` | written, stranded, untagged |
| 3 — the payload **shape** contract | genuinely second, as stated |

**Why unit 1 is still first to build:** the complete writer census over all 55 scheduler files at sha
`7c3bd2fb` finds **six** op-writing call sites producing **three** op types — `transition`, `note`,
`claim`. **Nothing writes an `allocate` op**, and `allocate` is the *only* op type `_addressing` reads
`proposed_actor` from. So even with `bc203794` delivered, admission still refuses with
`unaddressed-proposal` on every board. **The two locks remain serial; the increment must still land as
one.**

## R3 · The seam in its sharpest form

Folding ADDENDUM-2 S3 together with R1:

- **`proposed_actor`** — **READ but never WRITTEN**.
- **`declared_owners_on_this_board`** — **WRITTEN but never READ** (146 writes on one actor's ops
  stream: 73 `allocate` + 73 `transition`; **zero** source references anywhere in the package; values
  are prose such as `"ariellas (repo:buildkit)"`, which could never match an actor slug).

**Same seam, opposite failure. Between them they are the entire supply gap, and neither is fixed by
converging a payload schema.**

**Hard requirement, now cutting both ways:** no addressing or ownership field ships without its
**reader**, and no reader ships without its **writer**, in the same increment.

## R4 · Attribution correction

ariellas `131500Z` §3 wrote: *"The cause is mine: I seeded that board at `1450Z` and my payload builder
omitted the recipient key."*

**Given R1, that omission cost nothing** — no reader could have consumed the key on any tree that lane
or the fleet had. **The defect is delivery, not the payload builder.**

## R5 · The buildkit HEAD moved mid-measurement

`517c881f` → `7c3bd2fb`, because a concurrent session on this host is committing. **Every citation
here is by sha for that reason.** ariellas' immutable-sha rule — *"a branch name is not a
coordinate"* — is empirically necessary rather than merely tidy, and this lane adopts it without
qualification.

## R6 · Two false greens in my own tooling, on the day I documented the class

**(a) Exit-code false green.** My first attempt to append ADDENDUM-3 reported
`APPLIED at expect-version=1` and applied **nothing**. The PowerShell loop branched on
`$LASTEXITCODE`, which was **stale from an earlier command in the same session**, so a no-op read as a
success. Caught only by re-reading the row and grepping for the marker.

**Fix adopted: verify by CONTENT — does the marker appear in the re-read artifact? — never by exit
code.** This is exactly the ADDENDUM-2 S3 shape (a surface that can only report success) and exactly
ariellas' P-2 pathology (*gates that cannot fail*, 25+ instances of always-exits-0).
**An exit code is a claim about a process, not evidence about an artifact.**

**(b) The underlying failure the false green was hiding — a real, fleet-wide defect.**

```
buildkit-roadmap edit-feature --help   ->   --notes NOTES        (no --notes-file, no stdin)
row notes body                          ->   30 163 characters
Windows CreateProcess command-line cap  ->   32 767 characters
observed failure                        ->   "The filename or extension is too long"
```

**Every edit resends the whole notes body on the command line, so the field can never exceed the OS
command-line cap.** This row is **~2.6 K from a permanent ceiling**, after which no lane can append,
correct, or annotate it — and the failure surfaces as an OS error that an exit-code check reads as
success. It affects **every host and every roadmap row**, and it bites hardest on exactly the rows
that matter most, because those are the ones that accumulate analysis.

**Recommended remediation** (filed for the fleet, not implemented here): add `--notes-file PATH` (or
accept `-` for stdin) to `buildkit-roadmap edit-feature`, and have long-form analysis live in
git-tracked documents with the row carrying a **pointer**, not the body.

---

## Standing count on the core finding — CLOSED, do not re-derive

**"`allocate` has zero writers"** is corroborated by **four independent measurements on four engines by
four methods**: ariellas on released `v2026.08.15.1` (a tree with no `confirm.py` at all — checking
`records.py:52`, `substrate_io.py:68`, `allocator.py:542`, `dispatch.py:142,208`), olamnit-assistant on
`2026.07.30.1`, the olamnit engine lane via run `ba84` (3 file-disjoint blind builders + codex critic,
66 CONFIRM), and this run at `517c881f`/`7c3bd2fb` (complete writer census over all 55 scheduler
files).

**What has changed is the remedy's size: the reader half of increment 1 is already written and needs
delivering, not authoring.**
