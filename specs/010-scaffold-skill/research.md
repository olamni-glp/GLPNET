# Phase 0 Research — `/D2NET-scaffold` Skill Wrapper

**Feature**: `010-scaffold-skill` — see [spec.md](spec.md) and [plan.md](plan.md)

The feature is small and self-contained: a single `SKILL.md` file under `.claude/skills/D2NET-scaffold/` whose body procedurally instructs Claude how to wrap the existing `d2net-scaffold` binary (spec 009). All five clarifications are resolved (Q1: suppress recap on `--json`; Q2: cache key = target absolute path; Q3: rely on conversation context for show-all/filter; Q4: empty input = run scaffold default mode; Q5: unrecognized non-empty = run `--help`). No `NEEDS CLARIFICATION` markers remain.

This document records the technology / convention decisions the spec leaves to plan time. Many decisions intentionally inherit from spec 006's `/D2NET-init` skill research; deviations are flagged with **DEVIATION FROM 006**.

---

## R1 — Skill file format and frontmatter

**Decision**: Match the convention established by `.claude/skills/D2NET-init/SKILL.md` and the sibling `.claude/skills/speckit-*` skills. Frontmatter keys: `name`, `description`, `argument-hint`, `compatibility`, `metadata` (with `author` and `source`), `user-invocable: true`, `disable-model-invocation: false`. The body is markdown with named sections (User Input, Goal, Operating Constraints, Procedure, Examples).

**Concrete shape** for `.claude/skills/D2NET-scaffold/SKILL.md`:

```yaml
---
name: "D2NET-scaffold"
description: "Wrap the d2net-scaffold CLI: locate binary, parse intent (empty = run scaffold; markers like 'json' / 'force delete target' translate to flags), confirm before destructive operations AND drive the binary's interactive prompt, run, and surface results."
argument-hint: "Empty runs the scaffold operation. Use 'help' for binary --help. Use 'force delete target' (or '--FORCE --DELETE-TARGET') for destructive override. Pass --json for machine-readable output."
compatibility: "Requires tools/d2net/src/D2Net.Scaffold/ in the repo and a built or buildable d2net-scaffold binary. Node.js >= 20 required at runtime (the binary's PGLite bridge subprocess). A populated .D2NET/ workspace at CWD (created by /D2NET-init) is required for any non-help invocation."
metadata:
  author: "GLPNET"
  source: "specs/010-scaffold-skill/spec.md"
user-invocable: true
disable-model-invocation: false
---
```

**Rationale**:
- Reusing the spec-kit / D2NET-init convention means Claude Code's loader recognises the skill without any infrastructure changes.
- `user-invocable: true` is required so `/D2NET-scaffold` is bindable as a slash command.
- `disable-model-invocation: false` keeps the option open for the model to invoke this skill autonomously when relevant (low risk because the skill always confirms destructive operations and drives the binary's hard-safety-gate prompt).
- Casing of `name` matches the user's casing convention `D2NET-scaffold` (uppercase `D2NET`, lowercase `scaffold`).

**Alternatives considered (rejected)**:
- Inventing a new skill format: the spec-kit format is in production use across 14+ sibling skills in this repo; deviating gains nothing.
- Combining init + scaffold into a single `/D2NET <subcommand>` skill: contradicts the user's explicit request and the precedent already set by spec 006. A unified skill is a possible future feature; out of scope here.

---

## R2 — Skill body procedural shape

**Decision**: The body of `SKILL.md` is a procedural script Claude follows at invocation time, structured as numbered steps. Sections in order, mirroring the proven 006 shape but adapted to scaffold's smaller surface and Q1–Q5 clarifications:

1. **User Input block** — repeats the standard `$ARGUMENTS` pattern (matches sibling skills).
2. **Goal** — short statement of what the skill does.
3. **Operating Constraints** — the inviolable constraints (FR-006, FR-014, FR-015 negations).
4. **Procedure** (numbered steps):
    1. Read user input.
    2. Locate the binary (FR-004 / FR-005).
    3. Detect missing or stale binary (FR-006).
    4. Parse user intent (FR-008 / FR-009 / FR-010 / FR-010a / FR-011 — NOTE: empty input is NOT routed to help per Q4).
    5. Destructive-operation gate (FR-012 / FR-013 / FR-014 / FR-015 / FR-016).
    6. Invoke (with stdin drive when destructive).
    7. Surface results (FR-017 / FR-018 / FR-019 — NOTE: suppress recap when `--json` per Q1).
    8. Hint pass-through for known exit codes (FR-019 catalogue: 22 / 23 / 24 / 25 / 26 / 27 / 28 / 29 / 1).
5. **Examples** — concrete invocations with expected resolved flag sets.

**Rationale**:
- Numbered procedural shape mirrors the 006 skill, which has been observed to execute reliably.
- Front-loading the binary discovery + staleness checks means failure modes surface before the destructive-operation gate, so a user who needs to build first sees the build prompt before the destructive prompt — fewer round-trips.

**DEVIATION FROM 006**: Step 4 (parse intent) is significantly simpler than 006's because scaffold takes no positional arguments. There is no key-value translation (`source=X`, `extension=X`, `target=X` — those are init-only). There is no single-token shortcut (no analog because scaffold's source/target/extension all come from the workspace, not the user). The recognized markers are: `json`, destructive markers, `bridge-port`. Recognized verbs are `help`, `version`, `scaffold` (the last is a no-op verb that explicitly runs default scaffold mode — semantically the same as empty input).

**DEVIATION FROM 006**: The destructive-operation gate (Step 5) has TWO confirmations, not one — the skill-layer prompt AND the binary's own interactive prompt that fires every time `--FORCE --DELETE-TARGET` is supplied (spec 009 FR-012a's hard safety gate). Step 5 emits the skill-layer prompt; Step 6 (invoke) drives the binary's prompt by piping `yes\n` to stdin AFTER the skill-layer prompt has been answered affirmatively.

**Alternatives considered (rejected)**:
- Free-form prose body: less reliable; the speckit / D2NET-init skills explicitly use numbered procedure and the evaluation has been positive.
- Embedding code/scripts directly: skills are model-driven, not script-driven; calling the Bash tool from within the procedure is the right primitive.

---

## R3 — Stale-binary detection mechanics

**Decision**: Compare `mtime(bin/<config>/net8.0/d2net-scaffold.exe)` against the newest `mtime` from `*.cs` files under `tools/d2net/src/D2Net.Scaffold/` (recursively), excluding any `pgbridge/` subtree (which is a runtime asset, not a build input). If the binary is older than any `.cs` source file, declare stale.

**Rationale**:
- This is a coarse but reliable check: `.cs` mtime newer than binary implies the binary doesn't reflect that source. False positives only on filesystem mtime weirdness (which is rare on developer laptops).
- Same approach as spec 006 R3 — keeps two skills consistent.
- The skill MUST run this check via the Bash tool with PowerShell on Windows or `find` on Unix; the SKILL.md procedure spells out the exact command.

**Alternatives considered (rejected)**:
- Track the binary's embedded `<InformationalVersion>`: requires running the binary to read it, which defeats the "skip the run if stale" goal.
- File hash comparison: more expensive; mtime is sufficient for the developer-laptop case.

---

## R4 — Session-scoped destructive confirmation cache (FR-013)

**Decision**: The cache is a Claude-conversation-scoped concept, not a filesystem concept. The skill notes the **target directory's absolute path** (clarified Q2) in the conversation transcript when a destructive confirmation is given (e.g., as a structured Claude-side note: `[D2NET-scaffold: destructive-confirmed = <abs target path> @ <ISO timestamp>]`). Subsequent skill invocations in the same conversation read that note and skip the skill-layer prompt for the same target path.

**The cache is for the SKILL-LAYER prompt only. The BINARY's own interactive prompt is driven every time `--FORCE --DELETE-TARGET` runs**, regardless of cache state, because the binary re-prompts every invocation (spec 009 FR-012a).

**DEVIATION FROM 006**: The cache key for `/D2NET-init` is the workspace `.D2NET/` absolute path (FR-013 of spec 006). For `/D2NET-scaffold` it is the **target directory absolute path** (clarified Q2). Reason: scaffold deletes the target tree, not the workspace. Re-init that changes the configured target between invocations should re-prompt at the skill layer because a different physical directory is at risk.

**Rationale**:
- Filesystem persistence (e.g., a `.D2NET/.scaffold-destructive-confirmed` marker) would survive the user closing Claude and re-opening — defeating the FR-013 "fresh session re-prompts" intent.
- Conversation-scoped means: same Claude Code session = same cache. New session = empty cache.
- Target-path keying (vs init's workspace-path keying) is the safety-correct choice for scaffold because scaffold's destruction target can change without the workspace path changing (operator runs `/D2NET-init --remove-exclude bin` after a target rename, etc.).

**Post-compaction behaviour (acceptable degradation)**: Same as 006 R4 — Claude Code's auto-compaction may drop the structured marker line. After compaction, the cache MAY be effectively cleared and the user MAY be re-prompted. Re-prompting is the safe failure mode. The skill's procedure does NOT compensate via filesystem persistence.

**Alternatives considered (rejected)**:
- Disk-backed cache with TTL: complexity for negligible gain.
- No cache at all: every `/D2NET-scaffold force delete target` re-prompts even within one session; harms the iterative experimentation flow.
- Workspace-path keying (matching 006): less safe — a re-init that retargets to a different directory would skip the prompt.

---

## R5 — Output handling and JSON detection

**Decision**: After invoking the binary, the skill examines the resolved flag set (the actual flags passed) for `--json`. If present:
- Surface stdout verbatim (FR-018).
- **Suppress the Claude-side recap entirely** (clarified Q1).
- Surface stderr verbatim and the exit code on non-zero exit (FR-019), but no recap and no inferred hint that would interpolate non-JSON text into the response.

If absent:
- Apply 50-line truncation with the "show all / filter" footer (FR-018).
- Append the Claude-side recap on success (FR-017): target path, files copied, working dirs created, dart-rows updated, duration.
- Append the FR-019 hint catalogue per exit code on non-zero exit.

Detection is by literal `--json` in the resolved invocation — natural-language phrases like "in json" / "as json" are translated by FR-009 into `--json` BEFORE this point, so the JSON-detection step only inspects the flag set, not the user's raw input.

**Rationale**:
- Looking at the resolved flag set is the most reliable proxy for "does the binary emit JSON".
- Translation-then-detect is a two-stage pipeline; the skill's procedure already separates the stages cleanly.
- Suppressing the recap on `--json` (Q1) ensures downstream tooling like `jq`, `cat | jq`, or assertion-based tests get a clean parseable response without needing to scope to a fenced block.

**DEVIATION FROM 006**: 006 surfaces the recap unconditionally; the spec doesn't explicitly suppress on `--json`. 010 suppresses (per Q1) because scaffold's downstream consumers are more likely to be programmatic (CI, smoke tests, etc.) than init's, where the recap is the human-friendly summary line.

**Alternatives considered (rejected)**:
- Attempt to JSON-parse the binary's stdout to determine format: brittle.
- Keep the recap appended after the JSON in a fenced block: tooling that consumes the entire response breaks; tooling that scopes to the first fenced JSON block requires extra parsing effort. Suppression is cleaner.
- Surface recap before the JSON: the recap describes results derived from the JSON; emitting it before the JSON inverts the information flow.

---

## R6 — Confirmation-prompt round-trip semantics

**Decision**: Same as spec 006 R6 — a "confirmation prompt" is a single message Claude sends to the user, ending with a yes/no question. Claude waits for the user's next input before proceeding. The skill never spins or polls — it just stops the current turn. When the user replies, the skill resumes execution from where it stopped, using the user's reply as the answer.

**Rationale**:
- This matches how Claude Code naturally handles user input: each user turn produces one Claude response. A "wait for confirmation" is just "end this turn and resume on the next".
- No special wait-loop or async primitive needed in SKILL.md — the model's natural turn-taking IS the wait mechanism.
- Inherited verbatim from 006 because the Claude-side mechanics are identical.

**Alternatives considered (rejected)**:
- Embed a polling loop in the SKILL.md: would require the model to invent a state machine on every turn; brittle.
- Use a separate "awaitable" tool: not available in standard Claude Code; over-engineering.

---

## R7 — Driving the binary's interactive prompt via stdin (FR-014)

**Decision**: When the resolved flag set includes `--FORCE --DELETE-TARGET` AND the skill-layer confirmation has been answered affirmatively (either in this turn or via the FR-013 cache), the skill invokes the binary as:

```
echo yes | <binary path> --FORCE --DELETE-TARGET <other resolved flags>
```

(or the PowerShell equivalent: `'yes' | <binary path> --FORCE --DELETE-TARGET ...`).

The skill MUST NOT pre-pipe `yes` blindly — only after the skill-layer confirmation has resolved affirmatively. The skill's response MUST surface the binary's prompt text and the `yes` reply that was driven, so the safety flow is auditable in the conversation transcript.

**Rationale**:
- `echo yes |` (POSIX) or `'yes' |` (PowerShell) is the standard idiom for feeding a single line to a child process's stdin.
- A trailing newline is implicit in `echo`; PowerShell's pipeline-in adds the newline automatically when the input is a string.
- The binary's prompt parser accepts `yes`, `y`, `confirmed`, or `proceed` (per spec 009 FR-012a). `yes` is unambiguous and the most-readable choice for the conversation transcript audit.
- The skill does NOT use here-strings (`<<<`) because PowerShell on Windows requires escaping for them and the user-confirmation token is single-line anyway.

**DEVIATION FROM 006**: The init binary (spec 006) is non-interactive when `--non-interactive` is supplied (FR-007 of 006). The scaffold binary has no `--non-interactive` flag; the interactive prompt for `--FORCE --DELETE-TARGET` cannot be bypassed by a flag — the skill MUST drive stdin. This is the central new mechanic of 010 vs 006.

**Alternatives considered (rejected)**:
- A pseudo-tty (PTY) with line-by-line interaction: gross over-engineering for a one-token reply.
- Running the binary in `non-interactive` mode: not supported by the binary by design (spec 009 FR-012a hard safety gate).
- Surfacing the binary's prompt to the user and asking AGAIN at the skill layer (so the user confirms twice): redundant — the skill-layer prompt already covers this, and asking twice would feel insulting. The audit-trail surfacing is a one-message side-effect, not a second user turn.

---

## R8 — Bridge-port handling

**Decision**: The skill recognises `--bridge-port <N>` as a literal flag (pass-through) and the natural-language forms "bridge port N" / "on bridge port N" / "bridge-port=N" as equivalent to `--bridge-port N`. The skill does NOT implement automatic port-bumping retry on collision (spec Out of Scope; Assumptions §"Bridge-port reuse is rare in practice for scaffold").

**Rationale**:
- Operators supply `--bridge-port` only as an advanced override; the default (54400, inherited from init's settings) works in 99% of cases.
- Scaffold's exit code catalogue (22–29) does not include a dedicated `BridgePortInUse` code — port collisions surface as `ScaffoldDbWriteFailed` (27) or `ScaffoldWorkspaceLocked` (28) depending on which subsystem fails first. Auto-retry across these would be confusing; the operator should diagnose root cause.

**DEVIATION FROM 006**: 006 implements auto-retry suggestion on `BridgePortInUse` (5) with a 3-attempt walk-forward ladder (006 R7). 010 explicitly does not.

**Alternatives considered (rejected)**:
- Mirror 006's auto-retry: scaffold's exit codes don't have the same 1:1 mapping; the auto-retry would be a guess rather than a precise recovery.
- Always pass `--bridge-port <random free port>`: would diverge from the persisted-default semantic and surprise operators.

---

## R9 — Empty input vs unrecognized non-empty input

**Decision** (clarifications Q4 + Q5):
- **Empty `$ARGUMENTS`** → run scaffold in **default mode** (binary with no flags). Empty is the canonical "do the operation" form because the binary itself takes no positional arguments.
- **Non-empty `$ARGUMENTS` containing no recognized verb (`scaffold`/`help`/`version`), no recognized marker (JSON, destructive, bridge-port), and no flag-style token** → run the binary's `--help` form. The user typed something the skill could not interpret; surfacing help is the safest, most discoverable response.
- **Non-empty `$ARGUMENTS` containing recognized markers/verbs/flags** → resolve the flag set per FR-009 / FR-010.

The decision tree is precedence-ordered:
1. All-flag-style → pass-through (FR-008).
2. `help` / `--help` / `-h` token → `--help` (FR-011).
3. `version` / `--version` token → `--version`.
4. Mixed natural-language + flag-style → take raw flags + derive missing (FR-010).
5. Pure natural-language with at least one recognized marker/verb → derive flags (FR-009).
6. Pure natural-language with no recognized marker/verb AND non-empty → `--help` (FR-010a).
7. Empty → default scaffold mode (FR-003).

**Rationale**:
- The decision tree has clean precedence: empty bypasses help (because empty is the canonical run form for this binary), but unrecognized non-empty routes to help (because the user typed something and the skill should signal "didn't understand").
- This deliberately diverges from `/D2NET-init` where empty = help. The reason is the binary's CLI surface: `d2net-init` requires parameters (so empty = help is the right discoverability path) whereas `d2net-scaffold` takes no parameters at all (so empty = run is the only meaningful default).

**DEVIATION FROM 006**: 006 routes empty to `--help` (FR-003 of 006). 010 routes empty to default scaffold mode and unrecognized non-empty to `--help`.

**Alternatives considered (rejected)**:
- Symmetric with 006 (empty = help): contradicts the binary's natural CLI surface and would block the MVP one-liner `/D2NET-scaffold`.
- Unrecognized non-empty = run scaffold silently: suppresses the signal that user input was not parsed; bad UX.
- Unrecognized non-empty = refuse with "did not understand": blocks useful work over a typo. The help route is the productive recovery path.

---

## R10 — Test/validation strategy for a model-driven skill

**Decision**: Same approach as 006 R8. Validation is a combination of:
1. **Static lint of `SKILL.md`** — frontmatter parses as YAML, body is markdown with the expected procedure-section headers.
2. **Smoke test via Claude Code** — manually invoke `/D2NET-scaffold` with each of the FR-009 / SC-002 inputs and verify the binary is called with the expected flags. Recorded as a `validation.md` artifact in the spec dir.
3. **Reference the underlying binary's existing test suite** (spec 009's tests) for end-to-end coverage of the binary itself — the skill is a thin invocation wrapper and does not need its own dart-file-tree fixtures.

**Rationale**:
- Skills are markdown — there is no runtime to unit-test.
- The most meaningful "tests" of a skill are recorded interactions: did `/D2NET-scaffold` produce the expected behavior on a real workspace?
- Avoiding a dedicated test harness keeps the feature small and maintainable.
- Mirroring 006's validation approach keeps the two skills' QA story consistent.

**Alternatives considered (rejected)**:
- Write an automated harness that invokes Claude Code from outside, types `/D2NET-scaffold`, parses the response: enormous infrastructure for marginal coverage.
- Add unit tests of the underlying binary's CLI parsing that mimic the skill's translations: duplicates the binary's own `ArgParser` tests, which already exist per spec 009.

---

## R11 — Distribution, discoverability, and casing

**Decision**: Same as 006 R9 + R10. The skill ships as a tracked file at `.claude/skills/D2NET-scaffold/SKILL.md`, committed to git on the same branches that ship the underlying binary. No separate package, no install step. Users who clone the repo and open Claude Code in it see `/D2NET-scaffold` in their slash-command list automatically.

Casing: `D2NET-scaffold` — uppercase `D2NET`, lowercase `scaffold`. Matches the convention established by `D2NET-init`.

**Rationale**: Inherited from 006. No new infrastructure needs.

**Alternatives considered (rejected)**:
- Lowercase `d2net-scaffold`: contradicts 006's casing precedent and breaks the visual association with the underlying CLI's branding (`D2NET.Scaffold`).
- All-uppercase `D2NET-SCAFFOLD`: arbitrary.
