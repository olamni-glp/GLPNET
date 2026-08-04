# T040 escalations — spec determinations (for the engineer's ruling)

Each item below was taken back to the governing spec text. Where the spec answers
it, the answer is quoted verbatim. Where the spec is **silent**, that is stated and
nothing is filled in (CLAUDE.md — "If the spec is silent on the case: say so, don't
fill in"). No code was changed for any item on this page.

---

## E1 — Bridge trust boundary (load-bearing)

**Code fact.** `csharp/glp_quick_host/BridgeAcceptor.cs` admits peers over plain,
unauthenticated TCP into the same `Program.ClientPumpAsync` → `Mesh.RouteAsync` path
used by certificate-authenticated QUIC clients, and `--bridge-addr` (`Program.cs`
`Opts.Parse`) accepts any host, so the operator can bind it off-loopback. The Gleam
side enforces the opposite policy on the same seam.

**Governing text — `spec.md` FR-004, verbatim:**

> **FR-004**: Gleam peers MUST be able to join QUIC-WS meshes via the existing C#
> QUIC-WS endpoint acting as a bridge (Gleam↔bridge over an existing Gleam transport,
> bridge↔mesh over QUIC-WS); a native BEAM QUIC-WS leaf is OUT of this feature and
> recorded as a gated deferral.

**Determination.** The implementation satisfies FR-004 literally: TCP *is* "an existing
Gleam transport", and the clarification ruling that selected the bridge route
(`spec.md` Clarifications Q1) speaks only to routing, not to authentication. **The spec
is silent on authentication, identity, and bind scope for the Gleam↔bridge leg.**
This is therefore not a defect against the written spec — it is an unaddressed trust
boundary, and closing it either way is a spec change.

**The decision is yours. Three options, with consequences:**

| Option | Consequence |
|---|---|
| (a) Default the bridge to loopback; require an explicit opt-out flag for any other bind | Closes the asymmetry with the Gleam BE at the cost of one operator flag. Breaks nothing today (every shipped scenario binds loopback). |
| (b) Require identity on the bridge leg (shared secret / cert) | Strongest, but it is net-new protocol surface on a seam 064 only had to *reach*, and it needs its own spec text + a wire rule. |
| (c) Record it as intended operator surface with a documented trust note | Zero code. The hazard becomes explicit and owned rather than latent. |

**Advisory recommendation (not a decision): (a).** The real hazard is not the bridge
existing — it is that the two runtimes ship *opposite defaults on one seam*, so an
operator's mental model is wrong on whichever side they learned second.

---

## E2 — IL/text latch scope (`RequestDispatcher.cs:54`)

**Governing text — `contracts/il-request-kind.md` rule 3, verbatim:**

> 3. LOAD_SOURCE/RUN_GOAL (text kinds) remain valid during a deprecation window; a
>    client chooses per session, never mixed per module.

**Determination.** The rule has two clauses that were written for the **single-client**
062 world and pull in opposite directions once 064 added multi-client serve:

- *"a client chooses per session"* — the chooser is the **client**, which reads as a
  per-client-session latch (the current code latches once, engine-wide).
- *"never mixed per module"* — with one shared `GlpRuntimeEngine` behind N clients, a
  per-client choice necessarily produces a module space containing both text-loaded and
  IL-loaded modules, which this clause forbids.

**The contract is silent on the multi-client case** — it cannot be satisfied in both
clauses simultaneously under a shared engine. This is a contract gap introduced by
064's own FR-005, not a coding error.

**Needs your ruling**, and the ruling should be written back into
`contracts/il-request-kind.md`:
- (a) engine-wide latch (current behaviour) — "first client's choice binds the engine";
- (b) per-client latch + per-client module namespaces (larger: the engine gains a
  session-scoped module space);
- (c) per-client latch, accept a mixed module space, and delete the "never mixed" clause.

---

## E3 — Path selection committed before validation (`RequestDispatcher.cs:120`)

**Governing text — `spec.md` (062) FR-005a, verbatim:**

> - **FR-005a**: Compiled-IL-on-the-wire MUST be hardened for production use: malformed
>   IL and incompatible IL versions MUST be rejected safely with a diagnostic, and a
>   transport failure mid-transfer MUST NOT corrupt engine state.

062 `tasks.md` T018 records the discharge as: *"mid-transfer (dropped fragment) → no
whole frame → engine never reached (obligation 3)"*.

**Determination.** FR-005a's obligation is scoped to **engine state**. Module
registration is engine state and is already honoured. **Path selection is dispatcher
state, and the spec is silent on it** — a refused request currently still leaves the
dispatcher's text/IL latch set.

The obvious hardening (commit the latch only after validation) is *not* neutral: it
changes the observable outcome of "invalid IL request, then a text request" from
refused-as-mixed to accepted. That is an E2 question, not an E3 one. **Rule E2 first;
E3 then follows from it.**

---

## E4 — D064-7 cross-runtime binding rendering

Already a recorded deferral with a named gate (`DEFERRALS.md` D064-7: C# pre-rendered
strings vs Gleam structured terms — both legal 038 envelopes; each side's renderer
mis-displays the other; evidence in `t029-cross-febe-smoke.md`).

**Determination.** Nothing new was found. The review restated the deferral; it is not a
new defect. **Requested: confirm it stays deferred** (my reading: yes — D064-7 says to
decide the convention together with D064-2's wire work, and D064-2 has now left 064 for
the `distributed-unification-quiescence-protocol` feature, so the natural home for the
ruling moved with it).

---

## Standing queue — status, no work started

### DEF-F1 — self-prove liveness GLP goal 🔴 §1.14

Requires a **net-new system predicate**. Under CLAUDE.md §1.14 (Language Authority) the
GLP language definition "cannot be revised, extended, or added to without explicit
discussion with Gabi and his express approval". The proposal was delivered by 061 T028
and is **propose-only, zero implementation**:
`docs/research/repl-engine-separation/self-prove-liveness-proposal.md`.
`docs/research/repl-engine-separation/reconciliation/DEFERRALS.md:49` records it as
open, awaiting the language authority's ruling. **No code will be written until you
rule.** MVP liveness meanwhile remains host-timer only.

### UPPAAL / 061 T030 — blocked on tooling, not on work

`docs/research/repl-engine-separation/models/uppaal/RESULT.md` states verbatim:
*"BLOCKED — model + queries + harness complete; the real-tool verdict is pending an
UPPAAL license key (engineer action). NO verdict is claimed."*

Verified on this host: **no `uppaal` and no `verifyta` on PATH**, no install under
`C:\Program Files`. The model (`supervision.xml`), the queries and `run.sh` are done —
the file refreshes itself once a key exists. **Engineer action: obtain/install the
UPPAAL licence.** Note the ratified armoury (DECISIONS-LOG R14/R15) makes **Promela/SPIN
the required default** for wire-protocol verification and UPPAAL an armoury option, so
this blocks a *supplementary* timed verdict, not a required gate.

### GEPA — roadmap item, not implementation work

It is the captured standalone roadmap feature
`buildkit-coordination-optimisation-gepa-dspy-coop-scheduler-marathon-buildkit-tooling`.
Correct path is `/bk-roadmap review` → score → refine → promote → `/bk-specify`; see the
roadmap round run alongside this memo.
