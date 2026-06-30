# M2-0 Spike Result: erlang:monitor on AtomVM 0.6.6

**Feature**: `039-m2-0-verify-erlang-monitor-atomvm` · **Date**: 2026-06-30
**Status**: ✅ **COMPLETE — verdict issued from a real AtomVM 0.6.6 run.**

## Verdict

**WORKS.** On AtomVM 0.6.6, `erlang:monitor(process, Pid)` and the
`{'DOWN', Ref, process, Pid, Reason}` message behave **identically to stock
OTP-25 BEAM** for all three probed cases (normal exit, abnormal exit,
already-dead pid). Failure detection — a monitoring process learning, *as a
message*, that a peer has died — is therefore **available and faithful** on
AtomVM 0.6.6. The M2 fault-as-data model (#36) and OTP-supersession of the C#
liveness host (#30/#21) rest on a verified primitive, not an assumption.

**One caveat (not a monitor gap):** the `erlang:spawn_monitor/1` *convenience
BIF* (spawn+monitor in one call) is **absent** on AtomVM 0.6.6 (`undef`). This
does **not** affect the verdict — the monitor primitive itself is complete.
M2 code must establish a monitor with the two-step form
`Pid = spawn(Fun), Ref = erlang:monitor(process, Pid)` instead of
`spawn_monitor/1`. This is the idiomatic form anyway and loses no capability.

**D10 fork: NOT triggered.** D10 (owner choice of a fallback fault model) fires
only if monitor is *absent* or *partial*. It is neither. No owner decision is
required on the fault model; the #36 link-layer fault model is cleared to use
`monitor`/`'DOWN'` directly.

## Evidence

Probe: `spike/m2-0-monitor/monitor_probe.erl` (`start/0` = AtomVM entry point;
runs unchanged on stock OTP BEAM). It uses `erlang:monitor(process, Pid)`
**directly** (FR-001) with a `go`/exit handshake so the monitor is established
*before* the monitored process exits. Full reproducible log (versions, build,
runs, 5× reliability): `spike/m2-0-monitor/run-log.txt`.

**AtomVM 0.6.6 (`AtomVM-static monitor_probe.beam atomvmlib-v0.6.6.avm`):**

```
{spawn_monitor,{unavailable,error,undef}}
{normal_exit,{down,normal}}
{abnormal_exit,{down,boom}}
{already_dead,{down,noproc}}
done
```

**Stock OTP-25 reference (`erl -noshell -eval monitor_probe:start()`):**

```
{spawn_monitor,available}
{normal_exit,{down,normal}}
{abnormal_exit,{down,boom}}
{already_dead,{down,noproc}}
done
```

Side-by-side, the only difference is `spawn_monitor` (available on OTP,
unavailable on AtomVM). The `monitor`/`'DOWN'` results are byte-identical.

| Case (FR-002) | OTP-25 | AtomVM 0.6.6 | Match |
|---|---|---|---|
| normal exit → DOWN reason | `normal` | `normal` | ✅ |
| abnormal exit (`exit(boom)`) → DOWN reason | `boom` | `boom` | ✅ |
| monitor already-dead pid → DOWN reason | `noproc` | `noproc` | ✅ |
| `spawn_monitor/1` convenience BIF | available | **undef (absent)** | ⚠ caveat |

**Note on the AtomVM crash report in the log:** the `**End Of Crash Report**`
block AtomVM prints during the run is AtomVM's default reporting of the
*intentional* `exit(boom)` termination of the monitored process P2 (the log
shows `monitored by <0.1.0> ref=2`). It is expected, corroborates that AtomVM
observed the abnormal exit, and does not affect the result — the main process
received every `'DOWN'` and the VM exited 0.

## Reliability (SC-001, SC-004)

5/5 consecutive AtomVM runs produced byte-identical output (see run-log
"Reliability" section). Result is deterministic and reproducible by re-running
the recorded command on the same toolchain.

## Toolchain (FR-005)

- Host: Ubuntu 24.04.3 LTS (WSL).
- OTP release **25** / ERTS 13.2.2.5 (`erlc` + `erl`, `/usr/bin`).
- AtomVM **0.6.6**, binary `/opt/atomvm/AtomVM-static` (generic_unix, static
  build). The dynamic build `/opt/atomvm/AtomVM` fails on this host with
  `libmbedtls.so.10: cannot open shared object file` (Ubuntu 24.04 ships
  mbedtls 3.x / `.so.21`, AtomVM 0.6.6 was linked against mbedtls 2.x). The
  static binary has no such dependency and is the one to use here.
- Stdlib: `/opt/atomvm/atomvmlib-v0.6.6.avm`. No PackBEAM step needed —
  AtomVM-static accepts the raw `.beam` plus the lib `.avm` on its command line.

## Fallback inventory (FR-004)

Not required — verdict is **works**, so no fallback fault model is needed. (Had
monitor been absent/partial, the next probes would have been `link` +
`process_flag(trap_exit, true)` → `{'EXIT', Pid, Reason}`.)

## Downstream (FR-007)

- **#36 (link-layer fault model)**: cleared to use `erlang:monitor`/`'DOWN'`.
  Implementation note: use `spawn` + `erlang:monitor(process, Pid)`, not
  `spawn_monitor/1`.
- **#30/#21 (OTP-supersession of the C# liveness host)**: the BEAM-side failure
  signal that supersession depends on is confirmed present on AtomVM 0.6.6.
- Feeds RISK-PROOF-distDeref (PI:17), GAP-G6 (PB:170), FB-M2-20 (PB:130) with a
  positive monitor result.
