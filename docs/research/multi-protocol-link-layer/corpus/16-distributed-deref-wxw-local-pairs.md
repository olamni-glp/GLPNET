---
title: "Distributed dereference, path-compression bounding, and WxW detection across an agent boundary — resolved via madGLP local-pairs-plus-global-link decomposition"
authors: "Ehud Shapiro (implementation paper); GLPnet local specs (heap-pointer-architecture-spec.md v3.4, madGLP-spec.md v5.3, glp-runtime-spec.txt v2.19)"
year: "2026"
source_url: "https://arxiv.org/abs/2602.06934 (Implementing Grassroots Logic Programs with Multiagent Transition Systems and AI); local: docs/heap/heap-pointer-architecture-spec.md, docs/ma/madGLP-spec.md, glp_runtime/lib/runtime/heap_fcp.dart"
retrieved: 2026-06-06
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Path compression and the WxW-detect-during-deref check are specified as mandatory (spec §4.3, §4.5). Across a distributed boundary where a reader points at a remote (virtual) writer, how is dereference/compression bounded and how is WxW detected when one writer is local and the other remote?"
precedence_class: glp-current
access: full-text
---

# Distributed dereference / path-compression bounding / WxW detection across an agent boundary

## Bottom line (answers the question directly)

**There is no dereference chain that physically crosses the network, and therefore no
cross-boundary path to bound or cross-boundary WxW pair to detect at deref time.** The GLP
runtime model never lets a local reader point at a *remote* writer cell. Instead, every
maGLP shared pair that would span two instances is decomposed (by madGLP / the imported-
variable mechanism) into **two fully-local writer/reader pairs joined by a global link**.
`derefAddr`, path compression (spec §4.3), and the WxW check (spec §4.5) all run on **one
in-memory `cells[]` array on one side**; the "remote" end is represented locally as either an
imported reader holding a `VariableEntry` (the *virtual writer*) or a local writer registered
in the global writers table. Deref terminates at that local boundary cell — it never chases a
pointer over the wire.

This is the load-bearing fidelity constraint for blocker **B2 (distributed unification)**: a
multi-protocol link primitive may carry *assignment messages* across a transport, but it must
**not** attempt to make deref/compression/WxW span instances. Those invariants are local-heap
invariants and stay local on each side.

---

## Source precedence applied

1. **glp-current (HIGHEST)** — local specs are current-implementation truth:
   `heap-pointer-architecture-spec.md` (deref §4, WxW §4.5, imported vars §10),
   `madGLP-spec.md` (local-pairs-plus-global-link decomposition §1, §5, §8, §11.3),
   `glp-runtime-spec.txt`, and the running code `heap_fcp.dart`.
2. **glp-paper** — Shapiro, *Implementing Grassroots Logic Programs with Multiagent
   Transition Systems and AI* (arXiv:2602.06934, 2026): the maGLP→madGLP derivation whose
   "key insight" is exactly this decomposition. Used here to confirm the local specs.
3. **earlier-cl-paper (inspiration only)** — FCP emulator (`kernels.c`/`emulate.c`
   bidirectional writer↔reader cells; tag-based deref): the *local* deref mechanism's
   provenance, already cited inside the local heap spec §1.2. Does not govern the
   distributed boundary.

No conflict between (1) and (2); (3) supplies only the single-instance deref mechanism.

---

## 1. The local deref / compression / WxW invariants being asked about

From `docs/heap/heap-pointer-architecture-spec.md` (v3.4), verbatim:

**§4.1 Definition.** "Dereferencing is the act of following a chain of references until
reaching the final object which is not a reference. As part of dereferencing, the initial
reference is updated to point directly to the final object (path compression). This is
integral to the design, not an optional optimization."

**§4.3 Path Compression Semantics.** "Path compression updates references to point directly
to the final target, bypassing intermediate cells. This ensures that repeated dereferences of
the same variable are O(1) after the first access. Compression is applied to the starting cell
only."

**§4.5 Invariant Check (WxW Detection).** "During dereferencing, if we follow a pointer and
land on a writer, the previous cell MUST have been a reader. This is because writer-to-writer
bindings are forbidden. ... The deref operation MUST check this invariant and throw if
violated ... even if a bug allows WxW binding to occur, deref will detect and report it."

These are explicitly **single-array** invariants. The spec's own algorithm assumes one
`cells[]` array: §4.5's check is `cells[current].tag == WrtTag && previousTag == WrtTag`. The
code (`glp_runtime/lib/runtime/heap_fcp.dart`, `derefAddr`, ~L259) implements them with a
`visited` set for cycle detection plus the `previousTag`/`current` WxW check — all over
`this.cells`.

---

## 2. Why the question's premise dissolves at the boundary: no remote pointer exists

A local reader is **never** allowed to hold a `Pointer` into another instance's heap. The two
boundary representations, both local, are:

**(a) Imported reader holds a `VariableEntry` (the "virtual writer").** From
`heap-pointer-architecture-spec.md` §10 (Imported Variables), verbatim: "For multiagent GLP,
imported readers have no local writer. The reader cell contains a VariableEntry (virtual
writer) instead of a Pointer ... The VariableEntry serves as the 'virtual writer' ... When
`derefAddr` encounters an imported reader (cell content is VariableEntry), it returns the
VariableEntry directly. Callers should treat this as 'unbound' and suspend the goal, similar
to encountering an unbound local writer."

The code matches: in `derefAddr`, the `RoTag` branch checks `cell.content is VariableEntry`
and **returns the entry (or its cached `boundValue`) immediately** — it does not follow a
pointer. So the deref *stops at the local boundary cell*; it never walks toward a remote
writer. The WxW check (`previousTag == WrtTag && cell.tag == WrtTag`) is structurally
unreachable across the boundary because there is no cross-boundary pointer hop to make
`previousTag` carry over from a remote cell.

**(b) The madGLP decomposition: two local pairs, one global link.** From
`docs/ma/madGLP-spec.md` (v5.3), verbatim:

§1.1: "A maGLP shared variable pair `(X, X?)` with writer X at agent p and reader X? at agent
q is implemented by two local pairs connected by a global link: At agent p: a local pair
`(X_p, X_p?)` where both variables remain in p's resolvent; At agent q: a local pair
`(X_q, X_q?)` where both variables remain in q's resolvent; A global link connecting X_p to
X_q, realized as a `global_send` goal at the writer-owner and an entry in the reader-owner's
global writers table."

§11.3 (Heap Representation), verbatim and decisive: **"No special representation is needed for
'imported' variables—all variables are local pairs. The global writers table provides routing
information separately from the heap representation."**

So on *each* side the heap is a normal local two-cell pair. Deref/compression/WxW operate
exactly as in the single-instance case; the cross-instance binding is delivered later as an
*assignment message*, not as a pointer to chase.

---

## 3. How the binding actually crosses (and why deref still never crosses)

The value moves by **message**, applied by a local `assign`, after which the receiver's own
local deref sees an ordinary local binding:

- madGLP §4 `global_send/3`: `global_send(T, G, Q) :- known(T) | '_send'(T, G, Q).` —
  outgoing communication is a spawned GLP goal that fires only when the *local* reader T
  becomes known; it globalizes T and enqueues a message. No remote deref.
- madGLP §8.3 Receive: on `_w(p,i) := T↑` / `_r(p,i) := T↑`, the receiver "finds entry
  `(X, q)` / `(X_q, p, i)` ... assign X := T_p↓ ... reactivate suspended goals, and remove the
  entry". The assigned writer is **local**; subsequent `derefAddr` of its reader is a normal
  local deref.
- madGLP §5.3 Globalize-Localize Correspondence and §10 examples (client-monitor,
  friend-mediated introduction) show the value flowing p→…→q strictly through *local writer
  assignments + messages*, with each agent's local pair acting as a forwarding point.

Termination of deref is therefore bounded **locally** on every side: the chain length is
bounded by the local term's structure (the SO/SRSW single-occurrence invariant — see
`glp-runtime-spec.txt` L188: "Runtime must fail on writer-to-writer term matching attempts
(WxW)") plus the cycle-detection `visited` set in `derefAddr`. There is no network round-trip
inside deref, so deref cannot block on or be unbounded by remote state.

---

## 4. WxW when "one writer is local and the other remote"

Because the remote writer is never a local cell that a local reader points at, the WxW
condition the question worries about **cannot arise as a deref-time pointer hop**. The two
cases that *can* occur are both handled without a cross-boundary deref:

1. **Local reader, remote writer (imported reader).** Local deref returns the
   `VariableEntry`/virtual writer and the goal suspends locally (§10). The eventual remote
   value arrives as an assignment message (§8.3) and binds the *local* virtual writer; only
   then does a local deref succeed. No remote writer is ever dereferenced.

2. **Both ends exported (forwarding).** madGLP §5.4 / §8.3 "Automatic Forwarding": the agent
   holds a *local* pair `(X, X?)`; an inbound message binds local writer X, X? becomes known,
   and a watching `global_send` forwards onward. WxW between the two link ends is impossible
   because they are two **separate** local pairs joined by a global writers-table entry, not a
   writer bound to another writer. The SRSW/SO invariant (one writer + one reader per
   variable per resolvent) is preserved on each side independently (madGLP §13 "SRSW
   Property").

Consequently, a distributed link primitive must implement the **assignment-message** path,
not a distributed pointer. The WxW hard-fail and path compression remain *local* invariants on
each instance and are automatically satisfied by reusing the existing single-instance
`derefAddr`.

---

## 5. Confirming Shapiro paper (precedence-2)

Shapiro, *Implementing Grassroots Logic Programs with Multiagent Transition Systems and AI*,
arXiv:2602.06934 (2026). Verbatim:

- Abstract: "The key insight is that maGLP shared variable pairs spanning agents can be
  implemented as **local variable pairs connected by global links**, with correctness
  following from disjoint substitution commutativity (from GLP's single-occurrence invariant)
  and persistence."
- §5.2 (after Def. 5.10): "a maGLP shared variable pair (X,X?) with writer X at agent p and
  reader X? at agent q can be implemented by two local pairs connected by a *global link*."
- §6.1 (after Def. 6.1): "Global variable names appear only in messages between agents, never
  in resolvents." (i.e. `_w(p,i)`/`_r(p,i)` are wire names, not heap pointers.)
- Correctness basis: SO invariant gives disjoint writers across distinct goals (Lemma ~3.32:
  "By the SO invariant, each writer occurs at most once ... they have disjoint writers"),
  GLP is persistent (Lemma 3.17), maGLP transactions are monotonic (Lemma 5.4) — these are
  what make the deferred, message-applied binding equivalent to the abstract shared-variable
  binding *without* a distributed deref.

This is fully consistent with the higher-precedence local specs.

---

## 6. Implications for the multi-protocol link layer (B2)

- A link primitive replacing a shared variable across instances is the **transport for an
  assignment message**, parameterized by the per-instance role (writer-node vs reader-node),
  exactly mirroring globalize (writer-owner spawns the equivalent of a `global_send`) and
  localize (reader-owner registers the equivalent of a global-writers-table entry).
- Deref, path compression (§4.3), and the WxW hard-fail (§4.5) are **NOT** to be made
  distributed. They stay local on each side; the link only delivers `T↑`/`T↓`.
- The bilateral / strict-p2p framing fits the local-pairs model 1:1 (each side has its own
  pair; the link is the global link). Broker-mediated transports (MQTT/XMPP — open T1) and
  one-to-many BLE BIS (open T2) must still terminate in a **single** local writer per
  reader-side pair to preserve SRSW; multi-reader fan-out cannot be expressed as one shared
  variable and would require N independent links.
- Any "make deref span the wire" design contradicts spec §10/§11.3 and the paper's key
  insight — flag as a defect, not a feature.
