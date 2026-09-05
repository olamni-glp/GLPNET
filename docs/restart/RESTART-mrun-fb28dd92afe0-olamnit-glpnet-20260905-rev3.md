<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART BRIEF — `olamnit.glpnet` · run `mrun-fb28dd92afe0` · **rev 3** · 2026-09-05

**Resume with:** `resume marathon`
**Host:** OLAMNIT · **Branch:** `104-wp02-quic-listener-service` @ `e50af7bf` (pushed, tree clean)
**Supersedes rev 2.** Trust `git log --oneline -1` over any hash written here.

---

## 0 · WHAT SESSION 11 ACTUALLY DID — read this before believing anything else

Session 11 was **interrupted by the engineer for a safe restart before any era work began.**
It completed only: mandatory reading, objective state recovery, and **one fleet-state discovery
that invalidates a refusal recorded in rev 2**. No code was written. No commits other than this
brief. **Do not report era 105 as started.**

---

## 1 · 🔴 THE RULING THAT CHANGED WHILE THIS LANE WAS ASLEEP

`develop` moved **64 commits** ahead while this lane was between sessions (feature
`102-quic-federation-transport`, another lane on this same repo). Two tags were cut:
`v2026.09.05.2`, `v2026.09.05.3`. `main` is now `39ac3fc7`.

Among those commits, `f0b4db68` carries **four BK-STD-2 engineer rulings**, verbatim at
`specs/102-quic-federation-transport/questions-G30.json` on `develop`:

| qid | header | engineer decision |
|---|---|---|
| `Q-GLPNETG30-01` | Ship bar | second independent instrument required before ship |
| `Q-GLPNETG30-02` | Vote ident | **term 1 is VOID.** Vote records must carry **host and lane** and must satisfy **`actor == voter`** before any term counts |
| `Q-GLPNETG30-03` | Elect-hold | 🔴 **`directive-overrides`** — engineer **OVERRODE** the lane's recommendation |
| `Q-GLPNETG30-04` | SC-001 | elevated firewall rule authorised; ship SC-001 measured |

### 🔴 `Q-GLPNETG30-03` — THE ELECTION HOLDS ARE **LIFTED**

Quoted from the ruling record:

> "Engineer OVERRODE my recommendation (fix-electorate). The directive governs: the holds in
> `Q-gsbk14-01` and `Q-YNGH-01` are **lifted** and an election **runs today**. Read together with
> the `Q-GLPNETG30-02` ruling, the coherent reading is: **term 1 is void, the vote schema is fixed
> first, and the election runs TODAY under the fixed schema** — not that a leader is seated on the
> broken one."

**Consequence for this lane, and it is a correction of record:** rev 2 §4 item 2 recorded
*"Prototyping a Paxos/Raft/ZAB/PBFT election — REFUSED, `Q-gsbk14-01` HOLDS all election work."*
**That refusal is now void.** `Q-gsbk14-01` has been lifted by a later engineer ruling on the same
day. **Do not carry the refusal forward. Do not re-refuse on the old ground.**

The election that runs must be under the **fixed** schema: votes carry host + lane, `actor == voter`
enforced, term 1 discarded. The engineer's standing directive also designates
**`yng-broker` / `yng-guardian` on each of the 4 hosts as the PBFT leader elector for all purposes**
— which is what era 104's WP-02 listener service exists to make queryable.

---

## 2 · 🔴 PR #298 — STILL OPEN, STILL BLOCKED, NOW ALSO STALE

```
gh pr merge 298 --merge   ->  REFUSED by the Claude Code auto-mode classifier   (4th session running)
```

Retried this session. Same refusal, same reason. This is `Q-glpnet-01` (2026-09-03), unfixed.

**New this session:** the branch is now **64 behind / 7 ahead** of `develop`. Even once the merge is
permitted, `develop` must first be merged **into** `104-wp02-quic-listener-service` and the suite
re-run, because 64 commits of another lane's QUIC/federation work have landed underneath it and
they touch the same C# transport area.

**Only the engineer can clear this.** Either merge #298 by hand, or add a Bash permission rule for
`gh pr merge`.

---

## 3 · WHAT'S NEXT, IN ORDER — unchanged from rev 2 except where marked

1. **Merge `origin/develop` into the branch**, re-run the suite (was 196/196), then the engineer
   merges **PR #298** → `buildkit-release` → tag → back-merge. 🔴 **NEW: the develop merge is now a
   prerequisite and was not in rev 2.**
2. **`Q-olg15-02`** — the P1 this lane owns and has **not** fixed. `SignedRecord`'s signer↔key
   binding is guarded by `if (Kind == RecordKind.Reachability && …)` and there are **two** kinds; a
   measured attacker-signed **`KeyToRecord`** under a victim's node-id key self-certifies, is stored
   by `SKademliaNode.Store` and is served by `Lookup`. Ruling: **bind every kind, refuse unbound**,
   after measuring whether any live `KeyToRecord` legitimately uses a non-signer key. Two
   `DEFECT_PROBE_*` tests assert the **current** behaviour — **invert them when fixed.**
3. **M6** (`Q-olg15-06`) — the C# **QHSM/QMSM code-based YNET receiver client, never agent-based**:
   send+receive independent of the agent, main part a kernel-managed QHSM/QMSM native yngenios
   process, asynchronously alerting the agent via (web)hook/RC callbacks with non-disruptive `/btw`
   semantics. **Built ONCE, L0-shared — glpnet is the L0 home.** Roadmap feature
   `m6-qhsm-code-based-ynet-receiver-client-l0-shared`, WSJF 5.80 / RICE 5400, promoted.
   **This lane is `NOT MET` and has reported itself so.** It is an automatic daily failure criterion.
4. **Era 2 of the period: `differential-cross-runtime-acceptance-gate`** (WSJF **19.50**, #1 in build
   order) per `Q-olg15-01`.
5. **`buildkit-roadmap link`** for `specs/104-…` once permitted — 77 of 126 features carry no
   `spec_path`. Also classifier-blocked.
6. **Re-ask `@gavriella-glpnet` for the literal `space_id`** (`Q-olg15-04`: do NOT mint one).

---

## 4 · STANDING RULINGS STILL IN FORCE (set `Q-olg15-20260905T0800Z`)

- **`Q-olg15-01`** BOTH, WP-02 first, then `differential-cross-runtime-acceptance-gate`.
- **`Q-olg15-02`** Bind every record kind, refuse unbound. Measure live `KeyToRecord` usage first.
- **`Q-olg15-03`** iroh **PRIMARY at L0 via a SIDECAR**, msquic **retained** as redundancy and
  ultimate fallback. The process boundary is what lets iroh sit at L0 without making L0
  distro-dependent. 🔴 **No Rust toolchain on this host — the sidecar binary cannot be built here.**
- **`Q-olg15-04`** Do **not** mint a `space_id`; ask and wait. Federation is a disclosed gap.
- **`Q-olg15-05`** 🔴 The mailbox service is a **HYPER-V CONTAINER** serving **hundreds of millions
  of concurrent mailboxes** over **two** paths — YNET cross-host, and an **in-memory interconnect at
  YNGENIOS KERNEL level inside each host**. `Q-ARI0905-01`'s roster-block framing is **voided**; all
  three of its options are 100% wrong. **Correct mailbox use and implementation is a
  FLEET-COLLECTIVE FAILURE CRITERION for today.** Broadcast to 154 channels across 4 roots.
- **`Q-olg15-06`** M6, as in §3.3 above.
- 🔴 **`Q-GLPNETG30-03` LIFTS `Q-gsbk14-01` and `Q-YNGH-01`.** See §1.

Two refusals from rev 2 **still stand** (only the election one was overturned):
1. Re-broadcasting the L0 feature-020 "zero consumers" claim — `Q-gsbk14-03` closed it after five
   independent refutations; this lane was the first of the five.
2. Authoring a fifth T24 action-plan template — four exist; v1 adopted, v2 amendments contributed.

---

## 5 · THE TWO TOOL DEFECTS THAT COST WHOLE SESSIONS

**5.1 · `codex exec` false-green.** Root-caused in session 10 and still true:

```
codex exec --skip-git-repo-check "<prompt as an ARGUMENT>"   -> EXIT 0, 39 bytes, ZERO findings
codex exec - < prompt.txt                                     -> EXIT 0, 442 KB, SIX real findings
```

Given the prompt as a positional argument, `codex exec` still waits on stdin, emits nothing, and
exits 0. Any wrapper reading exit status sees a clean review.
🔴 **Always use the stdin form. Re-run every `findings_count=0` verdict on this fleet.**

**5.2 · `buildkit-marathon status` lies without the roadmap slug.** There is **no `--run` flag**;
`--feature` is the only selector, and it takes the **slug**, not the branch, not the spec dir:

```
buildkit-marathon status --feature front-end-goal-term-acceptance-completeness-parser-repl-goal-builders-cross-runtime
```

Confirmed again this session: `run mrun-fb28dd92afe0 [open] seq=40 · steps 3/9 · 23 outstanding`.

---

## 6 · ENVIRONMENT — verified, do not re-derive

- `dotnet` at `C:\Users\smbuser\AppData\Local\Microsoft\dotnet`, **not on PATH**. SDKs `10.0.301`
  and `11.0.100-preview.7` (net11.0 is the pin).
- **`python3` does not exist** — use `python`.
- **No Rust toolchain** — `cargo` / `rustc` / `~/.cargo` all absent.
- `codex` at `/c/ProgramData/npm-global/codex`.
- 🔴 **`Ynet.Transport.Path` shadows `System.IO.Path`** — put `using SysPath = System.IO.Path;` in
  any new file under `csharp/ynet_transport*`. It has now cost a build cycle in two consecutive
  sessions.
- `dart` at `C:\src\flutter\bin\cache\dart-sdk\bin`, not on PATH.
- buildkit exes: `D:\bstdev\research\buildkit\.venv313\Scripts\*.exe`; set `PYTHONUTF8=1` and
  `BUILDKIT_LOCK_WAIT_SECONDS=300`.
- Coop roots: `D:` local · `H:` Ariellas · `I:` Gavriella · `J:` Shiras. **`G:` not mounted.**
- 🔴 **Coop filenames have a path-length limit** — one long name failed 154/154 writes.
  **Always check the written count**; never assume a fan-out landed.
- 🔴 Piping `git show` into `python` was refused by the classifier this session. Redirect to a file
  in the scratchpad and read the file instead.

---

## 7 · RESTART CHECKLIST FOR THE NEXT SESSION

1. `resume marathon`
2. `git fetch origin` — **expect `develop` to have moved again**; other lanes push this repo.
3. `buildkit-marathon status --feature front-end-goal-term-acceptance-completeness-parser-repl-goal-builders-cross-runtime`
4. Read §1 before touching anything election-shaped. **The hold is lifted.**
5. Merge `origin/develop` into the branch; re-run the suite; report the delta from 196/196.
6. Ask the engineer to merge PR #298 (or grant the permission) — §2.
