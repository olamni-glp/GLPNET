# Done-ness & Final Framework Review — feature 027

**Feature**: `027-refinement-verification-framework` · **Tasks**: T026 (done-ness vs `quickstart.md`) +
T028 (final framework review). Closes the success criteria + the entity→requirement coverage. Every
row was checked against the actual artifact (and, for the spikes, the recorded real-tool `RESULT.md`
that reproduces). A criterion not met would be flagged here as a gap — none is.

## 1. Success-criteria traceability (T026, vs `quickstart.md` done-ness map)

| SC | Criterion | Satisfied by | Status |
|---|---|---|---|
| **SC-001** | Template present | [`METRIC-COMBINATION-TEMPLATE.md`](METRIC-COMBINATION-TEMPLATE.md) §1 | ✅ |
| **SC-002** | Loop matches `optimize.py:257–335` seams, zero unmatched | [`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §1.1 seam map | ✅ |
| **SC-003** | No-API grep clean on every refinement/verification path | [`NO-API-GATE.md`](NO-API-GATE.md) (T010 + T025: spikes literally zero) | ✅ |
| **SC-004** | Six formal-tooling slots enumerated (name + threshold-shape + dep-pointer) | [`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §4 (ANTLR4 · MLIR · byte-parity · Lean 4 · SMT · SPIN+armoury) | ✅ |
| **SC-005** | Five Shapiro criteria mapped mandatory/advisory | [`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §5 (5 criteria, each mapped) | ✅ |
| **SC-006** | Lean spike recorded against real tool | [`../spikes/lean/RESULT.md`](../spikes/lean/RESULT.md) — **PROVED**, 5/20 attempts, Lean 4.30.0 | ✅ |
| **SC-007** | MLIR spike recorded against real tool | [`../spikes/mlir/RESULT.md`](../spikes/mlir/RESULT.md) — **PASS** `decode(encode(p))==p`, MLIR 22.0.0 | ✅ |
| **SC-008** | Template worked example well-formed + formal tier present | [`METRIC-COMBINATION-TEMPLATE.md`](METRIC-COMBINATION-TEMPLATE.md) §2 (#5: 7 rows, 3 formal; T008-validated) | ✅ |
| **SC-009** | Each spike reproducible (committed command + pinned versions) | three `run.sh`/`run.ps1` + `tool-versions.txt`; all re-run exit 0 | ✅ |
| **SC-010** | Three highest-risk claims each have real-tool empirical evidence | the three `RESULT.md` (see §3) | ✅ |
| **SC-011** | SPIN spike: deadlock-free + progress (or counterexample) | [`../spikes/spin/RESULT.md`](../spikes/spin/RESULT.md) — **PASS**, SPIN 6.5.1 | ✅ |
| **SC-012** | Armoury ≥7 tools (paradigm/engine/strength/best-for) + selection | [`PROTOCOL-VERIFICATION-ARMOURY.md`](PROTOCOL-VERIFICATION-ARMOURY.md) §2 (7 tools) + §4 | ✅ |

**All 12 success criteria met.**

## 2. Entity → requirement coverage (T028, vs `data-model.md`)

The `data-model.md` coverage map enumerates **nine** conceptual entities (E1–E9) — note: the T028 task
text says "five", a stale count; the authoritative map is the nine-row table, all confirmed satisfied:

| Entity | Primary FRs | Closes SC | Delivered by |
|---|---|---|---|
| E1 Metric table | FR-003, FR-020/021 | SC-001, SC-008 | METRIC-COMBINATION-TEMPLATE.md |
| E2 Refinement loop | FR-010–013 | SC-002, SC-003 | REFINEMENT-METHOD §1/§1.1 + NO-API-GATE.md |
| E3 Formal-tooling slots | FR-022 | SC-004 | REFINEMENT-METHOD §4 |
| E4 Lean tactic loop | FR-030–035 | SC-006 | LEAN-TACTIC-LOOP.md + spikes/lean/* |
| E5 MLIR dialect | FR-040–043 | SC-007 | MLIR-GLP-DIALECT.md + spikes/mlir/* |
| E6 Shapiro mapping | FR-050–051 | SC-005 | REFINEMENT-METHOD §5 |
| E7 Interactive spec step | FR-060–061 | SC-001, SC-008 | INTERACTIVE-SPEC-STEP.md |
| E8 Validation spike | FR-035/043/080, FR-070–074 | SC-009, SC-010 | the three spikes/*/RESULT.md |
| E9 Wire-protocol model | FR-076–081 | SC-011, SC-012 | PROTOCOL-VERIFICATION-ARMOURY.md + spikes/spin/* |

**All nine entity→requirement coverages satisfied.**

## 3. SC-010 — the three highest-risk claims, each with real-tool evidence

The owner directive (FR-070, 2026-06-09): no methodology claim is accepted on desk research; each
highest-risk claim must be validated by a runnable experiment against the **real tool**. All three
are recorded and reproduce:

| Risk claim | Real tool | Evidence | Outcome |
|---|---|---|---|
| MLIR is a viable IL substrate (`decode(encode(p)) ≡ p`) | compiled-LLVM MLIR 22.0.0 (WSL2) | `../spikes/mlir/RESULT.md` + `run.sh` | ✅ round-trip PASS |
| A bounded Claude-over-kernel Lean loop can discharge a GLP property | Lean 4.30.0 (WSL2) | `../spikes/lean/RESULT.md` + `run.sh` | ✅ PROVED, 5/20 |
| A minimal front↔back protocol is deadlock-free + makes progress | SPIN 6.5.1 (WSL2) | `../spikes/spin/RESULT.md` + `run.sh` | ✅ PASS |

No LM sits on any verification path (the kernel / oracle / model-checker decides); the no-API rule
holds across all three (SC-003, §1 above).

## 4. Verdict

Feature **027-refinement-verification-framework** meets all 12 success criteria and all 9
entity→requirement coverages; the three highest-risk methodology claims are each backed by a recorded,
reproducible real-tool run. The framework is ready for the engine-separation successor seeds (#2–#16)
to instantiate. Open deferrals are tracked in [`DEFERRALS.md`](DEFERRALS.md) (DEF-B1/H1/A3 partly
de-risked here; DEF-B2 citation still open — non-blocking per FR-042).
