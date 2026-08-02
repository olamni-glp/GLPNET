# Proposal — Self-Prove GLP Liveness Goal (DEF-F1)

**To**: Gabi (language authority, CLAUDE.md / DISCIPLINE.md §1.14)
**From**: feature 061 wave-2 (T028, FR-021)
**Date**: 2026-07-30
**Status**: PROPOSAL ONLY — zero implementation exists or will exist without your
express approval. MVP liveness shipped host-timer-only per FR-021.

---

## What is being proposed

A new GLP **system predicate** so the supervisor's liveness probe can prove
*GLP-level* progress, not just process-level responsiveness:

```prolog
procedure '_liveness_probe'.
```

Semantics (proposed, subject to your revision): a goal of `'_liveness_probe'`
MUST reduce to success within a small bounded number of cycles on a healthy
engine, exercising one genuine reduction through the full machinery — clause
selection, a guard evaluation, a writer binding on a fresh heap variable pair,
and commit — rather than short-circuiting in the dispatcher. The supervisor
would submit it as a RUN_GOAL on its ping cadence and treat
non-success-within-bound as engine sickness even when the wire still answers.

## Why the current signal is insufficient (the gap)

The shipped MVP supervisor (061 US3) pings over the wire (PING → ACK). That
proves the host's event loop and dispatcher are alive; it proves **nothing
about the GLP engine's ability to reduce goals**. A wedged scheduler, a
corrupted heap, or a runaway infrastructure goal can leave the dispatcher
answering pings indefinitely while every real goal hangs — a silently sick
engine that supervision cannot see. This gap was identified in the seed
("liveness ping (timer + optional self-prove goal)", investigation.md §211)
and deferred as DEF-F1 because closing it requires new language surface.

## Why it is language work (the §1.14 gate)

There is no existing "no-op that must reduce" predicate in `programs/self.glp`.
Defining one — name, declaration, reduction semantics, reserved-constant
naming (`'_...'` prefix, system mode) — changes what the language ships with,
which is exactly the surface DISCIPLINE §1.14 reserves to you. Hence: this
memo, and nothing else.

## Alternatives considered (and why they were not adopted unilaterally)

1. **Use an existing trivially-reducible goal as the probe** (e.g. a fresh
   `X := 1 + 1` per ping — no new predicate, no approval needed). Rejected as
   the *permanent* answer because it encodes probe semantics in the
   supervisor's source as a magic goal string with no declared contract; the
   bound ("must reduce within N cycles") lives nowhere in the language. It
   would, however, be a legitimate interim if you prefer no new surface —
   your call.
2. **Skip GLP-level liveness entirely** (status quo). Leaves the gap above.
3. **Implement `_liveness_probe` "behind a flag"** — rejected outright:
   §1.14 forbids implementing first and asking later, flag or no flag
   (research.md D8 records this rejection).

## What approval would unlock (not in this wave)

- The predicate added to root `self.glp` + runtime support (per your chosen
  semantics), with type/procedure declarations per the tight-typing discipline.
- Supervisor config gaining `probe: ping | self_prove` with the bounded-cycle
  budget as a knob; UPPAAL timing model extended with the probe deadline.
- A regression test asserting the probe reduces within its bound.

## The ask

A ruling, in whichever direction:
(a) approve the new system predicate (name/semantics as above or amended);
(b) direct the interim existing-goal probe (alternative 1) with no new surface;
(c) keep host-timer-only and leave DEF-F1 open.

Until then DEF-F1 stays **open — proposal delivered, approval pending**
(DEFERRALS.md row unchanged except status note at wave close, T039).
