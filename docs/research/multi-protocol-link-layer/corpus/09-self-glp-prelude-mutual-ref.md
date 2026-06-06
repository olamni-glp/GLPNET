---
title: "self.glp (root prelude) — Channel/stream model + MutualRef merge idiom (MutualRef spec)"
authors: "GLP / glpnet runtime (Udi Shapiro lineage; glpnet local implementation)"
year: "2026"
source_url: "D:/bstdev/research/glp/glpnet/programs/self.glp ; D:/bstdev/research/glp/glpnet/docs/mutual-ref-spec.md ; D:/bstdev/research/glp/glpnet/docs/typed-glp-manual.md (§4–§9) ; D:/bstdev/research/glp/glpnet/programs/bonds_v2/self.glp (merge/3)"
retrieved: 2026-06-06
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Fetch, preserve & extract source: self.glp (root prelude) + mutual-ref-spec.md"
precedence_class: glp-current
access: full-text
---

# self.glp (root prelude) + MutualRef spec — the canonical stream/channel model

**Why this is in the corpus.** These are local glp-current specs: the *highest* authority
under SOURCE PRECEDENCE. They define the exact stream/channel atoms that the new distributed
link primitives must replace and compose with — `Channel`, `new_channel`, `send`, `receive`,
the binary `merge/3` fan-in, and the O(1) `mwm` multiway merge built on `MutualRef`. Any
scheme that "splits the shared logic variable across two REPL instances" must keep these
type signatures and SRSW behaviours observationally identical (B2 fidelity yardstick).

---

## 1. Canonical types (root prelude `programs/self.glp`, `-mode(system)`)

Verbatim type definitions (these are the building blocks a link primitive carries across instances):

```prolog
% Collections (parameterized)
Stream(X)     ::= [] ; [X | Stream(X)].
OpenStream(X) ::= [X | Stream(X)].
DiffList(X)   ::= Stream(X) \ Stream(X)?.

% Communication (parameterized)
Channel(In, Out) ::= ch(In, Out?).
```

Load-bearing facts:
- A **stream** is an (open or closed) cons-list whose tail is itself a stream; closure is the
  empty list `[]`. An *open* stream has an unbound writer at the tail. This unbound tail-writer
  IS the atomic writer/reader cell that a distributed link must transport.
- A **Channel** is a single struct `ch(In, Out?)` pairing two streams in *opposite* modes:
  position 1 `In` is the owner's **write/produce** side, position 2 `Out?` is the owner's
  **read/consume** side (the `?` marks the reader). A channel is therefore two half-duplex
  streams glued into one bidirectional term — the natural unit to span across instances.

---

## 2. Channel operations — single-unit-clause defined guards (root prelude)

Verbatim from `programs/self.glp`:

```prolog
% Channel operations
procedure new_channel(Channel(X, Y), Channel(Y, X)).
new_channel(ch(Xs?, Ys), ch(Ys?, Xs)).

procedure send(X?, Channel(Y, Stream(X))?, Channel(Y, Stream(X))).
send(X, ch(In, [X?|Out?]), ch(In?, Out)).

procedure receive(X, Channel(Stream(X), Y)?, Channel(Stream(X), Y)).
receive(X?, ch([X|In], Out?), ch(In?, Out)).
```

Extraction (the exact semantics a link layer must preserve):

- **`new_channel/2`** mints the two *ends* of one bidirectional channel from a single shared
  pair of streams. `new_channel(ch(Xs?, Ys), ch(Ys?, Xs))`: end A reads `Xs?` / writes `Ys`;
  end B reads `Ys?` / writes `Xs`. **The asymmetry is the whole mechanism** — what one end
  writes (`Ys`) the other end reads (`Ys?`), and vice versa. The type
  `new_channel(Channel(X,Y), Channel(Y,X))` encodes that the two ends are duals (`(X,Y)` vs
  `(Y,X)`). *This is precisely the "shared logic variable between writer X and reader X?" that
  the feature's core transform must split across two REPL instances: each `Xs`/`Ys` pair is
  one cross-instance link.*
- **`send/3`** is pure head construction (manual §6 "binding flows through head patterns, not
  body `=`"): `send(X, ch(In, [X?|Out?]), ch(In?, Out))` extends the produce-side stream by
  one cons cell `[X?|Out?]`, threading a fresh tail-writer `Out` for the next send. No body —
  it unfolds at compile time when used in guard position.
- **`receive/3`**: `receive(X?, ch([X|In], Out?), ch(In?, Out))` consumes one element off the
  consume-side stream head `[X|In]` and threads the tail `In`. The reader **suspends** if the
  head cell is unbound (three-valued unification → Suspend), reactivating when the remote
  writer binds it. **This suspend-on-unbound-head is the semantic the distributed transport
  must reproduce remotely**: a `receive` on a split channel must block until the remote
  instance's `send` has delivered the binding over the wire.

Manual §4.2 (verbatim): *"When a procedure takes `Channel?` as input: Position 1 becomes
`Stream?` (consume ↓) — reading from the channel; Position 2 becomes `Stream` (produce ↑) —
writing to the channel."* I.e., consuming `Channel?` inverts the view to `ch(In?, Out)`:
read from `In?`, write to `Out`.

Compile-time behaviour (manual §8.2): in guard position the PE unfolds these. E.g.
`play :- new_channel(AliceCh, BobCh) | alice(AliceCh?), bob(BobCh?).` partially evaluates to
`play :- alice(ch(Xs?, Ys)?), bob(ch(Ys?, Xs)?).` — the channel disappears into two raw
stream pairs. **A distributed link primitive cannot be a pure unit-clause unfolded at compile
time** (its two ends live in different instances), so it must be a *runtime* kernel akin to
`mwm`/`stream_append`, not a `self.glp` defined guard.

---

## 3. Binary fan-in: `merge/3` (canonical two-stream merge under SRSW)

NOT in the root prelude — defined per-program. Canonical verbatim form
(`programs/bonds_v2/self.glp` / `bonds_v2/agent.glp`, identical):

```prolog
procedure merge(Stream(X)?, Stream(X)?, Stream(X)).
merge([X|Xs], Ys, [X?|Zs?]) :- merge(Ys?, Xs?, Zs).
merge(Xs, [Y|Ys], [Y?|Zs?]) :- merge(Xs?, Ys?, Zs).
merge([], Ys, Ys?).
merge(Xs, [], Xs?).
```

(An archive variant `programs/archive/udi/circular_merge.glp` ends with `merge([], [], []).`
instead of the two pass-through closure clauses — a stricter both-must-close form.)

Extraction:
- Two reader inputs (`Stream(X)?`, `Stream(X)?`), one writer output (`Stream(X)`). The two
  recursive clauses **swap the argument order** (`merge(Ys?, Xs?, Zs)`) to alternate which
  input is polled first → *fair* interleaving, and to avoid starving one side.
- SRSW is honoured: each variable occurs once as writer, once as reader per clause; the
  output element `[X?|Zs?]` reads the consumed `X` and reads the recursive tail `Zs`.
- This is the **prior art for fan-in under SRSW** the prompt asked for: it folds *two* writers
  into *one* reader stream without violating single-reader/single-writer, because the merge
  *process itself* is the sole reader of each input and sole writer of the output. A
  distributed N-way fan-in (e.g. T2's BLE one-to-many) must be expressed as a tree/chain of
  such single-reader merges or via `mwm` (§4) — never as a literal multi-reader variable.

---

## 4. O(1) multiway merge: `mwm` + MutualRef (the durable merge idiom)

### 4.1 Root-prelude `mwm` clauses (verbatim, `programs/self.glp`)

> *Comment in self.glp:* "MWM types and procedure declarations are omitted from the scope
> chain because the type system's DFA builder cannot handle compound type constructors like
> `stream(Stream)` in `MwmInput`. The clauses below are compiled for runtime use only — they
> are not type-checked."

```prolog
mwm(In, Out?) :-
    '_allocate_mutual_reference'(Ref, Out),
    mwm_main(In?, Ref?).

mwm_main(In, Ref) :-
    is_mutual_ref(Ref?) |
    mwm1(In?, Ref?, done, Done),
    close_when_done(Done?, Ref?).

mwm1([stream(Xs)|Streams], Ref, L, R?) :-
    is_mutual_ref(Ref?) |
    mwm_copy(Xs?, Ref?, L?, M),
    mwm1(Streams?, Ref?, M?, R).
mwm1([merge(NewIn)|Streams], Ref, L, R?) :-
    is_mutual_ref(Ref?) |
    mwm1(NewIn?, Ref?, L?, M),
    mwm1(Streams?, Ref?, M?, R).
mwm1([], _, L, L?).

mwm_copy([X|Xs], Ref, L, R?) :-
    is_mutual_ref(Ref?) |
    stream_append(X?, Ref?, Ref1),
    mwm_copy(Xs?, Ref1?, L?, R).
mwm_copy([], _, L, L?).

close_when_done(done, Ref) :-
    is_mutual_ref(Ref?) |
    close_mutual_reference(Ref?).

stream_append(Value, RefIn, RefOut?) :-
    is_mutual_ref(RefIn?) |
    '_stream_append'(RefIn?, Value?, RefOut).

close_mutual_reference(Ref) :-
    is_mutual_ref(Ref?) |
    '_close_mutual_reference'(Ref?).
```

Runtime-primitive declarations (root prelude) backing the above:

```prolog
% MWM (Mutual Write Merge) runtime primitives
procedure _allocate_mutual_reference(_, _).
procedure is_mutual_ref(_?).
procedure _stream_append(_?, _?, _).
procedure _close_mutual_reference(_?).
```

### 4.2 MutualRef mechanism (from `docs/mutual-ref-spec.md`)

- **Purpose (verbatim):** "MutualRef enables O(1) stream append for multiway merge (mwm).
  Instead of traversing a stream to find its tail, MutualRef maintains a mutable pointer to
  the current tail, allowing constant-time append operations." Use case: "Multiway merge of
  streams with constant delay per element."
- **Representation** (`glp_runtime/lib/runtime/terms.dart`): `class MutualRefTerm implements Term`
  holds `WriterTerm current` ("Current stream tail (mutable, updated by stream_append)") and
  an `int id`. So a MutualRef is a *heap-side mutable box over the shared output stream's
  tail-writer* — the one place GLP permits destructive update of a writer cell.
- **`stream_append` kernel (verbatim semantics):** get current tail from MutualRef; create a
  new tail writer; bind current tail to `[Value|NewTail?]`; **destructively** set
  `ref.current = newTail`; bind `RefOut` to the updated MutualRef. (Throws if the tail is
  already bound — a single-writer invariant check.)
- **`close_mutual_reference` kernel:** if the tracked tail is still unbound, bind it to `[]`
  (closes the merged output stream).
- **Guard `is_mutual_ref/1` (verbatim three-valued behaviour):** bound to a MutualRefTerm →
  **succeed**; bound to another term → **fail**; **unbound → fail (NOT suspend)**. This is a
  *type/instantiation* guard, deliberately non-suspending.
- **SRSW relaxation (load-bearing):** *"When `is_mutual_ref(X?)` appears in guards, variable
  `X` may have multiple reader occurrences in the clause body (same treatment as
  `ground(X?)`)."* The analyzer (`compiler/analyzer.dart`) adds the guarded var to
  `relaxedVars`. **This is exactly how the merge idiom legally shares one structure (the Ref)
  among many recursive readers without breaking single-reader** — a precedent the distributed
  link layer can mirror: a link-handle guarded by an analogous instantiation guard could be
  read multiply.
- **Short-circuit termination (verbatim):** an `L/R` "done"-token chain threads through every
  `mwm1`/`mwm_copy` fork (`L?→M`, `M?→R`); each `[]` base case contracts it (`L = L?`); when
  *all* processes terminate the chain collapses so `done` reaches `Done`, firing
  `close_when_done`, which binds the output tail to `[]`. This is a distributed-termination /
  quiescence-detection idiom (relevant to closing a distributed merged stream when all remote
  producers finish).
- **Dynamic fan-in:** `mwm1([merge(NewIn)|...])` lets new streams join an in-flight merge —
  the unbounded-N analogue of binary `merge/3`, and the closest existing model for an
  open-ended set of remote link endpoints feeding one local reader.

---

## 5. Related runtime declarations bearing on a distributed link (root prelude)

```prolog
% madGLP network primitive (already present — single cross-node send)
procedure _send(_?, _?, _?).

% Output (system predicate)
procedure _output(_?).

% Univ (term <-> list) — wire (de)serialisation building blocks
procedure =..(_, Stream(_)?).      % Compose: Stream? -> Compound
procedure ..=(Stream(_), _?).      % Decompose: Compound? -> Stream
```

`_send/3` is the existing madGLP cross-network primitive (the only network-facing kernel in
the prelude); a multi-protocol link layer generalises this single hook to N transports while
preserving the stream/channel semantics above. `=..`/`..=` (univ) give term⇄list conversion —
the natural serialisation seam for putting a bound term on the wire.

---

## 6. Implications for the distributed link layer (B2 fidelity checklist)

A "split the shared variable across instances" link primitive must preserve, observably:

1. **Writer-once / reader-suspend.** Local `receive`/head-match suspends on an unbound remote
   tail and reactivates exactly when the remote `send` binds it — i.e. the transport must
   carry *the binding event*, and the local cell must stay a suspending reader until then
   (three-valued: Success | Suspend | Fail).
2. **Channel duality as two links.** Each `new_channel` pair (`Xs`/`Ys`) becomes one
   bidirectional link = two one-directional binding streams; a split puts each end's
   read-stream on the far instance's write-stream.
3. **SRSW preserved across the cut.** The link endpoint must remain single-writer / single-
   reader per instance; multi-endpoint fan-in must go through `merge/3` (binary, fair via arg
   swap) or `mwm`/MutualRef (O(1), dynamic, unbounded N) — never a multi-reader variable.
   (Directly answers T2: BLE one-to-many broadcast cannot map to a multi-reader var; it must
   be modelled as N single-reader merge endpoints.)
4. **No compile-time unfolding for cross-instance links.** Unlike `new_channel`/`send`/
   `receive` (self.glp defined guards unfolded by the PE), a cross-instance link spans two
   compilations and must be a *runtime kernel* (like `_send`, `_stream_append`), not a unit
   clause.
5. **Termination/closure.** Closing a distributed merged stream needs a quiescence signal
   analogous to `mwm`'s `done`-token short-circuit + `close_mutual_reference` binding the tail
   to `[]`.

---

## Provenance / precedence note

All content above is extracted from local **glp-current** sources (highest precedence): the
root prelude `programs/self.glp`, `docs/mutual-ref-spec.md`, `docs/typed-glp-manual.md`
(§§4–9), and the canonical `merge/3` in `programs/bonds_v2/self.glp`. No Shapiro paper or
earlier-CL source was needed to answer this question; on any future conflict these local
specs override papers per SOURCE PRECEDENCE. The lineage (writer/reader cells, three-valued
unification, stream/channel communication, fair merge) descends from Shapiro's GLP and the
FCP family, but the *exact* signatures and SRSW-relaxation rules recorded here are the local
implementation truth.
