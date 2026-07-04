# 035+ Deferral / Gap / Inconsistency Audit — "Nothing Lost" Register

**Date**: 2026-07-02. **Method**: adversarial 3-role team — 4 blind scanners over disjoint lenses (deferral-to-oblivion ‖ 3270-terminal spec-vs-code ‖ inconsistency/drift ‖ codex gap/over-claim), cross-verified, curated. Premise: *deferred = at-risk-of-loss until provably homed to a live, tracked feature.*
**Confidence key**: ⊕⊕ corroborated by ≥2 scanners · ⊕ single-scanner.

---

## Part 1 — Register (every item gets a real home + action)

### A. `http3-quic-ws-link-completion` (NEW feature, captured) — DOABLE NOW
| # | item | severity | source | action |
|---|---|---|---|---|
| A1 | **T019** live `glp_repl` *process* I/O bridge unbuilt (only envelope bridge done; `--repl` flag inert) | HIGH | ⊕⊕ (036 audit + role1) | implement via spec-025 link-message interface |
| A2 | **Mesh duplicate-id eviction evicts a LIVE sibling** = routing loss (`Program.cs:253`) | HIGH | ⊕⊕ (036 audit + role1) | fix eviction: reject/rename dup id, never drop the live link |
| A3 | **Gleam relay >1 MiB misroute = silent DATA LOSS** | HIGH | ⊕ role1 (036 code-review brief prose) | fix framing/bounds in the Gleam relay |
| A4 | Demo `AttributeError` on handshake timeout | MED | ⊕ role1 | fix error path |
| A5 | Latent pre-readiness stdout hang | MED | ⊕ role1 | fix startup ordering |
| A6 | **C# host not built in-tree → 9 integration tests skip**; T039 "18/104 green" unreproducible | HIGH | ⊕⊕ (036 audit + role1) | build `csharp/glp_quick_host`, re-run suites, fix the claim |
| A7 | T032a Profile-A wording over-implies (Gleam is a relay; C# terminates QUIC) | LOW | ⊕⊕ | accuracy fix in spec/tasks |
| A8 | T028 path-MTU-degradation explicit test = unhomed follow-up | LOW | ⊕ role1 | add test here |

### B. `http3-quic-ws-link-full-acceptance` (NEW feature, captured) — BLOCKED on toolchain/hardware
| # | item | severity | source | action |
|---|---|---|---|---|
| B1 | Profile C (full BEAM + `quicer`/MsQuic in-process) — needs MSVC (host has MinGW) | HIGH | ⊕⊕ | do on/with `gavri`; promote+specify |
| B2 | Two-host LAN final acceptance (T040) → SC-002/003/004 cross-host unproven | HIGH | ⊕⊕ | needs 2nd host |
| B3 | Marathon durability (T036/T003) — run `mrun-15d7dd0ffbc2` **never persisted** (phantom); FR-013/SC-008 unproven | HIGH | ⊕⊕ | requires a REAL persisted run; retires the phantom |

### C. `037-virtual-3270-term` (terminal feature — has spec, needs tasks + implementation)
The `--tui` prototype (`glp_quick/tui.py`) covers only US1/US3 basics; **most of the feature is unbuilt or unreliable**, `/rcopy` is only one gap.
| # | item | severity | source | action |
|---|---|---|---|---|
| C1 | **US2 page-block transmit + peer-owned received pages** NOT-IMPL (screen buffer never sent; incoming dumped to one shared CHAT page) | HIGH | ⊕ 3270 | core "pages" model — spec tasks + build |
| C2 | **`@name` directed routing NOT-IMPL but help advertises it** (all msgs go to `default_to`) | HIGH | ⊕ 3270 | fix now (prototype bug) + build FR-006 |
| C3 | **FR-005 TTY-fallback NOT-IMPL in `--tui`** (piped stdin errors, no fallback; SC-003 unmet) | HIGH | ⊕ 3270 | fix now (prototype bug) |
| C4 | **`recv_loop` swallows all exceptions + no lock on shared page/peer lists = data race** | HIGH | ⊕ 3270 | fix now (reliability) |
| C5 | **Zero automated tests import `tui.py`** | MED | ⊕ 3270 | add TUI test coverage |
| C6 | US4 joint-edit / pinpoint-overwrite / masks-forms NOT-IMPL | MED | ⊕ 3270 | tasks + build |
| C7 | US5 REPL-in-a-page + agent-sent fillable pages NOT-IMPL | MED | ⊕ 3270 | tasks + build |
| C8 | US6 `/rcopy` CLIENT wizard NOT-IMPL (backend = feature 040) | MED | ⊕⊕ | tasks + build (client only) |
| C9 | US7 bindable PF-keys + FR-025 dynamic legend NOT-IMPL; FR-020 PF13-24 / FR-023 two-strip PARTIAL | LOW | ⊕ 3270 | tasks + build |

### D. `040-rcopy-file-transfer-service` (backend — captured; **spec dir phantom, my doing**)
| # | item | severity | source | action |
|---|---|---|---|---|
| D1 | **`specs/040-…` absent on disk** while roadmap cites it & 037 hard-depends on it → phantom; I deleted the CAPTURE.md after creating the roadmap row | HIGH | ⊕⊕ (role1 + inconsistency) | RESTORE `specs/040-…/CAPTURE.md`; reconcile roadmap state (forward-only CLI shows `specified`) |
| D2 | The `/rcopy` responder service (registry, `/xfer/in` landing, SHA-256 sync, DuckLake provenance, per-root catalog, WAL) | — | — | this is 040's scope; specify later |

### E. `038-result-codec` residuals (core codec VERIFIED genuine — codex confirmed golden/loud-fail/AtomVM tests real)
| # | item | severity | source | action |
|---|---|---|---|---|
| E1 | Whole-Section-15 byte-parity **"final"** deferred to D4 ISA-freeze — **freeze/§15-authoring work not homed to any feature** | MED | ⊕⊕ | owner-gate D4; create/assign a home feature for the §15 authoring |
| E2 | Cyclic-term codec correctness — owner gate D5/FORK-1 OPEN | MED | ⊕ role1 | owner decision (never self-decide) |
| E3 | Live-goal `GlpEngine.runGoalToEnvelope` seam = follow-up, no target named (likely #11) | MED | ⊕ role1 | home to #11 result-envelope-and-deep-resolve |
| E4 | Lean `decode∘encode=id` proof authored but NOT machine-verified (no elan) | LOW | ⊕ role1 | owner-gated; mark Optional |
| E5 | AtomVM version drift: spec says 0.6.6, gated runs on 0.7.999 | MED | ⊕⊕ | update spec text to 0.7.999 |

### F. Cross-cutting drift / housekeeping
| # | item | severity | source | action |
|---|---|---|---|---|
| F1 | Two spec dirs share `036` (`-glp-gleam-baseline-program` ‖ `-http3-quic-ws-link`) | HIGH-confusion | ⊕ inconsistency | document; retro commits "docs(036)" are ambiguous |
| F2 | `036-glp-gleam-baseline-program` spec dir has **no roadmap row** | MED | ⊕ inconsistency | note as the research program (seeded full-gleam/optional epics) |
| F3 | `.specify/feature.json` → shipped `036-http3-quic-ws-link` (stale active pointer) | MED | ⊕ inconsistency | repoint on next feature pipeline start |
| F4 | roadmap-id ≠ spec-dir number (systemic) — the exact confusion that spawned the 038→040 renumber | LOW | ⊕ inconsistency | accept + document convention |
| F5 | roadmap rows for `036-http3` and `039` lack a `spec:` pointer | LOW | ⊕ inconsistency | add pointers |

---

## Part 2 — What was CORRECTLY not orphaned (audit fairness)
- 038 gated float/int64 cases **actually ran + passed on AtomVM 0.7.999** (byte-identical); the codec, golden byte-parity, and loud-fail fuzz tests are genuinely present and green (codex-confirmed).
- 039 `spawn_monitor/1`-undef finding is homed to #36 `glp-gleam-link-layer` (real, refined).
- 036-baseline folds #11/#10 homed to real refined roadmap rows.
- 035 is fully consistent (shipped v2026.06.26.1, roadmap correct).

---

## Part 3 — Allocation summary (proposed homes)
- **Fix-now code** → `http3-quic-ws-link-completion` (A1–A8): REPL bridge, **2 data-loss bugs**, build/re-verify.
- **Blocked** → `http3-quic-ws-link-full-acceptance` (B1–B3): Profile C, two-host, marathon durability.
- **Terminal feature** → `037-virtual-3270-term` (C1–C9): needs a real tasks.md + implementation; 4 fix-now prototype bugs (@name, FR-005, data race, silent-swallow).
- **`/rcopy` backend** → `040-rcopy-file-transfer-service` (D1–D2): restore the phantom spec dir.
- **Codec residuals** → 038 owner-gates + home the §15 authoring + point E3 at #11.
- **Housekeeping** → F1–F5.
