# Ch 3 Sources — GLP Core

**PDF**: `GLP_ART.pdf`, book pp 15–24 (PDF pp 27–36).

## Sections (verified)
- 3.1 GLP — p 15 (Reader/Writer pairs, SO Invariant, SRSW, Operational Semantics, GLP Safety, Monotonicity)
- 3.2 Guards — p 21 (Built-in / Defined / Negation; SRSW Rules for Defined Guards)
- 3.3 Exercises — p 24 (OUT OF SCOPE per charter)

## Code-block index
| Block | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| **Program 3.1** | GLP Fair Stream Merger | p 15 | 3 clauses (`merge/3` two recursive + base) — annotated with `% output from …` comments | concurrent stream / SRSW canonical example |
| Worked Ex. 1 | Success | p 18 | one-line goal+head trace narrative | semantics walkthrough |
| Worked Ex. 2 | Suspension | p 18 | one-line trace | semantics walkthrough |
| Worked Ex. 3 | Failure | p 18 | one-line trace | semantics walkthrough |
| Worked Ex. 4 | Writer-to-Writer Failure | p 19 | one-line trace | semantics walkthrough |
| Example 3.1 | Circular Term Formation | p 20 | clause `p(X?,X)` + query `p(X,f(Y?)), p(Y,f(X?))` | circular-term illustration |
| §3.2 inline | `lookup/3` — guard negation | p 22 | 2 clauses using `=?=` and `~(...)` | guard negation idiom |
| §3.2 inline | `channel/1` defined-guard type test | p 22 | 1 unit clause + 2-clause `process/2` | defined-guard idiom |
| §3.2 inline | Channel ops `send/3`, `receive/3`, `new_channel/2` | p 23 | 3 unit clauses | channel-abstraction primitives |
| §3.2 inline | `relay/3` (channel-bridge) | p 23 | 3 clauses | combines `send`/`receive` in guards |
| §3.2 inline | `make_pair/2` | p 23 | 1 clause using `new_channel/2` in guard | channel allocation idiom |
| §3.2 inline | `bind_response/3` | p 23 | 2 clauses | response-binding idiom (used by Ch 8 cold-call) |

### Program 3.1 — verbatim (p 15)
```
Program 3.1: GLP Fair Stream Merger

merge([X|Xs],Ys,[X?|Zs?]) :- merge(Ys?,Xs?,Zs).  % output from first stream
merge(Xs,[Y|Ys],[Y?|Zs?]) :- merge(Xs?,Ys?,Zs).  % output from second stream
merge([],[],[]).                                 % terminate on empty streams
```

### §3.2 inline blocks — verbatim
```
% Guard negation (p 22)
lookup(Key, [(K,V)|_], V?) :- Key? =?= K? | true.
lookup(Key, [(K,_)|Rest], V?) :- ~(Key? =?= K?) | lookup(Key, Rest?, V).

% Defined-guard type test (p 22)
channel(ch(_, _)).
process(X, ok)    :- channel(X?) | handle(X?).
process(_, error) :- otherwise   | true.

% Channel operations as unit clauses (p 23)
send(X, ch(In, [X?|Out?]), ch(In?, Out)).
receive(X?, ch([X|In], Out?), ch(In?, Out)).
new_channel(ch(Xs?, Ys), ch(Ys?, Xs)).

% relay — uses send/receive in guards (p 23)
relay([X|In], Out?, Ch) :- send(X?, Ch?, Ch1)    | relay(In?, Out, Ch1?).
relay(In?, [X?|Out?], Ch) :- receive(X, Ch?, Ch1) | relay(In, Out, Ch1?).
relay([], [], ch([], [])).

% Channel allocation (p 23)
make_pair(C1?, C2?) :- new_channel(C1, C2) | true.

% Response binding (p 23) — Ch 8 cold-call uses this
bind_response(yes, accept(RemoteCh?), LocalCh?) :- new_channel(LocalCh, RemoteCh) | true.
bind_response(no, reject, none).
```

## Formal boxes / Propositions
- Definitions 3.1–3.6 (Writers Assignment, Term Matching, GLP Renaming, Reduction, Transition System, Pure Logic Variant).
- Propositions 3.7 (Computations are Deductions), 3.8 (SO Invariant), 3.10 (Monotonicity); Lemma 3.9 (Reader-Instance).
- **Formal 3.1 Circular Term Semantics** — p 20 (dereferencing, ground test, equality test, term copying, term display).
- SRSW Rules for Defined Guards table — p 24.

## Tutorial mode
cohesive-synthesis — Program 3.1 is the chapter's narrative anchor; §3.2 idioms become small example .glp files (or `useful-techniques.glp` per charter).

## Companion repo references
- `programs/typed_book/streams/` (typed merge variant — same as Program 3.1 with type/mode annotations from Ch 5).
- `programs/cssg_modules/` (`bind_response`, `make_pair` are used by the cold-call protocol in Ch 8).
- `../charter.md`
