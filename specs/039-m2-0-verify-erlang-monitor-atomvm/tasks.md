# Tasks: Verify erlang:monitor on AtomVM 0.6.6 (M2-0 spike)

**Feature**: `039-m2-0-verify-erlang-monitor-atomvm` · **Plan**: [plan.md](./plan.md)

Dependency-ordered. `[MVP]` = the minimum that answers the core M2 question.

- [x] **T001** Confirm the WSL AtomVM 0.6.6 toolchain is runnable (erlc, AtomVM exe, PackBEAM); record versions. → OTP 25 (`/usr/bin`), AtomVM 0.6.6 at `/opt/atomvm/AtomVM-static` (static build; dynamic build blocked by absent `libmbedtls.so.10` on Ubuntu 24.04); stdlib `atomvmlib-v0.6.6.avm`. No PackBEAM needed — AtomVM-static runs raw `.beam` + lib `.avm`.
- [x] **T002 [MVP]** Write the minimal Erlang probe `monitor_probe.erl`. → Revised to use `erlang:monitor(process, Pid)` **directly** (FR-001) with a go/exit handshake, because `spawn_monitor/1` is `undef` on AtomVM 0.6.6.
- [x] **T003 [MVP]** Build `monitor_probe` and run on AtomVM 0.6.6; capture stdout. → `{normal_exit,{down,normal}}` arrived. **MVP done.**
- [x] **T004** Extend the probe with an **abnormal** exit case; record the DOWN + reason. → `{abnormal_exit,{down,boom}}`.
- [x] **T005** Edge: monitor an already-dead Pid; record behavior. → `{already_dead,{down,noproc}}`. Monitor is fully present → no `link`/`trap_exit` fallback probe needed.
- [x] **T006** Write `SPIKE-RESULT.md`: verdict + evidence + versions. → Verdict **WORKS** (monitor/DOWN identical to OTP-25; only `spawn_monitor/1` convenience BIF absent). Evidence: `spike/m2-0-monitor/{monitor_probe.erl,run-log.txt}`. D10 fork NOT triggered.
- [x] **T007** Surface the verdict to the owner. → Surfaced 2026-06-30: verdict **works**, so no D10 decision required; only an FYI on the `spawn_monitor/1` caveat for #36.

## Notes

- T001–T003 are the MVP (core happy-path verdict). T004–T005 harden it. T006–T007 record + surface.
- Spike artifacts live under `spike/m2-0-monitor/` (probe source + run log) and `specs/039-.../SPIKE-RESULT.md` (verdict).
