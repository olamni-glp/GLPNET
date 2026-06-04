> **SUPERSEDED 2026-05-04** — This file is from the prior ch07 implementation (`26e01792` / `f094f9db`) that was rejected by the project owner. Preserved per the no-removal directive but **not part of this chapter's runnable content**. The current ch07 (one-play-per-exercise REPL walkthroughs) is at the [chapter signpost](../ch07_tutorial.md). Content below is the prior implementation, kept as record.

---

# Exercise 10 — Cluster B parent-mediated child intro reject variants (plays 6–7 per Q4a)

Welcome to chapter 7, exercise 10 — the second of three play exercises
in cluster B.  Where ex-09 ran `play4` (everyone accepts) + `play5` (a
PARENT rejects), this exercise runs `play6` (Carol the CHILD rejects)
+ `play7` (Dave the CHILD rejects).  Together with ex-09 these complete
the §7.7 use case (c) parent-mediated child introduction reject-branch
coverage.

## What you'll learn

The §7.7 parent-mediated child introduction protocol (book p 61) has
TWO consent gates: a parent gate where each parent decides whether to
forward the introduction to its child, and a child gate where each
child decides whether to accept the introduction.  ex-09's `play5`
exercised a PARENT veto (Bob rejects, the introduction is short-
circuited at the parent gate before either child sees it).  This
exercise's `play6` and `play7` exercise CHILD vetoes — both parents
approve at the parent gate, the introduction propagates to both
children, but ONE of the children refuses at the child gate.

The CSSG (Concurrent Social Software Generation) model gives BOTH
parents AND children independent veto power.  Until you see all four
reject branches you don't have a complete picture of the protocol's
consent semantics.

## Distinguishing parent vs child reject

The §7.7 protocol's three reject branches (one parent reject + two
child rejects) cover EVERY way the introduction can be vetoed:

| Play | Where it rejects | Who rejects | What propagates | What does NOT |
|---|---|---|---|---|
| `play5` (ex-09) | parent gate | Bob (parent of Dave) | nothing reaches the children | `child_intro` never propagates beyond Bob |
| `play6` (this exercise) | child gate | Carol (child of Alice) | both parents approve, `child_intro` reaches both children, Dave accepts | Carol's `accept_child_intro` (she sends `reject_child_intro` instead) |
| `play7` (this exercise) | child gate | Dave (child of Bob) | both parents approve, `child_intro` reaches both children, Carol accepts | Dave's `accept_child_intro` (he sends `reject_child_intro` instead) |

Note the asymmetry: the parent reject (play5) is a SHORT-CIRCUIT — the
protocol terminates at the parent layer and the children never see the
introduction request.  The child rejects (play6, play7) are FULL
PROPAGATIONS that fail at the last step — both parents approve, both
children receive `child_befriend(...)`, but ONE child responds with
`reject_child_intro` instead of `accept_child_intro`.

## Play 6 — Carol rejects (child reject)

The four actors that drive `play6` live in
`olamni/tutorial/ch07/cssg-modules/ui/actors.glp` lines 387–433
(byte-exact from the canonical `programs/cssg_modules/ui/actors.glp`):

```glp
exported procedure alice6(ActorChannel?).
alice6(ch(In, [connect(bob)|Out?])) :-
    alice6_wait_connected(In?, Out).

procedure alice6_wait_connected(UserNotifyStream?, UserCmdStream).
alice6_wait_connected([connected(bob)|_],
                      [child_introduce(carol, bob, dave)]).
alice6_wait_connected([_|In], Out?) :-
    otherwise | alice6_wait_connected(In?, Out).
alice6_wait_connected([], []).

exported procedure bob6(ActorChannel?).
bob6(ch([befriend(alice, ReqId)|In],
        [decision(yes, alice, ReqId?)|Out?])) :-
    ground(ReqId?) |
    bob6_wait_child_intro(In?, Out).
bob6(ch([_|In], Out?)) :-
    otherwise | bob6(ch(In?, Out)).

procedure bob6_wait_child_intro(UserNotifyStream?, UserCmdStream).
bob6_wait_child_intro([child_befriend(alice, carol, ReqId)|_],
                      [approve_child_intro(carol, dave, ReqId?)]) :-
    ground(ReqId?) | true.
```

Alice opens with `connect(bob)`, waits for `connected(bob)`, then
issues `child_introduce(carol, bob, dave)` — asking for her child Carol
to be introduced to Bob's child Dave.  Bob accepts the cold-call
befriending (`decision(yes, alice, ReqId)`) and approves the child
intro (`approve_child_intro(carol, dave, ReqId)`) — so the parent gate
opens for both Alice and Bob.  This is the SAME parent-gate behaviour
as `play4` (everyone accepts).

Carol's actor (`carol6/1`) is where the play diverges:

```glp
exported procedure carol6(ActorChannel?).
carol6(ch([child_befriend(alice, dave, ReqId)|_],
          [reject_child_intro(dave, ReqId?)])) :-
    ground(ReqId?) | true.
carol6(ch([_|In], Out?)) :-
    otherwise | carol6(ch(In?, Out)).
```

Carol receives `child_befriend(alice, dave, ReqId)` (her parent Alice
is forwarding the introduction request from Bob's child Dave) and
responds with `reject_child_intro(dave, ReqId)` — the CHILD veto.  This
is the only line that distinguishes `play6` from `play4`: Carol's
response is `reject_child_intro` instead of `accept_child_intro`.

Dave's actor (`dave6/1`) ACCEPTS — Dave doesn't know yet that Carol
will reject, so he runs the same accepting logic he does in `play4`:

```glp
exported procedure dave6(ActorChannel?).
dave6(ch([child_befriend(bob, carol, ReqId)|In],
         [accept_child_intro(carol, ReqId?)|Out?])) :-
    ground(ReqId?) |
    dave6_wait_rejected(In?, Out).
dave6(ch([_|In], Out?)) :-
    otherwise | dave6(ch(In?, Out)).
```

Dave then waits for `rejected(carol)` — the notification that Carol
rejected — via `dave6_wait_rejected/2`.  This is the protocol's
notification path: when Carol's `reject_child_intro` reaches Alice,
Alice forwards a `rejected(...)` notification through the parent
network to Bob, who forwards it on the bob→dave child channel to Dave.

The `:limit 1000000\nplay6.\n` invocation runs this whole sequence
end-to-end and emits `→ suspended` (see `ex-10-repl-trace.md`).
Suspended is correct — the protocol completed (Carol's veto reached
Alice, the rejection notifications propagated through the network)
but the parent-child channels remain open at the end of the play.

## Play 7 — Dave rejects (child reject)

`play7` is the symmetric variant: alice7 / bob7 / carol7 / dave7
(`actors.glp` lines 439–485) follow the same shape as `play6`, but
Dave is the one who rejects.  The four actors are byte-exact except
for which child runs the reject branch:

```glp
exported procedure carol7(ActorChannel?).
carol7(ch([child_befriend(alice, dave, ReqId)|In],
          [accept_child_intro(dave, ReqId?)|Out?])) :-
    ground(ReqId?) |
    carol7_wait_rejected(In?, Out).

exported procedure dave7(ActorChannel?).
dave7(ch([child_befriend(bob, carol, ReqId)|_],
         [reject_child_intro(carol, ReqId?)])) :-
    ground(ReqId?) | true.
```

Carol now plays the accepting role (her `accept_child_intro(dave, ReqId)`
matches `dave6`'s shape from `play6`), and Dave plays the rejecting
role (his `reject_child_intro(carol, ReqId)` matches `carol6`'s shape).
Carol then waits for `rejected(dave)` via `carol7_wait_rejected/2`.

Same `→ suspended` outcome as `play6` — the protocol completes (Dave's
veto reaches Bob, the rejection notification propagates back through
the network to Alice and onward to Carol), but the parent-child
channels remain open at the end.

## Why these matter

Each reject branch demonstrates a different point in the §7.7 protocol
where consent is required:

- **Parent gate** (`play5`, ex-09): a parent veto means the parents
  control which introductions reach their children at all.  This is
  the "guardian" model — parents pre-screen.
- **Child gate, child A rejects** (`play6`, this exercise): the
  introduction reaches both children, the OTHER child accepts, but
  the FIRST child refuses.  This is the "informed refusal" model — a
  child has seen who is being introduced and decides no.
- **Child gate, child B rejects** (`play7`, this exercise): same
  shape as play6 but the OTHER child refuses.  The protocol must
  notify both directions symmetrically — Carol learns that Dave
  refused via `rejected(dave)` even though Carol herself accepted.

The CSSG model thus enforces FOUR-way consent for any child
introduction: both parents must approve at the parent gate AND both
children must accept at the child gate.  Any of the four can veto.
The protocol's notification design (the `rejected(...)` messages
that propagate back through the parent network) ensures that all
four agents end up consistently informed of the outcome, regardless
of who vetoed.

## Run the plays

```bash
cd D:/bstdev/research/GLP/GLP
# play6 — Carol rejects (child rejects)
printf "%s\n:limit 1000000\nplay6.\n:quit\n" "$(pwd -W)/olamni/tutorial/ch07/cssg-modules" | "/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill 2>&1 | head -40
# play7 — Dave rejects (child rejects)
printf "%s\n:limit 1000000\nplay7.\n:quit\n" "$(pwd -W)/olamni/tutorial/ch07/cssg-modules" | "/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill 2>&1 | head -40
```

Cross-check: `ex-10-repl-trace.md` records the verbatim REPL sessions
for both plays.

## Multimodule-project-derivation note

Per the `multimodule-project-derivation` cross-chapter relationship
contract (research R-008), cluster B's source canonical is
`programs/cssg_modules/` (the §7.7 validation example from book
p 61).  ALL six cluster B files (`self.glp`, `agent.glp`, `boot.glp`,
`mad_boot.glp`, `ui/mediator.glp`, `ui/actors.glp`) are byte-exact
inheritances from the canonical — cluster B retains the FULL §7.7
example including all four CSSG plays (4–7) and the corresponding
fplays.  This is in contrast to cluster A, where `boot.glp` is pruned
to the 3-agent friend-mediated subset.

## Next

Exercise 11 closes out cluster B's REPL exercises with cross-module-
call inspection goals.  Where ex-08–ex-10 ran the cluster B project
end-to-end via plays, ex-11 inspects the §7.5 procedure-renaming
mechanic AT A FINER GRAIN — using the same `:listing`-style goals
ex-04 used for cluster A — but now applied to the larger cluster B
multimodule structure.  See `ex-11-repl-trace.md` and
`ex-11-tutorial.md`.
