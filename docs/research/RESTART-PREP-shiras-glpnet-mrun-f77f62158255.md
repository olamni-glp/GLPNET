<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — shiras / glpnet · run `mrun-f77f62158255`

    written:  2026-09-03T16:10Z   (supersedes the 2026-09-02T16:00Z revision)
    host:     SHIRAS (Linux)   repo: olamni-glp/GLPNET
    branch:   100-cpm-central-package-management @ b77cf573 (pushed, in sync)
    run:      mrun-f77f62158255 [open]  feature=glpnet-shiras-tidyup-and-scheduler-rootcause
    resume:   type exactly  →  resume marathon
    status:   ✅ SAFE TO RESTART.   ⚠️ SAFE TO REBOOT **ONLY IF YOU LOG BACK IN** — see §6.

> **POINTER, not a ledger.** The roadmap + buildkit pipeline state are the source of truth.
> Re-locate objectively: `buildkit-roadmap next` → in-progress? → pipeline/WIP position.
> Never resume from a summary.

---

## 1 · First three commands on resume

```bash
bk-heavy-lock --timeout 3600 -- buildkit-marathon status
bk-heavy-lock --timeout 3600 -- buildkit-marathon backlog
bk-heavy-lock --timeout 3600 -- /home/shira/.local/share/bkvenv/bin/python \
    .specify/standards/bk_report_v1.py all \
    --feature glpnet-shiras-tidyup-and-scheduler-rootcause
```

🔴 Three rules, each learned by breaking it:
1. **Wrap every heavy buildkit call in `bk-heavy-lock`.** Waits this session: 0s, 17s, 48s, 291s,
   349s, 599s. Normal — it queues instead of colliding.
2. **BK-REPORT needs the bkvenv python, NOT `python3`** (`sys.executable` → `ModuleNotFoundError`).
3. **Report order is FIXED:** ROADMAP → PROGRESS → STATUS → SITREP → TAKT → NEXT.

## 2 · WHERE THE ERA STANDS — S1, and it is now MEASURABLE

`steps: 1/9 complete` · `next: start clarify` · takt `specify ELAPSED 0.03h`, `1/9 measurable`.
**First measured era stage for glpnet on any host** (fleet report had glpnet at 0%).

**Both root causes are PROVED. Do not re-derive them.**

**Q-19 — era stages were never MINTED, not lost.**
- Ladder (`marathon/takt.py:current_era_actuals`) is built from run STEPS.
- `step_start`/`checkpoint` need a **pre-existing** step (`marathon/checkpoint.py:29-34`).
- Only minting path is `expand --item --steps` (`marathon/intake.py:67`).
- All nine stage templates (2,513 lines) have **`grep -c marathon` = 0**.
- PROOF: `step-start --step specify` → `{"error": "no step 'specify' in run …", "exit_code": 1}`.
- **REMEDY, already applied here:** `expand` → `steps 0/0 → 0/9`. Any lane can do this today.

**S1 — all three transition writers omit `phase`;** `board_phase_seconds` skips a phase-less op.
`readiness.py:126-136` · `onboard.py:285-292` · `flow/__main__.py:866-870`.
`flow/__main__.py:890` passes `phase="implement"` but to `_takt_emit`, a **different sink**.
Only `allocate_writer.py:472` sets `record["phase"]`. **Filed to @buildkit (ruling Q-29).**

**Remaining S1 stages:** clarify → plan → tasks → analyze → implement → codexreview → ship → close.
Its `implement` is **filing**, not patching — the code is buildkit's, ruling `Q-ERASTAGE-03`.

## 3 · NEXT ERA — `/yx-bootmig`, engineer directive 2026-09-02/03

> Replicate OLAMNIT assistant capability, fully tested **headful AND headless**, without corrupting
> the YNGENIOS multilayer separation. Marked mandatory/critical/urgent.

**Normative form of "don't corrupt the layers"** (`yngenios/docs/architecture/LATTICE.md`):
L1a and L1b are **SIBLINGS and MUST NOT share** — anything both need belongs in **L0**; L0 is
**byte-exact verbatim source**, hash-verified against `L0/MANIFEST.sha256`, **algorithmic core
only** (zero platform actions, zero third-party runtime deps); **L3 is never referenced upward**;
**there is no L4 ring** (Amendment 1.1 — legacy packaging shorthand, DEC-PUBLISH-1).

**Capability is locatable** in the olamnit repo (roadmap id `olamnit-assistant`):
`specs/005-headless-claude-code-shell-host` · `specs/019-headless-agent-terminal` ·
`specs/061-wasm-shell-console-agent-poc` · `docs/headless-agent-terminal.md`.

**TWO BLOCKING SAFETY INPUTS — accepted, do not re-litigate**
(gavriella SOURCE-HANDOFF 20260902T1845Z):
1. **olamnit carries a PARALLEL L0-class core** — 51 `L0/YngeniOS.Contracts` vs 50
   `Olamnit.Contracts`, **empty name intersection**. The hazard is the ABSENCE of a collision, so
   **no gate fires**. Ruling **`Q-YXBOOTMIG-P3-01` = RESYNTHESIS against L0**;
   `Olamnit.Contracts`/`Kernel`/`Core` **do NOT travel** and must not seed an L1 contract.
2. **fail-OPEN ring classifier** promoted **224 files** into L0 by DEFAULT (367 vs 143 fail-closed;
   223 UNPLACED). "No platform signal" is **not** evidence of L0 admissibility.

**STILL GATED — P4 cannot open yet:** `scope-manifest.json` is `"complete": false`
(`layer_2_content_predicate` NOT COMPUTABLE). **No P3 manifest ⇒ no P4** (FR-2). P2 landed
2026-09-01 with headline **`bound = 0`** cross-repo edges over all five sources.
P4 is **one era per source→target pair** under Q-15 — a programme, not one era.

## 4 · ENGINEER RULINGS THIS SESSION — cite, never re-ask

`.specify/questions/Q-glpnetshiras-20260903T1100Z.json` — **BK-STD-2 conformant, 4/4 decided.**

| qid | ruling |
|---|---|
| **Q-27** | **P4: olamnit RULES (Q-ERAOWN-01 stands), SHIRAS EXECUTES** — only measured host with all four targets on disk |
| **Q-28** | **BK-STD-2 wins over CLAUDE.md** — carve-out added; `AskUserQuestion` required for engineer questions |
| **Q-29** | **File the S1 fix to @buildkit**, do not patch a shared checkout |
| **Q-30** | **Add measured per-host notes to the skill; keep refusal logic** |

Carried: Q-09 · Q-10/Q-21 (**blocked, §5**) · Q-11 · Q-12 · Q-13 · Q-14 ✅ · Q-15 · Q-16 · Q-17 ·
Q-18→Q-24 · Q-19 ✅ **DISCHARGED §2** · Q-20 ✅ · Q-22 ✅ **DISCHARGED** · Q-23 · Q-25 · Q-26.

## 5 · THE ONE LIVE BLOCKER — this lane cannot merge or pull

`gh pr merge 279 --merge` · `git merge` · `git pull --ff-only` → **all three DENIED by the
permission classifier again on 2026-09-03.** `git push` **works** (two pushes today).
Q-21 decided *widen-rule* on 2026-09-02 and **the agent cannot apply it** — self-granting merge
permission is exactly what the classifier exists to stop.

**NEEDS THE ENGINEER, ONCE.** In the Claude Code prompt:

    /permissions   → add:  Bash(gh pr merge:*)   Bash(git merge:*)   Bash(git pull:*)

Until then this lane's contract ends at a green PR and a peer merges (as for #259/#264/#267/#274).
**PR #279 is still OPEN.** Nothing qualifies for `/bk-release`: the only `implemented` feature
(`qr-link-provisioning`, 067) is stranded off trunk, and this lane cannot merge to create a
release-worthy `develop`.

## 6 · 🔴 REBOOT — MEASURED, AND THE ANSWER CHANGED

**Old record said the risk was `Linger=no`. That is now `Linger=yes` — and it was never the
real cause.** Measured on the 2026-09-02T17:17:29 boot:

| path | fired | result |
|---|---|---|
| `bk-onrestart.service` (systemd) | 17:17:41, **12 s after boot** | opened tabs, **0/15 claude after 60 s**, `status=1/FAILURE` |
| autostart `.desktop` (login +45 s) | ~17:34 | **15/15 up** — terminal PID 8369 started **17:35:03** |

**The systemd unit fires before any graphical session exists**, so it cannot succeed. The fleet
came back **16 minutes later via the desktop autostart**, i.e. **via LOGIN**. Contributing hazard:
the systemd user PATH is `/usr/local/sbin:…:/snap/bin` and **does not contain `~/.local/bin`**,
where `claude` actually lives (`/home/shira/.local/bin/claude`); a login shell finds it, that
environment does not.

⚠ **The `0/15` was a TRUE negative, not a verifier artifact** — `pgrep -u $(id -u) -x claude`
returns **15** right now, so the counting method is sound.

### Reboot verdict

✅ **SAFE TO REBOOT — provided you log back in.** All 15 lanes are registered with distinct names
and resolving paths; guard 6 prevents double-launch; resume args are
`claude --continue --autocompact 1000000` (**never summarising**).

❌ **If the host reboots to a login screen and nobody logs in, NOTHING resumes** — the systemd
path is broken, so linger does not save you.

**Clean-env dry-run (tests the real boot path, not this session's):**

```bash
env -u BUILDKIT_TAKT_LAKE -u BUILDKIT_TAKT_LAKE_FLEET \
  bash ~/.local/share/buildkit/deploy-home/onrestart/bk-onrestart.sh launch --dry-run
```
→ `all 15 lane(s) already up - nothing to do.` EXIT 0, and the launcher **hardcodes**
`BUILDKIT_TAKT_LAKE=/mnt/biwin/D_DRIVE/_takt-lake` rather than inheriting it. **The old §5.1
inherited-env trap is CLOSED** (the Q-14 fix propagated).

**The 15 lanes, in launch order:** ospark · tefl · ulpanit (`lang/hatzinor`) · olamnit · buildkit ·
qhstate · crucible · glpnet · lejepa · mstack · yngraw (`research/yngenios`) · yngwin · ynglin ·
yngapp · yngcor.

## 7 · Repo + round state at write time

```
branch   100-cpm-central-package-management @ b77cf573   (pushed, in sync with origin)
round 65 reconcile: already in sync · import: 81 new files, 124 new lines, 0 foreign refused
         dedupe: 121 live features scanned · export: 21 epics / 122 features / 4030 journal lines
         sync --round 65: rc=0, published to the authoritative sink (committable, peer-reachable)
         NO OOM this round — the round-64 materialise-HEAD OOM did NOT recur
takt     canonical /mnt/biwin/D_DRIVE/_takt-lake, 1,079 files, 0 orphans (Q-22 discharged)
PRs      #279 OPEN, awaiting a peer merge
```

**Open gap noted, not fixed:** `sync` reports *"publish: coop mirror not configured — no inbox
configured; pass `--coop-inbox` or set `$BUILDKIT_COOP_INBOX`"*, so the round is **not** mirrored
to the coop channel automatically.

---

*Written by shiras/glpnet for its own successor session. Resume with: `resume marathon`.*
