# Contract: planning / research sub-agent orchestration

Implements spec FR-005, FR-006, FR-007, FR-008, FR-009, FR-011; SC-001, SC-005, SC-006. The orchestration loop lives in `.claude/skills/codeconv-planagents/SKILL.md` (R1). This contract fixes the sub-agent prompt contracts so the LLM-judgement layer is reproducible and auditable.

## Source of truth references

- Research R1 (skill orchestrator), R3 (≤7 cap), R4 (SCC batch), R5 (separate research agent), R6 (escalate-don't-guess).
- Spec FR-005/FR-006/FR-007/FR-008/FR-009/FR-011; DISCIPLINE.md §1.2 (no workarounds), §1.10 (never guess).

## Orchestration loop (skill)

Pseudocode is normative in `planagents_cli.md` § "Skill orchestration loop". Concurrency rule (SC-001): **at most 7 planning sub-agents in flight at any instant**. The research sub-agent does NOT count against the 7 planning slots (it is a distinct, short-lived agent serving one planning agent's request) but the skill SHOULD avoid more than a few concurrent research agents to bound web load.

## Planning sub-agent prompt contract (FR-006/FR-007/FR-008)

Each planning sub-agent is spawned with exactly one tombstone. Its prompt MUST supply:

1. The tombstone path `.codeconv/tombstones/<rel>.dart.md` AND the **real source path** `<rel>.dart` — the agent MUST inspect the actual `.dart` (FR-006), not rely solely on the tombstone's scraped metadata.
2. The target artefact path `.codeconv/conversion-plans/<rel>.dart.md` and the mandated structure (`conversion_plan_artefact_format.md`).
3. `cycle_group_id` and `scc_siblings` (from `next` JSON). If `scc_siblings` non-empty: the agent MUST author §7 and write its plan aware that no sibling can be converted in isolation (FR-011).
4. The conversion target: **Dart → C#/.NET** (spec Assumptions; feature-012 clarification 2026-05-09).
5. The escalate-don't-guess boundary verbatim (FR-008 / R6): auto-fix in-artefact ONLY when verbatim-derivable from spec / a referenced 012/015 contract / a written project convention AND no new design decision + no scope change; otherwise a structured escalation (`conversion_plan_artefact_format.md` § escalation schema). MUST NOT guess or silently work around (DISCIPLINE.md §1.2/§1.10).
6. The research-delegation rule (FR-009): the planning agent MUST NOT do open-ended inline web research. When it needs information beyond the source + referenced project docs, it emits a **research request** (scoped question) back to the skill and waits for the research agent's findings.

Output: a single artefact at the given path, structurally valid per `conversion_plan_artefact_format.md`. The agent does **not** write `dart_plans` or tombstones (the Python CLI does, via `plan-started`/`plan-completed`).

## Research sub-agent prompt contract (FR-009 / R5)

A **separate** agent (Claude Code Agent, general-purpose, WebSearch/WebFetch), spawned by the skill on a planning agent's research request:

1. Input: the scoped research question + (optionally) raw Dart snippets/identifiers. The research agent MAY transmit raw Dart snippets/identifiers to external/web services when necessary for accurate research — the engineer accepted the IP-exposure risk (Clarification Q4).
2. Output: findings + **provenance** (source URLs/titles) + the **verbatim text of every external request issued** (FR-009 audit requirement). Returned to the skill, which hands it back to the requesting planning agent for embedding in artefact §4.
3. The research agent does NOT write the artefact and does NOT make conversion decisions — it only supplies researched facts.

## Research failure / timeout / empty (Clarification Q6 / R10)

If the research sub-agent fails, times out, or returns nothing usable: the planning agent records a `### E…` escalation `Observed: research unavailable for <topic>`, completes the rest of the plan **best-effort**, and the artefact is marked completed-with-escalation. The skill still calls `plan-completed … --escalations <n>` (n ≥ 1). Result: the file is `planned` for the planning frontier (downstream planning proceeds — FR-017) but conversion-blocked. The agent MUST NOT stall the file `plan_in_progress` on a flaky external dependency and MUST NOT silently substitute its own guess for the missing research.

## SCC coordinated-batch protocol (FR-011 / SC-006 / R4)

- When `next` emits an SCC unit, the skill spawns one planning agent **per member**, each within the 7-cap, each told the full `scc_siblings` list.
- Each member's artefact MUST contain §7 cross-referencing every sibling and flagging co-dependent decisions.
- The skill calls `plan-completed` per member as each finishes; it does NOT advance the loop past the SCC until **every** member is `plan-completed` (downstream gating is enforced by `readiness.py`, but the skill must not race ahead and call `next` expecting downstream files before the batch closes — `next` would correctly exclude them, but the skill keeps the batch coherent).
- Partial-batch resume: a re-invoked loop re-selects only the un-started members (FR-014 idempotent recovery; `plan_readiness_algorithm.md` partial-batch rule).

## Concurrency-cap enforcement (SC-001 / R3) — dual

1. **Python**: `next --limit 7` never returns an already-`plan_in_progress` tombstone (so a resumed loop cannot double-spawn an in-flight file).
2. **Skill**: at most 7 planning Agent calls concurrently; a new `next` is only issued when a slot frees. SCC units taken whole may transiently exceed the soft `--limit` count in the *returned list*, but the skill still throttles actual concurrent Agent calls to ≤7, draining the batch across iterations.

Together these guarantee SC-001 ("at no point are more than 7 planning sub-agents active concurrently"), crash-safe across loop interruption/resume.

## Determinism & audit (FR-021 / SC-005)

- Selection order is fully determined by `readiness.select_next` (deterministic — `plan_readiness_algorithm.md`); the skill MUST process `next.batch` in the order given.
- Every external request is logged verbatim in artefact §4 (FR-009).
- Every non-verbatim-derivable gap is an escalation in artefact §6 + the aggregated report (SC-005 — zero un-escalated unresolved gaps).
- The Python CLI's `plan-started`/`plan-completed`/`aggregate-escalations` give a complete, queryable audit trail independent of the (non-deterministic) LLM content.

## Out of scope here

- The exact LLM prompt wording (the *contract* — inputs, mandated outputs, escalate-don't-guess, research delegation — is fixed; phrasing is an implementation detail of `SKILL.md`).
- Converting `.dart` to C#/.NET (separate future tool).
- Resolving escalations (the engineer does this before conversion).
