---
title: "Implementing Grassroots Logic Programs with Multiagent Transition Systems and AI"
authors: "Ehud Shapiro (London School of Economics, UK; Weizmann Institute of Science, Israel)"
year: "2026"
source_url: "https://arxiv.org/abs/2602.06934 (HTML: https://arxiv.org/html/2602.06934v2 ; PDF: https://arxiv.org/pdf/2602.06934)"
retrieved: "2026-06-06"
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Fetch, preserve & extract source: CGLP paper, Section 7 'Multiagent Deterministic GLP (madGLP)' (Shapiro/Grassroots)"
precedence_class: "glp-paper"
access: "full-text (HTML; arXiv long-appendix sections C/D truncated by the fetch tool — see Access Caveats)"
arxiv_id: "2602.06934v2"
arxiv_categories: "cs.PL, cs.AI, cs.DC, cs.LO, cs.MA"
submitted: "2026-02-06 (last revised 2026-04-06)"
---

# CGLP paper — madGLP (Multiagent Deterministic GLP): correctness & monotonicity for the distributed link layer

## Source identity & precedence

This is the academic source the local `docs/ma/madGLP-spec.md` (v5.3) is derived from.
The local spec's References (§16) cite it as "CGLP Paper (`~/Grassroots/CGLP`), Section 7
'Multiagent Deterministic GLP (madGLP)'." The published paper is **arXiv:2602.06934**,
*"Implementing Grassroots Logic Programs with Multiagent Transition Systems and AI"* by
**Ehud Shapiro**. **The local "Section 7" maps to the published Section 5.2 ("Implementing
Multiagent GLP with Deterministic Agents") plus Appendix C ("madGLP Specification") and
Appendix D ("madGLP is Grassroots").** The `~/Grassroots/CGLP` working title corresponds to
this paper.

**Precedence class: `glp-paper` (tier 2).** Per the thread's SOURCE PRECEDENCE, the local
`docs/` specs (tier 1) remain the implementation truth on any conflict; this paper supplies
the formal N-instance correctness / monotonicity argument that the local spec only sketches.
Note: the published paper uses **`dGLP`/`madGLP`** (single-agent deterministic / multiagent
deterministic) as the implementation-ready semantics derived from the abstract
non-deterministic **`GLP`/`maGLP`**. The local spec's "irmaGLP/maGLP" lineage corresponds to
this `maGLP → madGLP` derivation.

## Abstract (verbatim)

> "Grassroots Logic Programs (GLP) is a multiagent, concurrent, logic programming language
> designed for the implementation of smartphone-based, serverless, grassroots platforms.
> Here, we start from GLP and maGLP -- concurrent and multiagent abstract nondeterministic
> operational semantics for GLP, respectively -- and from them derive dGLP and madGLP --
> implementation-ready deterministic operational semantics for both -- and prove them
> correct with respect to their abstract counterparts."

The concluding statement affirms that **"both madGLP and maGLP are grassroots."**

---

## 1. What "correct implementation" means (Definition 2.3) — the yardstick

The paper's notion of a correct implementation is the bar any distributed link-layer split
must clear. Quoted verbatim:

> **Definition 2.3.** "The implementation (TS′, σ) of TS is:
> - **live** if σ maps every fair TS′ run r′ to a fair TS run σ(r′)
> - **complete** if for every complete TS run with outcome O, there exists a complete TS′
>   run with outcome O
> - **correct** if it is live and complete"

So `correct = live ∧ complete`. **For the link layer this is the transparency target**: the
split-across-instances program (TS′ = madGLP over the chosen transports) must (a) only
produce abstract runs that the single-shared-variable original (TS = maGLP) could produce
(liveness), and (b) be able to reproduce every outcome of the original (completeness).

---

## 2. maGLP "Communicate" transaction — the shared-variable primitive being replaced

This is the binary, atomic, cross-agent assignment-transfer that the link layer must
distribute. Quoted verbatim (Definition 5.1, maGLP **Communicate p to q**):

> "A transaction with participants p, q ∈ P where c_p = (G_p, σ_p), c_q = (G_q, σ_q),
> {X? := T} ∈ σ_p, X? occurs in G_q, c′_p = (G_p, σ_p ∖ {X? := T}), and
> c′_q = (G_q{X? := T}, σ_q)."

In words: agent p holds an assignment `X? := T` for a reader X? that occurs in agent q's
goals; the transaction atomically moves that assignment into q's resolvent. **This is the
"shared logic variable carrying the binding across instances" that the new link primitives
must replace** — the writer side at p, the reader side at q.

The paper also states (Section 5.2): **"All madGLP transactions are unary. Cold-calls, which
in maGLP require binary transactions, are implemented using the index-0 serializer,
decomposing into unary Send and Receive transactions."** This unary decomposition is what
makes a genuinely multi-instance, decentralized realization possible — no synchronous
two-party rendezvous is required on the wire.

---

## 3. madGLP local state & transition system (Definitions 5.5, 5.6)

> **Definition 5.5 (madGLP Local State).** "The local state of agent p ∈ Π is a tuple
> s_p = (R_p, W_p, M_p) where:
> 1. R_p = (A_p, S_p, F_p) is a *deterministic resolvent*: A_p is a queue of active goals,
>    S_p contains suspended goals paired with blocking readers, and F_p contains failed goals
> 2. W_p is a *global writers table*
> 3. M_p is a set of pending *outgoing messages*"

> **Definition 5.6 (madGLP Transition System).** "The *madGLP transition system* over agents
> P ⊂ Π and GLP program M is the multiagent transition system madGLP = (C, c0, T) where C is
> the set of configurations, c0 is the initial configuration with empty resolvents except for
> the initial agent goal and index-0 serializer entry, and T consists of:
> - Reduce: unary transaction processing goals via FIFO scheduling
> - Send: unary transaction moving messages to communication channels
> - Receive: unary transaction processing incoming messages"

(Matches local `docs/ma/madGLP-spec.md` §6–§8, which gives the same three-tuple state and the
Reduce/Send/Receive transactions, plus the index-0 serializer at boot.)

---

## 4. The link mechanism: global_send, global writers table, Globalize/Localize (Appendix C)

These are the exact constructs the distributed link primitives instantiate. Quoted verbatim:

> **Definition C.3 (Global Writers Table).** "The global writers table W_p of agent p is an
> array of global writers table entries. For entries created by Globalize at index i, the
> index i is the index in the global name _w(p,i). For entries created by Localize, the entry
> stores the remote index explicitly."

> **Definition C.5 (global_send Predicate).** "The system predicate global_send/3 is defined
> as: `global_send(T, G, Q) :- known(T) | '_send'(T, G, Q).` where: T is the reader whose
> value will be sent when known; G is the global variable name identifying the link; Q is the
> destination agent."

> **Definition C.12 (Globalize).** "Given agent p, remote agent q, and term T, the
> globalization by p may update the global writers table and spawn goals. For each variable Y:
> if Y is a writer, allocate index i, create entry (Y, q) at index i, and replace Y with
> _w(p,i). If Y? is a reader, allocate index i, replace Y? with _r(p,i), and spawn
> global_send(Y?, _r(p,i), q)."

> **Definition C.13 (Localize).** "Given agent q, remote agent p, and globalized term, the
> localization may update the global writers table and spawn goals. For _w(p,i): create fresh
> pair (Y_q, Y_q?), replace _w(p,i) with Y_q, and spawn global_send(Y_q?, _w(p,i), p). For
> _r(p,i): create fresh pair (Z_q, Z_q?), add entry (Z_q, p, i), and replace _r(p,i) with
> Z_q?."

> **Definition C.16 (madGLP Reduce Transaction).** "The unary Reduce transaction for agent p
> transitions s_p → s_p' where A_p = A·A_r: if GLP reduction of A with first applicable clause
> C succeeds with (B, σ̂), then apply σ̂ to assign writers and σ̂? to propagate readers in
> resolvent, and reactivate suspended goals on instantiated readers."

**B2-relevant reading.** The atomic GLP unit (writer X / reader X?) is preserved by
*replacing the shared pair with two local pairs joined by a global link*: a `global_send`
goal at the writer-owner + a global writers table entry at the reader-owner. A writer being
globalized creates the **receiving** entry at the globalizer (it expects the value back); a
reader being globalized spawns the **sending** `global_send` at the globalizer (it keeps the
writer, sends the value forward). Writer-MGU semantics are untouched: each side still does
purely local writer assignment (`σ̂`) + reader propagation (`σ̂?`); only the *delivery* of a
binding to a remote reader is mediated by Send/Receive. This is precisely the transform the
link layer generalizes to N transports/instances.

---

## 5. Correctness theorem & its supporting lemmas (the N-instance argument)

> **Theorem 5.7 (madGLP Correctly Implements maGLP).** "The implementation (madGLP, π) of
> maGLP is correct."

> **Proof 5.8.** "*Live:* By Lemma C.41. *Complete:* By Lemma C.45."

Supporting results (referenced by number; full appendix text was truncated by the fetch tool
— see Access Caveats):

- **Lemma C.28 (Globalize-Localize Correspondence)** — the pairing that guarantees each
  globalized writer/reader is localized into a complementary reader/writer so dataflow is
  correct (used in Proof 5.10).
- **Lemma C.41 (π is Live)** — establishes the liveness half of correctness.
- **Lemma C.45 (madGLP Completeness)** — establishes the completeness half.

### Monotonicity / non-interference — the load-bearing basis for unbounded N

The paper grounds correctness (and its carry-over from single-agent to multiagent, i.e. to
**unbounded N**) on two compositional properties, quoted verbatim:

> **Lemma 3.20 (Disjoint Substitution Commutativity).** "Let σ̂₁ and σ̂₂ be writers
> substitutions produced by two GLP Reduce transitions on distinct goals. Then σ̂₁ and σ̂₂
> assign disjoint sets of variables, and σ̂₁ ∘ σ̂₂ = σ̂₂ ∘ σ̂₁."

> **Lemma 3.12 (Persistence).** "GLP is persistent" — all enabled transitions remain enabled
> until taken, ensuring non-interference.

> "disjoint substitution commutativity (from the SO [single-occurrence / SRSW] invariant) and
> Persistence enable correctness proofs that carry over directly from the single-agent to the
> multiagent case."

From Related Work (Concurrent Constraint Programming), the monotonicity heritage is stated:

> "their foundational insight—that concurrent agents communicate through a shared constraint
> store that grows **monotonically**—provides the semantic template for GLP's single-assignment
> variables."

And on madGLP's own state growth:

> "variable assignments accumulate but are never undone, and agent states grow through message
> reception."

The abstract corroborates the proof basis: correctness is "grounded in *disjoint substitution
commutativity* (from GLP's single-occurrence invariant) and *persistence*."

**B2 takeaway:** the formal reason a single shared variable can be split across two — and then
N — instances *without changing observable behaviour* is (i) **single-assignment monotonicity**
(a writer binds once, bindings only accumulate, never retract), (ii) **SRSW/single-occurrence**
⇒ distinct goals touch disjoint variable sets ⇒ their substitutions **commute** (Lemma 3.20),
and (iii) **persistence** (Lemma 3.12) ⇒ enabling is stable, so deferring a transaction across
a slow/asynchronous transport cannot disable it. Any distributed-unification scheme for the
link layer must preserve all three; if a transport reorders or loses messages it must still be
made to look monotone + FIFO-per-pair (the paper assumes FIFO per agent-pair) for Theorem 5.7
to transfer.

---

## 6. Grassroots property for unbounded instances (Appendix D)

> **Appendix D.4 — "Implementations Preserve Grassroots."** The grassroots property is
> described as: "any subset of agents forms a functioning subsystem without requiring
> permission or coordination" — "introduced for distributed platforms." The section
> establishes that **madGLP preserves the grassroots property**, i.e. correctness holds across
> N instances **without global coordination**. The concluding statement: **"both madGLP and
> maGLP are grassroots."**

(Full theorem text in D.4 was truncated by the fetch tool. The section title and the
concluding statement are reproduced above; the formal statement is "the implementation of a
grassroots transition system by madGLP is itself grassroots / preserves grassroots.")

**B2 takeaway:** "grassroots" is the formal analogue of the thread's **strict peer-to-peer /
bilateral, no-global-resource** requirement: any subset of instances must operate independently
and coalesce without a coordinator. A broker-mediated transport (T1: MQTT/XMPP) or a
one-to-many broadcast (T2: BLE LE-Audio BIS) only satisfies the grassroots/SRSW model if the
broker/broadcast is treated as transport plumbing under a per-link bilateral global_send pair —
not as a logical hub — otherwise it violates "no global resource other than the network."

---

## Access Caveats

- **HTML/PDF available, full text public** (arXiv:2602.06934v2). The body (Sections 2–5) was
  retrieved cleanly. **The long Appendices C and D were truncated by the fetch tool**
  (consistently cut off around Definition C.16 / mid-proof), so Lemmas C.28, C.41, C.45 and
  the Appendix-D grassroots theorem are cited by number/title with their available text but
  their *complete* verbatim proof bodies are not reproduced here. To capture them verbatim,
  fetch the PDF and read the appendix pages directly, or open
  `https://arxiv.org/html/2602.06934v2#A3` (Appendix C) and `#A4` (Appendix D).
- The local `docs/ma/madGLP-spec.md` (v5.3) is a faithful, more-detailed engineering
  rendering of this paper's Section 5.2 + Appendix C (it adds the serializer Receive case,
  the worked Client-Monitor / Return-Value / Friend-Mediated examples, and the Dart I/O
  predicates `send_to_net`/`send_to_ui`). On any conflict, the local spec (tier 1) wins;
  no conflict was observed — the spec's v5.3 "corrected Globalize/Localize direction" matches
  Definitions C.12–C.13 above (writer → entry at globalizer; reader → global_send at
  globalizer).

## Citation

Ehud Shapiro. *Implementing Grassroots Logic Programs with Multiagent Transition Systems and
AI.* arXiv:2602.06934v2 [cs.PL] (also cs.AI, cs.DC, cs.LO, cs.MA), submitted 2026-02-06, last
revised 2026-04-06. madGLP: Section 5.2 + Appendix C; grassroots preservation: Appendix D.
