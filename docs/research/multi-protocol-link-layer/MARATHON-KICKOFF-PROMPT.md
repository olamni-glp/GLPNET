# Marathon kickoff — `multi-protocol-link-layer` (end-to-end, harness-driven)

> **This is the INITIAL KICKOFF of the marathon in a fresh session — not a mid-marathon restart.**
> Drive the `multi-protocol-link-layer` feature end-to-end through the buildkit pipeline
> (specify → clarify → plan+tasks+analyze → implement → code-review-to-convergence) using the
> **marathon-stage-harness**. Run gated-autonomously: proceed inside an approved block, STOP for
> Gabi only at the two block-points (plan-approval gate + escalations) and at every new-primitive
> co-design point.

## 0. Mandatory startup (do first, in order — acknowledge each)
1. Read `CLAUDE.md` to completion → "I have read CLAUDE.md completely".
2. Read `docs/DISCIPLINE.md`, `docs/typed-glp-manual.md`, `docs/glp-cheat-sheet.md` → acknowledge.
3. Read `.claude/skills/marathon-stage-harness/SKILL.md` (the per-stage hook protocol you will follow).

## 1. Locate state OBJECTIVELY — never from this prompt (it can be stale)
Follow the Restart-Resume order; this prompt names *what* to build, the durable stores are the truth for *where it stands*:
- `buildkit-roadmap next` / `status` → confirm `multi-protocol-link-layer` is **promoted + dependency-satisfied** and is the recommended next feature.
- `codeconv/.venv/Scripts/python.exe -m codeconv.cli --data-dir C:/pglite/research/glpnet marathon resume --feature multi-protocol-link-layer` → the durable position (cold-start if the marathon has not begun; on store divergence it exits 2 + escalates — do NOT pick a side).
- If `resume` reports `commit_push_pending: true`, re-drive that block's commit/push first.

## 2. The design contract (APPROVED — read, honor, do not re-litigate)
The B2 / B3 / G design decisions were reviewed and ruled by Gabi. They are the binding contract:
- `docs/research/multi-protocol-link-layer/B2-B3-G-decision.md` — the decision doc. **The "## Decisions — RULED (Gabi, 2026-06-06)" section at the END is authoritative and supersedes the body recommendations where they differ — read it first.**
- `docs/research/multi-protocol-link-layer/corpus/` — the provenanced source corpus.
Apply **source precedence** on any conflict: local `docs/` GLP specs > Shapiro GLP / GLP-implementation / GLP-typing papers > his earlier concurrent-logic papers (FCP/CP/Logix).

## 3. Hard constraints (non-negotiable — RULED)
- **B2 framing:** the feature SPLITS a program at a shared writer/reader variable into 2→N separate instances connected by the new link primitives (the single-sequential-thread assumption is relaxed), decomposed as **one role-parameterized program** (branch-on-ground-`AgentId`). **Implement the BASE/current link primitives FIRST; do NOT block them.** glink (distributing the variable for full transparency) is a **higher-level construct built ON the base primitives, later** — dependency is base-primitives → glink, never the reverse. Hardening/bug-fixes are part of building the primitives correctly, not a gate before starting.
- **B3 build target — C# is the PRIORITY + REFERENCE.** Author the base link primitives + guards in **C# first** (`out/csharp/`, the mandated-default REPL — already real/building, serializer byte-parity with Dart). Create the **Dart mirror only after the C# reference works fully**. Cross-runtime parity is REQUIRED: a Dart instance must connect to a C# instance over one link (serializer is byte-parity; a real transport + an executed Dart↔C# round-trip test are the gap). Hand-authored C# must live where a codeconv regen cannot clobber it.
- **G (guard constraints):** implement the **approved** guard set; **do NOT cancel `comparison-guards` — implement it** (keep the feature). Fix `atom/1` (analyzer↔runner) and the compound-operand-suspend + imported-reader-reactivation bugs. **Decline** `==`/`\==`/`\=`/`reader/1`; add `@<`/`@>` only if peer-ids need a non-numeric total order. Keep `=\=` untouched.
- **T1:** broker = transport relay under a logically-bilateral link (preserve per-link FIFO + at-least-once). **T2:** model broadcast as N bilateral ground-copy links **AND keep BLE BIS true multi-reader in scope** (do not drop; SRSW tension is an open co-design item).
- **Failure:** faults as bound terms on a per-link monitor stream (NOT a 4th unification verdict); `ok/tempFail/permFail`; epoch/fencing for split-brain.
- **Language Authority (B1 condition):** the base primitives + approved guard set are approved-to-implement, with concrete signatures/semantics co-designed WITH Gabi at each stage gate; glink's higher-level primitives co-designed later. Never invent unilaterally.
- Preserve GLP semantics exactly: SRSW, writer-MGU (binds only writers), three-valued unification, suspend-on-reader. Spec-first; GLP-first (logic in GLP, host = thin I/O). Baseline tests before/after every change.

## 4. Start + drive the marathon
**Pre-flight (confirm all green before `marathon start`; if any is red, STOP and tell Gabi):**
- on the `multi-protocol-link-layer` feature branch (off `develop`, which already carries the hardened harness);
- `marathon doctor --feature multi-protocol-link-layer` → OVERALL OK, bridge reachable, `marathon` schema present;
- decision doc (§2) present and marked approved.

**Start (idempotent; records the two standing grants):**
```
codeconv/.venv/Scripts/python.exe -m codeconv.cli --data-dir C:/pglite/research/glpnet \
  marathon start --feature multi-protocol-link-layer --branch <FEATURE-BRANCH> \
  --budget <BUDGET-CEILING> --auto --preauth-commit-push --preauth-workflow
```

**Drive (per SKILL.md cadence):** one stage-block = one Workflow run = one checkpoint = one commit/push.
specify (then restart-the-block) → clarify → plan+tasks+analyze (one block) → implement (a series of subagent sessions) → `/buildkit-codexreview` review-and-fix to **convergence**.
- Each mutating block: present the plan at `marathon gate`, WAIT for approval, record it; honor a recorded approval on resume (no re-ask).
- Compose the **Workflow tool** for each stage-block; on a failed-subagent re-run, pass the `workflow_run_id` that `marathon rerun` echoes as `resumeFromRunId`.
- Emit `marathon status --emit` on the ~5-minute cadence (done / issues / tokens / to-do).
- BLOCK + durably checkpoint + wait at: each plan-approval gate; and escalations (non-retryable failure, store divergence, blocked push, budget ceiling, any stage-flagged item) **including every new-primitive co-design decision**. Never auto-approve.
- Record an experiment→verify trace per primitive (the GEPA/DSPy verify-loop is Claude-only, no external API).

## 5. If interrupted later (compaction / crash)
Do NOT continue from a conversation summary. Re-locate via §1 (roadmap → `marathon resume` → tasks), recover from the last durable checkpoint, skip partial work, tidy, then continue.

---
### Knobs to set before launch
- `<FEATURE-BRANCH>` — the `multi-protocol-link-layer` feature branch name (off `develop`).
- `<BUDGET-CEILING>` — token ceiling for the marathon (integer), or drop `--budget` for unbounded.
- `--auto` — keep for gated-autonomous; remove to require a prompt at every step.
