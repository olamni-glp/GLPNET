---
title: "Transmitting partially-instantiated terms over ground-only transports; per-hop globalization of every embedded variable (madGLP global_send / globalize / localize)"
authors: "Local specs (madGLP-spec v5.3, guards-reference) + live Dart implementation (global_send.dart, mad_context.dart, mad_helpers.dart); upstream: Ehud Shapiro et al. (CGLP / 'Implementing Grassroots Logic Programs with Multiagent Transition Systems and AI')"
year: "2026"
source_url: "D:/bstdev/research/glp/glpnet/docs/ma/madGLP-spec.md ; D:/bstdev/research/glp/glpnet/glp_runtime/lib/multiagent/{global_send.dart,mad_context.dart,mad_helpers.dart} ; D:/bstdev/research/glp/glpnet/docs/guards-reference.md ; corroborating upstream https://arxiv.org/abs/2602.06934 (arXiv:2602.06934)"
retrieved: "2026-06-06"
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — global_send fires on the onWriterBound callback when a watched reader becomes 'known' (known/1, not ground/1). For transports that can only carry GROUND payloads (e.g., BLE GATT, send_to_ui uses ground/1; no_readers/1 for UI), how are partially-instantiated terms (compound with embedded unbound readers/writers) transmitted, and does globalization correctly create new links for EVERY embedded variable on each hop?"
precedence_class: glp-current
access: full-text
---

# Ground-only transports vs. partially-instantiated terms; per-hop globalization

## Question

`global_send` fires (via `onWriterBound`) when a watched reader becomes **known** (`known/1`, not `ground/1`).
For transports that can only carry **ground** payloads (e.g., BLE GATT; `send_to_ui` uses `ground/1`;
`no_readers/1` for interactive UI), how are partially-instantiated terms — compounds with embedded
unbound readers/writers — transmitted, and does globalization correctly create a **new link for every
embedded variable on each hop**?

## Direct answer (one paragraph)

There are **two distinct output disciplines** in the local model, and the answer depends on which one a
transport maps to:

1. **The madGLP inter-agent path (`global_send` → globalize → wire → localize)** is *designed* to carry
   partially-instantiated terms. `global_send`'s guard is `known/1`, which succeeds on a compound term
   even if it still contains unbound readers/writers (e.g. `[add | Xs1]`, `value(V?)`). Globalize then
   **recurses over the whole term** and, for **every** embedded variable, creates a fresh global link on
   this hop — an entry `(Y,q)` for each embedded *writer* (`→ _w(p,i)`), and a spawned
   `global_send(Y?, _r(p,i), q)` for each embedded *reader* (`→ _r(p,i)`). The receiver's Localize
   recurses symmetrically and mints a **fresh local writer/reader pair per global name**. So open
   structures (streams, reply variables) ARE carried, and the link set is rebuilt hop-by-hop. This is the
   maximally-transparent path.

2. **The ground-only UI path (`send_to_ui` / `'_send_to_ui'`)** is a *different builtin* that does **NOT**
   globalize at all. It guards on `ground/1` (or `no_readers/1` for the interactive `ui_agent`), so it
   simply **suspends** until the term is fully instantiated (or reader-free) and then ships ground bytes
   with no link creation. No partially-instantiated term ever crosses it; it degrades to ground-only.

**Implication for the new link primitives:** a transport reachable via the madGLP globalize/localize seam
preserves open-structure transparency *for free* (the per-hop link rebuild already exists in code). A
transport wired to the `send_to_ui` discipline (ground-only, e.g. naive BLE GATT) can only do ground RPC.
The design choice for B2 is therefore: route each transport through the globalize/localize seam (so
embedded variables become further sub-links carried by *the same or sibling transport*), rather than
through the `_send_to_ui` ground gate.

---

## Load-bearing verbatim quotes

### 1. The guard is `known/1`, not `ground/1` (compounds with embedded vars pass)

madGLP-spec §4, *The global_send Predicate*:

> ```prolog
> global_send(T, G, Q) :- known(T) | '_send'(T, G, Q).
> ```
> "The guard `known(T)` succeeds when T is bound to a non-variable term."

guards-reference.md, `known(X)`:

> "Success: X bound to constant (number/string) or compound term (may contain unbound subterms)"
> "**Difference from ground**: `known(f(Y))` succeeds even if Y is unbound, because the structure f(Y)
> itself is a compound term. `ground(f(Y))` would suspend waiting for Y to be bound."

Upstream CGLP paper (arXiv:2602.06934) corroborates verbatim: *"global_send(T, G, Q) :- known(T) |
'_send'(T, G, Q)."* … *"The guard known(T) succeeds when T is assigned a non-variable term."* — i.e.
"**not `ground/1` but rather `known/1`, which accepts any assigned (non-variable) value, including
compound terms still containing variables.**"

### 2. Globalize recurses and creates a NEW link for EVERY embedded variable (per hop)

madGLP-spec §5.1, *Globalize* — "For each variable Y occurring in T":

> "1. **If Y is a writer**: allocate the next index i, replace Y in T_p↑ with `_w(p, i)`, and create
> entry `(Y, q)` at index i in W'_p. No goal is spawned…
> 2. **If Y? is a reader**: allocate the next index i, replace Y? in T_p↑ with `_r(p, i)`, and spawn goal
> `global_send(Y?, _r(p,i), q)` into p's resolvent. No entry is created…"

madGLP-spec §5.4, *Exporting Both Ends of a Pair* — concrete proof that the recursion creates one link
per embedded variable (and indices are per-occurrence):

> "Consider agent p exporting term `[X, X?]` to agent q. Globalize processes both … Writer X: entry
> `(X, q)` at index 1, no spawn. Reader X?: spawns `global_send(X?, _r(p,2), q)`, no entry."

madGLP-spec §10.1 Stage 1 — proof that this happens **on each hop**, on a partially-instantiated term:

> "p assigns Xs := [add|Xs1]: 1. Xs? becomes known (= [add|Xs1]) 2. `global_send(Xs?, _r(p,1), q)` fires
> 3. The term [add|Xs1] is globalized for q: Xs1 is a writer, so entry `(Xs1, q)` at index 2 in W_p,
> becomes `_w(p,2)` 4. Message `_r(p,1) := [add|_w(p,2)]` sent to q"

i.e. the **tail `Xs1` (an embedded writer) gets its own fresh link** created on this hop; only the head
`add` is ground and travels as data.

§10.2 shows the same for a reader nested inside a struct (`value(V?)`):

> "p assigns Xs1 := [value(V?)|Xs2], exporting reader V? and writer Xs2: 1. Globalize V? (reader): spawns
> `global_send(V?, _r(p,3), q)`, becomes `_r(p,3)` 2. Globalize Xs2 (writer): entry `(Xs2, q)` at index 4
> … 3. Message `_w(p,2) := [value(_r(p,3))|_w(p,4)]` sent to q"

Upstream paper corroboration: *"every embedded variable generates its own entry or global_send goal;
compound terms containing multiple variables trigger multiple global link creations."*

### 3. Localize recurses symmetrically — a fresh local pair per global name (links rebuilt on receive)

madGLP-spec §5.2, *Localize* — "For each global name in T_p↑":

> "1. **If `_w(p, i)`**: create fresh local pair `(Y_q, Y_q?)`, replace `_w(p, i)` with Y_q … and spawn
> goal `global_send(Y_q?, _w(p,i), p)` …
> 2. **If `_r(p, i)`**: create fresh local pair `(Z_q, Z_q?)`, allocate the next index k in W'_q, add
> entry `(Z_q, p, i)`, and replace `_r(p, i)` with Z_q? (the reader) …"

So on the receiving hop the term is reconstructed with **brand-new local writer/reader cells**, and a
**new outgoing/incoming link** is attached to each — the per-hop link rebuild that makes multi-hop
forwarding (§10.3 Charlie→Bob→Alice) work.

### 4. The ground-only UI path is a SEPARATE builtin that does not globalize

madGLP-spec §12.4 / §12.5:

> ```prolog
> send_to_ui([X|In]) :- ground(X?) | '_send_to_ui'(X?), send_to_ui(In?).
> ```
> "**Guard Requirement**: The `ground(X?)` guard ensures no unbound variables cross the GLP-Dart
> boundary. UI messages must be fully instantiated."

> "The `'_send_to_ui'(T)` builtin … This builtin does **NOT**: Globalize T (no variables, so nothing to
> globalize) … Create global links (purely local to isolate)."

madGLP-spec §12.6, *Comparison: Network vs UI Output*:

> | Aspect | Network (`send_to_net`) | UI (`send_to_ui`) |
> | Globalization | Yes (creates global links) | No |
> | Variables allowed | Yes (globalized) | No (must be ground) |
> | Guard | `known(T)` (in `global_send`) | `ground(X?)` |

§12.7 — the **`no_readers/1`** middle ground for *interactive* UI (writers OK, readers not):

> "The `no_readers` guard allows writers in output, enabling the query-response pattern where users bind
> writers to provide input."

guards-reference.md, `no_readers(X?)`: *"Success: X? is bound to a term containing no readers (ground
terms and/or writers only) … Suspend: X? contains any readers"* — and *"`no_readers(f(Y))` where Y is a
writer: succeeds; `no_readers(f(Y?))` where Y? is an unbound reader: suspends."*

---

## Live-implementation confirmation (current truth, highest precedence)

`global_send.dart::GlobalSendRegistry.onWriterBound` (fires on the watched writer's bind):
extracts variables from the *whole* value via the injected `extractVariables`, then calls `globalize(...)`,
and returns `newGoals` for every spawned reader-link. One-shot per link (`_goals.remove(writerAddr)`).

`mad_context.dart::_extractTermVarsRecursive` — recursion is over `StructTerm.args`, so **every embedded
variable** in a compound is collected (ConstTerm contributes none):

```dart
if (term is VarRef) { ... result.add(TermVar.reader/writer(...)) }
else if (term is StructTerm) { for (final arg in term.args) _extractTermVarsRecursive(arg, result); }
// ConstTerm has no variables
```

`mad_context.dart::_fireGlobalSendGoalIfExists` — after globalizing the value with
`globalizeTermWithResult`, it **registers a new global_send goal AND a heap `onBind` callback for every
nested link** ("for (final newGoal in result.newGoals) { globalSendRegistry.register(newGoal);
runtime.heap.onBind(newGoal.readerAddr, ...) }") — i.e. the per-hop link set is materialized in the heap,
not just on the wire.

`mad_helpers.dart::globalize` — one branch per `TermVar`: writer ⇒ `table.addGlobalizeEntry` (+ `_w`),
reader ⇒ `table.allocateIndex` + `GlobalSendSpawn` (+ `_r`). `globalizeTermWithResult` /
`_substituteGlobalNames` recurse over `StructTerm.args`, replacing each `VarRef` with `_w(agent,i)` /
`_r(agent,i)`. The receive side (`_handleWriterAssignment` / `_handleReaderAssignment` /
`_handleSerializerAssignment`) calls `extractGlobalNames` (recursive over args) → `localize` →
`localizeTermWithResult`, then `registerGlobalSendSpawns`, rebuilding links per global name on receive.

**Caveat (live, not spec):** `agent_runtime.dart` boot path comments note the **user input stream injects
ground terms** ("Dart injects ground terms"). The Dart↔GLP boundary in the current app wiring is
ground-on-input and ground/no_readers-on-UI-output; the open-structure path lives entirely on the
*GLP-to-GLP* madGLP link (globalize/localize), not on the Dart I/O builtins.

---

## Answers to the two sub-questions, crisply

1. **How are partially-instantiated terms transmitted over a ground-only transport?**
   - Via the **madGLP link discipline** (`global_send`/globalize): the ground sub-parts travel as data;
     **each embedded unbound writer/reader is replaced on the wire by a ground global name** (`_w(p,i)` /
     `_r(p,i)` — themselves ground compounds of an agent atom + integer index), and a *separate* link is
     established to carry that variable's eventual value on a later hop. So even a "ground-only" wire can
     carry an *open* logical term, because the openness is encoded as ground global-name placeholders plus
     out-of-band links — the term is **never** sent with raw unbound cells in it.
   - Via the **`send_to_ui` / `'_send_to_ui'` discipline**: it cannot — it `ground/1`-suspends until the
     term is fully instantiated (or `no_readers/1`-suspends until reader-free). This path degrades to
     ground-only RPC and is the wrong seam for transparent open-structure transport.

2. **Does globalization create new links for EVERY embedded variable on each hop?** **Yes.** Both the spec
   (§5.1/§5.2/§5.4/§10.1–10.3) and the live code (`_extractTermVarsRecursive`, `globalize`,
   `globalizeTermWithResult`, `localize`, `registerGlobalSendSpawns` + heap `onBind`) recurse over the
   full term and mint exactly one global link per embedded writer (entry `(Y,q)`) and one per embedded
   reader (spawned `global_send`), with fresh indices, **on every hop**; the receiver mints a fresh local
   pair per global name and re-attaches a link to each. Multi-hop forwarding (§10.3) is precisely this
   per-hop rebuild repeated.

## Design takeaway for the multi-protocol link layer (B2)

- To keep the split program **maximally transparent** (streams, reply variables, open structures), the new
  link primitives MUST sit on the **globalize/localize seam** (the `known/1` + per-variable global-name
  substitution), NOT the `_send_to_ui` `ground/1` gate. The ground-name encoding (`_w(p,i)`/`_r(p,i)`) is
  exactly what lets a ground-only transport (e.g. BLE GATT writes, fixed-size CoAP payloads) still carry an
  open term — the embedded variables become *further sub-links* (potentially on the same or a sibling
  transport).
- The "ground-only RPC" degradation is a *policy choice of the UI builtins*, not a fundamental limit of the
  madGLP model. The model already transmits open structures by construction.
- **Proposal flag (language authority):** if a transport genuinely cannot carry the global-name placeholder
  encoding inline and must serialize a single ground blob (e.g. BLE LE-Audio BIS broadcast — see open
  sub-question T2, multi-reader vs SRSW), a *new* guard/primitive distinguishing "ground-now" from
  "globalizable-open" output would be needed — that is a **proposal requiring the engineer's explicit
  approval**, not an established primitive. Today only `ground/1`, `no_readers/1`, and `known/1` exist.
