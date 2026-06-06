---
title: "Implementing Grassroots Logic Programs with Multiagent Transition Systems and AI — madGLP correctness (Thm 5.7) as the N-agent B2 fidelity yardstick"
authors: "Ehud Shapiro (with AI collaborators per the paper's title/method); the CGLP/implementation paper cited by docs/ma/madGLP-spec.md as its source"
year: "2026"
source_url: "https://arxiv.org/abs/2602.06934 (HTML: https://arxiv.org/html/2602.06934v2 ; PDF: https://arxiv.org/pdf/2602.06934)"
retrieved: "2026-06-06"
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Is there an authoritative LOCAL spec for DISTRIBUTED unification semantics across N>2 instances (B2), or is madGLP-spec.md v5.3 (pairwise agent links) the only ground truth? Specifically: how are nested global names with chained forwarding (A->B->C) proven correct beyond the 3-agent friend-introduction example, and is there a formal monotonicity/confluence theorem to cite as the B2 fidelity yardstick?"
precedence_class: glp-paper
access: full-text
---

# Extraction: madGLP correctness theorem and the N-agent B2 fidelity yardstick

## Bottom line (answers the question directly)

1. **No LOCAL spec states an N-instance correctness theorem.** Within the repo,
   `docs/ma/madGLP-spec.md` (v5.3, DRAFT) is the only ground-truth document for distributed
   unification, and it deliberately stops short of a formal theorem. It states only that
   correctness "relies on monotonicity" (an *informal* appeal), gives the **3-agent
   friend-introduction** (§10.3) as its largest worked chained-forwarding example, and cites
   its source as the **CGLP paper, Section 7** — which is **not present locally**.

2. **The authoritative N-agent theorem lives in the cited paper, now identified and fetched.**
   The cited "CGLP paper" is **arXiv 2602.06934**, *"Implementing Grassroots Logic Programs
   with Multiagent Transition Systems and AI"* (Shapiro et al., 2026). Its **Appendix C
   ("madGLP Specification")** is the long form of the local `madGLP-spec.md`, and it carries
   the formal result the local spec lacks:

   > **Theorem 5.7.** "The implementation (madGLP, π) of maGLP is correct."

   This is the citable **B2 fidelity yardstick**: any distributed link primitive that splits a
   writer X / reader X? across instances is faithful iff it realizes the madGLP transform that
   Theorem 5.7 certifies against maGLP.

3. **The theorem is for an arbitrary finite agent set, not pairwise.** madGLP is defined over
   "agents P ⊂ Π" (Definition 5.6 / local §7) with **no restriction to two agents**; all
   transactions are **unary** (each touches one agent's local state), so the N-agent case is
   not a separate construction — it is the base construction. Chained forwarding A→B→C is the
   same unary Reduce→Send→Receive mechanism applied at each hop; the 3-agent example is an
   *illustration* of the general mechanism, not its proof.

4. **The formal basis is PERSISTENCE (monotonicity), not confluence.** Correctness rests on a
   **persistence** property (assignments, once made, are never retracted; enabled transitions
   stay enabled), formalized as **Lemma 5.2 (maGLP Persistence)** and the GLP-level
   **Lemma 3.12 (GLP Persistence)**. This is exactly the "monotonicity" the local spec gestures
   at, now backed by named lemmas. **There is no confluence theorem** — and there cannot be a
   system-level one: madGLP is deterministic **only at the agent level**, non-deterministic
   system-wide because message delivery is asynchronous.

So: cite **Theorem 5.7** (from arXiv 2602.06934, App. C) as the B2 yardstick; cite
**Definition 2.3** for what "correct" means; cite **Lemma 5.2 / Lemma 3.12** for the
monotonicity foundation. The local `madGLP-spec.md` remains the *implementation* ground truth
(SOURCE PRECEDENCE class `glp-current`), but for the *correctness theorem* the paper is the
source.

---

## 1. What the LOCAL spec actually provides (and does not)

`docs/ma/madGLP-spec.md` v5.3 (precedence `glp-current`, the highest authority) is the
implementation ground truth for distributed unification. On the B2 correctness question it
gives only **informal** statements:

- §9.2 (Correspondence to maGLP), verbatim:
  > "The maGLP binary Communicate transaction, which atomically transfers an assignment from
  > one agent's writer to another agent's reader, is implemented in madGLP by the sequence:
  > Reduce (assigns writer, triggering `global_send`) → Send → Receive (applies assignment).
  > **The correctness of this implementation relies on monotonicity.**"

- §12.3 (Note on Atomicity), verbatim:
  > "Correctness relies on monotonicity—once global links are established, values flow forward."

- §10.3 (Friend-Mediated Introduction) is the **largest** chained-forwarding example: Charlie
  (writer) → Bob (local pair `(X, X?)` as forwarding point) → Alice (reader). The value flow is
  given step-by-step (9 steps), but as an *example*, with **no general N-hop theorem**.

- §13 (Invariants) lists SRSW, Entry Lifecycle, Send Atomicity, Index Uniqueness, and
  **Message Ordering** ("Messages between any pair of agents are delivered in FIFO order") — the
  operational invariants the proof needs, but **not** a stated soundness/completeness theorem.

- §16 References names the source: *"CGLP Paper (`~/Grassroots/CGLP`), Section 7 'Multiagent
  Deterministic GLP (madGLP)'"* — i.e. the theorem is deferred to a paper not in the repo.

**Conclusion for B2:** the local spec is pairwise-mechanism + informal-monotonicity +
3-agent example. The N>2 formal correctness statement is **not** local; it is in the paper below.

---

## 2. The authoritative paper (identification + provenance)

- **arXiv 2602.06934**, *"Implementing Grassroots Logic Programs with Multiagent Transition
  Systems and AI"* — abstract: <https://arxiv.org/abs/2602.06934>, HTML v2:
  <https://arxiv.org/html/2602.06934v2>, PDF: <https://arxiv.org/pdf/2602.06934>.
- This is the implementation companion to the founding paper **arXiv 2510.15747**
  (*"GLP: A Grassroots, Multiagent, Concurrent, Logic Programming Language"*) and the typing
  paper **arXiv 2601.17957** (*"Types for Grassroots Logic Programs"*).
- 2602.06934 **is** the "CGLP paper" the local spec cites: its **Appendix C** is titled
  "madGLP Specification" and its body **Section 5.2** is "Implementing Multiagent GLP with
  Deterministic Agents". The local `madGLP-spec.md` is a condensed, implementation-oriented
  restatement of this material; the paper is the precedence-class `glp-paper` source for the
  formal theorem.

Precedence note: per SOURCE PRECEDENCE, the local `glp-current` spec governs *current
implementation truth*; this paper (`glp-paper`) supplies the *correctness theorem* the local
spec omits and does not override any local semantics.

---

## 3. Load-bearing verbatim quotes from arXiv 2602.06934

**(a) The correctness theorem — the B2 yardstick.**
> **Theorem 5.7.** "The implementation (madGLP, π) of maGLP is correct."
>
> **Proof 5.8.** "*Live:* By Lemma C.41 [π is Live]. *Complete:* By Lemma C.45 [madGLP
> Completeness]."

(Lemma C.41 "π is Live" and Lemma C.45 "madGLP Completeness" are named and cited as the two
halves of the proof; their full statements sit in App. C.9 "Correctness Proofs", which the
arXiv HTML truncates — recorded here as the citation to retrieve from the PDF if the full text
of each lemma is needed.)

**(b) What "correct" means (the implementation-relation definition).**
> **Definition 2.3.** "The implementation (TS′, σ) of TS is: **live** if σ maps every fair TS′
> run r′ to a fair TS run σ(r′); **complete** if for every complete TS run with outcome O,
> there exists a complete TS′ run with outcome O; **correct** if it is live and complete."

So Theorem 5.7 means: every fair madGLP run maps to a fair maGLP run (no madGLP behaviour is
unfaithful), and every maGLP outcome is achievable by madGLP (nothing is lost). This is the
precise sense in which a faithful distributed link layer must behave like the original
single-store program.

**(c) Arbitrary agent set + all-unary transactions (why this is N-agent, not pairwise).**
> Definition 5.6 defines madGLP over "agents P ⊂ Π" (no two-agent restriction).
> "All madGLP transactions are unary. Cold-calls, which in maGLP require binary transactions,
> are implemented using the index-0 serializer, decomposing into unary Send and Receive
> transactions."
> "madGLP is deterministic at the agent level (not at the system level due to communication
> asynchrony)."

**(d) The monotonicity / persistence foundation (named lemmas).**
> **Lemma 3.12 (GLP Persistence).** "GLP is persistent." (all transitions, once enabled, remain
> enabled until taken — the resolvent/constraint store only grows; assignments are not
> retracted).
> **Lemma 5.2 (maGLP Persistence).** "maGLP is persistent. See Appendix B for proofs and
> further remarks."

These lemmas are the formal content of the local spec's informal "relies on monotonicity".

**(e) Chained forwarding — general mechanism vs. example.**
> Section 5.2 "Implementing Multiagent GLP with Deterministic Agents" gives the two-agent
> **Client-Monitor** scenario (Program 1, Fig. 1). **Appendix E.2 "Example 2: Friend-Mediated
> Introduction"** gives the 3-agent A→B→C chain (= local §10.3). The "exporting both ends of a
> pair" two-hop forwarding case is handled by a remark in App. C (local §5.4 / §8.3
> "Automatic Forwarding"). **No separate theorem is stated for arbitrary-length forwarding
> chains** — chains are correct because each hop is an instance of the same unary
> Reduce→Send→Receive that Theorem 5.7 certifies, and persistence guarantees values only flow
> forward. The proof is by the general unary-transaction argument, not by enumerating chain
> lengths.

---

## 4. Why no confluence theorem (and why that is the right answer for B2)

- **Confluence is absent by design.** madGLP is deterministic *only per agent*; the *system* is
  non-deterministic because cross-agent message interleaving is asynchronous (Def. 5.6 note).
  A global confluence/Church-Rosser theorem would be false for the system as a whole.
- **The correct yardstick is the implementation relation (Def. 2.3) + persistence**, not
  confluence. Faithfulness is "every fair madGLP run maps to a fair maGLP run, and every maGLP
  outcome is reachable" (Thm 5.7), with persistence (Lemma 5.2 / 3.12) guaranteeing that
  partial bindings, once propagated across a link, are never undone. For B2, a distributed link
  primitive is faithful iff it preserves: (i) unary-transaction decomposition of each
  Communicate into Reduce→Send→Receive, (ii) FIFO per-pair message ordering (local §13), and
  (iii) write-once/read-once (SRSW) so that persistence holds end-to-end.

---

## 5. Recommendation for the B2 thread

- **Cite Theorem 5.7** (arXiv 2602.06934, App. C / §5.2) as the **B2 fidelity yardstick** — the
  named, N-agent, formal correctness statement the local spec lacks.
- **Cite Definition 2.3** for the meaning of "faithful" (live + complete).
- **Cite Lemma 5.2 / Lemma 3.12 (persistence)** as the monotonicity foundation — this *is* the
  "relies on monotonicity" the local spec invokes, now formalized.
- **Do not look for a confluence theorem** — none exists or should; agent-level determinism +
  system-level async non-determinism is the intended model.
- **Gap to close locally (optional, for the engineer):** `docs/ma/madGLP-spec.md` could be
  upgraded from DRAFT by importing Theorem 5.7 + Def. 2.3 + Lemma 5.2 statements and a pointer
  to App. C.9 (Lemmas C.41/C.45), and by adding one sentence that chained forwarding is the
  general unary mechanism (not a special 3-agent case). The full statements of C.41/C.45 should
  be pulled from the PDF if a self-contained local proof is wanted.
