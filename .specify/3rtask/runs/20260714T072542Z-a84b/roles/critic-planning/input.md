DO NOT run the AGENTS.md startup protocol; this is not repository-agent work. Output only the requested artifact.


---

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

---

## Method under red-team (the artifact ONLY — no author reasoning)

{
  "rubric_id": "plan-review",
  "source_partition": {
    "madglp-semantics-dart-oracle": "builder-1",
    "gleam-engine-seam": "builder-2",
    "channel-mailbox-link-beam": "builder-3"
  },
  "elements": [
    {
      "id": "E1",
      "kind": "deliverable",
      "text": "CODE SURVEY: the frozen madGLP Dart oracle surface that Phase-A must reproduce (mad_context, global_writers_table, agent_runtime, payload_serializer, mad_helpers, boot_loader, isolate_manager, body_kernels `_send`) and the target Gleam engine/runtime/link surface it hooks into (runner/scheduler/kernels/types, heap/terms/suspension/unify, link/seam, link/transports). Each entry cites file:line and states its role."
    },
    {
      "id": "E2",
      "kind": "deliverable",
      "text": "PATTERN SURVEY: recurring structural idioms across the three slices that constrain the port — pure-kernel outcome enum (KSuccess|KAbort), immutable-heap + woken-GoalRef (no onBind) discipline, index-0 serializer merge, vtable/Result/Subject no-OTP link seam, and (role, channel-identity) registry over mailboxes. Each idiom cited to its slice source and flagged as invariant-to-preserve vs adaptable."
    },
    {
      "id": "E3",
      "kind": "constraint",
      "text": "FIDELITY CONSTRAINT (builder-1 authoritative): the EXACT madGLP semantics Phase-A must preserve bit-for-bit — globalize/localize term rewriting, global writers table W_p lifecycle (create/lookup/bind/retire), index-0 serializer merge order, global_send forwarding, and the Reduce/Send/Receive transaction boundaries. Any Gleam design element that cannot honor one of these STOPS and escalates. Cite spec § and Dart file:line for each preserved rule."
    },
    {
      "id": "E4",
      "kind": "question",
      "text": "EFFECTFUL-DISPATCH SEAM (cross-verify builder-1 x builder-2): WHAT must cross the kernel boundary (builder-1: the `_send` builtin's inputs/outputs, W_p mutation, message emission, ordering guarantees relative to Reduce) AND WHERE/HOW it hooks into the immutable scheduler-driven engine (builder-2: parallel effectful outcome vs widened KernelOutcome, where reduce/step/run invokes it, how the woken-GoalRef mechanism replaces onBind). The two independent answers must converge on one seam shape."
    },
    {
      "id": "E5",
      "kind": "question",
      "text": "ENGINE INTEGRATION POINTS (builder-2 authoritative): the precise reduce/step/run loop locations and type signatures where W_p / M_p / index state and the Send/Receive transactions attach without violating heap immutability. Cite runner/scheduler/kernels/types file:line and state the recommended effectful KernelOutcome type."
    },
    {
      "id": "E6",
      "kind": "question",
      "text": "DISTINGUISHED-CHANNEL REGISTRY DESIGN (builder-3 authoritative, resolves slice-3 open questions): (a) logical channel identity as first-class key over a (role, channel-identity) registry on mailboxes/Subjects; (b) physical multiplexing choice tagged-on-one-carrier vs distinct-carriers and why EITHER satisfies the contract; (c) identity-reuse-of-LinkId vs a separate channel-tag namespace; (d) stderr-equivalent = reuse fault/monitor streams vs a distinct diagnostic channel; (e) no-OTP dependency policy. Cite link/seam, transports/{loopback,tcp}, link-primitives.md, link-parity.md, gleam.toml."
    },
    {
      "id": "E7",
      "kind": "question",
      "text": "LINK-PARITY NOTE (cross-verify builder-1 x builder-3): global_send forwarding / send_to_net semantics from the madGLP oracle (builder-1: what bytes/terms leave an agent and their ordering) reconciled against the link seam + loopback/tcp parity contract (builder-3: how the channel carrier delivers them, LinkId/fault/monitor mapping). Confirm the prelude global_send/3, send_to_net/1, send_to_ui/1 map onto real channels with parity preserved."
    },
    {
      "id": "E8",
      "kind": "deliverable",
      "text": "STAGED TASK SCHEDULE A0-A4 + Phase B: A0 (glp/mad/global_name+global_writers_table+message), A1 (globalize/localize), A2 (effectful-dispatch seam + `_send`), A3 (mad_engine + Send + Receive + boot serializer), A4 (prelude global_send/3, send_to_net/1, send_to_ui/1 + multi-agent parity tests), Phase B (process-per-agent BEAM/AtomVM refactor). Every task carries: id, deps, oracle file:line, spec §, acceptance (gleam test green + parity), checkpoint boundary."
    },
    {
      "id": "E9",
      "kind": "deliverable",
      "text": "INTERFACES + CONTRACTS: concrete signatures for glp/mad/* modules, the effectful-kernel outcome type, the MadEngine run/step contract, the distinguished-channel registry interface, and the link-parity note. Each signature grounded in an oracle Dart signature (builder-1) and/or a Gleam target signature (builder-2/3) with file:line."
    },
    {
      "id": "E10",
      "kind": "rubric-criterion",
      "text": "FEASIBILITY: is each A0-A4 + Phase-B task implementable on the existing immutable Gleam engine and no-OTP link substrate without engine-core rewrites, with deps ordered so no task needs an unbuilt seam? Flag any task whose acceptance cannot be met by `gleam test` + parity as-is."
    },
    {
      "id": "E11",
      "kind": "rubric-criterion",
      "text": "COMPLETENESS: does the schedule cover every frozen madGLP semantic (E3) and every slice-3 open question (E6), with no builtin (`_send`, global_send/3, send_to_net/1, send_to_ui/1), transaction (Reduce/Send/Receive), or table (W_p/M_p/index) left unassigned to a task and a checkpoint?"
    },
    {
      "id": "E12",
      "kind": "rubric-criterion",
      "text": "RISK: where can fidelity break — index-0 merge ordering, onBind-vs-woken-GoalRef timing, effectful outcome vs immutable heap, Phase-B process-per-agent race vs single-BEAM serializer, physical-multiplexing choice leaking into logical identity? For each, name the STOP-and-escalate trigger and the checkpoint that catches it."
    }
  ],
  "questions": [
    {
      "for": "builder-1",
      "q": "From docs/ma/madGLP-spec.md and the corpus (10/11/00/02) + glp-runtime-spec.txt, quote verbatim (with §) the definitions of globalize and localize, and the W_p global writers table lifecycle. What EXACTLY must the Gleam port reproduce? Cite spec §.",
      "cite": "spec § + verbatim quote required"
    },
    {
      "for": "builder-1",
      "q": "From mad_context.dart / global_writers_table.dart / payload_serializer.dart, give file:line for: W_p create/lookup/bind/retire, the index-0 serializer merge, and the merge ordering guarantee. State each as a preserve-exactly rule.",
      "cite": "file:line required"
    },
    {
      "for": "builder-1",
      "q": "From body_kernels.dart and agent_runtime.dart, give the exact `_send` builtin contract: inputs, outputs, side effects on W_p/M_p, and its ordering relative to the Reduce transaction. What crosses the kernel boundary? (feeds E4)",
      "cite": "file:line required"
    },
    {
      "for": "builder-1",
      "q": "From mad_helpers.dart / agent_runtime.dart / boot_loader.dart, define the Reduce/Send/Receive transactions and global_send forwarding. What bytes/terms leave an agent on global_send, and in what order? (feeds E7)",
      "cite": "file:line required"
    },
    {
      "for": "builder-2",
      "q": "From runner.gleam / scheduler.gleam / kernels.gleam / types.gleam, describe the pure-kernel outcome type (KSuccess|KAbort) and the reduce/step/run loop. Give file:line for the exact points where an effectful madGLP dispatch would hook in. (feeds E4/E5)",
      "cite": "file:line required"
    },
    {
      "for": "builder-2",
      "q": "From heap.gleam / suspension.gleam / unify.gleam, explain how heap immutability and woken-GoalRef reactivation replace Dart's onBind. How do W_p/M_p/index and Send/Receive attach without mutating the heap in place? Cite file:line.",
      "cite": "file:line required"
    },
    {
      "for": "builder-2",
      "q": "Recommend the effectful-dispatch seam shape: a parallel effectful outcome channel vs widening KernelOutcome. Give the concrete Gleam type signature and the run/step contract for a MadEngine wrapper. Justify against types.gleam/runner.gleam file:line. (feeds E4/E5/E9)",
      "cite": "file:line + proposed signature required"
    },
    {
      "for": "builder-2",
      "q": "From parser.gleam / term_codec.gleam, what parse/encode surface must A0-A1 (global_name/message, globalize/localize) reuse or extend? Cite file:line and flag any gap needing a new construct (propose-first, do not assume approval).",
      "cite": "file:line required"
    },
    {
      "for": "builder-3",
      "q": "From link/seam/*.gleam and transports/{loopback,tcp}.gleam, describe the current link seam (vtable/Result/Subject, LinkId, fault/monitor streams). Give file:line. How does a (role, channel-identity) registry layer onto it? (feeds E6)",
      "cite": "file:line required"
    },
    {
      "for": "builder-3",
      "q": "Resolve slice-3 open questions with citations: (a) identity-reuse-of-LinkId vs separate channel-tag namespace; (b) stderr-equivalent = fault/monitor reuse vs distinct diagnostic channel; (c) physical multiplexing tagged-on-one-carrier vs distinct-carriers — show why EITHER satisfies the contract. Cite link-primitives.md § and link-parity.md §.",
      "cite": "spec § + file:line required"
    },
    {
      "for": "builder-3",
      "q": "From gleam.toml, state the current dependency set and confirm the no-OTP policy for Phase A. For Phase B process-per-agent, what BEAM/AtomVM process/mailbox/Subject primitives are needed and which introduce OTP deps? Cite gleam.toml + link-parity.md.",
      "cite": "file:line + spec § required"
    },
    {
      "for": "builder-3",
      "q": "From link-parity.md and transports/{loopback,tcp}.gleam, how do global_send/send_to_net/send_to_ui map onto channels, and how is byte/ordering parity with the Dart oracle asserted in tests? (cross-verify with builder-1 E7)",
      "cite": "spec § + file:line required"
    }
  ]
}
