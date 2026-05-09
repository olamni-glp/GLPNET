# Contract — `prereq-patterns/directory.md` format

## Purpose

`directory.md` is the index of the catalog. A reader scanning it must be able to tell, in seconds, whether any pattern in the catalog fits a given need.

## File-level shape

```text
# Pattern directory

<intro paragraph — 1-3 sentences>

## Patterns

- **<name-1>** — <description-1>.[ <status_suffix>]
- **<name-2>** — <description-2>.[ <status_suffix>]
- ...
```

## Line-level shape

Every pattern line MUST conform to:

```text
- **<name>** — <description>.[ <status_suffix>]
```

| Element | Rule |
|---|---|
| Bullet | `- ` (hyphen + space) |
| Name | Bolded, exactly matches the pattern's sub-directory basename. Lowercase, hyphen-separated. |
| Separator | ` — ` (space, em-dash U+2014, space). Not `--`, not `-`. |
| Description | One sentence. Sentence-cased. Combines purpose + applicability hint. **≤ 25 words** excluding the optional status suffix. Period-terminated. |
| Status suffix | Empty when status = `active`. Otherwise: ` (draft)` or ` (superseded by <pattern-name>)`. Single ASCII space before the opening parenthesis. |

## Examples — VALID

```text
- **pglite** — Local Postgres-compatible DB via pglite + Node bridge with single-session pool, for SQLAlchemy / Alembic / DBOS / psycopg consumers.
- **redis-stub** — In-memory Redis-protocol server for offline integration tests of cache-aware code paths. (draft)
- **pglite-v1** — Earlier pglite pattern using direct in-process API instead of the wire bridge. (superseded by pglite)
```

## Examples — INVALID

| Line | Why invalid |
|---|---|
| `- pglite — ...` | Name not bolded. |
| `- **pglite** -- ...` | Wrong separator (`--` instead of ` — `). |
| `- **pglite** — Local Postgres-compatible database with bridge for the various Python ORM and migration consumers in the project including DBOS, SQLAlchemy, Alembic, psycopg and probably more.` | Description > 25 words. |
| `- **pglite** — local DB.` | Sentence not capitalised. |
| `- **pglite** — local DB (active)` | Status suffix `(active)` not allowed; `active` is implicit. |
| `- **pglite** — local DB (superseded)` | `superseded` suffix MUST name the replacement: `(superseded by <name>)`. |

## Ordering

Pattern lines appear in the order they were added to the catalog (chronological). A future revision MAY introduce alphabetical ordering if the list grows long enough that chronological order becomes unhelpful — that is a separate decision and not part of this contract.

## Headings

The file MUST contain exactly one H1 (`# Pattern directory`) and exactly one H2 (`## Patterns`). No other headings. The H2 immediately precedes the bullet list of patterns.
