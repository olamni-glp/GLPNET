Roadmap status
==============

Epic: codeconv post-v1 capability enhancements (codeconv-postv1--01kwjbhv)
    [released   ] #47  semantic-tombstone-enrichment  WSJF=2.00 RICE=480.00  — Semantic tombstone enrichment  [delivered; overlap≈ codeconv-gleam-langpair, depgraph-cross-run-trends, depgraph-mark-and-recompute, multi-protocol-link-layer; spec: specs/035-semantic-tombstone-enrichment]
    [refined    ] #35  depgraph-cross-run-trends--01kwjbhv  WSJF=2.00 RICE=400.00  — depgraph cross-run trend reporting  [parallel-safe; overlap≈ depgraph-mark-and-recompute--01kwjbhv, multi-protocol-link-layer--01kwjbhv, semantic-tombstone-enrichment--01kwf6tg]
    [refined    ] #23  depgraph-mark-and-recompute--01kwjbhv  WSJF=2.33 RICE=337.50  — depgraph mark-and-recompute convenience subcommand  [parallel-safe; overlap≈ depgraph-cross-run-trends--01kwjbhv, multi-protocol-link-layer--01kwjbhv, semantic-tombstone-enrichment--01kwf6tg]

Epic: complete specified-but-unimplemented GLP runtime features (glp-runtime-gaps--01kwjbhv)
    [released   ] #13  comparison-guards--01kwjbhv  WSJF=3.25 RICE=1200.00  — Comparison guards  [delivered; overlap≈ abandon-operation--01kwjbhv, multi-protocol-link-layer--01kwjbhv; spec: docs/guards-reference.md#comparison-guards]
    [refined    ] #51  nested-structure-head-matching--01kwjbhv  WSJF=1.23 RICE=173.08  — Nested-structure matching in HEAD phase  [parallel-safe]
    [refined    ] #43  abandon-operation--01kwjbhv  WSJF=1.62 RICE=125.00  — Abandon operation (FCP-exact)  [parallel-safe; overlap≈ comparison-guards--01kwjbhv, multi-protocol-link-layer--01kwjbhv]
    [refined    ] #45  zmq-comm-base--01kwjbhv  WSJF=1.46 RICE=215.38  — ZMQ base comm primitives (zmq-receiver-base + zmq-sender-base)  [parallel-safe; blocked-by: multi-protocol-link-layer--01kwjbhv]

Epic: Tutorials (tutorials--01kwjbhv)
    [released   ] #50  glptutorial-run--01kwjbhv  WSJF=1.23 RICE=307.69  — /glptutorial-run — select, run & explain a REPL tutorial  [delivered; overlap≈ glptutorial-list--01kwjbhv, multi-protocol-link-layer--01kwjbhv; spec: specs/023-glptutorial-run]
    [released   ] #9   glptutorial-list--01kwjbhv  WSJF=3.33 RICE=850.00  — /glptutorial-list — list tutorials & scripts with descriptions  [delivered; overlap≈ glptutorial-run--01kwjbhv, multi-protocol-link-layer--01kwjbhv; spec: specs/022-glptutorial-list]

Epic: Distributed GLP connectivity (distributed-glp-connectivity--01kwjbhv)
    [released   ] #-   multi-protocol-link-layer--01kwjbhv  WSJF=— RICE=—  — Multi-protocol peer-to-peer link layer for distributed GLP  [delivered; blocked-by: marathon-stage-harness--01kwjbhv; overlap≈ abandon-operation--01kwjbhv, comparison-guards--01kwjbhv, depgraph-cross-run-trends--01kwjbhv, depgraph-mark-and-recompute--01kwjbhv, glptutorial-list--01kwjbhv, glptutorial-run--01kwjbhv, semantic-tombstone-enrichment--01kwf6tg; spec: specs/025-multi-protocol-link-layer]
    [released   ] #-   marathon-stage-harness--01kwjbhv  WSJF=— RICE=—  — Marathon stage harness — durable, restart-safe workflow backing for long multi-stage features  [delivered; spec: specs/024-marathon-stage-harness]
    [closed     ] #-   http3-quic-ws-link-full-acceptance--01kwjbhv  WSJF=— RICE=—  — 036 HTTP3-QUIC-WS — deferred full acceptance (Profile C quicer NIF, two-host LAN e2e, marathon durability)  [delivered]
    [promoted   ] #-   qr-link-provisioning  WSJF=4.00 RICE=252.00  — QR-code link + cert provisioning via generated PDF or hub display page  [parallel-safe]
    [promoted   ] #-   glp-native-true-quic-link  WSJF=2.88 RICE=112.50  — GLP-native true-QUIC link — genuine GLP over the wire, driven entirely by GLP programs  [parallel-safe]

Epic: Separation of REPL Front-end from Engine Execution & Scheduler (epic-separation-of-repl-front-end-from-engine-execution-scheduler--01kwjbhv)
    [closed     ] #29  repl-engine-split-mvp-binary-wire-format-intermediate-language-c--01kwjbhv  WSJF=2.00 RICE=2076.92  — REPL/engine split MVP — binary wire-format intermediate language (C#)  [delivered]
    [closed     ] #3   engine-review-and-design-dossier  WSJF=5.80 RICE=9600.00  — Engine review + refactoring design dossier  [delivered; spec: specs/026-engine-review-dossier]
    [released   ] #7   result-envelope-and-deep-resolve--01kwjbhv  WSJF=3.60 RICE=2250.00  — Self-contained result envelope + server-side deep-resolve  [delivered]
    [released   ] #5   structured-output-capture-seam--01kwjbhv  WSJF=3.60 RICE=2400.00  — Structured output capture seam  [delivered]
    [released   ] #4   il-codec-spike--01kwjbhv  WSJF=5.20 RICE=3000.00  — IL/bytecode round-trip codec spike  [delivered; spec: specs/029-il-codec-spike]
    [refined    ] #11  repl-engine-process-split-mvp--01kwjbhv  WSJF=3.25 RICE=4500.00  — REPL/engine two-process split MVP (TCP loopback)  [blocked-by: result-codec-and-framecodec-ride--01kwjbhv]
    [refined    ] #26  engine-state-snapshot-and-persistence-api--01kwjbhv  WSJF=2.25 RICE=1800.00  — Engine-state snapshot + persistence API  [blocked-by: repl-engine-process-split-mvp--01kwjbhv]
    [refined    ] #41  liveness-crash-restart-host--01kwjbhv  WSJF=1.62 RICE=750.00  — Liveness + crash-signal + supervised-restart host  [blocked-by: engine-state-snapshot-and-persistence-api--01kwjbhv]
    [refined    ] #22  restore-and-resume-with-link-reestablish--01kwjbhv  WSJF=2.38 RICE=1250.00  — Restore-and-resume with link re-establish  [blocked-by: liveness-crash-restart-host--01kwjbhv]
    [refined    ] #28  multi-accept-transport-extension--01kwjbhv  WSJF=2.20 RICE=1800.00  — Multi-accept TCP transport extension  [parallel-safe]
    [refined    ] #31  compiled-il-on-the-wire-and-factor-out-compiler--01kwjbhv  WSJF=2.00 RICE=1875.00  — Compiled-IL-on-the-wire + factor out compiler  [parallel-safe]
    [refined    ] #20  antlr4-shared-grammar-spike--01kwjbhv  WSJF=2.40 RICE=640.00  — ANTLR4 shared-grammar multi-target spike  [parallel-safe]
    [refined    ] #40  multi-client-control-program-in-glp--01kwjbhv  WSJF=1.62 RICE=1125.00  — Multi-client control program written in GLP  [parallel-safe]
    [refined    ] #38  cpp-engine-feasibility--01kwjbhv  WSJF=1.80 RICE=420.00  — C++ engine+scheduler+compiler feasibility spike  [parallel-safe]
    [refined    ] #48  many-instances-shared-static-memory-cooperative-scheduling--01kwjbhv  WSJF=1.38 RICE=450.00  — Many instances: shared-static memory + cooperative scheduling  [parallel-safe]
    [refined    ] #17  research-programme-and-llvm-feasibility--01kwjbhv  WSJF=3.00 RICE=533.33  — Research programme + LLVM feasibility (staged)  [parallel-safe]
    [released   ] #-   iterative-refinement-and-verification-framework--01kwjbhv  WSJF=— RICE=—  — Iterative refinement & verification framework (GEPA/DSPy + formal + pragmatic)  [delivered]

Epic: marathon (epic-marathon--01kwjbhv)
    [released   ] #-   marathon-refinement--01kwjbhv  WSJF=— RICE=—  — marathon refinement  [delivered; spec: specs/030-marathon-refinement]

Epic: Gleam AtomVM (gleam-atomvm)
    [released   ] #2   gleam-port-source-and-toolchain-spike  WSJF=6.80 RICE=480.00  — Gleam port source + toolchain/AtomVM feasibility spike  [delivered]
    [released   ] #14  codeconv-gleam-langpair  WSJF=4.20 RICE=256.00  — codeconv Gleam langpair  [delivered; blocked-by: gleam-port-source-and-toolchain-spike; overlap≈ depgraph-cross-run-trends, depgraph-mark-and-recompute, multi-protocol-link-layer, semantic-tombstone-enrichment; spec: specs/032-codeconv-gleam-langpair]
    [released   ] #6   glp-gleam-subtree-scaffold  WSJF=5.33 RICE=180.00  — glp_gleam subtree scaffold  [delivered; blocked-by: gleam-port-source-and-toolchain-spike; overlap≈ cross-runtime-csharp-gleam-distributed-tests, glp-gleam-bytecode-runner, glp-gleam-compiler-and-loader, glp-gleam-link-layer, glp-gleam-repl, glp-test-corpus-port-and-runner; spec: specs/033-glp-gleam-subtree-scaffold]
    [released   ] #18  glp-gleam-core-terms-and-heap  WSJF=3.62 RICE=262.50  — glp_gleam core terms + heap + unification  [delivered; blocked-by: codeconv-gleam-langpair, glp-gleam-subtree-scaffold; overlap≈ cross-runtime-csharp-gleam-distributed-tests, glp-gleam-compiler-and-loader, glp-gleam-link-layer, glp-gleam-repl, glp-test-corpus-port-and-runner; spec: specs/034-glp-gleam-core-terms-and-heap]

Epic: durable-mesh-messaging-protocol (epic-durable-mesh-messaging-protocol)
    [closed     ] #-   durable-mesh-messaging-protocol-prototype  WSJF=— RICE=—  — durable-mesh-messaging-protocol-prototype  [delivered]

Epic: HTTP3-QUIC-channel-and-WS-link (epic-http3-quic-channel-and-ws-link)
    [closed     ] #-   http3-quic-ws-channel-link-proto--01kwjbhv  WSJF=— RICE=—  — HTTP3-QUIC-WS-Channel-Link-proto  [delivered]

Epic: Full Gleam implementation (full-gleam)
    [released   ] #30  result-codec-and-framecodec-ride  WSJF=3.00 RICE=1680.00  — Result-envelope codec over FrameCodec/TcpTransport  [delivered; blocked-by: il-codec-spike; spec: specs/038-result-codec-and-framecodec-ride]
    [refined    ] #52  glp-gleam-bytecode-runner  WSJF=2.00 RICE=138.46  — glp_gleam bytecode runner/engine  [blocked-by: glp-gleam-compiler-and-loader, glp-gleam-core-terms-and-heap; overlap≈ cross-runtime-csharp-gleam-distributed-tests, glp-gleam-link-layer, glp-gleam-subtree-scaffold]
    [refined    ] #53  glp-gleam-compiler-and-loader  WSJF=2.00 RICE=138.46  — glp_gleam compiler + loader  [blocked-by: antlr4-shared-grammar-spike; overlap≈ comparison-guards, cross-runtime-csharp-gleam-distributed-tests, glp-gleam-core-terms-and-heap, glp-gleam-link-layer, glp-gleam-subtree-scaffold, multi-protocol-link-layer]
    [refined    ] #12  glp-gleam-repl  WSJF=4.60 RICE=280.00  — glp_gleam REPL (standalone Gleam GLP instance)  [blocked-by: glp-gleam-bytecode-runner, glp-gleam-compiler-and-loader, result-envelope-and-deep-resolve, structured-output-capture-seam; overlap≈ cross-runtime-csharp-gleam-distributed-tests, glp-gleam-core-terms-and-heap, glp-gleam-subtree-scaffold]
    [refined    ] #16  glp-test-corpus-port-and-runner  WSJF=3.80 RICE=256.00  — Shared test corpus ported to Gleam  [blocked-by: glp-gleam-bytecode-runner, glp-gleam-compiler-and-loader, glp-gleam-repl; overlap≈ glp-gleam-core-terms-and-heap, glp-gleam-link-layer, glp-gleam-subtree-scaffold]
    [refined    ] #62  glp-gleam-link-layer  WSJF=1.23 RICE=55.38  — glp_gleam multi-protocol link layer  [blocked-by: glp-gleam-repl, m2-0-verify-erlang-monitor-atomvm, result-codec-and-framecodec-ride; overlap≈ abandon-operation, comparison-guards, glp-gleam-bytecode-runner, glp-gleam-compiler-and-loader, glp-gleam-core-terms-and-heap, glp-gleam-subtree-scaffold, glp-test-corpus-port-and-runner, multi-protocol-link-layer]
    [refined    ] #10  cross-runtime-csharp-gleam-distributed-tests  WSJF=4.80 RICE=420.00  — Cross-runtime C#<->Gleam distributed tests  [blocked-by: glp-gleam-link-layer, glp-test-corpus-port-and-runner, multi-protocol-link-layer; overlap≈ glp-gleam-bytecode-runner, glp-gleam-compiler-and-loader, glp-gleam-core-terms-and-heap, glp-gleam-repl, glp-gleam-subtree-scaffold, zmq-comm-base]
    [promoted   ] #-   gleam-implementation-combined-full-gleam-feature  WSJF=— RICE=—  — GLEAM implementation — combined Full-Gleam feature
    [captured   ] #-   full-scope-gleam-glp-implementation  WSJF=— RICE=—  — Full-scope Gleam GLP implementation  [blocked-by: gleam-implementation-combined-full-gleam-feature]

Epic: codeconv Dart->C# conversion toolchain (v1) (epic-codeconv-dart-c-conversion-toolchain-v1)
    [released   ] #-   prereq-patterns-catalog--01kwf3zq  WSJF=— RICE=—  — Prereq patterns catalog  [delivered; spec: specs/011-prereq-patterns-catalog]
    [released   ] #-   codeconv-runner--01kwf3zw  WSJF=— RICE=—  — codeconv runner  [delivered; spec: specs/012-codeconv-runner]
    [released   ] #-   package-self-import-resolution--01kwf401  WSJF=— RICE=—  — Package self-import resolution  [delivered; spec: specs/014-package-self-import-resolution]
    [released   ] #-   codeconv-depgraph--01kwf406  WSJF=— RICE=—  — codeconv depgraph  [delivered; spec: specs/015-codeconv-depgraph]
    [released   ] #-   codeconv-init-scaffold-langpair--01kwf40b  WSJF=— RICE=—  — codeconv init/scaffold + langpair (D2NET removal)  [delivered; spec: specs/016-codeconv-init-scaffold-langpair]
    [released   ] #-   conversion-plan-agents--01kwf40g  WSJF=— RICE=—  — Conversion plan agents  [delivered; spec: specs/017-conversion-plan-agents]
    [released   ] #-   codeconv-builder--01kwf40n  WSJF=— RICE=—  — codeconv builder  [delivered; spec: specs/018-codeconv-builder]
    [released   ] #-   codeconv-codegen--01kwf40t  WSJF=— RICE=—  — codeconv codegen  [delivered; spec: specs/019-codeconv-codegen]
    [released   ] #-   trace-equivalence-fidelity--01kwf410  WSJF=— RICE=—  — Trace equivalence and fidelity  [delivered; spec: specs/020-trace-equivalence-fidelity]
    [released   ] #-   data-dir-override--01kwf4tk  WSJF=— RICE=—  — --data-dir override for PGLite cluster  [delivered]

Epic: GLP Gleam port (epic-glp-gleam-port)
    [closed     ] #-   gleam-port-spike  WSJF=— RICE=—  — Gleam port spike  [delivered]
    [released   ] #-   glp-gleam-baseline-program  WSJF=— RICE=—  — GLP Gleam baseline program  [delivered]
    [released   ] #-   m2-0-verify-erlang-monitor-atomvm--01kwf41x  WSJF=— RICE=—  — M2.0 verify Erlang monitor on AtomVM  [delivered]

Epic: Repo infrastructure and process (epic-repo-infrastructure-and-process)
    [released   ] #-   pglite-bridge-rca--01kwf3w2  WSJF=— RICE=—  — PGLite bridge root-cause analysis  [delivered]
    [released   ] #-   changelog-checkpoint--01kwf422  WSJF=— RICE=—  — CHANGELOG checkpoint  [delivered]
    [released   ] #-   buildkit-gitflow-adoption--01kwf427  WSJF=— RICE=—  — buildkit GitFlow adoption  [delivered]

Epic: D2NET init/scaffold toolchain (pre-codeconv foundation) (d2net-toolchain)
    [closed     ] #-   d2net-scaffold  WSJF=— RICE=—  — D2NET.Init scaffold MVP + CalVer branching  [delivered; spec: specs/001-d2net-scaffold]
    [closed     ] #-   d2net-init  WSJF=— RICE=—  — D2NET.Init companion command  [delivered; spec: specs/002-d2net-init]
    [closed     ] #-   d2net-pglite-bridge  WSJF=— RICE=—  — D2NET.Init SQLite->PGLite WASM direct bridge  [delivered; spec: specs/005-d2net-pglite-bridge]
    [closed     ] #-   d2net-init-skill  WSJF=— RICE=—  — C:/Program Files/Git/D2NET-init skill + --non-interactive init-only guard  [delivered; spec: specs/006-d2net-init-skill]
    [closed     ] #-   incremental-exclusions  WSJF=— RICE=—  — D2NET.Init --add-exclude incremental exclusions  [delivered; spec: specs/007-incremental-exclusions]
    [closed     ] #-   remove-exclude  WSJF=— RICE=—  — D2NET.Init --remove-exclude + --allow-system-exclusions  [delivered; spec: specs/008-remove-exclude]
    [closed     ] #-   scaffold-mirror  WSJF=— RICE=—  — D2NET.Scaffold source-tree mirror (per-dart workdirs)  [delivered; spec: specs/009-scaffold-mirror]
    [closed     ] #-   scaffold-skill  WSJF=— RICE=—  — C:/Program Files/Git/D2NET-scaffold skill + validation  [delivered; spec: specs/010-scaffold-skill]

Epic: HTTP3/QUIC/WS distributed channel-link (prototype line) (http3-quic-ws-link)
    [released   ] #-   virtual-3270-term  WSJF=— RICE=—  — Virtual IBM-3270 block-mode terminal UI over the QUIC/WS link  [delivered; blocked-by: rcopy-file-transfer-service; spec: specs/037-virtual-3270-term]
    [captured   ] #-   durable-mesh-messaging-protocol  WSJF=— RICE=—  — Durable mesh messaging protocol (signal-then-fetch, WAL/PGLite tiering)  [parallel-safe]
    [released   ] #-   rcopy-file-transfer-service  WSJF=— RICE=—  — Virtual 3270 terminal — revisit, harden, TOTALLY COMPLETE (definitive; legacy slug rcopy-file-transfer-service)  [delivered; spec: specs/040-rcopy-file-transfer-service]
    [captured   ] #-   http3-quic-ws-link-completion  WSJF=— RICE=—  — HTTP3/QUIC+WS link completion — live glp_repl bridge, mesh fix, build+re-verify  [parallel-safe; blocked-by: http3-quic-ws-channel-link-proto]

Epic: CRDT multi-format messaging (crdt-multiformat-messaging)
    [released   ] #-   crdtmsg-priorart-sibling-scan  WSJF=— RICE=—  — Prior-art scan: sibling-repo corpus (3-role team)  [delivered]
    [released   ] #-   crdtmsg-webresearch-corpus  WSJF=— RICE=—  — Web-research corpus: schema languages, encodings, CRDT (3-role team)  [delivered]
    [released   ] #-   crdtmsg-buildingblocks-synthesis  WSJF=— RICE=—  — Consolidated building-blocks synthesis (3-role team)  [delivered; blocked-by: crdtmsg-priorart-sibling-scan, crdtmsg-webresearch-corpus]
    [specified  ] #-   crdtmsg-mvp  WSJF=— RICE=—  — CRDT multi-format messaging MVP  [parallel-safe; blocked-by: crdtmsg-buildingblocks-synthesis; spec: specs/041-crdtmsg-mvp]
    [released   ] #-   crdtmsg-verify-and-harden  WSJF=— RICE=—  — Verify + harden F1/F2/F3 against their own 3-role method specs  [delivered; blocked-by: crdtmsg-buildingblocks-synthesis, crdtmsg-priorart-sibling-scan, crdtmsg-webresearch-corpus]
    [released   ] #-   crdtmsg-xsd-style-schema-language  WSJF=— RICE=—  — Higher-level XML-Schema-style schema language over the functor registry  [delivered; blocked-by: crdtmsg-mvp]

Epic: Roadmap sweep 2026-07 consolidated waves (epic-roadmap-sweep-2026-07-consolidated-waves)
    [closed     ] #-   wave-1-consolidated-glp-policy-guard-http3-quic-ws-link-full-acceptance  WSJF=— RICE=—  — Wave 1 consolidated: GLP policy-guard + HTTP3-QUIC-WS link full acceptance  [delivered]
    [captured   ] #-   wave-2-consolidated-repl-engine-split-spine  WSJF=— RICE=—  — Wave 2 consolidated: REPL engine split spine  [blocked-by: wave-1-consolidated-glp-policy-guard-http3-quic-ws-link-full-acceptance]
    [captured   ] #-   wave-3-consolidated-full-gleam-chain  WSJF=— RICE=—  — Wave 3 consolidated: Full Gleam chain  [blocked-by: wave-2-consolidated-repl-engine-split-spine]
    [captured   ] #-   wave-4-consolidated-parallel-safe-fillers  WSJF=— RICE=—  — Wave 4 consolidated: parallel-safe fillers  [blocked-by: wave-3-consolidated-full-gleam-chain]
    [captured   ] #-   wave-5-consolidated-captured-triad  WSJF=— RICE=—  — Wave 5 consolidated: captured triad  [blocked-by: wave-4-consolidated-parallel-safe-fillers]

Epic: YNET overlay — deferred BUILD-NEW gaps (epic-ynet-overlay-deferred-build-new-gaps)
    [captured   ] #-   ynet-human-memorable-decentralized-naming-resolver  WSJF=— RICE=—  — YNET human-memorable decentralized-naming resolver  [parallel-safe]
    [captured   ] #-   ynet-mobile-background-battery-budget-scheduling-policy  WSJF=— RICE=—  — YNET mobile background/battery-budget scheduling policy  [parallel-safe]

Standalone features:
    [released   ] #-   evidence-based-constitution--01kwjbhv  WSJF=— RICE=—  — evidence-based-constitution  [delivered; spec: specs/028-evidence-based-constitution]
    [closed     ] #-   038-rcopy-file-service  WSJF=— RICE=—  — C:/Program Files/Git/rcopy peer-to-peer file-transfer service + registry  [delivered]
    [captured   ] #-   three-role-agent-team-orchestration  WSJF=— RICE=—  — Formal 3-role agent-team orchestration (planning + execution triads)  [parallel-safe]
    [closed     ] #-   glp-policy-guard  WSJF=— RICE=—  — GLP policy-guard implementation (§1.14 gate)  [delivered]
    [promoted   ] #-   durable-listener-service-box  WSJF=— RICE=—  — durable-listener-service-box  [parallel-safe]

Recommended build order: abandon-operation--01kwjbhv → antlr4-shared-grammar-spike--01kwjbhv → compiled-il-on-the-wire-and-factor-out-compiler--01kwjbhv → cpp-engine-feasibility--01kwjbhv → crdtmsg-mvp → depgraph-cross-run-trends--01kwjbhv → depgraph-mark-and-recompute--01kwjbhv → durable-listener-service-box → durable-mesh-messaging-protocol → gleam-implementation-combined-full-gleam-feature → glp-gleam-compiler-and-loader → glp-native-true-quic-link → http3-quic-ws-link-completion → many-instances-shared-static-memory-cooperative-scheduling--01kwjbhv → multi-accept-transport-extension--01kwjbhv → multi-client-control-program-in-glp--01kwjbhv → nested-structure-head-matching--01kwjbhv → qr-link-provisioning → repl-engine-process-split-mvp--01kwjbhv → research-programme-and-llvm-feasibility--01kwjbhv → three-role-agent-team-orchestration → wave-2-consolidated-repl-engine-split-spine → ynet-human-memorable-decentralized-naming-resolver → ynet-mobile-background-battery-budget-scheduling-policy → zmq-comm-base--01kwjbhv → engine-state-snapshot-and-persistence-api--01kwjbhv → full-scope-gleam-glp-implementation → glp-gleam-bytecode-runner → wave-3-consolidated-full-gleam-chain → glp-gleam-repl → liveness-crash-restart-host--01kwjbhv → wave-4-consolidated-parallel-safe-fillers → glp-gleam-link-layer → glp-test-corpus-port-and-runner → restore-and-resume-with-link-reestablish--01kwjbhv → wave-5-consolidated-captured-triad → cross-runtime-csharp-gleam-distributed-tests
Parallel-safe (no hard constraints): abandon-operation--01kwjbhv, antlr4-shared-grammar-spike--01kwjbhv, compiled-il-on-the-wire-and-factor-out-compiler--01kwjbhv, cpp-engine-feasibility--01kwjbhv, crdtmsg-mvp, depgraph-cross-run-trends--01kwjbhv, depgraph-mark-and-recompute--01kwjbhv, durable-listener-service-box, durable-mesh-messaging-protocol, glp-native-true-quic-link, http3-quic-ws-link-completion, many-instances-shared-static-memory-cooperative-scheduling--01kwjbhv, multi-accept-transport-extension--01kwjbhv, multi-client-control-program-in-glp--01kwjbhv, nested-structure-head-matching--01kwjbhv, qr-link-provisioning, research-programme-and-llvm-feasibility--01kwjbhv, three-role-agent-team-orchestration, ynet-human-memorable-decentralized-naming-resolver, ynet-mobile-background-battery-budget-scheduling-policy, zmq-comm-base--01kwjbhv
