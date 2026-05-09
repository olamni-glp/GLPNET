# Contract — `prereq-patterns/<name>/description.md` format

## Purpose

`description.md` answers two questions a reader has before they decide to use the pattern:

1. What does this pattern actually produce?
2. Why does it matter — what load-bearing constraint or insight does it encode?

It also carries the pattern's lifecycle `Status:` line.

## File-level shape (mandatory)

```text
# <Pattern Name>

Status: <state>

<body — recommended sections below>
```

| Element | Rule |
|---|---|
| H1 title | Human-friendly name. Title-cased. Pattern's directory basename (lowercase) is **not** required to match exactly; e.g. directory `pglite` may render as `# pglite` or `# PGlite + bridge` per author taste. |
| Status line | Second non-blank line. Literal prefix `Status: ` followed by exactly one of: `active`, `draft`, `superseded by <pattern-name>`. No surrounding markdown emphasis. No trailing period. |
| Body | Free markdown. Recommended structure below. |

## `Status:` line — exhaustive forms

| Form | When |
|---|---|
| `Status: active` | Default. Pattern is the canonical, current implementation in this catalog. |
| `Status: draft` | Pattern is being authored / reviewed and not yet considered safe to cite from a feature spec. |
| `Status: superseded by <pattern-name>` | A successor pattern has replaced this one. `<pattern-name>` is the name of an existing sub-directory in `prereq-patterns/`. The superseded pattern's directory is **retained** (not deleted) so historical citations remain resolvable. |

## Recommended (not enforced) body sections

```text
## What this produces

<1-3 paragraphs describing the concrete artefact(s) a feature gets when it adopts this pattern. State the deliverable, not the implementation steps.>

## Why it matters

<1-3 paragraphs naming the load-bearing constraint, insight, or non-obvious correctness rule the pattern encodes. This is the "if you skip this pattern and roll your own, here is what you will get wrong" section.>

## How a feature uses this pattern

<1-2 paragraphs sketching the integration shape: what the consumer copies, what they configure, what they leave alone. Cross-reference applicability.md for per-consumer detail and sources.md for source citations.>
```

These three sections are recommended because they map to the three predictable questions a reader has. They are not enforced (some patterns will legitimately need a different shape).

## Triviality

A `description.md` whose substantive content collapses to a single statement (uncommon, but possible — e.g. a pattern whose entire content is "do exactly what RFC X.Y says") MUST still contain the H1 + `Status:` line + a single explanatory body line. It is never empty and never reduces to just the header.

## Cross-references

`description.md` MAY link to `applicability.md` and `sources.md` (sibling files) using relative links: `[applicability](./applicability.md)`, `[sources](./sources.md)`.

## Examples

### Minimal valid `description.md`

```text
# minimal-pattern

Status: draft

This pattern's content is still being authored. Do not cite from feature specs yet.
```

### Typical valid `description.md` (sketch)

```text
# pglite

Status: active

## What this produces

A local-machine Postgres-compatible database service callable from any
psycopg / SQLAlchemy / Alembic / DBOS code, …

## Why it matters

PGlite is a single-session WASM Postgres. …

## How a feature uses this pattern

Copy the four files cited in `sources.md` …
```
