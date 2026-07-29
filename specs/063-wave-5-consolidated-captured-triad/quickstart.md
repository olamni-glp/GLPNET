# Quickstart — 063 wave-5 consolidated captured triad

## US1 — link two live REPLs over QUIC+WS (after completion)

```bash
# Host A (server end)
python -m glp_quick --server --bind <A-ip>:4433 --repl out/csharp/glp_repl/bin/Debug/net10.0/glp_repl.exe
# Host B (client end)
python -m glp_quick --client --connect <A-ip>:4433 --repl out/csharp/glp_repl/bin/Debug/net10.0/glp_repl.exe
# At B's prompt: type a goal; A's REPL evaluates it; result returns to B.
```

Mesh regression: `dotnet test csharp/glp_link.tests --filter mesh_dup_id`
Full re-verify: `(cd out/csharp/glp_quick_host && dotnet build)` then the 036
demo suite script — every scenario prints an explicit verdict.

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
