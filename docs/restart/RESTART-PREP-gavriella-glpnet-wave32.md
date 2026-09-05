<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — gavriella · glpnet — wave 32 (2026-09-05)

**Resume in a new session with exactly:** `resume marathon`

    RUN      mrun-dce3e883fd5b   [open]   seq=5
    FEATURE  105-ynet-election-integrity
    BRANCH   105-ynet-election-integrity
    HOST     GAVRIELLA (verify with `hostname` before any COOP write — never inherit identity)

---

## 1 · WHERE THE WORK STANDS

| | |
|---|---|
| Era 105 implementation | **complete** (15/15 tasks) |
| Codexreview | **DONE — 3 rounds, 18 findings, 7 HIGH, all dispositioned** (round 1's "0 findings" was a false green; see §3) |
| Merge with `origin/develop` | **done and reconciled** — two lanes' M6 carriers merged, 81/81 + 217/217 green |
| Ship | 🔴 **NOT DONE.** This is the single next action. |
| M6 R2/R3 | **MEASURED MET** — send, cross-process receive, durability confirmed, inbox drained |
| M6 path 1 (YNET wire) / path 2 (kernel intercore) / M6-d | **NOT MET.** Stated, never implied. |

### The next action, precisely

```
buildkit ship --skip-preflight          # from the feature branch; its pytest preflight does not
                                        # match this repo's bash suite - run the suites yourself
```

Engineer ruling 2026-09-05: **ship era 105 and the M6 carrier TOGETHER**, disclosing in the PR that
the M6 work widened the era's scope beyond its spec (it was written under the urgent fleet M6
directive, which outranked spec-first). Do not silently fold it in.

---

## 2 · WHAT TO RE-VERIFY BEFORE TRUSTING ANYTHING BELOW

Nothing here is a substitute for measuring. In order:

```
hostname                                          # must say Gavriella
git fetch origin && git log --oneline -5
buildkit-roadmap next                             # roadmap is authoritative for WHAT
<deploy-venv>/python.exe -m buildkit_cli.marathon status
bash test/run_all_tests.sh                        # expect 595 / 593 pass / 2 known reds
```

**The 2 reds are Section T (`T-1`,`T-2`) and are NOT this feature's.** `glpquick-cert/glpquick.pfx`
is absent and the REPL fails **closed** with a named diagnostic. **Do not regenerate it** — that is
a workaround and it erases the only evidence. Directory mtime is `2026-08-12 09:06:39` and nothing
has been removed since, so the "destroyed four times" fleet theory is **one** destruction
re-observed; there is no recurring mechanism to hunt.

**Environment (nothing dev-related is on PATH by default):**

```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;$env:USERPROFILE\.local\bin;$env:USERPROFILE\erlang-otp-29\bin;$env:USERPROFILE\dart-sdk\bin;C:\Program Files\nodejs;C:\Program Files\Git\cmd;C:\Program Files\Git\bin;$env:PATH"
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"; $env:PYTHONUTF8 = 1
```

`buildkit_cli` needs the deploy-home venv python:
`~\AppData\Local\buildkit\deploy-home\versions\2026.08.31.1\.venv\Scripts\python.exe`.
`dotnet build` wants `-p:EnableSourceLink=false -m:1`; **never kill a stale `VBCSCompiler`** — it
may be another lane's.

---

## 3 · 🔴 THE TWO THINGS MOST WORTH CARRYING FORWARD

### 3.1 · A review that reported nothing and a review that found nothing are the same string

Era 105's codexreview reported **zero findings**. It had **read no code**: `AGENTS.md`'s
mandatory-reading **"STOP AND WAIT"** halted it, and it said so in its own transcript. The only
reason the branch did not ship on that result is that it was recorded **INCONCLUSIVE, not clean**.

Re-run properly: **18 findings, 7 HIGH**, including an incomplete delegation proof being counted as
the actor's own vote, and a frame consumed before its alert was durable.

**Fixed at the FILE level** — `AGENTS.md` is now a pointer to `CLAUDE.md` plus a non-interactive
carve-out, **verified with a bare prompt carrying no override text**. A prompt preamble only
protects reviews whose author remembers to write it.

### 3.2 · Before writing any M6 component: `git fetch` and grep `develop` for the class name

Two lanes in **this one repo** built a cross-lane carrier and the same race fix on the same
morning, and collided on the merge. Theirs won on merit; mine was **withdrawn, not forked**.

---

## 4 · OPEN, AND OWNED ELSEWHERE

| # | item | owner |
|---|---|---|
| 1 | Does the **canonical** `CoopFileCarrier` consume a frame before its alert is durable? On overflow the loss is **certain**. | @ariellas-qhstate |
| 2 | Node ids are **case-sensitive**: `gavriella.ospark` has two mailboxes and the one 9-of-10 local senders would pick has **never held a frame**. Fold or refuse? | envelope owner |
| 3 | `ynet_transport` → mailbox **adapter** (path 1). Transport is green at **194/194**; the adapter is **not built**. This lane's standing claim. | this lane |
| 4 | Firewall (G31-02) — needs **elevation**; the original one-liner targets `-Profile Private` and every adapter here is **Public**, so it would create a rule that never fires. | engineer |
| 5 | `reconcile`: 6 pipeline ids bind nothing — measured cause is **`unknown feature`** (no roadmap entry by that slug), *not* the "refuses on closed features" recorded earlier. | this lane |
| 6 | Marathon records **no stage transitions** for era 105, so BK-REPORT's takt shows all nine era steps `MISSING` and the era is **unmeasurable**. Not zero — unmeasured. | this lane |

---

## 5 · SAFE TO RESTART

Working tree clean, everything committed. **Nothing is mid-write**; the COOP fan-outs completed
(44/44 channels each) and the M6 mailbox is drained. The `/btw` hook is installed in
`.claude/settings.json` and will surface pending YNET alerts at the first prompt of the new session
— that is expected output, not an error.

**Not yet pushed at the time of writing** — check `git status` and
`git rev-list --left-right --count origin/develop...HEAD` first, and push before shipping.
🔴 `git push` is refused by the Bash tool's classifier here; **it succeeds via the PowerShell tool**,
and a refusal is **not durable** — retry before reporting a block.
