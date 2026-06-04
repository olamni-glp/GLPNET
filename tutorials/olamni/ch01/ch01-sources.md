# Ch 1 Sources — Introduction

**PDF**: `GLP_ART.pdf`, book pp 3–7 (PDF pp 15–19). Chapter 1: Introduction.

## Sections (verified against PDF)
- 1.1 The Grassroots Vision — p 3
- 1.2 The Programming Challenge — p 4
- 1.3 Logic Programming: A Natural Foundation — p 4
- 1.4 Concurrent Logic Programming — p 5
- 1.5 The Single-Reader/Single-Writer Insight — p 5
- 1.6 A First GLP Program — p 5
- 1.7 Security Through Cryptography — p 6
- 1.8 Book Overview — p 6

## Code-block index (Programs)
| Program | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| **1.1** | Fair Stream Merger | p 5 | 3 clauses (`merge/3` two recursive + base) | concurrent stream / SRSW demo |

### Program 1.1 — verbatim header + body (p 5)
```
Program 1.1: Fair Stream Merger

merge([X|Xs],Ys,[X?|Zs?]) :- merge(Ys?,Xs?,Zs).
merge(Xs,[Y|Ys],[Y?|Zs?]) :- merge(Xs?,Ys?,Zs).
merge([],[],[]).
```

## Formal boxes
- **Formal 1.1: The merge Program** — p 6 (SRSW compliance, fairness, stream processing properties; not executable code, supporting text only).

## Tutorial mode
cohesive-synthesis — single narrative `.glp` file; section-driven per charter §1.

## Companion repo references
- `programs/typed_book/streams/` (typed merge variants) — verify alignment with §1.6 Program 1.1.
- `../charter.md`
