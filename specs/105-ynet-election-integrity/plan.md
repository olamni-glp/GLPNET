<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Implementation Plan: YNET election integrity

**Feature**: `105-ynet-election-integrity` · **Date**: 2026-09-05 · **Spec**: [spec.md](./spec.md)

## Summary

Most of this feature is already built. `scripts/ynet_vote_audit.py` was written during today's
diagnosis, corrected after the delegation refutation, and is positive-controlled in the suite
(Section W). **Measured against the spec, exactly one requirement is unimplemented — FR-008.**

This plan therefore does the smallest honest thing: implement FR-008, prove each surviving
requirement against the code rather than assuming the diagnosis-era tool satisfies a
specification written after it, and bring an artifact built without a spec under one.

## Requirement → implementation audit (done before planning, not after)

| FR | State | Evidence |
|---|---|---|
| FR-001 franchise resolution | ✅ | `resolve_franchise()` — direct / delegated / REFUSED |
| FR-002 never fall back on a failed proof | ✅ | returns `(None, "REFUSED-…")`; the actor is not substituted |
| FR-003 sign over the declared field set | ✅ | `VOTER_SIGNED_FIELDS`, canonical JSON |
| FR-004 voter == digest of signing key | ✅ | `REFUSED-not-key-bound` when it differs |
| FR-005 one host counted once per candidate | ✅ | tally is `candidate → host → [submissions]`; `len(hosts)` |
| FR-006 F3 | ✅ | fires and names host + candidates |
| FR-007 F4 repeat submission | ✅ | reported with every timestamp, counted once |
| **FR-008 conflicting submissions** | 🔴 **MISSING** | a franchise naming two candidates surfaces only as host-level F3, or not at all when it is the host's only franchise |
| FR-009 F2 no hello | ✅ | |
| FR-010 dedupe by record id | ✅ | part of `load_oplog` |
| FR-011 exit codes | ✅ | 0 / 1 / 2, and 2 when unmeasurable |
| FR-012 positive control in the suite | ✅ | Section W W-1/W-2, real Ed25519 keys |
| FR-013 runnable anywhere, prints franchises | ✅ | stdlib + `cryptography`, `--json` |

**FR-008 is the whole build.** Everything else is verification.

## The FR-008 gap, precisely

F3 groups by **host**, so it catches a *host* backing two candidates. A **franchise** backing two
candidates is a different event and is currently invisible in the case that matters: when the
conflicting franchise is the only one on its host, F3 sees one host and one candidate set and
stays quiet, because the two submissions collapse into the same host bucket.

The distinction matters because the two have different owners. **F3 is a roster problem** — one
host holds many node ids. **FR-008 is an emitter problem** — one identity said two things. A tally
that reports them as one finding sends both to the wrong owner.

## Design

Track, per term, `franchise → {candidates}` alongside the existing host tally. After resolution:

- more than one candidate for a franchise → **F6 conflict**, naming the franchise and both
  candidates, and the franchise contributes **nothing** to any candidate for that term.

**Excluding rather than choosing is the point.** Counting the first, the last, or the
lexicographically smaller would be a silent tie-break — the specification's own words are "MUST NOT
silently choose between them". Excluding is loud, and it cannot favour a candidate.

`F6` rather than reusing `F4`: F4 is benign and deduplicates; a conflict is not benign and must not
inherit F4's "deduped to 1" phrasing or its non-fatal exit status.

## Constraints

- **Read-only.** The audit never writes to the oplog. It is an instrument, and an instrument that
  edits its subject is not one.
- **stdlib + `cryptography` only.** Any lane must run it on any host without a project install.
- **Refuse rather than degrade.** No verification library → exit 2. An unverified tally would drop
  every delegation and report a met quorum as unmet.
- **No hand-written vote record, ever.** GLPNET holds no emitter; authoring one would make this
  lane the fourth emitter and manufacture the defect the feature exists to close.

## Rejected alternatives

| Rejected | Why |
|---|---|
| **Fix the board's tally here** | `election.py` is another lane's code; `verify_voter` already exists there and is already correct — the defect is that nothing calls it. Ruling G31-06 assigns it. Reaching into another lane's repo would also breach the commit-scope rule. |
| **Take the newest submission on a conflict** | A silent tie-break dressed as a policy. Recency is not authority, and it hides the emitter defect that produced the conflict. |
| **Fold FR-008 into F3** | Different root causes, different owners: F3 is a roster problem, FR-008 an emitter problem. One finding for two causes routes at least one of them wrongly. |
| **Warn instead of exit non-zero on a conflict** | A conflict can change which candidate wins. A warning that does not change the exit status is invisible to any gate that consumes it. |
| **Verify the delegation lazily, only for contested terms** | Cheap, and it makes the tally depend on whether anyone contested — the reader-dependent outcome SC-001 exists to remove. |

## Verification approach

Every requirement is proven by a control that **must be shown to fail**, not by a green run:

1. Extend the fixture with a franchise submitting for two candidates; F6 must fire, the franchise
   must contribute nothing, and the exit status must be non-zero.
2. Keep the existing controls (valid delegation counted; forged delegation refused **and not
   downgraded**; F1/F2/F3 each fire).
3. Re-run against the live oplog: term 1 keeps F3+F4, term 2 keeps F4+F5, and **neither acquires an
   F6** — a new finding appearing on live records would mean the new rule is wrong, not that the
   records changed.
4. Section W runs both in the repository suite so a regression is visible in CI rather than in an
   election.

## Risks

| Risk | Mitigation |
|---|---|
| F6 misfires on the benign live F4 case | Explicit control: same-candidate repeat must produce F4 and **not** F6. Verified against term 2. |
| The audit is trusted because it is green | The control is the deliverable. W-2 exists because a tool that prints failures and exits 0 lies to its caller. |
| The spec is written to fit the code | FR-008 was found **by** auditing code against spec and is unimplemented — the audit had a live failure, so it was capable of one. |
