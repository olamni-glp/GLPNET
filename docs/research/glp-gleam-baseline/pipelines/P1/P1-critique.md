# Review: Three Gleam/AtomVM Baseline Approaches

All three converge on the same correct spine (F5 runner → F6 compiler/loader → F7 REPL → F8 corpus → F9 link → F10 cross-runtime; supersede-by-BEAM for host/transport/multi-accept/scheduling; fault-as-data via `monitor` not `link`/`EXIT`; raw `erlang:spawn`, no `gleam_otp`; `goal_id` dedupe + 034 fixes; local-only deref; distributed-bind-as-local-assign; `known/1` globalize/localize). They differ on three hinges: **does M1 split into processes**, **how hard is C#-contract reuse held**, and **is M2 frame-envelope parity pinned**.

---

## Approach 1 — gleam-native

- **(a) Aim — 8:** Correctly deletes C# host/transport/codec machinery, but imports the engine-separation *process split* into M1 — scope the owner's M1 (a single combined instance, in-process in Dart/C#) didn't ask for.
- **(b) Faithfulness — 7:** Strong on local-deref and TLV byte-parity; vague on the M2 frame envelope (CRC/version/fragmentation), and the two-process M1 adds a deep-resolve/message-copy hazard that buys no execution-semantics fidelity.
- **(c) Feasibility — 8:** Sound AtomVM handling; asserts `erlang:monitor` works on AtomVM — unverified by the ground docs.
- **(d) Speed — 7:** Two-process M1 + deep-resolve + a supervisor are real work off the M1 critical path.
- **(e) Risk — 7:** Heap-index-leak-over-Subject risk; dropping FrameCodec outright may bite at #5.
- **Strongest idea:** clearest articulation that BEAM supplies the C# epic's hand-built layers for free.
- **Worst flaw:** treats the REPL/engine process split as "native and therefore free," front-loading it into M1 where in-process is faster and more faithful to the reference topology.

## Approach 2 — contract-reuse

- **(a) Aim — 7:** The interop-spine framing serves M2 well, but leans on C# as the contract oracle when the ratified port source is **Dart** (A2) — and Dart↔C# aren't at parity, so the "reference contract" is itself contested, a reconciliation not a reuse.
- **(b) Faithfulness — 9:** Best M2 rigor: the only approach that explicitly *rides the 025 FrameCodec contract* (frame envelope) **on top of** TLV byte-parity. For the #5 C#↔Gleam gate you need both the term format and the frame envelope to match — this is the one approach that nails it.
- **(c) Feasibility — 7:** Same BEAM substrate, but insisting on C#-shaped serializable contracts adds friction where BEAM idioms differ.
- **(d) Speed — 6:** Slowest to M1 — specifies the envelope as a serialize-later contract and splits processes even in-node; ceremony before evidence.
- **(e) Risk — 6:** Over-fitting to C# contract shapes; the contested-oracle problem.
- **Strongest idea:** pinning FrameCodec **and** TLV as the byte-parity spine so a Gleam back-end and C# back-end are wire-compatible by construction.
- **Worst flaw:** front-loads serialization/contract ceremony into M1 and elevates C# to oracle, against the Dart-ratified ground.

## Approach 3 — parity-first

- **(a) Aim — 9:** Evidence-as-definition ("corpus-green = M1; byte-identical split = M2") maps exactly onto the owner's "faithful = identical observable semantics." Keeps M1 in-process, mirroring the reference topology; cleanest scope discipline.
- **(b) Faithfulness — 8:** Rigorous M1 gate and a disciplined M2 evidence order; but it *drops* #15 and underspecifies the M2 frame envelope — vaguer than #2 exactly where #2 is strongest.
- **(c) Feasibility — 8:** Most careful AtomVM hedging (OTP on BEAM / raw-spawn fault-monitor on AtomVM).
- **(d) Speed — 9:** Fewest moving parts; in-process M1 (no wire, no envelope codec, no deep-resolve) is the fastest route to a runnable, certified single instance.
- **(e) Risk — 8:** Lowest M1 risk; main exposure is the M2 framing-parity gap surfacing late at #5, plus hard-dropping (not deferring) persistence.
- **Strongest idea:** the test corpus *is* the definition of done — parity evidence, not feature count, defines the baseline.
- **Worst flaw:** dropping FrameCodec/frame-envelope parity entirely, leaving #5 cross-runtime framing underspecified.

---

## Ranked Verdict

**1st — Approach 3 (parity-first).** Best aim, speed, and risk; its evidence-gated definition and in-process M1 are the fastest faithful route. Its one gap (M2 framing) is narrow and patchable.

**2nd — Approach 2 (contract-reuse).** Edges out #1 on the strength of its load-bearing, uniquely-emphasized M2 insight (FrameCodec **+** TLV byte-parity), which a synthesis must absorb — outweighing #1's marginal M1-speed edge. Held back by M1 ceremony and the C#-oracle drift.

**3rd — Approach 1 (gleam-native).** Right instincts, but its distinctive "BEAM gives it free" claim is shared by all three, and it over-applies it by building the M1 process split as scope; drops FrameCodec like #3 without #3's discipline.

---

## Synthesis: SHOULD ADOPT

- **From #3:** parity-evidence-as-definition (corpus-green = M1; byte-identical split + cross-runtime round-trip = M2); **in-process M1** (REPL calls engine directly, mirroring the Dart/C# reference — fold the result-envelope field-set in as the engine's *return type*, not a wire).
- **From #2:** the **FrameCodec frame-envelope contract (CRC/version/fragmentation) plus TLV term byte-parity** as the M2/#5 wire spine, validated against the same adversarial corpus.
- **From #1:** deep-resolve (#11) and output-capture (#10) folded into the engine's result producer — keep them ready, because the moment any wire/cross-runtime boundary appears they are mandatory.
- **Shared core (all three):** the F5→…→F10 spine; supersede-by-BEAM #13/#15/#21/#30/#18/#33; fault-as-data via `monitor`; raw `erlang:spawn`; `goal_id` dedupe + 034 review fixes (self-bind⇒Unbound, forward-to-terminal suspension-drop); local-only deref/WxW/compression; loopback transport first to hit SC-001; roadmap hygiene (mark #4/#2/#030 **released**, promote #26 → specified).

## Synthesis: SHOULD AVOID

- **Building the REPL/engine process split as part of M1** (#1 & #2) — unneeded scope + message-copy/heap-index hazard; keep M1 in-process, defer the split as future engine-separation value.
- **Treating C# as the port oracle** (#2 drift) — Dart is ratified; reconcile the Dart↔C# divergence, use C# only as the #5 wire-parity oracle.
- **Dropping frame-envelope parity** (#3, and #1) — re-instate it as a #36 sub-requirement, else #5 fails late.
- **Hard-dropping persistence (#20/#18)** (#3) — prefer realign-defer; the classification is cheap to retain and matters for long-running linked nodes.
- **Assuming `erlang:monitor` on AtomVM** (all three) — make it an explicit early spike before committing the M2 fault model.