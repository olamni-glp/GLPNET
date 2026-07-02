# Implementation Plan: Verify erlang:monitor on AtomVM 0.6.6 (M2-0 spike)

**Feature**: `039-m2-0-verify-erlang-monitor-atomvm` · **Branch**: `039-...` · **Date**: 2026-06-30
**Spec**: [spec.md](./spec.md)

## Approach

A **verification spike**, not runtime code. Write the smallest possible BEAM program that exercises `erlang:monitor(process, Pid)` and observes the `{'DOWN', Ref, process, Pid, Reason}` message, build it to AtomVM-loadable form, run it on **AtomVM 0.6.6** (the F1 toolchain), and record the observed behavior as a verdict + evidence.

The probe is written in **Erlang** (the question is the VM primitive, not the source language; Erlang keeps the probe minimal and avoids Gleam stdlib indirection over `monitor`). It is built with the same toolchain F1 used (`031-gleam-port-spike` → WSL Ubuntu, OTP 25, AtomVM 0.6.6; PackBEAM to `.avm`).

## Technical Context

- **Host**: WSL Ubuntu; OTP 25.3.2.8; AtomVM 0.6.6 (per `docs/research/gleam-atomvm/`).
- **Build**: compile `.erl` → `.beam` (erlc), pack to `.avm` (AtomVM PackBEAM / `atomvm_rebar3_plugin` or the `packbeam` tool), run with the `AtomVM` executable.
- **Observation channel**: the probe prints (`erlang:display/1` or `io:format`) what it receives; stdout is the evidence.
- **Reference**: on standard BEAM, `monitor` + DOWN is guaranteed; the question is solely AtomVM 0.6.6 fidelity.

## Phases

1. **Toolchain confirm** — confirm WSL + AtomVM 0.6.6 + erlc are runnable (reuse F1 setup); if absent, record the gap (the spike cannot run without the host).
2. **MVP** — probe: `self()` monitors a spawned process B; B exits **normally**; assert/record whether a `'DOWN'` with reason `normal` arrives. Build + run on AtomVM. This alone answers the core M2 question for the happy path.
3. **Abnormal exit** — B crashes (`exit(B, kill)` / `error(...)`); record the DOWN + reason.
4. **Edge + fallback** — monitor an already-dead Pid; and IF monitor is absent/partial, probe `link` + `process_flag(trap_exit,true)` → `{'EXIT',...}` as the fallback inventory.
5. **Verdict** — write `SPIKE-RESULT.md`: verdict ∈ {works, partial, absent} + evidence (source + stdout) + (if not works) the D10 fork options for #36/#30/#21.

## Risks / Notes

- AtomVM may exit before the DOWN is scheduled → keep the main process blocked on `receive` with a timeout that is the *negative* signal.
- AtomVM 0.6.6 `monitor` reason fidelity may differ from OTP (e.g. `normal` vs `noproc`) — record the ACTUAL term, do not assume.
- If the WSL/AtomVM host is not provisioned in this environment, the spike's run step is blocked on host availability — record that as the result state rather than fabricating a verdict.

## Constitution Check

No violations: read-only verification spike; no GLP language/runtime change; no shared-state mutation; additive artifacts only.
