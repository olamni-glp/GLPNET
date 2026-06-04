# Ch 5 Sources — Types and Modes

**PDF**: `GLP_ART.pdf`, book pp 47–52 (PDF pp 59–64).

## Sections (verified)
- 5.1 Type Definitions — p 47 (`::=` syntax, recursive types)
- 5.2 Built-in Types — p 48 (`Number`, `Any`, `Atom`, generic `List`)
- 5.3 Moded Procedure Declarations — p 48 (`procedure …(…?, …)` syntax)
- 5.4 Mode Checking — p 49 (worked example on `merge/3`)
- 5.5 Embedded Modes: Response Slots — p 50 (`CounterMsg ::= … ; show(Number?)`)
- 5.6 Complete Example: Typed Quicksort — p 51
- 5.7 Type Errors and Mode Errors — p 51
- 5.8 Summary + Exercises — p 52 (exercises OUT OF SCOPE)

## Code-block index
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 5.1.1 | `Bit ::= 0 ; 1.` | p 47 | 1 type def | basic enum |
| 5.1.2 | `Nat ::= 0 ; s(Nat).` | p 47 | 1 recursive type def | Peano natural |
| 5.1.3 | `NumList ::= [] ; [Number | NumList].` | p 47 | 1 list type def | typed list |
| 5.2.1 | `List ::= [] ; [Any | List].` | p 48 | 1 generic list type | universal list |
| 5.3.1 | `procedure merge(List?, List?, List).` | p 48 | 1 procedure decl | mode declaration syntax |
| 5.4.1 | Typed `merge/3` (full worked example) | p 49 | `NumList` type + `procedure merge(NumList?, NumList?, NumList).` + 3 clauses | mode-checked recursive stream merge |
| 5.5.1 | `CounterMsg`, `CounterStream`, `procedure counter/2`, `counter([show(State?)|S], State)` clause | p 50 | 2 type defs + procedure + 1 clause | response-slot / embedded consume mode |
| **5.6** | **Typed Quicksort (canonical)** | **p 51** | `NumList` type + `procedure quicksort/2`, `qsort/3`, `partition/4` + 6 clauses | **chapter's flagship Program** |
| 5.7.1 | Type-error example `foo/1` | p 51 | `procedure foo(NumList).` + bad clause | type-error illustration |
| 5.7.2 | Mode-error example `bar/2` | p 51–52 | `procedure bar(Number?, Number).` + bad clause `bar(X?, Y)` + corrected `bar(X, Y?) :- Y := X? + 1.` | mode-error illustration |

### Program 5.6 — verbatim (p 51)
```
NumList ::= [] ; [Number | NumList].

procedure quicksort(NumList?, NumList).
procedure qsort(NumList?, NumList?, NumList).
procedure partition(NumList?, Number?, NumList, NumList).

quicksort(Unsorted, Sorted?) :- qsort(Unsorted?, Sorted, []).

qsort([X|Unsorted], Sorted?, Rest) :-
    number(X?) |
    partition(Unsorted?, X?, Smaller, Larger),
    qsort(Smaller?, Sorted, [X?|Sorted1?]),
    qsort(Larger?, Sorted1, Rest?).
qsort([], Rest?, Rest).

partition([X|Xs], A, Smaller?, [X?|Larger?]) :-
    A? < X? | partition(Xs?, A?, Smaller, Larger).
partition([X|Xs], A, [X?|Smaller?], Larger?) :-
    A? >= X? | partition(Xs?, A?, Smaller, Larger).
partition([], A, [], []) :- number(A?) | true.
```

## Formal boxes
- **Formal 5.1: Type Definition Syntax** — p 48 (alt forms: constant, structure, list cons, type ref).
- **Formal 5.2: Mode Semantics** — p 49 (consume vs produce data-flow table).
- **Formal 5.3: Mode Involution** — p 50 (consume × consume = produce, like double negation).

## Tutorial mode
cohesive-synthesis. Single flagship `.glp` file = §5.6 typed quicksort. Smaller didactic files for §5.1, §5.4 worked merge, and §5.5 counter-with-response-slot may also be useful.

## Companion repo references
- `programs/typed_book/recursive/list_processing/quicksort.glp` (and `qsort.glp`, `partition.glp`) — verify they match Program 5.6 exactly.
- `programs/typed_book/streams/objects_monitors/counter.glp` — verify against §5.5 response-slot variant.
- `../charter.md`
