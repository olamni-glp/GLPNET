## [Unreleased]

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
