---
title: "Efficient Logic Variables for Distributed Computing"
authors: "Seif Haridi, Peter Van Roy, Per Brand, Michael Mehl, Ralf Scheidhauer, Gert Smolka"
year: 1999
source_url: "https://dl.acm.org/doi/10.1145/319301.319347 (full text PDF: https://www.ps.uni-saarland.de/Publications/documents/Toplas_99.pdf)"
retrieved: 2026-06-06
fetched_for: "Prior art for distributing logic variables / concurrent-logic computation across processes and machines (FCP/Logix; KL1/KLIC/PDSS/PIM; Distributed Oz/Mozart; distributed/parallel Prolog; Erlang/OTP; CRDT/CALM monotonic binding) — surveyed for the GLPnet multi-protocol link-layer feature that distributes the GLP writer/reader atomic pair across remote REPL instances. — Fetch, preserve & extract source: Efficient Logic Variables for Distributed Computing (Haridi, Van Roy, Brand, Mehl, Scheidhauer, Smolka)"
precedence_class: earlier-cl-paper
access: full-text
---

# Efficient Logic Variables for Distributed Computing

**Citation:** Seif Haridi, Peter Van Roy, Per Brand, Michael Mehl, Ralf Scheidhauer,
Gert Smolka. "Efficient Logic Variables for Distributed Computing." *ACM Transactions
on Programming Languages and Systems (TOPLAS)*, vol. 21, no. 3, pp. 569–626, May 1999.
DOI 10.1145/319301.319347. Received Feb 1998; revised Sep 1998; accepted Dec 1998.

**Affiliations:** SICS (Haridi, Brand); UCL Louvain + SICS (Van Roy); DFKI (Mehl,
Scheidhauer); Univ. des Saarlandes + DFKI (Smolka). Algorithm realized in the **Mozart
Programming System** implementing **Distributed Oz**.

**Precedence note (per the GLPnet source-precedence rule):** This is an *earlier
concurrent-logic / concurrent-constraint* paper. It is the **closest external analog to
GLP writer/reader distribution** and is used here for **mechanism inspiration only**; it
**never overrides current GLP semantics** as defined in the local `docs/` specs or in
Shapiro's GLP papers. Where Oz semantics differ from GLP (e.g., consistent *multiple*
assignment vs. GLP's strict SRSW single-writer; cells/state; eager copying vs. GLP
suspension), GLP semantics win.

---

## 1. Why this is the prime analog for GLPnet

The GLPnet link-layer feature must "SPLIT [a shared logic variable] across TWO runtime
/ REPL instances ... where the NEW LINK PRIMITIVES replace that shared variable and
carry the binding across instances," generalizing to N instances, "aiming for maximal
transparency." This paper solves *exactly* that problem for Oz logic variables and is
the only one in the corpus giving a **formally proven** distributed single-assignment
binding protocol. It directly informs:

- the owner/proxy ("manager + per-site proxy") model for a distributed writer/reader;
- the binding-request / win / lose / arrive message protocol (variable elimination);
- the **message-count budget** for the common cases (1 message owner-bound, 2 = RPC);
- the lazy-vs-eager distinction relevant to GLP suspension semantics;
- the latency-tolerance + third-party-independence arguments that justify the whole
  design ("logic variables instead of explicit message passing");
- a worked treatment of **streams/ports as the channel abstraction** over these vars.

> [Conclusions, verbatim] "To our knowledge, the present article gives the first formal
> definition and correctness proof of a practical algorithm for distributed rational
> tree unification."

---

## 2. Abstract (verbatim, abridged)

> "We define a practical algorithm for distributed rational tree unification and prove
> its correctness in both the off-line and on-line cases. We derive the distributed
> algorithm from a centralized one, showing clearly the trade-offs between local and
> distributed execution. The algorithm is used to realize logic variables in the Mozart
> Programming System, which implements the Oz language ... Logic variables implement the
> dataflow behavior. We show that logic variables can easily be added to the more
> restricted models of Java and ML ... In common cases the algorithm maintains the same
> message latency as explicit message passing. In addition, it is able to handle
> uncommon cases that arise from the properties of latency tolerance and third-party
> independence."

---

## 3. Two motivating concerns (verbatim definitions)

> "Two basic concerns in distributed computing are **latency tolerance** and
> **third-party independence**. We say a program is *third-party independent* if its
> execution is unaffected by sites that are not currently involved in the execution. We
> show that using logic variables instead of explicit message passing can reduce the
> effect of both concerns with little programming effort."

> "Logic variables decouple the declaration of a variable from its binding. Once a
> variable is declared, it can be passed to other sites, even before it is bound. When
> it is bound, the binding will be transferred automatically and efficiently to the
> sites needing it. This decoupling allows programs to provide a degree of *latency
> tolerance* ..."

**Key property — the binding follows the variable (relevant to GLPnet "carry the
binding across instances"):**

> "A logic variable can be passed among sites arbitrarily. At all times, it 'remembers
> its origins,' i.e., when the value becomes known then the variable will receive it.
> The communication needed to bind the variable is part of the variable and not part of
> the program manipulating the variable. This means that the variable can be passed
> around at will, and the value will always arrive at the variable."

---

## 4. The logic variable (Oz semantics) and the GLP delta

> "A logic variable conceptually has a fixed value from the moment of its creation. The
> value is unknown at first, and it remains unknown until the variable is bound. At all
> times, the variable can be used as if it were the value. If the value is needed, then
> the thread requiring the value will block until the variable is bound."

**Single-assignment vs GLP SRSW (load-bearing difference):** Oz logic variables allow
*consistent multiple assignment*, GLP forbids it (single writer). Verbatim:

> "**Single assignment**: logic variables. Assignment is done by a distributed
> unification algorithm ... logic variables provide *consistent multiple assignment*,
> i.e., there can be multiple assignments as long as they are unifiable. We keep the
> phrase 'single assignment' to avoid multiplying terminology."

**Variable-variable binding (relevant to GLP writer-to-reader vs writer-to-writer):**
Oz *permits* variable-variable binding and argues it is essential. GLP's writer-MGU
*forbids* writer-to-writer binding. Verbatim rationale for Oz allowing it:

> "one reason that variable-variable binding is important is that it allows us to
> maintain maximum latency tolerance and third-party independence when communicating
> among more than two sites, independent of fluctuating message delays. A second reason
> is that it has a very simple logical semantics."

**Contrast with futures / I-structures (relevant to GLP reader/writer pair):**

> "There remains a crucial difference with logic variables, namely that futures and
> I-structures can be assigned only once, whereas logic variables can be assigned more
> than once, as long as the assignments are consistent with each other."

> "An important difference with a logic variable is that a future can only be bound by
> the concurrent computation that is created along with it. ... to precisely model
> futures a read-only logic variable should be used."  *(Read-only logic variables in
> Oz are the closest analog to a GLP **reader** that cannot itself bind; see §8.4
> below.)*

---

## 5. Distributed unification — the binding protocol (key ideas, §2.2)

This is the core mechanism GLPnet should mine. Verbatim:

> "The two basic operations on logic variables are binding and waiting until bound.
> Waiting until bound is easy: the variable has a list containing threads that need its
> value. These threads are blocked. When the value arrives, the threads are awoken.
> Binding is harder: it requires cooperation between sites. If a variable exists on
> several sites, then it must be bound to the same value on all sites, despite
> concurrent binding attempts."

> "The basic distributed operation is binding a variable to a value. This is implemented
> by making one site the 'owner' of the variable. In the current system, the site that
> declares the variable is its owner. A **binding request is sent to the owner, and the
> owner forwards the binding to each site that knows the variable.** In terms of network
> behavior, **one message is sent to the owner, and one message is sent by the owner to
> each site that knows the variable.** ... **The owner accepts the first binding request
> and ignores all subsequent binding requests.** An ignored request will be retried by
> its initiating site after it receives the binding."

**Proxy terminology (= per-site representative of the distributed variable):**

> "Each variable occurrence on a site is called a 'proxy.' One of the sites is the
> variable's owner."

**Eager (default) vs lazy variables (directly maps to GLP suspension granularity):**

> "By default the binding is *eager*, i.e., the new value is immediately sent to all
> sites that know about X. ... We say that a logic variable is *lazy* on a site if its
> value is only sent to that site when the site needs it, e.g., when a thread is waiting
> for the variable. Binding a lazy variable typically needs fewer messages ... Both
> eager and lazy variables are implemented by the on-line DU algorithm ... They differ
> only in the scheduling of one reduction rule."

---

## 6. Message-count budget (the "same latency as explicit message passing" claim)

These exact figures bound what GLPnet's link primitives can hope to achieve over a
real transport. Extracted per idiom:

| Idiom | Network behavior | Messages |
|---|---|---|
| Bind a var to a value | 1 to owner + 1 from owner to each proxy site | owner-resident binding = **0 net new** (Fig.5 example: owner on the site doing the bind needs no network op) |
| Stream element produced (eager) | producer→consumer | **1 message per element** |
| Stream w/ flow control | consumer requests, producer replies | **1 round-trip per element** (relax with n-element buffer) |
| Barrier sync (remote task) | task binds X to owner | **1 message** per task (ack does not affect barrier latency) |
| Distributed lock (token passing) | exchange + bind New | **1 message** to transfer lock between sites |
| RMI (remote object reply via owned var) | binding request 1 + reply 1 | **2 messages = RPC latency** (3rd message back does not affect latency) |

> [§2.4.7, verbatim] "Since the local site owns X, the binding request sends one message
> from the remote site to the local site. With the initial invocation, this gives a
> total message latency of two for the remote call, just like an RPC. There is a third
> message back to the remote site that does not affect the message latency."

---

## 7. Streams / ports as the channel abstraction (§2.1.2, §2.3.3, §2.4.2)

Directly relevant to GLP channels `ch(In, Out?)` and `send/receive/merge`.

> "A *port* is an asynchronous channel that supports many-to-one communication. A port P
> encapsulates a stream S. A *stream* is a list with an unbound tail. The operation
> `{Send P M}` adds M to the end of S. Successive sends from the same thread appear in
> the order they were sent."

> [§2.4.2] "a stream is a list whose tail is a logic variable. The producer thread
> repeatedly binds the tail to a pair of an element and a new tail. The consumer thread
> can start reading the stream while the producer is still creating it. ... binding L to
> N|L1 adds one element to the stream. In the distributed execution this will send
> exactly one message to the consumer."

**Multiple readers (collides with GLP SRSW — note for the link layer):** Oz lets a
stream be read by *multiple* consumers because they *bind the tail consistently*. GLP
SRSW forbids multiple readers of one variable. Verbatim:

> "It is allowed for multiple readers to bind the list's tail, since they bind it in a
> consistent way. This would not work with ordinary single assignment, e.g., as provided
> by I-structures."

This is the precise tension flagged in GLPnet open sub-question **T2** (BLE LE-Audio BIS
broadcast / multi-reader vs SRSW): Oz "solves" multi-reader by *consistent multiple
assignment*, a relaxation GLP does **not** adopt. Any GLPnet multi-reader transport must
instead fan out via merge/streams, not by relaxing SRSW.

---

## 8. Formal algorithm core (§3–§5) — the part GLPnet can cite for correctness

The paper's structure for defining + proving the algorithm (Fig. 1):

```
CU algorithm (Sec 4)  --generalize-->  DU algorithm (Sec 5)
      |  extend to model redundant work          ^
      v                                           | proof DU implements RCU (Sec 6)
RCU algorithm (Sec 6.2) <----------------- (proof RCU correct, Sec 6.2)
```

- **CU** = Centralized Unification (off-line, base case), 7 rules.
- **DU** = Distributed Unification, **10 rules** = 6 nonbind + 4 bind.
- **RCU** = Redundant CU — models the redundant work DU does (per-site memo tables,
  decoupled binding arrival), used as the proof bridge.
- **Constraint system** `(D, C)`: D = **rational trees** (trees with finitely many
  subtrees; can contain cycles), C = equalities. Binding = "telling" a constraint =
  unification.

**The 6 nonbind DU rules** (identical to CU nonbind rules, acting per-site): INTERCHANGE,
MEMO, DEREFERENCE, IDENTIFY, CONFLICT, DECOMPOSE. CONFLICT flags inconsistency on the
site that causes it.

**The 4 bind rules** (replace CU's single global BIND rule; implement coherent
**variable elimination**):

- **INITIATE** — on site s, for `(x = u)_s` with `less(u,x)` and `x ∉ lhs(Σ_s)`: put a
  binding initiation `(x ← −)_s` locally and emit a binding request `x ∼ u`. The
  `(x ← −)_s` + the `x ∉ lhs` guard ensure **only one binding attempt per site**.
- **WIN** — at the owner, on `x ∼ u` with `unbound(x)`: send `(x ⇐ u)` to **all sites**
  (`∀i ∈ S`) and set `bound(x)`. The first request wins.
- **LOSE** — at the owner, on `x ∼ u` with `bound(x)`: `skip` (request ignored / retried
  by initiator after it gets the binding).
- **ARRIVE** — on site s, `(x ⇐ u)_s` with `x ∈ var(Σ_s)`: install `x ← u` locally and
  drop the local initiation `(x ← −)_s`.

> [Table I — distributed-setting actions, verbatim] "`x ∼ u`  Binding request;
> `(x ⇐ u)_s`  Binding in transit to site s." And: "the action `x ∼ u` represents a
> message requesting the binding of x to u. For a given x, exactly one such action will
> cause a binding to be made; all others are discarded."

**Dereference chains — local-vs-distributed trade-off (relevant to transport copy vs
pointer-follow):**

> "A major difference between CU and DU is that CU always constructs dereference chains,
> whereas DU with eager variables forbids dereference chains to cross sites. Instead, DU
> copies remote terms to make them local. ... In a distributed setting, pointer
> dereferencing across sites is slow, and it makes the current site dependent on the
> other site. This makes copying terms preferable."

**Binding-cycle avoidance (relevant to GLP writer-MGU never binding writer-to-writer):**
A total order `less(u,v)` over terms (nonvariables < variables; variables totally
ordered) is used so that, e.g., x bound to y *and* y bound to x cannot both happen.
Verbatim:

> "The algorithm uses the order to avoid creating binding cycles (e.g., x bound to y and
> y bound to x). This is especially important in a distributed setting."

---

## 9. Correctness theorems (stated; relevant when GLPnet needs a soundness argument)

- **Logical Equivalence Property (CU):** "In every transition c_i → c_{i+1} of every
  execution of the CU algorithm, the logical equivalence ε(c_i) ↔ ε(c_{i+1}) holds under
  the standard equality theory." (Proof: standard unification theory.)
- **Entailment Property / CU Total Correctness (corollary):** any initial config reaches
  a terminal config logically equivalent to the start; if no `false` actions, the store
  entails the initial action.
- **Off-line total correctness (DU):** proved by a mapping `m` from any distributed
  config `(A;Σ;M)` to a centralized config; **safety** (Sec 6.3: `m(e)` is a correct
  centralized execution) + **liveness** (Sec 6.4: every execution eventually makes
  progress; nonbind rules + WIN are *progressing* rules).
- **On-line case** (equations introduced at any time): needs **weak fairness** + the
  **finite-size property**. Theorems: *Liveness of On-line DU*, *Finite Entailment of
  On-line DU* — "e will eventually contain either a `false_s` action or a store on site
  s that entails u = v."

> [Finite-size property, verbatim] "if it is intended by the programmer to be finite it
> will actually be finite during the execution. These two conditions [weak fairness +
> finite-size] suffice for all practical programs we know of."

---

## 10. Mozart implementation (§8) — concrete protocol GLPnet can mirror per transport

The Mozart algorithm = refined on-line DU, split into a **local algorithm** (Fig. 23,
the nonbind rules, runs in each unifying thread, memo table via *forwarding pointers*)
and a **distributed algorithm** (Fig. 24, the bind rules + globalization + registration).

**Distribution graph (§8.2):** language graph nodes = {record, unbound variable, thread}.
When a variable node is referenced from another site, it is replaced by an **access
structure**: one **proxy node Pi per referencing site** + one **owner/manager node M**.
A var referenced on >1 site is "certain to be represented by an access structure." A
**distributed** variable = one implemented as an access structure; otherwise **local**.

**Five node types + message interfaces (Fig. 21):** Record, Local variable, Proxy, Owner
(Manager), Thread. Node state (Table IV):

- Local variable: `state ∈ {UNBOUND, BOUND(Node)}`, `eager ∈ {FALSE, TRUE}`.
- Proxy: `state ∈ {UNBOUND, INITIATED, BOUND(Node)}`, `eager`, `reg ∈ {FALSE, TRUE}`,
  `owner: NodeId`.
- Owner (MANAGER): `state ∈ {UNBOUND, BOUND(Node)}`, `reglist: set of NodeId`.

**The distributed algorithm (Fig. 24), the message protocol, verbatim semantics:**

- **WIN:** `Receive(M.id, binding_request(N)) ∧ M.state=UNBOUND` → `∀i ∈ M.reglist:
  Send(i, binding_in_transit(N)); M.state ← BOUND(N)`.
- **LOSE:** `Receive(M.id, binding_request(_)) ∧ M.state=BOUND(_)` → `skip`.
- **ARRIVE:** `Receive(P.id, binding_in_transit(N)) ∧ (P.state=UNBOUND ∨
  P.state=INITIATED)` → `∀i ∈ proxyids(N): Send(i, reg); P.state ← BOUND(N)`.
- **Variable registration:** proxy sends `register(P.id)` to owner; owner adds it to
  `reglist` if unbound, or immediately sends `binding_in_transit(N)` if already bound.

**Globalization (§8.5.1) — how a local var becomes distributed (= what GLPnet's link
primitive does when a variable first crosses an instance boundary):**

> "Newly created variables are always local. When a message is sent referencing a local
> variable, then a new distributed variable is created, and the local variable is bound
> to it. This is called *globalizing* the local variable. An access structure is created
> when a local variable is globalized. When the message arrives then a new proxy will be
> created for the distributed variable if none exists on the arrival site. ... The
> inverse operation, *localization*, ... The distributed variable becomes a local
> variable again."

**Five Mozart optimizations / extensions (directly useful as a GLPnet checklist):**

1. **Variable registration** — bindings sent only to *registered* sites, not all.
2. **Grouping nested data structures** — bind a whole tree in one operation; avoids
   creating distributed vars for intermediate nodes (DU would create x2, x3; Mozart
   never does). *Maps to GLPnet "ground-only / per-hop globalization" corpus entry 14.*
3. **Winner optimization** — the winning proxy already has N, so the owner sends it a
   simple `binding_ack` instead of resending the term (avoids R-BIND redundant work).
4. **Asynchronous streams (preregistration)** — destination preregistered without
   waiting for a registration message, *requires a FIFO connection*; lets stream
   elements be added asynchronously (no round-trip per element).
5. **Lazy/eager + read-only logic variables + distributed GC (credit/weighted-reference
   counting) + a failure model** (3-valued operation results: succeed / wait / abort;
   no default time-outs; user decides).

**Read-only logic variables (§8.1.3.2) — the GLP-reader analog:**

> "Standard logic variables have two operations, reading the value and binding. For
> security reasons, it is often useful to prohibit binding, for example, when building
> abstractions or when passing the variable to a less-trusted site. ... [footnote:]
> Read-only logic variables are confusingly called 'futures' in these two references."

This is the closest Oz construct to a **GLP reader X?** that may observe but never write
— useful precedent for designing the *reader side* of a distributed GLP link primitive.

---

## 11. Related-work positioning (§9) — the rest of the GLPnet corpus in one map

The paper itself surveys the same prior-art landscape the GLPnet thread is building:

- **Concurrent logic languages (§9.1):** Flat GHC on Multi-PSI [Ichiyoshi 1987]; Parlog
  [Foster 1988] — owner/proxy-like, orders variables to avoid cycles, lazy remote refs,
  dereference chains may cross sites, no preregistration; Pandora, D/C-Parlog; **DRL**
  [Diaz 1997] — "logic channel" statically marked input/output, only one channel marked
  output, binding output causes term to appear at all input channels (a direct conceptual
  cousin of GLP `ch(In, Out?)`); **KLIC** [Fujise 1994] / distributed KLIC [Rokusawa
  1996] — but "binding cycles can be created ... inconsistencies are ignored ... a
  variable may be bound to different values on different sites" (only safe where
  inconsistency is impossible).
- **Languages not based on logic (§9.2):** futures/I-structures [Halstead; Arvind &
  Thomas]; two-level addressing in **Java RMI, CORBA, Erlang/OTP** — can be extended to
  "weak logic variables" by adding an "unknown" state.
- **Sending a bound term (§9.3):** [Lamma 1997] "consumption specifications" send only
  the part of a term a consumer needs (e.g., list-append needs only the spine) — relevant
  to GLPnet partial-term transport.

> [§9.1.1, Foster's Parlog, verbatim] "Variables exist on one site and have remote
> references, which is similar to the owner/proxy model of the Mozart algorithm.
> Variable-variable unification avoids binding cycles by ordering the variables, as is
> done in the DU algorithm."

---

## 12. Direct take-aways for the GLPnet multi-protocol link layer

1. **Owner/proxy is the proven topology** for one logical variable shared across N
   instances. A GLPnet link primitive should designate, per distributed variable, an
   **owner instance** (winner-decider) and **proxy instances** (one per REPL holding the
   variable). This matches "ONE program parameterized by a per-instance goal that
   designates its role."
2. **The 4-message-class protocol (request / win / lose / arrive)** is the minimal
   coherent variable-elimination handshake; GLPnet must carry these 4 message classes
   over *each* transport (MQTT/AMQP/CoAP/HTTP2/3/XMPP/DDS/WS/SSH/FTP/SFTP/BLE...). All
   are point-to-point except WIN's owner→all-proxies fan-out (a *reliable multicast* if
   the transport supports it). **T1 (MQTT/XMPP broker-mediated vs strict bilateral p2p):**
   the protocol itself is logically bilateral owner↔proxy; a broker is just the transport
   for those bilateral messages — broker-mediation does not break the logical bilaterality.
3. **Eager vs lazy is the suspension knob.** GLP's suspension-on-unbound-reader maps to
   the *lazy proxy* (request the binding only when a goal needs the value); GLP eager
   broadcast maps to eager proxies. Either is implementable by scheduling one rule.
4. **Preregistration requires FIFO** — relevant to choosing transports for GLP streams:
   asynchronous GLP channel sends without per-element round-trips need an in-order
   transport (TCP/QUIC/AMQP-ordered; **not** raw UDP/CoAP-NON, not unordered MQTT QoS0).
5. **GLP-specific divergences to preserve (do NOT import from Oz):** strict SRSW
   single-writer (Oz allows consistent multiple assignment); writer-MGU never binds
   writer-to-writer (Oz binds variable-variable freely); GLP suspension instead of
   eager term copying as the default; no cells/state in the link primitive's contract.
6. **Correctness obligation:** if GLPnet wants a soundness claim, the off-line/on-line
   proof template (map distributed execution → centralized via `m`; show safety +
   liveness; require weak fairness + finite-size) is the citable precedent.
7. **Grouping nested structures + "sending a bound term"** confirm the GLPnet corpus
   entry 14 direction (ground-only / per-hop globalization, partial-term transport):
   bind/transmit whole ground subtrees in one operation; send only the part a consumer
   needs.

---

## 13. Provenance / access notes

- Full text retrieved 2026-06-06 from the authors' open copy:
  `https://www.ps.uni-saarland.de/Publications/documents/Toplas_99.pdf` (550 KB, 58 pp.,
  read in full pp. 1–58). Canonical record: ACM DL `10.1145/319301.319347` (paywalled
  there; abstract mirror at mozart2.org had an invalid TLS cert at retrieval time).
- All verbatim quotes above transcribed from the PDF pages; figure/section numbers are
  the paper's own.
