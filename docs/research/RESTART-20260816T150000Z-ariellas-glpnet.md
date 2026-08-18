# RESTART BRIEF — ariellas / lane `glpnet` / 2026-08-16T15:00:00Z

**Signal: 🟢 GO NOW.** Verified safe: tree clean at `7448e97a`, **0 unpushed**, no suite or runtime
processes alive, channel root `I:/coop/glpnet` resolves. Nothing in flight — no agents, no timers,
no detached runs.

---

## 1 · Resume commands, in order

```
git status; git log -1 --oneline
buildkit-marathon status --feature type-checker-body-atom-moding-accept-head-flipped-readers-unblock-2
buildkit-scheduler status --root I:/coop/glpnet/sched
```

Then read `COOP/ROOT.md` (it is the pointer, **not** the channel), then poll
`I:\coop\glpnet\inbox\ariellas\` **newest-first by mtime**.

**Cursor:** gavriella `20260816T143500Z` · olamnit `20260816T124257Z` · ariellas/tefl `20260816T142705Z`.

## 2 · Position

| | |
|---|---|
| Branch | `076-typechecker-body-atom-moding` @ `7448e97a` — clean, pushed, 47 ahead / 101 behind `develop` |
| 076 | implement COMPLETE at `7821fd2a`; **sign-off BLOCKED** on the Section I C→G failure |
| Marathon | `mrun-d086da8a860f` seq=18, discharge item deliberately unsatisfied (suite 549/550) |
| Board | 6 WPs, **backlog 6, ready 0**, `stale=3` |
| Roadmap | 20 epics / 115 features / **27 not-closed**, 100 % of not-closed scored+promoted |
| Lane | **W4 — board ownership**: allocation, `declared_owners`, actor-id grammar |

## 3 · What to do next, in priority order

1. **Await gavriella's T2 honesty-graft diff (W5).** Review it as the finding's author. **Check it
   distinguishes `no-calendar-record` from a genuinely declared `0.0`** — collapsing them re-creates
   T2 one level down.
2. **Five items with the engineer** (four block other people):
   - **C→G Section I** — the host is clean; one Dart-free script (`bash test/parity/cross_runtime/link_both_ways.sh`)
     settles SC-002 empirically. Needs only a go-ahead. Blocks 076 sign-off **and** olamnit's 041.
   - **§1.14 callee-end** — head combination vs `self.glp`-only privilege. Gates olamnit's FR-002.
   - **066 US4** — mis-wired gate (BC-3 C#-only, BC-4 Gleam-unreachable). Gates gavriella's wave6.
   - **olamnit's DISTRIBUTION v6 row-2** one-word answer — routed to this host's **buildkit** lane.
   - **`aliases.jsonl` shape** — proposed to gavriella, awaiting her ACK.
3. **Do NOT touch D1.** `bc203794` integration belongs to @olamnit/buildkit by engineer ruling.

## 4 · 🔴 Rules learned this session — all four are new, all four cost something

1. **A scan that does not complete is not evidence.** If an exhaustive search times out, **report the
   timeout**. Never substitute a guessed narrow probe; never report its null as absence.
   *(F1 instance 19 — mine. I chased gavriella twice for artifacts she had published 48 min before I
   asked. Retracted at `143000Z`.)*
2. **Verify by CONTENT.** Not exit code *(gavriella — stale `$LASTEXITCODE` turned a no-op into
   `APPLIED`)*. Not timestamp *(olamnit — `robocopy /XO` mirrors a truncation over good history)*.
   Not a guessed path *(me)*. **Three lanes, three surfaces, one rule.**
3. **Never write a peer's status file.** Snapshot beside it with an immutable timestamped name.
   86 924 B protected on this channel at `142315Z`, all sha-verified.
4. **There are FOUR `ariellas` lanes on this host under one actor id** (glpnet, buildkit, tefl,
   qhstate). Before any ask, **check `I:/coop/` and `I:/coop/buildkit/` for an `ariellas` message you
   did not write.** Two of the four have now filed false-absence claims. One already **lost
   `status/ariellas.md`**.

## 4b · 🔴 TWO ACTION HAZARDS FOUND AT 15:10Z — read before running `/bk-close` or `buildkit ship`

**These are new. Both would have caused damage if the obvious command had been run.**

### H1 · `/bk-close` will close the WRONG feature

```
.specify/feature.json  ->  {"feature_directory": "specs/076-typechecker-body-atom-moding"}
```

`/bk-close` resolves its subject from that file and **never checks it is the requested subject**
(gavriella's F1 **instance 15**, corroborated here). Running `/bk-close` for any feature other than
076 **runs the close-out against 076 and exits clean.** 076 is NOT complete (2 open tasks) and is
not eligible to close.

> **Before any `/bk-close`: repoint `.specify/feature.json` at the intended feature, and verify by
> re-reading it. Never trust the command to target what you named.**

### H2 · `buildkit ship` risks a FOURTH trust-material destruction

```
glpquick-cert/   PRESENT, 8.0K, 5 files, gitignored at .gitignore:114
```

This is the exact material `buildkit ship` destroyed **three times** on gavriella's host. Checkout
mechanism REFUTED, blanket `git clean` REFUTED, **responsible agent still unidentified.** Standing
rule: *nothing that pins `glpquick-cert/` until jKMV lands* — **jKMV has not landed.**

**Integrity baseline recorded 2026-08-16T15:15Z (sizes + sha256 prefixes; NO key material copied or
reproduced). Verify these after reboot and after any ship:**

| file | bytes | sha256 (first 32) |
|---|---:|---|
| `glpquick.fingerprint` | 46 | `a6585c2ec6cf1d7ddbdbee6d18b45a12` |
| `glpquick.key` | 241 | `de0cc051b575ffa9747b1f995a1d3034` |
| `glpquick.macaroon.key` | 44 | `1c337222d696881689fc0a370e46acff` |
| `glpquick.pem` | 583 | `a960a1ef676f5b6eb52e3cfb5e2e75e3` |
| `glpquick.pfx` | 863 | `bf01c2c388cb944fc1f2b65aba63b6b1` |

Re-check with: `for f in glpquick-cert/*; do sha256sum "$f"; done`
**A mismatch or a missing file is a destruction event — report it, do not regenerate.**

### H3 · Ship/close eligibility — the analysis, so it is not redone

| feature | implement | codexreview | eligible? |
|---|---|---|---|
| `064-post-wave-gap-closure` | 32 done, 1 open | ✅ `t040-codexreview.md`, run `20260803T214953Z` | **Sole open task IS "ship then /bk-close" — but its roadmap row is ALREADY `closed`.** T041's stated outcome is already achieved; re-shipping risks H2 for no state change |
| `076-typechecker-body-atom-moding` | 13 done, **2 open** | ❌ none | **NO** — T014 *is* "both suites green vs baseline, SC-002", i.e. the C→G blocker |
| `durable-listener-service-box` | roadmap `released` | — | **NO** — spec dir `specs/064-durable-listener-service-box` **does not exist here**; awaiting olamnit's buildkit-side close-out receipt |
| `061` wave-2 | 41 done, 1 open (UPPAAL) | mvp-gate-review | **NO** — handed to gavriella |
| `062` / `063` wave-4 / wave-5 | 0 open | — | **NO** — blocked-on-D4; retirement impossible with the shipped tool (two hosts) |

**Nothing in glpnet is both fully complete and safely shippable right now.**

## 5 · Standing rules (unchanged, still binding)

- **Count not-closed from the export `heads` fold, NEVER `roadmap status`** — status returns 114/26
  and silently drops `qr-link-provisioning`. That is D8, worse than filed.
- **Never `buildkit-scheduler` without `--root I:/coop/glpnet/sched`.**
- **`fallback_used=True` is permanent and advisory** — never a root signal. The root check is
  `buildkit-scheduler root --root <R>` → `exists=True`.
- **No poll sorts by filename** (`BROADCAST-*` outranks every timestamped file — instance 17).
  Report **examined-vs-existing counts** or it is not a receipt.
- **Measure a notes row before appending** — `--notes` resends the whole body on the command line;
  32 767-char OS cap; at the cap the row is permanently unappendable. **glpnet is safe: largest row
  3 089 B, 9.4 % of cap.**
- **069 is NOT merged and will not be** — red suite + unredacted secret path.
- **Nobody merges alone** outside the D1 scope the engineer named.
- **Never start a second suite run while one is live**; concurrent suites exhaust Git-Bash forks and
  poison Section I.
- **The 5-minute ACK timer STAYS** (engineer: *"leave timer"*). Report a miss as **carrier latency**
  with a receipt; never name a lane absent; never take a one-way action off a silence reading.

## 6 · Closed this session — do not re-do

- **`078` collision** — engineer: *"078 ok yes"*. gavriella keeps `078`; olamnit → `080` (own files
  only). Awaiting olamnit's one-line confirmation.
- **`064` D11** — score restored to **3.20 / 1440** by gavriella.
- **3rtask programmes** — **both fetched** to `docs/research/3rtask-methods/` (`62d3ec43`).
  Programme A `d03e`: 13 frozen elements, 17 adjudications. Programme B: **DRAFT, not frozen** —
  red-team-then-freeze is olamnit's step. **Do not ask for these again**; six method documents exist
  across five channels.
- **T1** — closed. Fixed twice independently; D2 ruled *keep shipped R2*; `20d78ba4` is a **rival
  mechanism, refused on the merits, not on size**.
- **Q5/Q6/Q7** — ruled. **Q6 AMENDED**: ownership is entitled but **UNENFORCED**
  (`dispatch.py:156 ownership={}`) — the board never gated any lane, so only self-restraining lanes
  were ever constrained.
- **`/bk-scheduler` skill** — was missing entirely; installed byte-identical + a fenced local
  addendum making `--root` mandatory (`ba8ccc72`).
- **Flow-gate audit** — `docs/research/flow-gate-audit-2026-08-14.md` (`ceab70c6`). **T1/T2 are NOT
  this board's blocker.** The blocker is a **cold-start hold-out deadlock**: no completions → no
  actuals → PERT cannot estimate → held out → never ready → never allocated → never completes.
  **Neither D1 nor D2 starts the glpnet stream.**

— ariellas · lane `glpnet` · host Ariellas · 20260816T150000Z
