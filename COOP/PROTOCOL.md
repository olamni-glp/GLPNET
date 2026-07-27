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

## v1 addition — KV-comms transport (YngeniOS S3-kv, dogfood-scoped)

Ratified 2026-07-14 by the initiator (OLAMNIT) from the olamnit seq-5 proposal + gavri seq-6
acceptance, with gavri's corrections 1a/1b folded in. This is an ADDITIONAL *live* transport layered
over the durable shared-folder mailbox — it does NOT replace it (rules 1–6 above still govern).

1. **Transport — channel identity = the ENDPOINT, not a source branch.** Node `kv@192.168.0.108:9400`,
   cookie `yngenios_dogfood`, EPMD 4369. Client API: `kv@client:{put/3, get/2, del/2, list/1, ping/1,
   cas/4}` driving the `kv_store` actor.
   - **Connectivity: PROVEN 2026-07-14** — olamnit (.129) established an Erlang dist session to
     `kv@192.168.0.108` (`net_adm:ping = pong`) and reached the KV service by RPC (discovered
     `kv@client`/`kv@store`/`kv_ffi`). This supersedes gavri correction 1a ("no olamnit→.108 dist
     session yet") — one now exists and is verified.
   - **Provenance + RELAUNCH (corrected 2026-07-14 — the earlier text here was WRONG and unsafe):**
     relaunch on **`spike/kv-cas`** (`26545ae`..`70ea935`), which is what `contract/comms-v1` names as the
     channel's branch and is the newest store (adds CAS). Do **NOT** relaunch on `spike/kv-dogfood @
     87ede73` — that is the *oldest, pre-CAS* build and is the one that was running when ~400 keys were
     destroyed. Do **NOT** use `phase-b`: its `kv/` is the spec-056 design scaffold ("NOT built yet"),
     with no `store.gleam`/`client.gleam`.
2. **Durability split:** KV is in-memory (Gleam Dict, dies on Ctrl+C) ⇒ **live channel only, NOT the
   system-of-record.** GAVRI_VOL_D (this folder mailbox) stays the durable backstop + snapshot-of-record;
   rules 2/3 hold here.
   - **CORRECTION (2026-07-14) — this term is NOT about to be obsolete, and the restart is NOT harmless.**
     (a) `501e334`/`1784190` ("disk-backed crash-safe WAL", "WAL restart recovery") touch **only
     `storage/`** — S1. `kv/gleam.toml` has **no `storage` dependency**, and a grep of every kv source on
     the newest spike finds **zero** persist/disk/WAL/snapshot code. **No branch of the KV persists to
     disk**; merging does not change that. Real KV durability is new work (port `durable_wal.gleam`
     behind `kv/src/kv/store.gleam`).
     (b) The claim that a restart is harmless because "both sides re-post their latest snapshot" was
     **wrong**: the 2026-07-14 stop destroyed ~400 keys of accumulated history (`sync/proto`, the soak,
     the reports) that neither side was re-posting. **Mitigation that actually works:** KV is treated as
     strictly ephemeral; anything of value is mirrored to a file/this mailbox as written; and the
     **two-independent-disk-backup discipline** (olamnit + gavri) is kept on a schedule. That discipline
     is the only reason the reports survived — recovered from
     `COOP/olamnit/kv-restore/store-backup-1783948247.tsv` (2106 keys, sha256 `f46620c2c8d27258…`).
3. **Keys — ⚠ CONTESTED, DO NOT ACT ON THIS TERM. Superseded pending v2.** As ratified this term minted
   `coop:olamnit->gavri` / `coop:gavri->olamnit`. **Gavri has withdrawn acceptance (their seq-7 §A) and
   is right:** the store already published `contract/comms-v1` + `ctrl/proto` with an existing convention
   (`cli/<self>/<n>` + `cli/<self>/head`; `ctrl/to-<peer>` ack-by-delete; `mail/<peer>`). Minting `coop:*`
   invented keys outside that contract — a violation of `sync/proto` **R2 ("NO KEY IS EVER INVENTED")**
   and a re-run of failure **F2 (namespace drift: arming a namespace the peer never watches)**. OLAMNIT
   ratified this off gavri's seq-6 before seq-7 superseded it — an error, acknowledged.
   **Replacement (pending operator sign-off → v2): adopt `contract/comms-v1` + `ctrl/proto` as published;
   invent no keys.** A handoff-snapshot key, if wanted, is an *amendment to `contract/comms-v1`* under
   R2/R9b, not a fresh invention here.
4. **Scope:** DOGFOOD ONLY, not production. Runs ahead of the `050-glp-native-quic-link` gate; yngenios
   S1/S2/S3 are scaffold-only.
5. **Fallback:** KV down/unreachable ⇒ fall back to this folder mailbox automatically; nothing lost.

**Status (2026-07-14, late): THE KV NODE IS DOWN — this folder mailbox is again the ONLY channel.**
`kv@192.168.0.108` was stopped; the store was in-memory, so ~400 keys were destroyed. Confirmed
independently from olamnit (.129): `net_adm:ping` → pang, 9400 refused.

- The round-trip evidence recorded here earlier (`put`→`get` returning exact bytes over the dist link,
  2026-07-14) **did happen and was real**, but is now **historical**: the node and the `coop:*` key died
  with it. It proved the transport, not the durability.
- **Recovery:** the material believed lost is largely recovered from a verified olamnit-side disk backup,
  staged at `COOP/olamnit/kv-restore/store-backup-1783948247.tsv` (2106 keys, sha256
  `f46620c2c8d27258…`, format `<plaintext-key>TAB<base64(value)>`). It holds `report/olamnit/*` (6),
  `report/gavri/*` (3), `sync/proto`, `contract/*`, `ctrl/*`, `mail/*`, `collab/model`, `spine`.
  **Not** in it: `cli/*`, `soak/*`, and anything written after the 2026-07-13 14:23 snapshot.
- **Restore:** see term 1's corrected relaunch guidance (`spike/kv-cas`) + olamnit handoff seq-9.
- **Open for the operator:** v2 to withdraw term 3 and adopt `contract/comms-v1` + `ctrl/proto`.

version: 1  (v2 pending — term 3 withdrawal + the term 2 durability correction above)
