# Seed Reconciliation Index — `engine-separation` epic

17-seed reconciliation of each captured seed against the design dossier
(`../design-dossier.md`) + as-built code. Each row links to the per-seed memo with
its full tension/under-specification analysis, GEPA/DSPy refinement plan, formal-tooling
evaluation, and Shapiro-criteria mapping.

**Owner-facing decisions live in [`DECISIONS-FOR-OWNER.md`](DECISIONS-FOR-OWNER.md)
(read that one). Cross-cutting methodology is in
[`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md). Source brief:
[`SEED-RECONCILIATION-BRIEF.md`](SEED-RECONCILIATION-BRIEF.md).**

## Legend

**Alignment** — seed scope/classification vs dossier + code:
- `aligned` — scope, kind, and dossier §-anchors all confirmed by code; proceed.
- `needs-enrichment` — aligned in intent but a dossier citation, dependency edge, or
  scope line is wrong/incomplete and must be corrected before `/buildkit-specify`.
- `superseded-candidate` — pre-decomposition monolith; recommend close (see #1.5).

**Kind** (dossier §11): `PREP` (foundation/unblocker, no user-visible feature) ·
`MVP` (the shippable process-split slice) · `EXPERIMENT` (spike / feasibility verdict) ·
`FOLLOW-UP` (post-MVP deferral) · `UMBRELLA` (the superseded monolith).

**GEPA/DSPy applicability** — `methodological` (the refinement loop drives artifact/code
iteration) · `low` (mostly a research/organizational deliverable; loop adds little).

**Proof assistant** — best-fit primary for the seed's formal metric (`lean4` / `rocq` /
`n/a`). All LM-driven tactic work runs in Claude via Agent-tool seams — **no external API**.

## Index (topological order)

| # | feature_id | kind | align | dossier §-anchors | GEPA/DSPy | prover | one-line recommendation | memo |
|---|---|---|---|---|---|---|---|---|
| **1a** | iterative-refinement-and-verification-framework | PREP | aligned | §0.4, §11 #1a, App.B; brief §2/§3 | methodological | lean4 | The methodology base — land first; ship 3 artifacts (REFINEMENT-METHOD + DECISIONS + metric-table template); confirm Lean 4 primary. | [1a](1a-iterative-refinement-and-verification-framework.md) |
| **2** | result-envelope-and-deep-resolve | PREP | aligned | §0.4, §1.3, §2.3, §10.2–10.4, §12r4 | methodological | lean4 | Proceed; `Bindings` is shallow-deref not deep (glp_engine.cs:578) — widens scope. Resolve T2 (#2→#3 dep). Advisory: parallel `ResolvedBindings` field. | [2](2-result-envelope-and-deep-resolve.md) |
| **3** | structured-output-capture-seam | PREP | aligned | §1.3, §2.3, §8.1, §10.2, §0.4 | methodological | n/a | Wire `GlpEngine.TraceSink` → Scheduler (gap at glp_engine.cs:535); narrow/phased scope; remove codegen.cs foo/1 artifact. | [3](3-structured-output-capture-seam.md) |
| **4** | il-codec-spike | EXPERIMENT | aligned | §2.1, §2.2, §3, §0.4, §9.1–9.2, §12r7 | methodological | lean4 (rocq alt) | Scope to per-module round-trip; payload-type byte (not new FrameKind); structural identity; Lean 4 round-trip proof. | [4](4-il-codec-spike.md) |
| **5** | result-codec-and-framecodec-ride | PREP | needs-enrichment | §2.3, §3, §0.4, §10.2–10.4, §12r4/r7 | methodological | lean4 (rocq alt) | **Dossier §3/§0.4 citation wrong**: FrameCodec.cs:64 `OffKind` is fragmentation, not payload-type. Settle §10.3/§10.4; #3 must enumerate Console.Write* sites. | [5](5-result-codec-and-framecodec-ride.md) |
| **6** | repl-engine-process-split-mvp | MVP | aligned | §4, §4.1–4.5, §8.1, §9.1, §0.4, §12r1 | methodological | lean4 (rocq alt) | **The MVP (§8.1 Slice A).** Source text over TCP, compiler engine-side, one client. Treat FrameCodec byte-parity as a hard gate. | [6](6-repl-engine-process-split-mvp.md) |
| **7** | engine-state-snapshot-and-persistence-api | PREP/MVP | needs-enrichment | §6.1–6.4, §0.4, §8.2, §10.5–10.8, §12r2/r3/r5 | methodological | lean4 (rocq alt) | Expand scope line (+`_waitReaders`, `GlpEngine._goalId`, `InfrastructureGoalIds`, `GlpChannels`); C# Npgsql+JSON store; label-ref for ModuleTerm. | [7](7-engine-state-snapshot-and-persistence-api.md) |
| **8** | liveness-crash-restart-host | MVP | aligned | §5, §0.4, §8.2, §6.4, §10.7, §12r2/r3 | methodological | n/a | Slice B. Resolve unrecoverable-state taxonomy + platform scope + FR-057 placement. §ref typo: §7→§5. | [8](8-liveness-crash-restart-host.md) |
| **9** | restore-and-resume-with-link-reestablish | MVP | needs-enrichment | §6.4, §6.2–6.3, §5, §0.4, §10.7, §12r3/r5 | methodological | lean4 (rocq alt) | Net-new `RewireHandle` (WireEstablishedLink aborts on bound cells, LinkEstablish.cs:38-43). Verbatim-address snapshot. | [9](9-restore-and-resume-with-link-reestablish.md) |
| **10** | multi-accept-transport-extension | PREP/FOLLOW-UP | aligned | §4.2–4.5, §0.4, §12r6, App.B | methodological | n/a | Stateful TcpListener; per-accept atomic nonce; keep `ILinkTransport` single-endpoint. Blocking-runner concern deferred to #13. | [10](10-multi-accept-transport-extension.md) |
| **11** | compiled-il-on-the-wire-and-factor-out-compiler | FOLLOW-UP | needs-enrichment | §9.1, §2.4, §2.1–2.2, §3, §0.4, §10.1/10.10, §12r7 | methodological | lean4 (rocq alt) | **Add #5 to depends_on** (ModuleTerm-in-binding). VariableMap must cross wire. Effort lower than "large". | [11](11-compiled-il-on-the-wire-and-factor-out-compiler.md) |
| **12** | antlr4-shared-grammar-spike | EXPERIMENT | needs-enrichment | §10.10, §10.1, §9.1, §12r7, §2.5, §0.4; brief §3.2 | methodological | lean4 (rocq alt) | **Hidden #4 dep** for byte-level "identical IL". Scope verifier-first (parse 100% corpus, dep #1a) then production parser (dep #4+#11). Drop C++. | [12](12-antlr4-shared-grammar-spike.md) |
| **13** | multi-client-control-program-in-glp | FOLLOW-UP | aligned | §4.2–4.5, §7, §0.4, §12r6 | methodological | lean4 (rocq alt) | Soften #11 dep (split #13a source-text/#13b IL). mwm excluded from type-check → Lean 4 fan-in proof. | [13](13-multi-client-control-program-in-glp.md) |
| **14** | cpp-engine-feasibility | EXPERIMENT | needs-enrichment | §10.10, §11 #14, §2.1–2.2, §9.1, §0.4 | methodological | lean4 (rocq alt) | **Scope fork**: narrow to C++ executor only (dep #4,#12); full front-end adds #11. Must emit explicit infeasibility verdict. | [14](14-cpp-engine-feasibility.md) |
| **15** | many-instances-shared-static-memory-cooperative-scheduling | EXPERIMENT/FOLLOW-UP | aligned | §10.10, §11 #15, §6.2–6.3, §0.4, §8.2, §12r2 | methodological | lean4 (rocq alt) | Resolve T2 (in-process vs OS-process) + U1 (chain definition) first. FOLLOW-UP half needs an output gate. | [15](15-many-instances-shared-static-memory-cooperative-scheduling.md) |
| **16** | research-programme-and-llvm-feasibility | EXPERIMENT | aligned | §10.10, §0.3, §2.1–2.2, §6.3–6.4, §4.3/§7, §9.1–9.2, §12r7 | **low** | n/a | Near-complete (both reports drafted). Narrow & close: reports + spike-ownership table + LingoDB citation fix; hibernate LLVM spike on #14. | [16](16-research-programme-and-llvm-feasibility.md) |
| **1.5** | repl-engine-split-mvp-binary-wire-format-intermediate-language-c | UMBRELLA | superseded-candidate | §0.4, §2.1–2.4, §3, §8.1–8.2, §9.1–9.2, §11 #2–#16, §12r1/r7, App.B 1.5 | methodological (decision) | n/a | **CLOSE as superseded.** Fully decomposed into #2–#16; two false premises corrected (§9.1, §9.2). No `/buildkit-specify` for the monolith. | [1.5](1_5-repl-engine-split-mvp-binary-wire-format-intermediate-language-c.md) |

## Roll-up counts

- **Seeds:** 17 (1a, 2–16, monolith 1.5).
- **Alignment:** 11 `aligned` · 5 `needs-enrichment` (#5, #7, #9, #11, #12, #14 — note #14 listed under needs-enrichment ⇒ **6**) · 1 `superseded-candidate` (#1.5).
- **GEPA/DSPy applicability:** 16 `methodological` · 1 `low` (#16).
- **Proof assistant primary:** `lean4` 11 · `n/a` 6 (#3, #8, #10, #16, #1.5; plus #16 research) · `rocq` 0 primary (Rocq is the named *alternative* on 9 seeds — never the primary; see `DECISIONS-FOR-OWNER.md` §3).
- **Kinds:** PREP 4 (1a, 2, 3, 5) · PREP/MVP 1 (#7) · MVP 4 (#6, #8, #9; #6 is the §8.1 MVP) · EXPERIMENT 4 (#4, #12, #14, #16) · EXPERIMENT/FOLLOW-UP 1 (#15) · PREP/FOLLOW-UP 1 (#10) · FOLLOW-UP 2 (#11, #13) · UMBRELLA 1 (#1.5).

**MVP entry point:** dossier §8.1 Slice A → seed **#6**. #1a (methodology base) should
land first so #2–#16 inherit a common metric-table template + proof-assistant policy.
