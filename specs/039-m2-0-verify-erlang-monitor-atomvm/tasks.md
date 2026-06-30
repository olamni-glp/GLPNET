# Tasks: Verify erlang:monitor on AtomVM 0.6.6 (M2-0 spike)

**Feature**: `039-m2-0-verify-erlang-monitor-atomvm` · **Plan**: [plan.md](./plan.md)

Dependency-ordered. `[MVP]` = the minimum that answers the core M2 question.

- [ ] **T001** Confirm the WSL AtomVM 0.6.6 toolchain is runnable (erlc, AtomVM exe, PackBEAM); record versions. If absent → record host-gap as the result state and stop (do not fabricate a verdict).
- [ ] **T002 [MVP]** Write the minimal Erlang probe `monitor_probe.erl`: `self()` monitors a spawned B; B exits **normally**; main `receive`s and displays the message (or a timeout marker). (depends: T001)
- [ ] **T003 [MVP]** Build `monitor_probe` → `.avm` and run on AtomVM 0.6.6; capture stdout. Record whether `{'DOWN',Ref,process,B,normal}` (or equivalent) arrived. (depends: T002) — **MVP checkpoint here.**
- [ ] **T004** Extend the probe with an **abnormal** exit (kill/crash) case; rebuild, run, record the DOWN + reason term. (depends: T003)
- [ ] **T005** Edge: monitor an already-dead Pid; record behavior. IF monitor absent/partial → add `link`+`trap_exit` fallback probe and record `{'EXIT',...}` availability. (depends: T003)
- [ ] **T006** Write `SPIKE-RESULT.md`: verdict ∈ {works, partial, absent} + evidence (source + stdout) + versions + (if not works) the D10 fork options for #36 fault model and #30/#21 OTP-supersession. (depends: T004, T005)
- [ ] **T007** Surface the verdict to the owner as the D10 decision input (only a decision is needed if verdict ≠ works). (depends: T006)

## Notes

- T001–T003 are the MVP (core happy-path verdict). T004–T005 harden it. T006–T007 record + surface.
- Spike artifacts live under `spike/m2-0-monitor/` (probe source + run log) and `specs/039-.../SPIKE-RESULT.md` (verdict).
