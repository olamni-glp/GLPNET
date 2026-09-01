# Handoff receipt — olamnit/glpnet era discharge, 2026-09-01

**Authority:** ruling `Q-GLPNETO16-02` (discharge this run; migrate live items to a
properly-formed next era) and `Q-GLPNETO16-04` (transfer cross-lane items out with a
handoff receipt), taken 2026-09-01 via BK-STD-2, set
`.specify/decisions/Q-GLPNETO16-20260901T0730Z.json`.

**Discharged run:** `mrun-76da6e46bd44`, feature `tidy-up-branches-worktrees-olamnit`.
Its namesake work shipped, released and closed; the 23-item backlog that accreted onto
it is dispositioned below. **Nothing is dropped** — every item is either transferred
with a named owner, resolved with evidence, or migrated onto the successor run.

---

## A. Transferred OUT — BUILDKIT lane

Authority: `Q-hardening-01` (buildkit lane owns HOST-INTERCONNECTIVITY-HARDENING;
glpnet lanes are evidence contributors only) and `Q-tidyup-01` (M01 §5.2 is buildkit
spec-first work). These are engine/tool changes whose home is the buildkit repo; this
lane has no write authority for them and they were blocking a discharge gate they can
never satisfy here.

| item | title | why buildkit |
|---|---|---|
| `mitem-01M0NJQTDC9KJ0FHWWT2CTXPEJ` | P01 scheduler-feed starvation — **build half** | Rootcause half is DONE (3rtask run `20260828T012249Z-b414`, merged as PR #251). The fix composes `buildkit-scheduler` + `bk-flow` verbs. |
| `mitem-01M0NJQXPFP7A38CWZQTTMDJZ0` | P02 bk-onrestart configurable 1-or-2-window resume | Engineer-directed 2026-08-25 to the buildkit roadmap. |
| `mitem-01M0W62EA0GVH9475JX1ARF1V2` | M01 §5.2 reproducible phase-exit gates | `Q-tidyup-01`: cross-repo engine work, buildkit spec-first. |
| `mitem-01M0W63CS7F6EVW8N03FHKXM5S` | bk-onrestart 2-window per-host-configurable feature | Title states BUILDKIT lane. |
| `mitem-01M12K5CSVXE0ANH3CMR7W9T2S` | BUILDKIT feature: /bk-onrestart per-host-configurable + auto-install | Duplicate of the row above; both carried for lineage. |
| `mitem-01M126PSFHQE5S3BHZV39WWFXC` | HOST-INTERCONNECTIVITY-HARDENING | `Q-hardening-01` — buildkit lane owns; glpnet evidence contributed and merged. |
| `mitem-01M0QNDYFQYDQQKYAV87P5QJCC` | D01 era metric/tag in marathon | A change to the marathon tool itself. |
| `mitem-01M0QNEDY75SAE0WEJP85B11AG` | D05 takt metrics into marathon checkpoints | A change to the marathon tool itself. |
| `mitem-01M1CW7827ZP51MX4EAFQ8HM1R` | roadmap `import` reports a PGLite OOM as "malformed journal record" | Defect in shipped `buildkit-roadmap`; a peer export is silently dropped and the operator is told the peer sent bad data. |
| `mitem-01M1DY60RZJNYTW4WH2TYPFE4H` | `engineer-decisions.jsonl`: an empty-answer append clobbers a real ruling | Defect in the shipped decisions path — no guard against an empty answer overwriting a decided one. Live instance: `Q-glpnetshiras-01`. |

**Also for buildkit, discovered this session and not previously filed:** no verb writes
a taken ruling back onto its question record. 30 of 31 rulings in this repo had landed
in `engineer-decisions.jsonl` while the `Q-*.json` records still read undecided — the
exact re-ask failure BK-STD-2 exists to prevent. Remediated here at `ff4c419f`, but the
root cause is upstream.

## B. Transferred OUT — gavriella/glpnet lane

| item | title | authority |
|---|---|---|
| `mitem-01M0Q4KS3JEYDA0PB1NZ9PDVHN` | M01 /bk-marathon → /bk-flow migration | `Q-GLPNETS13C-04` gave the bk-flow readiness campaign to a full 3rtask in that lane. |
| `mitem-01M0QNE2YEE4WSPC795CQ8JR25` | D02 /bk-flow adoption readiness deep-check via /bk-3rtask | Same ruling. Also gated on `Q-GLPNETA15-04`, still open. |

## C. Resolved with evidence — nothing to carry

| item | title | evidence |
|---|---|---|
| `mitem-01M1DY5W8Q8C0DHPZHY8X07C7Z` | Three contradictory release-bar rulings | Resolved by `Q-GLPNETO16-01`; precedence written down as `BK-STD-3`. |
| `mitem-01M1C7RRYMQAB8MM4428NASKJG` | YX-BOOTMIG era refused here | The refusal *is* the decision; glpnet is source-only for that programme, and the era lives in the yngenios repo. |
| `mitem-01M0WBNQ9YW3XCG2X2XGA5ST57` | 3rtask shiras-partial-board-absence (staged) | Superseded by the consolidated 3rtask `20260828T012249Z-b414`, which established SHIRAS-absence and OLAMNIT-starvation as ONE SCHED-R7 defect. Its rootcauses are merged (PR #251). |
| `mitem-01M127NFHMKWRRFVEDE76FY34P` | Fleet .NET 11 + C# 15 | Discharged on this host: SDK `11.0.100-preview.7`, C#15 pin authoritative on develop `33ec94a2`, all 23 csproj `net11.0`, 1111 tests 0 failed. Remaining hosts are those hosts' work. |

## D. Migrated to the successor era

Successor era: **`083-glptutorial-corpus-goldens`** — ruling `Q-GLPNETO16-03`
(highest-WSJF candidate carrying no other lane's ruling and no §1.14 gate).

| item | title |
|---|---|
| `mitem-01M0Q4M3PWMHCQ97FBHC8A228D` | C083 completion spine glptutorial-corpus-goldens — **becomes the era itself** |
| `mitem-01M0Q4KX8SEER8WDY89WRNRQWQ` | X00 pipeline pointer drift — closed by setting the active-feature pointer to 083 |
| `mitem-01M0NJR0TRM39ZMTGSKAHKJ6W3` | P03 complete ALL outstanding unshipped work in this repo |
| `mitem-01M126Q01JNDB57Y2MWX7SWY6M` | Complete EVERY specified-status feature to close |
| `mitem-01M0Q4M0FDVBF5HE1JTC1Y4FQY` | C082 completion spine feature-stream-superset |
| `mitem-01M0Q4M6ZT37TJQYNY75N2E22V` | C085 completion spine onrestart-fleet-resume |
| `mitem-01M126PWPYWW21PXH8FQSADF3F` | Takt-ducklake reporting pin-blocked on this host |
| `mitem-01M0NAYSAG84C6WA88X1V48FAJ` | G01 private-key rotation + history rewrite (engineer-gated) |
| `mitem-01M0NAYZSJDZ4DKAV506JPHPVH` | G03 GitHub Release backfill + publish gap (engineer/permission-gated) |
| `mitem-01M0Q4MVCQHEC2ZD5ZZX1VZAKX` | G082 capability-name normalisation + 082 homing (engineer-gated) |
| `mitem-01M0Q4MYQ94XZMMH2SHDSJTCFW` | G083 083 homing + FR-002 §1.14 ruling (engineer-gated) |
| `mitem-01M0Q4N1GJ6GNV84R09RV8ME5Q` | G085 085 homing + FR-029 (engineer-gated) |

---

## What the receiving lanes should do

**buildkit:** items in section A are yours. The two shipped-tool defects (roadmap
import OOM misdiagnosis, empty-answer ledger clobber) and the missing ruling-write-back
verb are independently reproducible from this repo; ask and I will hand over the exact
reproductions.

**gavriella/glpnet:** items in section B are yours by your own rulings. `Q-GLPNETA15-04`
(bk-flow rollout controls) still gates D02.

**Nothing in this receipt is a request to re-do work.** It records where each item now
lives so that no item is owned by two lanes and none is owned by none.
