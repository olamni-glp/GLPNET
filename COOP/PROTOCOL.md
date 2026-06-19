# bk-colab v0 protocol — shared-directory mailbox (THROWAWAY)

This is the first, deliberately disposable design of `/bk-colab`: two Claude Code
instances on two hosts collaborating on the shared GLPNET codebase through a
**shared directory**, not a network protocol. We model it on the olamnit
cross-host pattern (a committed handoff/resume artifact + a convergent, resumable
pull-sync) but replace the transport with this folder. We will refine toward a
truly distributed design step by step.

## Hosts & paths

| Host | This volume is | Mailbox root |
|---|---|---|
| OLAMNIT (initiator) | `G:` | `G:\BSTDEV\research\glp\glpnet\COOP\` |
| GAVRI (peer) | `D:` | `D:\BSTDEV\research\glp\glpnet\COOP\` |

`GAVRI_VOL_D` is one shared volume: `G:` on OLAMNIT == `D:` on GAVRI, same files.

## Layout

```
COOP/
  PROTOCOL.md      <- this file, co-edited (see "Editing PROTOCOL.md")
  olamnit/         <- OLAMNIT outbound  (OLAMNIT writes, GAVRI reads)
    handoff.md
  gavri/           <- GAVRI outbound    (GAVRI writes, OLAMNIT reads)
    handoff.md
```

## Rules (v0)

1. **You write only your own `<host>/` subdir.** You never edit the peer's
   subdir. Everything for the peer goes through your `handoff.md`.
2. **The channel is ASYNCHRONOUS** — the shared volume is not always mounted on
   both hosts at once. Treat every read as a resume: re-reading must converge,
   never duplicate, never lose. Put a `last_updated` line + a monotonically
   increasing `seq:` at the top of your `handoff.md` so the reader can tell new
   from already-seen.
3. **`handoff.md` is a full snapshot, not a diff** (idempotent, like olamnit's
   epic-013-handoff.md). Overwrite it wholesale each turn; the peer always reads
   the latest complete state.
4. **Editing PROTOCOL.md:** propose changes in your `handoff.md` under a
   "PROTOCOL proposals" heading first; only the initiator (OLAMNIT) commits an
   agreed change into PROTOCOL.md, to avoid both sides clobbering it. Bump the
   `version:` line below when it changes.
5. **No git for the channel.** `COOP/` is gitignored on both repos; the shared
   volume is the transport, not git. (Code collaboration on the actual repo is a
   later step — v0 is just the handshake + mailbox.)
6. **Hard constraints inherited:** never modify the `olamnit`/`olamni-assistant`
   repos on any drive (read/observe OK); commit only files you changed; never
   `git add -A`; never force-push; this is throwaway — minimal, reversible.

version: 0
