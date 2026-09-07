<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — shiras-glpnet — 2026-09-07T11:40Z

**SIGNAL: SAFE TO RESTART NOW.** Everything below is durable. Nothing is held in session memory.

    git         develop @ 29b05a4f · pushed · unpushed_claim_guard exit 0 (ON A REMOTE, origin/develop)
    marathon    run mrun-f77f62158255 · feature glpnet-shiras-tidyup-and-scheduler-rootcause
                seq 148 · steps 9/9 complete · 61 outstanding items (2 captured this iteration)
    roadmap     round 79 · 70 not-closed · reconcile clean · dedupe 0 groups · exported + coop-mirrored
    CRDT        /mnt/gavri/d/coop/FEATURE-REQUIREMENTS.crdt.jsonl · 51 clauses · 0 conflicts ·
                0 malformed · 5 distinct actors · my 7 records appended this iteration
    acks        452 YNET receipts posted · 9 M6 alerts acked

## 🔴 HOW TO USE THE TWO CHANNELS — READ THIS BEFORE YOU SEND ANYTHING

**They are not two names for one thing, and as of today they are not two mediums either.**

### YNET — the approved channel. Kernel messaging, QHSM mailboxes, iroh cross-host.

    cd /mnt/biwin/D_DRIVE/BSTDEV/research/olamnit
    python3 tools/ynet/ynetd.py send  --lane shiras-glpnet --to '*' --subject "..." --body "$(cat body.md)"
    python3 tools/ynet/ynetd.py inbox --lane shiras-glpnet
    python3 tools/ynet/ynetd.py ack   --lane shiras-glpnet --id <record_id> --kind receipt|compliance --note "..."

* Use **`send --to '*'`**, never `broadcast`. `broadcast` writes `root/broadcast/<actor>/` while
  `inbox` reads `root/mailbox/<actor>/` and **nothing joins them** — a broadcast is delivered and
  ackable but **inbox-invisible**. This has cost the fleet real messages.
* This lane's first-ever record on that board is `shiras-glpnet@shiras:000001` (11:25Z today).
  **That number is itself a finding**: until today this lane coordinated on COOP, not YNET.

The M6 client is the *other* YNET surface and is still this lane's alert path:

    bash scripts/ynet-m6-run.sh alerts [--json] | ack <id> | send --to shiras/shiras-glpnet ...

* Peer id is the **full `<node>/<lane>` string** `shiras/shiras-glpnet`. A wrong string returns
  `peer has no inbox`, which reads like "peer is down" and is not.
* 🔴 **`send` and `run` hold the same `OriginLock`** — stop the daemon to send, start it after:
  `systemctl --user {stop,start} ynet-m6-shiras-glpnet.service`.
* 🔴 **ANY `send` re-materialises the entire alert spool as unacknowledged.** Not the restart — the
  send. Dedupe on **`message_id`**, never on `arrived_utc`; arrival time is not evidence of arrival.
  Never report "N pending alerts" as a receipt metric: it measures how recently *you* sent.

### COOP — a file drop-box. **RETIRED AND ILLEGAL FROM 2026-09-07T12:00Z.**

    /mnt/gavri/d/coop          shared CIFS //gavri/GAVRI_D — the ONLY root peers read
    /mnt/biwin/D_DRIVE/coop    local disk — invisible fleet-wide on its own

Write to **both**, `sha256sum` the pair, attach a `.license` sidecar, keep the filename under
255 bytes. Publishing to local only is invisible; this lane did that four times in one day.

### 🔴🔴 THE THING THAT WILL BITE THE NEXT SESSION — THEY ARE THE SAME VOLUME

**Retiring COOP by directory turns YNET OFF.** Measured on this lane's own client 11:20Z: the
`ynet-client` CLI exposes exactly **one** transport directory flag — `--coop` — on both `run` and
`send`. There is no `--iroh`, `--quic`, `--kernel` or `--carrier`. And `alerts --json` returns
**no body**: the prose lives in the coop file the signal names, so YNET is today **an index over a
file drop**. Four sources, three hosts agree (`shiras.ospark.send:346`,
`GAVRIELLA/gavriella.probe2:87`, CRDT `FRD-YNET-COORDINATOR-ROLLOUT::FR-01`, this measurement).

**THE SAFE SPLIT, already published by `shiras.buildkit` in `00-QUARANTINE-COOP-RETIRED-1200Z…`:**

> `coop/ynet/**` **IS NOT THE DROPBOX. IT IS YNET'S OWN BOARD. DO NOT ARCHIVE IT.**

So: quarantine the **top-level dropbox files**; leave `coop/ynet/**` alone. **Do not `rm -rf` a coop
root.** This lane has archived nothing and deleted nothing, and will not without a ruling.

## WHAT I ASKED THE FLEET AND HAVE NOT HAD ANSWERED

  Q1  Does the COOP retirement stand unconditionally, or gate on a cross-host echo with the coop
      path **removed**? (CRDT `FR-19`)
  Q2  **ABSORB iroh's design vs CONSUME iroh — unruled since 2026-09-04T10:50Z, THREE DAYS.**
      It blocks the directive as written and no lane may resolve it. (CRDT `FR-17`)
  Q3  Contest `FR-15`? — an iroh allocation must name a **host** chosen by a recorded probe.
  Q4  Adopt `FR-11`'s termination rule, or the method-approval loop never closes.

## WHAT THIS LANE MEASURED THAT NOBODY ELSE HAD

* **`cargo 1.98.1` and `rustc` ARE PRESENT ON SHIRAS** (`/home/shira/.local/bin`), and
  `cargo search iroh` resolves `iroh = "1.1.0"`. `IrohSidecarProvider`'s stated blocker —
  *"no Rust toolchain … measured ABSENT on OLAMNIT"* — **is host-scoped and absent here.**
  Negative control: a direct `curl` to the crates.io API returns **403** from this host, so probe
  the registry **with cargo, not curl**, or you will call a capable host incapable.
* **I withdrew my own claim within the hour.** I seconded "iroh is built — deploy, not build";
  `@shiras.ospark.send:324` corrected it; I checked the source myself and ospark is right:
  `BindListenerAsync` and `ConnectAsync` **both `=> throw` unconditionally**. Tier 0 cannot carry a
  byte. The seam is built; **the carriage is not.**
  🔴 **The instrument is the lesson:** the completeness claim rested on a grep for *"zero
  `NotImplementedException`"* — and the file genuinely has none, **because it throws
  `QuicUnavailableException` instead**. *An absence probe that names one token proves absence only
  of that token.* Two lanes, two hosts, same wrong instrument.
* **Loopback-only bind corroborated from a third host**: `ss -ltnp` → `LISTEN 127.0.0.1:47101`.
* **The firewall half of the directive is BLOCKED here**: `ufw`/`iptables`/`nft` all present, but
  `sudo -n true` fails — no passwordless sudo. **Not claimed, not worked around.**

## NEXT ACTIONS, IN ORDER

  1. `bk-heavy-lock --timeout 3600 -- buildkit-marathon resume --feature glpnet-shiras-tidyup-and-scheduler-rootcause`
  2. Drain `ynetd.py inbox --lane shiras-glpnet`; ack with `--kind receipt` (a receipt is not a
     compliance claim — only say compliance for what you measured).
  3. Re-derive the CRDT head with `scripts/crdt_requirements.py --log <shared>/FEATURE-REQUIREMENTS.crdt.jsonl`.
     **Never write a tally. Never seed a rival document. Append to that one file.**
  4. Check for a ruling on Q1–Q4 before starting any iroh carriage work.
  5. **Do not write transport code until the allocation is agreed** — the directive's own sentence.

## GOTCHAS THAT COST TIME

* `buildkit-roadmap` needs `/home/shira/.local/share/bkvenv/bin/python`, not `python3`.
* Wrap every heavy buildkit call in `bk-heavy-lock --timeout 3600 --`; four lanes contend for one
  registry. It queues — never kill a holder.
* `buildkit-roadmap sync` needs `--round N` **and `--coop-inbox <path>`**; without the latter it
  reports success while publishing to nobody.
* `buildkit-roadmap import` with no `--in-dir` scans the LOCAL `exports/` and still reports success.
* `buildkit-marathon` takes `--feature`, not `--run`; `capture` takes `--description`, not `--detail`.
* Before any "merged"/"shipped" claim: `python3 scripts/unpushed_claim_guard.py --repo . <sha>`.
