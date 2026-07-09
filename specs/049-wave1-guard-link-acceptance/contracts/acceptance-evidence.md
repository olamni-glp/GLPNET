# Contract — acceptance evidence record format

Traces: FR-013 (all acceptance evidence captured + referenced from tasks), FR-008/FR-010
(BLOCKED records), SC-010 (zero deferred gate items at release).

## Location
`specs/049-wave1-guard-link-acceptance/evidence/<campaign>/` with `<campaign>` ∈
`guard` | `gavri` | `two-host` | `marathon`. Long command output goes to a sibling `.log`/`.txt`
file referenced from the record; records are markdown.

## Record shape (one per criterion per run)

```markdown
## <criterion id> — <one-line name>
- **Criterion**: <e.g. 036 SC-002b mesh to-routing + broadcast / SC-009 (a)≡(b) equivalence>
- **Host(s)**: <Olamnit | gavri | both (roles)>
- **Command**: `<exact copy-paste command>`
- **Output**: <inline short output OR path to captured log in this dir>
- **Verdict**: PASS | FAIL | BLOCKED
- **Date**: YYYY-MM-DD
```

## Rules
- **FAIL** verdicts trigger the bug protocol (report before fix); the record stays (append a
  follow-up record for the re-run — never rewrite history).
- **BLOCKED** records MUST carry: what was attempted, what is missing, escalation note to Gabi.
  A BLOCKED record keeps the ship gate closed until resolved or expressly re-scoped by Gabi.
- Every FR-009..FR-012 criterion and every SC has at least one record before ship; tasks.md
  references the record paths.
- Secrets/pins: the SPKI pin is public-by-design; private key material (`.pfx`) is NEVER
  committed to evidence.
