# Ch 8 Sources — The Grassroots Social Graph

**PDF**: `GLP_ART.pdf`, book pp 69–79 (PDF pp 81–91).

## Sections (verified)
- 8.1 Graph Structure — p 69 (prose only)
- 8.2 Agent Initialization — p 69 (`agent/3` clause)
- 8.3 Cold-Call Befriending Protocol — p 70 (User Initiation, Processing Received Introductions, User Decision and Channel Establishment, Establishing the Connection, Processing Responses, Response Stream, Inject Predicate)
- 8.4 Friend-Mediated Introduction — p 72 (Initiating, Processing, Accepting/Rejecting, Catch-All)
- 8.5 Channel Notation — p 74 (prose only — describes `send`/`receive`/`new_channel` semantics)
- 8.6 Utility Predicates — p 74 (`lookup`, `lookup_send`, `tag_stream`)
- 8.7 Testing with Multiagent Plays — p 75 (Network Switch, Actors and Scripts, Play Structure, Execution Trace, Scaling, Friend-Mediated Intro test)
- 8.8 Exercises — p 78 (OUT OF SCOPE)

## Code-block index — §8.2 Agent
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 8.2.1 | `agent/3` initializer | p 69 | 1 clause merging `UserIn`/`NetIn` and entering `social_graph/3` event loop | agent boot pattern |

### §8.2 verbatim (p 69)
```
agent(Id, ch(UserIn, UserOut), ch(NetIn, NetOut)) :-
    merge(UserIn?, NetIn?, In),
    social_graph(Id?, In?, [(user, UserOut), (net, NetOut)]).
```

## Code-block index — §8.3 Cold-Call Befriending
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 8.3.1 | `social_graph` — `connect(Target)` (User Initiation) | p 70 | 1 clause; `lookup_send` to net + `response_stream` + recurse | cold-call initiator |
| 8.3.2 | `social_graph` — `intro(From, From, Resp)` (incoming intro) | p 70 | 1 clause; `lookup_send` to user as `befriend(...)` | cold-call recipient |
| 8.3.3 | `social_graph` — `decision(Dec, From, Resp)` (user accepts/rejects) | p 71 | 1 clause; `bind_response/6` then recurse | user-decision handler |
| 8.3.4 | `bind_response/6` | p 71 | 2 clauses (yes/accept, no/no); constructs channel pair in head on `yes` | response binding (acceptance branch) |
| 8.3.5 | `handle_accept/6` | p 71 | 1 clause; `tag_stream` + `merge` + `add_friend` | recipient channel completion |
| 8.3.6 | `handle_response/6` | p 71 | 2 clauses (`accept(...)` and `no`); same structure as `handle_accept` for initiator | initiator channel completion |
| 8.3.7 | `add_friend/4` | p 71 | 1 unit clause prepending `(Name?, Out?)` to friends list | friend-list append |
| 8.3.8 | `social_graph` — `response(Resp)` (initiator's response) | p 72 | 1 clause; `handle_response` + recurse | initiator response handler |
| 8.3.9 | `response_stream/4` | p 72 | 1 clause `[msg(Target?, Id?, response(Resp?))] :- known(Resp?) | true.` | inject single-element response stream |
| 8.3.10 | `inject/4` | p 72 | 2 clauses (`known(X)` / `unknown(X)`) | deferred message insertion (used for application-level traffic) |

## Code-block index — §8.4 Friend-Mediated Introduction
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 8.4.1 | `social_graph` — `introduce(P, Q)` (initiator) | p 73 | 1 clause; constructs cross-linked channel pair `ch(QtoP?, PtoQ)` / `ch(PtoQ?, QtoP)` and sends `intro(P, ch(...))` to each | introducer |
| 8.4.2 | `social_graph` — `intro(Other, Ch)` (incoming friend intro) | p 73 | 1 clause; `lookup_send` to user as `befriend_intro(From?, Other?, Ch?)` | mediated-intro recipient |
| 8.4.3 | `social_graph` — `accept_intro(Other, ch(FIn, FOut))` | p 73 | 1 clause; `tag_stream` + `merge` + `add_friend` | accept mediated intro |
| 8.4.4 | `social_graph` — `reject_intro(_, _)` | p 73 | 1 clause; just recurse | reject mediated intro |
| 8.4.5 | `social_graph` — catch-all `msg(_,_,_)` | p 74 | 1 clause guarded by `otherwise` | drop unknown message |

## Code-block index — §8.6 Utility Predicates
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 8.6.1 | `lookup/3` | p 74 | 2 clauses (key match / `otherwise` recurse) | assoc-list lookup |
| 8.6.2 | `lookup_send/4` | p 74 | 1 clause; `lookup` then `update` | send-on-named-stream idiom |
| 8.6.3 | `tag_stream/3` | p 74 | 2 clauses; wraps each msg as `msg(Name?, M?)` | source-id tagging |

## Code-block index — §8.7 Testing with Multiagent Plays
| # | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| 8.7.1 | `network2/2` (2-agent switch) | p 75 | 2 clauses (Alice→Bob, Bob→Alice); `n(n-1)` clauses general formula | network-switch pattern |
| 8.7.2 | `alice_actor/2` (initiator script) | p 76 | 1 unit clause writing `[msg(user, alice, connect(bob))]` | scripted initiator |
| 8.7.3 | `bob_actor/2` (responder script — cold-call only) | p 76 | 1 clause matching `befriend(From, Resp)` then writing `decision(yes, From?, Resp?)` | scripted accepter |
| 8.7.4 | `play_alice_bob/0` | p 76 | 1 clause wiring `network2`, `agent/3 × 2`, `alice_actor`, `bob_actor` | play orchestration |
| 8.7.5 | `play_4agents/0` (4-agent variant) | p 77–78 | 1 clause wiring `network4` + 4 agents + 4 actors | scaling demo |
| 8.7.6 | `bob_actor` + `bob_wait_intro` (mediated intro version) | p 78 | 2 clauses; first handles cold-call accept, second waits for `befriend_intro` then writes `accept_intro(...)` | 2-phase scripted recipient |
| 8.7.7 | `play_introduction` (3-phase) | p 78 | implicit play (described): Alice cold-calls Bob; Alice cold-calls Carol; Alice introduces Bob to Carol | full friend-mediated test |

## Tutorial mode
multi-actor-distillation. Per charter §1: use-case-driven; each project subdir matches `programs/cssg_modules/` shape `{self.glp, agent.glp, network.glp, actors.glp, boot.glp}` paired with `glp_multiagent/lib/main_olamni_ch08_<use-case>.dart`.

## Use cases (suggested grouping per charter)
1. **`ch08/cold-call-befriending/`** — §8.2 `agent/3` + §8.3 cold-call clauses + `bind_response`, `handle_accept`, `handle_response`, `add_friend`, `response_stream`, `inject` + §8.6 utilities + §8.7 `network2`, `alice_actor`, `bob_actor`, `play_alice_bob`.
2. **`ch08/friend-mediated-introduction/`** — same agent base + §8.4 introduce/intro/accept_intro/reject_intro + §8.7 mediated-intro `bob_actor`/`bob_wait_intro` + `play_introduction` (3-phase: 2 cold-calls then introduction).
3. **`ch08/four-agent-graph/`** — same agent base + `network4` + 4 actors + `play_4agents` exercising independent befriending and graph connectivity.
4. **`ch08/useful-techniques.glp`** — §8.6 `lookup`, `lookup_send`, `tag_stream` if not already in shared lib.

## Companion repo references
- `programs/cssg_modules/{self.glp, agent.glp, ui/{mediator.glp, actors.glp}, boot.glp}` — production cold-call/friend-mediated agent.
- `programs/typed_book/cssg/` — typed variants.
- `programs/social_graph_simulated_ui_modules/` — UI-mediator pattern for the `(user, …)` half of the agent's channel.
- `glp_multiagent/lib/main_cssg_mad_modules.dart` — Flutter template.
- `../charter.md`
