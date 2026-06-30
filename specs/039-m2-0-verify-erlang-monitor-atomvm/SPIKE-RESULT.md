# M2-0 Spike Result: erlang:monitor on AtomVM 0.6.6

**Feature**: `039-m2-0-verify-erlang-monitor-atomvm` · **Date**: 2026-06-30
**Status**: ⏳ **IN PROGRESS — MVP done (OTP-25 reference established); AtomVM 0.6.6 run BLOCKED on host provisioning.**

## Verdict

**PENDING** — cannot be issued yet. The AtomVM-specific run (the whole point) requires AtomVM 0.6.6, which is **not provisioned** in this environment. Per spec FR-005/plan risk note, the host-gap is recorded rather than fabricating a verdict.

## What was done (MVP / T002–T003 on the reference)

Probe `spike/m2-0-monitor/monitor_probe.erl` (`start/0` = AtomVM entry point; also runs on stock OTP BEAM) exercises three cases and prints the observed message via `erlang:display/1`.

**Reference run on OTP-25 (WSL Ubuntu, `erlc` + `erl`):**

```
{normal_exit,{down,normal}}
{abnormal_exit,{down,boom}}
{already_dead,{down,noproc}}
done
```

So on stock OTP-25 BEAM, `erlang:monitor`/`spawn_monitor` + the `{'DOWN',Ref,process,Pid,Reason}` message behave exactly as expected (normal→`normal`, crash→`boom`, already-dead→`noproc`). **These are the reference outcomes the AtomVM 0.6.6 run must match** for a `works` verdict.

## Toolchain state (probed 2026-06-30)

- WSL Ubuntu: `erl`/`erlc` = **/usr/bin** (OTP **25**) ✓ ; `gleam` = /usr/local/bin ✓
- **AtomVM**: NOT on PATH, not in `~`/`/opt`/`/usr/local`/build dirs. The AtomVM 0.6.6 executable that F1 (`031-gleam-port-spike`) used is **not present** in this environment.

## Remaining (T003 AtomVM / T004 / T005 / T006)

1. **Provision AtomVM 0.6.6** (generic_unix) in WSL — build from source (cmake + libmbedtls-dev + the OTP libs) or obtain a prebuilt binary.
2. PackBEAM `monitor_probe.beam` → `monitor_probe.avm`; run with `AtomVM monitor_probe.avm`; capture output.
3. Compare against the OTP-25 reference above → verdict {works | partial | absent}.
4. If partial/absent: probe `link` + `process_flag(trap_exit,true)` fallback, then surface the **D10 fork** options for #36 / #30 / #21.

## Owner decision needed

The AtomVM run is blocked on provisioning. Options: (A) build/obtain AtomVM 0.6.6 in WSL now and finish the run; (B) defer the AtomVM run until the host is provisioned. The probe + reference are ready either way.
