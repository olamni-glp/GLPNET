# Contract — `prereq-patterns/policies.md` format

## Purpose

`policies.md` is the single canonical home of cross-cutting rules that apply across multiple patterns in the catalog. Every affected pattern's `description.md` MUST cross-link to the relevant policy here; affected patterns MUST NOT restate the policy text. This contract is the shape every author of a new policy must follow.

This contract is a peer of `howto_md_format.md`, `directory_md_format.md`, `description_md_format.md`, `applicability_md_format.md`, and `sources_md_format.md` (all under `specs/011-prereq-patterns-catalog/contracts/`). It does NOT modify those contracts; it sits alongside them.

## File-level shape

```text
# Catalog policies

<1-paragraph framing — names this file as the canonical home of cross-cutting rules and states the no-restatement rule>

## Policy <N> — <short title> (FR-CC-<N>)

**Rule.** <one paragraph stating the rule>

**Specifics.** <the bar the rule pins — minimum-bar family / convention / inclusion-list / etc.>

**Applies to.** <bullet list of pattern names this policy applies to today>

**Concrete details live in.** <forward-pointer naming the pattern(s) whose description.md carries the pattern-specific concrete realisation>

## Policy <N+1> — <short title> (FR-CC-<N+1>)

<same shape as above>

## Cross-link rule

<re-states the no-restatement rule: affected patterns MUST cross-link, MUST NOT restate. This is the LAST H2 in the file.>
```

## Section rules

| Element | Rule |
|---|---|
| H1 title | Exactly `# Catalog policies`. No variation. |
| Framing paragraph | One paragraph. Names this file as the canonical home of cross-cutting rules; states the no-restatement rule explicitly (forward-references the `## Cross-link rule` section). |
| `## Policy <N> — <title> (FR-CC-<N>)` | One per cross-cutting rule. The H2 heading MUST carry the `(FR-CC-<N>)` parenthetical naming the spec requirement the policy implements. The numeric `<N>` matches the spec's FR numbering. |
| `**Rule.**` paragraph | One paragraph stating the rule. This is the text that affected patterns are forbidden to restate. |
| `**Specifics.**` paragraph(s) | The bar the rule pins. For policies whose specifics include a closed list (e.g. allowed hash primitives), the list MUST be explicit and complete in this section. |
| `**Applies to.**` bullet list | The patterns the policy applies to today. New patterns are added to this list as they are introduced. |
| `**Concrete details live in.**` paragraph | Forward-pointer naming the affected pattern(s) whose `description.md` carries the concrete realisation. Per the allocation discipline, `policies.md` does NOT carry the concrete realisation itself. |
| `## Cross-link rule` | The LAST H2 in the file. Re-states the no-restatement rule. Two paragraphs maximum. |

## Cross-link from affected patterns

For every pattern named in any `## Policy N` `Applies to` list, that pattern's `description.md` MUST contain at least one markdown link to `policies.md`. Anchor links to the relevant policy section (e.g. `[Policy 1](../policies.md#policy-1-no-cleartext-auth-tokens-fr-cc-1)`) are preferred; a top-level link to `policies.md` is accepted.

The cross-link is the affected pattern's machine-checkable assertion that it has read and is bound by the policy.

## No-restatement invariant

The text of each policy's `**Rule.**` paragraph MUST NOT appear verbatim in any affected pattern's `description.md` or `applicability.md`. Affected patterns may reference the rule in their own words (e.g. "as required by [Policy 1](../policies.md#…)"), but they MUST NOT copy the canonical rule text. v1 enforces this by review; a future linter is straightforward (string-similarity check across pattern files vs `policies.md`'s `**Rule.**` paragraphs).

## Tone

Imperative. "MUST", "MUST NOT", "MAY". `policies.md` is a contract with every pattern in the catalog; soft language defeats its purpose.

## Length

Aim for ≤ 100 lines of body per policy section, and ≤ 250 lines for the file as a whole. Past that, `policies.md` becomes the kind of doc no one reads, which defeats its purpose. If a policy's specifics need more than that to state, factor the elaboration into the affected pattern's `description.md` (per the allocation discipline).

## Common errors to avoid

| Error | Why bad |
|---|---|
| Embedding concrete pattern-specific details (e.g. the chosen Argon2id parameters; the exact unreachable-destination fallback) in `policies.md` | Violates the allocation discipline. Concrete details belong in the affected pattern's `description.md`. `policies.md` says *what* the rule is; the pattern says *how* it's realised in that pattern's domain. |
| Restating a policy's `**Rule.**` paragraph in a pattern's `description.md` | Violates the no-restatement rule. Cross-link instead. |
| Adding a policy without the `(FR-CC-<N>)` parenthetical in its H2 heading | Breaks the cross-reference from the spec to the policy file. |
| Reordering `## Cross-link rule` so it is no longer the last H2 | Breaks the validation rule. |
| `Applies to.` list that omits a pattern that does in fact emit non-config history / touch secrets | Silent gap; affected pattern is not on the hook for cross-linking, and the policy is silently violated. |
