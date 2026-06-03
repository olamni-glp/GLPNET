# ch06 ex-04 — §6.4 Bidirectional Communication — REPL trace

This trace captures the verbatim REPL session for ex-04.  Five phases: A
loads the `.glp`; B is the canonical bidirectional-channel demo; C, D, E
run three inspection goals.

## Phase A — Build / load

```glp
GLP> D:/bstdev/research/glp/glp/olamni/tutorial/ch06/exercise-04/ch-06-ex-04-bidirectional-communication.glp
✓ Loaded: D:/bstdev/research/glp/glp/olamni/tutorial/ch06/exercise-04/ch-06-ex-04-bidirectional-communication.glp
```

## Phase B — Primary demo: new_channel cross-linking

`new_channel(C1, C2).` allocates a cross-linked channel pair.  The output
shows the bidirectional structure: what is written to C1's output stream
position is read from C2's input stream position, and vice versa.

```glp
GLP> new_channel(C1, C2).
C1 = ch(X4, X6)
C2 = ch(X6, X4)
→ succeeds
```

The two channels share the same fresh variables (X4 + X6) but in
opposite slot positions — `C1 = ch(Xs?, Ys)` and `C2 = ch(Ys?, Xs)` per
the unit clause.  Whatever is written to one channel's `Ys` is the same
variable that the other channel reads from its `Xs?`.  This IS
bidirectional communication: shared state, opposite views.

The exact variable numbers (X4, X6) are runtime-allocated and
deterministic per fresh REPL invocation but vary per session — the
SHAPE (cross-linked slot positions) is the invariant.

## Phase C — Inspection 1: make_pair returns inverted-reader pair

`make_pair(P1, P2).` is a thin wrapper over `new_channel/2` that hands
the caller two channel-readers (the head construction inverts the
writer/reader pair).

```glp
GLP> make_pair(P1, P2).
P1 = ch(X12, X14)
P2 = ch(X14, X12)
→ succeeds
```

Same cross-linked shape as Phase B (different variable numbers because
this is a separate goal).  After partial evaluation the make_pair clause
is equivalent to `make_pair(ch(X1?, X2), ch(X2?, X1)).`

## Phase D — Inspection 2: send appends to output stream

`send(hello, ch([], Out), Result).` exercises the send/3 unit clause.
Calling send with an input channel `ch([], Out)` (empty input stream,
unbound output stream) and matching against the unit clause `send(X,
ch(In, [X?|Out?]), ch(In?, Out))` binds `Out = [hello | X28]` (hello
prepended to the output stream tail) and `Result = ch([], X28)` (the
new channel state has the same input stream and the new output tail).

```glp
GLP> send(hello, ch([], Out), Result).
Out = [hello | X28]
Result = ch([], X28)
→ succeeds
```

The cons-cell `[hello | X28]` is the new output-stream head; `X28` is
the fresh tail that the next send will fill.

## Phase E — Inspection 3: receive extracts from input stream

`receive(X, ch([world], R), Result).` exercises the receive/3 unit
clause.  Calling receive with an input channel `ch([world], R)` (input
stream containing one element `world`, unbound output stream) and
matching against the unit clause `receive(X?, ch([X|In], Out?), ch(In?,
Out))` binds `X = world` (the head of the input stream) and `Result =
ch([], X38)` (the new channel state has the empty input tail and a
fresh output stream).

```glp
GLP> receive(X, ch([world], R), Result).
X = world
R = <unbound>
Result = ch([], X38)
→ succeeds
```

`X = world` shows the receive successfully extracted `world` from the
input stream.  `R = <unbound>` because R was never used in this goal
(the input stream pattern only matched `[world]`, leaving R unbound).
`Result` carries the channel state forward.

---

These channel ops are byte-exact from ch03 §3.2 (book p 23), re-presented
here under §6.4 with the local non-parameterised `Stream` and `Channel`
types declared explicitly.  send/3, receive/3, and new_channel/2 are
also defined in `programs/self.glp` (the GLP root prelude); the local
declarations in this file OVERRIDE those (per typed-glp-manual.md §8.1).

ONE byte-exact-source amendment was applied to relay clause 2: `In?`
(reader in head) + `In` (writer in body) was swapped to `In` (writer in
head) + `In?` (reader in body) so the type-checker would accept the
arg 1 mode consistency across all three relay clauses.  The amendment
is documented in the `.glp` header comment + ex-04-tutorial.md.
