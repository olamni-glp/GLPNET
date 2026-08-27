# SLICE H1 - DECLARED CAPABILITIES of every actor on the glpnet board

Source of record: `<sched_root>/caps/<actor>/<actor>-caps-NNNNNN.jsonl` on the shared board
`\192.168.0.108\GAVRI_D\coop\glpnet\sched`. Grow-only CRDT capability streams, LWW by `lww_ts`.

A capability record has: kind (role|skill|tool), name, verified (bool), evidence, lww_ts.
**A capability that is not declared here is NOT declared anywhere the scheduler can see.**

## Coverage: which board actors have a caps stream at all

| board actor | caps stream present | record count |
|---|---|---|
| `ariellas` | YES | 43 |
| `gavri` | **NO - ABSENT** | 0 |
| `gavriella` | YES | 82 |
| `olamnit` | YES | 53 |

## Actor `ariellas`

43 capability records. Latest lww_ts: 2026-08-18T12:57:03Z

### roles (3)

| name | verified | evidence | lww_ts |
|---|---|---|---|
| `builder` | True | (none) | 2026-08-16T13:10:08Z |
| `engineer` | True | (none) | 2026-08-18T12:57:03Z |
| `lead` | True | (none) | 2026-08-13T19:01:45Z |

### tools (9)

| name | verified | evidence | lww_ts |
|---|---|---|---|
| `buildkit` | True | (none) | 2026-08-18T12:57:03Z |
| `buildkit-marathon` | True | (none) | 2026-08-16T13:10:08Z |
| `buildkit-roadmap` | True | (none) | 2026-08-16T13:10:08Z |
| `buildkit-scheduler` | True | (none) | 2026-08-16T13:10:08Z |
| `dart` | True | (none) | 2026-08-18T12:57:03Z |
| `gh` | True | (none) | 2026-08-18T12:57:03Z |
| `git` | True | (none) | 2026-08-18T12:57:03Z |
| `node` | True | (none) | 2026-08-18T12:57:03Z |
| `pytest` | True | (none) | 2026-08-18T12:57:03Z |

### skills (21)

| name | verified | evidence | lww_ts |
|---|---|---|---|
| `bk-close` | True | (none) | 2026-08-18T12:57:03Z |
| `bk-codexreview` | True | (none) | 2026-08-18T12:57:03Z |
| `bk-colab` | True | (none) | 2026-08-16T13:10:08Z |
| `bk-implement` | True | (none) | 2026-08-18T12:57:03Z |
| `bk-marathon` | True | (none) | 2026-08-13T19:01:45Z |
| `bk-plan` | True | (none) | 2026-08-18T12:57:03Z |
| `bk-ship` | True | (none) | 2026-08-18T12:57:03Z |
| `bk-specify` | True | (none) | 2026-08-18T12:57:03Z |
| `bk-tasks` | True | (none) | 2026-08-18T12:57:03Z |
| `codeconv` | True | (none) | 2026-08-18T12:57:03Z |
| `crdt` | True | (none) | 2026-08-18T12:57:03Z |
| `csharp` | True | (none) | 2026-08-16T13:10:08Z |
| `dart` | True | (none) | 2026-08-18T12:57:03Z |
| `distributed-host` | True | (none) | 2026-08-16T13:10:08Z |
| `dotnet-build` | True | (none) | 2026-08-16T13:10:08Z |
| `git-worktree` | True | (none) | 2026-08-16T13:10:08Z |
| `gleam` | True | (none) | 2026-08-18T12:57:03Z |
| `glp` | True | (none) | 2026-08-18T12:57:03Z |
| `glpnet-workstream` | True | (none) | 2026-08-16T13:10:08Z |
| `pipeline` | True | (none) | 2026-08-18T12:57:03Z |
| `python` | True | (none) | 2026-08-18T12:57:03Z |

## Actor `gavri`

**NO CAPABILITY STREAM EXISTS FOR THIS ACTOR.** There is no `caps/gavri/` directory on the board.
Zero declared roles, zero declared skills, zero declared tools, zero verified evidence.

## Actor `gavriella`

82 capability records. Latest lww_ts: 2026-08-16T17:46:34Z

### roles (4)

| name | verified | evidence | lww_ts |
|---|---|---|---|
| `builder` | True | (none) | 2026-07-29T20:23:41Z |
| `glpnet-workstream` | True | (none) | 2026-08-16T17:46:34Z |
| `peer-repo-driver` | True | (none) | 2026-08-16T10:28:56Z |
| `protocol-lead` | True | (none) | 2026-08-16T10:05:31Z |

### tools (6)

| name | verified | evidence | lww_ts |
|---|---|---|---|
| `buildkit-3rtask` | True | (none) | 2026-08-16T17:46:34Z |
| `buildkit-co` | True | (none) | 2026-08-16T10:05:31Z |
| `buildkit-codexreview` | True | (none) | 2026-08-16T10:28:56Z |
| `buildkit-marathon` | True | (none) | 2026-08-16T17:46:34Z |
| `buildkit-roadmap` | True | (none) | 2026-08-16T17:46:34Z |
| `buildkit-scheduler` | True | (none) | 2026-08-16T17:46:34Z |

### skills (15)

| name | verified | evidence | lww_ts |
|---|---|---|---|
| `bk-3rtask` | True | (none) | 2026-08-16T10:05:31Z |
| `bk-analyze` | True | (none) | 2026-08-16T17:46:34Z |
| `bk-clarify` | True | (none) | 2026-08-16T17:46:34Z |
| `bk-close` | True | (none) | 2026-08-16T17:46:34Z |
| `bk-codexreview` | True | (none) | 2026-08-16T17:46:34Z |
| `bk-implement` | True | (none) | 2026-08-16T17:46:34Z |
| `bk-marathon` | True | (none) | 2026-08-16T17:46:34Z |
| `bk-plan` | True | (none) | 2026-08-16T17:46:34Z |
| `bk-ship` | True | (none) | 2026-08-16T17:46:34Z |
| `bk-specify` | True | (none) | 2026-08-16T17:46:34Z |
| `bk-tasks` | True | (none) | 2026-08-16T17:46:34Z |
| `distributed-host` | True | (none) | 2026-08-16T10:28:56Z |
| `glp-repl-suite` | True | (none) | 2026-08-16T10:05:31Z |
| `roadmap-sync` | True | (none) | 2026-08-16T10:28:56Z |
| `verification-receipts` | True | (none) | 2026-08-16T10:28:56Z |

## Actor `olamnit`

53 capability records. Latest lww_ts: 2026-08-20T05:31:39Z

### roles (1)

| name | verified | evidence | lww_ts |
|---|---|---|---|
| `builder` | True | (none) | 2026-08-19T09:34:29Z |

### tools (11)

| name | verified | evidence | lww_ts |
|---|---|---|---|
| `bk-backlog` | True | (none) | 2026-08-16T09:17:03Z |
| `bk-codexreview` | True | (none) | 2026-08-16T09:17:03Z |
| `bk-deploy` | True | (none) | 2026-08-16T09:17:03Z |
| `bk-guardian` | True | (none) | 2026-08-16T09:17:03Z |
| `bk-marathon` | True | (none) | 2026-08-16T09:17:03Z |
| `bk-registry` | True | (none) | 2026-08-16T09:17:03Z |
| `bk-roadmap` | True | (none) | 2026-08-16T09:17:03Z |
| `bk-scheduler` | True | (none) | 2026-08-16T09:17:03Z |
| `bk-ship` | True | (none) | 2026-08-16T09:17:03Z |
| `bk-size` | True | (none) | 2026-08-16T09:17:03Z |
| `buildkit-marathon` | True | (none) | 2026-07-29T19:48:04Z |

### skills (25)

| name | verified | evidence | lww_ts |
|---|---|---|---|
| `bk-3rtask` | True | (none) | 2026-08-19T09:34:29Z |
| `bk-analyze` | True | (none) | 2026-08-16T09:17:03Z |
| `bk-clarify` | True | (none) | 2026-08-16T09:17:03Z |
| `bk-close` | True | (none) | 2026-08-19T09:34:29Z |
| `bk-codexreview` | True | (none) | 2026-08-19T09:34:29Z |
| `bk-implement` | True | (none) | 2026-08-19T09:34:29Z |
| `bk-marathon` | True | (none) | 2026-08-19T09:34:29Z |
| `bk-plan` | True | (none) | 2026-08-19T09:34:29Z |
| `bk-ship` | True | (none) | 2026-08-19T09:34:29Z |
| `bk-specify` | True | (none) | 2026-08-19T09:34:29Z |
| `bk-tasks` | True | (none) | 2026-08-19T09:34:29Z |
| `buildkit-pipeline` | True | (none) | 2026-08-19T09:34:29Z |
| `codeconv` | True | (none) | 2026-08-19T09:34:29Z |
| `csharp-dotnet` | True | (none) | 2026-08-13T16:33:49Z |
| `dart` | True | (none) | 2026-08-19T09:34:29Z |
| `distributed-host` | True | (none) | 2026-07-29T19:48:04Z |
| `git` | True | (none) | 2026-08-20T05:31:39Z |
| `gleam` | True | (none) | 2026-08-19T09:34:29Z |
| `glp` | True | (none) | 2026-08-19T09:34:29Z |
| `glp-repl` | True | (none) | 2026-08-13T16:33:49Z |
| `glpnet-compiler-engine` | True | (none) | 2026-08-13T16:33:49Z |
| `powershell` | True | (none) | 2026-08-20T05:31:39Z |
| `python` | True | (none) | 2026-08-20T05:31:39Z |
| `roadmap-sync` | True | (none) | 2026-08-13T16:33:49Z |
| `windows` | True | (none) | 2026-08-20T05:31:39Z |

## Verified-evidence audit

Count of capability records carrying a non-null `evidence` field, per actor:

```
ariellas     records= 43  with_evidence=  0  verified_true=43
gavri        records=  0  with_evidence=  0  verified_true=0
gavriella    records= 82  with_evidence=  0  verified_true=82
olamnit      records= 53  with_evidence=  0  verified_true=53
```

NOTE for the reader: `verified: true` on these records is a SELF-REPORT written by the actor's own
`buildkit-scheduler onboard` call. It is not an independent attestation and no record carries evidence.
