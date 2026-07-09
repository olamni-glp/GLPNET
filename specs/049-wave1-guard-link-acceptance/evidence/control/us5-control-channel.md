# US5 — bounded remote test-control over the link (FR-017..019)

- **Criterion**: FR-017 (control channel over the mutual-pin link, reusing GlpMessage + mesh routing),
  FR-018 (fixed whitelist, no remote shell), FR-019 (merge-loadable pure-Python, proven on loopback first)
- **Host(s)**: Olamnit (loopback proof); cross-host drive against gavri pending gavri's control-agent
- **Component**: `glp_quick/src/glp_quick/control.py` — `ControlAgent` (whitelist dispatch), `run_agent`
  (controlled host), `send_command` (controlling host). CLI: `python -m glp_quick.control agent|send …`.
- **Whitelist (FR-018)**: `ping`, `status`, `mesh_selftest`, `echo` — **only**. Any other command
  returns `{"ok": false, "error": "unsupported"}` and executes nothing. No shell/exec/eval/arbitrary-path
  surface exists. The mutual-pin QUIC link is the sole trust boundary.

## FR-018 whitelist safety — `glp_quick/tests/test_control.py` (pure-logic, always runs)
`4 passed` — including `test_unknown_command_returns_unsupported_and_does_not_execute` (exec/shell/eval/
`rm -rf /`/`__import__('os').system(...)` all refused) and `test_whitelist_is_exactly_the_declared_bounded_set`.

## Loopback proof (real server + real control agent + real driver over the link) — 2026-07-08
```
[loopback] server up on 127.0.0.1:64980
[ctl-agent] up as 'ctl' on 127.0.0.1:64980 (whitelist: ping, status, mesh_selftest, echo)
[drive] ping                     -> {'ok': True, 'cmd': 'ping', 'result': {'host': 'Olamnit', 'agent': '1.0', ...}}
[drive] status                   -> {'ok': True, 'cmd': 'status', 'result': {'host': 'Olamnit', 'agent_id': 'ctl', 'version': '1.0', 'uptime_s': 5.1}}
[drive] echo                     -> {'ok': True, 'cmd': 'echo', 'result': {'text': 'hello-control'}}
[drive] definitely_not_allowed   -> {'ok': False, 'cmd': 'definitely_not_allowed', 'error': 'unsupported'}
```
All four are genuine round-trips over a real glp-quick link (C# QUIC hosts). **Verdict: PASS on loopback
(FR-019 precondition met).** Cross-host drive against gavri is the next step (gavri runs the agent).

## Real bug found + fixed during the loopback proof (recorded, constitution II)
First loopback run: `status`/`echo` agent-handled but driver got `no_reply`. Cause: a fixed driver id
(`drv`) reused across successive connections collided with the mesh dup-id incumbent-route rule — the
reply routed to the previous, now-dead link. Fix: **unique driver id per `send_command` call**
(`drv-<pid>-<uuid6>`). Also hardened `send_command` to return `link_dropped_before_send` instead of
raising `OSError` when the server bounces a driver at capacity.

## To drive gavri (once its control agent is up)
gavri runs (alongside its server at 192.168.0.108:8443, `--max-clients 8` for headroom):
`python -m glp_quick.control agent --addr 192.168.0.108 --port 8443 --cert .\glpquick-cert`
Olamnit drives, e.g.:
`python -m glp_quick.control send --addr 192.168.0.108 --port 8443 --cert ./glpquick-cert --cmd mesh_selftest --clients 4`
