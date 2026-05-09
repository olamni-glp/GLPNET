# Contract — `prereq-patterns/<name>/applicability.md` format

## Purpose

`applicability.md` answers "is this pattern usable from my consumer (DBOS / SQLAlchemy / library X / language Y) and, if so, what changes from the vanilla setup?" It is the file an integrator opens after deciding the pattern's purpose (from `description.md`) is the right tool.

## File-level shape

```text
# Applicability — <Pattern Name>

<optional 1-paragraph framing: what kinds of consumers this pattern targets>

### <consumer-1>

<one-line "what changes from vanilla">

<optional further notes — adaptation steps, gotchas, version notes, monkey-patches required>

### <consumer-2>

...

### Other consumers   (optional)

<free-form notes about partial / experimental / untested fits>
```

## Section rules

| Element | Rule |
|---|---|
| H1 | Exactly one. `# Applicability — <Pattern Name>`. The em-dash form is recommended (matches `directory.md` separator) but not enforced. |
| Per-consumer sub-sections | Use `### <consumer-name>` (H3). Order MUST match the spec's enumeration order for the seed pattern when applicable (FR-010 lists `DBOS`, `SQLAlchemy`, `Alembic`, `psycopg` for pglite). For new patterns, author chooses the order — most-common consumer first is the default. |
| Per-consumer minimum content | At minimum a one-line statement of "what changes from a vanilla setup". Example: `Use `engine_kwargs` from \`pglite_engine_kwargs(application_name=...)\`; pool_size=1, prepare_threshold=None.` |
| `### Other consumers` | Optional. Free-form. Use for partial / experimental / untested fits or for consumers the author doesn't have first-hand experience with. |
| H2 | Avoid H2 in `applicability.md` — H1 + multiple H3 keeps the reading flow flat. Use H3 sub-sections to introduce per-consumer notes. |

## Triviality

A pattern that is genuinely universally applicable — same usage shape across all consumers, no per-consumer adaptation needed — MUST still have an `applicability.md` file. Its body is exactly one explanatory line, and it has no per-consumer sub-sections:

```text
# Applicability — <Pattern Name>

Universally applicable: this pattern has no domain-specific adaptations.
```

## Examples

### Per-consumer (typical)

```text
# Applicability — pglite

This pattern targets Python consumers that talk to Postgres via the standard
wire protocol. The bridge surfaces PGlite as a TCP Postgres endpoint, so any
psycopg-driven library should connect; the only adaptation is the engine /
pool config that enforces the single-session invariant.

### DBOS

Pass `db_engine_kwargs=pglite_engine_kwargs(application_name='dbos')` to
`DBOSConfig`. Additionally, monkey-patch DBOS's migration_one to strip
`CREATE EXTENSION uuid-ossp` (PGlite ships without uuid-ossp) — see
`sources.md` for the patch helper.

### SQLAlchemy

Construct `Engine` with `engine_kwargs = pglite_engine_kwargs(application_name='your-app')`.
For Flask-SQLAlchemy use the NullPool variant in the LMS reference
(`patch_entry.py`) instead of the default QueuePool helper — see `sources.md`.

### Alembic

Use the apply-revision helper from `ulpani_lms_apply_revision.py`'s
`_build_engine()` reference — `create_engine(..., poolclass=NullPool,
isolation_level='AUTOCOMMIT', connect_args={'prepare_threshold': None}, ...)`.
Do **not** rely on Alembic's default engine config; it will deadlock.

### psycopg

Direct psycopg consumers MUST pass `prepare_threshold=None` to
`psycopg.connect()` and MUST serialise their own access to one connection
at a time.
```

### Trivial (rare)

```text
# Applicability — universally-applicable-pattern

Universally applicable: this pattern has no domain-specific adaptations.
```
