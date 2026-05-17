# Contract — Conversion-Idiom KB (FR-012/013/014/024 · SC-007/008)

`codeconv.conversion_idioms` + `codeconv.research_findings`
(see [../data-model.md](../data-model.md) §2.2/2.3). DB = runtime store;
`.codeconv/conversion-idioms/` = checked-in round-trip export (DB-runtime /
checked-in-truth split, same as tombstones — D2, no new model).

## Decision order (per construct) — MANDATORY

```
1. normalise construct → construct_key
2. KB lookup conversion_idioms[construct_key]
   - hit (status=active) → REUSE verbatim; NO research, NO re-derive (FR-012/SC-007)
   - hit (status=conflicted|escalated) → escalate, do not guess (FR-014)
3. miss → research_findings lookup[construct_key]
   - cached → use cached finding (FR-024: never re-research)
   - absent → spawn SEPARATE research sub-agent (skill); official Dart/.NET
     docs authoritative, web only corroborating (FR-024)
       - authoritative conclusion → write research_findings + new idiom (active)
       - inconclusive / research unavailable / non-authoritative-only → ESCALATE
         (FR-013; NO silent naive fallback)
4. conflict checks before write:
   - new research contradicts an existing active idiom → status=conflicted,
     ESCALATE (FR-014) — never silently override either side
   - two idioms imply contradictory target for same key → ESCALATE (SC-008)
```

## Consistency guarantee

≥95% of recurring constructs resolved via a recorded idiom, not re-derived
(SC-007). Every undecidable point ⇒ escalation, **0 silent guesses**
(SC-008); resolved escalation feeds back as an idiom so it does not recur.

## Authority rule (FR-024)

`research_findings.is_authoritative=true` **iff** grounded in official Dart
or .NET/C# documentation. `corroborating_sources` may hold broader web but is
**never the sole basis**. After first research a construct is reproducible
offline (cache hit) — `test_convspec_research_provenance.py` asserts no
second research call for a cached construct_key.

## Tests

`test_convspec_idiom_kb.py` (reuse + consistency),
`test_convspec_idiom_conflict.py` (both conflict kinds → escalation),
`test_convspec_research_provenance.py` (authoritative + cached + offline).
