<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Quickstart: durable listener service box (064)

## Register the chat service (once)

Create `glpservice/resume.json` at the repo root:

```json
{
  "version": 1,
  "program": "programs/tests/quic/quic_chat.glp",
  "goal": "main(olamnit, R).",
  "enabled": true,
  "replay": true
}
```

## Run

```
dotnet run --no-build --project out/csharp/glp_repl
```

Expected startup lines (after the banner):

```
resume: arming main(olamnit, R). from programs/tests/quic/quic_chat.glp
resume: replayed <N> message(s) from the log
```

The listener is accepting; a peer (`main(gavri, R).` on the other host) can
connect and chat. Kill the process, run it again — the service re-arms itself
and the full chat history is back before the first new message.

## Disable / remove

Set `"enabled": false` (keeps the file inspectable) or delete
`glpservice/resume.json`. With no registration the REPL starts exactly as
before the feature.

## Fresh start (keep registration, drop history)

Set `"replay": false` — arms without replay. (The log itself is append-only;
retention/compaction is future work.)

## Troubleshooting

| Symptom | Meaning |
|---|---|
| `resume: invalid registration at <path>: <cause>` | JSON malformed / wrong version — fix the file; REPL is still usable |
| `resume: program load failed: <cause>` | registered `.glp` missing or rejected by the pipeline — path or program error |
| `resume: replay failed: <cause>` (listener still arms) | WAL unreadable/corrupt op — history prefix only; report before relying on it |
| listener endpoint refuses to bind at re-arm | previous socket lingering / port squatting — the establishment diagnostic names it |
