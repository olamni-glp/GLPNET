# Contract: Decision Dossier Outline

**Artifact**: `docs/research/gleam-atomvm/dossier.md` (entity **E1**)
**Purpose**: the required section structure the dossier MUST satisfy, each section mapped to the FR/SC and acceptance scenario it discharges. `/bk-implement` writes the dossier against this contract; `/bk-analyze` and review check it against this contract.

The dossier is **self-sufficient**: a reviewer reading only this file can act on it (SC-001, US1 independent test). No section may be left as a placeholder.

## Required sections (in order)

1. **Executive summary & verdict** — names the recommended source basis in one sentence and states the single go / no-go / go-with-revisions verdict up front. *(FR-001, FR-010, SC-001, SC-005)*

2. **Source-language decision**
   - 2.1 Criteria table: rows = {Dart, C#, file-by-file replication}; columns = {source health & currency, structural fit to Gleam, conversion effort, divergence between the two sources}, each cell evidenced. *(FR-001; US1 acceptance #1)*
   - 2.2 The C# candidate is multi-rooted — state which root (`glp_runtime_net/` vs `csharp/` vs `out/csharp/`) is treated as *the* C# candidate, and surface Dart↔C# divergence explicitly. *(Edge case; research R5)*
   - 2.3 Recommendation + one-sentence rationale; the roadmap's C#-lean confirmed or overturned with evidence. *(FR-001, SC-001)*

3. **Build-target matrix** — embeds the matrix per `build-target-matrix.schema.md`. *(FR-002; US4)*

4. **Architectural-fit assessment**
   - 4.1 Mutable heap / WAM-style cells vs Gleam immutability — **backed by the running unbound→bound demonstration** in the smoke (cite the smoke's observed output), not analysis alone. *(FR-006, SC-006; US1 acceptance #2)*
   - 4.2 FCP concurrency / SRSW & suspension-reactivation vs BEAM processes + message passing — stated as the top opportunity, with its bearing on the recommendation. *(FR-006, SC-006)*
   - 4.3 WAM-style bytecode execution & custom heap vs AtomVM's BEAM/OTP subset. *(FR-006, FR-007)*
   - Each finding states **how it affects the recommendation**. *(US1 acceptance #2)*

5. **Downstream re-scope notes** — each heavy feature named with a recommended re-scope **or** "confirmed unchanged": **F5 bytecode runner**, **F6 compiler/loader**, **F9 link layer** (and any other affected feature). Roadmap-actionable. *(FR-007, SC-005)*

6. **Downstream handoff (for F2/F3)** — chosen source basis · assumed `glp_gleam/` project layout & conventions · toolchain versions to build against. *(FR-008, SC-004)*

7. **Conclusion** — restates the single verdict; if go-with-revisions, enumerates the specific roadmap changes required. *(FR-010, SC-005; US1 acceptance #3)*

## Acceptance checklist (binary)

- [ ] Exactly one recommended source basis. *(SC-001)*
- [ ] Criteria table present with all four criteria, every cell evidenced. *(FR-001)*
- [ ] Dart↔C# divergence surfaced as a criterion, not assumed parity. *(Edge case)*
- [ ] Architectural-fit names ≥ the two required findings; mutable-heap finding cites the running smoke. *(SC-006)*
- [ ] Every heavy downstream feature named with re-scope or "unchanged". *(SC-005)*
- [ ] Exactly one go/no-go/go-with-revisions verdict; revisions enumerated if applicable. *(FR-010, SC-005)*
- [ ] Every "it works"/feasibility claim has command+output or citation. *(FR-009)*
- [ ] Reviewer can act using only this document. *(SC-001)*
