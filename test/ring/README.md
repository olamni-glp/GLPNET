# `test/ring` — ring delivery conformance

Guards for `specs/101-gleam-capability-delivery/contracts/ring-delivery.md` (C1–C6).

Runs **alongside** `test/parity/`, never replacing it:

| suite | measures |
|---|---|
| `test/parity/run_gleam_corpus.sh` | Dart-vs-Gleam agreement over the 206 pinned corpus cases |
| `test/ring/run_ring_tests.sh` | whether the **ring delivery contract** holds |

```
bash test/ring/run_ring_tests.sh
```

## Why the contract is shaped this way

The engineer directive needs one GLP capability on **BEAM at the workstation (L1b)** *and* on
**AtomVM in the MAUI Blazor Hybrid app (L1a/L2)**. `LATTICE.md` line 27 forbids L1a and L1b sharing
anything directly and pushes whatever both need down into L0 — but L0 admits **zero third-party
runtime dependencies**, and BEAM and AtomVM are both third-party runtimes.

Taken literally, the directive is **unsatisfiable**. The shape LATTICE line 35 already prescribes is
the one that works: **the contract sits at L0 and is runtime-free; each ring carries its own
realization of it.** Recorded as `008` FR-017 / FR-018. The consequence for glpnet is that its
delivery mode is **resynthesis, never copy**.

## The three outcomes, and why `pending` exists

| outcome | meaning | counted as evidence? |
|---|---|---|
| `pass` | the guard held | yes |
| `fail` | the guard is violated | — |
| `pending` | the guard is in place; **what it guards is not built yet** | **no** |
| `skip` | the test's **premise** does not hold on this platform (C5) — reason mandatory | **no** |

Suite exit codes: **0** green · **1** red · **2** pending.

A pending run does **not** exit 0. Because these guards are written before the things they protect
(C6), a pre-implementation run is legitimately red — but it must be red in a way that *names what is
missing*, rather than green in a way that reads as evidence. That is the same rule C4-R applies to
rings: **an unbuilt thing never reads as a pass.**

## The guards

| file | task | guards |
|---|---|---|
| `test_contract_purity.sh` | T004, T005 | C1-R runtime-dep-in-contract fails the build (SC-004); C2-R admission-by-name is refused **with the name quoted** (SC-005) |
| `test_report_shape.sh` | T006–T008 | C4 report shape: mandatory denominator (SC-002), `attempted = agreed + diverged + excused` exactly (SC-007), every excused case carries a reason (FR-007), `not_run[]` present (FR-006) |
| `test_aggregate.sh` | T009 | C4-R — build **one** ring, the aggregate must **refuse** (SC-006) |
| `test_mutation.sh` | T010 | C6 — replacing a guard with a no-op must turn the suite **RED** (SC-003) |
| `test_platform_conditional.sh` | T011 | C5 — a vacuous premise **skips with a named reason** (FR-009) |
| `test_retention.sh` | T022 | FR-005 — no file from `glp_runtime/`, `glp_multiagent/` or `programs/` in the delivered set |

## Every guard carries its own control

A guard that can only pass is not a guard. Each file above pairs its refusal case with the
converse, so the trivial cheat scores red:

- a report parser that **rejects everything** satisfies T006–T008 — so `test_well_formed_report_is_accepted` asserts a good report is accepted;
- an aggregate that **refuses everything** satisfies T009 — so `test_complete_aggregate_is_accepted` asserts a complete two-ring set succeeds;
- an **empty** delivery manifest trivially contains no forbidden file — so `test_manifest_is_not_vacuously_empty` asserts the manifest actually delivers something.

This is not hypothetical caution. Wave-22's review found a mutation test in this repo that stayed
**green under a no-op validator**, in a feature whose entire subject was verification. Four shipped
checks here could not fail.

**On its first run, this suite caught a defect in itself.** `test_contract_purity.sh`'s
`_c1r_exists` predicate grepped the tree for the token `C1-R` — and matched the *doc comment* in
`src/glp/contract/surface.gleam`, concluding enforcement existed because prose mentioned it. It now
requires an executable artifact (`check_contract_purity.sh`). Mentioning a rule is not enforcing one.

## What is deliberately not here

- **T017 — the AtomVM unsupported-construct enumeration — is UNMEASURED**, and it is the critical
  path for the app ring. Everything downstream of it (T018, T019) is blocked on a measurement nobody
  has taken. It must not be guessed. The one thing known: `gleam_otp` is excluded because its
  `proc_lib` use is outside AtomVM's BEAM/OTP subset.
- **The MAUI Blazor Hybrid host** is target-side and absent here (`maui` = 0 occurrences in glpnet).
  T019 reports host-side conformance as **UNREAD with a named reason**. Do not synthesize a stand-in
  host to make a suite green.
- Parity is over **206 pinned cases**, not the 384-test unified suite. 100% there is not total
  semantic equivalence.
