---
name: bk-onrestart
description: Post-reboot session relauncher. Reopens every open Claude Code repo session as Windows Terminal tabs, resuming each mid-thread via `claude --continue --autocompact 1000000` (never summarising). Auto-fires 45s after logon via the BK-OnRestart scheduled task. Canonical prototype lives in mstack; promote to a deployed buildkit capability.
---

# /bk-onrestart — post-reboot session restart (canonical = mstack prototype)

Reopens every open Claude Code repo session and **resumes each exactly where it left off**
(`claude --continue`, mid-thread, no summary), as one Windows Terminal window with one tab per repo.
Auto-fires 45s after logon so the whole tabset comes back by itself after a reboot.

**Canonical implementation** (single source of truth — do NOT fork a copy):
`D:\bstdev\tools\mstack\scripts\fleet\` — `post-reboot-restart.ps1` (relauncher),
`install-onrestart-task.ps1` (the AtLogOn+45s scheduled-task installer), `bk-onrestart.SKILL.md`.

## Run (by hand)

```
pwsh -File D:\bstdev\tools\mstack\scripts\fleet\post-reboot-restart.ps1 -DryRun -Layout Tabs     # preview, launch nothing
pwsh -File D:\bstdev\tools\mstack\scripts\fleet\post-reboot-restart.ps1 -Layout Tabs -WaitForMounts   # the real launch
```
`-Layout Tabs` is REQUIRED (default is separate windows). `-WaitForMounts` waits for local repo
paths (required) and briefly for I:/H: shares (optional — launches anyway if absent).

## Auto-fire at logon

```
pwsh -File D:\bstdev\tools\mstack\scripts\fleet\install-onrestart-task.ps1              # register BK-OnRestart (AtLogOn, 45s, -Layout Tabs -WaitForMounts)
pwsh -File D:\bstdev\tools\mstack\scripts\fleet\install-onrestart-task.ps1 -WhatIfOnly  # preview
pwsh -File D:\bstdev\tools\mstack\scripts\fleet\install-onrestart-task.ps1 -Remove      # unregister
```

## Verify by PROCESS COUNT, not by the message

A known failure mode is 12 tabs opening and running nothing. After launch:
```
@(Get-Process claude | Where-Object { $_.StartTime -gt (Get-Date).AddMinutes(-2) }).Count   # expect 12
```

## Semantics that must not be "fixed" away

- **Empty/missing session store → REFUSE to launch that repo.** `--continue` in a store-less dir
  silently starts a NEW empty session that looks like a successful resume. The script checks each
  store first and refuses rather than resuming into nothing.
- **`--fork-session` is NEVER passed** — it continues a COPY, not the session.
- **Absent I: (network share) means "I CANNOT SEE THE BOARD", never "the board is empty."** Do not
  let any tool fall back to a stale local sched root (three 2026-08 incidents). Remap when the file
  server host is back: `net use I: \\192.168.0.108\GAVRI_D /persistent:yes`.

## Boundaries

Advisory launcher only. Opens terminal tabs + registers a per-user scheduled task; never mutates a
repo, git, pipeline/DBOS state, or the scheduler board. Each host runs its own /bk-onrestart. This
is a PROTOTYPE of a future fleet-deployed `buildkit-onrestart` capability (roadmap: bk-onrestart-capability).
