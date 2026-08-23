# `/bk-flow` SKILL.md — promotion-ready draft (M01 Phase-A deliverable)

**Date** 2026-08-23 · **Author** olamnit · **Source of contract** 3rtask run `20260823T140508Z-227d`, builder-1 (slice s1-flow-target), 33 CLI-cited claims, codex-Critic CONFIRMED.

**Why this exists.** The M01 migration plan (`docs/research/m01-bkmarathon-to-bkflow-migration-plan-2026-08-23.md`) found that **no `.claude/skills/bk-flow/` doc exists** — `/bk-flow` cannot be invoked as a slash-command, only the CLI. Authoring this SKILL.md is a Phase-A prerequisite (no shared-registry mutation, no engineer ruling needed).

**Lane / promotion.** bk-flow is a **buildkit-owned** capability (like `bk-marathon`, whose `metadata.source` is `templates/commands/buildkit-marathon.md`). This draft is therefore staged here for **promotion into the buildkit repo** as `templates/commands/buildkit-flow.md`, from where `buildkit-deploy` renders it to every target's `.claude/skills/bk-flow/SKILL.md`. Do **not** hand-place it in glpnet's `.claude/skills/` — that would create an un-sourced skill buildkit does not manage.

**Contract fidelity note.** Every verb/flag below is transcribed verbatim from `bk-flow --help` + each subcommand `--help` (bk-flow `2026.08.14.1`). Where `--help` does **not** state a semantic (notably per-verb idempotency of the mutating verbs), this draft says so rather than inferring it — a stated gap, per the same E3/E14 discipline that produced it. Those gaps should be closed against the bk-flow source before this ships.

---

```markdown
---
name: "bk-flow"
description: "Board→pipeline bridge (advisory; part of buildkit). Turns a shared CRDT scheduler board into per-work-package pipeline action: poll a work-package's dispatchability with a reason (read-only), append an add-wins claim to your own actor log, bind a claimed WP to a feature + marathon run, report its done transition, and read per-phase takt for that feature's marathon run against its target bands. Never invokes a pipeline command itself — it tells you which one to run. Reads default to the shared board root (R1 sched_root / coop/sched); every write goes to the calling actor's own single-writer log. A board is a coordination channel shared across repos, so an envelope naming a different repo is refused rather than acted on. Advisory & passive: it never auto-invokes a /buildkit-* command and is NOT a canonical pipeline stage."
argument-hint: "[a natural-language request, or a subcommand: poll|claim|open|report|takt|version]"
compatibility: "Requires a resolvable scheduler board root (R1 sched_root / coop/sched) and, for open/takt, a buildkit feature + marathon run"
metadata:
  author: "buildkit"
  source: "templates/commands/buildkit-flow.md"
user-invocable: true
disable-model-invocation: false
---


## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty). It is either a
natural-language request ("what can I dispatch?", "claim WP-123", "open this WP on my feature",
"mark it done", "how's my takt?") or a `bk-flow` subcommand. If empty, summarise the surface
below and ask what they want.

## What this does

`/bk-flow` is the **bridge between the scheduler board and the per-feature pipeline**. It reads a
work-package's (WP) dispatchability off the shared CRDT board and, on the engineer's say-so, records
the actor's claim → feature/marathon binding → done transition on that board. It is **advisory &
passive**: it *tells you which pipeline command to run*, but **never** runs
`specify/clarify/plan/tasks/analyze/implement` or any ship/roadmap command itself, and mutates no
pipeline/DBOS state. It does not manage marathon run state — `open` *binds a WP to an existing
marathon run*; the durable run harness remains `/bk-marathon`.

## Surface

**Read (no board write)**
- `poll` — per-WP dispatchability with a reason (read-only). Reports what is dispatchable and why.
- `takt [wp_id]` — per-phase takt for this feature's marathon run against its target bands
  (read-only). `wp_id` is optional; its link resolves the feature. `--feature` overrides any
  resolved value.
- `version` — report the bk-flow capability version.

**Write (append-only to the caller's own single-writer log)**
- `claim <wp_id>` — append one **add-wins** claim to your own log.
- `open <wp_id>` — bind a claimed WP to a feature + marathon run. `--feature` is **required the
  first time** a given WP is opened.
- `report <wp_id>` — append one transition `to_state=done`.

**Common flags** (every subcommand): `--root ROOT` (board root; default `R1 sched_root /
coop/sched`), `--actor ACTOR` (your actor slug, or env `SCHEDULER_ACTOR`; a `HOST/lane` form is
accepted), `--json`, `--quiet`. The three write verbs (`claim`/`open`/`report`) additionally accept
`--dry-run` (**compute everything, write nothing**) — always dry-run a write first. `open`/`report`/
`takt` accept `--repo OWNER/NAME`: when given, a board envelope naming a **different** repo is
**REFUSED** rather than acted on (a board is a coordination channel shared across repos).

**Idempotency (help-stated only).** `poll`/`takt`/`version` are labelled read-only. The write verbs
are append-only to the caller's own log; the `--help` text does **not** state whether re-running
`claim`/`open`/`report` on the same `wp_id` is a no-op, an error, or a new append — treat that as
**unspecified** and use `--dry-run` to preview. *(Gap to close against the bk-flow source before
relying on re-run safety.)*

## Typical flow

```
bk-flow poll --actor <you>                       # what's dispatchable, and why
bk-flow claim <wp_id> --actor <you> --dry-run     # preview, then drop --dry-run
bk-flow open  <wp_id> --actor <you> --feature <F> --dry-run   # binds WP → feature + marathon run
# … run the feature's pipeline via /bk-marathon + the /buildkit-* commands …
bk-flow takt  <wp_id>                              # per-phase takt vs target bands
bk-flow report <wp_id> --actor <you> --dry-run    # then drop --dry-run to record done
```

## Boundaries

Advisory only. `/bk-flow` **never** auto-invokes a `/buildkit-*` command, is **not** a pipeline
stage, makes no pipeline/DBOS mutation, and refuses a cross-repo board envelope. Every write lands
in the calling actor's own single-writer log on the shared board; run the recommended next pipeline
command yourself (via `/bk-marathon` + the `/buildkit-*` toolchain).
```

---

## Open items before this ships (hand to the bk-flow source owner)

1. **State per-verb idempotency** for `claim`/`open`/`report` in the source help + this doc (currently unspecified — the only honest gap).
2. **Confirm exit-code table** (this draft omits it; bk-marathon documents `0/1/2/3` — bk-flow's `--help` did not surface one).
3. **Confirm `poll`/`report` preconditions** (must a WP be claimed before `open`? opened before `report`? — not stated in `--help`).
4. On promotion, register `metadata.source = templates/commands/buildkit-flow.md` and let `buildkit-deploy` render it — do not commit the rendered skill into glpnet by hand.
