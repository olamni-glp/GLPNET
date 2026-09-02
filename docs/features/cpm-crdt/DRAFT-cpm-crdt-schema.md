<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# 🔴 WITHDRAWN 2026-09-01T16:30Z — THIS IS NO LONGER A LIVE DRAFT

**Do not implement, pilot or cite this as a competing schema.**

By the time this was written there were **seven** live CPM-CRDT / YX-YPM drafts across the
fleet (shiras/mstack, shiras/yngenios-linux v0.1 and v0.2, gavriella/qhstate,
gavriella/lejepa, olamnit, and this one). The engineer asked for convergence to a **unanimous
hardened superset**; a seventh draft makes unanimity less reachable, not more, and
@olamnit/mstack had already named the two-draft case a fork at `20260901T1120Z`.

**The base is `.specify/standards/BK-CPM-1-DRAFT-crdt-schema.md`.** This lane's content is
contributed there as an appended contribution, not as a rival document. The measured findings
worth carrying forward — GLP has no version concept (measured, including the alternative
encodings a red-team proposed), the six ledger kinds, and the two constraints earned from the
takt lake — are summarised in `D:/coop/ACK-SWEEP-20260901T1630Z-gavriella-glpnet-...md` §3.

The original text is kept below **for provenance only**.

---

# DRAFT — **CPM-CRDT**: cross-ecosystem package-management state, history, chaining and upgrade proposals

**Drafted by** `gavriella/glpnet @ GAVRIELLA` · **UTC** 2026-09-01T13:45Z
**Status** 🟡 **DRAFT FOR CONVERGENCE.** Nothing implemented. Per engineer direction:
draft → COOP share → ACK → converge to a **unanimous hardened superset** → pilot →
`/bk-codify` → one scored, promoted `/bk-roadmap` feature → built in **buildkit** next era.

**Engineer ask this answers:** *"a hardened superset of a CRDT schema for recording the CPM
current state, [version] history and archiving/chaining point, and proposed and agreed package
upgrades. This must work for .NET from the beginning but must also work for Python, JavaScript,
Dart, GLP, GLPNET, TypeScript, C and C++, Go and Rust."*

---

## 0 · Why a CRDT and not a file

`Directory.Packages.props` already makes a version exist **once per repo**. It does not, and
cannot, answer any of these:

| question | why the props file cannot answer it |
|---|---|
| what did this repo pin **last week**? | the file is overwritten; git history is per-repo and not queryable across the fleet |
| are four hosts pinning the **same** version of the same package? | no host can read another host's props file |
| who **proposed** this upgrade, who **agreed**, and when did it land? | a props file records the outcome, never the decision |
| which upgrades are **in flight** right now, so two lanes do not both do it? | nothing to look at |
| what is the **chaining point** — the last state everyone agreed on before divergence? | no notion of it exists |

And the fleet has already paid for the absence twice this week: three packages pinned at two
versions in one repo, and a floating `10.*` that would silently defeat the .NET 11 mandate.

## 1 · Design constraints (inherited deliberately, not re-derived)

These are taken **verbatim from BK-STD-3** so the fleet runs one CRDT discipline, not two.
Re-deriving them would be the standards-fork this document exists to avoid.

| # | constraint |
|---|---|
| C1 | Grow-only, append-only, per-writer. No host ever rewrites another host's file. |
| C2 | Merge by union on a total order. Conflicts impossible by construction, not by locking. |
| C3 | Partition keys are a **CLOSED vocabulary**. An undeclared value is refused at write. |
| C4 | Every fact carries **`repo` AND `host`**, as partition keys *and* columns. |
| C5 | **Absence is reported, never rendered as zero.** No data is `NO DATA`, not `0`. |
| C6 | Writers are lease-free. A record must never block a build. |
| C7 | **Lamport-ordered.** `ts_utc` is for humans and is never an ordering key. |

🔴 **C4 addendum, and it is the lesson BK-STD-3 paid for:** `repo` MUST be a **declared slug
from a closed registry**. In the takt lake the same field holds a host-local absolute path, so
39 keys denote 15 repos and no cross-host comparison is possible at all. **A writer passing an
absolute path must be refused at write.** Do not repeat it here.

## 2 · The cross-ecosystem problem, stated honestly

Nine ecosystems, and they do not agree on anything:

| ecosystem | manifest | lock | version syntax | scope of a "central" pin |
|---|---|---|---|---|
| .NET | `*.csproj` | `packages.lock.json` | SemVer, `[1.0,2.0)`, `10.*` | `Directory.Packages.props` (repo) |
| Python | `pyproject.toml` | `uv.lock` / `poetry.lock` | PEP 440, `~=`, `>=` | none (per-project) |
| JavaScript / TypeScript | `package.json` | `package-lock.json` | SemVer `^`, `~` | workspaces / `overrides` |
| Dart | `pubspec.yaml` | `pubspec.lock` | caret `^1.2.3` | `dependency_overrides` |
| Go | `go.mod` | `go.sum` | SemVer + pseudo-versions | `replace`, workspaces |
| Rust | `Cargo.toml` | `Cargo.lock` | SemVer caret | `[workspace.dependencies]` |
| C / C++ | `vcpkg.json`, `conanfile` | varies | varies wildly | manifest + baseline |
| GLP / GLPNET | `self.glp` module graph | none | **no version concept at all** | — |

🔴 **GLP has no versioning concept.** Any schema that assumes one silently excludes it. So the
record must permit `version_scheme = "none"` with a **content hash** standing in for a version —
otherwise GLP is "supported" on paper and unrepresentable in practice.

**The superset rule:** the schema stores the ecosystem's **own** version string verbatim in
`version_raw`, plus a **normalised** `version_norm` where one exists, plus the `version_scheme`
that says how to read it. Never normalise into a lie; never store only the raw string, or
cross-ecosystem comparison dies exactly as it did for `repo`.

## 3 · Layout

```
<cpm-root>/cpm/
  schema_version=<N>/
    kind=<KIND>/          # CLOSED vocabulary — §4
      repo=<repo-slug>/   # DECLARED SLUG, never a path (C4 addendum)
        host=<host-slug>/
          date=<YYYY-MM-DD>/
            <host>-<lane>-<lclock>-<ulid>.parquet
```

## 4 · The closed `kind` vocabulary

| kind | grain | one row per | answers |
|---|---|---|---|
| `pin` | the CURRENT declared version of one package in one repo | (repo, ecosystem, package) at an lclock | "what is pinned now" |
| `pin_history` | a SUPERSEDED pin, written when a pin changes | every transition | "what did we pin last week" |
| `proposal` | a proposed upgrade, not yet agreed | (package, from, to) | "what is in flight" |
| `decision` | an ACK/NAK on a proposal | (proposal_id, actor) | "who agreed, and when" |
| `chain_point` | a signed snapshot everyone agreed on | fleet agreement event | "the last common state" |
| `violation` | a detected breach (drift, floating, banned) | detection | "what is wrong now" |

`pin_history` is separate from `pin` deliberately: a reader asking "what is pinned" must not have
to fold the whole history, and a reader auditing change must not have to guess which row is current.

## 5 · Common envelope (every row, every kind)

| column | type | note |
|---|---|---|
| `schema_version` | `INT` | starts at 1 |
| `repo` | `VARCHAR` | **declared slug**, NOT a path — refused at write otherwise |
| `host` | `VARCHAR` | declared slug |
| `lane` | `VARCHAR` | `<actor>-<repo>` |
| `actor` | `VARCHAR` | the writer |
| `lclock` | `BIGINT` | monotonic per writer (C7) |
| `ts_utc` | `TIMESTAMP` | humans only, never ordering |
| `record_id` | `VARCHAR` | ULID — the union key |
| `measured` | `BOOLEAN` | C5 |
| `unmeasured_reason` | `VARCHAR` | NULL iff `measured` |

🔴 **Every column's TYPE is pinned, not only its name.** In the takt lake the `reason` column is
VARCHAR in some files and JSON in others, so a plain `GROUP BY reason` fails depending on which
file sorts first. A reader must pass `union_by_name := true` **and refuse loudly on a type
conflict** rather than adopting the first file's schema.

### 5.1 `kind=pin` / `kind=pin_history`

| column | type | note |
|---|---|---|
| `ecosystem` | `VARCHAR` | CLOSED: `dotnet｜python｜npm｜dart｜go｜rust｜cpp｜glp` |
| `package` | `VARCHAR` | the ecosystem's own identifier, verbatim |
| `version_raw` | `VARCHAR` | **verbatim**, e.g. `^1.2.3`, `10.*`, `~=2.1` |
| `version_norm` | `VARCHAR` | normalised, NULL when the scheme has none |
| `version_scheme` | `VARCHAR` | CLOSED: `semver｜pep440｜caret｜gomod｜none` |
| `is_floating` | `BOOLEAN` | **derived at write**, not trusted from the caller |
| `content_hash` | `VARCHAR` | for `version_scheme='none'` (GLP) — the version stand-in |
| `manifest_path` | `VARCHAR` | repo-relative, so a finding is actionable |
| `is_central` | `BOOLEAN` | declared centrally vs per-project — measures CPM adoption itself |
| `supersedes` | `VARCHAR` | `record_id` of the pin this replaces (`pin_history`) |

### 5.2 `kind=proposal` / `kind=decision`

`proposal` adds `proposal_id` (ULID), `from_version`, `to_version`, `rationale`, `blast_radius`
(repos + refs affected, measured), `expires_at`.
`decision` adds `proposal_id`, `verdict` (CLOSED: `ack｜nak｜abstain`), `rationale`,
`decided_by` — and **unanimity is DERIVED by reading, never asserted by a writer**: a proposal is
agreed iff every repo in its blast radius has an `ack` and none has a `nak`.

### 5.3 `kind=chain_point`

`chain_point_id`, `covers_repos[]`, `merkle_root` over the folded `pin` set, `signature`,
`prev_chain_point_id`. This is the **archiving/chaining point**: an append-only chain of agreed
fleet states, each naming its predecessor, so "the last state everyone agreed on" is a lookup
rather than an argument, and history can be archived below a chain point without losing it.

### 5.4 `kind=violation`

`violation_kind` (CLOSED: `drift｜floating｜banned_version｜not_central｜ecosystem_eol`),
`severity`, `detail`, `manifest_path`, `first_seen_lclock`.
`ecosystem_eol` is what makes the .NET 11 mandate *machine-checkable*: `net8.0｜net9.0｜net10.0`
in our own code is a violation, while a **vendored** net10 component behind a net11 wrapper is a
declared exception carrying its wrapper's path.

## 6 · Merge semantics

G-Set of rows keyed by `record_id` (ULID). Merge = set union across partitions — commutative,
associative, idempotent. Total order `(lclock, host, record_id)`. **No deletes, no updates**; a
correction appends with `supersedes`, and the latest by `(lclock, host)` wins at read time. Two
hosts cannot mint the same `record_id` and no host writes another's partition, so conflicts
cannot occur.

## 7 · The three queries this must serve

```sql
-- 7.1  IS ANY PACKAGE PINNED AT TWO VERSIONS ANYWHERE IN THE FLEET?
SELECT ecosystem, package, count(DISTINCT version_norm) AS versions,
       array_agg(DISTINCT version_norm) AS which, array_agg(DISTINCT repo) AS repos
FROM read_parquet('<root>/cpm/schema_version=1/kind=pin/**/*.parquet', hive_partitioning := 1,
                  union_by_name := true)
GROUP BY ecosystem, package HAVING count(DISTINCT version_norm) > 1;
```

```sql
-- 7.2  EVERY FLOATING VERSION, FLEET-WIDE  (the ban, made checkable)
SELECT repo, host, ecosystem, package, version_raw, manifest_path
FROM read_parquet('<root>/cpm/schema_version=1/kind=pin/**/*.parquet', hive_partitioning := 1,
                  union_by_name := true)
WHERE is_floating ORDER BY repo, package;
```

```sql
-- 7.3  CPM ADOPTION ITSELF  (are we actually converging, or just claiming to?)
SELECT repo,
       count(*) FILTER (WHERE is_central)     AS central_refs,
       count(*) FILTER (WHERE NOT is_central) AS per_project_refs
FROM read_parquet('<root>/cpm/schema_version=1/kind=pin/**/*.parquet', hive_partitioning := 1,
                  union_by_name := true)
GROUP BY repo ORDER BY per_project_refs DESC;
```

## 8 · Pilot plan and exit criteria

1. **Do not pilot before the repo-slug registry exists.** Piloting first bakes a path-keyed
   split into a partition key, where it is far more expensive to undo — the exact mistake the
   takt lake is now living with.
2. Pilot on **.NET only**, two repos, two hosts. Exit criteria: 7.1 returns the drift each repo
   already knows it has; 7.2 returns every floating version and no false positives; 7.3's
   `per_project_refs` falls to 0 in a repo that has adopted CPM.
3. **Then** add one non-.NET ecosystem — **GLP first, not Python.** GLP is the hardest case
   (`version_scheme='none'`), and a superset that survives its hardest case is a superset. Adding
   Python first would prove only that two SemVer-ish ecosystems agree.

## 9 · Open questions for the fleet — please answer these in your ACK

- **Q1** Who owns the `repo-slug` registry, and is it shared with BK-STD-3's, or separate?
  *(This draft assumes SHARED — one registry, two consumers.)*
- **Q2** Is `pin` per-manifest or per-repo-per-package? Per-manifest is more honest for
  ecosystems with no central pin (Python, Dart); per-repo is cheaper to query.
- **Q3** Does a `chain_point` require unanimity across **all** repos, or only the blast radius?
- **Q4** For C/C++: is vcpkg the assumed manifest, or must conan be a first-class peer?
- **Q5** Is `ecosystem_eol` a violation kind here, or does it belong to a separate policy record?
- **Q6** Should this share BK-STD-3's `<root>`, or be its own lake? *(Draft assumes its own
  `kind` tree under a shared root, so one reader and one registry serve both.)*

## 10 · Contributions (grow-only — append, do not edit above)

<!-- Each lane appends its own section. Do not modify another lane's section. -->

### gavriella / glpnet — 2026-09-01

Authored §0–§9. Measured basis from glpnet at CPM adoption on this date: **13 packages, 55
references, 3 packages pinned at 2 versions** (`Microsoft.NET.Test.Sdk` 17.11.1/17.14.1, `xunit`
2.9.2/2.9.3, `xunit.runner.visualstudio` 2.8.2/3.1.4), **0 floating**, **31/31 projects build
clean** under `Directory.Packages.props`. Every drifted reference sat in one project
(`spike/antlr4-glp-grammar/parity/tests`), which is the shape §7.1 must surface: **drift
concentrates in the project nobody touches**, so a per-project bump would have looked like a fix
and left the mechanism intact.

**Cannot** contribute rows for Python, npm, Dart, Go, Rust or C/C++ conventions on other hosts —
this lane has not measured them, and asserting them from here would be inference, not
measurement. §2's table is drafted from documentation and is explicitly marked for correction by
the lanes that own those ecosystems.
