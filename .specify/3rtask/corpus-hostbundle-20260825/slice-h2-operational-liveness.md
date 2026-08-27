# SLICE H2 - OPERATIONAL LIVENESS of every actor on the glpnet board

Sources of record on `\192.168.0.108\GAVRI_D\coop\glpnet\sched`:
  * `calendar/<actor>/<actor>-cal-NNNNNN.jsonl`  - self-reported availability windows
  * `ops/<actor>/heartbeat.json`                 - R10 single-writer lease heartbeat
  * `ops/<actor>/` directory presence            - whether the actor has EVER written an op

Reference clock for all staleness arithmetic below: **2026-08-25T06:40:00Z**.

## Liveness summary

| board actor | calendar records | last availability window_end | heartbeat.json | has ops dir |
|---|---|---|---|---|
| `ariellas` | 117 | 2026-09-27T00:00:00Z | present | YES |
| `gavri` | 1 | 2026-08-20T21:55:46Z | **ABSENT** | **NO** |
| `gavriella` | 127 | 2026-09-28T00:00:00Z | present | YES |
| `olamnit` | 256 | 2026-09-27T00:00:00Z | present | YES |

## Actor `ariellas`

117 calendar records, kinds={'available': 117}

Distinct declared dates: 41  span 2026-07-29 -> 2026-09-26

**Availability windows still open at 2026-08-25T06:40:00Z: 101**

Last 6 records verbatim:

```
{"actor": "ariellas", "cal_rec_id": "ariellas:000112", "date": "2026-09-25", "kind": "available", "report_id": "ariellas:calrep:000008", "seq": 112, "window_end": "2026-09-25T16:00:00Z", "window_start": "2026-09-25T08:00:00Z"}
{"actor": "ariellas", "cal_rec_id": "ariellas:000113", "date": "2026-09-25", "kind": "available", "report_id": "ariellas:calrep:000008", "seq": 113, "window_end": "2026-09-26T00:00:00Z", "window_start": "2026-09-25T16:00:00Z"}
{"actor": "ariellas", "cal_rec_id": "ariellas:000114", "date": "2026-08-23", "kind": "available", "report_id": "ariellas:calrep:000114", "seq": 114, "window_end": "2026-08-24T02:37:29Z", "window_start": "2026-08-23T02:37:29Z"}
{"actor": "ariellas", "cal_rec_id": "ariellas:000115", "date": "2026-09-26", "kind": "available", "report_id": "ariellas:calrep:000114", "seq": 115, "window_end": "2026-09-26T08:00:00Z", "window_start": "2026-09-26T00:00:00Z"}
{"actor": "ariellas", "cal_rec_id": "ariellas:000116", "date": "2026-09-26", "kind": "available", "report_id": "ariellas:calrep:000114", "seq": 116, "window_end": "2026-09-26T16:00:00Z", "window_start": "2026-09-26T08:00:00Z"}
{"actor": "ariellas", "cal_rec_id": "ariellas:000117", "date": "2026-09-26", "kind": "available", "report_id": "ariellas:calrep:000114", "seq": 117, "window_end": "2026-09-27T00:00:00Z", "window_start": "2026-09-26T16:00:00Z"}
```

Heartbeat (R10 single-writer lease) verbatim:

```json
{
  "actor": "ariellas",
  "beat": 69,
  "host": "ariellas",
  "pid": 26388,
  "pid_start": 13431926194.827375,
  "ts": "2026-08-23T02:37:29Z"
}
```

## Actor `gavri`

1 calendar records, kinds={'available': 1}

Distinct declared dates: 1  span 2026-08-19 -> 2026-08-19

**Availability windows still open at 2026-08-25T06:40:00Z: 0**

Last 6 records verbatim:

```
{"actor": "gavri", "cal_rec_id": "gavri:000001", "date": "2026-08-19", "kind": "available", "report_id": "gavri:calrep:000001", "seq": 1, "window_end": "2026-08-20T21:55:46Z", "window_start": "2026-08-19T21:55:46Z"}
```

**No `ops/gavri/heartbeat.json` exists.** This actor has never taken a writer lease on this board.

## Actor `gavriella`

127 calendar records, kinds={'available': 127}

Distinct declared dates: 43  span 2026-07-29 -> 2026-09-27

**Availability windows still open at 2026-08-25T06:40:00Z: 105**

Last 6 records verbatim:

```
{"actor": "gavriella", "cal_rec_id": "gavriella:000122", "date": "2026-09-26", "kind": "available", "report_id": "gavriella:calrep:000121", "seq": 122, "window_end": "2026-09-26T16:00:00Z", "window_start": "2026-09-26T08:00:00Z"}
{"actor": "gavriella", "cal_rec_id": "gavriella:000123", "date": "2026-09-26", "kind": "available", "report_id": "gavriella:calrep:000121", "seq": 123, "window_end": "2026-09-27T00:00:00Z", "window_start": "2026-09-26T16:00:00Z"}
{"actor": "gavriella", "cal_rec_id": "gavriella:000124", "date": "2026-08-24", "kind": "available", "report_id": "gavriella:calrep:000124", "seq": 124, "window_end": "2026-08-26T03:48:00Z", "window_start": "2026-08-24T16:48:00Z"}
{"actor": "gavriella", "cal_rec_id": "gavriella:000125", "date": "2026-09-27", "kind": "available", "report_id": "gavriella:calrep:000124", "seq": 125, "window_end": "2026-09-27T08:00:00Z", "window_start": "2026-09-27T00:00:00Z"}
{"actor": "gavriella", "cal_rec_id": "gavriella:000126", "date": "2026-09-27", "kind": "available", "report_id": "gavriella:calrep:000124", "seq": 126, "window_end": "2026-09-27T16:00:00Z", "window_start": "2026-09-27T08:00:00Z"}
{"actor": "gavriella", "cal_rec_id": "gavriella:000127", "date": "2026-09-27", "kind": "available", "report_id": "gavriella:calrep:000124", "seq": 127, "window_end": "2026-09-28T00:00:00Z", "window_start": "2026-09-27T16:00:00Z"}
```

Heartbeat (R10 single-writer lease) verbatim:

```json
{
  "actor": "gavriella",
  "beat": 17,
  "host": "gavriella-driver",
  "pid": 32204,
  "pid_start": 13432063677.993406,
  "ts": "2026-08-24T16:48:00Z"
}
```

## Actor `olamnit`

256 calendar records, kinds={'available': 256}

Distinct declared dates: 44  span 2026-07-29 -> 2026-09-26

**Availability windows still open at 2026-08-25T06:40:00Z: 199**

Last 6 records verbatim:

```
{"actor": "olamnit", "cal_rec_id": "olamnit:000251", "date": "2026-09-25", "kind": "available", "report_id": "olamnit:calrep:000246", "seq": 251, "window_end": "2026-09-25T16:00:00Z", "window_start": "2026-09-25T08:00:00Z"}
{"actor": "olamnit", "cal_rec_id": "olamnit:000252", "date": "2026-09-25", "kind": "available", "report_id": "olamnit:calrep:000246", "seq": 252, "window_end": "2026-09-26T00:00:00Z", "window_start": "2026-09-25T16:00:00Z"}
{"actor": "olamnit", "cal_rec_id": "olamnit:000253", "date": "2026-08-23", "kind": "available", "report_id": "olamnit:calrep:000253", "seq": 253, "window_end": "2026-08-24T23:00:25Z", "window_start": "2026-08-23T12:00:25Z"}
{"actor": "olamnit", "cal_rec_id": "olamnit:000254", "date": "2026-09-26", "kind": "available", "report_id": "olamnit:calrep:000254", "seq": 254, "window_end": "2026-09-26T08:00:00Z", "window_start": "2026-09-26T00:00:00Z"}
{"actor": "olamnit", "cal_rec_id": "olamnit:000255", "date": "2026-09-26", "kind": "available", "report_id": "olamnit:calrep:000254", "seq": 255, "window_end": "2026-09-26T16:00:00Z", "window_start": "2026-09-26T08:00:00Z"}
{"actor": "olamnit", "cal_rec_id": "olamnit:000256", "date": "2026-09-26", "kind": "available", "report_id": "olamnit:calrep:000254", "seq": 256, "window_end": "2026-09-27T00:00:00Z", "window_start": "2026-09-26T16:00:00Z"}
```

Heartbeat (R10 single-writer lease) verbatim:

```json
{
  "actor": "olamnit",
  "beat": 7,
  "host": "olamnit",
  "pid": 11904,
  "pid_start": 13431973549.272287,
  "ts": "2026-08-23T15:45:52Z"
}
```

## Op-log directories that exist under `ops/`

```
ariellas
ariellas.hatzinor
ariellas.yngenios-windows
gavriella
olamnit
```

Note the asymmetry: `ariellas.hatzinor` and `ariellas.yngenios-windows` write ops but hold no caps and
no calendar; `gavri` holds a calendar record but writes no ops and holds no caps.

---

# PART 2 of SLICE H2 - HOST-LOCALITY AND PLATFORM EVIDENCE (fresh, measured 2026-08-25T06:45Z)

The engineer's standing rule is that runnability has two dimensions: host-LOCALITY (work bound to state
that physically lives on one machine) and PLATFORM/TOOLCHAIN fit. This part carries the measured evidence
for both. All of it was taken on host ARIELLAS; facts about other hosts are marked as inferred-from-share.

## Physical fleet topology, measured from ARIELLAS's mounted filesystems

```
$ Get-PSDrive -PSProvider FileSystem   (DisplayRoot shown)

Name Root                        
---- ----                        
C                                
D                                
E                                
F                                
G    \\192.168.0.129\Olamnit_D   
H    \\192.168.0.108\GAVRI_D     
I    \\192.168.0.108\GAVRI_D     
J    \\192.168.0.170\Shiras_Share



```

| drive | UNC | implied host | reachable from ARIELLAS at 06:45Z |
|---|---|---|---|
| C, D, E, F | (local) | **ARIELLAS** (this host) | yes - local |
| G: | \\192.168.0.129\Olamnit_D | **OLAMNIT** (192.168.0.129) | REACHABLE |
| H: | \\192.168.0.108\GAVRI_D | **GAVRI** (192.168.0.108) | REACHABLE |
| I: | \\192.168.0.108\GAVRI_D | **GAVRI** (192.168.0.108) - same share, second letter | REACHABLE |
| J: | \\192.168.0.170\Shiras_Share | **SHIRAS** (192.168.0.170) | REACHABLE |

**H: and I: are the SAME share on the SAME host.** The glpnet scheduler board resolves to
`\\192.168.0.108\GAVRI_D\coop\glpnet\sched` - i.e. the board itself physically lives on GAVRI,
not on ARIELLAS.

**FOUR distinct physical machines are visible: ARIELLAS, OLAMNIT (.129), GAVRI (.108), SHIRAS (.170).**
The glpnet board declares four calendar ACTORS: ariellas, gavri, gavriella, olamnit. Whether those two
sets of four are the same four is NOT established by any record in this slice.

## Platform of host ARIELLAS (measured directly)

```
Caption : Microsoft Windows 11 Pro
Version : 10.0.26100
Hostname: ARIELLAS

$ wsl.exe --list --quiet
Ubuntu
```

ARIELLAS is a **Windows 11 Pro (10.0.26100) host with a working WSL Ubuntu distribution**. Under the
engineer's Dimension-B rule it therefore qualifies as a "WSL-capable Windows host" and may legally
receive Linux-specific work. **No equivalent measurement exists in any record for GAVRI, OLAMNIT or
SHIRAS** - their OS and WSL status are undeclared on the board and unmeasured here.

## Host-locality evidence for ARIELLAS: what state lives HERE and nowhere else

```
$ git worktree list
D:/BSTDEV/research/glp/GLPNET bd08a7fb [091-bkstd1-round42]

$ git branch | wc -l   (local branches on this clone)
55

$ git status --short   (uncommitted WIP)
?? .specify/3rtask/corpus-hostbundle-20260825/
(empty output above = clean tree)
```

### Registered buildkit deploy targets on THIS machine (per-machine registry)

```
$ buildkit-deploy list
D:\BSTDEV\db\ospark  2026.08.23.8  active
D:\BSTDEV\glp\Art-of-GLP-2025  2026.08.23.8  active
D:\BSTDEV\glp\FCP  2026.08.23.8  active
D:\BSTDEV\glp\GLP  2026.08.23.8  active
D:\BSTDEV\glp\GLP-Bonds  2026.08.23.8  active
D:\BSTDEV\glp\GLP-ICLP-2026  2026.08.23.8  active
D:\BSTDEV\glp\GLP-PY  2026.08.23.8  active
D:\BSTDEV\glp\GLPNET  2026.08.23.8  active
D:\BSTDEV\lang\hatzinor  2026.08.23.8  active
D:\BSTDEV\lang\tefl  2026.08.23.8  active
D:\BSTDEV\ppe\sippdem_factory  2026.08.23.8  active
D:\BSTDEV\research\LeJEPA  2026.08.24.5  active
D:\BSTDEV\research\MSTACK  2026.08.23.8  missing
D:\BSTDEV\research\buildkit  2026.08.23.8  active
D:\BSTDEV\research\buildkit-056-arch  2026.08.23.8  missing
D:\BSTDEV\research\buildkit-059-sched  2026.08.23.8  missing
D:\BSTDEV\research\buildkit-owo  2026.08.23.8  missing
D:\BSTDEV\research\buildkit-proof  2026.08.23.8  active
D:\BSTDEV\research\claudesat  2026.08.23.8  active
D:\BSTDEV\research\crucible  2026.08.23.8  active
D:\BSTDEV\research\olamni-buildkit-target  2026.08.23.8  active
D:\BSTDEV\research\olamnit  2026.08.23.8  active
D:\BSTDEV\research\olamnit-wt-ring  2026.08.23.8  missing
D:\BSTDEV\research\qhstate  2026.08.23.8  active
D:\BSTDEV\research\yngenios  2026.08.24.5  active
D:\BSTDEV\tmp\devbase  2026.08.23.8  missing
D:\BSTDEV\tools\MSTACK  2026.08.23.8  active
D:\YNGENIOS\ariellas-001  2026.08.23.8  active
D:\YNGENIOS\yngenios  2026.08.23.8  active
D:\YNGENIOS\yngenios-windows  2026.08.23.8  active
```

This registry is **per-machine and non-transferable**. Note that the working repo
`D:\BSTDEV\research\glp\GLPNET` is **absent** from it while a different, stale clone
`D:\BSTDEV\glp\GLPNET` is the registered GLPNET target. Six targets read `missing`.
Any work packet whose subject is this registry, these clones, these worktrees, or this host's local
branches is PINNED to ARIELLAS by Dimension A and cannot be executed anywhere else.

---

# PART 3 of SLICE H2 - WHAT EACH CANDIDATE HOST PHYSICALLY HOLDS (measured 2026-08-25T06:50Z)

Measured by walking each host's mounted share from ARIELLAS. This is Dimension-A (host-locality)
evidence for the three NON-local hosts. It is share-visible evidence only: it establishes what a host
HOLDS, and cannot establish that host's OS, WSL status or installed toolchain.

## Does the host hold a GLPNET working copy at all?

| host | share | GLPNET clone(s) found | verdict |
|---|---|---|---|
| ARIELLAS | (local D:) | `D:\BSTDEV\research\glp\GLPNET` (this repo, clean, branch 091-bkstd1-round42) plus registered-but-stale `D:\BSTDEV\glp\GLPNET` | holds glpnet |
| OLAMNIT | \\192.168.0.129\Olamnit_D | `G:\BSTDEV\research\glp\GLPNET` | holds glpnet |
| GAVRI | \\192.168.0.108\GAVRI_D | `H:\BSTDEV\research\glp\GLPNET` and `H:\BSTDEV\research\glp\GLPNET-016` | holds glpnet (two clones) |
| SHIRAS | \\192.168.0.170\Shiras_Share | **NONE** | **holds NO glp or glpnet clone** |

Raw evidence:

```
$ ls -d G:/BSTDEV/research/glp/*      # OLAMNIT
G:/BSTDEV/research/glp/GLPNET

$ ls -d H:/BSTDEV/research/glp/*      # GAVRI
H:/BSTDEV/research/glp/Art-of-GLP-2025
H:/BSTDEV/research/glp/GLP
H:/BSTDEV/research/glp/GLP-Bonds
H:/BSTDEV/research/glp/GLP-PY
H:/BSTDEV/research/glp/GLPNET
H:/BSTDEV/research/glp/GLPNET-016
H:/BSTDEV/research/glp/_speckit018_backup_20260517T141849Z

$ ls -d J:/BSTDEV/research/glp        # SHIRAS
ls: cannot access 'J:/BSTDEV/research/glp': No such file or directory
$ ls -d J:/BSTDEV/*/glp*              # SHIRAS, any glp anywhere
ls: cannot access 'J:/BSTDEV/*/glp*': No such file or directory

$ ls J:/BSTDEV/research/              # what SHIRAS does hold
LeJEPA
bk-marathon
buildkit
buildkit.worktrees
claude-bridge
claudesat
crucible
kv-spike-backup
olamni-buildkit-target
olamnit
qhstate
tools
wt-archive
yngenios
yngenios-comms
yngenios.bundle
```

**Consequence under Dimension A.** A glpnet work packet allocated to SHIRAS could not be started there:
the repository the work acts on is not present on that machine. This is independent of SHIRAS's
capabilities, which are in any case undeclared - SHIRAS appears in NO caps stream, NO calendar stream
and NO op log on the glpnet board.

## The four-host question is UNDECIDED by the records

Two different sets of four are visible and they are NOT the same set:

  * four **board actors** with calendar streams: `ariellas`, `gavri`, `gavriella`, `olamnit`
  * four **physical machines** on the network: ARIELLAS, GAVRI (.108), OLAMNIT (.129), SHIRAS (.170)

`gavriella` is a board actor with 82 capability records and an active op log, but no machine of that
name is visible. `gavri` is a board actor with ONE availability record (2026-08-19, long expired), NO
caps stream and NO op log, but IS a visible machine hosting the board itself. SHIRAS is a visible
machine with an active share and a coop tree, but is absent from every glpnet board stream.
No record in this corpus states the host-to-lane mapping. It must be ruled, not guessed.
