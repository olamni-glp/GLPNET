# SLICE C - AVAILABLE BUILDING BLOCKS: the live CLI contracts a fix may compose

Only verbs that EXIST may appear in a fix. Each block below is the tool's own --help, captured live.

## buildkit-scheduler (board substrate)
```
usage: buildkit-scheduler [-h]
                          {cycle,loop,transition,takt-tokens,takt-sync,bulk-ready,reject,allocate,ingest,effort-assign,stock-edges,onboard,confirm,note,plan,board,report,watch,status,doctor,replicas,replicate,root,version} ...

CRDT-native CPM/PERT work scheduler (advisory; part of buildkit).

positional arguments:
  {cycle,loop,transition,takt-tokens,takt-sync,bulk-ready,reject,allocate,ingest,effort-assign,stock-edges,onboard,confirm,note,plan,board,report,watch,status,doctor,replicas,replicate,root,version}
    cycle               run one scheduling cycle
    loop                run hourly cycles for a bounded horizon (daemon)
    transition          write one board transition (e.g. make a claimed WP
                        ready)
    takt-tokens         record PER-PHASE TOKEN USE in the takt DuckLake (fleet
                        standard: tact data and per-phase token use are stored
                        in AND read from the lake)
    takt-sync           replay this host's takt-lake records to the shared
                        fleet root (batch; deliberately OFF the hot path)
    bulk-ready          move EVERY packet in one column to another in ONE
                        command (SC-008); operator-invoked only, and refuses
                        unless the board has DECLARED ingest_ready_default
    reject              free an assigned WP back to the ready pool (assignee-
                        only; S2b)
    allocate            propose an assignee for a WP (writes an addressed
                        allocate op)
    ingest              mint work packets from a roadmap export (the supply
                        verb; idempotent)
    effort-assign       declare a story size for one roadmap slot whose effort
                        text names none (the way past `needs_effort`)
    stock-edges         project roadmap dependencies onto the board as
                        versioned edges (complements ingest; idempotent per
                        edge)
    onboard             self-report caps / availability / claims
    confirm             confirm recommended WPs into a new board state
    note                write a progress / policy note op (the H3 signal)
    plan                derive + print the CPM/PERT plan view

$ buildkit-scheduler onboard --help
usage: buildkit-scheduler onboard [-h] [--root ROOT] [--feature FEATURE]
                                  [--home HOME] [--json]
                                  [--engine-override ENGINE_OVERRIDE]
                                  [--actor ACTOR] [--host HOST] [--role ROLE]
                                  [--tool TOOL] [--skill SKILL] [--cap CAP]
                                  [--avail-hours AVAIL_HOURS]
                                  [--shifts [SHIFT_DAYS]]
                                  [--window ISO_START/ISO_END] [--claim CLAIM]

options:
  -h, --help            show this help message and exit
  --root ROOT           board root (default: R1 sched_root / coop/sched)
  --feature FEATURE
  --home HOME

$ buildkit-scheduler note --help
usage: buildkit-scheduler note [-h] [--root ROOT] [--feature FEATURE]
                               [--home HOME] [--json]
                               [--engine-override ENGINE_OVERRIDE]
                               [--actor ACTOR] [--host HOST] --wp WP [--re RE]
                               [--body BODY] [--field KEY=VALUE]

options:
  -h, --help            show this help message and exit
```

## bk-flow (board to pipeline bridge)
```
usage: bk-flow [-h] [--version]
               {poll,claim,open,report,takt,lanes,version} ...

Board->pipeline bridge (advisory; part of buildkit). Never invokes a pipeline
command — it tells you which one to run.

positional arguments:
  {poll,claim,open,report,takt,lanes,version}
    poll                per-WP dispatchability with a reason (read-only)
    claim               append one add-wins claim to your own log
    open                bind a claimed WP to a feature + marathon run
    report              append one transition to_state=done
    takt                per-phase takt for this feature's marathon run against
                        its target bands (read-only)
    lanes               show the declared lane registry and how every board
                        actor relates to you (read-only)
    version             report the bk-flow capability version

options:
  -h, --help            show this help message and exit
  --version             show program's version number and exit
```

---

## THE DEFECT CLASS THIS FIX MUST ELIMINATE (measured today, three instances)

1. **Publishing sweep scoped to existing dirs.** `for d in <leg>/*/` reaches only channels already present.
   The SHIRAS leg had 5 channels vs 24/25 on peers, so sweeps reported `0 failures` while reaching almost nobody.
   Two lanes hit this independently.

2. **SMB share is a PARTIAL PROJECTION of a host.** `\\192.168.0.170\Shiras_Share` does NOT export
   `/mnt/biwin/D_DRIVE/BSTDEV` where the work lives. Measuring the share and concluding about the host produced
   five false findings (clone absent, identity absent, caps absent, oplog absent, buildkit absent) - all refuted by SSH.

3. **Capability stream has no RETRACTION.** Grow-only LWW by name; `onboard` only ADDS. A stale or false
   declaration (measured instance: `dart` declared on ariellas, dart NOT INSTALLED, and glpnet is a Dart project)
   is permanent and unfalsifiable by its own author. Only an explicit negative token can be published beside it.

4. **UNC through Git Bash is rewritten.** `\\192.168.0.108\GAVRI_D\...` became `D:\192.168.0.108\...`
   and the tool CREATED A STRAY EMPTY BOARD ROOT on the local disk. It warned; the warning is the error.

5. **A verified SSH mesh existed and went unused.** 60/60 routes verified 2026-08-17, full trust, ProxyJump
   aliases, documented on shiras itself. The fleet reasoned from file shares for hours instead.

## CONSTRAINTS A FIX MUST RESPECT

- Board streams are **grow-only, append-only, single-writer per actor**. Nothing may rewrite another actor's stream.
- The canonical fleet address is a **UNC**, never a drive letter (RULING F).
- An **era is a feature**, nine stages, never split.
- A fix may only compose verbs that EXIST in the contracts above; inventing a verb is out of scope and must be
  reported as a required upstream change instead.
