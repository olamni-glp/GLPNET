# Quickstart — 063 wave-5 consolidated captured triad

## US1 — link two live REPLs over QUIC+WS (after completion)

```bash
# Host A (server end) — glp-quick is the console script (or glp_quick/.venv python -m glp_quick.cli)
glp-quick --server --addr <A-ip> --port 4433 --cert <shared-cert-dir> --repl out/csharp/glp_repl/bin/Debug/net10.0/glp_repl.dll
# Host B (client end)
glp-quick --client --addr <A-ip> --port 4433 --cert <shared-cert-dir> --repl out/csharp/glp_repl/bin/Debug/net10.0/glp_repl.dll
# At B's prompt: send `tmsg(repl_goal,"P1","<goal>.")` to the peer; its REPL
# evaluates it and the tmsg(repl_result,...) returns to B (scripted form:
# glp_quick/tests/test_repl_bridge.py — both directions in ~9 s).
```

Mesh regression: `dotnet test csharp/glp_link.tests --filter mesh_dup_id`
Full re-verify: `dotnet build csharp/glp_quick_host` then
`glp-quick demo --addr 127.0.0.1 --port <udp> --cert <dir> --stack csharp --clients 3`
— every scenario prints an explicit verdict (superseding table:
specs/063-.../baseline.md §T014).

## US2 — durable first-hop messaging drill (SC-004)

```bash
# Terminal 1 — recipient (then Ctrl-C it to simulate offline)
python -m ms_message recipient --station bob --from 127.0.0.1:4501
# Terminal 2 — originator: journal 1000 messages while bob is offline
python -m ms_message originator --station alice --listen 127.0.0.1:4501 --mailbox news --to bob --count 1000
# Restart the ORIGINATOR (durability proof), then bring bob back:
python -m ms_message recipient --station bob --from 127.0.0.1:4501
# Expect: signal → resumable fetch → 1000 messages, exactly once, in order;
# gaps (if any) reported as named gap_events; nothing silently lost.
python -m ms_message status          # journal/position/gap/DLQ summary
python -m ms_message dlq list        # unresolvable targets with reasons
```

## US3 — run a triad engagement

Follow `docs/three-role-orchestration/PROTOCOL.md`: convene the planning
triad on the artifact under review; the critic red-teams blind; the curator
merges mechanically; you (the engineer) decide at each gate. Record lands in
`docs/three-role-orchestration/engagements/`.

## US4 — wave close

`buildkit-roadmap advance` the three consolidated features (link completion,
durable mesh messaging, 3-role orchestration) with receipts, then the wave's
own feature ships via the standard GitFlow.
