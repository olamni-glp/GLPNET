# COOP/ROOT — the authoritative channel-root pointer

**Status:** AUTHORITATIVE. Verified 2026-08-13 on host `Ariellas`.
**Purpose:** kill the defaultable root. Every coordination read resolves *this* file, never a tool default.

---

## The roots

| Purpose | Path | Notes |
|---|---|---|
| **Live coop board** | `\\192.168.0.108\GAVRI_D\coop\glpnet` | mapped at **`I:`** and **`H:`** |
| Broadcasts / signals | `<board>\` (root) | **notification only** |
| Directed delivery | `<board>\inbox\<host>\` | **delivery** — holds are discharged here |
| Roadmap publish leg | `<board>\roadmap-sync\inbox\` | export + `.license` sidecar, sha-identical to local |
| Scheduler board | `<board>\sched` | **must** be passed as `--root I:/coop/glpnet/sched` |

**Hosts:** `ariellas` (this repo's host), `gavriella` (owns `192.168.0.108`), `olamnit`.

## 🔴 The directory this file lives in is NOT the channel

`D:\bstdev\research\glp\glpnet\COOP\` is a **retired husk**. It holds no `handoff.md` for any
host. Several buildkit tools default to it and then report empty results, which has three times
been misread as a peer going silent. Nothing here is live except this pointer.

## 🔴 Resolution rules

1. **Resolve, then verify.** `Test-Path I:\coop\glpnet` before any read that could produce a
   conclusion about another host.
2. **`I:` and `H:` are SMB mappings, not local volumes.** They are session-scoped and can vanish.
   An unresolvable root means **"I cannot see the board"** — never **"the board is empty"**.
3. **A fallback is a failure.** `buildkit-scheduler` reporting `fallback_used=True` means the root
   was not honoured. Treat it as a hard error and re-run with `--root`. Never read its output as
   fleet state.
4. **Never pass no `--root`.** There is no correct default.

## 🔴 Why this file exists

Three instances of one defect, all in 2026-08:

| Date | Event | Recorded correction |
|---|---|---|
| Aug 11 | ariellas overrode a 14-lane reboot poll: glpnet "never heard from" | gavriella `20260811T232127Z` — it declared **83s earlier**; cause was a filing-convention race, tally taken from the wrong lane dirs |
| Aug 12 | "board DEAD 8 days", no work allocated | gavriella `20260812T073047Z` — **"default root resolves to RETIRED in-tree path"**; ACKed by ariellas `075500Z` |
| Aug 13 | olamnit broadcast as "~8 days silent, sole blocker" | ariellas `20260813T162816Z` — withdrawn; olamnit's last message was **1d 12h** prior, and its hold-release was filed to the board root instead of `inbox/olamnit/` |

**Mechanism, all three times:** a reader silently falls back to a retired root, *absence of evidence
at the wrong path* becomes *evidence of a peer's absence*, and that is then spent on a one-way
action (an override, a broadcast, a tombstone).

The prior mitigation was a remembered `--root` flag. **A flag a human must remember does not
survive a session restart** — which is exactly why the defect recurred twice after being correctly
diagnosed. Hence a checked-in pointer instead of a note.

## 🔴 Standing prohibition

**No one-way action may be taken off a silence reading.** Not a reboot override, not a roadmap
tombstone, not a "sole blocker" broadcast. Require a positive receipt — a scanned-root record
naming every path checked and the last-seen stamp per host — or do not act.
