# Feature Specification: Verify erlang:monitor on AtomVM 0.6.6 (M2-0 gating spike)

**Feature Branch**: `039-m2-0-verify-erlang-monitor-atomvm`
**Created**: 2026-06-30
**Status**: Draft
**Input**: Promoted full-gleam M2 feature (roadmap `m2-0-verify-erlang-monitor-atomvm`). ADD-NEW gating spike from `docs/research/glp-gleam-baseline/pipelines/P1b-realignment/DISPOSITIONS.md` row M2-0: verify `erlang:monitor` ahead of the #36 link-layer fault model; owner-fork **D10** if monitor is absent/partial on AtomVM 0.6.6. Feeds RISK-PROOF-distDeref (PI:17), GAP-G6 (PB:170), FB-M2-20 (PB:130).

## Overview

The M2 (linked, multi-instance) GLP design assumes BEAM-style **failure detection** — a monitoring process learns, as a message, that a peer process has died — to build its **fault-as-data** model and to let OTP supervision supersede the C# liveness host (#30/#21). That assumption was **never opened on AtomVM** (P2 C-68/C-127): AtomVM 0.6.6 is a pre-1.0 embedded BEAM and may implement `erlang:monitor/2` (and the `{'DOWN', Ref, process, Pid, Reason}` message) fully, partially, or not at all. This spike produces a **definitive, evidence-backed verdict** so the #36 fault model can be committed — or the **D10 fork** triggered with a concrete fallback.

This is a **verification spike**, not a runtime feature: its deliverable is a recorded verdict + reproducible evidence, not shipped GLP runtime code.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Definitive monitor verdict on AtomVM 0.6.6 (Priority: P1)

The architect needs to know whether a process can `monitor` another on AtomVM 0.6.6 and reliably receive a `'DOWN'` message when the monitored process exits (normally and abnormally), so the M2 fault-as-data model is grounded rather than assumed.

**Why this priority**: This is the entire purpose of the spike and the gate ahead of #36. Without it the M2 fault model rests on an untested assumption.

**Independent Test**: Run a minimal AtomVM 0.6.6 program where process A monitors process B, B exits (normal, then abnormal), and assert whether A receives a correctly-shaped `'DOWN'` message in each case; record the observed behavior.

**Acceptance Scenarios**:

1. **Given** AtomVM 0.6.6, **When** A monitors B and B exits normally, **Then** the spike records whether A receives `{'DOWN', Ref, process, B, normal}` (and the actual shape if it differs).
2. **Given** AtomVM 0.6.6, **When** A monitors B and B exits abnormally (crash), **Then** the spike records whether A receives a `'DOWN'` with the crash reason.
3. **Given** AtomVM 0.6.6, **When** A monitors an already-dead Pid, **Then** the spike records the behavior (immediate DOWN vs nothing vs error).

### User Story 2 - Fallback inventory if monitor is absent/partial (Priority: P2)

If `monitor` is absent or partial, the spike documents which adjacent failure-detection primitives DO work on AtomVM 0.6.6 (`link` + `trap_exit`/`{'EXIT',...}`, raw process-exit observation) so a D10 fallback fault model can be chosen.

**Why this priority**: A negative result is only actionable if it names what is available instead — that is the D10 fork content.

**Independent Test**: If US1 shows monitor absent/partial, run `link`+`process_flag(trap_exit, true)` probes and record which `'EXIT'`/exit-signal behaviors are delivered.

**Acceptance Scenarios**:

1. **Given** monitor is absent/partial, **When** A links to B with `trap_exit` set and B exits, **Then** the spike records whether A receives `{'EXIT', B, Reason}`.

### Edge Cases

- Monitored process **already dead** at monitor time (immediate-DOWN semantics vary across BEAM impls).
- **Abnormal vs normal** exit reason fidelity (`normal` vs a crash term).
- `demonitor` behavior (does it suppress an in-flight DOWN?) — record if cheap, not required.
- AtomVM running the program to **completion/exit** before the DOWN is delivered (scheduling/ordering) — the harness must keep the monitor alive long enough to observe.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The spike MUST run on **AtomVM 0.6.6** (the F1-verified host: WSL Ubuntu, OTP 25, AtomVM 0.6.6) and exercise `erlang:monitor(process, Pid)` with a monitored process that exits.
- **FR-002**: The spike MUST observe and record whether the monitoring process receives a `'DOWN'` message for **normal** exit and for **abnormal** exit, including the actual message shape/reason observed.
- **FR-003**: The spike MUST emit an explicit verdict ∈ {**works**, **partial**, **absent**} with the evidence (program source + observed output) that justifies it.
- **FR-004**: If the verdict is **partial** or **absent**, the spike MUST inventory the working fallback primitives (`link`+`trap_exit` `'EXIT'` messages, and/or raw exit observation) and name the **D10 fork** options for the #36 fault model and the #30/#21 OTP-supersession.
- **FR-005**: The spike MUST be **reproducible**: the program source, build/run commands, and AtomVM/OTP/Gleam versions are recorded so the verdict can be re-verified.
- **FR-006**: The spike MUST NOT build the actual link layer (#36) or any GLP runtime code; it delivers only the verdict + evidence + (if needed) the D10 fork framing.
- **FR-007**: The verdict MUST be reported back to the owner as the **D10 decision input** (if not "works"); the spike does NOT itself pick the fallback (that is the owner's D10 ruling).

### Key Entities

- **Monitor probe**: a minimal program — monitor → kill monitored process → await DOWN — on AtomVM 0.6.6.
- **Verdict**: {works | partial | absent} + evidence + (if applicable) D10 fork options.
- **Fallback inventory**: the set of failure-detection primitives that DO work on AtomVM 0.6.6 (only populated on partial/absent).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A minimal program runs on AtomVM 0.6.6 and produces recorded evidence of monitor/DOWN behavior for both normal and abnormal exit (2/2 cases observed and logged).
- **SC-002**: The spike emits exactly one verdict ∈ {works, partial, absent}, justified by the recorded evidence.
- **SC-003**: If the verdict is not "works", the D10 fork options + a working fallback inventory are documented (0 unactionable negative results).
- **SC-004**: The result is reproducible — re-running the recorded commands on the same toolchain reproduces the verdict.

## Assumptions

- The F1 toolchain (WSL Ubuntu, Gleam 1.17.0 / OTP 25.3.2.8 / AtomVM 0.6.6) from `docs/research/gleam-atomvm/` is available; the probe may be written in Erlang or Gleam as convenient (the question is the VM primitive, not the language).
- "Works" means: a DOWN message of the correct shape is delivered for both normal and abnormal monitored-process exit, reliably across repeated runs.
- D10 (the fallback choice if not "works") is an **owner** decision; this spike supplies its inputs only.

## Dependencies

- F1 AtomVM toolchain (`docs/research/gleam-atomvm/`, shipped `031-gleam-port-spike`).
- Feeds #36 (link-layer fault model), #30/#21 (OTP-supersession), RISK-PROOF-distDeref, GAP-G6, FB-M2-20.
