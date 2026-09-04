//// glp/ring — per-ring realizations of the L0 contract (feature 101, C1 · FR-003/FR-004).
////
//// One module per ring. Each is held to `glp/contract/` and may depend on exactly one
//// runtime; rings never depend on each other (LATTICE line 27 — L1a and L1b must not
//// share). The rings in scope for this feature:
////
////   - `glp/ring/beam`   — L1b, the workstation. Erlang/OTP on the developer machine.
////   - `glp/ring/atomvm` — L1a/L2, the app. AtomVM inside the MAUI Blazor Hybrid host.
////
//// Two rules this package exists to make enforceable, both of which have a positive
//// control before they have an implementation (C6):
////
////   1. **An unbuilt ring never reads as a pass.** Build one ring, ask for the aggregate,
////      and the aggregate must REFUSE — not report the built ring's result as the whole.
////      This is the single most likely way this feature could ship a lie, so it is guarded
////      by test/ring/test_aggregate.sh (SC-006) before any ring is written.
////   2. **Admission is by measured contract consumption, never by name.** `glp_gleam` is
////      not admitted to L0 on the strength of the shared word "Gleam": LATTICE line 35
////      names the polyglot-L0 service set as `kv/`, `mailbox/`, `network/`, and
////      `glp_gleam/src/` contains none of them. Refusal must quote the name it refused
////      (SC-005, test/ring/test_contract_purity.sh).
////
//// Empty-but-building at T002 by intent; `beam` lands at T014 and `atomvm` at T018. T018
//// is blocked on T017 — the enumeration of AtomVM's unsupported constructs — which is
//// **unmeasured today and must not be guessed**. The one thing already known: `gleam_otp`
//// is excluded because its `proc_lib` use is outside AtomVM's BEAM/OTP subset.
