# Ch 10 Sources — Interlaced Streams

**PDF**: `GLP_ART.pdf`, book pp 97–100 (PDF pp 109–112).

## Sections (verified)
- 10.1 Blocklace Structure — p 97 (prose: blockchain → DAG generalization, payloads + tips)
- 10.2 The Interlaced Streams Program — p 97 (Entry Point, Block Production, Tip Collection)
- 10.3 Execution Trace — p 98 (Single Agent, Multiple Agents w/ Incomplete Streams)
- 10.4 Multiagent Deployment — p 99 (3-agent invocation pattern)
- 10.5 Security Properties — p 99 (Immutability, Unforkability, Non-repudiation, Causal Ordering — prose)
- 10.6 Applications — p 100 (Consensus, Collaborative editing, Distributed ledgers — prose)
- 10.7 Exercises — p 100 (OUT OF SCOPE)

## Code-block index — §10.2 The Interlaced Streams Program
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 10.2.1 | `streams/2` (entry point) | p 97 | 1 clause: `streams(stream(S?), Others) :- produce_payloads(Payloads), interlace(Payloads?, S, Others?).` | per-agent entry |
| 10.2.2 | `interlace/3` (block production) | p 98 | 2 clauses (recursive + base) — for each payload, collect tips and emit `block(Payload?, Tips?)` | DAG block writer |
| 10.2.3 | `collect_tips/3` (tip collection) | p 98 | 3 clauses (unbound-tail / extended-tail / base) — uses `unknown(Bs?)` to detect current tips | DAG cross-reference collector |

### Program 10.2.2 — verbatim (p 98)
```
interlace([Payload|Payloads], [block(Payload?, Tips?)|Stream?], Others) :-
    collect_tips(Others?, Tips, Others1),
    interlace(Payloads?, Stream, Others1?).
interlace([], [], _).
```

### Program 10.2.3 — verbatim (p 98)
```
collect_tips([[Block|Bs]|Others], [Block?|Tips?], [Bs?|Others1?]) :-
    unknown(Bs?) |
    collect_tips(Others?, Tips, Others1).
collect_tips([[_|Bs]|Others], Tips?, [Bs?|Others1?]) :-
    otherwise |
    collect_tips(Others?, Tips, Others1).
collect_tips([], [], []).
```

## §10.4 Multiagent Deployment
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 10.4.1 | 3-agent goal pattern: `p(streams(stream(Ps?), [Qs?, Rs?]))`, `q(...)`, `r(...)` | p 99 | invocation form (no clauses, just a call shape) | how to wire `streams/2` for n agents |

## Tutorial mode
multi-actor-distillation. **Single use case** for the chapter (per charter §1: one project per use case for chs 7–13).

## Use case (suggested per charter)
- **`ch10/interlaced-streams-group/`** — `streams/2` + `interlace/3` + `collect_tips/3` + a 3-agent play (variant of Ch 9 §9.3 interlaced group, but standalone, not embedded in CSSN).

NOTE: §9.3 already presents `interlace`/`collect_tips` applied to group messaging. Ch 10 elevates the same code to its own chapter, framing it as the underlying data structure (blocklace) used by Ch 11 (cryptocurrencies) and Ch 12 (consensus). The tutorial may either:
- Re-use the §9.3 `member`/`tag_messages` wrapper for a complete play, OR
- Present the bare `streams/2` form per §10.4 for a distributed-ledger flavour.

## Companion appendix
- "Interlaced Streams Group Play" — `GLP_ART.pdf` p 156 — companion play code (extract at scan time and cross-reference with §9.3 code).

## Companion repo references
- `programs/typed_book/cssn/interlaced_streams/` — typed interlaced-streams modules.
- `glp_multiagent/lib/main_cssg_mad_modules.dart` — Flutter template.
- `../charter.md`
