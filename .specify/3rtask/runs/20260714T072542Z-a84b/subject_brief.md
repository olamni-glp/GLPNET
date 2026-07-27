# Subject brief — plan

- subject: madGLP->Gleam Phase-A port + distinguished-mailbox-channel I/O seam: design analysis, interfaces/contracts, and A0-A4 + Phase-B task schedule for feature 050 US4/T050 (anchors: docs/ma/madGLP-spec.md; specs/050-full-gleam-combined; memory 050-madglp-gleam-port-A-then-B; DECIDED: logical channel identity first-class, physical multiplexing either)
- rubric: plan-review
- lenses: feasibility | completeness | risk
- brief rule: size-invariant: the goal statement + the constraint-document list — never pasted document bodies
- cross-verify: a plan element is promoted only when independently derived or confirmed from a disjoint constraint slice by another blind Builder

## Evidence slices (names only — each blind role sees ONLY its own)

- madglp-semantics-dart-oracle: FIDELITY LENS: the frozen madGLP semantics and their Dart oracle. What must the Gleam Phase-A port preserve EXACTLY (globalize/localize, global writers table W_p lifecycle, index-0 serializer merge, global_send forwarding, Reduce/Send/Receive transactions, the _send builtin)? Cite spec sections and Dart file:line. Fidelity anchor = Shapiro Dart + papers; GHC fallback where GLP spec has gaps.
- gleam-engine-seam: ENGINE-INTEGRATION LENS: the immutable, scheduler-driven Gleam engine. WHERE and HOW does an effectful madGLP layer hook in? Characterize the pure kernel outcome (KSuccess|KAbort), the reduce/step/run drive loop, heap immutability (no onBind; reactivation via woken GoalRefs), and how W_p/M_p/index state + Send/Receive attach. Recommend the effectful-dispatch seam shape (parallel effectful outcome vs widened KernelOutcome). Cite Gleam file:line.
- channel-mailbox-link-beam: CHANNEL/BEAM MAPPING LENS: the distinguished-mailbox-channel I/O model (DECIDED: logical channel identity is first-class; physical multiplexing may be EITHER tagged-on-one-carrier OR distinct-carriers) and the BEAM/AtomVM process substrate for Phase B. How do link seam + loopback/tcp transports + LinkId + fault/monitor streams map onto a (role, channel-identity) registry over mailboxes/Subjects? Resolve: identity reuse-LinkId vs separate channel-tag namespace; stderr-equivalent = fault/monitor vs distinct diagnostic. No-OTP dep policy. Cite Gleam/contract file:line.
