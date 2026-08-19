<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Phase 0 Research — madGLP writer-reader address-discipline closure (079)

Audit-first (FR-008/FR-009). Every finding below is read from source at HEAD.

## 🔴 FINDING R-1 (scope discovery — FR-009 / ESCALATE E5): the `writerAddr+1` fallback is NOT dead code; it is the bound-writer reader path.

- **Decision:** FR-001/FR-002 as written ("remove the fallback; fail loud when the cross-pointer can't resolve") is **NOT behaviour-preserving as-is**, and must NOT be implemented verbatim. The audit splits residual (1).
- **Evidence (`heap_fcp.dart`):**
  - `readerForWriter(writerAddr)` (`:199`) returns `int?` and returns **`null` in Case 3 = "Bound or invalid"** (`:224`), and also when a `Pointer` writer is "bound to something else" (`:216`).
  - `pairedReaderAddr` (`:236`) doc: *"Get the paired reader address for a writer (works for **bound and unbound**)"* — the `return writerAddr + 1` at `:242` is **precisely the bound-writer branch**: when `readerForWriter` returns null (bound), the +1 convention supplies the reader.
  - **11+ call sites** in `bytecode/runner.dart` (`:411,1059,1724,1869,1880,1938,1949,2009,2036,2047,2065`) call `pairedReaderAddr` on writers that may be **bound** (they resolve a reader address after unification). Each is annotated "use readerForWriter() instead of +1 arithmetic" — i.e. the intent was already to prefer the cross-pointer, but the +1 remains the bound fallback.
- **Rationale:** For an **unbound** writer the cross-pointer (Pointer or WriterContent.readerAddr) resolves and the fallback never runs — dead in that state, per the spec's assumption. But for a **bound** writer the current code has **no cross-pointer reader accessor** (Case 3 returns null), so the +1 convention is the *only* mechanism. Removing it and failing loud would break bound-writer reader resolution at all 11 call sites.
- **Consequence for the plan:** residual (1) is larger than an audit-close. Two sub-paths:
  - **R-1a (behaviour-preserving, in-scope):** ADD a bound-writer cross-pointer reader accessor — extend the cell representation so a bound writer still records its paired reader address (WriterContent already preserves `readerAddr` for the suspended case; the bound case needs the same), then make `pairedReaderAddr` resolve via it and fail loud only if genuinely absent. This removes the *convention* while preserving behaviour.
  - **R-1b (STOP-and-report, §Bug-Protocol):** if the bound-writer reader cannot be recovered from the cross-pointer without a heap-format change that alters `_ClauseVar`/`_TentativeStruct`/allocation invariants, that exceeds an audit-close and is a **STOP-and-report to Gabi + a spec revision of FR-002**, not a judgement call (CLAUDE.md core-preservation rule; FR-009).
- **Alternatives considered:** (a) blind removal + fail-loud — REJECTED, breaks bound-writer callers (not behaviour-preserving, violates FR-003). (b) leave the fallback but add an assertion/telemetry when it fires on an *unbound* writer (a cross-pointer gap) — a cheaper partial that closes the *silent-guess* hazard without the heap-format work; viable as an MVP if R-1a proves large.

## FINDING R-2 (residual 2 — false-positive verdict): three_agent_pipeline_boot / globalise-send

- **Decision:** verify to a verdict (FR-005), do not assume live-or-dead.
- **Evidence:** `docs/bug-send-globalise-localise.md` exists; `three_agent_pipeline_boot` is referenced in `glp_runtime/test/multiagent/multiagent_glp_test.dart`. Verdict requires running that scenario deterministically (multiagent Dart suite) and reading the outcome.
- **Rationale/Alternatives:** clean audit-close either way — file a repro (live) or retire + update the doc (false positive). No heap-format risk.

## FINDING R-3 (residual 3 — mis-named field): GlobalSendSpawn.readerAddr

- **Decision:** rename/re-doc to match contents (FR-006).
- **Evidence (`mad_helpers.dart:61-64`):** `/// Address of the reader to watch (the ? end of the variable pair)` on `final int readerAddr;` — but the field carries an **onBind writer key** (per the roadmap profile + usage). Confirm exact usage by reading its call sites in `lib/multiagent/` before renaming; update all references consistently.
- **Rationale/Alternatives:** cheap, self-contained in `lib/multiagent/` (freely modifiable per maGLP constraints). Doc-only + field rename; ride-along fix for the Issue-1 header/body "Open" vs "Fixed" inconsistency (FR-007).

## FINDING R-4 (test baseline)

- **Decision:** baseline = multiagent Dart suite (`cd glp_runtime && dart test test/multiagent/`) + the REPL suite, recorded before any change (FR-003/SC-002). This session's REPL baseline ran green through Section S (Section T abort is the known 064 unguarded-abort, PR #158, orthogonal).
- **Rationale:** madGLP behaviour is exercised by the multiagent Dart tests; heap addressing regressions surface there.

## Net plan-shaping conclusion

- Residuals **R-2** and **R-3** are clean audit-closes → proceed in this feature.
- Residual **R-1** is split: attempt **R-1a** (add bound-writer cross-pointer accessor, then drop the convention) as the behaviour-preserving path; if it requires touching heap-format/core invariants (`_ClauseVar`/`_TentativeStruct`/allocation), **STOP-and-report** (R-1b) rather than force it. The MVP fallback (R-1 alt-b: assert-on-unbound-fallback) closes the silent-guess hazard cheaply if R-1a is deferred.
- This is the FR-009 scope confirmation the spec asked for, resolved with evidence at plan time rather than discovered mid-implement.
