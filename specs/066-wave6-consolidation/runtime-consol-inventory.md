<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# glp-runtime-consol inventory (T010) — with §1.14 language-authority screen

**Sources**: docs/handover/glp-runtime-consol-restart-2026-08-03.md (seed bc5ea232);
3rtask run 20260803T134205Z-8bcd curator report; roadmap brief. The audit already found
**12 of 16 open runtime/engine features delivered** (wave-4/062 + specs/050) and closed them —
the item consolidates the only genuine remaining gaps.

| # | sub-scope | §1.14 screen | disposition | status |
|---|---|---|---|---|
| A | ANTLR4 shared-grammar multi-target spike (no .g4, hand-written parsers across runtimes) | WOULD trip §1.14 if the grammar changed accepted syntax — spike-only was mandated | **SUPERSEDED by the Option-B rider** (ariellas host, engineer-directed, 210601Z): IL-on-the-wire + compiler factor-out removes the multi-runtime parser need; roadmap row antlr4-shared-grammar-spike superseded (G5). No wave-6 build. Reversal path: if the engineer rules the FE-side parser still warrants the spike, it re-opens as its own feature — never silently here. | disposed (rider) |
| B | Dead conversion stub `out/csharp/lib/runtime/abandon.cs` (`AbandonOps.AbandonWriter` throws NotImplementedException; abandon delivered as anonymous-writer discard, 062 US5) | No gate — dead code, no language surface | **IMPLEMENTED here**: error-level `[Obsolete]` tombstone applied (repo convention, cf. runtime.cs:253), rationale in-code; zero live call sites verified (grep: only Dart originals/archives/conversion docs) | done |

**Explicitly out of scope** (per the handover — do not re-fold): qr-link-provisioning (own
feature, wave-6 T013); atomic-toolchain + batch-roadmap-advance (buildkit repo, wave-6 US2).

**Checkpoint evidence**: dotnet build glp_repl 0 errors; glp_link.tests 172/172,
glp_crdtmsg.tests 188/188 (post-change); full REPL suite green at the story checkpoint.
