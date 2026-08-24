---
name: "bk-flow"
description: "The board→pipeline bridge (spec 031-bk-flow-bridge): the missing arrow between what the scheduler board says to work on and how the buildkit pipeline gets it done. Poll per-WP dispatchability with a per-packet reason (no packet is skipped silently), claim a packet into your own add-wins op log, open it — binding the WP to a feature, seeding a marathon run and switching the active-feature pointer — report it done through the authoritative board fold, and read per-phase takt against the run's own recorded target bands. Composes rather than re-implements: the CRDT substrate, the R12 add-wins board fold, capability fit, record-schema validation, the marathon run lifecycle and the active-feature pointer are all imported verbatim. Advisory, additive and single-writer: it appends only to the invoking actor's own op log, never renames/deletes/rewrites an existing stream, refuses loudly on a root that is not a board rather than reporting a plausible-looking empty one, and PRINTS the next pipeline command instead of invoking it — it is not a canonical pipeline stage and never auto-invokes a /bk-* command."
argument-hint: "[a natural-language request, or a subcommand: poll|claim|open|report|takt|version]"
compatibility: "Requires spec-kit project structure with .specify/ directory"
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
natural-language request ("what can I work on?", "claim this packet", "start work on wp-…",
"how are we doing on takt?") or a `bk-flow` subcommand. If empty, run `bk-flow poll` and
summarise what is dispatchable, then ask what they want.

## What this does

The scheduler board says **what** to work on; the buildkit pipeline (specify → plan → tasks →
implement → ship → close) is **how** work gets done. Nothing joined them: a work packet could sit
`ready` on the board forever while the pipeline sat idle, and a finished feature never reported
`done` back to the board. `/bk-flow` **is that arrow and nothing else**.

**It consumes `/bk-marathon`; it does not replace it.** `bk-flow open` calls
`marathon.run.open_run` directly, and the marathon run lifecycle is a declared dependency of the
package. There is no migration *away from* marathon — you drive marathon *through* bk-flow.
Anyone framing this as "migrate from marathon to bk-flow" has the direction wrong.

## Surface

Every subcommand accepts `--root ROOT` (board root; default R1 `sched_root` / `coop/sched`),
`--actor ACTOR` (or env `SCHEDULER_ACTOR`; the `HOST/lane` spelling is accepted and normalised on
read), `--json` and `--quiet`. The three writing subcommands also accept `--dry-run` (compute
everything, write nothing).

**Read the board**
- `bk-flow poll` — every work packet with its state and a **per-packet reason**, plus how many are
  dispatchable by you. When nothing is dispatchable it says so explicitly: the reasons above it are
  complete and no packet was skipped silently.
- `bk-flow version` — the bk-flow capability version.

**Take and start work**
- `bk-flow claim <wp_id> [--dry-run]` — append one add-wins claim to **your own** log.
- `bk-flow open <wp_id> --feature <feature-id> [--repo owner/name] [--dry-run]` — bind a claimed WP
  to a feature, seed (or resolve) its marathon run, switch the active-feature pointer, and print
  the pipeline command to run next. `--feature` is required the first time a WP is opened.

**Close the loop**
- `bk-flow report <wp_id> [--repo owner/name] [--dry-run]` — append one transition `to_state=done`.

**Measure**
- `bk-flow takt [wp_id] [--feature <feature-id>] [--repo owner/name]` — per-phase takt for this
  feature's marathon run against its target bands. The feature resolves exactly as `open`/`report`
  do: explicit `--feature` wins, else the wp link, else `.specify/feature.json`. Nothing is
  recomputed here — the numbers come from marathon's own `takt.summarise` over the run's recorded
  steps and its configured target bands, because a second implementation of the same statistic is
  a second chance to disagree with the run's own record.

## Reading a poll

`poll` answers two different questions and prints both, because they disagree in practice:

- **dispatchable** — is the board asking *you* to work this packet?
- **resolvable (the binding gap)** — can the packet be *opened* at all, i.e. does its wp_id resolve
  to a feature? A packet can be perfectly dispatchable and still unopenable; on this repo's board
  20 of 20 wp_ids were unresolvable, including the 6 at `ready`. `poll` states the gap count and
  names the unresolvable ids rather than letting you discover them one refusal at a time. It also
  flags any conflict where the local link and the board envelope name **different** features.

`poll` additionally announces the host fold (so you can see that `olamnit-assistant` was treated as
`olamnit` — and refute it if that is wrong) and warns when substrate lines are quarantined.

## Guards that actually fire

Each of these refuses **before** anything is written, and says that nothing was written:

- **A board envelope naming a different repo.** Pass `--repo owner/name` to `open`, `report` or
  `takt` and an envelope for another repo is REFUSED rather than acted on — a board is a
  coordination channel shared across repos.
- **A reserved substrate id.** The board fold drops reserved ids and prefixes before it dispatches
  on op type, so a claim on one would report success and never appear on any board.
- **An unclaimed or someone else's packet.** `open` refuses both, rather than starting pipeline work
  nobody can see you own.
- **A packet the board is not asking anyone to work.** `open` refuses a state outside the actionable
  set even when you hold it yourself — otherwise a self-claimed packet that had since gone done,
  bounded or escalated would still switch your active feature and seed a marathon run.
- **A `done` the fold would discard.** `report` mirrors every refusal in `derive_board` — a gate
  required but not passed, a declared deliverable without reachable evidence, a provisional packet,
  an escalation-frozen one. Writing such an op would print success, move nothing, and latch the
  packet `already_done` for the whole fleet against a grow-only substrate.
- **An unmintable actor spelling**, and a `--root` that is not a board at all (exit 2), rather than
  a plausible-looking report of zero.

Exit codes: **0** success · **1** refused · **2** the root is not a board. `--json` emits a
`{"schema_version": …, "capability": "bk-flow", …}` envelope on both success and refusal.

## Boundaries

Advisory only. bk-flow **never** invokes a pipeline command — it tells you which one to run, derived
from which artifacts the feature already has (`spec.md` → `/bk-specify`, `plan.md` → `/bk-plan`,
`tasks.md` → `/bk-tasks`, otherwise `/bk-implement`), so a resumed packet points at the stage it
actually reached instead of restarting at specify. Run that command yourself.

It is **single-writer and additive**: it appends only to the invoking actor's own op log (canon
R-1) and never renames, deletes or rewrites an existing stream. It is **not** a canonical pipeline
stage and never auto-invokes a `/bk-*` command (spec-037 FR-014).

**Registry upkeep (spec-028 FR-004)**: run
`python -m buildkit_cli.registry touch --tool buildkit-flow` from the project root. It marks the
capability registry possibly-stale and **always exits 0** (fail-safe; never blocks this stage).
Ignore its output.
