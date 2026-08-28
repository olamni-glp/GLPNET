# SLICE P2 - the EXECUTION CONTRACT: marathon WIP era, bk-flow binding, and the run harness

What a host must have and must finish before it may start an allocated bundle.

---

## SOURCE 1 - the LIVE marathon run on this lane (ariellas / glpnet), read from durable rows

```
$ buildkit-marathon resume --feature glpnet-full-completion-programme
run mrun-f5ef56dba3c1 [open] feature=glpnet-full-completion-programme seq=302
next: start W11 [implement mini 7] Resolve origin/080-occurs-checked-substitution - only 2 conflicting paths - BLOCKED on the Udi section 1.14 language-authority ruling recorded as marathon discharge item J2 - PREREQ W10 (from item mitem-01a01f1d-c9b4-77af-b9c0-e81d0e47f57c)
steps: 40/111 complete; outstanding items: 146
```

The `next:` line above is KNOWN-WRONG as a work selector: it names W11, which is engineer-gated on
Udi's section-1.14 language-authority ruling. `next` does not model gating. The governing ranking is
the restart record reproduced as SOURCE 3.

---

## SOURCE 2 - the marathon CLI contract (engine-live)

```
$ buildkit-marathon --help
usage: buildkit-marathon [-h]
                         {open,resume,status,position,doctor,discharge,capture,expand,park,sequence,resolve,defer,backlog,step-start,checkpoint,trace,gate,discharge-item,override,takt,takt-target,version} ...

Durable, resumable run harness (advisory; spec-037).

positional arguments:
  {open,resume,status,position,doctor,discharge,capture,expand,park,sequence,resolve,defer,backlog,step-start,checkpoint,trace,gate,discharge-item,override,takt,takt-target,version}

options:
  -h, --help            show this help message and exit

$ buildkit-marathon open --help
usage: buildkit-marathon open [-h] [--feature FEATURE] [--home HOME] [--json]
                              [--title TITLE]

options:
  -h, --help         show this help message and exit
  --feature FEATURE
  --home HOME
  --json
  --title TITLE
```

---

## SOURCE 3 - the ariellas-lane restart record (rev2), verbatim

> <!--
> SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
> 
> SPDX-License-Identifier: MIT
> -->
> 
> # SAFE-RESTART PREP · rev2 — `mrun-f5ef56dba3c1` · ariellas / glpnet · 2026-08-24T22:35Z
> 
> **Resume phrase in the new session: `resume marathon`** — nothing else is needed.
> Supersedes rev1 (`RESTART-mrun-f5ef56dba3c1-ariellas-20260824.md`).
> 
> ---
> 
> ## 1 · Objective position
> 
> ```
> mrun-f5ef56dba3c1 · feature glpnet-full-completion-programme
> seq 297+ · steps 40/91 · outstanding items 144 · open (in_progress)
> roadmap: 20 epics · 120 features · 26 open · 94 closed   (reconciles 26+94=120)
> ```
> 
> 🔴 **`buildkit-marathon` MUST be given `--feature glpnet-full-completion-programme`.** The bare
> command resolves `.specify/feature.json` (→ `085-onrestart-fleet-resume`) and **falsely reports
> "no active marathon run"**. Single most likely trap for a fresh session.
> 
> ## 2 · Standard tooling — USE THESE, do not hand-render
> 
> | purpose | command |
> |---|---|
> | **full standardised report** (roadmap → progress → status → sitrep → takt → next) | `python scripts/BK-REPORT-v1-generator-20260823.py all --feature glpnet-full-completion-programme` |
> | not-closed roadmap table | `python scripts/roadmap_open_table.py` |
> | takt (also read from the TAKT DuckLake) | the `takt` section of BK-REPORT-v1 |
> 
> - Canonical formats: `docs/SITREP-FORMAT.md` · `.specify/STANDARD-SITREP-AND-ROADMAP-TABLE-v1.md` (buildkit repo)
> - Engineer questions: **BK-STD-2**, `I:\coop\BK-STD-2-ENGINEER-QUESTION-TEMPLATE.md`, adopted verbatim
>   (+ gavriella's A1 `cost: band|measured,n=k` and A2 `ORIGIN: superseded-by-evidence`)
> - **TAKT DuckLake**: `I:\coop\_takt-lake` — 809 records; **naive read now works** (no `union_by_name` needed)
> - CO lake (different store, do not confuse): `.specify/co-lake`
> 
> ## 3 · Delivered this session
> 
> | item | evidence |
> |---|---|
> | **RELEASE `v2026.08.24.1`** — develop 100 ahead → **1**; PR #226 merged, tagged, back-merged; 0 open PRs | Q01 |
> | **T16** — 2 C: scratchpad clones deleted, ~103 MB, preservation verified first | marathon checkpoint |
> | **T18** — `bk-flow` + `bk-proof` **installed** (premise corrected: install, not authoring); verified live | `88174d1b` |
> | **083 FR-002 RULED (b)** record-the-rejection → FR-009 **in scope**, B02 unblocked, B10 confirmed | `88174d1b` |
> | **Takt lake normalised** — 4 legacy JSON `reason` files (all mine) → VARCHAR; originals md5-preserved; naive fleet read fixed | `D:\BSTDEV\evidence\takt-lake-schema-normalise-20260824\` |
> | roadmap rounds 47 + 48; BK-REPORT-v1 + ruled table adopted | PR #228 |
> | 2 features raised: `takt-and-token-persistence-to-ducklake`, `renderers-read-export-fold-not-status` | roadmap |
> | BK-STD-2 questions Q01–Q04, all decided | `docs/questions/QID-glpnet-ariellas-Q01-Q04-20260824.md` |
> 
> **Open PR**: #228 (`091-bkstd1-round42` → develop) — round 48 + generators + questions.
> 
> ## 4 · 🔴 Two of my own claims were WRONG and are retracted — do not rebuild on them
> 
> 1. **"The ruled renderer has a `{promoted,specified,captured}` whitelist"** — **FALSE.** The renderer
>    is faithful (`line 91` skips only `closed`). **`buildkit-roadmap status` emits no row for
>    `implemented` features.** Mechanism in `_cmd_status` **NOT located**; `implemented` is a legal
>    state and the feature has a valid epic, so both obvious hypotheses are refuted. **Do not guess a
>    third.** Filed to buildkit lane.
> 2. **"The ariellas lake has ZERO takt rows"** — **FALSE, wrong store.** I measured `.specify/co-lake`.
>    The takt lake is `I:\coop\_takt-lake`. Tokens **are** recorded: **17,728,085 over 43/569 rows,
>    coverage 8%** — a coverage gap, not an absence.
> 
> Both retracted fleet-wide (`20260824T203000Z`, `20260824T223000Z`). A third correction: the 4
> divergent takt files were **mine**, not olamnit's.
> 
> ## 4a · 🆕 ZA-SERIES LANDED — the specified-features completion spine
> 
> **20 durable steps** added to this run (`91 → 111`), parent item
> `mitem-01a035f2-a1a3-778a-9e5f-9ae17bdfdf3e`. Plan:
> `docs/research/specified-completion-crdt-plan-ZA-series-ariellas-2026-08-24.md`.
> 
> **All six `specified` features already have code on `origin/develop`** — verified with
> `merge-base --is-ancestor`: `8a83bfc2` (083) · `fb038d11` (079) · `3037f155` (085) ·
> `78c056a4` (080). **The stall is in the record, not the work** — but `close` must NOT be reached by
> stamping the record: that code never passed `/bk-codexreview`, which is the exact class feature 078
> exists to eliminate. Every ZA spine routes through `codexreview` before `ship`.
> 
> 🔴 **COORDINATION**: gavriella's `mrun-20d9230f767b` holds Z00–Z08 for the **same six**.
> Proposed split broadcast (`20260824T231000Z`): **ariellas takes 083 + 079** (no gates);
> **gavriella keeps 080/082/085/065** (their Z-series carries those gates).
> **Until ACKed, this lane starts ONLY ZA00/ZA01/ZA08 and touches none of the gated four.**
> 
> Four gates owed: **G080** (Udi §1.14 — `UnifyFail` vs `CompileError`) · **G085** (homing) ·
> **G082** (fold + **no `feature_pipeline` row**) · **G065** (G2/FR-008).
> 
> ## 5 · WHAT'S NEXT — ranked, blockers named
> 
> | rank | step | size | state | blocked-by |
> |---:|---|---|---|---|
> | 0 | **ZA18** broadcast lane split | nano 1 | ✅ **DONE** | — |
> | 1 | **ZA00** reconcile the record for all six | micro 3 | ✅ **UNBLOCKED** | — |
> | 1 | **ZA01 / B02** — 083 `/bk-plan` | midi 11 | ✅ **UNBLOCKED — START HERE** | — (FR-002 ruled) |
> | 1 | **ZA08** — 079 record the skipped clarify | nano 1 | ✅ **UNBLOCKED** | — |
> | 2 | B03–B08 — 083 tasks→analyze→implement→codexreview→ship→close | mixed | follows B02 | B02 |
> | 3 | B10 — report the book-§4.3.1 guard finding to Udi | nano 1 | ✅ unblocked | — |
> | 4 | Merge PR #228 | nano 1 | ✅ unblocked | — |
> | 5 | T19 — ERA tag in marathon | midi 11 | held | PREREQ T11 |
> | 6 | T20 — link 14 spec dirs | mini 7 | held | `link` CLI defect (buildkit lane) |
> | 7 | W11 — resolve 080 | mini 7 | gated | Udi §1.14, discharge item J2 |
> | 8 | W18 — Gleam cluster | mini 7 | gated | two contradictory recorded reads |
> 
> **B02 is the next action.** The marathon's own `next:` still points at **W11, which is
> engineer-gated** — `next` ignores gating, so this table governs (trap #1 in `docs/SITREP-FORMAT.md`).
> 
> ## 6 · 🔴 Standing hazards
> 
> 1. **Three lanes are live in glpnet** (ariellas, gavriella, olamnit). We collided on roadmap round 47.
>    Check `origin/develop` and the coop root before any shared-resource write.
> 2. **"STUCK lock" is a FALSE POSITIVE.** Verify liveness with PowerShell `Get-Process` sampling CPU
>    twice. Git-Bash `ps -p` cannot see native Windows PIDs. **Never kill a holder.**
> 3. **Never parse `buildkit-roadmap status`** for counts — use the signed-export `heads` fold.
> 4. **Pipes mask failures**: `cmd | grep | tail` reports the *filter's* exit status. A silent success
>    is not a success — this bit me once today.
> 5. **This repo is NOT a registered deploy target** (`pin mirror absent` on every command). The stale
>    clone `D:\BSTDEV\glp\GLPNET` is the registered one. Unfixed — deploying would pin an engine
>    version mid-marathon and needs an engineer decision.
> 6. **3 dangling `spec_path` pointers**: `specs/067-qr-link-provisioning`, `specs/066-wave6-consolidation`,
>    and `guards-reference.md#comparison-guards` (a markdown anchor recorded as a spec dir).
> 7. **Never force-fetch tags** — `v2026.06.10.1` reports "would clobber existing tag".
> 8. **Do not normalise another host's takt records.** Check only your own `host=` partition.
> 
> ## 7 · Restart readiness
> 
> - [x] Release cut and landed; 0 open release PRs
> - [x] All work committed and pushed; PR #228 open for the remainder
> - [x] Findings durable as marathon items, not scrollback
> - [x] All four engineer rulings recorded in citable QIDs **and** the marathon
> - [x] COOP: ACK sweep, ERA re-broadcast, 2 retractions, fulfilment ACK — all on the live root
> - [x] Takt lake verified readable by the naive query
> - [x] Next action identified and unblocked (**B02**)
> 
> **READY FOR RESTART.**
> 
> — `ariellas` · `glpnet` · `mrun-f5ef56dba3c1` · `2026-08-24T22:35:00Z`

---

## SOURCE 4 - the bk-flow board->pipeline bridge contract (`.claude/skills/bk-flow/SKILL.md`, verbatim)

> ---
> name: "bk-flow"
> description: "The board→pipeline bridge (spec 031-bk-flow-bridge): the missing arrow between what the scheduler board says to work on and how the buildkit pipeline gets it done. Poll per-WP dispatchability with a per-packet reason (no packet is skipped silently), claim a packet into your own add-wins op log, open it — binding the WP to a feature, seeding a marathon run and switching the active-feature pointer — report it done through the authoritative board fold, and read per-phase takt against the run's own recorded target bands. Composes rather than re-implements: the CRDT substrate, the R12 add-wins board fold, capability fit, record-schema validation, the marathon run lifecycle and the active-feature pointer are all imported verbatim. Advisory, additive and single-writer: it appends only to the invoking actor's own op log, never renames/deletes/rewrites an existing stream, refuses loudly on a root that is not a board rather than reporting a plausible-looking empty one, and PRINTS the next pipeline command instead of invoking it — it is not a canonical pipeline stage and never auto-invokes a /bk-* command."
> argument-hint: "[a natural-language request, or a subcommand: poll|claim|open|report|takt|version]"
> compatibility: "Requires spec-kit project structure with .specify/ directory"
> metadata:
>   author: "buildkit"
>   source: "templates/commands/buildkit-flow.md"
> user-invocable: true
> disable-model-invocation: false
> ---
> 
> ## User Input
> 
> ```text
> $ARGUMENTS
> ```
> 
> You **MUST** consider the user input before proceeding (if not empty). It is either a
> natural-language request ("what can I work on?", "claim this packet", "start work on wp-…",
> "how are we doing on takt?") or a `bk-flow` subcommand. If empty, run `bk-flow poll` and
> summarise what is dispatchable, then ask what they want.
> 
> ## What this does
> 
> The scheduler board says **what** to work on; the buildkit pipeline (specify → plan → tasks →
> implement → ship → close) is **how** work gets done. Nothing joined them: a work packet could sit
> `ready` on the board forever while the pipeline sat idle, and a finished feature never reported
> `done` back to the board. `/bk-flow` **is that arrow and nothing else**.
> 
> **It consumes `/bk-marathon`; it does not replace it.** `bk-flow open` calls
> `marathon.run.open_run` directly, and the marathon run lifecycle is a declared dependency of the
> package. There is no migration *away from* marathon — you drive marathon *through* bk-flow.
> Anyone framing this as "migrate from marathon to bk-flow" has the direction wrong.
> 
> ## Surface
> 
> Every subcommand accepts `--root ROOT` (board root; default R1 `sched_root` / `coop/sched`),
> `--actor ACTOR` (or env `SCHEDULER_ACTOR`; the `HOST/lane` spelling is accepted and normalised on
> read), `--json` and `--quiet`. The three writing subcommands also accept `--dry-run` (compute
> everything, write nothing).
> 
> **Read the board**
> - `bk-flow poll` — every work packet with its state and a **per-packet reason**, plus how many are
>   dispatchable by you. When nothing is dispatchable it says so explicitly: the reasons above it are
>   complete and no packet was skipped silently.
> - `bk-flow version` — the bk-flow capability version.
> 
> **Take and start work**
> - `bk-flow claim <wp_id> [--dry-run]` — append one add-wins claim to **your own** log.
> - `bk-flow open <wp_id> --feature <feature-id> [--repo owner/name] [--dry-run]` — bind a claimed WP
>   to a feature, seed (or resolve) its marathon run, switch the active-feature pointer, and print
>   the pipeline command to run next. `--feature` is required the first time a WP is opened.
> 
> **Close the loop**
> - `bk-flow report <wp_id> [--repo owner/name] [--dry-run]` — append one transition `to_state=done`.
> 
> **Measure**
> - `bk-flow takt [wp_id] [--feature <feature-id>] [--repo owner/name]` — per-phase takt for this
>   feature's marathon run against its target bands. The feature resolves exactly as `open`/`report`
>   do: explicit `--feature` wins, else the wp link, else `.specify/feature.json`. Nothing is
>   recomputed here — the numbers come from marathon's own `takt.summarise` over the run's recorded
>   steps and its configured target bands, because a second implementation of the same statistic is
>   a second chance to disagree with the run's own record.
> 
> ## Reading a poll
> 
> `poll` answers two different questions and prints both, because they disagree in practice:
> 
> - **dispatchable** — is the board asking *you* to work this packet?
> - **resolvable (the binding gap)** — can the packet be *opened* at all, i.e. does its wp_id resolve
>   to a feature? A packet can be perfectly dispatchable and still unopenable; on this repo's board
>   20 of 20 wp_ids were unresolvable, including the 6 at `ready`. `poll` states the gap count and
>   names the unresolvable ids rather than letting you discover them one refusal at a time. It also
>   flags any conflict where the local link and the board envelope name **different** features.
> 
> `poll` additionally announces the host fold (so you can see that `olamnit-assistant` was treated as
> `olamnit` — and refute it if that is wrong) and warns when substrate lines are quarantined.
> 
> ## Guards that actually fire
> 
> Each of these refuses **before** anything is written, and says that nothing was written:
> 
> - **A board envelope naming a different repo.** Pass `--repo owner/name` to `open`, `report` or
>   `takt` and an envelope for another repo is REFUSED rather than acted on — a board is a
>   coordination channel shared across repos.
> - **A reserved substrate id.** The board fold drops reserved ids and prefixes before it dispatches
>   on op type, so a claim on one would report success and never appear on any board.
> - **An unclaimed or someone else's packet.** `open` refuses both, rather than starting pipeline work
>   nobody can see you own.
> - **A packet the board is not asking anyone to work.** `open` refuses a state outside the actionable
>   set even when you hold it yourself — otherwise a self-claimed packet that had since gone done,
>   bounded or escalated would still switch your active feature and seed a marathon run.
> - **A `done` the fold would discard.** `report` mirrors every refusal in `derive_board` — a gate
>   required but not passed, a declared deliverable without reachable evidence, a provisional packet,
>   an escalation-frozen one. Writing such an op would print success, move nothing, and latch the
>   packet `already_done` for the whole fleet against a grow-only substrate.
> - **An unmintable actor spelling**, and a `--root` that is not a board at all (exit 2), rather than
>   a plausible-looking report of zero.
> 
> Exit codes: **0** success · **1** refused · **2** the root is not a board. `--json` emits a
> `{"schema_version": …, "capability": "bk-flow", …}` envelope on both success and refusal.
> 
> ## Boundaries
> 
> Advisory only. bk-flow **never** invokes a pipeline command — it tells you which one to run, derived
> from which artifacts the feature already has (`spec.md` → `/bk-specify`, `plan.md` → `/bk-plan`,
> `tasks.md` → `/bk-tasks`, otherwise `/bk-implement`), so a resumed packet points at the stage it
> actually reached instead of restarting at specify. Run that command yourself.
> 
> It is **single-writer and additive**: it appends only to the invoking actor's own op log (canon
> R-1) and never renames, deletes or rewrites an existing stream. It is **not** a canonical pipeline
> stage and never auto-invokes a `/bk-*` command (spec-037 FR-014).
> 
> **Registry upkeep (spec-028 FR-004)**: run
> `python -m buildkit_cli.registry touch --tool buildkit-flow` from the project root. It marks the
> capability registry possibly-stale and **always exits 0** (fail-safe; never blocks this stage).
> Ignore its output.

---

## SOURCE 5 - the bk-flow CLI, live

```
$ bk-flow --help
usage: bk-flow [-h] [--version]
               {poll,claim,open,report,takt,lanes,version} ...

Board->pipeline bridge (advisory; part of buildkit). Never invokes a pipeline
command — it tells you which one to run.

positional arguments:
  {poll,claim,open,report,takt,lanes,version}
    poll                per-WP dispatchability with a reason (read-only)
    claim               append one add-wins claim to your own log
    open                bind a claimed WP to a feature + marathon run
    report              append one transition to_state=done
    takt                per-phase takt for this feature's marathon run against
                        its target bands (read-only)
    lanes               show the declared lane registry and how every board
                        actor relates to you (read-only)
    version             report the bk-flow capability version

options:
  -h, --help            show this help message and exit
  --version             show program's version number and exit
```

---

## SOURCE 6 - LIVE MEASUREMENT of what the board->pipeline bridge will actually accept (2026-08-25T06:50Z)

This is the decisive constraint on any bundle that is meant to reach a marathon via `bk-flow open`.

```
$ bk-flow poll --actor ariellas
board \\192.168.0.108\GAVRI_D\coop\glpnet\sched
actor ariellas (normalised ariellas; aliases ariellas, ariellas.hatzinor, ariellas.yngenios-windows)
32 work packets — 2 dispatchable by you
reasons: claimed_by_other=3, not_claimed=2, not_ready=25, ok=2
  ~ host fold: 'ariellas' resolves to host 'ariellas' (scheduler ruling 2, the same rule the allocator charges capacity with); also folded here: ariellas.hatzinor, ariellas.yngenios-windows
  ! capability_gate_inert: no work packet declares a required_capability, so the capability-fit ranking never executed — missing_capability=0 here means UNMEASURED, not clear. 65 capabilities are published by this actor were never compared against anything.
  067-qr-link-provisioning:codexreview-to-close-SHIP-TOKEN-GATED escalated    not_ready:escalated
  076-type-checker-body-atom-moding:implement-to-close done         not_ready:done
  trust-material-controlled-reproduction:ariellas-only-clean-control-host ready        OK dispatchable
  wave-2-consolidated-repl-engine-split-spine in-progress  OK dispatchable
  wave-4-consolidated-parallel-safe-fillers backlog      not_ready:backlog
  wave-5-consolidated-captured-triad     in-progress  claimed_by_other:gavriella
  wp-041-cross-runtime-and-two-host-acceptance-completion-t055-pa backlog      not_ready:backlog
  wp-atomic-toolchain-installs-venv-swap-post-install-smoke backlog      not_ready:backlog
  wp-buildkit-coordination-optimisation-gepa-dspy-coop-scheduler- backlog      not_ready:backlog
  wp-coordination-feature-stream-durable-superset-fix ready        claimed_by_other:olamnit
  wp-crdtmsg-post-mvp-completion-cose-sign1-wrapper-1-14-gated-gl backlog      not_ready:backlog
  wp-distributed-unification-quiescence-protocol-two-runtime-spec backlog      not_ready:backlog
  wp-durable-listener-service-box        backlog      not_ready:backlog
  wp-front-end-goal-term-acceptance-completeness-parser-repl-goal backlog      not_ready:backlog
  wp-full-scope-gleam-glp-implementation backlog      not_ready:backlog
  wp-glptutorial-corpus-golden-reconciliation-stale-goldens-drift in-progress  not_claimed
  wp-guarded-term-traversal-utilities-cycle-tolerant-compiler-wal backlog      not_ready:backlog
  wp-madglp-writer-reader-address-discipline-closure-n-n-1-audit- backlog      not_ready:backlog
  wp-multi-host-state-discipline-reversible-states-untracked-deri backlog      not_ready:backlog
  wp-occurs-checked-substitution-pipeline-compiler-bind-time-occu ready        not_claimed
  wp-per-host-toolchain-and-environment-contract-declared-machine backlog      not_ready:backlog
  wp-product-defect-burn-down-with-regression-proof-no-defect-clo backlog      not_ready:backlog
  wp-qr-link-provisioning                backlog      not_ready:backlog
  wp-sc-002-il-parity-bridge-antlr-parse-tree-engine-ast-lowering backlog      not_ready:backlog
  wp-seam-specification-normative-contracts-at-every-trust-lifecy backlog      not_ready:backlog
  wp-single-source-of-truth-one-authority-per-subject-provenance- backlog      not_ready:backlog
  wp-type-checker-body-atom-moding-accept-head-flipped-readers-un backlog      not_ready:backlog
  wp-verification-receipts-and-loud-failure-no-check-may-pass-wit in-progress  claimed_by_other:gavriella
  wp-wave6-consolidation                 backlog      not_ready:backlog
  wp-ynet-consolidation                  backlog      not_ready:backlog
  wp-ynet-human-memorable-decentralized-naming-resolver backlog      not_ready:backlog
  wp-ynet-mobile-background-battery-budget-scheduling-policy backlog      not_ready:backlog
binding: 1 of 32 packet(s) resolve to a feature; 31 cannot. 1 envelope(s) on the board. This count is repo-UNSCOPED — no repo was stated, so an envelope naming ANOTHER repo is counted as resolvable here and would be acted on without that check. Pass --repo <owner/name> for a count you can act on.
  unresolvable: 067-qr-link-provisioning:codexreview-to-close-SHIP-TOKEN-GATED, 076-type-checker-body-atom-moding:implement-to-close, trust-material-controlled-reproduction:ariellas-only-clean-control-host, wave-2-consolidated-repl-engine-split-spine, wave-4-consolidated-parallel-safe-fillers, wave-5-consolidated-captured-triad, wp-041-cross-runtime-and-two-host-acceptance-completion-t055-pa, wp-atomic-toolchain-installs-venv-swap-post-install-smoke (+23 more)
```

### Three measured facts a bundle plan must survive

1. **The binding gap: 1 of 32 packets resolves to a feature; 31 cannot.** `bk-flow open` binds a
   claimed packet to a feature and a marathon run. For 31 of 32 packets that bind is impossible today,
   so those packets CANNOT be shipped into any marathon through the bridge, on any host, regardless of
   who they are allocated to. The count is also repo-UNSCOPED: no `--repo` was passed, so an envelope
   naming another repo would be counted resolvable here.
2. **The capability gate is INERT.** `capability_gate_inert: no work packet declares a
   required_capability, so the capability-fit ranking never executed - missing_capability=0 here means
   UNMEASURED, not clear. 65 capabilities published by this actor were never compared against anything.`
   The board therefore CANNOT currently verify that any packet is runnable on any host: there is no
   declared requirement on any packet to check a host against.
3. **Readiness starvation.** Of 32 packets: 25 are `not_ready:backlog`, 3 `claimed_by_other`,
   2 `not_claimed`, and exactly **2 are dispatchable** to this actor. A bundle drawn from `ready`
   packets alone cannot be four-way equal - there are only 3 packets in `ready` on the whole board.
