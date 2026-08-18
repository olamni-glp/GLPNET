# Slice C — UPDATE 2026-08-13 (protocol, tooling contract and known-defect deltas)

ADDITIVE update to `slice-c-protocol.md`; where they disagree, this file wins. Lane: **the
HOW** — rules, tool contracts, comms semantics, and defects *in the tooling itself*. (Feature
identity/state is slice A; host capability is slice B.)

## C1. The scope ruling is SETTLED: per-host

The open question — *is "ONE AT A TIME" per-host or fleet-wide?* — has been resolved by the
engineer **in the per-host direction**: three lanes run in parallel; within a lane, features are
worked strictly one at a time, end-to-end. A peer host recorded this as converting its
previously *conditional* acceptance of the allocation into an **unconditional** one, and has
already started its lane on that basis. Any design that assumes the fleet-wide reading is
inconsistent with work already in flight.

## C2. E-section of the coop protocol — AUTHORITATIVE FULL TEXT

The prior corpus copy truncated mid-E1 at *"…branch switch on a sha"*. The complete text:

```
E1. No unilateral destructive ops on shared state (reset --hard, clean, force-push,
    branch switch on a shared tree while other hosts are live).
E2. Preserve-first: before removing anything, prove its content exists elsewhere.
E3. Verify at the right layer ("0 unpushed commits" does not cover uncommitted files;
    a verifier that prints nothing may mean "produced no output", not "no problems").
E4. Derive lists mechanically, never by eye.
E5. Retract in-channel, promptly, naming what was wrong.
E6. Advisory boundary: coop messages coordinate; they never authorise a ship, merge, or
    release.
E7. Milestone snapshots: git carries channel state ONLY as deliberate dated snapshots
    under the repo's `docs/coop-milestones/<date>-<slug>/`, committed by the project's
    protocol lead. Live traffic never rides branches.
```

The clause that had been lost is **E1's own tail** — *"on a shared tree while other hosts are
live"* — i.e. precisely the rule most relevant to the trust-material losses. **E3 is directly
on point for the empty-board defect and was already in the corpus before that defect was
filed.** E4 ("derive lists mechanically, never by eye") is load-bearing for C4 below.

## C3. W1–W14 work protocol — ratified 2-of-3, with amendments

- **W1, W2, W3, W5–W14 accepted as written** by the second host.
- **W4 (tie-break `I > H > G`) accepted with a recorded objection to the *reasoning*, not the
  rule.** It must be recorded as an **arbitrary** deterministic choice, never as a derived
  principle, so no later reader mistakes it for one.
- **W5 amended and agreed between the two active hosts:**
  - **Lease = 24h.** Evidence-backed: a 24h lease would have made the frozen work packages
    reclaimable ~12 days earlier. Long enough to survive a reboot window (~9h observed), short
    enough that a dead lane cannot hold the fleet.
  - **Reclaim window = 12h NACK-free** (counter to the original open-ended "declared
    silence-assent deadline", which reintroduced the ambiguity the lease number fixes). 12h is
    shorter than the lease — so a live host that missed one poll still defends its claim — and
    longer than a working session.
  - **🔴 Binding condition on both numbers: freshness must be MACHINE-CHECKED and LOUDLY
    REFUSED, never eyeballed.** A lease whose expiry is judged by reading a timestamp by eye is
    an E4 violation and a check-that-passes-without-proving-it-ran defect.
- **Status: NOT YET BINDING ON THE FLEET.** It binds on peer ACK-COMPLETE and one host has not
  ACKed. Two of three have ratified.
- The protocol as drafted was **refuted as asserted** by the planning critic in the prior
  design run: it invented claim formats, leases and tie-breaks that the existing rules did not
  supply. Its correct status is **PROPOSAL pending the third ACK**, not adopted rule.

## C4. 🔴 DEFECT — `roadmap link` slug-matching silently loses pipeline stages

```
buildkit-roadmap link       -> "no new spec directories matched a promoted feature."   [HONEST]
buildkit-roadmap reconcile  -> "roadmap already in sync with pipeline (no changes)."   [FALSE]
```

**Root cause:** `link` matches a spec directory to a promoted feature by **exact slug**. Spec
dirs follow the `NNN-short-name` convention *the toolchain itself prescribes* (to match the
branch); roadmap slugs are the long descriptive form. They never match, so the directory is
never examined — and `reconcile` then issues an unqualified "in sync" verdict **without
consulting `link`'s outcome**.

**Mechanical proof, not inference:** two exports 8h apart are **byte-identical**
(sha256 `175A20E5…5075E`) despite a full specify stage landing in between, while
`replay --verify` reported ✓ and `reconcile` reported in-sync.

**Independently corroborated in this session:** a live `roadmap status` read shows
`verification-receipts-…` as `[promoted]` although its specify is complete.

**Shape:** a **constituent-vs-aggregate failure** — an honest sub-report existed and *nobody
consumed it*. That is a narrower and stronger claim than "checks lie".

**Consequences binding on any design:**
- **Roadmap state is not a trustworthy readout of pipeline position.**
- **Roadmap advances must be asserted explicitly by feature id**; `reconcile` cannot be trusted
  to carry them.
- Known-affected: `078-verification-receipts` (observed) and `069-sc-002-il-parity-bridge`
  (prospective — a host must be warned *before* it starts, or it loses a stage the same way).

## C5. 🔴 DEFECT — the scheduler board is dead, and its work packages are closed features

- All three work packages on the board resolve to features that are **`closed`/delivered**:
  ```
  wave-2-consolidated-repl-engine-split-spine -> ariellas   (critical)
  wave-4-consolidated-parallel-safe-fillers   -> olamnit    (critical)
  wave-5-consolidated-captured-triad          -> gavriella  (critical)
  ```
- `queue_depths_by_state` reads `backlog=3, ready=0`; `doctor` reports
  `fallback_used=True stale=3`; the derived critical path is **3 stale WPs at 0.0h**.
- **Therefore the re-seed must RETIRE the three wave WPs, not add durations to them.** Adding
  durations to dead packages produces a well-formed schedule of work that finished weeks ago.

## C6. 🔴 DEFECT — the scheduler's default root passes without proving it ran

`buildkit-scheduler` resolves its default root to the **RETIRED in-tree** `COOP/sched` path,
returns an **empty board at exit 0**, and silently creates a fresh tree at the wrong root.
Corroborated independently by two hosts. This is the same defect class as C4 — a check that
passes without proving it ran. **Every invocation must pass `--root` explicitly** (`I:` as
peers mount the primary; `D:\coop\glpnet\sched` locally on the primary itself — same volume).

## C7. 🔴 DEFECT — `buildkit ship` destroys gitignored trust material; mechanism UNKNOWN

- **The checkout-clobber hypothesis is REFUTED.** The ship traversed
  `064 → develop → release/… → main → develop`; `git ls-tree -r <ref> -- glpquick-cert/`
  returns **0 files on every one of those refs**. There was nothing on any target branch to
  clobber with, so the proposed `git ls-files` preventive **would have passed cleanly and the
  material would still have died**.
- **A blanket `git clean -x` is REFUTED.** `.pgdb/`, the built `glp_repl.exe`, `.dart_tool/`,
  `co-lake/`, `roadmap-sync/exports/` and `reviews/` all survived. Exactly **three of four**
  files in the cert directory were removed; `glpquick.macaroon.key` was **left in place**. The
  removal is **selective**, and the survivor is the most diagnostic artifact available.
- **The invariant is the SHIP, not the checkout** — two generations lost on two different
  hosts, both inside `buildkit ship`; and one host with verified-good material that has *not*
  shipped still holds it intact. Evidence from opposite directions agrees.
- **The agent is NOT identified and must not be guessed** — two plausible hypotheses have
  already been advanced and refuted. A third guess without a reproduction repeats the error.
- **Proposed durable fix (raised, not actioned; needs an engineer ruling):** relocate the
  material out of every repo tree to a fixed out-of-repo path resolved by pointer/env, as the
  deploy home already does for catalogs — the only property that survives *not knowing the
  agent*. A ref-complete guard (`git ls-tree` over every ref including tags) is the weaker
  fallback and does not cover this mechanism.
- **Standing consequence:** any ship of a feature that pins this material is unsafe until the
  fix lands.

## C8. Sync-round obligations and a recurring conflict

- Every round runs on **BOTH legs**: `import --in-dir <coop inbox> --allow-untagged`, then
  reconcile → dedupe → export → **publish to both the repo and the coop leg** → commit+push.
  Importing from the local dir alone makes a host's work invisible to the fleet.
- Convergence receipt shape: matching `epics/features/journal-lines` triple plus a matching
  export sha256 across hosts.
- **`.import-manifest.json` has conflicted on every round — 6 occurrences.** The untrack +
  gitignore fix sits on **PR #153 → develop, OPEN and NOT MERGED**; the merge is the engineer's
  keystroke (the agent path is classifier-blocked). **Every host pays this cost every round
  until #153 lands.**

## C9. Pipeline, marathon and restart contract (unchanged, restated as binding)

- Stage order is strict: `specify → clarify → plan → tasks → analyze → implement →
  codexreview → ship → close`; the sidecar gates stage order per feature.
- `/bk-marathon` is **advisory and passive**: a durable, resumable run with a discharge-item
  gate ladder per feature. It never invokes a pipeline command. Its state lives in an
  out-of-repo machine catalog and survives repo deletion and reboot.
- `/bk-scheduler` is CRDT-native CPM/PERT: actors self-report caps via `onboard`; cards carry
  work packages; `cycle` derives the plan; the critical path is **derived, never asserted**.
- **A SAFE RESTART (fresh session) is required before each of `/bk-specify`, `/bk-implement`
  and `/bk-codexreview`**, for every feature.
- Coop messages are **advisory only** — they coordinate; they never authorise a ship, merge or
  release (E6). A schedule is therefore not an authorisation.
- Cross-host writes CRDT-merge by guid; slot/slug collisions resolve first-claim-wins with the
  loser re-sequenced.
