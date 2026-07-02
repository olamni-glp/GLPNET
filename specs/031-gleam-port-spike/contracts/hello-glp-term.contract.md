# Contract: Hello-GLP-Term Smoke

**Artifact**: `docs/research/gleam-atomvm/hello-glp-term/` (entity **E3**) — a self-contained Gleam project.

## Inputs / build

- A standard Gleam project (`gleam.toml`, `manifest.toml`, `src/`, optional `test/`).
- Toolchain: the pinned Gleam + Erlang/OTP recorded in the toolchain inventory.
- Build to BEAM via the Gleam build tool; run on Erlang. JS backend build optional (for the matrix's JS row).

## Behavioural contract (what it MUST demonstrate)

1. **Term construction** — constructs a representative GLP term containing **at least one compound/structure term** and **one unbound-variable analogue**. The constructed term's representation is printed/observable. *(FR-004; US2 acceptance #2)*

2. **One unbound→bound transition, reader-observed** — exactly **one** logic-variable cell starts unbound; a writer binds it; a **separate reader observes** the now-bound value. Primary model: **process/state-holder** (a BEAM process / Gleam actor holding the cell, with reader + writer processes). A **functional sibling** expresses the same single bind via immutable threaded state for contrast. *(FR-004; research R4)*

3. **Observable, correct result** — running on Erlang/BEAM emits the documented, correct representation of the term **and** the observed bound value. *(FR-004; US2 acceptance #2)*

## Output / evidence contract

- `README.md` records, verbatim: the **exact** setup/build/run commands, the **observed BEAM output**, and the **AtomVM attempt** result (success / partial / failure-with-output, or the bring-up blocker). *(FR-009, US3)*
- The recorded commands reproduce the same observed result for a second person on a clean checkout. *(SC-002)*

## Out of scope (MUST NOT attempt)

- Full unification of two terms; suspension/reactivation **scheduling**; bytecode execution; any performance measurement. The single bind is the **bounded** mutable-variable demonstration. *(Assumptions; FR-004)*

## Acceptance checklist (binary)

- [ ] Compiles with the pinned Gleam toolchain. *(US2 acceptance #1)*
- [ ] Term has ≥1 compound/structure + 1 unbound-variable analogue. *(FR-004)*
- [ ] Exactly one unbound→bound bind, observed by a reader. *(FR-004)*
- [ ] Runs on Erlang/BEAM with documented correct output. *(US2 acceptance #2)*
- [ ] AtomVM attempt recorded (result or blocker). *(FR-005, US3)*
- [ ] Re-running recorded commands reproduces the result. *(SC-002, US2 acceptance #3)*
- [ ] No out-of-scope mechanism implemented. *(Assumptions)*
