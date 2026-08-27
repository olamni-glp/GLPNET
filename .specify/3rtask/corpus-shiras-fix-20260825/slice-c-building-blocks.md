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

---

# ADDENDUM C2 — CONSTRAINTS DISCOVERED SINCE THE FIRST CORPUS BUILD. A FIX MUST SURVIVE ALL OF THESE.

## C2.1 PROVENANCE IS UNRECOVERABLE FROM THE SUBSTRATE — lane testimony is all that remains
```
git -C \192.168.0.108\GAVRI_D\coop\glpnet\sched rev-parse --is-inside-work-tree
  -> fatal: not a git repository (or any of the parent directories)
```
The CRDT substrate is plain JSONL on a share with NO VCS. There is no committer to recover on any
board. With uid dead (forceuid), pid%4 dead, and line endings dead, THE ONLY SURVIVING PROVENANCE
INSTRUMENT IS LANE TESTIMONY. A fix that needs to know who wrote a record CANNOT get it today.

## C2.2 NO ROOT CARRIES AN IDENTITY, AND STAMPING ONE IS FORBIDDEN FOR NOW
```
buildkit-scheduler root  ->  root_id (none - records mint root-scoped ids)
                             "this repo accepts whatever root resolves"
```
That is the mechanism by which three boards came to share one name. BUT shiras published the
binding constraint and it is ENGINEER-LEVEL:
  "You cannot verify convergence safety without the identity, and you cannot safely stamp the
   identity without verifying convergence. DO NOT STAMP IDENTITY UNTIL IT IS BROKEN."
NOBODY may run --ensure-identity yet. A fix proposing it is OUT OF SCOPE until that is ruled.

## C2.3 THE CAPABILITY CONTRACT HAS NO POLARITY AND NO RETRACTION
caps are grow-only, LWW by name; onboard only ADDS. Measured instance: ariellas declares
tool=dart AND skill=dart; dart is NOT INSTALLED; glpnet is a Dart project (pubspec sdk ^3.9.4).
The false declaration CANNOT BE WITHDRAWN - only an explicit negative published beside it
(absent.dart-sdk-NOT-INSTALLED). Every stale declaration on every board is PERMANENT and
unfalsifiable by its own author. crucible holds ruling Q-041-01 for a typed platform capability;
a fix should CONSUME that contract, not build a rival, and it must carry POLARITY.

## C2.4 IDENTITY-FORGING IS A HARD REFUSAL (three lanes independently held this line)
Writing caps/ops onto a grow-only board UNDER ANOTHER HOST IDENTITY is identity forging. It was
refused independently by olamnit-assistant, qhstate and ariellas even where each knew the exact
command that would work. A FIX MUST BE EXECUTABLE BY THE TARGET HOST ITSELF, or by explicit
engineer instruction. A fix requiring one host to write another host identity is INVALID.

## C2.5 VERSION SKEW IS UNEXAMINED AND IS A SERIALISATION RISK
shiras runs 2026.8.24.5; fleet pin 2026.8.23.8; this lane ambient 2026.8.18.2.
MEASURED CONSEQUENCE: on 2026.8.18.2 a UNC --root passed through Git-Bash was rewritten to
D:\192.168.0.108\... and onboard CREATED A STRAY EMPTY BOARD ROOT on the local disk (it warned).
On 2026.08.23.8 the same input REFUSES. So a lane on an older engine can SILENTLY FORK A BOARD.
Separately, the engine changed its on-disk line-ending output in a datable window. NOBODY HAS
CHECKED WHAT A NEWER ENGINE WRITES INTO A SHARED BOARD.

## C2.6 THE PLAN PAYLOAD DIFFERS ACROSS BOARDS/ENGINES
ariellas glpnet (engine 2026.8.18.2): default_calendar_assignees PRESENT = [unassigned]
  -> the key is emitted EVEN WHEN it derives to zero real actors, so ABSENT != EMPTY.
hatzinor: [ariellas,gavriella,unassigned] | lejepa: [ariellas-lejepa,unassigned] (79% of declared
capacity invisible) | buildkit: KEY ABSENT ENTIRELY.
Six boards, six values, one behaviour: THE DERIVATION DROPS QUALIFYING ACTORS. Mechanism is
board-local; the defect class is fleet-wide. A fix must repair the DERIVATION, not a constant.

## C2.7 DELIVERY IS NOT PUBLICATION
A fan-out written as  for d in <leg>/*/  reaches only channels that ALREADY EXIST. It cannot fail
and cannot warn, and reports 0 failures whether the recipient has 25 channels or 3. The shiras leg
had 5 channels against 24/25 on peers. TWO lanes hit this independently. There are FOUR legs, not
three: D:\coop on ARIELLAS itself was missed by this lane all day.
A fix must ENUMERATE intended recipients, mkdir -p the missing, and emit a RECEIPT naming every
path written per leg. A copy count is not delivery evidence; only a read-back from the recipient is.
