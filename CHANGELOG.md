## [Unreleased]

## [v2026.08.27.4] - 2026-08-27

### Changed
- Merge branch 'develop' of https://github.com/olamni-glp/GLPNET into develop
- session-9 cycle-3 close - v2026.08.27.3 released; I REFUTED my own import-emptiness discriminator (non-empty import still lossless); engine version is the unifying axis behind 3 pin-dependent defects; BK-STD-1 fix robust across catalog growth and a new state value
- Merge pull request #238 from olamni-glp/096-host-interconnectivity-hardening-evidence
- round 56 - import 35 new lines from 6 files (non-empty), reconcile, dedupe 0 groups over 120 live, export 21 epics/121 features/3890 journal lines; import-refused ledger measured LOSSLESS on a NON-EMPTY import, refuting my own discriminator hypothesis
- rev4 - P0 coop dead-drop rootcause + fix persisted, PR 235 merged, 4 engineer rulings, next=ZA01 bk-plan on 083
- Merge pull request #237 from olamni-glp/main

## [v2026.08.27.3] - 2026-08-27

### Changed
- Merge pull request #235 from olamni-glp/096-host-interconnectivity-hardening-evidence
- rounds 51-52 - 21 epics/121 features/3871 lines; round 52 fixes the coop mirror to the shared volume
- session-9 cycle-2 close - v2026.08.27.2 released, Q-GLPNETS8-04 and WinError 4551 confirmed fixed live, counter-measurement filed (import-refused rewrite is lossless here so the false-green is conditional not unconditional)
- round 55 - sync/reconcile/dedupe/export after v2026.08.27.2
- Merge pull request #234 from olamni-glp/main
- rev3 corrected - 091 branch was merged and deleted; 9 commits moved to 096 branch, unpushed (HTTP 408 + classifier)
- rev3 reboot prep - SHIRAS rootcause, HIH feature promoted in buildkit, 7 self-retractions, push and checkpoint BLOCKED by classifier
- host-interconnectivity CRDT dossier + 3 codify notes; feature promoted in buildkit, glpnet copy rejected as duplicate
- shiras durable-fix synthesis - NO component is durable, all are optional; only the lane-registry sweep is buildable from existing verbs
- corpus addenda - platform declaration, six dead detectors, divergent roots, provenance/identity/version constraints
- shiras root-cause + fix corpus - 3 disjoint slices from first-party SSH measurement
- per-host WP bundle claim sheets - SHIRAS 6 gaps, OLAMNIT 1, GAVRI 1; C1 discharged for ARIELLAS by first-party measurement
- four-host bundle claim instructions - prerequisite bundles per host, lane mapping resolved, ACK required
- four-host WP bundle partition - ZERO packets verify runnable on any host; blockers are unmeasured hosts, inert capability gate, 31-of-32 binding gap
- host-bundle allocation corpus - 6 rotated disjoint slices + manifest for the four-host WP partition

## [v2026.08.27.2] - 2026-08-27

### Changed
- import-refused ledger rewrite after round 54 - measured LOSSLESS, 7011 entries / 561 guids unchanged, 0 guids dropped (refutes the false-green rewrite concern for this lane)
- session-9 CLOSE - all 4 rulings executed, v2026.08.27.1 released (71 commits); 3 corrections (BK-STD-1 cause is upstream status not the filter, 2026.8.26.2 not installed so pinned 2026.08.26.1, takt verdict was effort read as ELAPSED and is OVER not in-band); Q-GLPNETS8-04 discharged - STUCK-lock now probes liveness
- round 54 post-release - sync/reconcile/dedupe/export after v2026.08.27.1
- Merge pull request #232 from olamni-glp/main

## [v2026.08.27.1] - 2026-08-27

### Added
- promote the feature (WSJF 3.0 / RICE 1755) + two multi-contributor CRDT docs - 6 root causes and 9 FRs with 6 SCs, add-wins union by owner-namespaced id, disagree-by-supersession, every FR traces to an RC
- shiras/glpnet onboard - adopt bkquestion byte-identical, record 4 engineer rulings, scheduler three-stage leak root-cause
- surface provisioning event lines in supervisor, fold T026 loopback friction into quickstart+cli-contract
- --derived-dir client plumbing, ERR token exit map, session PROVISION_REDEEMED consumer (T014/T020)
- C# acceptance seam - DerivedCredentialValidator, extended pin callback, replay refusal, PROVISION_REDEEMED (T012/T013/T019/T020)

### Fixed
- backfill not-closed features that roadmap status omits - table now reports 26 and reconciles with BK-REPORT-v1 section 1; the dropped row was the implemented feature 067, and the backfill binds to THIS lane's export (a bare glob picked olamnit's 4-day-stale one)
- reject non-string path components and run_ids instead of coercing - str(0) would write under a '0' dir while keeping int 0 in the receipt, bypassing the run binding and reopening prior-run PASS reuse; 14/15 mutants killed
- close the 4 HIGHs from re-review 20260826T084941Z - run_id mandatory on receipt-backed verdicts, EMPTY requires zero counts on the loaded path, negative counts rejected, receipt paths confined to the root; 13/14 mutants killed (path-confinement backstop marked NOT test-covered)
- close the 6 product HIGHs + 2 MEDs - receipt run_id identity binding, FR-010 examined+skipped reconciliation, an earned PASS branch, expected-set refusal, sidecar-loading run reconciliation, reason-scoped overrides; 9/9 mutants killed
- the two TEST findings - conformance coverage is case-keyed (BOUNDED + OVERRIDDEN now actually run) and the SC-007 mutation test now goes RED under a no-op validator

### Changed
- decisions(glpnet): record 4 engineer rulings Q-GLPNETS9-01..04 - waive and release, leave 083 on branch, re-pin to 2026.8.26.2, fix BK-STD-1 here and broadcast
- questions(glpnet): Q-GLPNETS9 set - release gate after round-3 HIGH, 083 unmerged spec artifacts, WinError 4551 vs the engine pin, BK-STD-1 table dropping the implemented row; validated against bkquestion schema+rules
- session-9 addendum - 078 mutation survivor CLOSED 15/15 honest, roadmap round 53, COOP ACK sweep x5 with measured mandatory discharge, Q-GLPNETS8-02 discharged; 4 traps measured incl wrong-cwd 12 phantom failures and WinError 4551 blocking the pinned engine; release STILL held
- round 53 - sync import 0 new lines over 2 files, reconcile 15/21 pipeline ids bound (6 unbound), dedupe 0 groups over 119 live, export 20 epics/120 features/3855 journal lines, both publish legs OK
- cover the _confine containment backstop at its own boundary - closes the published 1/15 mutation survivor; both mutants killed, 15/15 honest
- session-8 addendum - 3 review rounds, 19 findings closed, RELEASE STILL NO-GO because the ruling's ship condition (only MEDIUM/LOW) was not met; every round found a defect INSIDE the previous round's fix; 078 takt coverage is 100pct not the fleet's 6pct; STUCK-lock diagnostic false a 7th time
- round 52 - reconcile in-sync, dedupe 0 groups over 119 live, export 20 epics/120 features/3854 journal lines
- session-7 addendum - all 10 codexreview findings closed with 9/9 mutants killed; run_id and the examined+skipped rule were ALREADY in data-model.md, not extensions; 9/9 packets claimed; two findings on the claim instructions themselves
- session-6 addendum - HOST-INTERCONNECTIVITY-HARDENING is in BUILDKIT not glpnet; CRDT store is merge-on-read (8 FRs + 8 RCs contributed); codex caught a false corroboration in my own record, withdrawn as rev2; allocation landed 22 WPs/63pts; 3 new tooling defects
- round 51 - reconcile in-sync, dedupe 0 groups over 119 live, export published, barrier 4/4; glpnet duplicate of host-interconnectivity-hardening withdrawn
- withdraw the glpnet host-interconnectivity-hardening duplicate - the feature belongs in buildkit (engineer ruling); this lane's 8 RCs + 8 FRs contributed to the fleet CRDT store instead, and the standalone .crdt.md pair is superseded by the merge-on-read store
- decisions(glpnet): record 6 engineer rulings from session 6 - release hold, remediation-is-the-era, six-vs-eight report standards, 083 mechanism split, renderer fix, buildkit link verb
- gate CONFIRMED on baseline after Debug rebuild - 561/559/2/0 unsearchable 0, zero regression over 5 merges; exit 1 (2 known 064 drills) vs exit 2 (a group did not run) are different verdicts
- session-6 close - THE FEATURE SUPPLY OPENED (22 WPs/63pts allocated 09:32Z, 3 engineer asks are already packets); 6 rulings recorded; SELF-CORRECTION dropped-implemented is a roadmap-status defect not a renderer defect; shiras onboarded 1h47m after the freeze that blocks it; olamnit's board copy folds empty
- decisions(glpnet): raise 6 engineer questions in 2 validated bkquestion sets - release gate, allocation start condition, six-vs-eight section report standard, 083 scope after 3 new gates, hidden implemented row, roadmap link verb
- round 50 - import 4 files/13 lines, reconcile in-sync (6 pipeline ids unbound), dedupe 0 groups over 119 live, export 120 features, both publish legs OK, barrier 4/4
- merge(067): qr-link-provisioning - the 5 commits unique to 067 not carried by 067b (engineer ruling Q-GLPNETS2-01)
- merge(067b): qr-link-provisioning continuation - C# acceptance seam, DerivedCredentialValidator, replay refusal, PROVISION_REDEEMED, --derived-dir plumbing, T012-T027 implement stage complete (engineer ruling Q-GLPNETS2-01 merge both then specify)
- merge(tidyup-olamnit): X03 workplan ledger reconcile, bk-flow + bk-proof SKILL.md, BK-REPORT-v1 at .specify/standards/bk_report_v1.py
- merge(091): ariellas round 48 sync, ZA-series 20-step CRDT plan, BK-REPORT-v1 generator, BK-STD-2 engineer questions Q01-Q04, restart rev2
- merge(095): shiras/glpnet onboard - bkquestion tooling (TEMPLATE-question-set.json + schema + CLI), 7 engineer rulings, scheduler three-stage leak root-cause, host-wide deploy-home registry contention finding
- host-wide deploy-home registry contention - the lock error names the wrong repo, cost 11 captures
- specified-feature audit (2 state contradictions, 067 stranded off trunk), clean worktree survey, bk-flow migration is next session's first task
- correct the PR blocker - gh default repo was the sibling GLP repo, not the rate limit; PR #230 open
- session-1 close for mrun-f77f62158255 - shiras/glpnet onboard, three-stage scheduler leak root-caused, 7 rulings recorded
- X03 reconcile workplan ledger — T02-T12/H01-H03 all DONE (T03-T06 unblocked via scoped bypass)
- install bk-flow + bk-proof SKILL.md (M01 Phase A; bk-flow hand-installed from buildkit git template due to packaging gap in 2026.8.23.1)
- rev2 updated - ZA-series 20 steps landed, lane-split proposed, ZA00/ZA01/ZA08 are the unblocked start
- specified-features completion CRDT plan - 20 durable steps in mrun-f5ef56dba3c1; Z-series independently confirmed cell-by-cell; all 4 merge commits verified on develop; lane-split proposed
- adopt fleet BK-REPORT-v1 generator (canonical 6-section sitrep order + takt-ducklake retrieval) at .specify/standards/bk_report_v1.py
- rev2 safe-restart prep - release v2026.08.24.1 cut, 4 rulings recorded, 3 self-corrections logged
- Merge pull request #227 from olamni-glp/main
- round 48 sync + adopt BK-REPORT-v1 generator + BK-STD-2 engineer questions Q01-Q04
- supersede the INERT gitignore warning — glpquick material rotated
- roadmap: sync round 153835Z - import 0-delta (190 untagged refused), reconcile clean, 0 dups, 105 live/17 open, export published, replay-verify OK
- roadmap: RCA clustering landed - 6 features from ~300 deduplicated fleet defects (3rtask run 20260811T113723Z-1f7c), 5 ordering edges, 6 backlog issues legitimated into F4; 105 live / 17 open
- roadmap: import-manifest + refused-log after ariellas 111Z export (0-delta, untagged refusals unchanged)
- mark T026/T027 done with recorded suite counts - implement stage complete
- DerivedCredentialTests matrix + RedemptionTracker + python lifecycle tests (T015/T021)
- T002 verify half complete - cert dirs untracked post-069, .gitignore warning resolved
- roadmap: US2 dispositions advanced - atomic-toolchain-installs + batch-advance-calver -> shipped (buildkit PRs #299/#300 MERGED by engineer, commits verified in develop); sync round, export published, replay-verify OK
- untrack glpquick-cert/ on this branch (matches develop/main since v2026.08.10.1) - the .gitignore rule was inert while the files were tracked; 3rd-gen material must never be committable
- roadmap: sync round - import 0-delta (190 untagged refused, 5th consecutive no-op round), reconcile clean, 0 dups, 99 live/11 open, export published, replay-verify OK
- roadmap: sync round 163006Z - import 0-delta (190 untagged still refused), reconcile clean, 0 dups, 99 live/11 open, export published, replay-verify OK
- roadmap: sync round 000109Z - import 0-delta (190 more untagged refused), reconcile clean, 0 dups, 99 live/11 open, export published, replay-verify OK
- roadmap: sync round 235430Z - import 5 files/0-delta (954 untagged entities REFUSED by policy - peers on newer project-scope tagging), reconcile clean, 0 dups, 99 live/11 open, export published, replay-verify OK
- impl(067): US3 decode vectors published (T022/T023) - 6 GQP1 vectors + 10 conformance tests; retracts the wrong deferral (payload contract is producer-consumer, independent of the C# seam)
- roadmap: import-manifest after ariellas 075700Z import (0-delta, converged)
- roadmap: sync after both peer ships - 99 live / 11 open (ariellas gap-closure closed by their v2026.08.04.1), export published, replay-verify OK
- impl(067): Python producer complete - derived-cert minting, GQP1 payload codec, encrypted envelope, session lifecycle, display-only QR, non-secret PDF, append-only audit/revocation, join intake, provision CLI; 39 tests green
- roadmap: sync round 164321Z - import 0-delta (50 files), reconcile clean, 0 dups (99 live/12 open), export 18/99/2740 published, replay-verify OK
- roadmap: import ariellas 163034Z (3 lines) + re-export/publish, replay-verify OK
- roadmap: sync round 162541Z - import 0-delta (45 files), reconcile clean, 0 dups (99 live), export 18/99/2737 published, replay-verify OK
- roadmap: sync round 2 - imported ariellas 153531Z + olamnit (79 lines), 99 live / 12 not-closed, 0 dups, export published, replay-verify OK
- roadmap: sync round 153055Z - import 0-delta, reconcile clean, 0 dups (99 live), export 18/99/2658 published, replay-verify OK
- analyze(067): 0 critical - remediate R-003 keypair-mint wording, plan token hygiene, SC-001 timing fold into T026
- tasks(067): 27 tasks across setup/foundational/US1-US4/polish - US1 MVP one-scan onboarding
- plan(067): qr-link-provisioning design - Python glp_quick producer + C# join-seam derived-cert acceptance, GQP1 payload contract, append-only lifecycle stores
- roadmap: sync round import-manifest update
- roadmap: sync round - import 0-delta, reconcile clean, 0 dups (99 live, 067 specified), export 18/99/2658 published, replay-verify OK
- spec(067): clarify session - 5 delegated defaults (trunk-signed per-device certs, join-seam revocation, device fingerprint binding, TTL/session/enforcement defaults, PDF P4 in scope)
- spec(067): qr-link-provisioning specification - one-scan provisioning with mandatory security posture (derived credentials, encrypted QR, audit+revocation)

## [v2026.08.24.1] - 2026-08-24

### Added
- install the two SHIPPED fleet report generators; withdraw my forked one
- emit the fleet STANDARD REPORTS R-1 roadmap, R-2 sitrep, R-3 tact in the exact ruled shapes
- record FR-002 ruling (b) record-the-rejection, FR-009 in scope; install bk-flow and bk-proof skills from buildkit templates
- adopt the fleet standardised not-closed roadmap table in glpnet
- MVP verification-receipts mechanism (US1+US2+US3) - codeconv.receipts package (outcome/receipt/consumer/manifest/override/bind/paths), reference check + conformance fixture, 13 fault-injection test files (29 tests green); adoption manifest checked in (reference adopted, 4 glpnet areas non-adopted)
- T031a: extend olamnit DSDV into the NAT-piercing internet overlay
- P6 US4: real relay forward - circuit-relay-v2 + Tor-cell, ciphertext-only (T028-T033)
- US2 NAT hole-punch + S-Kademlia DHT foundation (T017-T022, P3)
- first-class capability exposure + resolver seam (T015; T014/T016 complete)
- real MsQuic wire (QuicWireChannel) + authenticated handshake (T011)
- real US1 link session — handshake + ECDH-sealed send/receive (T014/T016)
- Ed25519-primary node identity + P-256 fallback (DEC-CRYPTO-1)
- implement — real+tested native crypto/policy core (29/29 green)

### Fixed
- T031a: bind route ingest to the authenticated peer (route spoofing)
- T031a: codex review cycle 2 - separate link KIND from link STATE
- T031a: three codex-review findings in the DSDV internet extension
- codexreview — fix 6 real bugs from adversarial review (34/34 green)

### Changed
- session-5 rev2 - 12 engineer rulings recorded and citable; Z-series landed (14 steps, mitem-01a03540); engine version skew requires --engine-override ambient for takt verbs; codexreview run records 0s duration and no tokens
- round 49 - import 0 new lines from 2 files, reconcile in-sync, dedupe 0 groups over 118 live, export 20/119/3823, both publish legs OK
- record 12 engineer rulings via bkquestion-v0 - 8 rulings, 2 tie-breaks, 2 risk-acceptances with expiries
- specified-features completion CRDT plan - ALL SIX already have code on develop, so the stall is in the record not the work; 083 is unblocked and ready for /bk-plan; 4 gates owed (Udi 1.14 on 080, homing on 085/082, G2 on 065)
- record the session-5 capture gap - 7 marathon rows did not land; registry lock held ~50min by concurrent pytest runs; kinds corrected and retry driver checked in
- STUCK-lock verdict now false 6 times across two independent live holders - it means busy and nothing more; Get-CimInstance names the holder in one step
- record the verified merge gate - 561/559/2/0 identical to baseline, zero regression across both merges
- session-5 close - codexreview UNBLOCKED and root-caused, review ran NO-GO with 8 HIGH on 078 itself; SCHED-R4 discharged; 2 branches merged; 10 engineer blocks published
- codexreview UNBLOCKED - root-caused to a git pathspec quirk, not a buildkit defect; the review then ran and returned NO-GO with 8 HIGH findings on the receipts module itself
- round 48 - import 0 new lines from 8 files, reconcile in-sync, dedupe 0 groups over 118 live, export 20/119/3823, both publish legs OK
- merge(olamnit): ERA definition + BK-STD-1 sitrep/takt/snapshot-guard generators + 3 exports; both add/add conflicts resolved to develop (owners.json theirs was empty; open-table theirs is a second renderer - fork flagged, not silently picked)
- merge(091): bkstd1 round42 - ariellas lane roadmap round 47, bk-flow + bk-proof skills, restart prep (clean probe)
- publish the 20260824T145416Z export - the dependency source for SCHED-R4 edge stocking
- publish exports from the standard-reports read path
- session-4 close - Y05/Y08/Y10/Y11/Y12/Y13 done, origin at 14 heads, 0 open PRs; adds the lake double-count rule
- sync round 50 - import applied, reconcile in-sync, dedupe 0 groups across 118 live
- Merge pull request #225 from olamni-glp/tidy-y08-merge-051
- merge(TIDY-Y08): 051-ynet-transport onto develop - engineer-ruled Q3 triage outcome
- session-3 close - the release gate is a codexreview tool defect, not missing work
- Merge pull request #224 from olamni-glp/078-codexreview
- two defects that together block every release on this host
- session-2 close - 078 MVP recovered and green on develop, codexreview is now the single gate to release
- sync round 49 - import applied, reconcile in-sync, dedupe 0 groups across 118 live
- Merge pull request #223 from olamni-glp/q1-merge-078-archived-mvp
- safe-restart prep for mrun-f5ef56dba3c1, ariellas lane, GLPNET
- merge(078): recover the archived 078 MVP implementation onto develop - engineer-ruled Q1
- record the takt DuckLake fleet-root defect that hid this host from every fleet takt query
- round 47 sync + adopt ruled open-table renderer (drops implemented state - defect filed)
- safe-restart prep for mrun-20d9230f767b, gavriella lane, GLPNET
- sync rounds 47 and 48 - import applied both legs, reconcile in-sync, dedupe 0 groups across 118 live
- Merge pull request #221 from olamni-glp/091-bkstd1-round42
- Merge pull request #222 from olamni-glp/tidy-y05-merge-066
- export 20/116/3762 (23:25 refresh) - reconcile in-sync, import 0-new-lines, dedupe 0 groups/115 live
- SCHED-R6 and SCHED-R7 measured on the glpnet board root
- merge(TIDY-Y05): 066-wave6-consolidation spec dir onto develop - 4 conflicts resolved (3 keep-deleted: feature.json/import-manifest/abandon.cs; CLAUDE.md BUILDKIT block keeps active 078), net +13 spec files 1213 insertions 0 deletions
- sync round 46 - reconcile in-sync, dedupe 0 groups (118 live), export 20/119/3821 both legs
- sync round 42 - reconcile in-sync, 0 dup groups, export 20/118/3806 both legs
- sync round 45 - reconcile in-sync, dedupe 0 groups (118 live), export 20/119/3821 both legs
- Merge pull request #220 from olamni-glp/main
- adopt fleet-standard BK-STD-1 tooling (roadmap_open_table + marathon_sitrep + takt_report + snapshot_guard) from qhstate d1f64b4; re-pin glpnet engine 2026.8.23.1
- export 20/116/3762 (17:01 refresh) - full-identity not-closed feature render
- adopt VERBATIM authoritative engineer ruling (ERA=FEATURE, nine-stage span, no lossy compression, era-verb is buildkit-lane surface) - supersedes paraphrase; fleet-aligned crucible 697ba70+773396c
- canonical ERA==FEATURE definition (engineer ruling 2026-08-23) - full /bk-specify->/bk-close arc; lossy feature compression forbidden; fleet-aligned w/ crucible 697ba70
- export 20/116/3762 (2026-08-23T16:52 refresh for not-closed table)
- sync 2026-08-23 - import 1 new, reconcile, dedupe 0 groups (115 live), export 20/116/3762; olamnit scheduler onboard 35d 3x8h + directive items D01-D05 captured
- safe-restart handover + pre-existing-failures filing - MVP implemented+green (29/29), next=codexreview; 18 pre-existing codeconv failures filed separately (engineer-ruled)
- tasks(078): 42 tasks by user story - MVP=Phases 1-5 (setup+foundational+US1+US2+US3 on reference check)=first SHIP-TOKEN increment; US4 retrofit of 6 areas + SC-corpus post-MVP incremental
- plan(078): verification-receipts plan+research+data-model+contracts+quickstart - MVP mechanism first (US1-3 on reference check), sidecar JSON receipts, buildkit-owned contract bound by version, 5-value outcome non-collapse, per-repo manifest + per-run expected-set; all gates PASS
- clarify(078): ratify 6 provisional decisions (blocks 24-29) - engineer individually ruled all six on adopted option, none overturned; provisional caveat superseded
- impl(066): US2 LANDED - ITEM-01/ITEM-02 disposed, both PRs merged by engineer and feature commits verified in develop; advanced to shipped (not closed - no buildkit release yet, and closed is the one-way door)
- impl(066): ITEM-03 work chain COMPLETE - olamnit shipped Dart/codeconv half as v2026.08.05.1 (verified); .3 moot by day-roll; live confirmation of the import-manifest friction
- impl(066): ITEM-03 dispute WITHDRAWN (olamnit shipped 065 as v2026.08.04.2, verified); ship-round table - both peers shipped, ours alone uncut on the systemic engineer gate; next free CalVer .3
- impl(066): ITEM-06 RESOLVED (1.14 approval 20260804 Gabi+Udi postdates G5 rider - supersession stale, revived under olamnit/065); fleet finding: closed is a one-way door in the roadmap CLI; CalVer .2 ACKed uncontested
- impl(066): EXT.ariellas SATISFIED - 064 shipped v2026.08.04.1 + closed, tag/back-merge independently verified; ITEM-04 disposed by owner; T023 gate arithmetic recorded (still G1+R6-R12 gated, brief was one gate short)
- impl(066): peer-round record 2026-08-04 - ITEM-03 DISPUTED + ITEM-06 CONTRADICTED (antlr4 1.14 approval postdates rider), EXT.olamnit satisfied, EXT.ariellas reclassified engineer-blocked, ITEM-02 corrected to 2 baseline failures
- roadmap: sync round - import/reconcile clean, 0 dups, export published, replay-verify OK
- impl(066): record ariellas 003500Z peer escalations (bridge auth asymmetry a-d) in evidence inventory; their v2026.08.04.1 claim ACKed, our 064 takes next free
- impl(066): US2 both items implemented on pushed buildkit branches (634d4a0a, 554836f6), dispositions gated on engineer landing; T006/T008 ticked, T007/T009 landing-gated
- impl(066): ITEM-05 qr-link-provisioning graduation proposal - mandatory security posture + cross-repo consumer => own feature pipeline; T013 superseded, T014 engineer-gated
- roadmap: sync round post runtime-consol close - export published, replay-verify OK
- impl(066): US3 glp-runtime-consol closed - abandon.cs dead stub tombstoned (error-level Obsolete, 062 US5 rationale, 0 live call sites), antlr4 sub-scope superseded per rider; inventory w/ 1.14 screen; T010-T012 ticked
- roadmap: sync round - import/reconcile clean, 0 dups, export published, replay-verify OK
- impl(066): US2 parked w/ evidence (R5 ruling gate + buildkit-repo foreign WIP), T024 triage proposal packaged (defer-on-this-host, ariellas-led), OPS gate row added
- roadmap: sync round - import 214119Z (0-delta), reconcile clean, 0 duplicates, export published, replay-verify OK
- roadmap: import ariellas 214119Z export, reconcile
- roadmap: US1-close sync round export published (FR-007 per-close discipline)
- impl(066): US1 gate ledger complete - 18-row mapping w/ invariants verified, rulings R1-R12 inventory, drift record (ariellas Option-B rider: 6 gleam rows closed + antlr4 superseded, implement receipts 459be1b2), T001-T005 ticked
- analyze(066): 0-critical, 6 findings, top-5 remediations applied - T010 language-authority screen (IV-a), FR-007 per-close sync discipline, FR-010 marathon checkpoint line, T013 loopback acceptance, group-label normalize
- tasks(066): 30 tasks across US1-US6 + polish - ledger MVP, parallel lanes for quick-wins/singletons/spike, gate-parked Gleam chain (spike-go + ariellas receipts + G1), triage packaging, wave-close sweep
- roadmap: import ariellas 205616Z export, reconcile
- plan(066): wave-6 orchestration plan - constitution PASS, D1-D7 research decisions, gate-ledger + disposition-protocol contracts, ledger-driven data model, resume quickstart
- spec(066): wave-6 roadmap consolidation - 18-item not-closed snapshot 150440Z, gates G1-064-ship/G2-065/G3-rulings + ariellas gap-closure carve-out, stories P1-P6 (ledger, quick wins, singletons, ANTLR4 spike, Full-Gleam chain consuming peer receipts, captured triage)
- register host Olamnit as trusted roadmap-export signer (engineer ruling 2026-07-16, co sol#103)
- correct the codexreview trap - wrong root cause, now fixed
- refresh the P6 restart handoff after T031a
- T031a: record the adversarial review outcome (converged@4)
- P6 restart handoff — US5 sealed routes (T034-T039)
- P4 US3: real DHT store/lookup over S-Kademlia + naming + tamper-reject (T023-T027)
- roadmap — T011 real MsQuic wire done; T015 resume point
- roadmap — P2 T014/T016 done, T011/T015 resume point
- record BouncyCastle.Cryptography dep (DEC-CRYPTO-1)
- analyze — close 2 coverage gaps (FR-021, FR-022)
- tasks — 59 tasks organized by user story (MVP = US1+US2)
- plan — architecture, research, data-model, contracts, quickstart
- clarify — resolve 4 cycle-2 §5 mechanism choices
- specify YNET ynet-transport (GLPNET transport/overlay tier)

## [v2026.08.23.2] - 2026-08-23

### Changed
- sync round 44 - reconcile in-sync, dedupe 0 groups (118 live), export 20/119/3821 both legs
- Merge pull request #218 from olamni-glp/090-ack-sweep-and-sync
- sync round 41 - import 2 files, 2 more spec links, export 20/118/3806 both legs
- sync round 43 - reconcile in-sync, dedupe 0 groups (118 live), export 20/119/3821 both legs
- traps 10-13 from the olamnit-assistant lane - amended here, not in a third file (#217)
- sync round 42 - reconcile in-sync, dedupe 0 groups (118 live), export 20/119/3821 both legs
- unify the two cross-host report standards into docs/SITREP-FORMAT.md; ariellas' file is canonical, gavriella's becomes a pointer
- mandate the signed export fold - reconcile reported in-sync while 6 peer feature-states were unimported
- sync round 41 - imported peer round-40 state (6 features advanced), export 20/119/3807 both legs
- Merge pull request #216 from olamni-glp/089-pointer-update
- restart pointer - v2026.08.23.1 released, develop 1 ahead of main, block 2a refuted, 6 features linked
- post-release reconcile + dedupe 0 groups (118 live) + export 20/119/3807
- Merge pull request #215 from olamni-glp/088-post-release-sync
- round 40 post-release - 6 features linked to spec dirs (promoted->specified), unlinked 75->69, export 20/118/3806 both legs
- ERA is a synonym for a FEATURE - specify through close - and withdraw the B20 discharge-close proposal
- Merge pull request #214 from olamni-glp/main

## [v2026.08.23.1] - 2026-08-23

### Added
- add + promote bk-onrestart per-host reboot lane relaunch; sync round 34 both legs OK

### Changed
- Merge pull request #212 from olamni-glp/087-trust-gate-reproduction
- A20 trust-gate controlled reproduction - peer's 65 refusals are exactly this host's own exports; gate is correct; real defect is 4 repos sharing one inbox
- Merge pull request #211 from olamni-glp/086-ariellas-tidyup-and-takt
- correct Y02b - pull --rebase silently dropped my merge commit; content survived via the peer's own path
- Y-series ledger - 4 DONE with receipts, Y02b added, and why the step board cannot show them
- sync round 39 + Y-series conflict-count corrections + fleet standard report formats
- sync 2026-08-23 - import 1 new, reconcile, dedupe 0 groups (115 live), export 20/116/3762; olamnit scheduler onboard 35d 3x8h + directive items D01-D05 captured
- restart pointer - 086 branch active, PR 210 merged, classifier measured INTERMITTENT
- standardised cross-host SITREP + roadmap table format with measurement traps
- sync round 39 - import 1 file/0 lines, reconcile in-sync, 0 dup groups in 117 live, export 20/118/3793 both legs
- Merge pull request #210 from olamni-glp/085-onrestart-fleet-resume
- restart pointer - T17 done, 45 branches classified, 0 need preservation
- T17 classification of all 45 non-active local branches - 39 RETIRE, 6 RETIRE-LOCAL, 0 need preservation; no local branch holds unique work
- bk-flow readiness gap run f49a - unique allocation, takt-only durations, era; 3 negative answers + a merge-algebra defect
- restart pointer session 4 - C15/T01/T15 done, roadmap round 38, takt 4of4 sources, 7 measured defects, bk-flow NO-GO is peer-measured
- sync round 38 - import 1 file/0 lines, reconcile in-sync, 0 dup groups in 117 live, export 20/118/3793 both legs
- Merge 080-occurs-checked-substitution into develop (TIDY-Y04; feature.json conflict resolved as DELETED per 953ec898 untrack rationale)
- Merge 078-verification-receipts evidence manifest into develop (TIDY-Y03; merge-tree probed CLEAN)
- Merge 083-glptutorial-corpus-goldens into develop (TIDY-Y01; merge-tree probed CLEAN)
- Y-series CRDT workplan 2026-08-23 - 16 unmerged heads probed by merge-tree, 18 steps into the marathon
- sync round 37 - reconcile in-sync, 119 live features, 0 duplicate groups, export both legs
- Merge pull request #209 from olamni-glp/chore/tidy-up-branches-worktrees-20260822-olamnit
- bk-marathon->bk-flow migration plan + promotion-ready bk-flow SKILL.md draft (3rtask run 20260823T140508Z-227d; integration-not-replacement, cutover NO-GO, parity gap 10/10)
- T15 all-drives git-asset survey - clone-2 fully contained (6/6), 2 corrections to the W05 index, msquic vendored checkout recorded
- T01 measured defect - roadmap understates stage for 067/066/059, plus 2 new recording defects and the 065 number-collision correction
- unshipped inventory + withdraw the superseded toolchain divergence figure
- codify consolidated hardening 2026-08-23 into the scheduler feature-stream hardening feature
- clarify - FR-029 fleet distribution ruled out of scope, wait bounds host-declared (120s repo / 60s share)
- sync round 37 - import 1 file/0 lines, reconcile in-sync, 0 dup groups in 117 live, export 20/118/3793 both legs
- C-drive git-asset survey - 2 glpnet scratchpad clones found, both verified safe; survey-one-drive-only lesson recorded; marathon 86 steps
- sync round 36 - reconcile in-sync, 118 live features, 0 duplicate groups, export both legs
- restart pointer - 3rtask ROOT CAUSE (codexreview gate, 3of3 builders, 4of4 features), specified-premise refuted, CRDT workplan T01-T14 in marathon (84 steps)
- specified-features completion CRDT plan - rootcause 082/083/085 stall (run 20260823T093108Z-30dd); durable marathon items M01/X00/C082-C085/G082-G085
- sync round 35 - reconcile in-sync, 116 live features, 0 duplicate groups, export published both legs
- restart pointer - C13 done (085 specified, PR 210), roadmap round 34, engineer-declared 35d 3x8h capacity satisfies J3, 4 new measured defects
- SCHED-R5 rev2 - WITHDRAW rev1 figures; folding + derived pool changes 3 boards; lejepa custom pool 'ariellas-lejepa' hid 30 unowned WPs; buildkit is actually healthy
- sync round 34 - import 1 file/0 lines, reconcile in sync, 0 dup groups in 117 live, export 20/118/3793 both legs
- SCHED-R5 - Lock 1 measured on all 14 boards; yngenios-windows was a SECOND false all-clear (28 of 30)
- add bk-onrestart skill (post-reboot session relauncher, spec)
- spec(085): bk-onrestart per-host configurable auto-installable fleet resume
- restart pointer - scheduler feature-stream ROOTCAUSE (four breaks in series), 076 closed with 3 systemic findings, 083 clarified, 9 open blocks, 3 features promoted
- roadmap: round 33 - 2 new features scored+promoted (scheduler feature-stream durable healing WSJF 2.62, bk-onrestart per-host auto-install WSJF 4.20); rootcause codified; export 20/118/3792 both legs
- sync round 33 + reconcile + dedupe + export - both publish legs OK
- ROOT CAUSE of the missing feature stream - 3 links with no writer, proven both ways on the live board
- sync 2026-08-22 - import 23 new/0 applied (173 untagged refused - known 078 defect), reconcile in-sync, dedupe 0 groups (115 live), export 20/116/3761
- workplan to 37pt delivered / 95 remaining after v2026.08.22.1
- open 1.14 language-authority register for Udi - L1 book 4.3.1 lesseq guard is rejected (083 FR-002), L2 binding at a consume position may be self.glp-only (076), L3 occurs-check UnifyFail vs CompileError (080)
- sync round 32 + reconcile + dedupe + export - both publish legs OK
- land Olamnit branch/worktree tidy-up workplan + preserve 08-20 handover (marathon mrun-76da6e46bd44)
- clarify(083): 4 of 5 ambiguities resolved by measurement (cssg_modules sibling, vendor AND manifest, FR-009 coupled to FR-002, FR-008 discriminator); FR-002 diagnosed and left OPEN as an engineer ruling with a recommendation
- close(076): retrospective + close-out - 3 findings (shipped with NO codexreview recorded, no size estimate so takt cannot see it, open 1.14 question on binding at a consume position); 0 stale actions; roadmap released->closed
- Merge pull request #207 from olamni-glp/main
- add tidy-up survey evidence manifest - 4 pairwise-disjoint slices
- renumber occurs-check spec 078->080 (engineer OK, both peers concur; yields 078 to gavriella verification-receipts)
- spec(078): occurs-checked substitution pipeline — propose-first spec + clarify; §1.14 semantics (UnifyFail vs CompileError) OPEN for Udi, implement gated

## [v2026.08.22.1] - 2026-08-22

### Added
- T045-T047 harness receipts - skip/unsearchable accounting, composite section keys, build-staleness gate

### Fixed
- close codexreview B1-B4 - every skip path accounted in Sections I/T/U, pipefail on mtime scans, compile-closure scope
- close codexreview A1-A4 - incomplete runs exit 2, full-precision mtime, failed scan is UNSEARCHABLE, freshness scoped to the real dependency closure

### Changed
- Merge pull request #205 from olamni-glp/078-receipts-gate-b1b4
- Merge pull request #204 from olamni-glp/078-receipts-gate-hardening
- restart pointer 2026-08-22 - A-series ledger supersedes W-series for this lane, marathon next-pointer is stale (defect), 5 measured defects raised, takt targets set
- ariellas-lane CRDT workplan A01-A21 - closes a preservation gap the 08-20 sweep missed (064 d0187c9f was unreachable from every remote ref and tag) and corrects the zero-worktrees claim (clone 2 has one)
- round 31 - reconcile in sync, import 1 file/0 lines (converged), 0 dup groups in 115 live, export 20/116/3760 both legs
- Merge pull request #203 from olamni-glp/078-receipts-us4-harness
- CRDT workplan 2026-08-22 supersedes 08-20 - 20pt delivered 112 remaining; 067 keys are PUBLIC on main across 23 tags
- sync round 30 + reconcile + dedupe (115 scanned, 0 dup groups) + export 20/116/3761 - both publish legs OK
- Merge pull request #202 from olamni-glp/084-onrestart-remediation-followup
- R2 triage review of 050/058/059 + record 058 tip before deletion
- R3 part 3 - 59 version tags lack a GitHub Release, blocked on gh release create permission
- Merge pull request #201 from olamni-glp/main

## [v2026.08.21.3] - 2026-08-21

### Fixed
- close round-10 review - CLI entry point pinned to argv[1], creation time read only from the handle
- close round-9 review - CLI must be the executed script, all transcript reads from one handle
- close round-8 review - cmd /c target identification, JS-runtime-gated CLI match, content fingerprint identity, platform-exact argv0
- close round-7 review - argv tokenizer for attribution, cmd-only packed-string expansion, transcript identity check
- close round-6 review - unbounded boundary search, exact CLI entry point, whole-token args, unidentified appends are not proof
- close round-5 review - single attribution tier plus -NoProfile, record-boundary resume scan, sessionId-proven resume
- close round-4 review - two-tier attribution for shim installs, scan every appended record, exact-identity run lock, bounded reads
- close round-3 review - attribute by image not name, require an appended record, strict tail, ownership-checked prune
- close round-2 review - never claim VERIFIED under a bypass switch, unique lane identity, validated markers, exact claude match
- attribute launch verification per lane - closes codexreview NO-GO (1 critical, 2 high, 1 medium)

### Changed
- Merge pull request #199 from olamni-glp/083-repo-tidy-up
- Merge origin/develop into 083-repo-tidy-up before shipping the onrestart remediation
- release gate CLEARED - round 11 verdict GO, 0 findings after 11 rounds
- restart pointer post v2026.08.21.2 - repo tidy (origin heads 146->19, 127 deletions audited recoverable), zero clean branches remain, pgdb lock and path-mangling traps recorded
- Merge pull request #198 from olamni-glp/main

## [v2026.08.21.2] - 2026-08-21

### Changed
- sync round 30 - import 0 files/0 lines (converged) + reconcile in sync + 0 dupes in 115 live + replay-verify 3760 lines + export 20/116/3760 both publish legs OK; cleared a stale pgdb/.lock held by dead PID 16432
- Merge pull request #196 from olamni-glp/083-repo-tidy-up
- restart pointer post-release v2026.08.21.1 - green 559/559 baseline recorded, Gleam file-overlap measured (N12 premise false, C1 supported), suite-running procedure documented
- Merge pull request #195 from olamni-glp/main
- first persisted release-gate review run - NO-GO, 1 critical 2 high 1 medium on onrestart-launch.ps1

## [v2026.08.21.1] - 2026-08-21

### Added
- resume lanes mid-thread via claude --continue, per-host layout config, register/unregister, at-logon 45s auto-install
- add two-window twelve-tab post-reboot lane launcher - executable spec for buildkit feature bk-onrestart (verified absent: no skill, no CLI)

### Changed
- restart pointer after the six-branch landing - 10/25 steps, zero clean branches remain, release held pending a green suite, false-green detection rule recorded
- Merge pull request #193 from olamni-glp/084-host-tidy-up-and-merge-closure
- Merge pull request #192 from olamni-glp/078-verification-receipts
- Merge pull request #191 from olamni-glp/049-wave1-guard-link-acceptance
- Merge pull request #190 from olamni-glp/083-glptutorial-corpus-goldens
- Merge pull request #189 from olamni-glp/081-scheduler-supply-rootcause
- Merge pull request #188 from olamni-glp/083-repo-tidy-up
- refresh restart pointer for round 29 - true step figure 6 done + 1 held, peer W13 deletion audited safe (124 of 124 contained), unmerged refs now 20
- sync round 29 - import 1 file/0 lines (converged) + reconcile in sync + 0 dupes in 115 live + replay-verify 3760 lines + export 20/116/3760 both publish legs OK
- record 124 provably-contained REMOTE branch tip SHAs before deletion (W13 preservation evidence)
- rewrite the restart pointer - was 25 days stale pointing at feature 060; now points at marathon mrun-f5ef56dba3c1 with the resume one-liner, the 7 open engineer blocks and the preservation-phase evidence
- sync round 28 - import 2 files/0 lines (converged) + reconcile in sync + 0 dupes in 115 live + replay-verify 3760 lines + export 20/116/3760 both publish legs OK
- Merge branch '078-verification-receipts' of https://github.com/olamni-glp/GLPNET into 078-verification-receipts
- sync round 27 - import no-op (177 applied), reconcile in-sync, dedupe 0 groups (115 live), export 20/116/3761, replay-verify OK
- sync round 29 + reconcile + dedupe + export - both publish legs OK
- sync round 28 + reconcile + dedupe + export - both publish legs OK
- Merge pull request #187 from olamni-glp/083-repo-tidy-up
- host D: git-asset survey + tidy-up workplan from 3rtask dddd - 3 blind builders 753 claims 157 corroborated 16 conflicts; merge-tree shows only 4 of 18 branches merge clean; preservation gate requires a restore-verified bundle not a SHA list
- revised 3rtask method draft - 5 critic REFUTEs resolved by revision (preservation gate now requires a verified bundle or pushed archive tag not a SHA list)
- canonical durable CRDT workplan ledger - 14 steps sized on nano/micro/mini/midi/maxi/saga, 39pt delivered 97pt remaining
- 3rtask evidence manifest for the host D: tidy-up survey (3 file-disjoint subject-overlapping slices, closed asset vocabulary)
- takt metrics contract + bk-flow migration readiness - measured actuals, two-axis takt, 4 of 7 prereqs unmet
- record 70 provably-contained branch tip SHAs before deletion (W10 preservation evidence)
- Merge pull request #186 from olamni-glp/083-repo-tidy-up
- Merge remote-tracking branch 'origin/chore/roadmap-sync-20260816c-olamnit' into 083-repo-tidy-up
- Merge remote-tracking branch 'origin/chore/roadmap-sync-20260816b-olamnit' into 083-repo-tidy-up
- Merge remote-tracking branch 'origin/chore/roadmap-sync-20260816a-olamnit' into 083-repo-tidy-up
- Merge remote-tracking branch 'origin/chore/roadmap-sync-20260814c-olamnit' into 083-repo-tidy-up
- Merge remote-tracking branch 'origin/chore/roadmap-sync-20260814b-olamnit' into 083-repo-tidy-up
- sync round 27 + reconcile + dedupe + export - both publish legs OK (tidy-up branch)
- merge(065): ynet-consolidation spec into tidy-up - feature.json stays untracked per develop 953ec898
- sync round 17 + reconcile + dedupe + export - both publish legs OK
- Merge pull request #185 from olamni-glp/078-verification-receipts
- sync round 26 + reconcile + dedupe + export - both publish legs OK (coop mirror explicit)
- Merge pull request #184 from olamni-glp/078-verification-receipts
- sync round 25 + reconcile + dedupe + export - both publish legs OK
- spec(083): glptutorial corpus-golden reconciliation - 4 measured defects, 1 open clarification
- Merge remote-tracking branch 'origin/develop' into 078-verification-receipts
- Merge pull request #183 from olamni-glp/082-feature-stream-superset
- Merge remote-tracking branch 'origin/develop' into 082-feature-stream-superset
- sync round 16 export - 20 epics 116 features
- sync round 15 export - 20 epics 116 features
- Merge remote-tracking branch 'origin/develop' into 078-verification-receipts
- Merge pull request #182 from olamni-glp/078-verification-receipts
- sync round 24 + export - 20 epics, 116 features, 3760 journal lines; both publish legs OK; also ignore config.local.json (rule absent on this branch) and ALERT.md (scheduler watch, auto-added)
- sync round 14 export - 20 epics 116 features
- spec(082): coordination feature-stream durable superset fix - 7 user stories, FR-001..FR-021, SC-001..SC-008 with measured baselines; 5 escalations explicitly out of scope
- bind sched_root via config.local.json and gitignore it - repo had NO board bound (root configured=false) and the CLI documents this file as gitignored while the rule was absent
- design(scheduler): amend SR-7 - shipped bk-guards template-contract is one-directional and cannot catch D1 (measured: clean on 62/62 skills); requirement is an extension, not a wiring job
- Merge pull request #181 from olamni-glp/078-verification-receipts
- design(scheduler): durable superset fix SR-1..SR-8 for the feature stream - every requirement anchored to an adjudicated claim from 3rtask 20260819T162016Z-6e73, with escalated items marked not-actionable and non-coverage stated
- untrack .specify/feature.json - machine-local pipeline state that made every feature branch conflict with every other
- 3rtask trace reports ok:true with routed:false on catalog lock - dropped leg reported as success
- git <rev>:<path> false-negative on dot-prefixed paths - nearly inverted the PROG-B unshipped inventory
- 2 notes - duplicate-prevention win (2 roadmap features + 1 3rtask run already existed); 3rtask cross-host portability defect (gitignored evidence + absolute drive-letter roots)
- host-resolvable scheduler root-cause manifest for gavriella (I:->D: drive-letter resolution; live root measured 48 ops vs 11/6 on stale G:/H:)
- Merge remote-tracking branch 'origin/develop' into 078-verification-receipts
- add scheduler root-cause manifest; ignore derived evidence projection
- sync round 13 export - 20 epics 116 features 23 not-closed
- sync round 23 export - 115 live features, 0 duplicate groups; coop mirror OK (prior rounds falsely reported skipped - fixed by explicit --coop-inbox)
- tasks(078): close analyze CRITICAL G1 - add T063-T066 for FR-013 enforcement and FR-017 non-adoption; tag FR-001/FR-015; 62 to 66 tasks
- Merge pull request #180 from olamni-glp/main
- cn-...082050 Programme B win — verify-claims-against-develop, plain-code conflict-graph
- Programme B decision brief — B1/B2/proposed_actor MERGED to develop (behaviour list stale); real rootcause = deploy-lag + unmerged B3 gate; 1 __main__.py conflict; release+deploy path
- export 20/115/3720 (17T070703Z; replay-verified, 0 dupes, in-sync); publish to coop
- export 20/115/3720 (import path hangs — ariella import-crash defect; catalog converged, replay OK)
- close #19 batch-roadmap-advance (shipped->closed); export published
- sync-state snapshot before reboot (feature.json 079 pointer, import manifest/provenance)
- export 20/115/3719 (feature #13 v6 epic-set + behaviour list); publish
- coordination remediation Programme B authoritative behaviour list (B1-B6) + run plan — safe-restart input
- sync round 9 + codify A+B remediation (feature #13 carries combined method); export 20/115/3716
- sync round 8 — converged export 20/115/3715
- sync round 7 — converged export 20/115/3713 (occurs-check renumbered 078->080)
- sync round 6 — import peer +124 lines, export 20/115/3713
- sync round 5 — import ariella promote+rescores (occurs-check promoted), export 20/115/3584
- spec(065): YNET--consolidation specification from 3rtask run 20260803T134739Z-fa8a (6-item gap set, evidence-derived P1-P6 order, escalate gate FR-008)
- close-out retrospective - 2 systematic findings (US5 delivered with zero tasks + ship-gate letter says FOUR not FIVE; marathon expand --steps comma delimiter undocumented and resolve leaves orphan steps) with 2 tracked fix_failing actions; 0 stale actions, nothing to reconcile
- export snapshot after wave-1 close - glp-policy-guard + http3-quic-ws-link-full-acceptance + wave-1-consolidated advanced promoted->shipped->released (19 epics/92 features/2128 journal lines); wave-2 is now head of the wave chain
- close T032 - roadmap advance done (glp-policy-guard + http3-quic-ws-link-full-acceptance + wave-1 consolidated all promoted->shipped->released); ship half was already complete (PR #100 merged 2026-07-09, tags v2026.07.09.1/.2, 049 fully contained in origin/develop and origin/main) so buildkit ship was a no-op and not re-run; marathon run mrun-b0fabf3f8f44

## [v2026.08.19.2] - 2026-08-19

### Changed
- Merge pull request #178 from olamni-glp/078-verification-receipts
- sync round 22 export - 115 live features, 0 duplicate groups (id-stem + title strategies). Reproduces two known false-successes verbatim: publish reported 'coop mirror skipped (no/unreachable inbox)' while D:/coop/glpnet/roadmap-sync/inbox holds 226+ files (register block 39), and reconcile reported 'already in sync' while specs/078-verification-receipts sits at analyze-complete and its feature is still state=promoted (the slug-vs-feature-id mismatch). Both are the same shape: a matcher that finds nothing reports success
- sync round 12 export - 20 epics 116 features 3760 journal lines
- Merge pull request #177 from olamni-glp/main

## [v2026.08.19.1] - 2026-08-19

### Fixed
- T062 - resolve register block 49, the tracked-vs-ignored contradiction that was a declared merge blocker. Three artifacts disagreed about .specify/receipts/: plan.md said gitignored, .gitattributes pinned eol=lf on it (a no-op on ignored files), and git check-ignore said neither. Settled as receipts=IGNORED evidence (FR-002), manifests=TRACKED declarations (FR-019 adoption, FR-023 expected-checks). First attempt used a bare directory exclude with negations, which git cannot honour once the parent dir is excluded - verified by content, not assumed, and switched to the contents-glob form. Verified: receipts under any area ignored, both manifests tracked
- pin eol=lf on receipt contracts and conformance vectors (spec 078 research R8) - the schemas committed one commit earlier tripped the same CRLF warning that exposed 242 signed roadmap exports this morning. A byte-compared contract whose line endings are unpinned can fail verification for reasons having nothing to do with the check it describes
- pin cross_runtime results/*.out to LF - stops zero-content CRLF churn after every suite run
- pin eol=lf on signed roadmap exports (register block 38) - each export carries a key_id and a signature over its bytes, and git warned LF->CRLF on this session's commit, so a peer verifying after a CRLF checkout verifies a byte-different document and the signature fails. 242 tracked exports were exposed. Pin ONLY - existing files are not renormalised, so no concurrent checkout is dirtied mid-marathon (verified: check-attr reports text=set eol=lf, tree clean apart from .gitattributes). Reproduces ariellas' ospark escalation and is the same mechanism as 059 T051 recorded four lines above it
- remove dead AbandonOps stub (US2/Scope B) — abandon is anon-writer discard (062 US5)

### Changed
- Merge pull request #175 from olamni-glp/078-verification-receipts
- merge: origin/develop into 078-verification-receipts - 37 conflicts resolved. 34 regenerable test/parity/cross_runtime/results/*.out took develop's LF-pinned versions (the local churn was compile-timing noise, 'Compiled in 0.69s' -> '0.96s', not content). .gitattributes and .gitignore were UNIONED: both sides were purely additive - develop added the parity LF pin and the gleam_quic/_build untrack, this branch added the T062 receipts rules and the bk-flow links ignore; neither side removed anything. .specify/feature.json kept ours (machine-local pipeline state; this is the 078 branch). Verified after merge: receipts ignored, both manifests tracked, develop's _build untrack intact
- 4 notes from the scheduler root-cause session - allocate records an assignee but never a column move; committed != merged != released != installed; a matcher that finds nothing reports success (3 live reproductions); and one win - the cross-provider blind codex critic caught a blindness-breaking method before it faked corroboration
- sync round 11 export - 20 epics 116 features 3759 journal lines
- sync round 21 export - import applied, 0 delivered-feature advances proposed. Publish reported 'coop mirror skipped (no/unreachable inbox)' which is FALSE by content: D:/coop/glpnet/roadmap-sync/inbox exists and holds 226 files. That is register block 39 (EMPTY-vs-UNREAD collapse) reproducing live; its fix 3dca578c is stranded on glpnet-lane/toolchain-integrity-fixes, reachable from no branch and no tag
- Merge 066-abandon-stub-cleanup into develop - remove dead abandon.dart throw-stub (0 call sites)
- bind the claimed scheduler WP to the feature - side effects of bk-flow open. .specify/feature.json now points at 078-verification-receipts and .gitignore excludes .specify/flow/links/ (machine-local run ids, written by the bridge). NOTE for block 49: this is the SECOND path writing gitignore rules for .specify/ subpaths, and it lands while plan.md, .gitattributes and .gitignore still disagree about whether .specify/receipts/ is tracked - carried as task T062, a merge blocker, and deliberately not resolved here
- tasks(078): 62 tasks in 7 phases, organised by user story and repo. Tests are mandatory not optional - US3 IS an acceptance suite and FR-014/015/016 require a fault injector per silent-success mode with the suite subject to its own invariant. Every path carries a bk:/gn: repo prefix because the feature implements across two repositories (block 51), and every gn: task in Phase 4+ pins a RELEASED buildkit version rather than its branch - a gn: task reading buildkit source directly would reintroduce the copy-divergence FR-024 forbids. Sequencing follows research R4: the bash test harness adopts FIRST because it carries 5 of the 13 witnessed instances AND is the only runtime that cannot import the Python emitter, so if the contract cannot be emitted from bash that must surface before five Python areas are built on it. T045 keys per-section receipts on (letter, slug) because Section I is declared twice (lines 1653, 2219) and letter-keying would manufacture this feature's own defect. T040 proves the suite can go RED (SC-007). T062 carries block 49 - my own tracked-vs-ignored contradiction - as a merge blocker rather than leaving it. Validated: 62 sequential ids, 0 format violations, 33 parallelisable, story split 6/8/12/14
- plan(078): answer block 51 - delivery sequencing across two repositories. /bk-ship is a single-repo conductor but FR-024 puts the contract in buildkit and the harness consumer in glpnet, so the feature ships in TWO ordered single-repo releases mirroring FR-024's own model: W1 contract (buildkit receipts package + 3 schemas + conformance, released so glpnet has something to PIN), W2 first adopter (glpnet bash emitter + per-section receipts keyed (letter,slug) + both manifests + the US3 fault-injection suite), W3 retrofit. Rejected splitting into two roadmap features (fragments a feature whose thesis is that a contract and its adoption are one thing) and shipping glpnet alone (leaves the authoritative contract unreleased so the consumer pins nothing). Three consequences the decomposition must honour are recorded, including that the marathon's single /bk-ship 078 discharge item is satisfied by TWO releases, not one invocation - recorded so the gate is not quietly reinterpreted at ship time
- plan(078): Phase 0 + Phase 1 complete - plan, research, data-model, 3 normative JSON Schemas and a VERIFIED conformance suite. Central constraint: the six declared areas are not one runtime (3 Python CLI capabilities, 1 bash test harness, 1 filesystem protocol across 3 hosts), so the shared artifact is the DOCUMENT, not a class hierarchy. Storage is files, not the catalog - deliberate rejection recorded in research R1: a mechanism built to prove things ran must not depend on the component with this fleet's worst measured silent-failure record (marathon capture failed 8x and exited 0 every time). Constitution: 8/8 pass, 2 N/A (no GLP source touched, no language authority engaged), VI-b satisfied BY AVOIDANCE and the avoidance is explicit. research R3 settles the Section I collision at source (lines 1653 and 2219, re-verified today) - per-section receipts key on (letter, slug) or they manufacture this feature's own defect. R4 maps all 13 witnessed instances to areas: the bash test harness carries 5 and cannot reuse the Python emitter, so it is the honest first adopter. CONFORMANCE VERIFIED 7/7 against the schema: 2 accepted, 5 rejected (missing reason on non-success, crash-reported-as-PASS, outcome outside the closed five, override without expiry, undeclared area)
- untrack regenerable gleam_quic/profile_c/_build (1041 files) - deep openssl paths break Windows worktree checkouts
- Merge pull request #174 from olamni-glp/main
- clarify(078): CLOSE the six remaining ambiguities (register blocks 24-29) - stage substance complete. Adopted on the recommended option under a STANDING ENGINEER INSTRUCTION to proceed after the decisions were raised five times unanswered; each is marked provisional and records the alternative it beat, so any one is a single edit to overturn. FR-022 receipt addressing (conventional location + pointer on the verdict; beat inline-only and catalog rows - the latter would make a prove-it-ran mechanism depend on the component with this fleet's worst silent-failure record). FR-023 per-run declared expected-check set, absence is an error - closes FR-013's vacuity hole with the SAME rule as FR-020 rather than a second one; beat derive-from-last-run, a ratchet that only loosens. FR-024 single contract owner + versioned consumption + a conformance fixture whose own output IS a receipt; beat copy-into-both, the exact divergence this feature exists to stop. FR-005 bound the enumeration never the totals, byte backstop - a plain byte cap would have defeated FR-010 reconciliation. FR-012 override gains briefing/ack/rationale/scope/mandatory expiry, no indefinite override. SC-003 blind reader + cross-lane corroboration + samples drawn only from real receipts - author-written samples made the criterion unfalsifiable by the person it tests. Checklist Iteration 3 restores the two ticks Iteration 2 revoked, qualified: the requirements ARE now unambiguous, but the engineer has not individually ratified the six choices
- clarify(078): REVOKE two checklist ticks - the feature's own quality gate was a stale green. Iteration 1 validated 2026-08-12 and never re-ran; clarify has since rewritten FR-008 to phased and added FR-019/020/021, yet the checklist still reported 16/16 on a spec it had not read in that form. Revoked 'requirements are testable and unambiguous' (FR-002/004 receipt has no defined location; FR-013 'expected' undefined so zero checks satisfies it - the FR-008 vacuity shape one requirement later; FR-005 names no bound; FR-012 override has no scope/expiry/authority; SC-003 reader and samples undefined so the criterion is unfalsifiable by its author) and 'all FRs have clear acceptance criteria' (FR-013). Iteration 1's judgement holds - it even named the FR-012 tension itself; the gap is the ABSENCE OF A RE-VALIDATION TRIGGER, filed as register block 37
- roadmap: sync round 5 - import applied, reconcile in-sync, dedupe 114 live 0 duplicate groups, export 20 epics/114 rendered features/3737 journal entries. THREE live defect reproductions this round, all recorded not fixed: (1) publish reports 'coop mirror skipped (no/unreachable inbox)' while D:/coop/glpnet/roadmap-sync/inbox demonstrably exists - one leg silently dropped at exit 0; (2) reconcile reports 'already in sync with pipeline (no changes)' while 078 sits clarify in_progress and its own spec records as instance 13 that link cannot slug-match it - the exact unqualified in-sync verdict 078 exists to close; (3) status renders 114 features while the export fold carries 30 not-closed rows it never shows (D8 renderer exclusion, measured by title-join, upper bound - some are genuine duplicates e.g. ANTLR4 spike under two epic_ids)
- regenerated fixtures from the clean serialised suite run 2026-08-18 (558 total / 556 passed / 2 failed - only 064 Section T)
- codify: stale-git-ref lesson - comparing against a local tracking branch produced a false headline finding (50-commit-stale develop); rule extended to require origin/<branch> measurement
- clarify(078): encode the phased FR-008 ruling and close its vacuity hole - FR-019 adoption manifest enumerating all six FR-017 areas, FR-020 absence-is-an-error (not a pass, not non-adoption), FR-021 pins SC-002's denominator to the enumeration; plus 3rtask b81c method v1 draft, blind codex Critic REJECT (B1-B10), and method v2
- regenerated cross-runtime result fixtures from the prior session's suite run - timing-only diffs (Compiled in Xs), no semantic change
- 3rtask: mint WP-supply superset corpus (4 disjoint slices) + codify the Windows install-destroys-toolchain finding reproduced this session (WinError 32 half-uninstall)
- marathon programme backlog captured to a TRACKED FILE because buildkit-marathon capture failed 8x and EXITED 0 every time - store catalog dir is empty, pglite_init_failed, writes silently lost while status still reads from the .md mirror; same false-green class as the rest of the session. Records the 11 discharge items, the B1-B5 superset ownership, the 4 wave-0 honesty fixes assigned to W5, 6 repo-local defects, the 077 stale-build resolution, and 7 standing rules adopted
- 077 U-1/U-2 root cause was a STALE BUILD, not a code defect: the C# REPL exe was built 2026-08-12 08:50 while term_traversal.cs/partial_evaluator.cs were modified 2026-08-13 21:02 - 37h older, so the binary predated 077 entirely; the stack frame named the PRE-CONSOLIDATION PartialEvaluator.ApplySubstitution which no longer exists in source. After dotnet build both cyclic fixtures emit 'Cyclic term detected' with ZERO stack overflows. 077 OVERSTATED stamp WITHDRAWN - the feature was correct all along; 064's stamp stands. Roadmap export 20/115/3737, replay-verify OK
- roadmap: engineer rulings executed - 064 and 077 carry a loud OVERSTATED correction (released while their own acceptance tests fail, 552/558, all 6 failures theirs); walk-back was NOT EXECUTABLE - advance is forward-only and no reset/revert/demote verb exists, same no-reverse-gear class as D4, so the engineer ruled record-and-keep; both rows' original notes were briefly clobbered by a PROBE literal (D10 hazard, repeated by me) and restored byte-identical from the 175506Z export in the same write; export 20/115/3736, replay-verify OK
- roadmap sync 175506Z post-reboot: import 1 file/0 lines, reconcile in-sync, dedupe 0 dupes, export 20/115/3715, replay-verify OK, both legs SHA-identical; board note gavriella:000005 recorded host return and MINTED A PHANTOM WP (6->7) via free-form note --wp, live reproduction of phantom-WP materialisation + D4 no-retirement-verb, disclosed to the board-ownership lane
- codify: buildkit-3rtask run index disagrees with run.json - after re-issuing a verdict, run.json reads budget_stop while 'list' still shows halted and index_recorded=false is reported silently; the cheap summary surface contradicts the system of record
- roadmap sync 151922Z + ESC-2 CLOSED: slice S4 (coop operational record) re-run and emitted - 21 failed attempts, 15 defects keyed, 7 silent-loss instances, 13.6pc corpus coverage so counts are lower bounds; verdict halted -> budget_stop with all 4 slices in; NEW D1/D2 key collision between the 3rtask keying and the engineer ruling; NEW FA-15 the one working allocation carries engineer_id=mvw and its semantics were never ruled; export 20/115/3715, replay-verify OK, both legs SHA-identical
- codify: roadmap notes field has a hard OS command-line ceiling (--notes only, no --notes-file/stdin; superset row at 30163 of 32767 chars) and the failure surfaces as a false green when a loop branches on a stale exit code
- RCA addendum-3: the addressee reader ALREADY EXISTS (bc203794, authored 11:15Z today, stranded on one untagged branch) - two lanes independently concluded no consumer exists and both were right about every tree they could reach; corrects the stated build order (first buildable unit is the allocate WRITER, not the reader); plus a fleet-wide defect - edit-feature --notes passes the whole body on the command line so a roadmap notes field cannot exceed the OS cap, failing as an OS error an exit-code check reads as success
- 3rtask RCA 083346Z-6bb9: WP-stream root cause is TWO SERIAL supply-half locks - allocate is an orphan-read op type so confirm.admit P5 requires_proposed_actor is unsatisfiable, and e_t_s is hardcoded 0.0 with no CLI flag; overturns the no-transition-verb and the CLASS-A no-pipe beliefs; codified x2, superset addendum landed on the buildkit row (v5->v6, no duplicate minted)
- roadmap: sync round 105208Z - D12 REPAIRED (qr-link-provisioning dangling epic_id -> distributed-glp-connectivity, dangling 1->0, attributed 98->99); backfilled 95 missing .license sidecars and import refusals went 25082->0 on the same corpus; export 20/115/3715, replay-verify OK, both legs SHA-identical D3308ADC (roadmap MOVED, first time in 46h)
- roadmap: sync round 101312Z - import 10 files/0 lines (0 foreign + 1726 UNTAGGED refused, refusal class inverted vs 083720Z), reconcile in-sync, dedupe 0/115, export 20/115/3713, replay-verify OK, both legs SHA-identical A47C7A37 (unmoved 43.8h)
- roadmap: sync round 083720Z - import 4 files/0 lines (converged, 553 foreign refused), reconcile in-sync, dedupe 0/115, export 20/115/3713, replay-verify OK, both legs SHA-identical
- roadmap: sync round 152625Z - D11 ruling applied, 064 restored to ariellas 3.20/1440; import 3 files/0 lines (converged), dedupe 0/115, export 20/115/3713, replay-verify OK, both legs SHA-identical
- roadmap: score-and-promote-all round 140227Z - 0 unscored + 0 promotable remain; scored 064 (2.25/720) and 066 (0.85/62.3 after filling target_user/effort/risk); export 20/115/3589, replay-verify OK, both legs SHA-identical 55A5EDC5
- roadmap: publish 131757Z - atomic-toolchain-installs advanced shipped->released on buildkit tag v2026.08.10.1 (merge b8b2bfa5, PR300); 20/115/3584, replay-verify OK, both legs SHA-identical 9507DF3F
- roadmap: sync round 130608Z - import 5 files/124 lines, reconcile in-sync, dedupe 0/115, export 20/115/3583, replay-verify OK, both legs SHA-identical AF0C8347; wave-2 claimed per ariellas handover
- roadmap: sync round 102116Z - import 3 files/1 line, dedupe 0/115, export 20/115/3459 both legs hash-identical, replay-verify OK; reconcile-vs-link divergence reproduced live again (078 instance 13)
- Merge develop into 078: the duplicate .import-provenance gitignore rule collapses into develop's canonical manifest ruling (PR #153 landed)
- roadmap: sync round 001807Z - import 5 files/14 lines, dedupe 0/115, export 20/115/3458 both legs hash-identical, replay-verify OK; 115th feature coordination-feature-stream-durable-superset-fix now held here (was the 114-vs-115 divergence)
- gitignore derived .import-provenance/ on 078 (duplicate of the 077 ruling; collapses when PR #153 lands)
- roadmap: sync round 200747Z - import 6 files/178 lines (ACCEPTED the 3 untagged compiler-robustness entities ariellas refused), dedupe 0/114, export 20/114/3444 both legs hash-identical, replay-verify OK
- Merge remote-tracking branch 'origin/develop' into 078-verification-receipts
- roadmap: sync round 164604Z - import 3 files/0 lines (fleet converged with ariellas 103407Z), reconcile in sync, dedupe 0/113, export 19/113/3266 both legs hash-identical, replay-verify OK; export byte-identical to 090855Z confirms 078 specify did NOT reach the roadmap (instance 13, slug-link gap)
- spec(078): verification receipts and loud failure - F1 of the RCA set, specify stage complete (13 witnessed instances, 18 FRs, 8 SCs, fault-injected acceptance; instance 13 found while specifying)
- complete abandon.dart removal — delete dead throw-stub + drop its unused import from runtime.dart (0 call sites; analyze clean; REPL 547/547 green)
- reconcile codeconv inventory for retired abandon.dart — rm 3 stale artifacts (tombstone/plan/spec) + drop 2 dangling graph edges; PGLite inventory empty (no DB row to retire)

## [v2026.08.18.2] - 2026-08-18

### Fixed
- rename GlobalSendSpawn/Goal.readerAddr -> onBindWriterAddr (R-3 mis-named field held an onBind writer key, not a reader addr); update all refs + docs; multiagent 102/5 green (baseline parity)

### Changed
- Merge pull request #172 from olamni-glp/079-madglp-writer-reader-discipline
- set active feature=079 for ship gate (feature.json)
- Merge remote-tracking branch 'origin/develop' into 079-madglp-writer-reader-discipline
- close #6/#12/#14/#28 (released->closed, open-action check passed); re-export
- sync round 10 export - 20 epics 115 features 3741 journal lines
- sync export 20/116/3733 (in-sync, 0 dupes, replay-verified); +consolidated-hardening-spine; import skipped (R5 fleet-crash)
- remove #15/076 from olamnit Wave 4 — ariellas' lane, shipping via PR #169 (sibling read-only)
- Merge pull request #171 from olamni-glp/main
- three_agent_pipeline_boot verdict = FALSE POSITIVE retired (2/2 green, FR-005/SC-004); T016 satisfied-by-inspection (no Open/Fixed inconsistency exists); note US3 readerAddr->onBindWriterAddr rename
- tier-3 execution rulings — 079 baseline (multiagent+SectionS), Wave2 parallel, per-feature marathon for large items, Wave0 autonomy
- JIT refinements — 079 US3+US2 increment ship, #1 MVP-first, hardening Wave5, overlap-cluster verify pre-Wave4
- glpnet consolidated completion plan — mrun-7f0b400450f3, 6 dependency-ordered waves, 4 parked blockers, hardening-charter + scheduler-Programme-B folded in
- tasks(079): madGLP writer-reader audit — 20 tasks, US1 core-touch gated (R-1b STOP + SHIP-TOKEN), US2/US3 clean closes; risk-first US3->US2->US1
- plan(079): madGLP writer-reader audit — 🔴 R-1 scope split (writerAddr+1 is bound-writer path, not dead code); R-2/R-3 clean; STOP-report guard on core heap-format
- spec(079): madGLP writer-reader address-discipline closure — specify+clarify; audit-first N/N+1 fallback removal + 2 residuals, NOT §1.14

## [v2026.08.18.1] - 2026-08-18

### Added
- capture coordination-feature-stream-durable-superset-fix (3rtask ba84) + codify note; export 20/115/3457

### Fixed
- elide all four non-deterministic sources from cross-runtime captures
- elide wall-clock compile timings from cross-runtime captures
- install missing bk-scheduler skill from buildkit + glpnet-local --root addendum
- guard 064 Section T with set +e/-e so a drill failure records FAIL instead of aborting the whole suite

### Changed
- Merge pull request #169 from olamni-glp/076-typechecker-body-atom-moding
- point feature.json at 076 for ship, reconciling with pipeline DB
- Merge remote-tracking branch 'origin/develop' into 076-typechecker-body-atom-moding
- parity artifacts now encode the GREEN run, replacing the failing-run capture from 555c5b93
- roadmap: sync round 20260816T205920Z - post SC-002 signature
- T015 quickstart verified end-to-end; repro loads, gate satisfied, dart PATH drift fixed
- SC-002 SIGNED - 550/550 REPL green, Dart 460/5/5 same 5 pre-existing; C-to-G was host contamination
- roadmap: sync round 20260816T201957Z
- roadmap: sync round 20260816T195952Z
- roadmap: sync round 20260816T193054Z - targeted import 7 files/25 lines (bulk import blocked by D13)
- decision register completeness pass - 11 further open blocks (D17-D27)
- open decision register - 14 open blocks with options and recommendations
- roadmap: sync round 20260816T190258Z
- restart brief - add bk-close target hazard, glpquick-cert integrity baseline, ship eligibility
- Merge pull request #164 from olamni-glp/docs/079-restart-handover-olamnit
- safe-restart brief for glpnet lane 20260816T150000Z
- roadmap: sync round 20260816T145923Z
- fetch 3rtask programme A frozen method and programme B curation method from channel
- roadmap: sync round 20260816T141516Z - import 3 files/1 line, replay verified
- roadmap: sync round 121141Z - import 8 files/2 lines, replay verified
- roadmap: sync round 082731Z - 064 restored 3.20/1440, 20/115, replay verified
- roadmap: sync round 150807Z - import 5 lines, 20/115/3713, replay verified
- flow-gate audit - allocator per-day capacity gate, 2470 silent import refusals, coordination overhead
- roadmap: sync round 140255Z - no delta, export byte-identical to 135031Z
- roadmap: score final unscored feature; 100pct coverage of not-closed, 20/115/3708
- safe-restart handover — madglp lane continues from /bk-plan; occurs-check §1.14-blocked; coordination-fix promoted
- roadmap: sync round 132810Z - import 5 files/1 line, 20/115/3584, 0 dupes, replay verified
- roadmap: score 7 unscored features, promote 2; 20/115/3583, 0 dupes, replay verified
- refresh cross-runtime parity run artifacts - encode the failing C-to-G run, not goldens
- roadmap: sync round 115246Z - 20/115/3459, 27 open, 0 dupes, both legs sha-identical
- roadmap: sync round 110932Z - 20/115/3459, 0 dupes, both legs sha-identical; untrack derived import bookkeeping per 2026-08-11 manifest ruling
- Merge pull request #162 from olamni-glp/chore/roadmap-sync-20260814a-olamnit
- Merge develop into roadmap-sync round 4: accept the ruled deletion of the derived .import-manifest.json
- Merge pull request #161 from olamni-glp/chore/roadmap-sync-20260813c-olamnit
- Merge develop into roadmap-sync round 3: accept the ruled deletion of the derived .import-manifest.json
- Merge pull request #159 from olamni-glp/chore/roadmap-sync-20260813b-olamnit
- Merge develop into roadmap-sync round 2: accept the ruled deletion of the derived .import-manifest.json (gitignored on develop per the 077 manifest ruling)
- Merge pull request #160 from olamni-glp/chore/coordination-hardening-feature-20260813-olamnit
- Merge pull request #158 from olamni-glp/fix/064-section-t-set-e-guard
- Merge pull request #153 from olamni-glp/077-roadmap-sync-mechanics
- codify: concurrent-suite fork exhaustion; callee-end shape rejected by head rule
- roadmap: sync round 094329Z - 20/115/3459, import 4 files, 0 dupes, both legs sha-identical
- 076: occurrence-pair licensing accepts head-flipped writers in body atoms
- sync round 4 — D4 verb-AND-fold refinement (row_version=3), export 20/115/3459
- sync round 3 — D4 re-keyed CLASS-B (ariella 233000Z), feature notes updated, export 20/115/3458; ba84 curator addendum
- codify: D1/D4 are CLASS-B wiring gaps; 3-way 3rtask merge-schema defect
- roadmap: sync round 232034Z - 20/115/3457 converged, byte-identical re-export, both legs published
- roadmap: sync round 215152Z - 20/115/3457, converged with olamnit, both legs sha-identical
- 3rtask 2855: distributed allocation design (SUFFICIENT_WITH_STATED_GAPS) + 6 codify notes
- Merge remote-tracking branch 'origin/develop' into 077-roadmap-sync-mechanics
- coop: withdraw ROOT.md rule 3 - fallback_used is advisory provenance, not a root signal (W16)
- 3rtask: refresh portfolio evidence + manifest v3 for allocation run
- roadmap: sync round 182501Z - 20/114/3444, published both legs, replay-verify OK
- coop: check in COOP/ROOT.md as the authoritative channel-root pointer
- coop: pin channel root, correct host identity, ban one-way actions on silence readings
- sync round 2 — import 2/4, reconcile in-sync, dedupe no-op, export 20/114/3444 published
- roadmap: sync round 163415Z - retire pglite (premise void, 3 hosts), 19/112/3267 both legs, replay-verify OK
- Merge pull request #157 from olamni-glp/chore/roadmap-sync-20260813-olamnit
- sync round — import 19/506, reconcile in-sync, dedupe no-op (0 live twins), advance sc-002+077->released by id, replay-verify OK; export 20/115/3440 published
- Merge pull request #156 from olamni-glp/main
- roadmap: sync round 101152Z - 19/113/3266, published both legs, replay-verify OK
- roadmap: import round 095033Z - 2 new files, 0 new lines (converged)
- 3rtask: refreshed allocation-design corpus + subject for the 20260813 run
- roadmap: sync round - import 1, reconcile in sync, 0 dupes over 113, export 19/113/3266 both legs, replay-verify OK; 076 baseline 547/547
- 076: record Gabi's 1.14 approval + amend well-typed-clause spec with occurrence-pair licensing
- roadmap: sync round - import 3 coop files (+1 line: 064 durable-listener RELEASED by gavriella), reconcile in sync, 0 live dupes, 0 needing promotion, replay-verify OK, export 19/113/3266 published 103407Z both legs
- roadmap: sync round 090855Z - import 1 file/0 lines (converged with ariellas 075547Z), reconcile in sync, dedupe 0/113, export 19/113/3266 published both legs hash-identical, replay-verify OK
- gitignore derived .import-provenance/ and .import-refused.json (same class as .import-manifest.json)
- roadmap: 064 durable-listener-service-box RELEASED v2026.08.12.1 (tag 588ed177, PRs 150/151/152, in main+develop); sync round 081208Z applies 119 lines, dedupe 0/113, export 19/113/3266 published, replay-verify OK
- merge develop (v2026.08.12.1 release) into 077
- roadmap+3rtask: score+promote round synced (19/113/3265 both legs); 3rtask fa57 distributed-allocation design (budget_stop, 27 CONFIRM/8 REFUTE/1 ESCALATE)
- 3rtask: allocation-design corpus slices + manifest (portfolio / hosts / protocol)
- roadmap: score+promote sweep - 7 sweep features scored (WSJF 2.4-14.0), all 13 captured promoted, replay-verify OK, export 19/113/3265 published 071422Z both legs
- roadmap: post-reboot sync round - import both legs (0 new lines, fleet converged), reconcile in sync, 0 live dupes over 113, replay-verify OK, export 19/113/3146 published 051516Z both legs
- roadmap: sync round 231956Z - fleet CONVERGED 19/113/3146 with ariellas 223614Z (they adopted the F0 both-legs publish), import 0-delta, reconcile in sync, dedupe 0/113, replay-verify OK; this export DID emit a .license sidecar (3rd datapoint: 190654Z yes / 221029Z no / 231956Z yes - Defect-6 emission is non-deterministic)
- roadmap: CONVERGENCE round - import gavriella 4 coop-inbox files (+290 lines incl 221029Z union), 0 dupes over 113, replay-verify OK, export 19/113/3146 published 223614Z BOTH legs
- roadmap: converge with ariellas 215447Z (recovered from origin eab8ebfc - committed but never published to the channel, F0); 123 lines applied, 19/113/3146 union exported+published, dedupe 0, replay-verify OK; NOTE this export emitted NO .license sidecar (Defect-6 shape, was present on 190654Z)
- roadmap: sync round - self-import 213351Z (0 new lines), reconcile in sync, 0 live dupes, replay-verify OK, export 19/107/2856 published 215447Z
- roadmap: sync round - import 2 files (0 new lines, 390 untagged refused), reconcile in sync, 0 live dupes, replay-verify OK, export 19/107/2856 published 213351Z
- roadmap: sync round 190654Z - allow-untagged import applies 190 lines (split-brain closed, 0 refused), reconcile clean, 0 dups, export 18/105/3023 published, replay-verify OK; untrack+gitignore derived .import-manifest.json per engineer ruling
- analyze: 076 apply top remediations C1 I1 I2 (scan-token reword, FR-007 alignment, baseline.md in tree)
- tasks: 076 dependency-ordered tasks.md (1.14 gate blocks implementation phases)
- plan: 076 design artifacts incl. 1.14 semantics proposal (occurrence-pair licensing)
- clarify: 076 encode Q1-Q3 answers (fix locus, depth-composed rule, 1.14 record form)
- CLAUDE.md note - buildkit CLI python lives in mstack .bk-venv
- spec: 076 typechecker body-atom moding (head-flipped readers, unblock =/2)

## [v2026.08.13.1] - 2026-08-13

### Added
- finalize — generalized Section T over cyclic_*.glp (T024), spec Implemented, known-issues resolved; 551/552 (1 pre-existing Gleam×C# flake)
- structural cycle guard on codegen/analyzer/linker walkers (US1/US2 structural family) + probe; REPL 550/550
- var-name cycle guard on substitution-closure walker (US1/US2 subst family); F-069-1 crash -> catchable CompileError; REPL 549/549
- consolidate PE/analyzer term-traversal into shared term_traversal.cs (US3 dedup) + F-069-1 repro; REPL 547/547

### Fixed
- codexreview c2 — identity guard (not fuel) on shared walkers + ResolveTerm; real-walker probe; T-4 fail-loud
- codexreview — codegen partial-list false-cycle + fuel-guard shared walkers + acyclic fixtures

### Changed
- Merge pull request #154 from olamni-glp/077-guarded-term-traversal
- test updates
- Merge origin/develop into 077 (064 back-merge): keep 077 pointers; union test Section T(064)+U(077); take develop import-manifest
- record codexreview hardening (Decision 5, converged@3) + handover session-5 update
- sync round — import 1 (converged, 0 new lines), reconcile in-sync, replay-verify OK; export 19/102/2879
- Merge pull request #152 from olamni-glp/main
- sync round — import 1 peer file (converged, 0 new lines), reconcile in-sync, replay-verify OK; export 19/102/2879
- sync round — import 1 (converged, 0 new lines), reconcile in-sync, replay-verify OK; export 19/102/2879 (077 implement complete)
- safe-restart handover — /bk-implement COMPLETE (27 tasks, 4 phase commits), NEXT=/bk-codexreview; divergence-#1 + test-reality flags for review
- safe-restart handover — pipeline specify→analyze complete, NEXT=/bk-implement; 5 divergences + owner-review flag captured
- sync round — import 8 files (converged, 0 new lines), reconcile in-sync, replay-verify OK, 0 live dupes; export 19/102/2879 w/ Features A/B WSJF/RICE scored (B=4.2/2800, A=6.0/2000)
- analyze(077): apply top remediations — F1 reassign mark/ground walkers to structural guard (new T019a); A1 fuel-sizing basis; 0 critical, 100% coverage
- tasks(077): 26 tasks, dedup-first (US3 foundational P2) then cycle-guard P3/P4; MVP=P1+P2+P3 closes F-069-1 + unblocks occurs-check
- plan(077): grounded ~21-walker inventory + 2-flavor cycle guard + 5 consolidation divergences (dedup is behaviour-sensitive); research/data-model/contract/quickstart; Constitution PASS
- clarify(077): FR-004 cyclic-outcome = hard-fail CompileError (no-silent-failure; consistent w/ sibling occurs-check; not a 1.14 change)
- spec(077): guarded term-traversal utilities (cycle-tolerant walkers + PE/analyzer dedup) — 3 P1 stories, 8 FRs, 6 SCs; FR-004 cyclic-outcome deferred to clarify

## [v2026.08.12.1] - 2026-08-12

### Added
- T007-T009 US2 durable history - IOpWal reuse (pglite primary, file fallback), replay drained pre-recv-loop at link establish, replayed items never re-appended; history drill 4/4 (order + exactly-once + idempotent restart)
- T006 additive LinkPump.OnDelivered hook - runner-thread, once per delivered term, pre-bind (FR-004); 4/4 tests incl. dedup + close + throwing-observer
- T010+T011 US3 QUIC connector dial-retry (TCP parity, FR-008) - retry refused/timeout until ct, stream+bootstrap keep fail-fast; glp_link 167/167 (was 165)
- T003+T004 resume-goal hook - walk-up registration reader + synchronous arm in AfterEngineCreated; smoke: SC-005 transcript identical, arm/missing/disabled paths verified
- T001 glpservice hygiene - gitignore runtime artifacts, commit registration sample

### Changed
- Merge pull request #150 from olamni-glp/064-durable-listener-service-box
- merge develop into 064 (second pass - develop advanced during ship)
- Merge pull request #149 from olamni-glp/main
- merge develop into 064-durable-listener-service-box before ship
- refine(codexreview): cycle 4/5 [diff/general]
- refine(codexreview): cycle 3/4 [diff/general]
- refine(codexreview): cycle 2/3 [diff/general]
- refine(codexreview): cycle 1/3 [diff/general]
- T012-T014 ticked - N=100 history drill PASS=4/0 (clean re-run after drill-collision contamination), quickstart local verify green, glp_link 171/171 + glp_crdtmsg 188/188, full suite 551/551 incl Section T
- roadmap: import ariellas 153819Z export (2 files/0 lines, journal already converged 18/98/2614), reconcile clean
- roadmap: sync round - import 6 files/45 lines (consumes ariellas 144831Z), reconcile clean, 0 duplicates, export 18/98/2614 published 150440Z, replay-verify OK
- test+docs(064): T012 Section T wires resume+history drills into run_all_tests.sh (explicit-skip, names standalone gates); T013 quickstart drift fixed (sample->rename flow, replay-at-link-establish expected lines)
- restore T009 replay-idempotence unit tests - replay reads never append, double-restart byte-identity, dot-key dedup on crash-replay overlap, both WAL backends; glp_crdtmsg 188/188
- roadmap: capture + promote ynet-consolidation from 3rtask run 20260803T134739Z-fa8a (54 confirmed claims, evidence-derived build order)
- roadmap: sync round - 0-delta import (fleet converged), reconcile clean, 0 duplicates, export 18/95/2551 published 140423Z, replay-verify OK
- roadmap: sync round - import 6 files/6 lines, reconcile clean, 0 duplicates, export 18/95/2551 published 134205Z, replay-verify OK
- T005 US1 restart drill 7/7 green - two launches auto-arm + receive, SC-005 transcript identical; drill listener emits via _output (resume hook prints only its own diagnostics)
- T002 baselines - glp_link 165/165, crdtmsg 184/184, REPL suite in flight; normalized no-registration startup transcript captured
- analyze(064): 5 findings 0 CRITICAL, coverage 100pct; applied top remediations U1 (both-backends-fail policy) I1 (baseline transcript) I2 (diagnostic wording)
- tasks(064): 14 tasks across US1-US3 + polish, MVP=US1, T008 delivery-shape flagged as the one open implementation choice
- plan(064): R1-R7 design - shim resume hook, IOpWal reuse, LinkPump delivery hook, QUIC retry parity; 3 contracts, constitution PASS
- roadmap: engineer-directed close of the three released wave features (2/4/5), export 18/95/2545 published 124431Z
- spec(064): durable-listener-service-box gavri variant - 3 user stories, 9 FRs, 5 SCs, zero GLP language surface (FR-006)

## [v2026.08.11.1] - 2026-08-11

### Added
- US3 T020 DECISION.md (ADOPT-WITH-CONDITIONS, BC-1..BC-4 enumerated) + T024 REPORT.md SC-002-closed cross-refs
- US2 T015 per-construct coverage floor complete — op_forms.glp closes op-as-functor/neg gap; all B-boxes ticked w/ cited IL/parse coverage; +dynamic_dispatch 4/4 (# / imported)
- US2 T014 expanded corpus — sweep tests/typed(70/71) + lib(8/8) + typed_book(175/223), 0 un-caused divergences; all rejects bounded BC-1 (SC-002)
- US2 T016 DEC U3 — mod-functor lexer predicate (Gabi+Udi approved); mod(...) call-form parity MATCH, 7/7 corpus + fuzz unregressed
- US2 T018/T019 DEC F3 — scope fuzzer non-cyclic =; full-budget fuzz 10000 clean, 0 un-caused divergences (SC-003)
- US1 IL-parity bridge — 7/7 byte-identical IL (SC-001)

### Fixed
- untrack glpquick-cert key material + OTP floor instead of equality pin

### Changed
- Merge pull request #147 from olamni-glp/069-sc-002-il-parity-bridge
- Merge remote-tracking branch 'origin/develop' into 069-sc-002-il-parity-bridge
- test updates
- Merge pull request #146 from olamni-glp/075-backlog-cluster-3rtask
- Merge pull request #145 from olamni-glp/074-roadmap-sync-postimplement
- regenerate cross_runtime parity result banners (build-hash + cwd-casing) from T022 run
- sync export (19 epics/102 features/2795 lines) — +GLP compiler robustness epic w/ occurs-check + guarded-traversal features (3rtask run 20260811T085855Z-8d6f); dedup-first dep
- roadmap: backlog root-cause sweep - 3rtask run a625 clusters 15 actionables into 7 features under new epic, 24 no-feature dispositions, export 19/107/2856 published 112740Z
- 3rtask: backlog-cluster corpus slices + manifest (research run input)
- T022 regression — 0 failures across all executed sections (A/B/C 221/110/50); run stopped in known-flaky late multi-isolate; production untouched (FR-010 held)
- T023/T024 — correct quickstart corpus usage (referenced-in-place, --project form) + mark T014-T024 done in tasks.md
- T021 xUnit — FirstDiff localization (5) + fuzzer determinism (6) + DEC F3 non-cyclic-= invariant (1); 12/12 pass
- roadmap: sync round - import gavriella 230104Z + ariellas 174815Z (2 lines), reconcile in sync, 0 live dupes, replay-verify OK, export 18/100/2762 published 235444Z
- wip(069): US2 T017/T018 fuzzer+wiring+ResultsWriter upsert; fuzz surfaced engine SO bug F-069-1 (blocked)
- Merge pull request #144 from olamni-glp/073-roadmap-sync-postqueue
- roadmap: sync round - import gavriella 055225Z (0 lines, converged), reconcile in sync, replay-verify OK, export 18/100/2760 published 174815Z
- Merge pull request #143 from olamni-glp/072-3rtask-808a-dispositions
- 3rtask 808a disposition round - staging adopted, O1-O7 worked, O1/O2 rulings proposed
- Merge pull request #142 from olamni-glp/071-postship-roadmap-sync
- roadmap: sync round post-ship v2026.08.10.1 - 7 peer files/0 lines (converged), 0 live dupes, export 18/100/2760 published
- Merge pull request #141 from olamni-glp/main
- Merge pull request #140 from olamni-glp/release/v2026.08.10.1
- release: v2026.08.10.1
- Merge pull request #139 from olamni-glp/069-tracked-key-remediation
- merge: origin/develop into 069 (union import manifest)
- roadmap: sync round - import 1 peer file/0 lines (converged), 0 live dupes, export 18/100/2760 published
- roadmap: sync round - 0-delta import (fleet converged), reconcile in sync, 0 live dupes, export 18/100/2760 published
- roadmap: sync round (import 2 files/15 lines), 0 live dupes, export 18/100/2760
- roadmap import manifest
- roadmap: sync round — import 6 peer files (5 lines, 0 dupes), export+publish 18/100/2760
- analyze(069): apply remediations — corpus dir names (typed_book not book), T016 §1.14 re-confirm
- tasks(069): 24 tasks across 6 phases, organized by US1/US2/US3
- plan(069): impl plan + research + data-model + contracts + quickstart
- spec(069): clarify corpus floor, mod-functor fix-first, bounded fuzz budget
- spec: SC-002 IL-parity bridge — parse-tree->engine lowering + adoption decision (069)
- roadmap: sync round after promotes (5 promoted), 0 live dupes, export 18/99/2745
- Merge pull request #138 from olamni-glp/main

## [v2026.08.10.1] - 2026-08-10

### Fixed
- untrack glpquick-cert key material + OTP floor instead of equality pin

### Changed
- Merge pull request #139 from olamni-glp/069-tracked-key-remediation
- merge: origin/develop into 069 (union import manifest)
- roadmap: sync round - import 1 peer file/0 lines (converged), 0 live dupes, export 18/100/2760 published
- roadmap: sync round - 0-delta import (fleet converged), reconcile in sync, 0 live dupes, export 18/100/2760 published
- roadmap: sync round (import 2 files/15 lines), 0 live dupes, export 18/100/2760
- roadmap import manifest
- roadmap: sync round — import 6 peer files (5 lines, 0 dupes), export+publish 18/100/2760
- roadmap: sync round after promotes (5 promoted), 0 live dupes, export 18/99/2745
- Merge pull request #138 from olamni-glp/main

## [v2026.08.05.1] - 2026-08-05

### Changed
- Merge pull request #136 from olamni-glp/068-abandon-stub-cleanup
- .specify updates
- complete abandon.dart removal — delete dead throw-stub + drop its unused import from runtime.dart (0 call sites; analyze clean; REPL 547/547 green)
- reconcile codeconv inventory for retired abandon.dart — rm 3 stale artifacts (tombstone/plan/spec) + drop 2 dangling graph edges; PGLite inventory empty (no DB row to retire)
- roadmap: sync round (import 2 files/0 lines), 0 live dupes, export 18/99/2740
- roadmap: sync round (import 8 files/0 lines), 0 live dupes, export 18/99/2740 published 181033Z
- SHIPPED+CLOSED handover — v2026.08.04.2; open follow-ups (066 Dart+codeconv, SC-002 PREP, tracked keys, reconcile defect)
- Merge pull request #135 from olamni-glp/main

## [v2026.08.04.2] - 2026-08-04

### Fixed
- commit ANTLR-generated gen/ so the spike harness builds from a clean checkout (codexreview HIGH: gen/ was gitignored but Harness.csproj compiles it — SC-001 now reproducible-from-source w/o jar/Java)
- remove dead AbandonOps stub (US2/Scope B) — abandon is anon-writer discard (062 US5)

### Changed
- Merge pull request #133 from olamni-glp/065-glp-runtime-consol
- Merge remote-tracking branch 'origin/develop' into 065-glp-runtime-consol
- .specify updates
- impl(065): US1 antlr4 grammar spike T010-T017 — faithful Glp.g4 (§1.14-approved) + generated C# parser + coverage harness (SC-001 7/7 parity) + REPORT (GO-WITH-CONDITIONS, SC-002 IL-bridge deferred); REPL 547/547
- sync round — import 28 files/164 lines (guid-union, 0 re-seq), converge to 18/99/2740, export+publish olamnit; replay-verify OK
- bk-close retro mirror (5 findings) + post-ship roadmap advance to closed + export 18/99/2740
- Merge pull request #132 from olamni-glp/main
- mid-pipeline restart handover — US2 done, US1 at §1.14 gate, codex fixed, 066 F1/F2 review
- spike(065): US1 antlr4 prep (T007-T009) + STOP at T010 §1.14 gate — written owner proposal, awaiting Gabi+Udi
- tasks(065): glp-runtime-consol — 17 tasks, US2 first (no gate), US1 spike (T010 §1.14 gate)
- plan(065): glp-runtime-consol plan + research + design artifacts (spike additive, IV-a gated)
- spec(065): glp-runtime-consol — antlr4 grammar spike + abandon dead-stub cleanup

## [v2026.08.04.1] - 2026-08-04

### Changed
- Merge pull request #130 from olamni-glp/064-post-wave-gap-closure
- roadmap: sync round (import 7 files/3 lines), GEPA feature scored+promoted, export 18/99/2737 published; T040 escalation determinations recorded
- Merge remote-tracking branch 'origin/develop' into 064-post-wave-gap-closure
- impl(064): T040 codexreview complete - 11 fixes, 2 regressions caught, 4 escalations, sweep green
- refine(codexreview): cycle 3 regression fixes - accept-fault classification+pacing, bridge bind-gate race
- refine(codexreview): cycle 2/3 [diff/general]
- cross-runtime parity harness run outputs (T029 evidence)
- roadmap: sync round (import 2 files/20 lines), 0 live dupes, export 18/99/2655 published 214119Z
- roadmap: sync round (import 2 files/0 lines), export 18/98/2635 published 210353Z
- impl(064): T037 final zero-regression sweep green (REPL 381, CS 360, gleam 618)
- impl(064): T039 roadmap rider - 6 Full-Gleam rows closed, antlr4 superseded (G5), protocol feature captured; export 18/98/2635 published
- impl(064): T038 DEFERRALS register (9 durable deferrals, gates named)
- impl(064): T020 MVP gate review PASS (US1 rescoped + US2, evidence table, 2 recorded partials)
- impl(064): Option-B ruling encoded - US1 rescoped, FR-001/002 transferred to new distributed-unification-quiescence-protocol feature (captured on roadmap), contracts bannered, T014 checkpoint
- impl(064): T032 US4 checkpoint green (gleam 618, all C# suites; sweep flags recorded)
- impl(064): T031 059 acceptance sweep (9 discharged-by-064, 7 earlier, 23 recorded deferrals, 5 flags for engineer)
- check off T029
- impl(064): T029 cross-runtime FE/BE smoke both directions (wire clean; RESULT-binding rendering divergence characterized)
- check off T026-T028
- impl(064): T026-T028 gleam FE/BE process split over ported split-protocol wire codec (gleam 618, two-OS-process smoke verified)
- check off T030
- impl(064): T030 glp_embed load/run/observe surface (G3-A, gleam 597, host-only test)
- impl(064): T019+T036 checkpoints green (link 171, il 64, host 73, wire 6, split 46, gleam 591; serial-suite rule recorded)
- check off T033
- impl(064): T033 gleam :boot via T039 boot loader (gleam 591, live-verified two-agent play)
- impl(064): T017 EngineServer multi-client mode (opt-in, 061 single-client preserved) + T018 suite (engine_host 73; A31 GLP-merge recorded partial, 1.14-gated)
- impl(064): T025 US3 checkpoint review note (3 recorded deviations), tasks checked
- impl(064): T021-T024 IL request kinds, client-side compile+ship, compiler-free IlExecutePath, split_protocol.tests 46 (corpus equivalence 12/12)
- check off T012 in tasks.md
- impl(064): T012 QUIC-WS bridge acceptor on glp_quick_host + gleam bridge_client dial helper (glp_link 171, gleam 588)
- impl(064): T034 bytecode lint (v2.16 operand+phase checks) + T035 param_arity panic to typed error per F1 ruling (gleam 588)
- impl(064): T010 gleam multi-accept transport + T011 suite (gleam 572, per-accept nonce, exit_on_close inherited, zero regression)
- impl(064): T015 TcpTransport.AcceptLoopAsync + T016 ClientSession/RoutedReply (glp_link 168, engine_host 69, zero regression)
- impl(064): T001 baselines green (381 REPL, 298 CS, 569 gleam, 206/206 corpus; Section I 12/18 host deviation recorded) + T002 parity checklist
- roadmap: sync import 2 files (2 lines), export 18/98/2614, published 153819Z
- analyze(064): 0 critical, top-3 remediations applied (split_protocol.tests creation, scan self-mention, lint anchor)
- tasks(064): 41 dependency-ordered tasks across 5 stories, MVP gate US1+US2
- plan(064): implementation plan + research + data-model + 4 contracts + quickstart
- clarify(064): four engineer rulings encoded (full C# parity, bridge route, build FE/BE+embeddability, US1+US2 MVP)
- roadmap: sync import 7 files (44 lines), export 18/98/2612, published 144831Z
- glp-runtime-consol pipeline restart handover (3rtask gap-audit seed)
- spec(064): post-wave gap closure specification (3rtask-verified inventory)
- roadmap: close 12 wave-4/062+050-delivered features (3rtask hygiene); export + publish olamnit__glpnet__20260803T140210Z.json
- roadmap: rebuild import-manifest after rebase (glp-runtime-consol)
- roadmap: add+promote glp-runtime-consol (3rtask gap-audit seed); export + publish olamnit__glpnet__20260803T135406Z.json
- roadmap: close waves 2/4/5 per engineer directive, sync import 5 files (8 lines), export 18/95/2551
- roadmap: sync import 4 files/5 lines (incl 064 spec-link) + export, published olamnit__glpnet__20260803T132333Z.json
- roadmap: advance released waves 2/4/5 -> closed; export + publish olamnit__glpnet__20260803T131923Z.json
- roadmap: rebuild import-manifest mirror from catalog after rebase
- roadmap: sync import 4 peer exports (9 lines) + export 18/95/2540, published 122835Z
- Merge branch 'develop' of https://github.com/olamni-glp/GLPNET into develop
- roadmap: sync round - import 1 file/4 lines (wave-2 + wave-4 advanced released by peers), 0 duplicates, export 18/95/2540 published 122743Z, replay-verify OK
- roadmap: sync import 4 peer exports (13 lines), repair wave-2/4 state regression, export 18/95/2540
- roadmap: manifest row for own 121535Z export (verification round, zero journal delta)
- roadmap: post-ship sync round - import 1 file/0 lines, reconcile clean, 0 duplicates, export 18/95/2536 published 121535Z, replay-verify OK
- post-ship close-out retrospective (3 findings incl exit_on_close root cause, nothing to reconcile)
- Merge pull request #129 from olamni-glp/main

## [v2026.08.03.1] - 2026-08-03

### Added
- T043-T048 cross-runtime Gleam x C# suite 18/18 + string display parity fix + run_all_tests.sh Section I wiring
- wire link kernels into Gleam engine - K1/K6/K7 + payload codec + link-aware run loop, gleam test 563 green
- C1 link_terms + transport_registry Gleam port (contracts/link-primitives-port.md)
- T039 multiagent boot loader - AST-native spawn-directive extraction (no Dart regex/strip), per-agent MadEngine boot with net-in slot, drive loop routing messages to Receive; 4 tests; 563 green, corpus 206/0. US4 COMPLETE
- US4 L2-L4 - link primitives per amended contract: capability gate, handle, registry, establish core (verify-before-act, either role), egress (window+sequence+frames), pump (parse-reassemble-order, rules 2/4/5), fault lattice + bounded-silence <=30s; 12 tests over loopback+TCP; 559 green, corpus 206/0
- US4 L1 - reliability state machines ported (link_sequencer, inbound_ordering, frame_reassembler, send_window) per amended contract; roadmap import applied 2 peer export files; 547 green
- T030+T031 - determinism verified (two identical full runs), SC-001 gate enforced in corpus runner (100pct 206/206, zero exceptions), recorded in research.md
- T028 named divergences in run_differential.sh + T017 dissolved by ratified lint disposition; C# REPL empty-output condition surfaced
- T025+T026 - three-verdict corpus report per contract, per-case verdict lines with expected/observed, aggregate block, completeness invariant P+F+O==N enforced exit 3; full run 206/206/0/0
- T011+T016 - late resolution both arms proven (call-time resolution per FR-008, Dart parity), multi-module co-load, duplicate first-load-wins, re-load replacement; 547 green
- T009 project static linker - discover/ancestor-scope/type-check-each/link/compile per Dart loadProject, facade load_project, Section F oracle plays 1-7 + fplay1/2/4 green; 542 green, corpus 206/0
- dispatch B3 - embedded serve/2 via compile_prelude, auto-activate exported modules per run, Distribute/Transmit as data-threaded channel sends, program import table; Section L oracle L1-L3 green; 537 green, corpus 206/0
- dispatch B2 - _activate/2 kernel emits RemoteSpawn as data, runner threads Reduced.remote, scheduler resolves vs module registry + 7 dispatch tests; 534 green, corpus 206/0
- dispatch B1 - scheduler module registry, per-goal programs, infrastructure goals, channels + channel_send/activate_module seams; 527 green
- US2 T019-T024 - :bytecode/:bc full-program dump per Dart reference, :boot deferral to G9, session-safety + read-only tests; contract amended to reference; 527 green, corpus gate 206/0 held
- T012+T014/T015/T018/T018a US1 remainder - name-keyed load registry with replace-in-place, 6 verification tests; 521 green, corpus gate 206/0 held; T013 lint disposition proposed
- T007/T008 engine transport-injection seam - with_transports/transport_for on the composition root + 2 tests; gleam test 515 green
- T005/T006 _copy/2 kernel - faithful port of Dart copyKernel + 5 gleeunit tests; gleam test 513 green

### Fixed
- exit_on_close false on gen_tcp sockets - OTP default auto-closed the whole port on peer FIN, killing undrained egress (D-9 root cause c); repro 8/8 green
- pump holds socket open until link terminal - release-subject exit gate; pump exit on peer FIN was closing the socket under undrained egress (D-9 root cause)
- D-9 run-termination barrier - peer half-close no longer terminal alone (in_ended flag), should_wait holds until close handshake completes, fixes graceful-close truncation race
- Section I gate uses SCRIPT_DIR-anchored paths - suite cwd is glp_runtime, relative check always skipped
- sender close is half-close not link death - Gleam x C# pc.glp passes BOTH directions, gleam test 563 green
- mirror Dart path-ish resolution in load (drive letters, backslash, embedded separators, quoted paths) - relative/absolute loads no longer mangled with glp\ prefix; three-way differential AGREE
- repair UTF-8 double-encoding + BOM introduced by PowerShell Set-Content rewrites in engine_test, output_capture_test, tasks.md
- CR-tolerant parity-harness parse + LF pin for test/parity - resolves 059 T051 (44 false MISSING goldens were CRLF-corrupted block ids)

### Changed
- Merge pull request #127 from olamni-glp/060-wave3-full-gleam-chain
- roadmap: sync round - import 5 peer files/29 lines, reconcile clean, 0 duplicates, export 18/95/2536 published 143832Z, replay-verify OK
- case-insensitive path check - NTFS canonical case made the self.glp load-path check cwd-dependent (false FAIL from canonical-case cwd)
- Merge origin/develop (post-062) into 060: keep-060 pointers, additive gitignore+suite sections, develop manifest, engine.gleam integrates 062 conjunction drain_goals with 060 link-aware per-goal drive
- Merge branch 'develop' of https://github.com/olamni-glp/GLPNET into develop
- post-ship close-out retrospective (3 findings, token ledger reconciles, marathon discharged 9/9)
- roadmap: sync import (5 files/30 lines) + export 18/95/2531, published 092034Z
- Merge pull request #126 from olamni-glp/main
- olamnit 160428Z zero-delta import verified - fleet re-converged 18/94/2493, dedupe wave closed
- sync cycle - imported ariellas dedupe merge (+1 line, quic-link twin merged), reconcile no-op, converged 18/94/2493, re-exported + published
- milestone-2 snapshot - R3 ACK-CONVERGED fleet-wide 18/95/2492, F1 round closed (R1+R2+advance-directive+R3 complete on all three hosts)
- import-manifest update for 145518Z fold import
- operator advance-directive fold - imported ariellas 145518Z (+2 lines, wave-3 closed), converged 18/95/2492, re-exported + published; R3 triple fixed, olamnit sole open item
- R2 - imported olamnit's 40-line delta, converged 18/95/2490, re-exported + published; R3 awaits peer R2 completes
- untrack live COOP mailbox (gitignored per operator ruling - no in-branch logic for live dialogue) + milestone snapshot 1: PROTOCOL-DRIVES v1 adopted, gavriella-ariellas verified converged
- stage 2 vs ariellas - imported 163 lines, converged 18/94/2450, re-exported; olamnit stage 1 still open
- commit local mailbox mirror updates (seq 29 + PROTOCOL amendment, advanced by a concurrent session on this host)
- COOP protocol portable write-up - three-host drive-letter law, identity-by-hostname, CRDT action dialogue, sync stage gating (for yngenios-windows adaptation)
- tick T039 - US4 complete
- tick T032-T038, T040-T042 - US4 link primitives delivered per amended contract
- sync stage 1 - import 2 peer files, reconcile no-op, shipped/released already closed, merged 2 umbrella dupes into wave-3-consolidated, export
- spec(060): amend link-handshake contract to reference mechanisms per owner ruling - frame-version rejection, capability gate, path-A/B establishment, reasoned refusal; rules 1-7 re-anchored
- research(060): US4 dossier - link-layer subsystem map + SPEC CONFLICT: link-handshake.md Hello/Accept/Refuse absent from both references (frame-version byte + capability gate + path-B rendezvous are the real mechanisms); ruling requested
- tick T050 - non-regression set green: gleam 547 (baseline 508), REPL 532/532
- tick T009 - static linker delivered, Section F oracle green
- research(060): T009 dossier - project static linking subsystem map, Dart loadProject chain to Gleam loci, skipGlobalSRSW is reference-sanctioned, F oracle plan
- tick T010 - dynamic dispatch delivered as B1-B3, locus per G1 dossier, Section L oracle green
- tick T013 - lint disposition ratified via marathon trace T013-lint-disposition
- research(060): concrete Gleam mapping for module dispatch - sentinel struct not Term variant, per-goal programs, _activate as data-threaded spawn, 7-point plan
- research(060): G1 design dossier - module dispatch subsystem map (ModuleTerm/serve/channels/_activate), corrected locus vs tasks.md
- baseline(060): final T003 corpus baseline 206/0 - 100pct agreement post CRLF fix, clean verified run
- spec(060): revise FR-018a/FR-018b/SC-010 + T027/T028a/T029 per Bug Protocol ruling - harness defect, nothing regenerated
- baseline(060): T001-T004 checkpoint-zero - gleam 508, REPL 532/532, corpus rc=44 root-caused to CRLF harness artifact (ruling pending)
- tasks(060): close analyze findings C1/B1/A1/U1 - add T018a writer-MGU + T028a bug-protocol gate, harden T031 and T042
- correct marathon resume flag in restart pointer (--feature, not --run)
- persist analyze findings + refresh restart pointer for /bk-implement handoff
- tasks(060): 52 tasks across 8 phases, one per user story, MVP = US1
- plan(060): implementation plan, phase-0 research, data model, quickstart, 3 contracts
- clarify(060): resolve 4 scope questions - grammar, transports, AtomVM, corpus goldens
- spec(060): wave 3 consolidated Full Gleam chain - spec.md + requirements checklist

## [v2026.08.02.1] - 2026-08-02

### Added
- Gleam REPL conjunction-query MVP capability (parity w/ Dart; gleam 514/514, +6 tests)

### Changed
- Merge pull request #124 from olamni-glp/062-wave-4-consolidated-parallel-safe-fillers
- Merge remote-tracking branch 'origin/develop' into 062-wave-4-consolidated-parallel-safe-fillers
- COOP updates
- roadmap: sync import 7 peer exports (9 lines), export 18/95/2523, published 083957Z
- Merge pull request #123 from olamni-glp/main
- ship-prep — T001 closed, /bk-analyze clean (0 critical, 100% coverage), safe-restart handover
- T037 final full-suite sweep GREEN across all runtimes (REPL 546, Gleam 514, C# green); ship gated
- Phase 8 T036 fleet UPDATE posted + (b) Gleam delivered (514/514); T034 ledger updated
- Phase 8 T034 terminal-state ledger (SC-008) + T035 codify win note
- impl(062): US3 T020-T022 — NetMQ ZMQ transport (PAIR base) + envelope-over-zmq execute-on-B; il_codec 64/64, link 161/161
- impl(062): US3 T019 — role-aware Loopback multi-accept (>=2 clients, distinct links; glp_link 154/154)
- impl(062): US3 T017-T018 — receiver execute-on-B==local + hardening (il_codec 61/61)
- impl(062): US3 T015-T016 — compiled-IL wire envelope (il_version/digest/source_meta over IlCodec), il_codec 51/51
- US5 pins T031-T033 — REPL A32+SectionC, engine unit, 3-way parity 0-diverge (REPL 546/546, engine 11/11)
- US5 parity confirmed (Dart/C#/Gleam) + US3 mapped; fork=both, baselines captured
- safe-restart handover — US1/US2/US4 done+green, US3/US5 gated on 2 operator rulings
- impl(062): US1 depgraph mark-and-recompute+trends, US2 studies, US4 control program (US1 66/66, US4 538/538 green)
- tasks(062): 37 tasks across 8 phases, by user story; US5 §1.14 proposal-gated
- plan(062): impl plan + research + data-model + quickstart + contracts; constitution PASS, §1.14 semantics external-source-gated
- clarify(062): resolve US3=hardened runtime capability, US5=§1.14 approve-and-implement (operator approval 2026-07-29)
- spec(062): Wave 4 consolidated parallel-safe fillers — 11-item wave spec, §1.14 items gated/deferred

## [v2026.07.31.2] - 2026-07-31

### Changed
- Merge pull request #121 from olamni-glp/063-wave-5-consolidated-captured-triad
- Merge origin/develop into 063 (resolve buildkit feature-pointer conflicts, keep 063)
- bk-close retro mirror + post-T041 roadmap export + trust key refresh
- T041 complete - wave feature released, four consolidated features closed with receipts, quickstart verified; 41/42 (T030 key-gated)
- Merge pull request #120 from olamni-glp/main
- impl(063): roadmap-sync import manifest + host trust key from T030 sync round
- impl(063): T030 wave-close - triad advanced to closed with receipts, exports 060746Z+061228Z published to fleet (18/94/2502), UPDATEs fanned
- impl(063): T029 closure evidence + T033 full re-verify - CLOSURE.md links E1/E2/PROTOCOL; suites REPL 534, glp_link 156, glp_quick 188, ms_message 36, drill 5/5; T030+ship parked on engineer keystroke
- impl(063): T028 engagement E2 + fix pass - 3-blind code review found 6 real US1-diff defects, all fixed (silent-stall, block misattribution, dup-reply race, unlocked stdin, mesh-before-spawn, injection/trailing-parse) + cp-02/cp-07 + st-08 test; glp_link 156/156, glp_quick 188 passed
- impl(063): T027 engagement E1 complete - 3-blind-builder plan review, codex critic 32C/2R/16E, 0011/0012 staleness conflict found and fixed, 6 spec-improvement escalates open for engineer
- impl(063): T031-T032 polish - quickstart US1 CLI drift corrected, gitignore coverage verified
- impl(063): T026 three-role PROTOCOL.md - operator runbook distilled from method doc + spec-051, references not duplicates
- impl(063): T025 US2 complete - QUIC-leg drill over real link 100/100 exactly-once, build_fetch_batch extracted carrier-agnostic, suite 36/36, TCP drill 5/5
- impl(063): T016-T024 US2 core - WAL+store+dlq+lake+roles, unit 35/35, SC-004 drill 5/5 at N=1000, Section S wired, batch-transaction fix for bridge contention
- impl(063): T014-T015 US1 complete - demo suite PASS per-criterion, 18/104 superseded by 187+156 table, profile wording corrected at authoritative site
- impl(063): T011-T013 live REPL bridge - host spawns REPL child, tmsg repl_goal/repl_result over the link, both directions proven over real QUIC in 9s, 187 passed no regressions
- impl(063): T009-T010 mesh_dup_id closure witness - 4/4 pass, symptom not reproducing, provenance recorded, mutation check proves witness, glp_link.tests 156/156
- impl(063): T007-T008 audit divergence reconciled - the 9 dll-gated modules are glp_quick pytest tier, build closes the gate, 185/2F/1S verdicts recorded, quicer NIF root token + teardown leak reported
- impl(063): T001-T006 setup+foundational - ms_message scaffold, msmesh migration 0012 (head 0011->0012, deviation recorded), protocol shapes 22/22, migration family 15/15
- impl(063): T004 baseline recorded - host builds clean, glp_quick 185/2F/1S (pre-existing profile-C fails), glp_link.tests 152/0 skips (audit divergence flagged for T007/T008)
- analyze(063): top remediations applied - C1 token in plan gate note, FR-011 failure-path coverage, SC-001 timing assertion, T007 skip-mechanism honesty, FR-009 wording
- tasks(063): 33 tasks across 7 phases - US1 MVP 9, US2 10, US3 4, US4 1; deps + parallel lanes + FR-015 discipline
- plan(063): full plan artifacts - R1-R10 research, msmesh data model, 3 contracts, quickstart; constitution gates PASS
- clarify(063): all markers resolved - mesh-fix baseline + US3 scope from records, wire-carriage + first-hop boundary engineer-accepted, US2 aligned to intake brief
- spec(063): wave-5 consolidated captured triad - 4 user stories, 15 FRs, 2 clarify markers carried to /bk-clarify

## [v2026.07.31.1] - 2026-07-31

### Changed
- Merge pull request #118 from olamni-glp/061-wave-2-consolidated-repl-engine-split-spine
- refine(codexreview): cycle 3/3 [diff/general]
- refine(codexreview): cycle 2/4 [diff/general]
- impl(061): T041 partial - quickstart verified end-to-end with real binaries, load-path form corrected; roadmap advance stays blocked on GitFlow ship
- impl(061): polish T036-T039 - R8 metric tables (R14 rows), machine-check scan 0 violations, full-suite diff zero regression, DEFERRALS A3/D1/D2/E1/E2/F2 done(061), F1 delivery annotated
- impl(061): US4 restore-resume - RewireHandle adopt path, 0x09 role, LinkRewirer restore-order gating, FR-033 kill-and-restart deterministic, TLA+ TLC PASS, 62/62 + REPL 532/532
- impl(061): US3 supervision - BackgroundService supervisor, crash log, DEF-F2 taxonomy, DEF-F1 memo, 55/55; UPPAAL model authored (verdict license-blocked)
- impl(061): US2 snapshot + persistence - quiescence-gated capture/restore, two-backend store, 52/52 + REPL 532/532
- impl(061): T040 Anchor-A MVP-gate review - PASS, DEF-A3 discharged
- impl(061): US1 split MVP - engine host, thin client, parity corpus, SPIN full-protocol PASS
- impl(061): Phase 1+2 - four project skeletons, wire protocol, codec + tests, suite baseline
- analyze(061): apply top remediations - shared protocol lib, SC-004 probe test, mid-restore test
- tasks(061): 41-task dependency-ordered breakdown for wave-2
- plan(061): wave-2 engine split spine implementation plan
- spec(061): clarify session - 4 answers integrated, markers resolved
- spec(061): wave-2 REPL engine split spine specification
- fleet sync 0-delta import + QUIC-link twin dedupe merge - export 18/94/2493 published; retro report 050
- Merge pull request #117 from olamni-glp/main

## [v2026.07.29.1] - 2026-07-29

### Added
- network-callable S4 mint/attenuate policy endpoint + macaroon-v2 (T035)
- async-capable capability-gate variant beside the sync one (T034)

### Fixed
- ZMQ transport robustness (codex review) - #1 bounded establishment handshake (0x02) on both ends mirroring tcp bounded-connect so a never-appearing peer is a fault not a blocking endpoint; #2 erlzmq-absent returns erlzmq_unavailable not undef-crash; #3 malformed frame emits LinkFaultSignal not silent EOS; gleam 508/0
- plan.md Constitution Check - avoid reproducing forbidden literal scan tokens (III/V) so artifacts don't trip their own machine-checkable gate

### Changed
- Merge pull request #115 from olamni-glp/059-full-scope-gleam-glp-implementation
- verify(059) Wave-2 batch 2/2: T052 platform-atomvm (DELIVERED by-construction), T054 proofs (PI:14 DISCHARGED lake-green, PI:17 undischarged expected), T051 parity (harness OK but HALT/ESCALATE: corpus rc=44, 44 missing-goldens = evidence-reproducibility drift, engineer decision needed)
- verify(059) Wave-2 batch 1/2: T039 acceptance-sweep (DELIVERED-as-verify, capstone unstarted), T049 module-scope-chain (ABSENT, single-root-prelude only), T058 transports (PARTIAL; multi-accept/quiescence ABSENT, frame-hardening DELIVERED, ZMQ premise superseded->in-contract)
- rule(059) C: rule-quic-sideprocess-relay RESOLVED (Gabi 2026-07-27, Disposition 2) - minimal in-corpus relay smoke test required before any Wave-4 QUIC dependent; new escalation-register.md entry + enforcing WP T098 close-quic-sideprocess-relay-smoketest gating T084-T086; rulings.md updated; T036 RULED
- impl(059) D+E: AtomVM gated probe RUN on OLAMNIT (release v0.7.0-alpha.1 wrapper, sha256-verified) -> PASS byte-identical (T021 complete, 3rd independent AtomVM confirmation); gitignore .specify/3rtask/runs evidence dir
- docs(059) Wave-2 reconcile: mark 12 ruled rule-requests + 15 committed verify verdicts done (evidence-pointered to rulings.md / phase2-verify); research.md marathon run-id note (scoping mrun-8bda036d9e9b vs execution mrun-7e6cfbf0a9fb)
- impl(059) Wave 1 COMPLETE: freeze-interface register (16 entries @49b52342) + 5 guard tripwires verified green (Gleam 508/0, REPL 0-fail, C# glp_link 152/0); 21 WPs + 4 setup done, 76 remain (waves 2-5)
- tasks(059): tasks.md - 97 tasks (4 setup + 90 WPs wave-ordered + 3 polish), each WP tagged to its user story; embeddability escalation marked RESOLVED per spec clarification
- plan(059): /bk-plan artifacts - plan.md (Constitution gate PASS) + research/data-model/quickstart + frozen-interface/parity/service-box contracts, composing the 90-WP/5-wave FINAL plan
- merge(059): sync develop into 059-full-scope-gleam-glp-implementation
- wip(059): COOP session artifacts + 3rtask run evidence (ring/mesh findings, kv backups, planning evidence)
- ZMQ transport leaf (owner ruling) - zmq.gleam + glp_link_zmq_ffi.erl behind T045 seam + link_scheme.zmq(); Windows-native compiles green, baseline 465 intact; runtime WSL-provisioned via profile_zmq/
- wave2(059): record ZMQ ruling — G5 zmq-comm-base OVERRULED by owner, ZMQ mandatory/in-scope, transport contract extended to loopback/TCP/QUIC/ZMQ (DISCIPLINE 1.14 owner-approved)
- wave2(059): verify-quicws-link-completion-live-repl-bridge verdict (b3-c1-009) — engine-side QUIC-WS ABSENT (T055 open, no quic_ws.gleam); websocket-framing PRESENT in gleam_quic/glpq_quic.erl but unwired; profile-c-quic-acceptance ENV-BLOCKED not code-absent (WSL quicer 0.2.15 build-hook fails, no MSVC on Windows); quic-host = C#-only (no Gleam role, spec Q1); mesh out-of-scope (G5); live-repl-bridge captured residual → activates close-quicws-link-completion-live-repl-bridge
- wave2(059): verify-link-inbound-pump verdict (b3-c1-008) — per-sublayer boundary: link-seam + link-transport-seam (loopback/TCP) DELIVERED, link-reliability PARTIAL (FrameCodec+CRC floor only), inbound-pump/link-acceptance/link-capability-gate/instance-network-join ABSENT; clean line = tasks T045-T049 done / T050-T058 open → activates close-link-inbound-pump
- wave2(059): verify-wireproto-crdt-convergence verdict (b3-c1-012)
- wave2(059): verify-bytecode-bytecode-instruction-set verdict (b3-c1-005) — bytecode-instruction-set + bytecode-mode-conversion DELIVERED (Op union discriminant-complete vs Dart production set + §2-14; is_reader polarity; runtime WxW via unify writer-MGU SC-004), bytecode-lint ABSENT (placeholder); FINDING: test_wxw.glp is SRSW-rejected at load (agrees only as reject-parity, not runtime WxW) → activates close-bytecode-bytecode-instruction-set (lint disposition)
- wave2(059): verify-compiler-antlr-shared-grammar-spike verdict (b3-c1-004) — 3 DELIVERED (parser-recursive-descent/compile-mode/strict-gate), 2 ABSENT (module-static-linking + module-dynamic-dispatch: Unimplemented distribute), reduce-metainterpreter PARTIAL (blocked by missing _copy/2), antlr superseded (G5) → activates close-compiler-antlr-shared-grammar-spike
- wave2(059): verify-febe-embedded-switch-role-framing verdict (b3-c1-013)
- wave2(059): record F1 ruling — param-arity panic fix lands under a shared type-checker-robustness close (not close-langsurface)
- wave2(059): verify-langsurface-channel-convention verdict (b3-c1-003) — 5/5 detail_ids DELIVERED (parity across 10 harness runs + decline-reader reject); FINDING F1: param_arity_mismatch panics on Gleam (program_dfa.gleam:580 panic vs Dart/C# graceful load-error) → activates close-langsurface-channel-convention
- wave2(059): verify-process-baseline-program-dossier verdict (b3-c1-019)
- wave2(059): verify-embed-embeddability-service-box verdict (b3-c1-014)
- wave1(059): guard-atomvm-gated-probe — corroborate on source-built main AtomVM
- wave2(059): verify-codec-compiled-il-on-the-wire verdict (b3-c1-011)
- wave2(059): verify-multiagent-multiagent-boot-loader verdict (b3-c1-007) — all 3 ABSENT (empty module); FINDING: named reference plays malformed on BOTH runtimes (| type-alt); activates close-multiagent-multiagent-boot-loader
- wave2(059): verify-engine-engine-composition-root verdict (b3-c1-015) — output-capture + reference-envelope DELIVERED, engine-composition-root PARTIAL (kernels compiled-in, no transport injection seam); activates close-engine-engine-composition-root
- wave2(059): verify-repl-repl-boot-command verdict (b3-c1-006) — :trace/:limit DELIVERED, :boot/:bytecode ABSENT; activates close-repl-repl-boot-command (engineer scope Q on narrow surface)
- wave2(059): verify-guards-guard-defined verdict (b3-c1-002) — guard-defined + guard-purity both DELIVERED (Dart/C#/Gleam parity); no close activation
- wave1(059): guard-atomvm-gated-probe — RESOLVED
- wave2(059): verify-runtime-arithmetic-expression verdict (b3-c1-001) — 4 DELIVERED / 3 PARTIAL / 1 ABSENT; activates close-runtime-arithmetic-expression
- wave1(059): register — mark guard-fe-be-envelope-seam RESOLVED (3ea7dde9), pin seam golden in codec-envelope protected list, Gleam floor 463->465
- wave1(059): guard-fe-be-envelope-seam — pin ED-1 seam bytes to golden corpus (b2-c1-001)
- Merge pull request #114 from olamni-glp/050-full-gleam-combined
- R1 supersession stamps - link-primitives PROPOSAL header superseded by rulings-log, arch-context ok(LinkId) superseded by self.glp:451 bare ok, tasks.md T050 authors-no-GLP scope correction
- Merge pull request #113 from olamni-glp/059-full-scope-gleam-glp-implementation
- clarify(059): integrate Q1 ruling - yngenios embeddability is FULL WIRING (Gleam engine as controller across all 4 spec-056 services), resolves rule-embeddability-api-yngenios-wiring
- wave1(059): fix AOT-smoke self.glp path regex for glpnet repo name (glp->glp(net)?); Dart oracle now 532/532 green, all four baselines pinned
- restore COOP bk-colab carve-out + drive topology to CLAUDE.md (lost again since ebc9da07) + live-vs-stale mailbox and prepend-dont-clobber discipline
- wave1(059): frozen-interface register (17 entries) + atomvm probe runbook + path-pointer; baselines measured Gleam 463/463, C# link 147/147, C# result-codec 131/131
- coop(seq27): open co-op dialogue with Olamnit on feature 059 full-scope Gleam GLP - mesh ownership collision, K5 acceptance-target finding, yngenios-003 wiring asks
- impl(050): T050.C0 link-primitives port scope breakdown - ratified surface + Gleam module map + oracle + deviation list
- impl(050): T050.A4b madGLP multi-agent parity - §10.1 client-monitor + §10.3 friend-intro in Gleam (mad_multiagent_test drives 2-3 real MadEngines over in-test routing loop = Phase-A stand-in for Phase-B mesh; run_to_quiescence drains M_p, deliver_all routes Message->receive; boot=network prelude over self.glp + appended agent clauses; §10.1: cold-call export reader Xs?->q serializer _r(p,1)+watching global_send, q localize entry, p client Xs:=[add] fires forward _r(p,1):=[add]->q, q Xs_q?=[add]; §10.3 3-agent 2-hop: bob exports X?->alice _r(bob,1) + X->charlie entry idx2 _w(bob,2), charlie X_c:=hi->_w(bob,2):=hi->bob, bob binds X X? known forwards _r(bob,1):=hi->alice, alice X_a?=hi - value flows charlie->bob->alice per exact spec §10.3 index/name/msg seq = Dart oracle; writer-assign via unit-clause-head not =; KNOWN-GAP: open-stream [add|Xs1] anon output-tail writer hits pre-existing void-slot gap, closed-list [add] same path; gleam 504->506 warning-free)
- impl(050): T050.A4a madGLP network prelude loads through Gleam pipeline (mad_prelude_load_test reads self.glp + system/mad_predicates.glp via file:read_file FFI, asserts loader.load Ok w/ global_send/3 + send_to_net/1 callable labels; full SRSW/PE/typecheck/compile - known(T?)-guard-non-multiplicity + ground(Q?) relaxation confirmed; gleam 503->504 warning-free; ESCALATION: send_to_ui/1 + _send_to_ui host kernel spec-only, absent from self.glp+programs/, needs Gabi §1.14 decision, out of network-prelude scope)
- impl(050): T050.A3 madGLP mad_engine.gleam wrapping scheduler.Engine in Gleam (s_p=(R_p,W_p,M_p): new/boot c0 permanent index-0 serializer entry spec-4.1; step=Reduce+Send-drain-M_p per-step returning List(Message) contract-shape; receive/3 = 3 Receive cases spec-8.3 faithful to Dart handleMadAssignment - serializer cold-call extend N_p / globalize-writer lookup-bind-remove / localize-reader search-bind-remove; Localize threads immutable heap + enqueues _w-branch global_send spawns; scheduler step_mad injects W_p reads Reduced.mad LOWERS reader-spawns to runnable global_send/3 goals drains M_p; +enqueue_global_sends/set_heap/bind_and_wake/alloc_local; missing global_send/3 or consumed-entry SURFACED not swallowed - dedup=T052 spec-v5.3-PURE; StepReduced + 3 call sites byte-identical; 7 gleeunit tests; gleam 496->503 warning-free)
- impl(050): T050.A2 madGLP effectful-dispatch seam + _send kernel in Gleam (RATIFIED engine-surface change §1.14; parallel MadOutcome not widened KernelOutcome; RunnerContext.mad Option(MadState) threaded out on Reduced.mad like output; _send serializer=[T↑|_w(q,0)]/normal=G:=T↑ globalizing T for Q via A1; MadAbort->Failed non-fatal path; wired at BODY Spawn label-miss mad_spawn; 8 unit + 1 reduce-level seam test; gleam 487->496 warning-free)
- impl(050): T050.A1 madGLP globalize/localize in Gleam (host-level term traversals over heap+W_p+counter; §5.1 writer=entry-no-spawn/reader=spawn-no-entry + §5.4 export-both-ends round-trip; faithful in-outcome to mad_helpers.dart; Gleam VarRef carries neither role nor pair -> read from heap tag; localize threads immutable heap for fresh pairs; 2 modules + 10 gleeunit tests; gleam 477->487, warning-free)
- impl(050): T050.A0 madGLP W_p foundation in Gleam (global_name _w/_r polarity + immutable global_writers_table + message; single never-reused counter, index-0 serializer, created-exactly-once; gleam 463->477)
- plan(050): T050 decomposed into madGLP Phase-A (T050.A0-A4)+Phase-B via /bk-3rtask run 20260714T072542Z-a84b; contracts/madglp-port.md; 3 escalations ratified (parallel MadOutcome+RunnerContext; spec-v5.3-pure; pure local-pair)
- spec(059): full-scope Gleam GLP implementation - spec.md + requirements checklist from FINAL phase2 plan and gate rulings G1-G5
- plan(fullscope-gleam): FINAL phase2 outline plan via cycle-2 resume - 88 CONFIRM, 0 blocked, 2 open escalations, dangling deps zero (run 20260719T134320Z-544f)
- research(fullscope-gleam): record engineer gate rulings G1-G5 (resume, multiagent, mesh/yngenios, unifyconstant parity, OOS proposals)
- upgrade buildkit integration artifacts to 2026.07.14.1
- roadmap: export after full-scope-gleam-glp-implementation scoping (enriched+promoted, dep on 050; phase1 inventory + phase2 plan committed)
- plan(fullscope-gleam): phase2 3rtask outline plan NON-FINAL - 66 accepted WPs waves1-5, 10 blocked, 3 escalations, 154/154 traceability (run 20260719T134320Z-544f)
- research(fullscope-gleam): phase1 3rtask gap inventory - 154 capabilities, 44 delivered / 9 partial / 99 gap-class / 2 escalates (run 20260719T130005Z-782b)
- research(fullscope-gleam): freeze roadmap snapshot 2026-07-19 as 3rtask phase1 evidence
- roadmap: sync w/ olamnit - import 0713 ynet export, dedup 5 twin features + 2 empty epic shells, export 20260719T123341Z
- Merge origin/050-full-gleam-combined (T045-T049 M2 wave) into local T043 programs
- T043 volume-run programs - 1000-ping mesh acceptance + loopback reproducer for the egress-drainer kill defect (0/1000, blocks SC-005)
- Merge pull request #112 from olamni-glp/058-s4-policy-service
- macaroon-v2 shared vector conformance - 22/22 (T037)
- impl(050): T049 US4 TCP transport (gen_tcp passive FFI; 4B BE length-prefix framing parity w/ Dart/C#; real-socket smoke; gleam 463/463)
- impl(050): T048 US4 loopback transport (in-BEAM hub+channel processes behind seam; FIFO/close/fault parity; gleam 460/460)
- impl(050): T046+T047 US4 FrameCodec+CRC32 Gleam port + parity (byte-parity vs Dart/C#; corpus.hex golden ride; gleam 456/456)
- M2 restart-prep — T045 done (mark [X]), handover banner: resume mrun-6bea075ec79e at T046
- impl(050): T045 US4 transport seam — port i_link_transport/ILinkEndpoint to Gleam (loopback|tcp|quic vocab; vtable seam below GLP)
- Merge pull request #110 from olamni-glp/main

## [v2026.07.13.2] - 2026-07-13

### Added
- E3pcCtrl frame kind - CBOR codec, must-understand section, reliability conformance (T034)
- quic_chat.glp — genuine full-duplex single-link chat
- ratify quic_chat reconciliation - full-duplex chat canonical, one-bind preserved as quic_chat_onebind.glp
- quic_chat.glp - two-way chat over per-message QUIC one-binds (loopback-verified)
- US4 mesh program + US5 graceful teardown + polish - quic_mesh.glp (crdtmsg/7 over quic, all links as GLP goals, SRSW-clean load), QuicTeardownTests T037-T039, known-issues + FR-019 audit (T032-T042,T045); REPL 526/527 (mesh loads, 1 pre-existing AOT baseline)
- US3 - macaroon gate (verify-before-act) on the quic link; ICapabilityGate seam + MacaroonLinkGate + slot-as-section-0x20 (D-2 resolved, no 041 codec change); T022-T028 green (129/129 xUnit, REPL 525/526 baseline)
- US2 - crdtmsg envelopes on the "quic" wire (CrdtMsgPayloadCodec + composition-root inject); T013-T021 green
- T011 form (b) system guard primitive satisfiable/2 - native clause-spec table in runner, builtin+analyzer registration, GLP_POLICY_GUARD_FORM toggle for SC-009; A29 form-a reference + A30 pure-b probe wired
- T009+T010 form (a) via ruled a1 runtime-defined guards - PE pass-through, codegen definedGuards side table, runner three-valued evaluator; wx1-wx4 + 12/12 vectors green; suite A29 wired
- US1 MVP - register genuine QUIC transport into REPL LinkRuntime (fail-closed cert loader, one-bind kernel-path tests, GLP program, REPL regression)
- US5 bounded remote test-control over the link - control agent+driver, fixed whitelist (no remote shell), loopback-proven, 184 pytest green (FR-017..019)
- US1 vectors.json SSOT + C# parity tests 124/124 + guard GLP sources + a1 runtime-defined-guard design (T005-T008)
- US2 Profile C - in-process BEAM QUIC client via quicer NIF, demo PASS equal to Profile A baseline (milestone: Profile C verdict)
- box partitioning + PGlite op-WAL + real MsQuic link adapter (buildkit spec-048 T011-T013)
- polish T033-T036 - SC-001 re-expression acceptance, determinism sweep fixes + repeated-run test, quickstart API accuracy, full re-test green (218/218 + substrate 6/6+86/86, zero substrate diff)
- US4 T028-T032 - CompatChecker rule table with NFA pattern inclusion, transitive chain check, refusal law, override registration (212/212)
- US3 T023-T027 - CDDL-subset parser, Lifter with per-construct fidelity + hash drift detection, DSL printer, round-trip equivalence (188/188)
- US2 T019-T022 - InstanceValidator (kind-structure-facets order, closed-world), crdt_message 043 re-expression, SC-003 corpus agreement harness (175/175)
- US1 MVP T012-T018 - canonical CDDL emitter, lowering+allocation, all-or-nothing registration, SC-002 defect suite, SC-006 walkthrough (142/142, substrate 6/6+86/86)
- T008-T011 DSL parser, schema validator (6 rule groups, all-errors-one-pass), compat records, seeded overlay registry skeleton (94/94 green)
- T004-T007 AST records, verdict/error records, restricted-regex NFA engine tests-first (62/62 green)
- implement (041) Polish - dual-DSL schema registry, GLP guard PROPOSAL (propose-only, §1.14 gate), parity vectors, docs; C# gates 253 green (T053-T057)
- implement (041) US5 routing+e2e - unified header, v2 additive cap slot, @name loud-fail, dedup, policy matcher, mesh demonstrator; SC-007/008/009 green (T043-T052)
- implement (041) US4 cap/sig - macaroon fail-closed + amulet slot + Ed25519 whole/sub-content seals + provenance; SC-005/006/011 green (T035-T042)
- implement (041) US3 MANDATORY rich-text - Fugue no-interleaving + Peritext unknown-mark preservation, op semantics/tombstone/delivery (T026-T034)
- implement (041) US2 store-first - op-WAL (040 shape) + rebuildable projection + Merkle anti-entropy; convergence+crash-rebuild green (T020-T025)
- implement (041) US1 MVP - TLV+4 surface codecs, loud-fail, version tolerance; 16-cell conformance matrix green (T010-T019)
- implement (041) foundational - wire registry (SC-010), abstract model, DVV/hash-chain, transport seam; T001-T009,T012 green
- E1-E9 rulings encoded in buildingblocks-synthesis section 6 + new feature crdtmsg-xsd-style-schema-language + export 20260704T072850Z
- F3 buildingblocks-synthesis delivered - 86 claims to 40 blocks, 9 escalations + roadmap 040 shipped/F3 released + export
- US9 Polish — SC coverage-map (SC-013) + help-completeness proxy + link_console parity + quickstart/help sync (T057-T060)
- US6 rcopy wizard — pure exclusion filter + run_transfer core + LinkProxy/ResponderSession over-the-link + /rcopy tui (T048-T053)
- US8 rcopy responder backend — file-WAL source-of-truth + rebuildable catalog/provenance, commit-on-complete, perm/quota/path (T039-T047)
- US7 user-bindable PF keys — BindingRegistry (free-key/PF13-24/Ctrl-alt/typed-equiv) + /bind live legend (T054-T056)
- US5 REPL-in-a-page — ReplBridge process bridge + /repl over link + agent-page /return (T034-T038)
- US4 joint pinpoint + masks/forms — joint.py/forms.py + /joint /pin /undo-pin /mask /fill wiring (T028-T033)
- US3 presentation — themes/OIA/splash to presentation.py, two-strip layout + /layout, reverse-video PF-legend (T024-T027)
- US2 pages — /transmit owned-block, received page not merged/no focus-steal, /pages owner-by-name (T019-T023)
- US1 MVP — type-only conversation hardened (state-backed tui, @name resolve, no-TTY gate, link-drop surface, one codec) (T009-T018)
- US-MVP Phase 2 foundational — tmsg codec + terminal state + @name resolve (T004-T008)
- US-MVP Phase 1 setup — terminal/rcopy skeletons, FakeHandle, two-tier tests (T001-T003)
- T042 (optional) Lean decode∘encode=id proof for term sub-codec — mirrors verified 029 IlCodecRoundTrip (flat ground-term model, no mathlib/sorry); authored, machine-verification pending Lean toolchain (auto-install sandbox-blocked)
- T039/T040 GATED corpus RUN on real AtomVM 0.7.999 via Node/WASM wrapper — real Gleam codec, float 0x03 + int64 edges byte-identical + round-trip (PASS); T043 #36 handoff note (verified FrameCodec offsets)
- T031 cross-runtime golden byte-parity harness + quickstart wiring — Dart==C#==Gleam==corpus.hex; harness PASS on dev box
- T032 V5 oracle cross-check — result-codec term bytes byte-identical to 029 ConstantCodec (int64/double/string/struct-header); models diverge at 0x05 wrapper by design; C# 131
- T038 loud-fail fuzz (0 silent accepts) + T041 cyclic-term depth-bounded no-loop — all 3 runtimes; D5/FORK-1 policy left OPEN (test only)
- US3 T033-T037 — deref+var->writer fidelity (all 3 runtimes): exact depth-32/33 boundary + $truncated marker, var->writer identity, canonical-order determinism; deref-corpus.md reference; Dart/C#-builder/Gleam green
- US1 T025 — suspended-status acceptance (all 3 runtimes): Status=suspended + blocking-reader set + no heap-addr leak; Dart+2/C#113/Gleam79 green
- US2 T027/T028 — C#+Gleam golden byte-identity + cross-decode against pinned corpus.hex (encode(corpus)==golden, decode(golden)==corpus, all 13 non-gated); C# 111, Gleam 77 green
- Gleam result-envelope builder (T022/T023) — new result_envelope_builder.gleam; heap-threaded deep-resolve (depth-32 + $truncated) over 034 heap.deref, build from query writers, round-trips shipped codec; 74 gleam tests green
- C# result-envelope builder (T020/T021) — new glp_result_codec_builder project w/ IHeapView seam (owner A+B); deep-resolve depth-32 + $truncated, build from queryVarWriters/DrainResult, round-trips shipped codec; 7/7 tests green
- RDP-robust command mode in 3270 TUI — transmit via '//'+Enter or Alt-Enter (no F-keys needed); slash-commands /help /theme /pages /new /next /prev /goto /focus /quit /send; F-keys still work where passed through
- 3270 TUI enhancements — 5 colour themes (F2/Ctrl-T), F1 help page, F10 page list w/ owners, startup screen art, configurable command lines (GLPQUICK_CMDLINES), Ctrl-key alternates for swallowed F-keys; record PF-key activation reqs
- prototype virtual IBM-3270 full-screen chat TUI (--tui) — block-mode compose (F9 transmit), green-screen transcript, pages (PF7/8/6), OIA status line; web-researched 3270 model
- prompt_toolkit REPL for interactive chat (input pinned at bottom, incoming renders cleanly above via patch_stdout); plain stdin/outbox path retained for background/file-driven use; GLPQUICK_QUIET send-only mode
- interactive --server/--client link console (real cross-process QUIC, both directions) + quickstart runbook (machine-name + gavri two-host steps); 18 pytest green
- US3 Gleam Profile A — Gleam/BEAM channel-link + C# genuine-QUIC side-process (real_quic side_process); gleam StackAdapter; demo --stack gleam SC-001..006 PASS; 18 pytest green (T030-T034). Profile C honestly build-blocked (no MSVC)
- US2 multi-accept mesh server — QuicListenerHandle (N isolated links/port) + Mesh router (to/broadcast, over_capacity, isolation); demo --clients 4 PASS SC-001..005+mesh; 14 pytest + 104 xUnit green (T023-T029)
- US1 demo + CLI wiring — genuine same-host conformance (SC-001/002/005 PASS, SC-003/004/006+two-host honestly NOT-RUN); 12 pytest + 104 xUnit green; tasks.md status (T014-T022, US2/US3 notes)
- US1 C# host exe + csharp StackAdapter — two-process genuine QUIC+WS GLP-message exchange, full-duplex + cert-mismatch reject; 11 pytest green (T018/T019/T020 message-level)
- US1 genuine QUIC+WS leaf — real System.Net.Quic handshake (IsSupported-gated, mutual SPKI pin, ALPN h3) + RFC6455 over QuicStream + minimal CONNECT bootstrap; 5 xUnit + 9 cert pytest green (T014/T015/T016/T017)
- US0 Setup+Foundational — glp_quick scaffold (cert/SPKI pin, GLP-msg envelope, CLI skeleton), /GLP-Quick skill, C# QUIC+WS leaf stubs (LinkScheme.Quic); IV-a gate PASS, real-QUIC probes PASS
- polish — additive-only + quickstart walkthrough + artifact hygiene green (T023-T025)
- WSL smoke gate + config-only conversion recognition + README (US3, T019-T022)
- 8 subsystem placeholders 1:1 with glp_runtime/lib (US2, T009-T018)
- glp_gleam MVP — buildable+testable Gleam/BEAM subtree (US1, T001-T008)
- Dart->Gleam codeconv langpair (dart,gleam) + R3-b generic collision seam
- Phase 7 US5 (T040-T050) - gate/orchestrate/trace/escalation ported onto stage+checkpoint rows, reconcile (in_sync/fast-forward/fork escalation, resume reconciles first), budget_exceeded kind, CLI gate/rerun/trace/reconcile; US5 6/6, full marathon set 26/26
- Phase 6 US4 (T033-T039) - scoped commit+push folded onto checkpoint rows (named paths only, hooks run, never force), push_blocked escalation, rule-2a re-drive guard + redrive_commit, status line grammar + emit_status at every boundary, CLI status/--emit + exit 2 on push_blocked; tests 4/4
- Phase 5 US3 keeper (T026-T032) - start/stop/recover over bridge_client, kernel-fd single-writer lock with ConcurrentWriter refusal distinct from stale residue, read-only doctor, keeper CLI; FIX latent bridge_client.request_force_shutdown marker path (inside data_dir -> sibling, matching bridge poll + 012 sibling convention); tests 2/2
- Phases 3+4 US1+US2 (T012-T025) - data-driven stages register/append/finalize, start_stage+checkpoint, pure derive_position resume, emergent intake with 5-stage mini-pipeline + fractional routing + prereq escalation, CLI register/append-stage/stage-start/checkpoint/resume/position/finalize/capture; tests 11/11
- Phase 2 Foundational (T005-T011) - per-run isolated store: resolve_env off-repo guard, idempotent 9-table schema, bridge-composed single-writer repository CRUD, JSON-mirror dual-write, monotonic sequencing; foundation tests 3/3
- Phase 1 scaffold (T001-T004) — verify greenfield precondition, rewrite models data-driven, new module stubs, drop obsolete 024 tests/modules
- plan + tasks + analyze marathon-refinement; resolve VI-b via constitution v1.1.0
- clarify marathon-refinement — resolve 4 forks (hybrid store, codeconv-module now+extract-later, 5-stage mini-pipeline→marathon implement, greenfield)
- specify marathon-refinement (spec + requirements checklist; 29 FRs, 5 user stories, 3 clarify forks)
- polish — pin Typed-Datalog-IR citation, KEEP decision + findings to seed/quickstart, FR-012 baseline re-check green (T026-T028)
- part B — Lean 4 formal gate, sorry-free decode∘encode=id (propext only); install elan/lean 4.30
- phase-b heap-embedded ModuleTerm round-trip + execute-equivalence (3/3)
- US2 contract gates + US3 coverage/completeness (41/41); reconcile contract drift (7 v2 classes, Decode record, status-based execute-equiv)
- IL codec core + harness MVP — US1 round-trip identity + execute-equivalence green (14/14)
- clarify+plan+tasks+analyze il-codec-spike (3 forks resolved; 5 analyze remediations folded)
- populate evidence-based constitution v1.0.0 (8 principles) + plan/tasks/analyze + before/after baseline & negative-control evidence
- block 07 — Polish/close-out (T025-T028); feature 28/28 complete
- block 06 — real-tool spike RUNS: SPIN (T024) + Lean tactic loop (T014/T015)
- block 05 — US1 template+interactive-spec, US2 loop-seam+no-API gate, US3/US5 docs+subjects (parallel author batch wf_17e57fd5-646)
- block 04 — US4/MLIR vertical slice complete (T016 MLIR-GLP-DIALECT.md + T018 ILFRAG-1 + T019 harness + T020 real-MLIR round-trip PASS, mlir-python-bindings 22.0.0/WSL2)
- block 03 complete — T017 real MLIR bindings via option A (mlir-python-bindings 22.0.0.2025112901, mlir.ir round-trip verified); escalation #1 resolved
- block 03 partial — real SPIN 6.5.1 (T022) + Lean 4.30.0/lean-lsp-mcp (T012) provisioned in WSL2; MLIR (T017) escalated #1 (no real wheel)
- marathon block 02 — finalize REFINEMENT-METHOD §4 six formal-tooling slots (T004) + DECISIONS-FOR-OWNER cross-link to ratified R1-R15 (T005)
- marathon block 01 — spike subtree skeleton (T001) + Python baseline (T002) + 026 input gate (T003)
- GATE D Dart<->Dart 8/8 green — path-B listen-driver fix + clean link shutdown
- Phase D layer 2 complete — async-aware link establish + 7 kernels + boot + engine async pump-driver
- Phase D layer 1 — Dart mirror of link seam+reliability+transports
- WORKING two-process producer/consumer over real TCP (C# REPL x2, 127.0.0.1) - Got=[10,20,30] byte-identical. Fixes: TcpTransport connect-retry (timing-independent rendezvous) + LinkTerms.Unquote (GLP string constants carry quotes by design for type-checker string-vs-atom; kernels must strip for host interop - xUnit used bare ConstTerms, hiding it). pc.glp role-boot demo (T037)
- relocate link types+wrappers link.glp -> root self.glp (Gabi-approved A, callable universally like send/receive) + deep-deref kernels for real compiler terms (LinkTerms.GroundResolve across all 7 kernels; xUnit used ground ConstTerms, hiding the nested-VarRef bug); Dart baseline 524/525, 99 xUnit, wrapper->kernel chain proven on C# REPL
- T038 wire link kernels into C# REPL boot (exe composition-root hook -> LinkKernels.Install + register TcpTransport/LoopbackTransport) + TcpTransport (raw TCP/IPv4 localhost, first real cross-process leaf) + C# builtinProcedures mirror; link.glp loads on C# REPL; 99/99 xUnit
- T036 programs/lib/link.glp - link-layer types + 12 GLP wrappers over the host kernels (H1/H2/H3/M1 mode fixes applied); register 7 ratified link kernels in type-checker builtinProcedures allowlist; loads clean via dart REPL, baseline 524/525 unchanged
- T035 link_close - '_link_close'/2 + graceful [] close converge on LinkTeardown core (emit closed(LinkId,Reason) on every monitor + end-stream + CloseAsync + live T024 GC via LinkRuntime.Reclaimer); data path untouched (FR-024/044); 95/95 xUnit
- T034 per-link fault monitor - '_link_monitor'/2 + LinkFaults fan-out core + LinkHandle.MonitorCursors + pump OnFault->inbox delivery; fault = bound term on per-link stream (never 4th verdict/never Fail; FR-008/043-046); 85/85 xUnit
- T033 path-B handshake (Option A) - request/listen/accept kernels + explicit request_listener + rendezvous term; shared LinkEstablish core converges all paths on T030 registry (FR-002/R-5); 79/79 xUnit
- T031 '_link_send'/3 kernel + shared LinkEgress ground-relay ship (LinkId face backs out_relay/3; deep ground-resolve gate; 72/72 xUnit)
- T030 '_link_setup'/5 kernel + Option-B LinkPump (setup/egress/ingress wiring over loopback; idempotent-at-identity; 66/66 xUnit)
- Option-B inbound-pump seam (IInboundPump + engine.InboundPump + run-to-quiescence driver loop in both goal paths); null-guarded = zero change for non-link runs; out/csharp builds clean, glp_link.tests 62/62
- T030 infra - LinkTerms mapping + TransportRegistry + idempotent LinkRegistry + LinkHandle (FR-007/013); 62/62 xUnit green
- T026 deterministic loopback transport + full Phase-2-stack round-trip test (FR-002/004/018/020); Phase 2 complete, 52/52 xUnit green
- T025 bounded backpressure SendWindow N=8 (FR-025); 44/44 xUnit green
- T024 distributed GC framework - LinkReclaimer + ResourceSnapshot (FR-024); 36/36 xUnit green
- T023 epoch/fencing token split-brain defense (FR-047); 30/30 xUnit green
- T022 per-link sequence/dedup + FIFO + reorder buffer (FR-020/023/053); 22/22 xUnit green
- T021 wire format - version+length/CRC32+fragmentation/reassembly+cycle-guard (FR-022); 15 xUnit tests green
- T020 LinkTransport seam (ILinkTransport/ILinkEndpoint + value types) in clobber-safe csharp/glp_link/ (FR-058); T002-T004 bookkeeping
- FR-037/SC-006 @< @> @=< @>= standard-order term-comparison guards (lexer+parser+runner _compareTerms+analyzer+prelude+self.glp; Dart + C# mirror; Section A24f tests)
- FR-033/SC-005 atom/1 guard = string/1 synonym (runner arm + prelude reg + self.glp decl + C# mirror + Section A24d/e tests)
- polish — auto-mode policy, stage-hook skill, docs, multi-session e2e (marathon complete)
- US2 gate + US3 rerun + US5 status/budget + US6 gitblock + US7 trace
- US4 verify-spike + US1 restart-safe resume MVP (resume/reconcile, gate reader, budget, trace)
- marathon harness foundation — 0010 schema, dual store, cadence, start/doctor
- US5 backend choice + dart fallback, exit-codes 6/11 (exec-path+drift), JSON/parity tests, docs
- /glptutorial-run unified run-model (preview/run/explain/propose) + shape-classifier + skill
- /glptutorial-list GLP tutorial browser (bridge-free codeconv tutorials list)
- clone GLP tutorial corpus into glpnet (olamni/tutorial, 47 .glp + 42 repl-trace.md, byte-identical to sibling) - self-contained equiv corpus, no sibling dependency
- converge test/ harness to sibling (to_repl_path + run_aot_smoke/run_cross_mode_parity) - fixes suite vs converged loader; point equiv oracle tests at the cloned-in tutorial corpus
- programs/.glp byte-identical to sibling (Gabi-approved) - self.glp +procedure tuple/is_list (completes runner is_list/tuple convergence) + 4 typed_book play sources (bonds/agent, cssg+cssn typed_social_agent, cssn typed_ui_mediator); programs .glp diff=0
- add bin/triage_loader.dart from sibling (new file under gitignored bin/, force-added) - completes bin Dart convergence
- glp_runtime lib+bin DART byte-identical to sibling GLP - 9 lib overwrites (runner+is_list/tuple, compiler x3, glp_engine, type_checker x3, repl_play_runner) + delete unify_result.dart + bin/glp_repl.dart (Windows/abs path fix) + triage_loader.dart; rebuilt golden exe; static diff=0, tutorials 77/88 (was regressed; remaining 8 are program-level)
- comprehensive sweep driver (incr 3) - sweep() runs goal-bearing corpus through dual-REPL oracle, tallies equivalent/divergent/needs_agent_work/error + decision-2 outcome cross-check; 2 hermetic tests green
- live dual-REPL capture backend (incr 2) - capture_pair/compare_goal spawn Dart golden(:trace+:debug)+C# candidate(GLP_EQUIV_TRACE), outcome cross-check (decision 2), strict verdict; injectable spawn; 8 tests green incl live append([1,2,3]) EQUIVALENT
- goals.yml reviewed artifact (incr 1b) - to_yaml/load/write_artifacts serde + round-trip test; seed 88 ch01-06 goals for review (g1=c)
- goal-bearing tutorial corpus parser (incr 1a) - GoalEntry + parse_trace_goals handles in-fence+prose formats w/ load-context source tracking; 88 goals from ch01-06; 6 pure tests green
- T031 part-a - fidelity GEPA metric (SC-004 import identity) + optimize oracle seam
- T022 - parse_dart adapter (Dart :trace/:debug -> canonical wire); 28/28 events match append fixture, only OUT pending finding-#3 deref
- T022 - relabel goal ids in separate g-namespace (GoalId sentinel) instead of dropping goal; SUSPEND/REACTIVATE goal stays a (relabeled) fidelity signal. 34 equiv pure tests green
- T017(ii) option-a - align BYTECODE_OP spine to Dart :debug-observable op set (14 ops; exclude conditionally-printed GetValue); append spine now matches golden except the isolated Ground->Commit divergence
- Stage 5 T017(ii) - candidate-side canonical EV/OUT trace emission (equiv_trace.cs) at runner spine/commit/suspend seams + engine OUT; flag-gated (GLP_EQUIV_TRACE), no-op + behaviour-unchanged when off
- Stage 5 T017(i) - wire glp_repl exe to converted REPL (delegating entrypoint); runs + matches Dart golden on true.
- Stage 4 COMPLETE — goal_queue marked no_emit on canonical cluster (migrate 0009 applied; status no_emit:1/escalated:0/open_escalations:0); E1 escalation resolved (option-a no_emit)
- Stage 4 CODE — first-class no_emit status (migration 0009 single-head off 0008; status() _classify_codegen_row precedence; mark-no-emit CLI; readiness satisfied; codegen_no_emit tombstone key); offline tests 19/19 green. Canonical migrate+mark PENDING Gabi OK.
- Stage 3 runner ingest — build-gate pass → built; E1 escalation resolved (6-chunk conversion); frontier now 74/75 built, 1 escalated (goal_queue=Stage 4)
- runner.cs Stage 3 chunk 6/6 — concurrency arms (Spawn/Requeue/Distribute/Transmit via GlpChannel) + guard arms (Guard/Ground/GroundEqual/Known/NoReaders) + all 6 helpers (_evaluateGuard 25-arm switch, _termsEqual cycle-detect, _dereferenceWithTracking, _evaluateArithmetic, _convertTentativeToStruct); runner.cs COMPLETE (5740 lines), full sln green 0 errors, zero stubs
- runner.cs Stage 3 chunk 5/6 — clause control + Commit (ApplySigmaHatFCP) + env (Allocate/Deallocate) + Push/Pop/TailStep/Union/Reset/Proceed/Otherwise/Nop/Label/Halt; sln green
- runner.cs Stage 3 chunk 4/6 — BODY-phase structure building (Put[Constant|Structure|Nil|List|BoundConst|BoundNil], SetConstant, BodySet[Const|ConstArg|StructConstArgs]); sln green
- runner.cs Stage 3 chunk 3/6 — UNIFY arms (Constant/Void/Structure) + v1 Get[Variable|Value] + all 7 v2 arms; sln green
- runner.cs Stage 3 chunk 2/6 — HEAD-phase arms (HeadConstant/Structure/Nil/List, HeadBindWriter[Arg], Require[Reader|Writer]Arg, GuardNeedReader[Arg]); sln green
- runner.cs Stage 3 chunk 1/6 — skeleton (support types real + RunStep/RunWithStatus loop + 60-arm _Step dispatch + stub Exec/helpers); full sln green, downstream unbroken
- Stage 2 — GEPA run on bytecode (build-only): generator regenerated opcodes->C# (1.0), build ceiling confirmed, bytecode.md frozen w/ measured provenance; gitignore covers per-subsystem candidate + GEPA scratch
- Stage 1 — per-subsystem Claude-driven GEPA wiring (T032 dataset split, T033 program subsystem field, T034 prompt.load(subsystem), T035 codegen-opt skill loop + dataset/score CLI, T036 _base+5 subsystem prompts); build-only metric per 2026-06-03 decision; 24/24 targeted tests green
- bulk codegen FINAL — 73/75 built (97.3%); 2 escalated (runner.dart 4863-line interpreter deferred; goal_queue Dart-export no-emit by design). codegen, compiler, glp_engine, isolate_manager, agent_runtime, bin/glp_repl all built against runner stub; full sln dotnet build GREEN (0 errors, 140 warnings); gitignore allows out/csharp/bin/*.cs source while still ignoring dotnet Debug/Release output.
- bulk codegen batches 15-16 — 5 built (system_predicates_impl, result, asm, scheduler, linter; downstream files built against runner.cs stub)
- bulk codegen batch 14 — pmt/validator built (added Module.ModeDeclarations() extension stub for missing dep)
- bulk codegen batch 13 — SCC cg=36 + pmt/checker (6 built: pmt/checker, mad_context, body_kernels, glp_activation, runtime, system_predicates; class GlpRuntime renamed to GlpRuntimeEngine to disambiguate namespace; runner.cs stubbed + escalated — 4863-line WAM dispatch exceeds single-pass)
- bulk codegen batch 12 — 5/5 built (occurrence, pmt/type_checker, commit, external_io, suspend_ops; ModedArg extended with TypeName/TypeParams + ModeDeclaration.Predicate to resolve pmt/type_checker E1/E2/E3)
- resolve 2 escalations — heap_fcp (CellTag→HeapCellTag rename) + mode_table (new mode_declaration.cs stub); 50/75 built (Gabi-approved 2026-05-28)
- bulk codegen batch 10 — 1/1 built (project_linker; manual patch for 2nd missing guards param)
- bulk codegen batch 9 — 3/3 built first pass (type_checker, analyzer, module_hierarchy)
- bulk codegen batch 8 — 2/2 built (type_env_builder, partial_evaluator; 1 repair)
- bulk codegen batch 7 — 3/3 built (suspend, well_typed_clause, parser; parser needed long→int site missed by repair-agent)
- bulk codegen batch 6 — 5 built (2 repairs) + 2 escalated (mode_table dep_missing, heap_fcp CellTag conflict)
- bulk codegen batch 5 — 7/7 built (4 first-pass + 3 bounded repairs)
- bulk codegen batch 4 — 7/7 built first pass (topo=1 mixed)
- bulk codegen batch 3 — 6/7 built + 1 escalated (goal_queue Dart export-only, undecidable per spec)
- bulk codegen batch 2 — 7/7 built first pass (compiler/engine/multiagent leaves)
- bulk codegen batch 1 — 7/7 built (analysis/type_checker/bytecode/compiler leaves)
- codegen Converted.props append hook + 12 pure tests (bulk-codegen pre-req B)
- T025 + C# REPL infra (out/csharp .sln/.csproj/Converted.props + glp_repl placeholder, dotnet build green); safe-restart ledger for bulk codegen drive
- US2 readiness + durable equiv-step pure core (T023/T024)
- US1 capture/compare/bytecode-diff CLI (T018/T019) — standalone deterministic verdict over recorded artifacts; shared db.engine.connect; DB writes deferred to durable step (T024)
- US1 corpus.py + reviewed corpus.yml enumeration + materialized split (T016; 256 sources, book 141 exact)
- US1 oracle core — normalize/relation/bytecode_diff + SC-005 batteries (T013-T015, T020-T021, 21 pure green)
- Setup + Foundational — migration 0008, equiv tool skeleton, pure trace/fidelity/manifest, tombstone keys (T001-T012, 14 pure tests green)
- build-gated codegen tool + offline GEPA optimizer (decision B; R11 commit out/csharp; migration 0007)
- planagents complete — 130 per-file conversion plans + 129 plan-stamped tombstones (plan_started/completed_at, plan_path); 2 open escalations recorded in conversion-plans/_escalations-report.md (bin/glp_repl.dart convspec-absent, type_ast.dart getType/object.GetType shadowing) pending engineer decision before conversion
- US3+US4 — status via status.py projection (T045), unified aggregate-escalations (T042 delegates to convspec), retry/redrive (T046), tombstone<->DB divergence exit-4 (T047); +T043/T044/T048/T049/T058 tests, capability-preservation 25/25
- US1 FULLY GREEN (T024/T025/T026/T054/T055/T057/smoke all pass — init fix landed) + T039 convspec skill (analysis + SEPARATE research sub-agent contracts, escalate-don't-guess, official-docs-authoritative) + US2 idiom-KB/conflict/provenance/ingest/both-bases tests
- R12 step-transaction fix (process engine cache + bootstrap pre-warm — f405 forbidden-rollback ELIMINATED, T024/T025/T054 green) + US2 convspec tool (readiness/idioms/artefact/workflow/CLI, convspec stage wired scaffold->convspec->plan) + T030 4/4; T057 git-head fix; helper adds init
- T054 GATE GREEN — R12 cleared. DBOS in-process bootstrap (engine pre_launch hook, decorate-before-launch) + drive-to-completion + deterministic resume fix; 22-file durable run on single-writer PGLite completes, status<5s, idempotent recovery. +T024/T025/T055/T057 e2e tests
- US1 — T023 frontier-order test (4/4 green: dep-order+SCC-indivisible+determinism), T026 nothing-to-convert test, T027 builder skill (durable loop + needs_agent_work handler)
- Phase 2 COMPLETE (T005/T006 bridge-verified 6/6 green) + US1 builder tool T019-T022 (Typer run/resume/status/trace/retry/redrive/aggregate, orchestrate frontier over read-only 015 depgraph, register->durable.activate); discovered, full surface
- T012-T015 — durable workflows/queue/trace + activate(); 6 tools' register() delegate to durable activation (D2: run_* unchanged); imports+pure smoke green, plan_units SCC-collapse verified
- T011 — durable/steps.py @DBOS.step wrappers calling discover/depgraph/scaffold/plan entrypoints VERBATIM (D2, signatures verified against real defs); lazy DBOS, replay-safe, registry smoke OK
- T009/T010 — durable/ workflow-id derivation (SHA-256, deterministic) + step registry; pure determinism test 4/4 incl. cross-process stability (R9/FR-004/SC-002)
- T007/T008 — shared workspace.py read-facade (D2: mirrors 016 SQL, no behaviour change) + pure status.py state projection (data-model §5, smoke 9/9)
- T016 — append-only feature-018 tombstone keys (convspec/builder state) into _FIELD_ORDER + _PRESERVED_APPENDED_KEYS; test_tombstone 4/4 green
- codeconv mirror stage (spec Amendment 1, Option 1) + init scope overrides (FR-042/043) + depgraph option-A' filter restore
- Phase 8 polish — remove tools/d2net + D2NET-* skills (FR-022/D1/D2), doc+gitignore de-brand, SQL-safety scan (T032-T035)
- US2 codeconv scaffold (T018-T023) — mirror tree+target_path+phase, managed-target idempotence (contract reconciled), e2e-verified
- US1 codeconv init (T013-T017) — workspace config + delegated discover, e2e-verified on side cluster
- T011 repoint discover to source Dart-specifics via langpairs (byte-identical; Phase 2 foundational complete)
- planagents foundation — 0003 migration, pure readiness predicate (17 unit tests green), workflow/CLI/artefact/tombstone-writer, SKILL orchestration loop; _FIELD_ORDER+round-trip extended (Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>)
- Phase 6 stamp/rebuild tests (8/8 green) + codeconv-depgraph SKILL.md (T037)
- Phase 5/US3 cycle fixture (A->B->C->A + D->A) + test_depgraph_cycle_fixture (6/6 green)
- Phase 4/US2 mark-* + status lifecycle — tombstone_writer 6th-key (target_path) preservation fix + test_depgraph_mark (13) + 2 lifecycle tests (15/15 green)
- checkpoint codeconv-depgraph WIP (migration 0002 + tools/depgraph + tests) before merging pglite 0.4.5 fix
- codeconv-discover resolves package:glp_runtime/... self-imports (US1+US2)
- --data-dir override for PGLite cluster + ship initial glp_runtime_net/ tombstones (128 files, 146 import edges). Routes PGLite around exFAT-hostile filesystem of repo's home volume. cli.py / bridge_client.acquire_or_discover / workflow.run_discover all accept the override; sidecar / lock / consumers / shutdown-marker derive from <data-dir>. known-issues Issue 8 documents detection + usage.
- T080-T092 — Phase 7 polish. SC-011/SC-012 schema-isolation + caller-graph-inside-only tests; SC-003 full two-stack (Python psycopg + .NET Npgsql, 100 cycles each); phase7_verification_report.md maps every flow + SC to test/evidence; CLAUDE.md adds .pgdb/ + .codeconv/ + Migration to unified bridge; known-issues Issue 7 documents the four DBOS-on-PGLite hooks. 39/42 codeconv tests pass (2 perf + 1 Windows-symlink skipped). FR-026 + FR-027 greps clean.
- T060-T076 — Phase 6 US4 codeconv-discover (walker / parse / tombstone / workflow + Typer app + SKILL). DBOS-on-PGLite launch fixed: postgres-DB role override + pool_size 5 + uuid-ossp rewrite preserves semicolon + listen_notify off. 36/39 tests pass (2 perf opt-in + 1 Windows-symlink skipped).
- T050-T059 — Phase 5 codeconv runner + experimental bridge daemon coordination protocol (heartbeat / orphan poll / linger / force-shutdown). 12/12 tests + 1 xfail. Deep coordination investigation deferred to follow-up feature; artefacts under docs/research/bridge-daemon-coordination/.
- T030-T039,T042-T043 — Phase 4 BridgeClient + PgdbMigrate + skill; T040-T041 deferred
- T020-T026 — Phase 3 US1 bridge cross-process lock + sidecar + log rotation; sibling lock path
- T010-T014 — Phase 2 vendored loaders + D2NET schema audit + bridge description
- T001-T005 — Phase 1 skeletons + .gitignore + npm/dotnet wiring
- prereq-patterns catalog — 8 patterns, 6 format contracts, merged pglite bridge, conformance gate (C1..C6 PASS); v2026.05.09
- author local-secrets-store + secure-signatures (T021-T023); Phase 3 / US1 complete
- import format contracts (Phase 2) + author governance + 5 non-pglite patterns (T013-T020)
- /D2NET-scaffold skill ship — SKILL.md + tasks closeout + validation + CHANGELOG v2026.05.01

### Fixed
- bounded full-duplex quic_chat — close after collect, no teardown race
- preserve trailing mode annotation on compound type alternatives (codexreview P2)
- quic payload codec understands the E3pcCtrl section (0x15) - carries E3PC frames instead of loud-rejecting (T034 follow-up)
- path-B listen failure fails closed gracefully — establishment class complete (codexreview P2)
- path-B request connect failure fails closed gracefully (codexreview P2)
- correct quic_mesh.glp GLP mode/SRSW so it loads clean (codexreview P1)
- defer QUIC link close until inbound collection completes (codexreview P1)
- any transport-establishment failure fails closed gracefully (codexreview P2)
- capability-gate evaluation failure fails closed gracefully (codexreview P2)
- verify-before-act on path-B + controlled codec-failure on egress (codexreview P1/P2)
- defer QUIC trust-material load to first use (codexreview P1)
- per-role rendezvous timeouts in LinkSetupKernel (listener 180s, connector 120s) for cross-host soak
- widen pump fault-guard to the frame parse/reassembly/ordering layer (not just decode) so a malformed/adversarial frame surfaces an observable fault instead of silently killing the receive task; lock _recvLoops with _faultSubs
- codexreview fixes — genuine macaroon expiry (</<=  operators + real elapsed-token test), pump surfaces malformed-payload fault instead of silent link death, codec rejects term-visible cap slot 0x20, thread-safe ProvenanceLog, LinkPump.Dispose detaches OnFault + joins loops, quic_one_bind ships crdtmsg/7 (production wiring)
- codexreview non-blocking fixes — control-agent typed-error hardening (US5 FR-018), quicer ALPN code-6 token parity + WS total-reassembly cap (US2), defined-guard StackOverflow backstop (US1)
- gavri evidence - correct msquic version for the 0.2.15 build (2.3.8, not 2.5.7)
- gavri evidence CORRECTION - MSVC 14.50 (VS Community 2026 Insiders) IS installed; earlier MSVC-less claim was false
- apply code-review findings - LEB128 overlong/overflow loud-fail, seal count-binding, section type_number CrdtMsgException, injective caveat encoding (+NUL cleanup); 86 tests green
- CHANGELOG ordering - stray v2026.06.03.1 block moved to chronological slot, Unreleased restored to top
- E1 store side confirmed delta-CRDT+Merkle (option b both layers) - doc + mvp notes + export
- codexreview P1s — commit-time quota re-check (FR-038) + rcopy reply spoof-guard
- drain host stdout at spawn to prevent pre-readiness pipe-fill hang (code-review #6)
- demo records SC-001 FAIL on handshake timeout instead of AttributeError (code-review #5)
- Gleam relay reassembles >1MiB lines instead of misrouting fragments to stderr (data-loss guard); erlc-verified via WSL
- mesh dup-id no longer hijacks or evicts the incumbent link (routing/data-loss guard); regression test
- @name routing (FR-006), --tui TTY fallback (FR-005), report link-drops; shared parse_addressed + 5 tests
- codexreview fixes #1/#2/#4 — bound WS frame size + surface FrameException as clean fault (FR-019); default gleam profile A; exit-code 6 -> quic_unsupported; +regression tests
- codexreview cycle-2 — golden harness rejects zero-match C# filter (dotnet test --filter exits 0 on no matches; a renamed class would false-pass); guard on non-zero Passed count
- codexreview cycle-1 — AtomVM gate hard-fails on gleam build error + missing beam (was unchecked, could false-pass on stale beams); output-content stays the success signal (AtomVM exits 1 benignly on success)
- process-tree kill on stop (no orphaned QUIC hosts incl. gleam->erl->dotnet); REPL polish (incoming on its own line); restore _spawn method
- client stays alive for the link lifetime (not stdin) + disable QUIC idle timeout; link console survives EOF, auto-announces, file-outbox (GLPQUICK_OUTBOX), @to grammar
- strip placeholder export markers -> doc-only (codexreview: T009-T016 'no exported definitions')
- per-run marathon bridge resolves script from toolchain checkout, not the off-repo store (Fix A) - T057 e2e drive found the primary PGLite store never started via the real CLI; decouple repo_root(script source) from store_root(cluster) and commit-target repo_dir; junction-free fixture + regression test
- flip stale T017 checkbox to [X] (MLIR bindings done block 03, used block 04) — tasks.md now 28/28
- codexreview cycle 1 — loopback cancel busy-loop + _rendezvous socket leak + clean recv-loop teardown
- LinkTerms.ToTerm re-quotes string components + path-B example
- core runner heap-addr/register-index deref conflation (Dart + C# mirror)
- FR-035/SC-009 imported-reader reactivation via bindAny ingress seam (heap_fcp.dart + mad_context wiring + C# mirror + regression test)
- FR-034/SC-009 compound-operand guard suspends on nested unbound reader (runner.dart generic-guard recursion + C# mirror + Section A24b/c regression test)
- FR-021/SC-008 redelivered madGLP assignment is a verified no-op (mad_context Dart + C# mirror + regression test)
- harden marathon harness pre-marathon (rerun runId echo, resume commit/push crash guard, budget-halt escalation, live-spike recorder)
- guard rerun_subagent against sibling-block units (FR-007) + regression test
- converge C# arithmetic to Dart num (int-preservation) + Dart double printing; A5 convergence record
- converge C# moded-path rendering to Dart lowercase mode words (AsModeString)
- converge C# runner constant matching to Dart num== (NumEquals) — fixes recursive base-clause selection
- converge C# runner guard dispatch — add is_list/tuple guard aliases per runner.dart
- converge C# type DFA — add Any builtin type (states/automata/leaf arms) per program_dfa.dart
- converge C# REPL to Dart — self.glp path resolver + tuple/is_list builtins
- capture uses repo-root-relative (../) load paths - current Dart REPL (glp_repl.dart:193-198) only honors / ./ ../ verbatim and roots else at glp/, so Windows-abs D:/ mis-resolved; sibling tutorials load as ../GLP/... (FR-006, no copy); 8 capture tests green
- T022 finding-#3 - recursively deref OUT binding shape (candidate-side); re-captured append_csharp OUT now ./2(const(a),./2(const(c),const(nil)))
- #2 resolved - emit Commit conditionally from ExecCommit (proceeding-commit only) to match Dart's conditional COMMIT print; NOT a runner bug. Append spine now matches golden exactly across all 3 goals
- Stage 5 - scheduler.cs success-determination wires onReduction callback (was stub-era gap); converted REPL now matches Dart golden on append/reverse/quicksort
- buildprops — ignore example Include in header comment (regression test added)
- resolve 2 plan escalations — bin/glp_repl retroactive convspec + project-wide getType→LookupType rename; gitignore !.codeconv/**/bin/ so FR-029 inventory tracks the lone bin/ file; E1 (convspec absent) closed via hybrid (best-effort plan accepted + convspec generated/ingested, specced 0-esc); E2 (getType shadows object.GetType) resolved by getX→LookupX idiom recorded in KB, applied to both definition sites (TypeEnvironment in type_ast, TypeTable in type_table) + callers (pmt/type_checker ×6, module_hierarchy_test) across convspecs AND plans; convspec 130/130 specced 0-esc, planagents 130 planned 0-esc 0-stale, both escalation reports 0
- close escalations #5 + #6 — isolate_manager Channel<T> actor mailbox + rpc_routing_test auto-resolved (Gabi 2026-05-21, Option C); each agent is a single Task.Run consuming a per-agent Channel.CreateUnbounded<IsolateMessage> via 'await foreach (var msg in reader.ReadAllAsync())', one-for-one with Dart isolate ports: Isolate.spawn→Channel+Task.Run, SendPort.send→writer.TryWrite, ReceivePort→ChannelReader, await for→await foreach, port.close→writer.Complete (preserves Dart no-Isolate.kill contract verbatim), Completer<void>→TaskCompletionSource (cached idiom); composes with heap_fcp #4 single-owning-context (consumer Task IS the agent's owning execution context, heap accessed only inside await-foreach body, Channel is the only cross-context primitive, no lock/Interlocked/ConcurrentDictionary in per-agent state); rejected A (Thread-per-agent: OS-thread budget), B (ConcurrentExclusiveSchedulerPair: serialisation not mailbox), D (SynchronizationContext: UI-affinity not headless actors); #6 (rpc_routing_test same(channel) reference-identity) auto-resolves under Channel<T> since IsolateMessage + GlpChannelHandle are in-process .NET references never marshalled across boundaries; isolate_manager + rpc_routing_test both escalated→scaffolded; ALL 6 ESCALATIONS NOW CLOSED, aggregate files_blocked=0; final: scaffolded=128 specced=1 blocked_on_deps=1 (bin/glp_repl.dart) escalated=0 total=130
- close escalation #4 — heap_fcp single-owning-context (Gabi 2026-05-21, Option A); .NET port preserves Dart's single-owner-thread invariant verbatim (HeapFCP fields stay plain int/List/Dictionary, NO lock/Interlocked/ConcurrentDictionary/volatile); rationale: Dart HeapFCP has zero concurrency primitives by language invariant + FCP itself is single-thread emulator + DerefAddr hot path runs lock-free + multi-step read-modify-write sequences (derefAddr chain-follow, bindWriterToReader cross-cell suspension forwarding) cannot be made safe by concurrent collections alone + 7 dependent multiagent specs already pre-committed to single-owning-thread invariant in their nuance sections; atomicity enforced at agent-mailbox boundary (isolate_manager.dart owns it — #5 gates on this); heap_fcp escalated→scaffolded, aggregate now 2 blocked (isolate_manager + rpc_routing_test)
- close escalation #3 — lift UnifyResult to lib/compiler/unify_result.dart (byte-identical ADT was duplicated in analyzer.dart + partial_evaluator.dart) + rename analyzer's strict PartialEvaluator class → DefinedGuardEvaluator (semantically distinct from partial_evaluator.dart's lenient version: throws on suspend/fail rather than returning failure); Gabi-chosen Option (b) compiler.dart now explicitly imports partial_evaluator.dart so its PartialEvaluator() call resolves to the lenient version (semantic change: non-reducing defined guards now return failure rather than throwing CompileError at compile time — prior coupling via 'import analyzer.dart' alone was deemed accidental); Dart tests 350/5/3 pre/post identical (3 pre-existing module_hierarchy Windows-path fails unrelated); workspace excluded_directories add-exclude'd lib/multiagent/archive-irma-2026-01-30/ + test_archive/ per Gabi (130 inventory total: 124 scaffolded + 4 escalated + 1 specced unify_result + 1 blocked_on_deps bin/glp_repl); 4 convspec re-spec'd: analyzer.dart (escalated→scaffolded, 22 constructs / 0 escalations; was 23/2), partial_evaluator.dart + compiler.dart (manual sha-bump + imports update), unify_result.dart (NEW, 6 constructs / 0 escalations); aggregate now 3 blocked (heap_fcp, isolate_manager, rpc_routing_test)
- close escalation #2 — no bug in glp_printer._isAtom (Gabi 2026-05-20); prior convspec sub-agents misread regex semantics (claimed anchored ^...*...$ via String.contains always matches via empty prefix at pos 0; wrong — $ requires cursor at end-of-input, fails for non-empty inputs); Python-verified 13/13 expected/actual; re-spec produced (12 constructs, 0 escalations) with Regex.IsMatch faithful translation + char-range loop as performance alternative; glp_printer escalated→scaffolded, aggregate now 4 blocked
- close escalation #1 — keep CompileError verbatim in C# port (Gabi 2026-05-20); project-wide policy: all Dart *Error types retain source names, .editorconfig suppresses CA1710 (off by default in .NET 10); 16 sibling specs already committed to CompileError, zero re-spec churn; error.dart escalated→specced, aggregate now 5 blocked
- facet-1/2/3 remediation — Option-4 agent-gate split (PRE deterministic + content-addressed POST), migration 0006 widens builder_runs.outcome to allow needs_agent_work, _resume_epoch mints new epoch on awaiting-agent so plain re-drive ingests agent-written specs (FR-044); spec.md Amendment 2; dbos_workflow_model.md taxonomy/protocol rewrite (the infeasible same-child-recover voided); test re-baseline + stage_convspec_artifacts helper; pure 17/17, real-bridge builder suite 23/23, facet-3 acceptance gate test_agent_gate_traversal.py GREEN (plain re-drive ingests agent spec end-to-end)
- US3/US4 e2e — status query column (dart_conversions.completed_at not conversion_completed_at) + builder trace bootstraps DBOS in-process (same launch-gap as run/resume); T052 scope-clean, T053 memory updated
- update stale 017 downgrade test to the linear chain (downgrade 0003 not -1) — consequence of spec-mandated T003 linearization; Gabi-approved; verified 1 passed, full suite regression-free 264/264
- resolve pre-existing @needs_bridge suite contention — isolated_repo per-test teardown via kill_bridge (proven discover_repo pattern) + operator progress hook; 2 parallel agents verify 30/30 together, 0 BridgeStartupTimeout
- T003/T004 — linearize migration chain (0003_dart_plans->0004, new 0005_codeconv_builder); single head 0005 offline-verified, dual-0003 defect resolved (FR-015/SC-004)
- correct bridge-test data-dir note — isolated_repo fixture uses pytest tmp_path (OS temp), not <repo>/.pgdb; drops contested NTFS claim (re-review P1)
- apply codex review — P1 needs_agent_work returned-not-raised (DBOS step failed-vs-pending), P2 pytest --data-dir removed, P2 research_findings.construct_key UNIQUE; +review artefact
- two SCC-batch bugs found by bridge tests
- correct tombstone under A' — test/debug_negative.dart faithfully retains its dangling import dep (supersedes 18e232d0's destructive-option-A removal)
- option A' — non-destructive referential completeness (codex P2): keep dangling edges in dart_imports, filter at depgraph compute (self-healing) instead of deleting (which lost edges across idempotent runs)
- normal-mode discover drops+warns dangling dart_imports edges (Amendment v3, option A) — unblocks depgraph compute on live inventory
- codexreview triage — P2 verify-tombstones aborts exit 65 on missing/invalid sha256 (not stale/0); P3 verify is read-only (no .codeconv/tombstones mkdir) +2 regression tests
- bump bridge READY_TIMEOUT 30->60s for PGLite 0.4.5 first-WASM-load variance (measured cold-init ~5s)
- upgrade to 0.4.5 + coalesce doubled error-path ReadyForQuery (fixes aborted-txn extended-ROLLBACK hang)
- bridge data-dir exFAT guard + 10s->30s cold-spawn timeout

### Changed
- Merge pull request #108 from olamni-glp/050-full-gleam-combined
- Merge remote-tracking branch 'origin/develop' into 050-full-gleam-combined
- M1 LOCK restart banner - safe-restart doc for M2 (US4 links T045-T058 + US5 cross-runtime capstone T059-T063 + polish T064-T068); NEW-SESSION START PROCEDURE, contracts, Olamnit env (QUIC-WS WSL-only likely host-blocked, link-rig DART override), M2 watch-outs
- impl(050): FORK-1 RESOLVED (owner-directed 2026-07-13) - deep_resolve now detects a revisited variable on the deref path (path-based visited set) and emits <circular>, matching the Dart/C# REPL cycle rendering (f(f(<circular>)) / pair(a,pair(a,<circular>))); depth-bound $truncated retained for deep non-cyclic terms. All 3 runtimes converge -> corpus 206 agree / 0 diverge / 0 fork = 100%; differential all-agree; gleam test 443/443 warning-free
- substantive close-out retrospective — 4 root-cause findings (bk-close)
- T040/T041/T044 M1 LOCK verification - corpus 205 agree/1 fork=100%, differential harness validated, regression guard green (Dart 530/531 baseline, link 16/16, C# 727/727, gleam 443/443); mark T037-T044 done
- post-ship retrospective report (bk-close)
- Merge pull request #107 from olamni-glp/main
- Merge pull request #106 from olamni-glp/release/v2026.07.13.1
- release: v2026.07.13.1
- Merge pull request #105 from olamni-glp/050-glp-native-quic-link
- Merge remote-tracking branch 'origin/050-glp-native-quic-link' into 050-glp-native-quic-link
- mark T043 done — two-host acceptance performed 7x (engineer-confirmed)
- impl(050): T041 three-way differential harness run_differential.sh <prog|-> <goal> (Dart+C#+Gleam, shared normalize, per-runtime + agree/diverge with divergent-pair count; exit = #divergent pairs) — closes MISS-04/FR-012; validated: agree on X:=2+3 + primes(10); FORK-1 X=f(X?) reports dart/gleam+csharp/gleam (Dart&C# both detect the cycle, Gleam truncates)
- impl(050): T040 GAP-G1/G2/G3/G8 + FORK-1 named programs (FR-011) in programs/tests/typed/ + corpus.list blocks + Dart goldens; G1 ground/1 multi-read, G2 standardize-apart, G3 fair-merge, G8 =:=/</integer/known three-valued (incl. suspend) all AGREE; FORK-1 circular-deref discriminator (Dart <circular> vs Gleam $truncated) recorded as owner-gated fork (expected.list). Corpus 205 agree / 0 diverge / 1 fork = 100% in-scope agreement
- T042/T043 baseline - US3 corpus parity DRIVEN TO 100% (201/201 agree, 10x PASS gleam~35s vs dart~26s); 13 engine fixes summarized
- impl(050): T042 Fix 3e - port the 049 runtime-defined-guard interpreter (Dart _evalDefinedGuardCall): three-valued multi-clause evaluation (any-success/else-suspend-union/else-fail, fail dominates suspend) with head-pattern matching into a clause-local frame + ground/known/=?= conjuncts + recursive :* calls, over the merged program + system defined-guard table (satisfiable/2 form-b default); a29w/a29v/a30 policy guards agree -> CORPUS 201/201 100% AGREEMENT; gleam test 443/443, warning-free
- impl(050): T042 Fix 3c-complete mwm - (1) runner get_variable_writer now captures a VarRef to a bound VALUE cell (a body goal passing a writer a prior kernel bound) instead of the no-op that lost it; (2) mwm mutable-ref made immutable-safe: stream_append + close WALK the cons chain to the open tail and keep the ref at the head addr, so a ref shared across streams (mwm1) keeps appending -> matches Dart in-place currentWriterAddr mutation; a26 mwm agrees (198/201); gleam test 443/443
- impl(050): T042 Fix 3d - wait/wait_until guards: a bound number succeeds (Dart Duration<=0 immediate + Duration>0 timer-then-success are both success outcomes; pure engine has no wall-clock so succeeds immediately, outcome-equivalent), non-number fails, unbound suspends upstream; a22 agrees; gleam test 443/443
- impl(050): T042 Fix 3c - port mutual-reference body kernels (_allocate_mutual_reference/_stream_append/_close_mutual_reference) + is_mutual_ref guard for mwm/2; MutualRef repr = $mutual_ref(addr) struct (Gleam Term has no custom cell; GLP threads RefOut immutably); gleam test 443/443. mwm reactivation across concurrent kernel-bind still WIP
- impl(050): T042 Fix 3b - port univ (=../2) body kernels _list_to_tuple/_tuple_to_list (list<->compound) faithful to Dart; a26 =.. cases agree; gleam test 443/443
- impl(050): T042 Fix 3a - port math + type-conversion body kernels (_abs/_sqrt/_sin/_cos/_tan/_exp/_ln/_log10/_pow/_asin/_acos/_atan/_integer/_real/_round/_floor/_ceil) faithful to Dart body_kernels.dart; transcendentals via Erlang :math FFI (BEAM builtin); int^int>=0 pow -> int, sqrt/ln/asin domain aborts; a16 agrees; gleam test 443/443
- impl(050): T042 Fix 2c - unify_structure_read reader-suspend inserts the PAIRED WRITER into U (U carries writers; no_more_clauses filters to writers), not the raw reader which was dropped -> spurious fail; fixes a24 level2/level3 head-match-on-unbound-reader suspension; gleam test 443/443
- impl(050): T042 Fix 2b - runner unify_structure_read follows a BOUND READER via dval (not deref_value's is_value-only gate); goal-boot materialises a nested struct arg (p(a,b) in w(p(a,b))) as a bound reader, so nested head matching (w(p(_,_))) wrongly soft-failed; fixes a21 test_nested + a1 metainterpreter reduce; gleam test 443/443
- impl(050): T042 Fix 2a - runner put_structure: a TOP-LEVEL arg (arg_slot>=0) starts a FRESH structure instead of pushing stale HEAD build-state (current=BTStruct from a [X|Xs] head read) as a parent; fixes guard/body operand structures (e.g. X mod P) collapsing to their last element -> a9 primes/sieve now correct; gleam test 443/443
- impl(050): T042 Fix 1 - engine.load ACCUMULATES multi-file loads (Dart combinedProgram parity, merge into engine.program; first-occurrence-wins matches insertion order); runner loads block files sequentially (no concat); a2 now agrees, a1/a9 unblocked (reveal deeper arith/reduce bugs); gleam test 443/443
- impl(050): T039 Gleam corpus runner (concat-block load, per-goal diff vs goldens, 10x wall-clock) + binding-order normalize; RESULT 190 agree / 8 diverge / 3 blocked, 10x PASS (gleam 27.4s vs dart 28.0s); expected.list seeds 3 verified-blocked (engine load-replace)
- impl(050): T038 finalize shared normalize.sh - RHS-scoped internal-var renumber (_V<k>, preserves channel-end sharing ch(_V1,_V2)/ch(_V2,_V1)) + Gleam Error-line strip + unbound fold; fix recorder filtered-run clobber of load.golden; restore load.golden 162 + timings
- impl(050): T037 corpus parity manifest + pinned corpus.list + Dart goldens recorder; 39 runtime blocks (A) + 162 load cases (B/C/D/E) recorded to goldens/, shared normalize.sh rules (T038 substance); concatenated-case model over frozen engine.load; stable re-record verified
- US2 polish DONE (:trace lines @8dd46c34, facade step/Event @fd8dba7c); restart banner rewritten for /bk-marathon resume + /bk-implement US3 (T037-T044) w/ parity watch-outs; marathon reconciled -> next T037
- impl(050): US2 polish item 2 - facade engine.start/step/Event (contract Engine surface); RunSession live run-state, shared finish_run, scheduler.status; step envelope == run envelope; 443/443
- impl(050): US2 polish item 1 - :trace line emission (head :- body / -> suspended / -> failed, Dart onReduction shape); goal_format.gleam + scheduler trace + run_with_limit_traced; 439/439
- restart banner - US1+US2 DONE (T034 output-capture @96078234, 432/432); 2 US2 thin spots flagged (:trace lines, facade step/Event); NEXT = US3 corpus parity
- impl(050): T034 output capture - _output/1 kernel port + formatGroundTerm seam, threaded as data (KSuccess->Reduced->scheduler->engine->REPL) not envelope; run_with_limit_capturing; US2 COMPLETE; 432/432
- impl(050): T031/T032/T035/T036 - REPL loop+commands+main+stdin FFI, run_with_limit for :limit; scripted + envelope-identity tests; gleam run verified end-to-end; 424/424
- gitignore guardian backups; point active feature at 050-glp-native-quic-link for ship
- impl(050): T033 slice - repl/results.gleam render_outcome + format_term (ED-1 envelope->reference-REPL text, Dart _formatTerm/_printStatus parity); 14 tests, 406/406
- restart banner - US1 FULLY DONE (T025 @64d0a2a3, T026+T028 PI:14 discharge @06e0427f, 392/392, lake build re-verified on Olamnit); NEXT = US2 REPL T031-T036
- proof(050): T026+T028 discharge PI:14 writer-MGU - adversarial suite (10 tests, 392/392) + INDEX OPEN->proved + PARITY-BAR refs + lake build re-verified exit 0 on Olamnit
- T025 engine-semantics tests - three-phase HEAD/GUARD/BODY ordering, reactivate-exactly-once (FR-005), otherwise-after-failure-not-suspension (382/382)
- restart doc - NEW-SESSION START PROCEDURE for US1 hardening (T025/T026+T028) then US2 REPL (T031-T036)
- T029 + T030 DONE - baseline.md smoke-set record, tasks.md T029/T030 checked, restart banner (US1 acceptance complete, X:=2+3 Dart-verified)
- impl(050): T029 Slice 2 - goal-boot + engine.run + T030 (X:=2+3 e2e -> Success X=5; suspension; list-of-consts)
- impl(050): T029 Slice 1 - engine.gleam facade (new/new_with_prelude/load over loader; disk self.glp via FFI; captured deferred per R4)
- T029 Slice 0 DONE + Slice 0b (output capture) DEFERRED per R4 (captured excluded from parity, no Dart oracle) - Gabi-approved
- impl(050): T029 Slice 0 - scheduler refinement (caps 1-3): faithful RunStatus + blocking-readers + single-step
- T029 safe-restart doc - scheduler refinement (Slice 0/0b) + goal-boot + facade + signaling protocol
- impl(050): T029 slice - loader.compile_prelude (type-check-skipping prelude compile per Dart _loadRootSelf)
- T024 DONE restart banner + T029 bootstrap (resume from 365/365 @ 78dd7ef9)
- impl(050): T024 generic Guard opcode + native body kernels + shared arith; native gleam 365/365
- T024 restart note - kernels + generic Guard plan (resume from 350/350)
- impl(050): T023 structural guards (ground/known/otherwise/=?=/no_readers) in runner; native gleam 350/350
- impl(050): T022 scheduler - run loop + suspend/reactivation; native gleam 346/346
- impl(050): T021 slice 21d - BODY construction + spawn; T021 closed; native gleam 344/344
- impl(050): T021 slice 21c - HEAD structures (writer-MGU crux); native gleam 343/343
- T021 restart note + verified Dart runner.dart architecture map (porting reference for slices 21c/21d)
- impl(050): T021 slice 21a/b - three-phase runner control spine + HEAD-constant + Commit + suspend; immutable stepper porting Dart runWithStatus; writer-keyed Si/U; 3 e2e gleeunit tests run flip end-to-end (native gleam 339/339)
- impl(050): T020 loader - single-entry load pipeline (parse->SRSW->PE->typecheck->codegen->load) with staged diagnostics; prelude threaded as param (no global state, FR-009); 4 gleeunit tests (native gleam 336/336)
- impl(050): T019 codegen - codegen.gleam faithful port of Dart CodeGenerator + P5 merge byte-parity smoke test; additive srsw.clause_register_map (register alloc, option a); native gleam test 332/332
- T018 complete - tick type-checker port task (native gleam test 331/331)
- impl(050): T018 type_checker checkModule - type_checker.gleam + tests port; covariance+contravariance, checkModule wiring (closes T018, native gleam test 331/331)
- impl(050): T018 clause_validation - clause_validation.gleam + tests port; anonymous-reader rejection as Result (native gleam test 322/322)
- impl(050): T018 type_environment_builder - builder.gleam + tests port; alias resolution, determinism, errors-as-Result, prelude-source param (native gleam test 313/313)
- quic_chat round lines - T018 split coordination with Olamnit
- impl(050): T018 well_typed_clause - well_typed_clause.gleam + tests port; counter-threaded, Case-B inference, clause duality (native gleam test 303/303)
- impl(050): T018 well_typed_term - well_typed_term.gleam + tests port; fix program_dfa bare const-label (Dart parity) (native gleam test 288/288)
- impl(050): T018 moded_head - moded_head.gleam + tests port (native gleam test 275/275)
- impl(050): T018 moded_term - moded_term.gleam + tests port (native gleam test 268/268)
- T018 restart note for moded_term/moded_head (Olamnit native-gleam, position @ subtyping done 252/252)
- impl(050): T018 subtyping - subtyping.gleam + tests port (native gleam test 252/252)
- Merge origin/050-full-gleam-combined (T018 chunk C from Olamnit) into local
- impl(050): T018 chunk C - program_dfa.gleam + tests port (native gleam test 246/246; WSL absent on Olamnit, Gabi-approved native run)
- Merge origin/050-glp-native-quic-link into 050-full-gleam-combined
- commit outstanding tree state (manifest churn, EOL normalization, T027 lake manifest)
- T018 handover note for Olamnit workstation
- gitignore reviews/ (codexreview advisory artifacts, never shipped)
- mark T044 done; gitignore glpquick-cert (private keys) + gleam_quic test build artifacts
- install buildkit skills 2026.07.10.1 (rebrand author->buildkit, +bk-3rtask/bk-guards/bk-owo; trim BUILDKIT block)
- US4 T029-T031 - multi-accept mesh isolation, dup-suppression/exactly-once/fault-report, rogue-pin reject + tampered-seal detect over real QUIC (134/134 green)
- impl(050): T018 chunk B - param_expansion.gleam port + TypeExpr toString in type_ast (WSL gleam test 227/227)
- impl(050): T018 chunk A - type-checker foundations (mode.gleam, TypeEnvironment in type_ast, prelude sets; WSL gleam test 221/221)
- impl(050): T017 partial evaluator port to compiler/partial_eval.gleam (both live Dart PE copies; WSL gleam test 212/212; 5 error channels byte-identical to Dart REPL)
- impl(050): T015 parser tests + T016 SRSW checker port (WSL gleam test 184/184; SRSW messages byte-identical to Dart REPL)
- impl(050): T027 Lean PI:14 writer-MGU proof (lake build green, sorry-free) + T028 prose PROOF.md (INDEX flip deferred to T026 discharge commit)
- impl(050): T014 parser hand-port to parser/parser.gleam + T013 CRLF lexer fix (WSL gleam test 120/120; corpus sweep 70/70 Dart-conformant)
- impl(050): T013 lexer hand-port to parser/lexer.gleam (WSL gleam test 119/119)
- impl(050): Phase 2 foundational complete - T006-T012 (v2.16 opcodes, program model, AST, engine types, generation-scoped wake, staged diagnostics; WSL gleam test 104/104)
- impl(050): Phase 1 complete - T001-T005 verified (lake build x2, WSL gleam test 91/91, cross-rig 16/16)
- impl(050): Phase 1 setup scaffolds + baseline record (WIP checkpoint)
- tasks(050): tasks stage complete - 68 dependency-ordered tasks
- plan(050): plan stage complete - research, data model, contracts, quickstart
- spec(050): clarify stage complete - 5 clarifications integrated
- spec(050): specify stage complete for combined Full-Gleam feature
- upgrade buildkit skills to 2026.07.09.1 (buildkit-deploy)
- Merge pull request #104 from olamni-glp/main
- Merge pull request #103 from olamni-glp/release/v2026.07.10.1
- release: v2026.07.10.1
- import olamnit 20260708 export and re-export merged journal
- Merge pull request #102 from olamni-glp/main
- Merge pull request #101 from olamni-glp/release/v2026.07.09.2
- release: v2026.07.09.2
- Merge pull request #100 from olamni-glp/049-wave1-guard-link-acceptance
- Merge pull request #99 from olamni-glp/main
- Merge pull request #98 from olamni-glp/release/v2026.07.09.1
- release: v2026.07.09.1
- Merge pull request #97 from olamni-glp/049a-gavri-us2-us3
- T012+T013+T029-T031 - SC-009 equivalence both forms, parity+gate audit PASS, final baselines green, ship-gate audit ALL FOUR US PASS
- T020/T021 US3 two-host records + us3-verdict PASS; T010 form-a EquivalenceRun evidence
- mark US2a tasks done (T016/T019/T020 - IPayloadCodec seam + egress/ingress rewire)
- US2a - per-link IPayloadCodec seam; egress/ingress route through it; default codec preserves ground-relay byte-for-byte (118/118 green)
- mark T003 realization-checkpoint gate discharged in tasks.md
- plan+tasks+analyze - quic link integration pipeline (register QuicTransport, crdtmsg payload seam, macaroon gate, GLP mesh test); analyze remediations U1/A1/A2/I1/C1 applied
- ship-plan handoff to primary session - full-wave ship runs there on the canonical branch, gated on all 4 US
- Merge pull request #96 from olamni-glp/049a-gavri-us2-us3
- Merge remote-tracking branch 'origin/049-wave1-guard-link-acceptance' into 049a-gavri-us2-us3
- gavri US2+US3 evidence complete - SC-005 + SC-006 both PASS, 90-summary completion signal, transport-soak footnoted as out-of-scope
- gavri MSVC-native quicer attempt - toolchain proven (quictls+msquic.dll link), blocker is upstream unix-only quicer C source (0.2.15 and 0.4.3), escalated per FR-010
- T003 vector addendum - v05 success-on-empty (parity), v12 fail (decidable) per Gabi ruling; 1.14 gate fully discharged
- T003 realization addendum - form (a) via (a1) compiler extension then form (b), per Gabi ruling; v05/v12 outcomes still to be ruled
- us2-verdict follow-up - gavri MSVC correction 8facff21 relayed, PASS unaffected (WSL path stands)
- US2 Profile C verdict PASS SC-005 - gavri evidence reviewed and integrated (T015-T018)
- FR-015 regression coverage - #5 timeout-FAIL + #6 pre-readiness flood pytest, #7 >1MiB reassembly erlang harness PASS local OTP29 (T025-T028)
- US4 marathon durability VERIFIED - kill-resume PASS + durable-first commit re-drive exactly-once PASS (T022-T024, SC-007)
- step-3 checkpoint durable-first, commit withheld by index.lock (T024)
- us4-step-2 checkpoint - marathon durability probe
- marathon durability run record mrun-9724364d684a (T022)
- US3 Olamnit prep - cert generated, addr corrected 192.168.0.143 -> 192.168.0.136, firewall+server handed to engineer (T019)
- baseline checkpoint 524/525 REPL + 114 xUnit + 178 pytest, evidence tree, gavri prompt mesh>=4 fix, delegation record (T001/T002/T004/T014)
- analyze remediations - empty-targets and excluded-vs-unbound edge recorded for T003 ruling, gavri-lane execution semantics, T009 protocol wording (buildkit spec-049)
- tasks - 32 tasks in 4 story lanes, T003 1.14 realization gate, gavri delegation lane, marathon+fixes parallel (buildkit spec-049)
- plan - R1 form-(a) realization checkpoint, shared decision vectors, gavri delegation + evidence contracts (buildkit spec-049)
- gavri evidence - two-host prep done, awaiting cert + Olamnit server (milestone: US3 prep)
- gavri evidence - WSL provisioning + gleam Profile A baseline PASS (milestone: baseline)
- gavri evidence - environment discovery (US2/US3 delegation, milestone: environment done)
- Merge pull request #95 from olamni-glp/049-wave1-guard-link-acceptance
- roadmap-sync import manifest - applied 6 olamnit exports (1031 journal lines) on gavriellas host
- clarify - 1.14 approved staged a-to-b, hard ship gate, gavri delegation prompt (buildkit spec-049)
- specify wave-1 consolidated - GLP policy-guard (1.14-gated) + 036 link full acceptance (buildkit spec-049)
- Merge pull request #94 from olamni-glp/048-colab-foundations
- scan-reconcile 042/043 released, dedup A-I closed with engineer approval, CRDT migration 0022, double export-import idempotent (0 dup groups, 86 features)
- codify bounded-behavior cap+error+test rule (act-62c7bf6a99)
- spec(043): additive-optional carve-out - optional additions stay forward-compatible per engineer decision, closing review escalation
- post-ship close-out retrospective for v2026.07.08.1
- Merge pull request #93 from olamni-glp/main
- Merge pull request #92 from olamni-glp/release/v2026.07.08.1
- release: v2026.07.08.1
- Merge pull request #91 from olamni-glp/043-xsd-schema-language
- refine(codexreview): cycle 5/6 [diff/general]
- refine(codexreview): cycle 4/5 [diff/general]
- refine(codexreview): cycle 3/4 [diff/general]
- refine(codexreview): cycle 2/3 [diff/general]
- Checkpoint: 043 project skeleton, substrate baseline green (wire_registry 6/6, crdtmsg 86/86)
- analyze(043-xsd-schema-language): apply remediations I1 I2 A1 A2 U1 T1 W1 - closed-world compat rows per spec US4-AS2, drop symbol primitive, scope FR-007 agreement law, pin QmeditDsl=XsdSource for 043 entries
- tasks(043-xsd-schema-language): 36 tasks, tests-first per story - setup 3, foundational 8, US1 7 MVP, US2 4, US3 5, US4 5, polish 4
- plan(043-xsd-schema-language): plan + research R1-R12 + data-model + 5 contracts + quickstart - new csharp/glp_schema_lang over seeded overlay, E9 tables untouched
- clarify(043-xsd-schema-language): 3 clarifications - plaintext qmedit-family DSL (no XML), cycles rejected at schema-validation, evolution refuses without declared compatibility mode
- specify(043-xsd-schema-language): spec + quality checklist - XSD-style schema layer over E9 dual-DSL functor registry, 4 stories, 14 FRs, 6 SCs, zero markers (2 open choices routed to clarify)
- commit crdtmsg-verify-harden retrospective artifact from close-out
- Merge pull request #90 from olamni-glp/main
- Merge pull request #89 from olamni-glp/release/v2026.07.06.1
- release: v2026.07.06.1
- Merge pull request #88 from olamni-glp/042-crdtmsg-verify-harden
- refine(codexreview): cycle 4/3 [diff/general]
- refine(codexreview): cycle 2/3 [diff/general]
- commit crdtmsg-mvp draft retrospective artifact from close-out
- impl(042): COMPLETE - report assembled s9/s11/s12 all nine SC PASS, SC-008 zero silent edits (61 rows), SC-009 refs in all 3 docs, T030 env-blocked bracket reproduced (T029-T031)
- impl(042): US4 complete - 231/231 pointers resolved SC-007 met (225 resolve, 2 host-blocked, 2 link-rot corrected in F2, 2 transcript unrecoverable), Tier1 39/39 bib-verified, report s8 (T026-T028)
- impl(042): T026 partial - 83/231 evidence pointers resolved (in-repo+sibling+transcript classes), transcript pointers superseded in F3 (rows 45-46); 148 F2 URLs pending web sweep
- impl(042): US3 complete - register closure SC-003 met (2 promoted incl BB-CRDT-7 self-promotion, 6 re-affirmed, 0 escalations), report s7+s10, F3 change-log rows 35-44 (T021-T024)
- impl(042): US2 complete - T016 merge rederivation 37/37 COHERENT, T017 26 blind rescans over 13 singletons, T018 curation SC-002 met (11 confirmed, 2 no-further-evidence, 0 escalations)
- impl(042): US1 conformance ledgers complete (SC-001 3/3, 18 elements) + US2 ledger rederivations corrected + drift dispositions + E1-E9 propagation fixes + pointer census 231 rows (T007-T015,T019,T020,T025)
- impl(042): Phase 2 foundational - report section 1 method reconstruction, 18 elements F1/F2/F3 RECORDED-vs-RECONSTRUCTED (T004-T006)
- impl(042): Phase 1 setup - report+evidence skeletons, changelog sections, scanner-C view resolved d2689a71, env-blocked baseline recorded (T001-T003)
- analyze(042-crdtmsg-verify-harden): 0 critical/high - applied 5 remediations (changelog skeletons in T001, tasks.md authoritative ordering, SC-008 min(10) sampling, T021 wording, 4-vs-3 ledger note)
- tasks(042-crdtmsg-verify-harden): 31 tasks across 4 stories - US1 conformance MVP, US2 hardening w/ blind re-scans, US3 register closure, US4 evidence census; single-writer-per-doc rule
- plan(042-crdtmsg-verify-harden): verification plan - 5 WPs, hybrid baselines pinned (c20317ce/6ecc975f/v2026.07.04.4), method-strength survey, report+ledger+changelog contracts
- clarify(042-crdtmsg-verify-harden): 3 rulings encoded - targeted re-execution (FR-014), mechanical PROV promotions w/ batch review (FR-008), hybrid delivery-time/HEAD baseline (FR-005/FR-015)
- spec(042-crdtmsg-verify-harden): verification+hardening spec for F1/F2/F3 against their frozen 3-role methods; 1 clarify fork (FR-014 evidence depth)
- Merge pull request #87 from olamni-glp/main
- Merge pull request #86 from olamni-glp/release/v2026.07.04.4
- release: v2026.07.04.4
- Merge pull request #85 from olamni-glp/041-crdtmsg-mvp
- analyze (041) - apply top remedies (FR-019/031/023 coverage, FR-038 relabel)
- tasks (041) - 57 tasks by user story, store-first, tests-first
- plan (041) - design artifacts, C# workspace, store-first, constitution PASS
- clarify (041) - rich-text CRDT mandatory, C# primary, guard gated on 1.14
- specify crdtmsg-mvp (041) - CRDT multi-format messaging MVP spec
- dedup 55 dup-GUID groups from cross-host merge (0 dups; 78 feats/18 epics) + restart pointer
- restart pointer for 2026-07-04 — ship v2026.07.04.1 done, roadmap synced; NEXT=dedup dup-GUIDs, bk-upgrade+deploy, marathon
- roadmap-sync import+export (olamnit↔gavriellas cross-host merge, idempotent)
- bk-close retro for F3 cycle - 3 findings (agent-teams win, release-planner creatordate gotcha, retro-inputs gap)
- Merge pull request #83 from olamni-glp/main
- Merge pull request #82 from olamni-glp/release/v2026.07.04.3
- release: v2026.07.04.3
- roadmap export 20260704 pre-release
- promote crdtmsg-mvp + export 20260704
- roadmap - virtual-3270-term released (superseded via 040) + export 20260704
- roadmap export 20260704T070059Z post-ship (13 epics, 75 features, 1003 journal lines)
- bk-close retrospective - 4 findings (2 systematic: ship-state visibility, review-ledger) + CLAUDE.md update
- Merge pull request #81 from olamni-glp/main
- Merge pull request #80 from olamni-glp/release/v2026.07.04.2
- release: v2026.07.04.2
- Merge pull request #79 from olamni-glp/037-virtual-3270-term
- Merge pull request #78 from olamni-glp/037-virtual-3270-term
- roadmap fold - crdtmsg-verify-and-harden feature + 3-role dogfood win note + codify notes + exports 20260704
- Merge pull request #77 from olamni-glp/037-virtual-3270-term
- roadmap capture fix - crdtmsg F1/F2 released with doc pointers + export 20260704T063315Z
- Merge pull request #76 from olamni-glp/main
- Merge pull request #75 from olamni-glp/release/v2026.07.04.1
- release: v2026.07.04.1
- Merge branch 'develop' of https://github.com/olamni-glp/GLPNET into develop
- Merge pull request #74 from olamni-glp/037-virtual-3270-term
- roadmap-sync export 20260703T213044Z for cross-host resume (13 epics, 74 features)
- Merge pull request #73 from olamni-glp/037-virtual-3270-term
- sweep session artifacts - deploy/guardian/roadmap-sync state, 038 retro mirror, crdt-multiformat-messaging research
- Merge origin/develop into develop (integrate 036/037/040 work + PR #72)
- plan+tasks+analyze for complete+hardened virtual-3270 terminal (Phase 0/1 artifacts + 60 tasks + top remedies)
- Merge pull request #72 from olamni-glp/037-virtual-3270-term
- specify+clarify complete+hardened virtual-3270 terminal (superset of 037: US1-9 / FR-001..046 / SC-001..013; 3 clarifications)
- record buildkit v2026.07.03.1 deploy audit log
- refresh restart pointer — 035+ audit outcome, link-completion fixes done, next=T019 then promote+specify 040
- 035+ oblivion audit, 3-role-team method+dogfood, 040 complete-hardened-3270 capture, 2 codify notes
- restore virtual-3270-term spec on develop base; renumber /rcopy backend refs 038 to 040 (038/039 shipped)
- close-out retrospective report (4 root-cause findings) for v2026.07.02.3
- Merge pull request #71 from olamni-glp/main
- Merge pull request #70 from olamni-glp/release/v2026.07.02.3
- release: v2026.07.02.3
- Merge pull request #69 from olamni-glp/036-http3-quic-ws-link
- T037 done — single-host quickstart validated (csharp SC-001..005+mesh, gleam Profile A SC-001..006 all PASS); record deferred acceptance as known-issues Issue 11
- Merge origin/develop into 036-http3-quic-ws-link (integrate 130 commits: bk-* skill rename, gleam-port 031-039, engine-split); resolve feature.json/CLAUDE.md/current_plan.md to 036 + preserve gleam-baseline T015 pointer
- carve deferred acceptance (T003/T032/T036/T040) into roadmap feature http3-quic-ws-link-full-acceptance + follow-up brief
- Merge pull request #68 from olamni-glp/main
- Merge pull request #67 from olamni-glp/release/v2026.07.02.2
- release: v2026.07.02.2
- Merge pull request #66 from olamni-glp/038-result-codec-and-framecodec-ride
- 8 codify notes from 2026-07-02 roadmap history reconciliation (reconcile bug, post-ship stall, backfill gap, number collisions, scan-method win)
- Merge pull request #65 from olamni-glp/main
- Merge pull request #64 from olamni-glp/release/v2026.07.02.1
- release: v2026.07.02.1
- Merge pull request #63 from olamni-glp/038-result-codec-and-framecodec-ride
- Merge remote-tracking branch 'origin/develop' into 038-result-codec-and-framecodec-ride
- T044 doc audit + T045 end-to-end validation — Dart 83/C# 131/builder 14/Gleam 91 + golden harness PASS + AtomVM gated PASS; all 44 tasks done (+T042 optional authored)
- T038/T039 verified green — REPL 524/525 (1 unrelated AOT-smoke fail, no 036 regression), glp_quick 18 pytest + glp_link 104 xUnit
- 038(impl): US2 golden corpus authored from Dart + Dart byte-identity test (T004/T026/T029/T030); 69 Dart codec tests green
- 038(impl): C# + Gleam result-codec fan-out — byte-identical to Dart source of truth (T002/3/5/6/8/9/11/13/24); C# 84/84, Gleam 68/68 green
- 038(impl): Dart engine->envelope builder + depth-32 deep-resolve (T017/T018/T019); MVP sub-checkpoint green (55 codec tests)
- 038(impl): Dart codec foundation — value types + term sub-codec + envelope frame codec; US1 round-trip/no-heap/in-process green (T001/T007/T010/T012/T014/T015/T016)
- 038(analyze): cross-artifact analysis — 0 critical/high, 100% coverage; applied U1 remedy (Gleam GlobalVarId agentId = explicit builder param, no Gleam engine yet)
- 038(tasks): 45 tasks across 6 phases by US1/US2/US3; MVP=US1 Dart envelope round-trip+no-heap; gated float/64bit/cyclic quarantined
- 038(plan): result-envelope codec plan — rides Section-15 term codec (029 conventions), buildable on 034 w/o F5; D4=A/ED-6=A encoded; float/64-bit-edge/cyclic-term gated
- Merge pull request #62 from olamni-glp/main
- Merge pull request #61 from olamni-glp/release/v2026.06.30.1
- release: v2026.06.30.1
- Merge pull request #60 from olamni-glp/039-m2-0-verify-erlang-monitor-atomvm
- 039(implement): VERDICT=works — erlang:monitor/2+DOWN faithful on AtomVM 0.6.6 (vs OTP-25); spawn_monitor/1 absent (use spawn+monitor); D10 fork not triggered
- 039(implement MVP): monitor_probe + OTP-25 reference (normal/boom/noproc); AtomVM 0.6.6 run blocked on host provisioning (not present in WSL)
- 039 tasks: T001-T007, MVP=T001-T003 (toolchain, probe, run+observe normal-exit DOWN).
- 039 plan: Erlang monitor probe built+run on AtomVM 0.6.6 via F1 WSL toolchain; 5 phases (toolchain confirm, MVP normal-exit DOWN, abnormal exit, edge+fallback, verdict).
- 039 m2-0 specify: gating spike to verify erlang:monitor + DOWN on AtomVM 0.6.6; gate-free (D10 fork only on negative result). sidecar specify=complete; marathon mrun-117a92c4eea7.
- 038 clarify: owner-ruled D4=A (freeze toward v2, author Section-15 in the freeze) and ED-6=A (authorize AtomVM float-decode spike); NEEDS CLARIFICATION resolved. clarify=complete; plan next.
- 038 specify: result-envelope codec spec (rides ED-6 Section-15 codec; framing/transport split to #36). 2 owner gates marked NEEDS CLARIFICATION: D4 ISA-freeze, ED-6 float-decode-on-AtomVM. Pipeline sidecar specify=complete; marathon run mrun-67d510b22e34.
- Merge pull request #59 from olamni-glp/036-glp-gleam-baseline-program
- 036: program complete — P1/P5/spike research artifacts + spec/plan/contracts; T015 two-epic roadmap reconfiguration applied & marathon mrun-5611c436ba95 discharged (also sweeps 034/035 retros + BEACON-JOIN.md per commit-all)
- 036: restart pointer — T014 approved, T015 migration next in new session
- 036(T013): completeness-critic pass + folded gap fixes
- 036(T007): P8 two-epic reconfiguration synthesis
- 036(T012): P3 opportunities register (70; saturated)
- 036(T011): P2 concerns register (218 concerns; loop not yet saturated -> T013)
- 036(T010): P7 QHSM/YngeniOS integration dossier
- 036(T006): P1b corrected realignment dispositions
- 036(T009): P6 Gleam/AtomVM implementation-strategy dossier
- 036(T008): ANTLR-integration deep-dive dossier (FR-005 verified via spike)
- 036(T005): P4 proof artifact register (3 proved / 2 open)
- 036(T004): P4 faithfulness parity bar (M1+M2, primary-source-cited)
- 036(T003): pipeline status index for the glp-gleam-baseline research machinery
- 036(T002): proof-harness wiring for the glp-gleam-baseline research machinery
- 036(T001): corpus index for the glp-gleam-baseline research machinery
- fold RDP command-mode hard requirement + prototype learnings into virtual-3270-term reqs
- intake briefs for virtual-3270-term (full 3270 reqs), durable-mesh-messaging-protocol, and HTTP3-QUIC-WS (036 record + re-specify prompt + restart prep)
- commit gleam_quic dependency lockfile (manifest.toml)
- rework plan/tasks/analyze to 2026-06-28 clarifications (genuine WS-over-QUIC, cross-platform C#, two Gleam profiles)
- correct WS-over-QUIC framing (first-class, de-facto) + cross-platform .NET QUIC; encode 2026-06-28 clarifications
- research corpus (106 sources) + distillation; resolve RFC 9220 + AtomVM-QUIC feasibility
- plan + research + data-model + contracts + tasks; analyze remediations (constitution tokens, addressing/mid-drop coverage, scenario numbering)
- clarify GLP-over-link (REPL mesh), C#-first build order, concurrency, cert distribution
- specify HTTP/3 QUIC + WebSocket channel-link prototype
- Merge pull request #58 from olamni-glp/main
- Merge pull request #57 from olamni-glp/release/v2026.06.26.1
- release: v2026.06.26.1
- Merge pull request #56 from olamni-glp/035-semantic-tombstone-enrichment
- 035(fix): --from-tombstones rebuild carries purpose_source/key_idea_source (FR-008) — was resetting inferred/doc to absent; pre-035 derives from blank-ness; +regression test
- 035(corpus): enrich glp_runtime_net tombstones via Claude seam — 68 inferred (9 compiler + 59), 104 doc, 7 stubs left blank; gitignore enrich-runs logs
- 035(enrich): mark T023 (consolidated feature gate 22/22 green) — all 24 tasks complete
- 035(enrich polish): T022 isolated quickstart e2e (dry-run + scoped enrich + FR-014 git-diff); T024 SC-004 grep guard verified
- 035(enrich US3): --path scope + per-file fault isolation + low-confidence + run summary/durable log; T018 green
- 035(enrich US2): discover provenance-aware seed + conditional inferred-preservation (FR-008); enrich idempotence/stale-guard; T013/T014 green
- 035(enrich US1/MVP): run_enrich candidate scan + Claude-seam infer/write + non-candidate stamping + no-API exit-2; T007/T008 green
- 035(enrich P1-2): tool skeleton + no-API seam + migration 0011 + frontmatter provenance keys + head tests
- 035(plan/tasks/analyze): semantic tombstone enrichment pipeline artifacts + analyze remediations (B1 len-caps, C1 file run-log, D1/E1/F1)
- Merge pull request #55 from olamni-glp/main
- Merge pull request #54 from olamni-glp/release/v2026.06.25.1
- release: v2026.06.25.1
- Merge pull request #53 from olamni-glp/034-glp-gleam-core-terms-and-heap
- 034(F4): codexreview fixes — deref self-bind->Unbound (Dart parity), forward suspensions to terminal writer (FR-008), correct R-007/parity-evidence claims, +4 tests (54 green)
- 034: implement glp_gleam core terms+heap+unify (F4) — immutable threaded store, 50 tests green on BEAM
- 034: plan/tasks/analyze for glp_gleam core terms+heap+unify (F4) — immutable threaded store; 4 analyze remediations
- Merge pull request #52 from olamni-glp/main
- Merge pull request #51 from olamni-glp/release/v2026.06.24.2
- release: v2026.06.24.2
- Merge pull request #50 from olamni-glp/033-glp-gleam-subtree-scaffold
- upgrade installed artifacts to v2026.06.24.3
- analyze(033): apply top remediations — clarify FR-007/SC-005 wired-in wording; strengthen T021 (FR-008 establish+verify) and T018 (FR-006 segment legality)
- tasks(033): 25 tasks for glp_gleam subtree scaffold (US1 MVP build+test, US2 8 placeholders, US3 smoke+recognition)
- plan(033): glp_gleam subtree scaffold — plan, research, data-model, contracts, quickstart
- Merge pull request #48 from olamni-glp/main
- Merge pull request #47 from olamni-glp/release/v2026.06.24.1
- release: v2026.06.24.1
- Merge pull request #46 from olamni-glp/032-codeconv-gleam-langpair
- refine(codexreview): cycle 2/10 [diff/general]
- refine(codexreview): cycle 1/10 [diff/general]
- analyze(032): remediate F3 (add PairMismatch coverage to T008); F1/R-003 owner decision pending
- tasks(032): 20 tasks across 3 user stories; R-003 owner-decision gate flagged before implement
- plan(032): Dart->Gleam langpair plan + Phase0/1 artifacts; flag FR-005<->FR-008 collision tension (R-003)
- clarify Gleam target path policy (verbatim mirror, F3 owns layout)
- add codeconv-gleam-langpair (Dart-to-Gleam) feature spec + checklist
- Merge pull request #45 from olamni-glp/main
- Merge pull request #44 from olamni-glp/release/v2026.06.22.1
- release: v2026.06.22.1
- Merge pull request #43 from olamni-glp/031-gleam-port-spike
- fix codexreview cycle-2 evidence findings (correct C# inventory counts, record JS-probe output)
- fix codexreview cycle-2 residual (stale gleam_otp mention in js-probe comment)
- fix codexreview cycle-1 findings (gleam_otp stale listing x2, JS actor citation, inventory JS-build, AtomVM packaging note)
- gitignore buildkit refine cache (.specify/.refine-cache/, regenerable)
- full Gleam smoke runs on AtomVM (raw erlang:spawn, no gleam_otp) + codex-review fixes
- Gleam port spike deliverables - dossier, toolchain inventory, hello-glp-term smoke
- spec(031): plan, tasks, analyze remediations for Gleam port spike
- Merge develop (bk-* aliases, pinned CLI) into 031-gleam-port-spike
- Merge pull request #42 from olamni-glp/chore-bk-aliases-pin-v2026.06.17.1
- pin CLI v2026.06.17.1, apply /bk-* aliases, register deploy
- spec(031): Gleam port source+toolchain / AtomVM feasibility spike
- Merge pull request #41 from olamni-glp/main
- Merge pull request #40 from olamni-glp/release/v2026.06.19.1
- release: v2026.06.19.1
- Merge pull request #39 from olamni-glp/030-marathon-refinement
- T058 full-suite gate done + T057 addendum (Fix A supersedes the prereq-patterns junction workaround; 34/34 marathon on reconciled tree, real-CLI primary-store smoke green)
- T057 quickstart e2e validated + tasks.md T051-T057 DONE notes (Phase 8 complete except T058 full-suite gate, held for the Sunday 2026-06-14 ~01:00 intensive-regression window)
- T056 /marathon-stage-harness skill drives the refined CLI - canonical --run, data-driven register/append-stage/capture intake, keeper lifecycle + hygiene, rule-2a re-drive in Restart-Resume step 4, gate/rerun by stage NAME, full contracts/cli.md command table, preauth grants documented as library-level Repository.update_run
- T055 point marathon-stage-harness references at the refined model - CLAUDE.md + current_plan.md now describe the data-driven per-run isolated store (default C:/pglite/marathon/<run-id>, keeper, JSON mirror), canonical --run resume (--feature deprecated alias), 030 contracts pointer; 024 shared-cluster schema noted inert history (VIII)
- T054 Constitution V guard - zero OPENAI_API_KEY/litellm/openai tokens anywhere in the marathon package source; bridge-free 1/1
- T053 shared-cluster no-new-head guard - Alembic head stays exactly 0010, no versions/ file beyond 0010, only marathon migration is 024's inert 0010_marathon_schema, per-run store schema imports no Alembic machinery (VI-a, D2); bridge-free 4/4
- T052 resume-position byte-identity (SC-008) - pure derive_position over reconstructed+reshuffled rows (incl. rule-2a re-drive branch) and live three-way check (session env / fresh env / fresh CLI subprocess) all canonical-JSON byte-equal; 2/2
- T051 CLI parity guard - registered Typer surface == contracts/cli.md table, declared lib functions importable, callback wiring references its declared function, no function owns two subcommands (position->resume alias folded); bridge-free 4/4
- Merge pull request #38 from olamni-glp/main
- Merge pull request #37 from olamni-glp/release/v2026.06.12.1
- release: v2026.06.12.1
- Merge pull request #36 from olamni-glp/030-marathon-refinement
- Merge pull request #35 from olamni-glp/main
- Merge pull request #34 from olamni-glp/release/v2026.06.11.1
- release: v2026.06.11.1
- Merge pull request #33 from olamni-glp/029-il-codec-spike
- refine(codexreview): cycle 1/10 [csharp/glp_il_codec/general]
- specify evidence-based-constitution feature (spec + requirements checklist)
- Merge pull request #32 from olamni-glp/main
- Merge pull request #31 from olamni-glp/release/v2026.06.10.1
- release: v2026.06.10.1
- Merge pull request #30 from olamni-glp/027-refinement-verification-framework
- refine(codexreview): cycle 1/10 [diff/general]
- commit marathon m57f4c46e durable JSON-mirror state for block 07 (open+approve, checkpoints 13-14, git block) — feature complete 28/28
- commit marathon m57f4c46e durable JSON-mirror state for block 06 (open+approve, checkpoints 11-12, git block)
- commit marathon m57f4c46e durable JSON-mirror state for block 05 (open+approve, checkpoints 9-10, git block, run-linkage)
- commit marathon m57f4c46e durable JSON-mirror state for block 04 (open+approve, checkpoints 7-8, git block, status)
- commit marathon m57f4c46e durable JSON-mirror state + spin scratch ahead of safe restart
- pipeline artifacts (plan/research/data-model/quickstart/contracts/tasks) + marathon launch prompt; buildkit pointer 026->027
- spec #1a refinement-verification-framework (Option D + real-tool validation spikes: Lean/MLIR/SPIN; protocol-verification armoury) + ratify R13-R15 + DEF-A3
- ratify 12 MVP-critical decisions (DECISIONS-LOG R1-R12) + anchored deferral register (DEFERRALS.md, stages A-H + pickup protocol); seed notes carry PRE-SPECIFY pointers
- apply reconciliation corrections - D3 FrameCodec payload-type-prefix-byte (header Kind is fragmentation-only), shallow-vs-deep Bindings clarification, LingoDB citation candidate; (§7 note was provenance, not a typo)
- 17-seed reconciliation memos + README index + DECISIONS-FOR-OWNER + REFINEMENT-METHOD (GEPA/DSPy + formal/pragmatic metrics, per-seed Lean4-vs-Rocq, monolith supersession)
- record owner decision - evaluate Lean4 + Rocq per seed, pick best-fit primary, keep alternative only where identified
- add #1a iterative-refinement-and-verification-framework seed; complete reconciliation brief with formal+pragmatic verification methodology (MLIR IL-dialect, model-agnostic Lean/Rocq via Claude, ANTLR4 grammar-verifier, Shapiro-criteria pragmatic anchor, no-API resolution)
- dossier seed cross-refs (in-situ §1-§9 + Appendix B registry) + reconciliation brief with GEPA/DSPy metrics methodology and formal-verification research
- engine-separation design dossier (§0-§12, re-verified citations); complete tasks; verify roadmap seeds
- plan + Phase-1 artifacts + tasks + analyze remediations for engine-review-dossier
- engine review + refactoring design dossier spec (specify + clarify)
- Merge pull request #29 from olamni-glp/main
- Merge pull request #28 from olamni-glp/release/v2026.06.08.1
- release: v2026.06.08.1
- Merge pull request #27 from olamni-glp/025-multi-protocol-link-layer
- codexreview cycle 1 — per-peer timeout guard on link harnesses (fail-fast, never hang the gate)
- marathon status checkpoint row 100 (GATE D + cross-runtime green)
- cross-runtime Dart<->C# link rig — 16/16 both directions (release gate T042/T081)
- persistent-embeddable-engine epic dossier (REPL/engine separation)
- wip(025): Phase D async-correctness fixes — Dart<->Dart 6/8 two-process GATE-D green
- wip(025): Phase D layer 2 partial — link primitives infra+glue + null-guarded core inbound-pump seam
- fault-monitor + graceful-close two-process link example (FR-008/044)
- bidirectional (FR-003) + link_recv-chain two-process link examples
- checkpoint WIP — requirements edit, design dossier (docx/pdf), transport-runtime-feasibility research, buildkit-codexreview skill, marathon harness state
- refresh restart resume pointers - CURRENT STATUS block in runtime-integration-plan.md (Phase A/B done, Phase C 4/4 two-process examples + driver, next = link_recv-chain debug -> monitor/path-B/bidir -> Dart mirror -> regression); tasks.md points to it
- add link_send/3 wrapper producer (producer_ls) to pc.glp + driver - 4/4 two-process examples PASS (integers, strings, compound terms, link_send wrapper over real TCP). Isolated: explicit link_recv-chain consumer has a separate runtime issue (link_recv alone suspends correctly; the 3-recv concurrent-body variant fails) - next debug batch
- scripted 2-process real-TCP link integration driver (test/link/run_link_tests.sh) + pc.glp integer/string/compound-term producers; 3/3 PASS over 127.0.0.1 (Got byte-identical to produced values); results captured to test/link/results/
- T032 recv-ingress contract proof (suspend/reactivate-once/dup-no-op/reorder on the T030+T022 ingress; link_recv composable; 76/76 xUnit)
- resume pointer + T030 status (infra+Option-B done, kernel next); marathon-checkpoint-stale caveat
- inbound-pump + isolate_manager design reference (md/docx/pdf) + Option-B decision record
- T013 FR-032 consolidate guards-reference.md as single authoritative guard spec (fold in @< @> @=< @>= standard-order family + atom/1=string synonym + decline == \== \= reader/1 with canonical forms; =\= unchanged; nested-compound suspend note)
- T012 FR-033/036/037/038 guard three-valued + decline + =\= regression (@< & atom reactivate-exactly-once Section A24g; =\= untouched A24h; declines == \== \= reader/1 rejected Section C; @< & atom SRSW-relaxation Section B; +13 checks, suite 524/525)
- correct exemplar GLP per REPL-verified canonical forms (channel-head modes, send-shape, output-holes, bare-_ singletons, body-= -> head-construct, Fault/Link types); add adversarial GLP review (2 passes) + canonical-forms card
- plan block - plan/tasks/analyze + design dossier, contracts, per-transport tutorials, integration-harness + coverage matrix (gate ruled: 9 primitives + guard set + 3 core fixes)
- clarify block — resolve peer-id ordering (ruling B: compound/totally-ordered, @</@> family in scope)
- specify block - spec.md (67 FR/17 SC/4 stories) + requirements checklist
- Merge pull request #26 from olamni-glp/marathon-harness-hardening
- lock B2/B3/G rulings — C#-first reference, base-primitives-before-glink, keep+implement comparison-guards, keep BLE BIS, cross-runtime Dart<->C#
- B2/B3/G decision doc + 18-source provenanced corpus (multi-protocol-link-layer design study)
- end-to-end marathon kickoff prompt for multi-protocol-link-layer (fresh-session launch template)
- SKILL.md — honor rerun workflow_run_id (resumeFromRunId) + resume commit_push_pending crash-window on resume
- Merge pull request #25 from olamni-glp/main
- Merge pull request #24 from olamni-glp/release/v2026.06.05.1
- release: v2026.06.05.1
- Merge pull request #23 from olamni-glp/024-marathon-stage-harness
- refine(codexreview): cycle 1/5 [diff/general]
- plan + tasks + analyze for marathon-stage-harness (one logical block)
- specify + clarify marathon-stage-harness spec
- roadmap + buildkit pipeline state as the restart-resume source of truth; current_plan.md → thin pointer
- add buildkit-roadmap skill forwarder
- mark comparison guards implemented in glp-bytecode-v216 11.7 (was stale Planned)
- Merge pull request #22 from olamni-glp/main
- Merge pull request #21 from olamni-glp/release/v2026.06.04.1
- release: v2026.06.04.1
- Merge pull request #20 from olamni-glp/023-glptutorial-run
- add buildkit-ship + buildkit-release skill forwarders (CLI was installed; skills were missing)
- gated real-backend coverage for ch03 multi-compose + ch07 use-case (US2)
- plan, research, data-model, contracts, tasks for /glptutorial-run
- Merge pull request #19 from olamni-glp/main
- Merge pull request #18 from olamni-glp/release/v2026.06.03.3
- release: v2026.06.03.3
- Merge pull request #17 from olamni-glp/main
- Merge pull request #16 from olamni-glp/release/v2026.06.03.2
- release: v2026.06.03.2
- Merge pull request #15 from olamni-glp/021-buildkit-gitflow-adoption
- adapt glpnet branching/versioning to canonical buildkit GitFlow (feature->develop->release->main, CalVer vYYYY.MM.DD.N via buildkit release; CLAUDE.md branch rules + end-of-task ship)
- Merge pull request #14 from olamni-glp/main
- Merge pull request #13 from olamni-glp/release/v2026.06.03.1
- release: v2026.06.03.1
- Merge pull request #12 from olamni-glp/020-trace-equivalence-fidelity
- plan - top-priority Dart convergence mandate (glpnet glp_runtime <= sibling GLP, 100% byte-level, static+dynamic)
- design - combined comprehensive equiv test driver + goal-bearing corpus (suites + sibling tutorials; ratified decisions 1-4)
- back up frozen build-only bytecode.md (9506ac81) before T031 fidelity re-run can overwrite it; restore via cp
- .codeconv updates
- HANDOFF - turnkey T031 fidelity-metric-swap build spec (part-a metric rewrite mock-testable now; part-b GEPA re-run forces the T018-capture sequencing decision); T017/T022 marked done in S3
- HANDOFF - T022 COMPLETE (parse_dart adapter + finding-#3 deref + e2e green); next = T031 fidelity-metric swap + GEPA re-run
- T022 e2e - append strict-tier oracle equivalence over captured pair (Dart golden = C# candidate); finding-#3 + parse_dart regression guards + negative controls; 6 green
- HANDOFF - one-line state points at T022 parse_dart as the immediate next (T017 complete)
- HANDOFF - turnkey parse_dart build spec (line-by-line append mapping, shape canonicalizer incl list syntax, C# OUT deref fix); goal kept via relabeling done
- T022 - capture matched append fixtures (C# canonical EV/OUT + Dart :trace+:debug) for the parse_dart adapter + e2e
- HANDOFF - T022 scoping (parse_dart finalization plan + 3 normalization items; goal-field comparability decision teed up)
- HANDOFF - #2 RESOLVED (conditional Commit emission, not a runner bug); append spine matches golden exactly
- HANDOFF - finding #1 RESOLVED via option-a spine alignment; #2 (Ground->Commit) isolated as sole remaining append divergence
- HANDOFF - T017(ii) done; record real-capture findings (Dart :debug partial-spine spec-gap, Ground soft-fail spine divergence, shallow OUT shape)
- HANDOFF - Stage 5 progress: T017(i) wired + first fidelity bug (scheduler onReduction) fixed; carry-forward note
- safe-restart prep - re-verify anchor green 2026-06-03; pure subset 40->36; note section-1c run-from-repo-root bridge trap
- SAFE-RESTART handoff — Stages 1-4 DONE (incl Stage 4 canonical no_emit), only Stage 5 left; anti-drift facts (runner.cs compile-verified-only + semantic-risk list) + verified-green anchor + Stage-5 recipe; ledger RESTART pointer
- ledger — Stage 3 DONE (runner.cs converted+built), Stage 4 code DONE (canonical migrate+mark GATED on Gabi OK), Stage 5 unblocked+mapped
- spec(020-trace-equiv): gepa_optimizer contract — NO-API/Claude-driven GEPA revision (ruled 2026-06-03); the spec-first basis Stage 1 implements
- ledger — Stages 1+2 DONE (72ca51d1, 9506ac81); runner.cs (Stage 3) is the gate, Stage 5 blocked on it, Stage 4 no_emit confirm-with-Gabi; precise restart maps recorded
- ledger — Stage 1 (Claude-driven GEPA wiring) DONE at 72ca51d1; NEXT=Stage 2 GEPA on bytecode
- mark bulk drive COMPLETE at 73/75 (97.3%); escalations resolved + final-surface analysis
- bulk drive PAUSED at 48/75 — escalation cascade analysis + Gabi-decision request
- checkpoint ledger at 47/75 built (mid bulk drive)
- record bfd00a8a + flip POSITION to A in-progress
- record dc997583 in safe-restart ledger
- handoff for /buildkit-implement (safe restart) — pipeline through analyze green, Next=/buildkit-implement, MVP=US1, resolve R11 out/csharp git policy first
- tasks(019-codeconv-codegen): 46 tasks across 7 phases (Setup→Foundational→US1 MVP→US2 GEPA→US3 review/test→US4 durable→Polish); migration 0007, dual-tool split, codegen DBOS stage; R11 git-policy gate (T045) before bulk gen
- plan(019-codeconv-codegen): impl plan + Phase 0 research (R1-R12) + Phase 1 data-model/quickstart/7 contracts — (C) hybrid (deterministic codegen tool + offline DSPy/GEPA optimizer), composite metric, dart_codegen + migration 0007, codegen DBOS stage after plan; agent-context → 019 plan
- gitignore buildkit pipeline pgdb/ (local PGLite+DBOS cluster state, regenerable)
- migrate spec-kit → buildkit (local source v2026.05.20-9) — 14 buildkit-* skills replace speckit-*; .specify scripts/templates/extensions/workflows/manifests + CLAUDE.md token-rewritten speckit→buildkit; specs/ (incl. in-flight 019) and constitution.md preserved verbatim; later upgrades via buildkit upgrade apply --ref <tag>; backup at .buildkit-upgrade-backup/ (gitignored)
- spec(019): tidy stale 'to confirm in clarify' phrase — architecture (C) is confirmed
- clarify(019-codeconv-codegen): resolve 5 clarifications (Session 2026-05-23) — (C) hybrid confirmed; composite metric 0.6 tests/0.4 human + build hard-gate + sampled review + median≥4/5 gate; staged test-scope; OpenAI-via-litellm offline-only optimizer + GEPA budget cap; dart_codegen table + migration 0007 + codegen builder stage; 0 markers remain
- spec(019-codeconv-codegen): initial spec — GEPA/DSPy-optimized Dart→C# codegen stage; (C) hybrid + composite metric proposed; 3 NEEDS CLARIFICATION deferred to /speckit-clarify
- archive planagents frontier scratch (all-scoped/completed/remaining/scoped path-lists) into .codeconv/archive/
- snapshot(018-live-pass): COMPLETE — 128/128, 122 specced+scaffolded + 6 escalated (4 pre-existing + isolate_manager.dart + rpc_routing_test.dart), naw=0; 20 batch-of-5 specs ingested clean, single new load-bearing escalation = .NET execution-context choice on isolate_manager (heap_fcp-dependent), rpc_routing_test inherits + adds a same(channel) reference-identity sub-escalation; aggregate-escalations report at .codeconv/conversion-idioms/_escalations-report.md; docs/current_plan.md retired
- resume protocol for paused live-pass — 108/128 done, 20 remain, 4 escalations awaiting Gabi's ruling, exact next-session steps + 20-file frontier
- snapshot(018-live-pass): 108/128 specced+scaffolded — 104 specced + 4 escalated; 20 analysed remaining (paused at Anthropic rate-limit ~4:20am Europe/London); escalations on compiler/error.dart (exception-naming policy), compiler/glp_printer.dart (latent _isAtom Dart-source bug), runtime/heap_fcp.dart (threading model — inherited by runner/body_kernels/scheduler/system_predicates_impl/mad_context), compiler/analyzer.dart (duplicate UnifyResult/PartialEvaluator vs partial_evaluator.dart); protective snapshot of agent-produced conversion-spec artifacts + tombstone stamps so the genuine-pass state survives the pause
- remove stale 017 current_plan.md (017 merged to main); track 018 codex review artifact
- Polish complete — T050 full re-baseline (324/325 green; 1 isolated transient bridge-cold-init flake, passes 1/1 isolated, ZERO 018 regressions vs T001), T051 quickstart smokes covered by green e2e, T052 scope-clean, T053 memory. Feature 018 IMPLEMENTATION COMPLETE
- T005/T006/T017/T018 Phase-2 tests — single-head+linear (offline green), schema isolation, tombstone stamp/rebuild fixed-point + capability/signature preservation (29 bridge-free PASS; T005/T006 bridge-verify in flight)
- phase-1 checkpoint — T001 baseline recorded (no-bridge guard 62p/1s/0f; bridge per-test green, full-suite contention pre-existing), T002 dbos 2.21.0 verified
- cleanup stale exFAT premise — D: verified NTFS (Gabi-approved); CLAUDE.md data-dir now convention not necessity, known-issues Issue 8 marked premise-void
- spec(018): codeconv-builder spec/plan/tasks checkpoint (58 tasks, E1-E5 remedies); point .specify+CLAUDE at 018
- release(v2026.05.17): integrate codeconv 015/016/017 into main; PG17 cluster rebuild
- Merge 017-conversion-plan-agents into main (codeconv-planagents: orchestrated per-tombstone Dart->C#/.NET plan generation)
- Merge 015-codeconv-depgraph into main (depgraph + readiness oracle, option-A', .dbsnapshots ignore, specs/017 scaffolding)
- Merge 016-codeconv-init-scaffold-langpair into main (init/scaffold/mirror Dart->C# pipeline + tombstone refresh)
- commit-all per Gabi — refreshed .codeconv tombstones; pre-integration snapshot
- commit-all per Gabi — .gitignore (.dbsnapshots ignore), specs/017 spec edits, .agents/ AGENTS.md reviews/ docs/current_plan.md; pre-integration snapshot
- Merge main into 016 (reconcile: take 015 depgraph option-A'; drop 016 re-add; accept current_plan deletion)
- final verification — 72/72 green per-group serial; 2 combined-run flakes confirmed isolation-green (PGLite cold-init exhaustion, known flakiness class, not a defect); zero regressions
- final serial verification — 72/72 feature tests green; tick T002/T025-T044/T046; annotate T003/T004/T005/T045 BLOCKED (empty glp_runtime_net + LLM-loop forbidden, mirrors 015 parked live tasks)
- US5 full Dart→C# pipeline regression (T030-T031) — init→depgraph→scaffold cross-stage consistency e2e-verified, no 015 regression
- US3 registry extensibility (T024-T026) — 15 unit tests pass, SC-003 zero-stage-tool-edit proven
- fix aggregate-report back-link assertion to spec-correct <rel>.dart.md#e<n> (conversion_plan_artefact_format.md); SCC-batch reds were bridge-contention not logic
- add downgrade-then-upgrade idempotence test (T011 obligation); discover round-trip comment accuracy for feature-017 keys; tick T001/T006-T024
- Phase 1 setup T001-T004 — substrate+contract confirmed, schema snapshot
- US1 bridge tests green (13 passed) + US2-US5/Polish test suites + SCC fixture
- checkpoint WIP from stalled agent (langpairs registry + 0003 migration)
- schema isolation + FR-020 runtime write-surface (T011) — 6 green; migration 0003 verified on fresh PG17 cluster
- plan+tasks+analyze(017): Phase0/1 (plan/research/data-model/5 contracts/quickstart) + 46 tasks + analyze remedies C1 (FR-002 reword: thin-wrapper vs mandated orchestration loop) + C2 (T011 FR-020 runtime write-surface assertion); SPECKIT marker 015->017
- Merge 015-codeconv-depgraph into main
- tick T045 (full suite 116 passed/3 skipped; 4 flakes confirmed isolation-green, 0 regressions); remove completed current_plan
- tick T039-T044; known-issues Issue 9 (PG16/PG17 cluster split + quickstart staleness)
- Stamp tombstones with depgraph + conversion state (feature 015)
- analyze(016): apply 2 MEDIUM remedies — F1 FR-026/-027 numbering collision reworded (T035+plan); F2 spec FR-004 discover back-compat-default carve-out
- tasks(016): 37 tasks US1-US5 — setup/foundational(registry+dart_csharp+0003+pair-generic discover)/init/scaffold/registry/exclusions/pipeline/polish-removal
- plan(016): Phase 0/1 — plan/research/data-model/contracts/quickstart + SPECKIT marker repoint (constitution N/A, zero clarifications)
- tick tasks.md — Phases 2-6 + T041/T042 verified green (T039/T040/T043/T045 pending)
- Phase 7 polish — T041 depgraph FR-026/027 grep test + T042 .gitignore .codeconv/depgraph.json
- spec(016): codeconv init+scaffold behind pluggable language-pair registry (Dart->C#); D1-D6 encoded, checklist pass, zero clarifications
- feat+fix(015): #16 from-tombstones preflight+Option-B single-txn+exit-code plumbing; #17 --verify-tombstones read-only audit (+tests, suite green 87)
- spec+fix(015): Amendment v2 (codex-reviewed) + Bug1 compute --json exit2 + target_path 6th tombstone key end-to-end round-trip
- Merge origin/main (pglite 0.4.5 fix) into 015-codeconv-depgraph
- spec(015): codeconv-depgraph spec/plan/tasks + Phase 1 baseline (128/443/6)
- Merge pull request #9 from olamni-glp/014-package-self-import-resolution
- Refresh tombstones after feature 014 self-package rewrite (SC-007)
- spec(014): plan, tasks, analyze + top-3 remediations applied (no code yet)
- Merge pull request #8 from olamni-glp/followup/014-package-import-docs
- file 014 follow-up — codeconv-discover should resolve package:glp_runtime/... as in-subtree edges. Includes prepared /speckit-specify prompt.
- Merge pull request #7 from olamni-glp/013-data-dir-override
- Merge pull request #6 from olamni-glp/012-codeconv-runner
- Phase 7 done — feature ready to ship; update next-session pointers.
- update current_plan.md — Phase 6 done; Phase 7 next. Document the four DBOS-on-PGLite hooks.
- update current_plan.md mid-flight — phases 1-4 done, resume at phase 5
- spec(012): /speckit-plan + tasks + analyze + remediations — implement-ready
- spec(012): /speckit-clarify — 5 clarifications (perf SLO, edge uniqueness, gitignore policy, orphan revival, bridge log)
- spec(012): /speckit-specify — codeconv-runner with unified .pgdb / cross-process lock / D2NET migration / dart inventory + tombstones
- Merge pull request #5 from olamni-glp/011-prereq-patterns-catalog
- gitignore .claude/settings.local.json (per-machine permission accumulation)
- record speckit permissions accumulated during 011; gitignore .D2NET workspace dir
- spec(011): /speckit-plan + /speckit-tasks + /speckit-analyze; 4 MEDIUM remeds applied; ready for /speckit-implement
- validate(010): T012 PASS + T029 PASS* + T022 PARTIAL; document gated walks
- release(010): v2026.05.02 — validation walkthrough recorded; T013 misstatement fixed
- allow d2net-scaffold smoke-walk commands and check-prerequisites
- validate(010): in-session smoke walks 1/2/3/4/5/8 + T013; correct T013 misstatement (recap conflated total writes with net deltas)
- spec(010): /D2NET-scaffold skill wrapper — spec + plan + research + data-model + contracts + quickstart + tasks (5 clarifications, 4 remediations applied)
- D2NET.Scaffold: source-tree mirror with per-dart workdirs (009)
- spec(009): plan + research + data-model + contracts + quickstart + tasks + handoff
- spec(009): D2NET.Scaffold source-tree mirror with per-dart working dirs (post-clarify)
- D2NET.Init: --remove-exclude with --allow-system-exclusions safety override
- D2NET.Init: --add-exclude for non-destructive incremental exclusions
- Make --non-interactive init-only; binary rejects it on inspection commands
- Consolidate and deduplicate CLAUDE.md, retarget to glpnet (Gabi)
- Add /D2NET-init Claude Code skill wrapping the d2net-init CLI
- Add main_AofGLP.pdf reference document to repo root
- D2NET.Init: swap storage from SQLite to PGLite WASM via direct bridge
- Merge pull request #4 from olamni-glp/004-changelog-checkpoint
- Add CHANGELOG.md summarising v2026.04.30 / -2 / -3
- Merge pull request #3 from olamni-glp/003-pglite-bridge-rca
- Root-cause analysis: PGLite + pg-gateway + ODBC stack failures
- Merge pull request #2 from olamni-glp/002-d2net-init
- Add D2NET.Init companion command + 002-d2net-init spec/plan
- Merge pull request #1 from olamni-glp/001-d2net-scaffold
- Add d2net-scaffold MVP toolkit + speckit workflow + CalVer branching

## [v2026.07.13.1] - 2026-07-13

### Added
- E3pcCtrl frame kind - CBOR codec, must-understand section, reliability conformance (T034)
- quic_chat.glp — genuine full-duplex single-link chat
- ratify quic_chat reconciliation - full-duplex chat canonical, one-bind preserved as quic_chat_onebind.glp
- quic_chat.glp - two-way chat over per-message QUIC one-binds (loopback-verified)
- US4 mesh program + US5 graceful teardown + polish - quic_mesh.glp (crdtmsg/7 over quic, all links as GLP goals, SRSW-clean load), QuicTeardownTests T037-T039, known-issues + FR-019 audit (T032-T042,T045); REPL 526/527 (mesh loads, 1 pre-existing AOT baseline)
- US3 - macaroon gate (verify-before-act) on the quic link; ICapabilityGate seam + MacaroonLinkGate + slot-as-section-0x20 (D-2 resolved, no 041 codec change); T022-T028 green (129/129 xUnit, REPL 525/526 baseline)
- US2 - crdtmsg envelopes on the "quic" wire (CrdtMsgPayloadCodec + composition-root inject); T013-T021 green
- US1 MVP - register genuine QUIC transport into REPL LinkRuntime (fail-closed cert loader, one-bind kernel-path tests, GLP program, REPL regression)

### Fixed
- bounded full-duplex quic_chat — close after collect, no teardown race
- preserve trailing mode annotation on compound type alternatives (codexreview P2)
- quic payload codec understands the E3pcCtrl section (0x15) - carries E3PC frames instead of loud-rejecting (T034 follow-up)
- path-B listen failure fails closed gracefully — establishment class complete (codexreview P2)
- path-B request connect failure fails closed gracefully (codexreview P2)
- correct quic_mesh.glp GLP mode/SRSW so it loads clean (codexreview P1)
- defer QUIC link close until inbound collection completes (codexreview P1)
- any transport-establishment failure fails closed gracefully (codexreview P2)
- capability-gate evaluation failure fails closed gracefully (codexreview P2)
- verify-before-act on path-B + controlled codec-failure on egress (codexreview P1/P2)
- defer QUIC trust-material load to first use (codexreview P1)
- per-role rendezvous timeouts in LinkSetupKernel (listener 180s, connector 120s) for cross-host soak
- widen pump fault-guard to the frame parse/reassembly/ordering layer (not just decode) so a malformed/adversarial frame surfaces an observable fault instead of silently killing the receive task; lock _recvLoops with _faultSubs
- codexreview fixes — genuine macaroon expiry (</<=  operators + real elapsed-token test), pump surfaces malformed-payload fault instead of silent link death, codec rejects term-visible cap slot 0x20, thread-safe ProvenanceLog, LinkPump.Dispose detaches OnFault + joins loops, quic_one_bind ships crdtmsg/7 (production wiring)

### Changed
- Merge pull request #105 from olamni-glp/050-glp-native-quic-link
- Merge remote-tracking branch 'origin/050-glp-native-quic-link' into 050-glp-native-quic-link
- mark T043 done — two-host acceptance performed 7x (engineer-confirmed)
- gitignore guardian backups; point active feature at 050-glp-native-quic-link for ship
- T018 complete - tick type-checker port task (native gleam test 331/331)
- impl(050): T018 type_checker checkModule - type_checker.gleam + tests port; covariance+contravariance, checkModule wiring (closes T018, native gleam test 331/331)
- impl(050): T018 clause_validation - clause_validation.gleam + tests port; anonymous-reader rejection as Result (native gleam test 322/322)
- impl(050): T018 type_environment_builder - builder.gleam + tests port; alias resolution, determinism, errors-as-Result, prelude-source param (native gleam test 313/313)
- quic_chat round lines - T018 split coordination with Olamnit
- impl(050): T018 well_typed_clause - well_typed_clause.gleam + tests port; counter-threaded, Case-B inference, clause duality (native gleam test 303/303)
- impl(050): T018 well_typed_term - well_typed_term.gleam + tests port; fix program_dfa bare const-label (Dart parity) (native gleam test 288/288)
- impl(050): T018 moded_head - moded_head.gleam + tests port (native gleam test 275/275)
- impl(050): T018 moded_term - moded_term.gleam + tests port (native gleam test 268/268)
- T018 restart note for moded_term/moded_head (Olamnit native-gleam, position @ subtyping done 252/252)
- impl(050): T018 subtyping - subtyping.gleam + tests port (native gleam test 252/252)
- Merge origin/050-full-gleam-combined (T018 chunk C from Olamnit) into local
- impl(050): T018 chunk C - program_dfa.gleam + tests port (native gleam test 246/246; WSL absent on Olamnit, Gabi-approved native run)
- Merge origin/050-glp-native-quic-link into 050-full-gleam-combined
- commit outstanding tree state (manifest churn, EOL normalization, T027 lake manifest)
- T018 handover note for Olamnit workstation
- gitignore reviews/ (codexreview advisory artifacts, never shipped)
- mark T044 done; gitignore glpquick-cert (private keys) + gleam_quic test build artifacts
- install buildkit skills 2026.07.10.1 (rebrand author->buildkit, +bk-3rtask/bk-guards/bk-owo; trim BUILDKIT block)
- US4 T029-T031 - multi-accept mesh isolation, dup-suppression/exactly-once/fault-report, rogue-pin reject + tampered-seal detect over real QUIC (134/134 green)
- impl(050): T018 chunk B - param_expansion.gleam port + TypeExpr toString in type_ast (WSL gleam test 227/227)
- impl(050): T018 chunk A - type-checker foundations (mode.gleam, TypeEnvironment in type_ast, prelude sets; WSL gleam test 221/221)
- impl(050): T017 partial evaluator port to compiler/partial_eval.gleam (both live Dart PE copies; WSL gleam test 212/212; 5 error channels byte-identical to Dart REPL)
- impl(050): T015 parser tests + T016 SRSW checker port (WSL gleam test 184/184; SRSW messages byte-identical to Dart REPL)
- impl(050): T027 Lean PI:14 writer-MGU proof (lake build green, sorry-free) + T028 prose PROOF.md (INDEX flip deferred to T026 discharge commit)
- impl(050): T014 parser hand-port to parser/parser.gleam + T013 CRLF lexer fix (WSL gleam test 120/120; corpus sweep 70/70 Dart-conformant)
- impl(050): T013 lexer hand-port to parser/lexer.gleam (WSL gleam test 119/119)
- impl(050): Phase 2 foundational complete - T006-T012 (v2.16 opcodes, program model, AST, engine types, generation-scoped wake, staged diagnostics; WSL gleam test 104/104)
- impl(050): Phase 1 complete - T001-T005 verified (lake build x2, WSL gleam test 91/91, cross-rig 16/16)
- impl(050): Phase 1 setup scaffolds + baseline record (WIP checkpoint)
- tasks(050): tasks stage complete - 68 dependency-ordered tasks
- plan(050): plan stage complete - research, data model, contracts, quickstart
- spec(050): clarify stage complete - 5 clarifications integrated
- spec(050): specify stage complete for combined Full-Gleam feature
- upgrade buildkit skills to 2026.07.09.1 (buildkit-deploy)
- Merge pull request #104 from olamni-glp/main
- mark US2a tasks done (T016/T019/T020 - IPayloadCodec seam + egress/ingress rewire)
- US2a - per-link IPayloadCodec seam; egress/ingress route through it; default codec preserves ground-relay byte-for-byte (118/118 green)
- plan+tasks+analyze - quic link integration pipeline (register QuicTransport, crdtmsg payload seam, macaroon gate, GLP mesh test); analyze remediations U1/A1/A2/I1/C1 applied

## [v2026.07.10.1] - 2026-07-10

### Changed
- import olamnit 20260708 export and re-export merged journal
- Merge pull request #102 from olamni-glp/main

## [v2026.07.09.2] - 2026-07-09

### Added
- T011 form (b) system guard primitive satisfiable/2 - native clause-spec table in runner, builtin+analyzer registration, GLP_POLICY_GUARD_FORM toggle for SC-009; A29 form-a reference + A30 pure-b probe wired
- T009+T010 form (a) via ruled a1 runtime-defined guards - PE pass-through, codegen definedGuards side table, runner three-valued evaluator; wx1-wx4 + 12/12 vectors green; suite A29 wired
- US5 bounded remote test-control over the link - control agent+driver, fixed whitelist (no remote shell), loopback-proven, 184 pytest green (FR-017..019)
- US1 vectors.json SSOT + C# parity tests 124/124 + guard GLP sources + a1 runtime-defined-guard design (T005-T008)
- US2 Profile C - in-process BEAM QUIC client via quicer NIF, demo PASS equal to Profile A baseline (milestone: Profile C verdict)

### Fixed
- codexreview non-blocking fixes — control-agent typed-error hardening (US5 FR-018), quicer ALPN code-6 token parity + WS total-reassembly cap (US2), defined-guard StackOverflow backstop (US1)
- gavri evidence - correct msquic version for the 0.2.15 build (2.3.8, not 2.5.7)
- gavri evidence CORRECTION - MSVC 14.50 (VS Community 2026 Insiders) IS installed; earlier MSVC-less claim was false

### Changed
- Merge pull request #100 from olamni-glp/049-wave1-guard-link-acceptance
- Merge pull request #99 from olamni-glp/main
- Merge pull request #97 from olamni-glp/049a-gavri-us2-us3
- T012+T013+T029-T031 - SC-009 equivalence both forms, parity+gate audit PASS, final baselines green, ship-gate audit ALL FOUR US PASS
- T020/T021 US3 two-host records + us3-verdict PASS; T010 form-a EquivalenceRun evidence
- mark T003 realization-checkpoint gate discharged in tasks.md
- ship-plan handoff to primary session - full-wave ship runs there on the canonical branch, gated on all 4 US
- Merge pull request #96 from olamni-glp/049a-gavri-us2-us3
- Merge remote-tracking branch 'origin/049-wave1-guard-link-acceptance' into 049a-gavri-us2-us3
- gavri US2+US3 evidence complete - SC-005 + SC-006 both PASS, 90-summary completion signal, transport-soak footnoted as out-of-scope
- gavri MSVC-native quicer attempt - toolchain proven (quictls+msquic.dll link), blocker is upstream unix-only quicer C source (0.2.15 and 0.4.3), escalated per FR-010
- T003 vector addendum - v05 success-on-empty (parity), v12 fail (decidable) per Gabi ruling; 1.14 gate fully discharged
- T003 realization addendum - form (a) via (a1) compiler extension then form (b), per Gabi ruling; v05/v12 outcomes still to be ruled
- us2-verdict follow-up - gavri MSVC correction 8facff21 relayed, PASS unaffected (WSL path stands)
- US2 Profile C verdict PASS SC-005 - gavri evidence reviewed and integrated (T015-T018)
- FR-015 regression coverage - #5 timeout-FAIL + #6 pre-readiness flood pytest, #7 >1MiB reassembly erlang harness PASS local OTP29 (T025-T028)
- US4 marathon durability VERIFIED - kill-resume PASS + durable-first commit re-drive exactly-once PASS (T022-T024, SC-007)
- step-3 checkpoint durable-first, commit withheld by index.lock (T024)
- us4-step-2 checkpoint - marathon durability probe
- marathon durability run record mrun-9724364d684a (T022)
- US3 Olamnit prep - cert generated, addr corrected 192.168.0.143 -> 192.168.0.136, firewall+server handed to engineer (T019)
- baseline checkpoint 524/525 REPL + 114 xUnit + 178 pytest, evidence tree, gavri prompt mesh>=4 fix, delegation record (T001/T002/T004/T014)
- analyze remediations - empty-targets and excluded-vs-unbound edge recorded for T003 ruling, gavri-lane execution semantics, T009 protocol wording (buildkit spec-049)
- tasks - 32 tasks in 4 story lanes, T003 1.14 realization gate, gavri delegation lane, marathon+fixes parallel (buildkit spec-049)
- plan - R1 form-(a) realization checkpoint, shared decision vectors, gavri delegation + evidence contracts (buildkit spec-049)
- gavri evidence - two-host prep done, awaiting cert + Olamnit server (milestone: US3 prep)
- gavri evidence - WSL provisioning + gleam Profile A baseline PASS (milestone: baseline)
- gavri evidence - environment discovery (US2/US3 delegation, milestone: environment done)

## [v2026.07.09.1] - 2026-07-09

### Added
- box partitioning + PGlite op-WAL + real MsQuic link adapter (buildkit spec-048 T011-T013)

### Changed
- Merge pull request #95 from olamni-glp/049-wave1-guard-link-acceptance
- roadmap-sync import manifest - applied 6 olamnit exports (1031 journal lines) on gavriellas host
- clarify - 1.14 approved staged a-to-b, hard ship gate, gavri delegation prompt (buildkit spec-049)
- specify wave-1 consolidated - GLP policy-guard (1.14-gated) + 036 link full acceptance (buildkit spec-049)
- Merge pull request #94 from olamni-glp/048-colab-foundations
- scan-reconcile 042/043 released, dedup A-I closed with engineer approval, CRDT migration 0022, double export-import idempotent (0 dup groups, 86 features)
- codify bounded-behavior cap+error+test rule (act-62c7bf6a99)
- spec(043): additive-optional carve-out - optional additions stay forward-compatible per engineer decision, closing review escalation
- post-ship close-out retrospective for v2026.07.08.1
- Merge pull request #93 from olamni-glp/main

## [v2026.07.08.1] - 2026-07-08

### Added
- polish T033-T036 - SC-001 re-expression acceptance, determinism sweep fixes + repeated-run test, quickstart API accuracy, full re-test green (218/218 + substrate 6/6+86/86, zero substrate diff)
- US4 T028-T032 - CompatChecker rule table with NFA pattern inclusion, transitive chain check, refusal law, override registration (212/212)
- US3 T023-T027 - CDDL-subset parser, Lifter with per-construct fidelity + hash drift detection, DSL printer, round-trip equivalence (188/188)
- US2 T019-T022 - InstanceValidator (kind-structure-facets order, closed-world), crdt_message 043 re-expression, SC-003 corpus agreement harness (175/175)
- US1 MVP T012-T018 - canonical CDDL emitter, lowering+allocation, all-or-nothing registration, SC-002 defect suite, SC-006 walkthrough (142/142, substrate 6/6+86/86)
- T008-T011 DSL parser, schema validator (6 rule groups, all-errors-one-pass), compat records, seeded overlay registry skeleton (94/94 green)
- T004-T007 AST records, verdict/error records, restricted-regex NFA engine tests-first (62/62 green)

### Changed
- Merge pull request #91 from olamni-glp/043-xsd-schema-language
- refine(codexreview): cycle 5/6 [diff/general]
- refine(codexreview): cycle 4/5 [diff/general]
- refine(codexreview): cycle 3/4 [diff/general]
- refine(codexreview): cycle 2/3 [diff/general]
- Checkpoint: 043 project skeleton, substrate baseline green (wire_registry 6/6, crdtmsg 86/86)
- analyze(043-xsd-schema-language): apply remediations I1 I2 A1 A2 U1 T1 W1 - closed-world compat rows per spec US4-AS2, drop symbol primitive, scope FR-007 agreement law, pin QmeditDsl=XsdSource for 043 entries
- tasks(043-xsd-schema-language): 36 tasks, tests-first per story - setup 3, foundational 8, US1 7 MVP, US2 4, US3 5, US4 5, polish 4
- plan(043-xsd-schema-language): plan + research R1-R12 + data-model + 5 contracts + quickstart - new csharp/glp_schema_lang over seeded overlay, E9 tables untouched
- clarify(043-xsd-schema-language): 3 clarifications - plaintext qmedit-family DSL (no XML), cycles rejected at schema-validation, evolution refuses without declared compatibility mode
- specify(043-xsd-schema-language): spec + quality checklist - XSD-style schema layer over E9 dual-DSL functor registry, 4 stories, 14 FRs, 6 SCs, zero markers (2 open choices routed to clarify)
- commit crdtmsg-verify-harden retrospective artifact from close-out
- Merge pull request #90 from olamni-glp/main

## [v2026.07.06.1] - 2026-07-06

### Changed
- Merge pull request #88 from olamni-glp/042-crdtmsg-verify-harden
- refine(codexreview): cycle 4/3 [diff/general]
- refine(codexreview): cycle 2/3 [diff/general]
- commit crdtmsg-mvp draft retrospective artifact from close-out
- impl(042): COMPLETE - report assembled s9/s11/s12 all nine SC PASS, SC-008 zero silent edits (61 rows), SC-009 refs in all 3 docs, T030 env-blocked bracket reproduced (T029-T031)
- impl(042): US4 complete - 231/231 pointers resolved SC-007 met (225 resolve, 2 host-blocked, 2 link-rot corrected in F2, 2 transcript unrecoverable), Tier1 39/39 bib-verified, report s8 (T026-T028)
- impl(042): T026 partial - 83/231 evidence pointers resolved (in-repo+sibling+transcript classes), transcript pointers superseded in F3 (rows 45-46); 148 F2 URLs pending web sweep
- impl(042): US3 complete - register closure SC-003 met (2 promoted incl BB-CRDT-7 self-promotion, 6 re-affirmed, 0 escalations), report s7+s10, F3 change-log rows 35-44 (T021-T024)
- impl(042): US2 complete - T016 merge rederivation 37/37 COHERENT, T017 26 blind rescans over 13 singletons, T018 curation SC-002 met (11 confirmed, 2 no-further-evidence, 0 escalations)
- impl(042): US1 conformance ledgers complete (SC-001 3/3, 18 elements) + US2 ledger rederivations corrected + drift dispositions + E1-E9 propagation fixes + pointer census 231 rows (T007-T015,T019,T020,T025)
- impl(042): Phase 2 foundational - report section 1 method reconstruction, 18 elements F1/F2/F3 RECORDED-vs-RECONSTRUCTED (T004-T006)
- impl(042): Phase 1 setup - report+evidence skeletons, changelog sections, scanner-C view resolved d2689a71, env-blocked baseline recorded (T001-T003)
- analyze(042-crdtmsg-verify-harden): 0 critical/high - applied 5 remediations (changelog skeletons in T001, tasks.md authoritative ordering, SC-008 min(10) sampling, T021 wording, 4-vs-3 ledger note)
- tasks(042-crdtmsg-verify-harden): 31 tasks across 4 stories - US1 conformance MVP, US2 hardening w/ blind re-scans, US3 register closure, US4 evidence census; single-writer-per-doc rule
- plan(042-crdtmsg-verify-harden): verification plan - 5 WPs, hybrid baselines pinned (c20317ce/6ecc975f/v2026.07.04.4), method-strength survey, report+ledger+changelog contracts
- clarify(042-crdtmsg-verify-harden): 3 rulings encoded - targeted re-execution (FR-014), mechanical PROV promotions w/ batch review (FR-008), hybrid delivery-time/HEAD baseline (FR-005/FR-015)
- spec(042-crdtmsg-verify-harden): verification+hardening spec for F1/F2/F3 against their frozen 3-role methods; 1 clarify fork (FR-014 evidence depth)
- Merge pull request #87 from olamni-glp/main

## [v2026.07.04.4] - 2026-07-04

### Added
- implement (041) Polish - dual-DSL schema registry, GLP guard PROPOSAL (propose-only, §1.14 gate), parity vectors, docs; C# gates 253 green (T053-T057)
- implement (041) US5 routing+e2e - unified header, v2 additive cap slot, @name loud-fail, dedup, policy matcher, mesh demonstrator; SC-007/008/009 green (T043-T052)
- implement (041) US4 cap/sig - macaroon fail-closed + amulet slot + Ed25519 whole/sub-content seals + provenance; SC-005/006/011 green (T035-T042)
- implement (041) US3 MANDATORY rich-text - Fugue no-interleaving + Peritext unknown-mark preservation, op semantics/tombstone/delivery (T026-T034)
- implement (041) US2 store-first - op-WAL (040 shape) + rebuildable projection + Merkle anti-entropy; convergence+crash-rebuild green (T020-T025)
- implement (041) US1 MVP - TLV+4 surface codecs, loud-fail, version tolerance; 16-cell conformance matrix green (T010-T019)
- implement (041) foundational - wire registry (SC-010), abstract model, DVV/hash-chain, transport seam; T001-T009,T012 green

### Fixed
- apply code-review findings - LEB128 overlong/overflow loud-fail, seal count-binding, section type_number CrdtMsgException, injective caveat encoding (+NUL cleanup); 86 tests green

### Changed
- Merge pull request #85 from olamni-glp/041-crdtmsg-mvp
- analyze (041) - apply top remedies (FR-019/031/023 coverage, FR-038 relabel)
- tasks (041) - 57 tasks by user story, store-first, tests-first
- plan (041) - design artifacts, C# workspace, store-first, constitution PASS
- clarify (041) - rich-text CRDT mandatory, C# primary, guard gated on 1.14
- specify crdtmsg-mvp (041) - CRDT multi-format messaging MVP spec
- dedup 55 dup-GUID groups from cross-host merge (0 dups; 78 feats/18 epics) + restart pointer
- restart pointer for 2026-07-04 — ship v2026.07.04.1 done, roadmap synced; NEXT=dedup dup-GUIDs, bk-upgrade+deploy, marathon
- roadmap-sync import+export (olamnit↔gavriellas cross-host merge, idempotent)
- bk-close retro for F3 cycle - 3 findings (agent-teams win, release-planner creatordate gotcha, retro-inputs gap)
- Merge pull request #83 from olamni-glp/main

## [v2026.07.04.3] - 2026-07-04

### Added
- E1-E9 rulings encoded in buildingblocks-synthesis section 6 + new feature crdtmsg-xsd-style-schema-language + export 20260704T072850Z

### Fixed
- CHANGELOG ordering - stray v2026.06.03.1 block moved to chronological slot, Unreleased restored to top
- E1 store side confirmed delta-CRDT+Merkle (option b both layers) - doc + mvp notes + export

### Changed
- roadmap export 20260704 pre-release
- promote crdtmsg-mvp + export 20260704
- roadmap - virtual-3270-term released (superseded via 040) + export 20260704
- roadmap export 20260704T070059Z post-ship (13 epics, 75 features, 1003 journal lines)
- bk-close retrospective - 4 findings (2 systematic: ship-state visibility, review-ledger) + CLAUDE.md update
- Merge pull request #81 from olamni-glp/main

## [v2026.07.04.2] - 2026-07-04

### Added
- F3 buildingblocks-synthesis delivered - 86 claims to 40 blocks, 9 escalations + roadmap 040 shipped/F3 released + export

### Changed
- Merge pull request #79 from olamni-glp/037-virtual-3270-term
- Merge pull request #78 from olamni-glp/037-virtual-3270-term
- roadmap fold - crdtmsg-verify-and-harden feature + 3-role dogfood win note + codify notes + exports 20260704
- Merge pull request #77 from olamni-glp/037-virtual-3270-term
- roadmap capture fix - crdtmsg F1/F2 released with doc pointers + export 20260704T063315Z
- Merge pull request #76 from olamni-glp/main

## [v2026.07.04.1] - 2026-07-04

### Added
- US9 Polish — SC coverage-map (SC-013) + help-completeness proxy + link_console parity + quickstart/help sync (T057-T060)
- US6 rcopy wizard — pure exclusion filter + run_transfer core + LinkProxy/ResponderSession over-the-link + /rcopy tui (T048-T053)
- US8 rcopy responder backend — file-WAL source-of-truth + rebuildable catalog/provenance, commit-on-complete, perm/quota/path (T039-T047)
- US7 user-bindable PF keys — BindingRegistry (free-key/PF13-24/Ctrl-alt/typed-equiv) + /bind live legend (T054-T056)
- US5 REPL-in-a-page — ReplBridge process bridge + /repl over link + agent-page /return (T034-T038)
- US4 joint pinpoint + masks/forms — joint.py/forms.py + /joint /pin /undo-pin /mask /fill wiring (T028-T033)
- US3 presentation — themes/OIA/splash to presentation.py, two-strip layout + /layout, reverse-video PF-legend (T024-T027)
- US2 pages — /transmit owned-block, received page not merged/no focus-steal, /pages owner-by-name (T019-T023)
- US1 MVP — type-only conversation hardened (state-backed tui, @name resolve, no-TTY gate, link-drop surface, one codec) (T009-T018)
- US-MVP Phase 2 foundational — tmsg codec + terminal state + @name resolve (T004-T008)
- US-MVP Phase 1 setup — terminal/rcopy skeletons, FakeHandle, two-tier tests (T001-T003)

### Fixed
- codexreview P1s — commit-time quota re-check (FR-038) + rcopy reply spoof-guard
- drain host stdout at spawn to prevent pre-readiness pipe-fill hang (code-review #6)
- demo records SC-001 FAIL on handshake timeout instead of AttributeError (code-review #5)
- Gleam relay reassembles >1MiB lines instead of misrouting fragments to stderr (data-loss guard); erlc-verified via WSL
- mesh dup-id no longer hijacks or evicts the incumbent link (routing/data-loss guard); regression test
- @name routing (FR-006), --tui TTY fallback (FR-005), report link-drops; shared parse_addressed + 5 tests

### Changed
- Merge branch 'develop' of https://github.com/olamni-glp/GLPNET into develop
- Merge pull request #74 from olamni-glp/037-virtual-3270-term
- roadmap-sync export 20260703T213044Z for cross-host resume (13 epics, 74 features)
- Merge pull request #73 from olamni-glp/037-virtual-3270-term
- sweep session artifacts - deploy/guardian/roadmap-sync state, 038 retro mirror, crdt-multiformat-messaging research
- Merge origin/develop into develop (integrate 036/037/040 work + PR #72)
- plan+tasks+analyze for complete+hardened virtual-3270 terminal (Phase 0/1 artifacts + 60 tasks + top remedies)
- Merge pull request #72 from olamni-glp/037-virtual-3270-term
- specify+clarify complete+hardened virtual-3270 terminal (superset of 037: US1-9 / FR-001..046 / SC-001..013; 3 clarifications)
- record buildkit v2026.07.03.1 deploy audit log
- refresh restart pointer — 035+ audit outcome, link-completion fixes done, next=T019 then promote+specify 040
- 035+ oblivion audit, 3-role-team method+dogfood, 040 complete-hardened-3270 capture, 2 codify notes
- restore virtual-3270-term spec on develop base; renumber /rcopy backend refs 038 to 040 (038/039 shipped)
- close-out retrospective report (4 root-cause findings) for v2026.07.02.3
- Merge pull request #71 from olamni-glp/main

## [v2026.07.02.3] - 2026-07-02

### Added
- RDP-robust command mode in 3270 TUI — transmit via '//'+Enter or Alt-Enter (no F-keys needed); slash-commands /help /theme /pages /new /next /prev /goto /focus /quit /send; F-keys still work where passed through
- 3270 TUI enhancements — 5 colour themes (F2/Ctrl-T), F1 help page, F10 page list w/ owners, startup screen art, configurable command lines (GLPQUICK_CMDLINES), Ctrl-key alternates for swallowed F-keys; record PF-key activation reqs
- prototype virtual IBM-3270 full-screen chat TUI (--tui) — block-mode compose (F9 transmit), green-screen transcript, pages (PF7/8/6), OIA status line; web-researched 3270 model
- prompt_toolkit REPL for interactive chat (input pinned at bottom, incoming renders cleanly above via patch_stdout); plain stdin/outbox path retained for background/file-driven use; GLPQUICK_QUIET send-only mode
- interactive --server/--client link console (real cross-process QUIC, both directions) + quickstart runbook (machine-name + gavri two-host steps); 18 pytest green
- US3 Gleam Profile A — Gleam/BEAM channel-link + C# genuine-QUIC side-process (real_quic side_process); gleam StackAdapter; demo --stack gleam SC-001..006 PASS; 18 pytest green (T030-T034). Profile C honestly build-blocked (no MSVC)
- US2 multi-accept mesh server — QuicListenerHandle (N isolated links/port) + Mesh router (to/broadcast, over_capacity, isolation); demo --clients 4 PASS SC-001..005+mesh; 14 pytest + 104 xUnit green (T023-T029)
- US1 demo + CLI wiring — genuine same-host conformance (SC-001/002/005 PASS, SC-003/004/006+two-host honestly NOT-RUN); 12 pytest + 104 xUnit green; tasks.md status (T014-T022, US2/US3 notes)
- US1 C# host exe + csharp StackAdapter — two-process genuine QUIC+WS GLP-message exchange, full-duplex + cert-mismatch reject; 11 pytest green (T018/T019/T020 message-level)
- US1 genuine QUIC+WS leaf — real System.Net.Quic handshake (IsSupported-gated, mutual SPKI pin, ALPN h3) + RFC6455 over QuicStream + minimal CONNECT bootstrap; 5 xUnit + 9 cert pytest green (T014/T015/T016/T017)
- US0 Setup+Foundational — glp_quick scaffold (cert/SPKI pin, GLP-msg envelope, CLI skeleton), /GLP-Quick skill, C# QUIC+WS leaf stubs (LinkScheme.Quic); IV-a gate PASS, real-QUIC probes PASS

### Fixed
- codexreview fixes #1/#2/#4 — bound WS frame size + surface FrameException as clean fault (FR-019); default gleam profile A; exit-code 6 -> quic_unsupported; +regression tests
- process-tree kill on stop (no orphaned QUIC hosts incl. gleam->erl->dotnet); REPL polish (incoming on its own line); restore _spawn method
- client stays alive for the link lifetime (not stdin) + disable QUIC idle timeout; link console survives EOF, auto-announces, file-outbox (GLPQUICK_OUTBOX), @to grammar

### Changed
- Merge pull request #69 from olamni-glp/036-http3-quic-ws-link
- T037 done — single-host quickstart validated (csharp SC-001..005+mesh, gleam Profile A SC-001..006 all PASS); record deferred acceptance as known-issues Issue 11
- Merge origin/develop into 036-http3-quic-ws-link (integrate 130 commits: bk-* skill rename, gleam-port 031-039, engine-split); resolve feature.json/CLAUDE.md/current_plan.md to 036 + preserve gleam-baseline T015 pointer
- carve deferred acceptance (T003/T032/T036/T040) into roadmap feature http3-quic-ws-link-full-acceptance + follow-up brief
- Merge pull request #68 from olamni-glp/main
- T038/T039 verified green — REPL 524/525 (1 unrelated AOT-smoke fail, no 036 regression), glp_quick 18 pytest + glp_link 104 xUnit
- fold RDP command-mode hard requirement + prototype learnings into virtual-3270-term reqs
- intake briefs for virtual-3270-term (full 3270 reqs), durable-mesh-messaging-protocol, and HTTP3-QUIC-WS (036 record + re-specify prompt + restart prep)
- commit gleam_quic dependency lockfile (manifest.toml)
- rework plan/tasks/analyze to 2026-06-28 clarifications (genuine WS-over-QUIC, cross-platform C#, two Gleam profiles)
- correct WS-over-QUIC framing (first-class, de-facto) + cross-platform .NET QUIC; encode 2026-06-28 clarifications
- research corpus (106 sources) + distillation; resolve RFC 9220 + AtomVM-QUIC feasibility
- plan + research + data-model + contracts + tasks; analyze remediations (constitution tokens, addressing/mid-drop coverage, scenario numbering)
- clarify GLP-over-link (REPL mesh), C#-first build order, concurrency, cert distribution
- specify HTTP/3 QUIC + WebSocket channel-link prototype

## [v2026.07.02.2] - 2026-07-02

### Changed
- Merge pull request #66 from olamni-glp/038-result-codec-and-framecodec-ride
- 8 codify notes from 2026-07-02 roadmap history reconciliation (reconcile bug, post-ship stall, backfill gap, number collisions, scan-method win)
- Merge pull request #65 from olamni-glp/main

## [v2026.07.02.1] - 2026-07-02

### Added
- T042 (optional) Lean decode∘encode=id proof for term sub-codec — mirrors verified 029 IlCodecRoundTrip (flat ground-term model, no mathlib/sorry); authored, machine-verification pending Lean toolchain (auto-install sandbox-blocked)
- T039/T040 GATED corpus RUN on real AtomVM 0.7.999 via Node/WASM wrapper — real Gleam codec, float 0x03 + int64 edges byte-identical + round-trip (PASS); T043 #36 handoff note (verified FrameCodec offsets)
- T031 cross-runtime golden byte-parity harness + quickstart wiring — Dart==C#==Gleam==corpus.hex; harness PASS on dev box
- T032 V5 oracle cross-check — result-codec term bytes byte-identical to 029 ConstantCodec (int64/double/string/struct-header); models diverge at 0x05 wrapper by design; C# 131
- T038 loud-fail fuzz (0 silent accepts) + T041 cyclic-term depth-bounded no-loop — all 3 runtimes; D5/FORK-1 policy left OPEN (test only)
- US3 T033-T037 — deref+var->writer fidelity (all 3 runtimes): exact depth-32/33 boundary + $truncated marker, var->writer identity, canonical-order determinism; deref-corpus.md reference; Dart/C#-builder/Gleam green
- US1 T025 — suspended-status acceptance (all 3 runtimes): Status=suspended + blocking-reader set + no heap-addr leak; Dart+2/C#113/Gleam79 green
- US2 T027/T028 — C#+Gleam golden byte-identity + cross-decode against pinned corpus.hex (encode(corpus)==golden, decode(golden)==corpus, all 13 non-gated); C# 111, Gleam 77 green
- Gleam result-envelope builder (T022/T023) — new result_envelope_builder.gleam; heap-threaded deep-resolve (depth-32 + $truncated) over 034 heap.deref, build from query writers, round-trips shipped codec; 74 gleam tests green
- C# result-envelope builder (T020/T021) — new glp_result_codec_builder project w/ IHeapView seam (owner A+B); deep-resolve depth-32 + $truncated, build from queryVarWriters/DrainResult, round-trips shipped codec; 7/7 tests green

### Fixed
- codexreview cycle-2 — golden harness rejects zero-match C# filter (dotnet test --filter exits 0 on no matches; a renamed class would false-pass); guard on non-zero Passed count
- codexreview cycle-1 — AtomVM gate hard-fails on gleam build error + missing beam (was unchecked, could false-pass on stale beams); output-content stays the success signal (AtomVM exits 1 benignly on success)

### Changed
- Merge pull request #63 from olamni-glp/038-result-codec-and-framecodec-ride
- Merge remote-tracking branch 'origin/develop' into 038-result-codec-and-framecodec-ride
- T044 doc audit + T045 end-to-end validation — Dart 83/C# 131/builder 14/Gleam 91 + golden harness PASS + AtomVM gated PASS; all 44 tasks done (+T042 optional authored)
- 038(impl): US2 golden corpus authored from Dart + Dart byte-identity test (T004/T026/T029/T030); 69 Dart codec tests green
- 038(impl): C# + Gleam result-codec fan-out — byte-identical to Dart source of truth (T002/3/5/6/8/9/11/13/24); C# 84/84, Gleam 68/68 green
- 038(impl): Dart engine->envelope builder + depth-32 deep-resolve (T017/T018/T019); MVP sub-checkpoint green (55 codec tests)
- 038(impl): Dart codec foundation — value types + term sub-codec + envelope frame codec; US1 round-trip/no-heap/in-process green (T001/T007/T010/T012/T014/T015/T016)
- 038(analyze): cross-artifact analysis — 0 critical/high, 100% coverage; applied U1 remedy (Gleam GlobalVarId agentId = explicit builder param, no Gleam engine yet)
- 038(tasks): 45 tasks across 6 phases by US1/US2/US3; MVP=US1 Dart envelope round-trip+no-heap; gated float/64bit/cyclic quarantined
- 038(plan): result-envelope codec plan — rides Section-15 term codec (029 conventions), buildable on 034 w/o F5; D4=A/ED-6=A encoded; float/64-bit-edge/cyclic-term gated
- Merge pull request #62 from olamni-glp/main
- 038 clarify: owner-ruled D4=A (freeze toward v2, author Section-15 in the freeze) and ED-6=A (authorize AtomVM float-decode spike); NEEDS CLARIFICATION resolved. clarify=complete; plan next.
- 038 specify: result-envelope codec spec (rides ED-6 Section-15 codec; framing/transport split to #36). 2 owner gates marked NEEDS CLARIFICATION: D4 ISA-freeze, ED-6 float-decode-on-AtomVM. Pipeline sidecar specify=complete; marathon run mrun-67d510b22e34.

## [v2026.06.30.1] - 2026-06-30

### Changed
- Merge pull request #60 from olamni-glp/039-m2-0-verify-erlang-monitor-atomvm
- 039(implement): VERDICT=works — erlang:monitor/2+DOWN faithful on AtomVM 0.6.6 (vs OTP-25); spawn_monitor/1 absent (use spawn+monitor); D10 fork not triggered
- 039(implement MVP): monitor_probe + OTP-25 reference (normal/boom/noproc); AtomVM 0.6.6 run blocked on host provisioning (not present in WSL)
- 039 tasks: T001-T007, MVP=T001-T003 (toolchain, probe, run+observe normal-exit DOWN).
- 039 plan: Erlang monitor probe built+run on AtomVM 0.6.6 via F1 WSL toolchain; 5 phases (toolchain confirm, MVP normal-exit DOWN, abnormal exit, edge+fallback, verdict).
- 039 m2-0 specify: gating spike to verify erlang:monitor + DOWN on AtomVM 0.6.6; gate-free (D10 fork only on negative result). sidecar specify=complete; marathon mrun-117a92c4eea7.
- Merge pull request #59 from olamni-glp/036-glp-gleam-baseline-program
- 036: program complete — P1/P5/spike research artifacts + spec/plan/contracts; T015 two-epic roadmap reconfiguration applied & marathon mrun-5611c436ba95 discharged (also sweeps 034/035 retros + BEACON-JOIN.md per commit-all)
- 036: restart pointer — T014 approved, T015 migration next in new session
- 036(T013): completeness-critic pass + folded gap fixes
- 036(T007): P8 two-epic reconfiguration synthesis
- 036(T012): P3 opportunities register (70; saturated)
- 036(T011): P2 concerns register (218 concerns; loop not yet saturated -> T013)
- 036(T010): P7 QHSM/YngeniOS integration dossier
- 036(T006): P1b corrected realignment dispositions
- 036(T009): P6 Gleam/AtomVM implementation-strategy dossier
- 036(T008): ANTLR-integration deep-dive dossier (FR-005 verified via spike)
- 036(T005): P4 proof artifact register (3 proved / 2 open)
- 036(T004): P4 faithfulness parity bar (M1+M2, primary-source-cited)
- 036(T003): pipeline status index for the glp-gleam-baseline research machinery
- 036(T002): proof-harness wiring for the glp-gleam-baseline research machinery
- 036(T001): corpus index for the glp-gleam-baseline research machinery
- Merge pull request #58 from olamni-glp/main

## [v2026.06.26.1] - 2026-06-26

### Changed
- Merge pull request #56 from olamni-glp/035-semantic-tombstone-enrichment
- 035(fix): --from-tombstones rebuild carries purpose_source/key_idea_source (FR-008) — was resetting inferred/doc to absent; pre-035 derives from blank-ness; +regression test
- 035(corpus): enrich glp_runtime_net tombstones via Claude seam — 68 inferred (9 compiler + 59), 104 doc, 7 stubs left blank; gitignore enrich-runs logs
- 035(enrich): mark T023 (consolidated feature gate 22/22 green) — all 24 tasks complete
- 035(enrich polish): T022 isolated quickstart e2e (dry-run + scoped enrich + FR-014 git-diff); T024 SC-004 grep guard verified
- 035(enrich US3): --path scope + per-file fault isolation + low-confidence + run summary/durable log; T018 green
- 035(enrich US2): discover provenance-aware seed + conditional inferred-preservation (FR-008); enrich idempotence/stale-guard; T013/T014 green
- 035(enrich US1/MVP): run_enrich candidate scan + Claude-seam infer/write + non-candidate stamping + no-API exit-2; T007/T008 green
- 035(enrich P1-2): tool skeleton + no-API seam + migration 0011 + frontmatter provenance keys + head tests
- 035(plan/tasks/analyze): semantic tombstone enrichment pipeline artifacts + analyze remediations (B1 len-caps, C1 file run-log, D1/E1/F1)
- Merge pull request #55 from olamni-glp/main

## [v2026.06.25.1] - 2026-06-25

### Changed
- Merge pull request #53 from olamni-glp/034-glp-gleam-core-terms-and-heap
- 034(F4): codexreview fixes — deref self-bind->Unbound (Dart parity), forward suspensions to terminal writer (FR-008), correct R-007/parity-evidence claims, +4 tests (54 green)
- 034: implement glp_gleam core terms+heap+unify (F4) — immutable threaded store, 50 tests green on BEAM
- 034: plan/tasks/analyze for glp_gleam core terms+heap+unify (F4) — immutable threaded store; 4 analyze remediations
- Merge pull request #52 from olamni-glp/main

## [v2026.06.24.2] - 2026-06-24

### Added
- polish — additive-only + quickstart walkthrough + artifact hygiene green (T023-T025)
- WSL smoke gate + config-only conversion recognition + README (US3, T019-T022)
- 8 subsystem placeholders 1:1 with glp_runtime/lib (US2, T009-T018)
- glp_gleam MVP — buildable+testable Gleam/BEAM subtree (US1, T001-T008)

### Fixed
- strip placeholder export markers -> doc-only (codexreview: T009-T016 'no exported definitions')

### Changed
- Merge pull request #50 from olamni-glp/033-glp-gleam-subtree-scaffold
- upgrade installed artifacts to v2026.06.24.3
- analyze(033): apply top remediations — clarify FR-007/SC-005 wired-in wording; strengthen T021 (FR-008 establish+verify) and T018 (FR-006 segment legality)
- tasks(033): 25 tasks for glp_gleam subtree scaffold (US1 MVP build+test, US2 8 placeholders, US3 smoke+recognition)
- plan(033): glp_gleam subtree scaffold — plan, research, data-model, contracts, quickstart
- Merge pull request #48 from olamni-glp/main

## [v2026.06.24.1] - 2026-06-24

### Added
- Dart->Gleam codeconv langpair (dart,gleam) + R3-b generic collision seam

### Changed
- Merge pull request #46 from olamni-glp/032-codeconv-gleam-langpair
- refine(codexreview): cycle 2/10 [diff/general]
- refine(codexreview): cycle 1/10 [diff/general]
- analyze(032): remediate F3 (add PairMismatch coverage to T008); F1/R-003 owner decision pending
- tasks(032): 20 tasks across 3 user stories; R-003 owner-decision gate flagged before implement
- plan(032): Dart->Gleam langpair plan + Phase0/1 artifacts; flag FR-005<->FR-008 collision tension (R-003)
- clarify Gleam target path policy (verbatim mirror, F3 owns layout)
- add codeconv-gleam-langpair (Dart-to-Gleam) feature spec + checklist
- Merge pull request #45 from olamni-glp/main

## [v2026.06.22.1] - 2026-06-22

### Changed
- Merge pull request #43 from olamni-glp/031-gleam-port-spike
- fix codexreview cycle-2 evidence findings (correct C# inventory counts, record JS-probe output)
- fix codexreview cycle-2 residual (stale gleam_otp mention in js-probe comment)
- fix codexreview cycle-1 findings (gleam_otp stale listing x2, JS actor citation, inventory JS-build, AtomVM packaging note)
- gitignore buildkit refine cache (.specify/.refine-cache/, regenerable)
- full Gleam smoke runs on AtomVM (raw erlang:spawn, no gleam_otp) + codex-review fixes
- Gleam port spike deliverables - dossier, toolchain inventory, hello-glp-term smoke
- spec(031): plan, tasks, analyze remediations for Gleam port spike
- Merge develop (bk-* aliases, pinned CLI) into 031-gleam-port-spike
- Merge pull request #42 from olamni-glp/chore-bk-aliases-pin-v2026.06.17.1
- pin CLI v2026.06.17.1, apply /bk-* aliases, register deploy
- spec(031): Gleam port source+toolchain / AtomVM feasibility spike
- Merge pull request #41 from olamni-glp/main

## [v2026.06.19.1] - 2026-06-19

### Fixed
- per-run marathon bridge resolves script from toolchain checkout, not the off-repo store (Fix A) - T057 e2e drive found the primary PGLite store never started via the real CLI; decouple repo_root(script source) from store_root(cluster) and commit-target repo_dir; junction-free fixture + regression test

### Changed
- Merge pull request #39 from olamni-glp/030-marathon-refinement
- T058 full-suite gate done + T057 addendum (Fix A supersedes the prereq-patterns junction workaround; 34/34 marathon on reconciled tree, real-CLI primary-store smoke green)
- T057 quickstart e2e validated + tasks.md T051-T057 DONE notes (Phase 8 complete except T058 full-suite gate, held for the Sunday 2026-06-14 ~01:00 intensive-regression window)
- T056 /marathon-stage-harness skill drives the refined CLI - canonical --run, data-driven register/append-stage/capture intake, keeper lifecycle + hygiene, rule-2a re-drive in Restart-Resume step 4, gate/rerun by stage NAME, full contracts/cli.md command table, preauth grants documented as library-level Repository.update_run
- T055 point marathon-stage-harness references at the refined model - CLAUDE.md + current_plan.md now describe the data-driven per-run isolated store (default C:/pglite/marathon/<run-id>, keeper, JSON mirror), canonical --run resume (--feature deprecated alias), 030 contracts pointer; 024 shared-cluster schema noted inert history (VIII)
- T054 Constitution V guard - zero OPENAI_API_KEY/litellm/openai tokens anywhere in the marathon package source; bridge-free 1/1
- T053 shared-cluster no-new-head guard - Alembic head stays exactly 0010, no versions/ file beyond 0010, only marathon migration is 024's inert 0010_marathon_schema, per-run store schema imports no Alembic machinery (VI-a, D2); bridge-free 4/4
- T052 resume-position byte-identity (SC-008) - pure derive_position over reconstructed+reshuffled rows (incl. rule-2a re-drive branch) and live three-way check (session env / fresh env / fresh CLI subprocess) all canonical-JSON byte-equal; 2/2
- T051 CLI parity guard - registered Typer surface == contracts/cli.md table, declared lib functions importable, callback wiring references its declared function, no function owns two subcommands (position->resume alias folded); bridge-free 4/4
- Merge pull request #38 from olamni-glp/main

## [v2026.06.12.1] - 2026-06-12

### Added
- Phase 7 US5 (T040-T050) - gate/orchestrate/trace/escalation ported onto stage+checkpoint rows, reconcile (in_sync/fast-forward/fork escalation, resume reconciles first), budget_exceeded kind, CLI gate/rerun/trace/reconcile; US5 6/6, full marathon set 26/26
- Phase 6 US4 (T033-T039) - scoped commit+push folded onto checkpoint rows (named paths only, hooks run, never force), push_blocked escalation, rule-2a re-drive guard + redrive_commit, status line grammar + emit_status at every boundary, CLI status/--emit + exit 2 on push_blocked; tests 4/4
- Phase 5 US3 keeper (T026-T032) - start/stop/recover over bridge_client, kernel-fd single-writer lock with ConcurrentWriter refusal distinct from stale residue, read-only doctor, keeper CLI; FIX latent bridge_client.request_force_shutdown marker path (inside data_dir -> sibling, matching bridge poll + 012 sibling convention); tests 2/2
- Phases 3+4 US1+US2 (T012-T025) - data-driven stages register/append/finalize, start_stage+checkpoint, pure derive_position resume, emergent intake with 5-stage mini-pipeline + fractional routing + prereq escalation, CLI register/append-stage/stage-start/checkpoint/resume/position/finalize/capture; tests 11/11
- Phase 2 Foundational (T005-T011) - per-run isolated store: resolve_env off-repo guard, idempotent 9-table schema, bridge-composed single-writer repository CRUD, JSON-mirror dual-write, monotonic sequencing; foundation tests 3/3
- Phase 1 scaffold (T001-T004) — verify greenfield precondition, rewrite models data-driven, new module stubs, drop obsolete 024 tests/modules
- plan + tasks + analyze marathon-refinement; resolve VI-b via constitution v1.1.0
- clarify marathon-refinement — resolve 4 forks (hybrid store, codeconv-module now+extract-later, 5-stage mini-pipeline→marathon implement, greenfield)
- specify marathon-refinement (spec + requirements checklist; 29 FRs, 5 user stories, 3 clarify forks)

### Changed
- Merge pull request #36 from olamni-glp/030-marathon-refinement
- Merge pull request #35 from olamni-glp/main

## [v2026.06.11.1] - 2026-06-11

### Added
- polish — pin Typed-Datalog-IR citation, KEEP decision + findings to seed/quickstart, FR-012 baseline re-check green (T026-T028)
- part B — Lean 4 formal gate, sorry-free decode∘encode=id (propext only); install elan/lean 4.30
- phase-b heap-embedded ModuleTerm round-trip + execute-equivalence (3/3)
- US2 contract gates + US3 coverage/completeness (41/41); reconcile contract drift (7 v2 classes, Decode record, status-based execute-equiv)
- IL codec core + harness MVP — US1 round-trip identity + execute-equivalence green (14/14)
- clarify+plan+tasks+analyze il-codec-spike (3 forks resolved; 5 analyze remediations folded)
- populate evidence-based constitution v1.0.0 (8 principles) + plan/tasks/analyze + before/after baseline & negative-control evidence
- block 07 — Polish/close-out (T025-T028); feature 28/28 complete
- block 06 — real-tool spike RUNS: SPIN (T024) + Lean tactic loop (T014/T015)
- block 05 — US1 template+interactive-spec, US2 loop-seam+no-API gate, US3/US5 docs+subjects (parallel author batch wf_17e57fd5-646)
- block 04 — US4/MLIR vertical slice complete (T016 MLIR-GLP-DIALECT.md + T018 ILFRAG-1 + T019 harness + T020 real-MLIR round-trip PASS, mlir-python-bindings 22.0.0/WSL2)
- block 03 complete — T017 real MLIR bindings via option A (mlir-python-bindings 22.0.0.2025112901, mlir.ir round-trip verified); escalation #1 resolved
- block 03 partial — real SPIN 6.5.1 (T022) + Lean 4.30.0/lean-lsp-mcp (T012) provisioned in WSL2; MLIR (T017) escalated #1 (no real wheel)
- marathon block 02 — finalize REFINEMENT-METHOD §4 six formal-tooling slots (T004) + DECISIONS-FOR-OWNER cross-link to ratified R1-R15 (T005)
- marathon block 01 — spike subtree skeleton (T001) + Python baseline (T002) + 026 input gate (T003)

### Fixed
- flip stale T017 checkbox to [X] (MLIR bindings done block 03, used block 04) — tasks.md now 28/28

### Changed
- Merge pull request #33 from olamni-glp/029-il-codec-spike
- refine(codexreview): cycle 1/10 [csharp/glp_il_codec/general]
- specify evidence-based-constitution feature (spec + requirements checklist)
- Merge pull request #32 from olamni-glp/main
- Merge pull request #31 from olamni-glp/release/v2026.06.10.1
- release: v2026.06.10.1
- Merge pull request #30 from olamni-glp/027-refinement-verification-framework
- refine(codexreview): cycle 1/10 [diff/general]
- commit marathon m57f4c46e durable JSON-mirror state for block 07 (open+approve, checkpoints 13-14, git block) — feature complete 28/28
- commit marathon m57f4c46e durable JSON-mirror state for block 06 (open+approve, checkpoints 11-12, git block)
- commit marathon m57f4c46e durable JSON-mirror state for block 05 (open+approve, checkpoints 9-10, git block, run-linkage)
- commit marathon m57f4c46e durable JSON-mirror state for block 04 (open+approve, checkpoints 7-8, git block, status)
- commit marathon m57f4c46e durable JSON-mirror state + spin scratch ahead of safe restart
- pipeline artifacts (plan/research/data-model/quickstart/contracts/tasks) + marathon launch prompt; buildkit pointer 026->027
- spec #1a refinement-verification-framework (Option D + real-tool validation spikes: Lean/MLIR/SPIN; protocol-verification armoury) + ratify R13-R15 + DEF-A3
- ratify 12 MVP-critical decisions (DECISIONS-LOG R1-R12) + anchored deferral register (DEFERRALS.md, stages A-H + pickup protocol); seed notes carry PRE-SPECIFY pointers
- apply reconciliation corrections - D3 FrameCodec payload-type-prefix-byte (header Kind is fragmentation-only), shallow-vs-deep Bindings clarification, LingoDB citation candidate; (§7 note was provenance, not a typo)
- 17-seed reconciliation memos + README index + DECISIONS-FOR-OWNER + REFINEMENT-METHOD (GEPA/DSPy + formal/pragmatic metrics, per-seed Lean4-vs-Rocq, monolith supersession)
- record owner decision - evaluate Lean4 + Rocq per seed, pick best-fit primary, keep alternative only where identified
- add #1a iterative-refinement-and-verification-framework seed; complete reconciliation brief with formal+pragmatic verification methodology (MLIR IL-dialect, model-agnostic Lean/Rocq via Claude, ANTLR4 grammar-verifier, Shapiro-criteria pragmatic anchor, no-API resolution)
- dossier seed cross-refs (in-situ §1-§9 + Appendix B registry) + reconciliation brief with GEPA/DSPy metrics methodology and formal-verification research
- engine-separation design dossier (§0-§12, re-verified citations); complete tasks; verify roadmap seeds
- plan + Phase-1 artifacts + tasks + analyze remediations for engine-review-dossier
- engine review + refactoring design dossier spec (specify + clarify)
- Merge pull request #29 from olamni-glp/main

## [v2026.06.10.1] - 2026-06-10

### Added
- block 07 — Polish/close-out (T025-T028); feature 28/28 complete
- block 06 — real-tool spike RUNS: SPIN (T024) + Lean tactic loop (T014/T015)
- block 05 — US1 template+interactive-spec, US2 loop-seam+no-API gate, US3/US5 docs+subjects (parallel author batch wf_17e57fd5-646)
- block 04 — US4/MLIR vertical slice complete (T016 MLIR-GLP-DIALECT.md + T018 ILFRAG-1 + T019 harness + T020 real-MLIR round-trip PASS, mlir-python-bindings 22.0.0/WSL2)
- block 03 complete — T017 real MLIR bindings via option A (mlir-python-bindings 22.0.0.2025112901, mlir.ir round-trip verified); escalation #1 resolved
- block 03 partial — real SPIN 6.5.1 (T022) + Lean 4.30.0/lean-lsp-mcp (T012) provisioned in WSL2; MLIR (T017) escalated #1 (no real wheel)
- marathon block 02 — finalize REFINEMENT-METHOD §4 six formal-tooling slots (T004) + DECISIONS-FOR-OWNER cross-link to ratified R1-R15 (T005)
- marathon block 01 — spike subtree skeleton (T001) + Python baseline (T002) + 026 input gate (T003)

### Fixed
- flip stale T017 checkbox to [X] (MLIR bindings done block 03, used block 04) — tasks.md now 28/28

### Changed
- Merge pull request #30 from olamni-glp/027-refinement-verification-framework
- refine(codexreview): cycle 1/10 [diff/general]
- commit marathon m57f4c46e durable JSON-mirror state for block 07 (open+approve, checkpoints 13-14, git block) — feature complete 28/28
- commit marathon m57f4c46e durable JSON-mirror state for block 06 (open+approve, checkpoints 11-12, git block)
- commit marathon m57f4c46e durable JSON-mirror state for block 05 (open+approve, checkpoints 9-10, git block, run-linkage)
- commit marathon m57f4c46e durable JSON-mirror state for block 04 (open+approve, checkpoints 7-8, git block, status)
- commit marathon m57f4c46e durable JSON-mirror state + spin scratch ahead of safe restart
- pipeline artifacts (plan/research/data-model/quickstart/contracts/tasks) + marathon launch prompt; buildkit pointer 026->027
- spec #1a refinement-verification-framework (Option D + real-tool validation spikes: Lean/MLIR/SPIN; protocol-verification armoury) + ratify R13-R15 + DEF-A3
- ratify 12 MVP-critical decisions (DECISIONS-LOG R1-R12) + anchored deferral register (DEFERRALS.md, stages A-H + pickup protocol); seed notes carry PRE-SPECIFY pointers
- apply reconciliation corrections - D3 FrameCodec payload-type-prefix-byte (header Kind is fragmentation-only), shallow-vs-deep Bindings clarification, LingoDB citation candidate; (§7 note was provenance, not a typo)
- 17-seed reconciliation memos + README index + DECISIONS-FOR-OWNER + REFINEMENT-METHOD (GEPA/DSPy + formal/pragmatic metrics, per-seed Lean4-vs-Rocq, monolith supersession)
- record owner decision - evaluate Lean4 + Rocq per seed, pick best-fit primary, keep alternative only where identified
- add #1a iterative-refinement-and-verification-framework seed; complete reconciliation brief with formal+pragmatic verification methodology (MLIR IL-dialect, model-agnostic Lean/Rocq via Claude, ANTLR4 grammar-verifier, Shapiro-criteria pragmatic anchor, no-API resolution)
- dossier seed cross-refs (in-situ §1-§9 + Appendix B registry) + reconciliation brief with GEPA/DSPy metrics methodology and formal-verification research
- engine-separation design dossier (§0-§12, re-verified citations); complete tasks; verify roadmap seeds
- plan + Phase-1 artifacts + tasks + analyze remediations for engine-review-dossier
- engine review + refactoring design dossier spec (specify + clarify)
- Merge pull request #29 from olamni-glp/main

## [v2026.06.08.1] - 2026-06-08

### Added
- GATE D Dart<->Dart 8/8 green — path-B listen-driver fix + clean link shutdown
- Phase D layer 2 complete — async-aware link establish + 7 kernels + boot + engine async pump-driver
- Phase D layer 1 — Dart mirror of link seam+reliability+transports
- WORKING two-process producer/consumer over real TCP (C# REPL x2, 127.0.0.1) - Got=[10,20,30] byte-identical. Fixes: TcpTransport connect-retry (timing-independent rendezvous) + LinkTerms.Unquote (GLP string constants carry quotes by design for type-checker string-vs-atom; kernels must strip for host interop - xUnit used bare ConstTerms, hiding it). pc.glp role-boot demo (T037)
- relocate link types+wrappers link.glp -> root self.glp (Gabi-approved A, callable universally like send/receive) + deep-deref kernels for real compiler terms (LinkTerms.GroundResolve across all 7 kernels; xUnit used ground ConstTerms, hiding the nested-VarRef bug); Dart baseline 524/525, 99 xUnit, wrapper->kernel chain proven on C# REPL
- T038 wire link kernels into C# REPL boot (exe composition-root hook -> LinkKernels.Install + register TcpTransport/LoopbackTransport) + TcpTransport (raw TCP/IPv4 localhost, first real cross-process leaf) + C# builtinProcedures mirror; link.glp loads on C# REPL; 99/99 xUnit
- T036 programs/lib/link.glp - link-layer types + 12 GLP wrappers over the host kernels (H1/H2/H3/M1 mode fixes applied); register 7 ratified link kernels in type-checker builtinProcedures allowlist; loads clean via dart REPL, baseline 524/525 unchanged
- T035 link_close - '_link_close'/2 + graceful [] close converge on LinkTeardown core (emit closed(LinkId,Reason) on every monitor + end-stream + CloseAsync + live T024 GC via LinkRuntime.Reclaimer); data path untouched (FR-024/044); 95/95 xUnit
- T034 per-link fault monitor - '_link_monitor'/2 + LinkFaults fan-out core + LinkHandle.MonitorCursors + pump OnFault->inbox delivery; fault = bound term on per-link stream (never 4th verdict/never Fail; FR-008/043-046); 85/85 xUnit
- T033 path-B handshake (Option A) - request/listen/accept kernels + explicit request_listener + rendezvous term; shared LinkEstablish core converges all paths on T030 registry (FR-002/R-5); 79/79 xUnit
- T031 '_link_send'/3 kernel + shared LinkEgress ground-relay ship (LinkId face backs out_relay/3; deep ground-resolve gate; 72/72 xUnit)
- T030 '_link_setup'/5 kernel + Option-B LinkPump (setup/egress/ingress wiring over loopback; idempotent-at-identity; 66/66 xUnit)
- Option-B inbound-pump seam (IInboundPump + engine.InboundPump + run-to-quiescence driver loop in both goal paths); null-guarded = zero change for non-link runs; out/csharp builds clean, glp_link.tests 62/62
- T030 infra - LinkTerms mapping + TransportRegistry + idempotent LinkRegistry + LinkHandle (FR-007/013); 62/62 xUnit green
- T026 deterministic loopback transport + full Phase-2-stack round-trip test (FR-002/004/018/020); Phase 2 complete, 52/52 xUnit green
- T025 bounded backpressure SendWindow N=8 (FR-025); 44/44 xUnit green
- T024 distributed GC framework - LinkReclaimer + ResourceSnapshot (FR-024); 36/36 xUnit green
- T023 epoch/fencing token split-brain defense (FR-047); 30/30 xUnit green
- T022 per-link sequence/dedup + FIFO + reorder buffer (FR-020/023/053); 22/22 xUnit green
- T021 wire format - version+length/CRC32+fragmentation/reassembly+cycle-guard (FR-022); 15 xUnit tests green
- T020 LinkTransport seam (ILinkTransport/ILinkEndpoint + value types) in clobber-safe csharp/glp_link/ (FR-058); T002-T004 bookkeeping
- FR-037/SC-006 @< @> @=< @>= standard-order term-comparison guards (lexer+parser+runner _compareTerms+analyzer+prelude+self.glp; Dart + C# mirror; Section A24f tests)
- FR-033/SC-005 atom/1 guard = string/1 synonym (runner arm + prelude reg + self.glp decl + C# mirror + Section A24d/e tests)

### Fixed
- codexreview cycle 1 — loopback cancel busy-loop + _rendezvous socket leak + clean recv-loop teardown
- LinkTerms.ToTerm re-quotes string components + path-B example
- core runner heap-addr/register-index deref conflation (Dart + C# mirror)
- FR-035/SC-009 imported-reader reactivation via bindAny ingress seam (heap_fcp.dart + mad_context wiring + C# mirror + regression test)
- FR-034/SC-009 compound-operand guard suspends on nested unbound reader (runner.dart generic-guard recursion + C# mirror + Section A24b/c regression test)
- FR-021/SC-008 redelivered madGLP assignment is a verified no-op (mad_context Dart + C# mirror + regression test)
- harden marathon harness pre-marathon (rerun runId echo, resume commit/push crash guard, budget-halt escalation, live-spike recorder)

### Changed
- Merge pull request #27 from olamni-glp/025-multi-protocol-link-layer
- codexreview cycle 1 — per-peer timeout guard on link harnesses (fail-fast, never hang the gate)
- marathon status checkpoint row 100 (GATE D + cross-runtime green)
- cross-runtime Dart<->C# link rig — 16/16 both directions (release gate T042/T081)
- persistent-embeddable-engine epic dossier (REPL/engine separation)
- wip(025): Phase D async-correctness fixes — Dart<->Dart 6/8 two-process GATE-D green
- wip(025): Phase D layer 2 partial — link primitives infra+glue + null-guarded core inbound-pump seam
- fault-monitor + graceful-close two-process link example (FR-008/044)
- bidirectional (FR-003) + link_recv-chain two-process link examples
- checkpoint WIP — requirements edit, design dossier (docx/pdf), transport-runtime-feasibility research, buildkit-codexreview skill, marathon harness state
- refresh restart resume pointers - CURRENT STATUS block in runtime-integration-plan.md (Phase A/B done, Phase C 4/4 two-process examples + driver, next = link_recv-chain debug -> monitor/path-B/bidir -> Dart mirror -> regression); tasks.md points to it
- add link_send/3 wrapper producer (producer_ls) to pc.glp + driver - 4/4 two-process examples PASS (integers, strings, compound terms, link_send wrapper over real TCP). Isolated: explicit link_recv-chain consumer has a separate runtime issue (link_recv alone suspends correctly; the 3-recv concurrent-body variant fails) - next debug batch
- scripted 2-process real-TCP link integration driver (test/link/run_link_tests.sh) + pc.glp integer/string/compound-term producers; 3/3 PASS over 127.0.0.1 (Got byte-identical to produced values); results captured to test/link/results/
- T032 recv-ingress contract proof (suspend/reactivate-once/dup-no-op/reorder on the T030+T022 ingress; link_recv composable; 76/76 xUnit)
- resume pointer + T030 status (infra+Option-B done, kernel next); marathon-checkpoint-stale caveat
- inbound-pump + isolate_manager design reference (md/docx/pdf) + Option-B decision record
- T013 FR-032 consolidate guards-reference.md as single authoritative guard spec (fold in @< @> @=< @>= standard-order family + atom/1=string synonym + decline == \== \= reader/1 with canonical forms; =\= unchanged; nested-compound suspend note)
- T012 FR-033/036/037/038 guard three-valued + decline + =\= regression (@< & atom reactivate-exactly-once Section A24g; =\= untouched A24h; declines == \== \= reader/1 rejected Section C; @< & atom SRSW-relaxation Section B; +13 checks, suite 524/525)
- correct exemplar GLP per REPL-verified canonical forms (channel-head modes, send-shape, output-holes, bare-_ singletons, body-= -> head-construct, Fault/Link types); add adversarial GLP review (2 passes) + canonical-forms card
- plan block - plan/tasks/analyze + design dossier, contracts, per-transport tutorials, integration-harness + coverage matrix (gate ruled: 9 primitives + guard set + 3 core fixes)
- clarify block — resolve peer-id ordering (ruling B: compound/totally-ordered, @</@> family in scope)
- specify block - spec.md (67 FR/17 SC/4 stories) + requirements checklist
- Merge pull request #26 from olamni-glp/marathon-harness-hardening
- lock B2/B3/G rulings — C#-first reference, base-primitives-before-glink, keep+implement comparison-guards, keep BLE BIS, cross-runtime Dart<->C#
- B2/B3/G decision doc + 18-source provenanced corpus (multi-protocol-link-layer design study)
- end-to-end marathon kickoff prompt for multi-protocol-link-layer (fresh-session launch template)
- SKILL.md — honor rerun workflow_run_id (resumeFromRunId) + resume commit_push_pending crash-window on resume
- Merge pull request #25 from olamni-glp/main

## [v2026.06.05.1] - 2026-06-05

### Added
- polish — auto-mode policy, stage-hook skill, docs, multi-session e2e (marathon complete)
- US2 gate + US3 rerun + US5 status/budget + US6 gitblock + US7 trace
- US4 verify-spike + US1 restart-safe resume MVP (resume/reconcile, gate reader, budget, trace)
- marathon harness foundation — 0010 schema, dual store, cadence, start/doctor

### Fixed
- guard rerun_subagent against sibling-block units (FR-007) + regression test

### Changed
- Merge pull request #23 from olamni-glp/024-marathon-stage-harness
- refine(codexreview): cycle 1/5 [diff/general]
- plan + tasks + analyze for marathon-stage-harness (one logical block)
- specify + clarify marathon-stage-harness spec
- roadmap + buildkit pipeline state as the restart-resume source of truth; current_plan.md → thin pointer
- add buildkit-roadmap skill forwarder
- mark comparison guards implemented in glp-bytecode-v216 11.7 (was stale Planned)
- Merge pull request #22 from olamni-glp/main

## [v2026.06.04.1] - 2026-06-04

### Added
- US5 backend choice + dart fallback, exit-codes 6/11 (exec-path+drift), JSON/parity tests, docs
- /glptutorial-run unified run-model (preview/run/explain/propose) + shape-classifier + skill
- /glptutorial-list GLP tutorial browser (bridge-free codeconv tutorials list)

### Fixed
- converge C# arithmetic to Dart num (int-preservation) + Dart double printing; A5 convergence record
- converge C# moded-path rendering to Dart lowercase mode words (AsModeString)
- converge C# runner constant matching to Dart num== (NumEquals) — fixes recursive base-clause selection
- converge C# runner guard dispatch — add is_list/tuple guard aliases per runner.dart
- converge C# type DFA — add Any builtin type (states/automata/leaf arms) per program_dfa.dart
- converge C# REPL to Dart — self.glp path resolver + tuple/is_list builtins

### Changed
- Merge pull request #20 from olamni-glp/023-glptutorial-run
- add buildkit-ship + buildkit-release skill forwarders (CLI was installed; skills were missing)
- gated real-backend coverage for ch03 multi-compose + ch07 use-case (US2)
- plan, research, data-model, contracts, tasks for /glptutorial-run
- Merge pull request #19 from olamni-glp/main

## [v2026.06.03.1] - 2026-06-03

### Added
- clone GLP tutorial corpus into glpnet (olamni/tutorial, 47 .glp + 42 repl-trace.md, byte-identical to sibling) - self-contained equiv corpus, no sibling dependency
- converge test/ harness to sibling (to_repl_path + run_aot_smoke/run_cross_mode_parity) - fixes suite vs converged loader; point equiv oracle tests at the cloned-in tutorial corpus
- programs/.glp byte-identical to sibling (Gabi-approved) - self.glp +procedure tuple/is_list (completes runner is_list/tuple convergence) + 4 typed_book play sources (bonds/agent, cssg+cssn typed_social_agent, cssn typed_ui_mediator); programs .glp diff=0
- add bin/triage_loader.dart from sibling (new file under gitignored bin/, force-added) - completes bin Dart convergence
- glp_runtime lib+bin DART byte-identical to sibling GLP - 9 lib overwrites (runner+is_list/tuple, compiler x3, glp_engine, type_checker x3, repl_play_runner) + delete unify_result.dart + bin/glp_repl.dart (Windows/abs path fix) + triage_loader.dart; rebuilt golden exe; static diff=0, tutorials 77/88 (was regressed; remaining 8 are program-level)
- comprehensive sweep driver (incr 3) - sweep() runs goal-bearing corpus through dual-REPL oracle, tallies equivalent/divergent/needs_agent_work/error + decision-2 outcome cross-check; 2 hermetic tests green
- live dual-REPL capture backend (incr 2) - capture_pair/compare_goal spawn Dart golden(:trace+:debug)+C# candidate(GLP_EQUIV_TRACE), outcome cross-check (decision 2), strict verdict; injectable spawn; 8 tests green incl live append([1,2,3]) EQUIVALENT
- goals.yml reviewed artifact (incr 1b) - to_yaml/load/write_artifacts serde + round-trip test; seed 88 ch01-06 goals for review (g1=c)
- goal-bearing tutorial corpus parser (incr 1a) - GoalEntry + parse_trace_goals handles in-fence+prose formats w/ load-context source tracking; 88 goals from ch01-06; 6 pure tests green
- T031 part-a - fidelity GEPA metric (SC-004 import identity) + optimize oracle seam
- T022 - parse_dart adapter (Dart :trace/:debug -> canonical wire); 28/28 events match append fixture, only OUT pending finding-#3 deref
- T022 - relabel goal ids in separate g-namespace (GoalId sentinel) instead of dropping goal; SUSPEND/REACTIVATE goal stays a (relabeled) fidelity signal. 34 equiv pure tests green
- T017(ii) option-a - align BYTECODE_OP spine to Dart :debug-observable op set (14 ops; exclude conditionally-printed GetValue); append spine now matches golden except the isolated Ground->Commit divergence
- Stage 5 T017(ii) - candidate-side canonical EV/OUT trace emission (equiv_trace.cs) at runner spine/commit/suspend seams + engine OUT; flag-gated (GLP_EQUIV_TRACE), no-op + behaviour-unchanged when off
- Stage 5 T017(i) - wire glp_repl exe to converted REPL (delegating entrypoint); runs + matches Dart golden on true.
- Stage 4 COMPLETE — goal_queue marked no_emit on canonical cluster (migrate 0009 applied; status no_emit:1/escalated:0/open_escalations:0); E1 escalation resolved (option-a no_emit)
- Stage 4 CODE — first-class no_emit status (migration 0009 single-head off 0008; status() _classify_codegen_row precedence; mark-no-emit CLI; readiness satisfied; codegen_no_emit tombstone key); offline tests 19/19 green. Canonical migrate+mark PENDING Gabi OK.
- Stage 3 runner ingest — build-gate pass → built; E1 escalation resolved (6-chunk conversion); frontier now 74/75 built, 1 escalated (goal_queue=Stage 4)
- runner.cs Stage 3 chunk 6/6 — concurrency arms (Spawn/Requeue/Distribute/Transmit via GlpChannel) + guard arms (Guard/Ground/GroundEqual/Known/NoReaders) + all 6 helpers (_evaluateGuard 25-arm switch, _termsEqual cycle-detect, _dereferenceWithTracking, _evaluateArithmetic, _convertTentativeToStruct); runner.cs COMPLETE (5740 lines), full sln green 0 errors, zero stubs
- runner.cs Stage 3 chunk 5/6 — clause control + Commit (ApplySigmaHatFCP) + env (Allocate/Deallocate) + Push/Pop/TailStep/Union/Reset/Proceed/Otherwise/Nop/Label/Halt; sln green
- runner.cs Stage 3 chunk 4/6 — BODY-phase structure building (Put[Constant|Structure|Nil|List|BoundConst|BoundNil], SetConstant, BodySet[Const|ConstArg|StructConstArgs]); sln green
- runner.cs Stage 3 chunk 3/6 — UNIFY arms (Constant/Void/Structure) + v1 Get[Variable|Value] + all 7 v2 arms; sln green
- runner.cs Stage 3 chunk 2/6 — HEAD-phase arms (HeadConstant/Structure/Nil/List, HeadBindWriter[Arg], Require[Reader|Writer]Arg, GuardNeedReader[Arg]); sln green
- runner.cs Stage 3 chunk 1/6 — skeleton (support types real + RunStep/RunWithStatus loop + 60-arm _Step dispatch + stub Exec/helpers); full sln green, downstream unbroken
- Stage 2 — GEPA run on bytecode (build-only): generator regenerated opcodes->C# (1.0), build ceiling confirmed, bytecode.md frozen w/ measured provenance; gitignore covers per-subsystem candidate + GEPA scratch
- Stage 1 — per-subsystem Claude-driven GEPA wiring (T032 dataset split, T033 program subsystem field, T034 prompt.load(subsystem), T035 codegen-opt skill loop + dataset/score CLI, T036 _base+5 subsystem prompts); build-only metric per 2026-06-03 decision; 24/24 targeted tests green
- bulk codegen FINAL — 73/75 built (97.3%); 2 escalated (runner.dart 4863-line interpreter deferred; goal_queue Dart-export no-emit by design). codegen, compiler, glp_engine, isolate_manager, agent_runtime, bin/glp_repl all built against runner stub; full sln dotnet build GREEN (0 errors, 140 warnings); gitignore allows out/csharp/bin/*.cs source while still ignoring dotnet Debug/Release output.
- bulk codegen batches 15-16 — 5 built (system_predicates_impl, result, asm, scheduler, linter; downstream files built against runner.cs stub)
- bulk codegen batch 14 — pmt/validator built (added Module.ModeDeclarations() extension stub for missing dep)
- bulk codegen batch 13 — SCC cg=36 + pmt/checker (6 built: pmt/checker, mad_context, body_kernels, glp_activation, runtime, system_predicates; class GlpRuntime renamed to GlpRuntimeEngine to disambiguate namespace; runner.cs stubbed + escalated — 4863-line WAM dispatch exceeds single-pass)
- bulk codegen batch 12 — 5/5 built (occurrence, pmt/type_checker, commit, external_io, suspend_ops; ModedArg extended with TypeName/TypeParams + ModeDeclaration.Predicate to resolve pmt/type_checker E1/E2/E3)
- resolve 2 escalations — heap_fcp (CellTag→HeapCellTag rename) + mode_table (new mode_declaration.cs stub); 50/75 built (Gabi-approved 2026-05-28)
- bulk codegen batch 10 — 1/1 built (project_linker; manual patch for 2nd missing guards param)
- bulk codegen batch 9 — 3/3 built first pass (type_checker, analyzer, module_hierarchy)
- bulk codegen batch 8 — 2/2 built (type_env_builder, partial_evaluator; 1 repair)
- bulk codegen batch 7 — 3/3 built (suspend, well_typed_clause, parser; parser needed long→int site missed by repair-agent)
- bulk codegen batch 6 — 5 built (2 repairs) + 2 escalated (mode_table dep_missing, heap_fcp CellTag conflict)
- bulk codegen batch 5 — 7/7 built (4 first-pass + 3 bounded repairs)
- bulk codegen batch 4 — 7/7 built first pass (topo=1 mixed)
- bulk codegen batch 3 — 6/7 built + 1 escalated (goal_queue Dart export-only, undecidable per spec)
- bulk codegen batch 2 — 7/7 built first pass (compiler/engine/multiagent leaves)
- bulk codegen batch 1 — 7/7 built (analysis/type_checker/bytecode/compiler leaves)
- codegen Converted.props append hook + 12 pure tests (bulk-codegen pre-req B)
- T025 + C# REPL infra (out/csharp .sln/.csproj/Converted.props + glp_repl placeholder, dotnet build green); safe-restart ledger for bulk codegen drive
- US2 readiness + durable equiv-step pure core (T023/T024)
- US1 capture/compare/bytecode-diff CLI (T018/T019) — standalone deterministic verdict over recorded artifacts; shared db.engine.connect; DB writes deferred to durable step (T024)
- US1 corpus.py + reviewed corpus.yml enumeration + materialized split (T016; 256 sources, book 141 exact)
- US1 oracle core — normalize/relation/bytecode_diff + SC-005 batteries (T013-T015, T020-T021, 21 pure green)
- Setup + Foundational — migration 0008, equiv tool skeleton, pure trace/fidelity/manifest, tombstone keys (T001-T012, 14 pure tests green)

### Fixed
- capture uses repo-root-relative (../) load paths - current Dart REPL (glp_repl.dart:193-198) only honors / ./ ../ verbatim and roots else at glp/, so Windows-abs D:/ mis-resolved; sibling tutorials load as ../GLP/... (FR-006, no copy); 8 capture tests green
- T022 finding-#3 - recursively deref OUT binding shape (candidate-side); re-captured append_csharp OUT now ./2(const(a),./2(const(c),const(nil)))
- #2 resolved - emit Commit conditionally from ExecCommit (proceeding-commit only) to match Dart's conditional COMMIT print; NOT a runner bug. Append spine now matches golden exactly across all 3 goals
- Stage 5 - scheduler.cs success-determination wires onReduction callback (was stub-era gap); converted REPL now matches Dart golden on append/reverse/quicksort
- buildprops — ignore example Include in header comment (regression test added)

### Changed
- Merge pull request #12 from olamni-glp/020-trace-equivalence-fidelity
- plan - top-priority Dart convergence mandate (glpnet glp_runtime <= sibling GLP, 100% byte-level, static+dynamic)
- design - combined comprehensive equiv test driver + goal-bearing corpus (suites + sibling tutorials; ratified decisions 1-4)
- back up frozen build-only bytecode.md (9506ac81) before T031 fidelity re-run can overwrite it; restore via cp
- .codeconv updates
- HANDOFF - turnkey T031 fidelity-metric-swap build spec (part-a metric rewrite mock-testable now; part-b GEPA re-run forces the T018-capture sequencing decision); T017/T022 marked done in S3
- HANDOFF - T022 COMPLETE (parse_dart adapter + finding-#3 deref + e2e green); next = T031 fidelity-metric swap + GEPA re-run
- T022 e2e - append strict-tier oracle equivalence over captured pair (Dart golden = C# candidate); finding-#3 + parse_dart regression guards + negative controls; 6 green
- HANDOFF - one-line state points at T022 parse_dart as the immediate next (T017 complete)
- HANDOFF - turnkey parse_dart build spec (line-by-line append mapping, shape canonicalizer incl list syntax, C# OUT deref fix); goal kept via relabeling done
- T022 - capture matched append fixtures (C# canonical EV/OUT + Dart :trace+:debug) for the parse_dart adapter + e2e
- HANDOFF - T022 scoping (parse_dart finalization plan + 3 normalization items; goal-field comparability decision teed up)
- HANDOFF - #2 RESOLVED (conditional Commit emission, not a runner bug); append spine matches golden exactly
- HANDOFF - finding #1 RESOLVED via option-a spine alignment; #2 (Ground->Commit) isolated as sole remaining append divergence
- HANDOFF - T017(ii) done; record real-capture findings (Dart :debug partial-spine spec-gap, Ground soft-fail spine divergence, shallow OUT shape)
- HANDOFF - Stage 5 progress: T017(i) wired + first fidelity bug (scheduler onReduction) fixed; carry-forward note
- safe-restart prep - re-verify anchor green 2026-06-03; pure subset 40->36; note section-1c run-from-repo-root bridge trap
- SAFE-RESTART handoff — Stages 1-4 DONE (incl Stage 4 canonical no_emit), only Stage 5 left; anti-drift facts (runner.cs compile-verified-only + semantic-risk list) + verified-green anchor + Stage-5 recipe; ledger RESTART pointer
- ledger — Stage 3 DONE (runner.cs converted+built), Stage 4 code DONE (canonical migrate+mark GATED on Gabi OK), Stage 5 unblocked+mapped
- spec(020-trace-equiv): gepa_optimizer contract — NO-API/Claude-driven GEPA revision (ruled 2026-06-03); the spec-first basis Stage 1 implements
- ledger — Stages 1+2 DONE (72ca51d1, 9506ac81); runner.cs (Stage 3) is the gate, Stage 5 blocked on it, Stage 4 no_emit confirm-with-Gabi; precise restart maps recorded
- ledger — Stage 1 (Claude-driven GEPA wiring) DONE at 72ca51d1; NEXT=Stage 2 GEPA on bytecode
- mark bulk drive COMPLETE at 73/75 (97.3%); escalations resolved + final-surface analysis
- bulk drive PAUSED at 48/75 — escalation cascade analysis + Gabi-decision request
- checkpoint ledger at 47/75 built (mid bulk drive)
- record bfd00a8a + flip POSITION to A in-progress
- record dc997583 in safe-restart ledger

## [v2026.06.03.3] - 2026-06-03

### Changed
- Merge pull request #17 from olamni-glp/main
- Merge pull request #15 from olamni-glp/021-buildkit-gitflow-adoption
- adapt glpnet branching/versioning to canonical buildkit GitFlow (feature->develop->release->main, CalVer vYYYY.MM.DD.N via buildkit release; CLAUDE.md branch rules + end-of-task ship)

## [v2026.06.03.2] - 2026-06-03

# Changelog

All notable changes to GLPNET. Versions follow the CalVer convention defined in
[`docs/VERSIONING.md`](docs/VERSIONING.md): tags are `vYYYY.MM.DD[-N]` where the
optional `-N` suffix increments per same-day release.

## [v2026.05.17] — 2026-05-17

### Added

- **codeconv conversion pipeline integrated into `main`.** Features 015
  (depgraph + conversion-readiness oracle, non-destructive option-A'
  referential completeness), 016 (`codeconv-init` / `codeconv-scaffold` /
  `codeconv-mirror` Dart→C#/.NET pipeline behind a language-pair registry),
  and 017 (`codeconv-planagents` — orchestrated per-tombstone conversion-plan
  generation, Alembic `0003` plan schema) merged together. Feature branches
  are no longer maintained as permanently separate spaces.

### Changed

- **PGLite cluster rebuilt on PostgreSQL 17.** The PG16→PG17 data migration
  was closed (not performed): under codeconv all data is recreatable afresh,
  so the stale PG16 canonical cluster `C:/pglite/research/glpnet/` was retired
  to a gitignored `.dbsnapshots/` (fileset + integrity-checked snapshot
  archive) and a fresh PGLite 0.4.5 / PG17 cluster created and migrated
  (Alembic `0001`/`0002`/`0003` + DBOS). Bridge/sidecar suite green (8/8).

## [v2026.05.09] — 2026-05-09

### Added

- **`prereq-patterns/` catalog**. New top-level peer of `specs/`, `docs/`,
  `programs/`, `glp_runtime/`, `glp_multiagent/`, `test/`, holding curated
  prerequisite implementations any future glpnet feature can adopt without
  re-deriving the design. Lands three governance files (`directory.md`,
  `howto.md`, `policies.md`) plus eight pattern sub-directories — `pglite`
  (active), `dbos`, `flask-sqlalchemy-alembic-api`, `pglite-backup-restore`,
  `blazor-spa-bg-api`, `background-task-manager`, `local-secrets-store`,
  `secure-signatures` (all `draft`) — each with its required
  `description.md`, `applicability.md`, `sources.md`. `policies.md` carries
  Policy 1 (no cleartext auth tokens; secret-material hashes restricted to
  `{Argon2id, scrypt, bcrypt}`) and Policy 2 (operational data routes to
  `D:/BSTDEV/research/glpnet-datalake/<pattern-or-app>/<data-class>/<partition>.parquet`).

- **Merged pglite bridge** at `prereq-patterns/pglite/pglite_bridge.mjs`.
  Single canonical implementation consolidating glpnet's no-pg-gateway
  hand-rolled wire-protocol bridge (Npgsql / psqlODBC compatible; two
  diagnosed bug fixes — PGLite implicit-Sync after `execProtocolRaw`;
  pg-gateway 0.3.0-beta.4 response-corruption avoidance) with AIGRID's
  `globalWorkChain` global FIFO, per-connection `workChain`,
  `endsAtFlushBoundary()`, synthetic-`ROLLBACK` startup handshake, Windows
  `DETACHED_PROCESS` lifecycle (via the cited Python sidecar), `sidecar.json`
  discovery, and `@electric-sql/pglite@0.2.17` pin (sibling
  `package.json`).  `COPY ... FROM STDIN` interception is dropped with
  rationale — PGLite WASM does not implement COPY-IN over the wire.

- **Format contracts** at `specs/011-prereq-patterns-catalog/contracts/`. Six
  format contracts copied verbatim from AIGRID
  (`@004a-opskit-sidecar-autospawn`, SHA `83b60585...`) and scrubbed of
  AIGRID-only references per FR-011: `description_md_format.md`,
  `applicability_md_format.md`, `sources_md_format.md`, `directory_md_format.md`,
  `howto_md_format.md`, `policies_md_format.md`.

- **Pglite merge analysis** at
  `specs/011-prereq-patterns-catalog/pglite-merge-analysis.md`. Classifies
  every distinguishing feature of both pre-merge bridges (16 from glpnet
  `bridge-direct.mjs`, 18 from AIGRID `pglite_bridge.mjs`) as
  `present-in-merged` / `superseded-with-rationale` / `dropped-with-rationale`.
  Zero unclassified.

- **Conformance script** at
  `specs/011-prereq-patterns-catalog/conformance-check.ps1`. Pure PowerShell,
  no third-party dependency. Implements C1 (three-files-per-pattern), C2
  (lifecycle agreement), C3 (catalog self-containment), C4 (no live AIGRID
  cross-references), C5 (format-contract reachability), C6 (migration-analysis
  completeness). Final pre-merge gate: PASS on all six checks.

- **`docs/research/pgbridge-reference/MIGRATED.md`** — forwarding note from
  the archival pre-merge investigation directory to the canonical merged
  bridge under `prereq-patterns/pglite/`.

### Validated

- **Catalog conformance gate**. `conformance-check.ps1` ran from the repo
  root with exit code `0`: 109 internal markdown links resolve inside glpnet,
  75 grep hits for `breenlake|aigrid|opskit` all in allowed contexts
  (`sources.md` files or "external sibling" footnote in `policies.md`), 34
  classification rows across 2 tables in `pglite-merge-analysis.md` all
  with valid classifications and non-empty rationales, and "Unclassified:
  0" assertion present.

### Deferred

- **SC-003 (Npgsql / psqlODBC connectivity, 100 sequential cycles)** and
  **SC-004 (psycopg-style concurrent-pipeline invariant)**. Buildable success
  criteria intentionally NOT verified by this catalog-import feature —
  documented verbatim in `prereq-patterns/pglite/sources.md` (Flow D1 / D2)
  for the first glpnet feature that *adopts* the merged bridge to run as part
  of its own work.

### References

- Spec: `specs/011-prereq-patterns-catalog/spec.md`
- Plan: `specs/011-prereq-patterns-catalog/plan.md`
- Tasks: `specs/011-prereq-patterns-catalog/tasks.md`
- Handover: `specs/011-prereq-patterns-catalog/handover.md`

## [v2026.05.02] — 2026-05-02

### Validated

- **`/D2NET-scaffold` in-session smoke walks**. Rows 1, 2, 3, 4, 5, 8 + the
  T013 idempotent re-run from `specs/010-scaffold-skill/validation.md` executed
  in-session against the binary at `tools/d2net/src/D2Net.Scaffold/bin/Release/
  net8.0/d2net-scaffold.exe` (version `0.2.0+a89bed71`) and the
  `glp_runtime → glp_runtime_net (_net)` workspace. All seven walks PASS:
  `--help`, `--version`, default scaffold (empty input), `--json` (verbatim,
  recap suppressed), `--json --bridge-port 55001` (pass-through, recap
  suppressed), `please scaffold quickly` (FR-010a → `--help`), and the
  reconciliation-block check (`added_paths: 0, removed_paths: 0`). The
  remaining 9 rows (T012, T012a, T014, T018–T022, T029) require an
  operator-driven session — fresh repo, deleted binary, destructive
  `yes/no` confirmations, or fresh-Claude-Code-session discoverability —
  and stay PENDING in `validation.md`.

### Fixed

- **T013 misstatement** in `specs/010-scaffold-skill/tasks.md` and
  `validation.md`. The task previously expected the recap to show
  `0 files copied; 0 working directories created; 0 dart_files rows updated`
  on idempotent re-run. The binary's `files_copied / workdirs_created /
  dart_files_updated` fields are per-run write totals (always equal to the
  full source-tree count on a successful scaffold), not net deltas — only
  the reconciliation block (`added_paths / removed_paths`) carries the net
  change. The corrected expectation references spec 009 User Story 2
  Acceptance Scenario 3 ("zero net additions and zero net removals") and
  the reconciliation summary's `0 added paths; 0 removed paths`.

## [v2026.05.01] — 2026-05-01

### Added

- **`/D2NET-scaffold` Claude Code skill.** Wraps the spec-009 `d2net-scaffold`
  CLI as a slash command, sibling to `/D2NET-init`. Empty input
  (`/D2NET-scaffold`) runs the scaffold operation in default mode; the binary
  takes no positional arguments — its inputs are the workspace populated by an
  earlier `/D2NET-init`. Supports raw flag pass-through (`--json`,
  `--bridge-port <N>`, `--FORCE --DELETE-TARGET`) and natural-language markers
  (`as json` / `in json` / `structured` → `--json`; `bridge port N` /
  `bridge-port=N` → `--bridge-port N`; the closed destructive-marker word list
  `force` / `delete` / `rebuild` / `reset` / `recreate` / `reinitialise` /
  `reinitialize` / `nuke` / `wipe` / `redo` triggers the destructive gate).
  Help / version verbs (`help` / `--help` / `-h` / `version` / `--version`)
  short-circuit. Unrecognized non-empty input routes to `--help` (FR-010a).
  Auto-builds the binary on user confirmation when missing or stale.
- **Two-confirmation destructive safety flow.** Destructive invocations
  (`force delete target` or the literal `--FORCE --DELETE-TARGET` pair) require
  both (a) a skill-layer confirmation prompt naming the absolute target path,
  and (b) the binary's own interactive prompt — driven by piping `yes\n` to the
  binary's stdin only after the skill-layer confirmation has resolved
  affirmatively. The cache key is the **target directory's absolute path**
  (clarified Q2), parsed from `<cwd>/.D2NET/D2NET-Settings.json`'s `target`
  field. Already-confirmed paths skip the skill-layer prompt within the same
  conversation but ALWAYS still drive the binary's prompt (the binary
  re-prompts every invocation by design — spec 009 FR-012a hard safety gate).
  Unbalanced flag pair (only one of `--FORCE` / `--DELETE-TARGET` supplied) is
  passed through to the binary's `ArgParser` for exit 1 with the
  argument-error hint (FR-016).
- **Output handling.** JSON outputs (`--json` in resolved flag set) are
  surfaced verbatim regardless of size and the Claude-side recap is
  **suppressed entirely** (clarified Q1) so downstream tooling (`jq`, smoke
  tests) consumes the response cleanly. Plain-text outputs over 50 lines are
  truncated with the standard "show all / filter <substring>" footer; recap
  appended on success: `Target at <path>; <N> files copied; <M> working
  directories created; <K> dart_files rows updated; <T>s wall-clock.`
- **Exit-code hints.** 22 (`ScaffoldWorkspaceMissing` → "Run /D2NET-init
  first"), 23 (`ScaffoldSourceMissing`), 24 (`ScaffoldTargetNotEmptyAndNotManaged`
  → suggest `/D2NET-scaffold force delete target`), 25 (`ScaffoldWorkdirCollision`),
  26 (`ScaffoldCopyError` — idempotency note), 27 (`ScaffoldDbWriteFailed`),
  28 (`ScaffoldWorkspaceLocked`), 29 (`ScaffoldOperatorCancelledTargetDeletion`),
  1 (`ArgumentError`).
- **Casing requirement.** The skill directory and frontmatter `name` are
  exactly `D2NET-scaffold` (uppercase `D2NET`, lowercase `scaffold`). Matches
  the casing precedent of `/D2NET-init`.
- Spec under [`specs/010-scaffold-skill/`](specs/010-scaffold-skill/):
  spec.md (5 clarifications resolved — JSON suppresses recap; cache key =
  target absolute path; show-all/filter via conversation context; empty
  input = run scaffold; unrecognized non-empty = run `--help`), plan.md,
  research.md (11 R-decisions covering all spec-time deferrals), data-model.md,
  contracts/skill-contract.md, quickstart.md, tasks.md, validation.md (smoke
  walkthrough seed; PENDING rows filled at operator-driven validation time).

### Notes

- The skill is purely additive — no changes to `tools/d2net/` or any existing
  test. The shipped D2Net.Init and D2Net.Scaffold test suites continue to pass
  unchanged.
- Bridge-port auto-retry from `/D2NET-init` (3-attempt walk-forward ladder) is
  **deliberately not** implemented for `/D2NET-scaffold`. Scaffold's exit-code
  catalogue does not include a dedicated `BridgePortInUse` code; collisions
  surface as exit 27 / 28 depending on which subsystem fails first. Auto-retry
  across these would be a guess rather than a precise recovery; operators
  diagnose root cause manually (research.md R8).

## [v2026.04.30-5] — 2026-04-30

### Added

- **`/D2NET-init` Claude Code skill.** Wraps the spec-005 `d2net-init` CLI as a
  slash command for one-line invocation from any Claude Code session in this
  repo. Supports raw flag pass-through, key-value natural-language
  (`source=X extension=Y target=Z`), positional verbs (`init`, `list`,
  `exclusions`, `current-phase`, `help`, `version`), and a single-token
  shortcut (`/D2NET-init glp_runtime` derives `_net` defaults after
  confirmation). Auto-builds the binary on user confirmation when missing or
  stale. Confirms before destructive operations
  (`--FORCE --DELETE-EXISTING`); confirmed paths skip re-prompts within the
  same conversation. Surfaces JSON outputs verbatim regardless of size;
  plain-text outputs over 50 lines are truncated with a "show all" footer.
  Hints recovery actions for `BridgePortInUse`, `pglite_init_failed`,
  `NodeMissing`, and `WorkspaceAlreadyExists` exit codes. Casing is exactly
  `D2NET-init` (filesystem path, frontmatter, slash-command name).
- Spec under [`specs/006-d2net-init-skill/`](specs/006-d2net-init-skill/):
  spec.md (3 clarifications resolved — auto-build with single confirmation,
  JSON output bypasses truncation, single-token shortcut), plan.md,
  research.md (10 R-decisions), data-model.md, contracts/skill-contract.md,
  quickstart.md, tasks.md, validation.md.

### Notes

- The skill is purely additive — no changes to `tools/d2net/` or any existing
  test. The 89 D2Net.Init tests + 34 D2Net.Scaffold tests continue to pass
  unchanged.

## [v2026.04.30-4] — 2026-04-30

### Changed

- **`D2NET.Init` storage swap: SQLite → PGLite WASM via direct Postgres-wire bridge.**
  The shipped 002 `D2NET.Init` (v2026.04.30-2) ran on embedded SQLite via
  `Microsoft.Data.Sqlite` after the original PGLite + `pg-gateway` + ODBC stack
  failed end-to-end. The follow-up RCA (v2026.04.30-3) shipped a working
  hand-rolled bridge as a reference artefact. **This release integrates that
  bridge into D2NET.Init.** The five-table schema, all CLI flags, the
  temp-staging + atomic-rename safety pattern, and the prompt/exclusion flow
  are preserved unchanged from 002; only the storage engine and the persisted
  connection contract change. See
  [`specs/005-d2net-pglite-bridge/spec.md`](specs/005-d2net-pglite-bridge/spec.md).
- **`D2Net.Init.csproj`**: removed `Microsoft.Data.Sqlite`; added `Npgsql 8.0.3`.
  An MSBuild target now runs `npm ci` inside `pgbridge/` before compilation;
  the resulting tree (~256 MB, dominated by PGLite's bundled Postgres contrib
  extensions) is excluded from git via `pgbridge/.gitignore` but bundled into
  the build output via `<None CopyToOutputDirectory="PreserveNewest" />`.
- **`d2net-init` version bumped to `0.2.0`** to signal the storage-engine swap.
- **Default `--bridge-port`** is now `54400` (matching
  `docs/research/pgbridge-reference/`'s example). On init, the chosen port is
  persisted to `D2NET-Settings.json`'s `connection.port` and the `db_port` row
  in the `setting` table. On inspection commands, the persisted port is the
  default; `--bridge-port` on a non-init invocation overrides only the live
  run and does NOT modify settings (per FR-012 / Q3 clarification).
- **Settings JSON `connection` block reshaped**: `engine` flips from `sqlite`
  to `pglite`; `db_file` removed; `host`, `port`, `database`, `user`,
  `password`, `data_dir`, `connection_string` (Npgsql), and
  `connection_string_odbc` (`PostgreSQL ODBC Driver(UNICODE)`-style) are added.
  The `setting` table mirrors these as `db_*` keys.
- **Pre-existing SQLite-format `.D2NET` workspaces** (a `pgdb/workspace.sqlite`
  file or a settings JSON with `connection.engine != "pglite"`) are detected
  by the existing-workspace gate and refused without `--FORCE
  --DELETE-EXISTING`. No automatic data migration — re-init rebuilds from the
  source tree.

### Added

- **`tools/d2net/src/D2Net.Init/PgBridgeProcess.cs`** — IDisposable lifecycle
  wrapper for the per-invocation Node.js bridge subprocess. Spawns `node`,
  waits up to 15 s for `BRIDGE_READY`, runs the FR-006 staged shutdown on
  dispose (close stdin → 5 s → SIGTERM → 2 s → kill).
- **Vendored bridge bundle** at `tools/d2net/src/D2Net.Init/pgbridge/`:
  `bridge-direct.mjs` (verbatim port from `docs/research/pgbridge-reference/`
  with the smoke-seed `t (x INT)` table removed to preserve the
  inspection-modifies-zero-bytes invariant), `package.json` pinning
  `@electric-sql/pglite@0.2.17` as the only runtime dep, and a
  `.gitignore` for the materialized `node_modules`.
- **`scripts/verify-pgbridge-deps.ps1`** — build-time guardrail wired into
  `D2Net.Init.csproj` that walks the materialized `node_modules` and fails
  the build if `pg-gateway` is anywhere in the transitive tree (FR-008 +
  SC-010).
- **New exit codes** for bridge failures: `BridgePortInUse` (5),
  `BridgeStartFailed` (7), `NodeMissing` (10), `BridgeBundleMissing` (11).
  Pre-existing exit-code numbering preserved.
- **19 new test cases** across `PgBridgeProcessTests`,
  `BridgeStartupTests`, `InspectionPortLifecycleTests`,
  `SqliteEraDetectionTests`, `ExternalClientTests`, plus extended
  `WorkspaceLayoutTests` for SQLite-era detection. Total D2Net.Init test
  count: 89/89 passing. `D2Net.Scaffold.Tests` unaffected (34/34 passing).

### Speckit artefacts

- Full set under
  [`specs/005-d2net-pglite-bridge/`](specs/005-d2net-pglite-bridge/): spec.md
  with 5 clarifications resolved, plan.md, research.md (10 R-decisions),
  data-model.md, contracts/ (4 files: db-schema.sql, settings-schema.json,
  cli-contract.md, pgbridge-contract.md), quickstart.md, tasks.md (with
  in-flight remediations from `/speckit-analyse`), checklists/.

## [v2026.04.30-3] — 2026-04-30

### Documentation

- **PGLite + pg-gateway + ODBC root-cause analysis.** Documents the
  deep-dive that followed the 002-d2net-init SQLite pivot. Identifies
  PGLite's implicit-`Sync`-on-`execProtocolRaw` behaviour and the
  response-stream corruption in `pg-gateway` 0.3.0-beta.4 as the joint
  root cause of the Npgsql `ReadyForQuery while expecting
  BindCompleteMessage` and the psqlODBC `STATUS_STACK_BUFFER_OVERRUN`
  failures. Ships a working hand-rolled minimal Postgres-wire bridge
  (`docs/research/pgbridge-reference/bridge-direct.mjs`, ~150 lines) as
  a reference artefact: any future feature that wants to revive PGLite
  should start from this rather than re-introducing pg-gateway. See
  [`docs/research/pglite-pg-gateway-odbc-failure-analysis.md`](docs/research/pglite-pg-gateway-odbc-failure-analysis.md).
- No behavioural change to any shipped code path.

## [v2026.04.30-2] — 2026-04-30

### Added

- **`D2NET.Init`** — companion CLI to `D2NET.Scaffold` under
  `tools/d2net/src/D2Net.Init`. Creates a hidden `.D2NET` workspace at
  the repo root (CWD is the repo root; no walk-up to find `.git`),
  writes `D2NET-Settings.json`, and populates an embedded single-user
  SQLite database at `.D2NET/pgdb/workspace.sqlite` with five tables:
  `setting`, `excluded_directories`, `dart_files`, `phase_sequence`,
  `phase_status`. Inspection options `--list`, `--Exclusions`,
  `--current-phase` (each with TSV plain-text default and a stable
  `--json` schema). Force-delete re-init via `--FORCE
  --DELETE-EXISTING` using a temp-stage + atomic-rename pattern.
- 70 new xUnit integration tests in `tools/d2net/tests/D2Net.Init.Tests`
  — all green; `D2Net.Scaffold.Tests` (34 tests) unaffected.
- Full speckit artefact set under
  [`specs/002-d2net-init/`](specs/002-d2net-init/) — spec (with six
  recorded clarifications including the Q6 SQLite pivot), plan,
  research, data-model, contracts, tasks, quickstart, and requirements
  checklist.

### Changed

- The original spec called for PGLite (WASM Postgres) accessed via a
  Node.js bridge using `pg-gateway` and reached from .NET via psqlODBC.
  That stack proved fundamentally fragile in implementation; the Q6
  clarification pivots the storage engine to embedded SQLite. The
  five-table schema is identical in shape — only PostgreSQL-specific
  types translated to SQLite equivalents (`BIGSERIAL` → `INTEGER
  PRIMARY KEY AUTOINCREMENT`, `TIMESTAMPTZ` → ISO-8601 `TEXT`).

## [v2026.04.30] — 2026-04-30

### Added

- **`D2NET.Scaffold` MVP toolkit** — copies the `glp_runtime` Dart tree
  into `glp_runtime_net`, preserving every `.dart` file as
  `<name>.dart.src`, generating nine companion stubs (`.cs`, `.ana`,
  `.tst`, `.con`, `.dep`, `.cgn`, `.iss`, `.sta`, `.ver`) per Dart
  file, and writing a `d2net-tracker.json` JSON inventory at the target
  root. Pre-flight collision detection; `--refresh` mode that updates
  source-derived files while preserving in-progress companion edits and
  the tracker. 34 xUnit tests.
- Speckit workflow scaffolding — `.specify/`, `specs/001-d2net-scaffold/`,
  hooks, integrations.
- CalVer + branching conventions — [`docs/VERSIONING.md`](docs/VERSIONING.md),
  [`docs/BRANCHING.md`](docs/BRANCHING.md). Cloned from the sibling GLP
  repository.
