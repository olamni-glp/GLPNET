> **SUPERSEDED 2026-05-04** — This file is from the prior ch07 implementation (`26e01792` / `f094f9db`) that was rejected by the project owner. Preserved per the no-removal directive but **not part of this chapter's runnable content**. The current ch07 (one-play-per-exercise REPL walkthroughs) is at the [chapter signpost](../ch07_tutorial.md). Content below is the prior implementation, kept as record.

---

# Exercise 11 — Cluster B cross-module-call inspection (Formal 7.2)

Welcome to chapter 7, exercise 11 — the final REPL drill of cluster B.
This exercise exercises §7.4 + Formal 7.2 (book p 58): the cross-module
type-checking contract.  You inspect how every `M # goal(args)` call
site in `boot.glp` is verified at load time using ONLY the local
`imported procedure` declarations — without the type checker reading
the source of `agent.glp`, `ui/mediator.glp`, or `ui/actors.glp`.

## What you'll learn

1. **Cross-module call form `M # goal(args)`** — the syntax that names a
   sibling module M and a goal `goal(args)` that M exports.  This is the
   §7.3 "imported / cross-module call" form.  It appears only in
   clause-body call positions; the top-level REPL goal grammar does NOT
   accept it (Phase C of the trace confirms this).
2. **Imported procedure decls as the type-check interface** — the seven
   `imported procedure` declarations at the top of `boot.glp` are the
   COMPLETE type-check interface for cross-module calls.  They carry
   procedure name, arity, argument types, and argument modes.  Once
   declared, the type checker uses them locally — it never opens sibling
   source.
3. **Why this enables modular per-module type-checking** — Formal 7.2's
   guarantee is that a project with N modules requires only N
   independent per-file type-checks (not N×(N-1) cross-references).
   This is what makes the GLP module system scale: you can develop and
   type-check `boot.glp` in isolation as long as the `imported procedure`
   decls at its top match the `exported procedure` decls in the actual
   sibling files.

## Imported procedures in boot.glp (lines 14-51)

The `imported procedure` declarations are the cross-module type-check
interface.  Each line states: "the procedure named `M#name/arity` is
declared in module M with this signature; trust this signature when
type-checking call sites in this file".

```glp
%% Cross-module dependencies declared via imported procedures.
%% The type checker uses these declarations locally — it does NOT
%% access agent.glp, mediator.glp, or actors.glp.

-module(boot).
-mode(system).

%% =============================================================================
%% IMPORTED PROCEDURES — Cross-module dependencies
%% =============================================================================

%% From agent.glp (sibling)
imported procedure agent#agent(Constant?, UserInStream?, NetInStream?, OutputsList?).

%% From ui/mediator.glp
imported procedure mediator#ui_mediator(Constant?, AgentChannel?, UserChannel?, PendingList?, Constant?).

%% From ui/actors.glp — plays 1-3 (3 agents)
imported procedure actors#alice1(ActorChannel?).
imported procedure actors#bob1(ActorChannel?).
imported procedure actors#charlie1(ActorChannel?).
imported procedure actors#alice2(ActorChannel?).
imported procedure actors#bob2(ActorChannel?).
imported procedure actors#charlie2(ActorChannel?).
imported procedure actors#alice3(ActorChannel?).
imported procedure actors#bob3(ActorChannel?).
imported procedure actors#charlie3(ActorChannel?).

%% From ui/actors.glp — plays 4-7 (4 agents: CSSG)
imported procedure actors#alice4(ActorChannel?).
imported procedure actors#bob4(ActorChannel?).
imported procedure actors#carol4(ActorChannel?).
imported procedure actors#dave4(ActorChannel?).
%% … (continues for plays 5, 6, 7 — 25 actor procedures total)
```

There are 27 imported procedure declarations in total: 1 for `agent`,
1 for `mediator`, and 25 for the per-play actors (3 actors each for
plays 1–3 + 4 actors each for plays 4–7).  Each line mirrors the
`exported procedure` decl in the corresponding sibling file — the
sibling file's exported decl is the source of truth; boot.glp's
imported decl is the local copy used for type-checking.

## Cross-module call sites in boot.glp (play4, lines 288-339)

`play4` is the §7.7 "all four accept child introduction" play.  Its
body has many cross-module call sites — one per actor, agent, and
mediator wired into the four-agent CSSG topology.  Below is the body
with the key call sites annotated:

```glp
%% =============================================================================
%% PLAY 4 — CSSG: All four accept child introduction
%% =============================================================================

play4 :-
    network2(ch(AliceNetOut?, AliceNetIn),
             ch(BobNetOut?, BobNetIn)),

    actors # alice4(ch(AliceActorIn?, AliceActorOut)),
    %% type-checks against `imported procedure actors#alice4(ActorChannel?)`
    %% runtime: dispatches to actors.glp's exported `alice4/1` clause

    tee(AliceActorOut?, AliceMedIn, AliceDispCmd),
    agent # agent(alice, AliceAgentIn?, AliceNetAndChildIn?,
          [output('_user', AliceAgentToUser),
           output('_net', AliceNetOut),
           output(child(carol), AliceToCarol)]),
    %% type-checks against `imported procedure agent#agent/4`
    %% runtime: dispatches to agent.glp's exported `agent/4` clause

    merge(AliceNetIn?, AliceFromCarol?, AliceNetAndChildIn),
    mediator # ui_mediator(alice, ch(AliceAgentToUser?, AliceAgentIn),
                ch(AliceMedIn?, AliceMedOut), [], 1),
    %% type-checks against `imported procedure mediator#ui_mediator/5`
    %% runtime: dispatches to mediator.glp's exported `ui_mediator/5` clause

    tee(AliceMedOut?, AliceActorIn, AliceDispNotify),
    sink(AliceDispCmd?), sink(AliceDispNotify?),

    actors # bob4(ch(BobActorIn?, BobActorOut)),
    %% (… same shape repeats for bob, carol, dave …)
    …
    actors # carol4(ch(CarolActorIn?, CarolActorOut)),
    …
    actors # dave4(ch(DaveActorIn?, DaveActorOut)),
    …

    merge(AliceToCarol?, [], CarolFromAlice),
    merge(CarolToAlice?, [], AliceFromCarol),
    merge(BobToDave?, [], DaveFromBob),
    merge(DaveToBob?, [], BobFromDave).
```

The play body has 12 cross-module call sites (4 actor calls + 4 agent
calls + 4 mediator calls).  When the type checker processes `play4`'s
body, it walks each call site, looks up the corresponding `imported
procedure` decl, checks arity and argument modes against the declared
signature, and moves on.  The sibling files (`agent.glp`,
`ui/mediator.glp`, `ui/actors.glp`) are NOT opened during this check —
the imported decls are sufficient.

## Formal 7.2 in plain prose

The formal rule from book p 58 says:

> *Cross-Module Well-Typing.*  Let `M # goal(args)` be a cross-module
> call site in module `M_caller`.  If `M_caller` declares `imported
> procedure M#goal(T1?, T2?, …, Tn?)` with the appropriate argument
> types and modes, then the call site type-checks well-formed iff
> `args` match `(T1?, T2?, …, Tn?)`.  No access to module M's source
> is required.

In plain prose: the imported decl carries the FULL type signature, so
the type checker only needs to read the local file plus its imported
decls.  This is the formal underpinning of the §7.4 modularity
property: each module type-checks in isolation against its own imports.

## Why this matters

1. **Independent module development.**  Multiple developers can work
   on `agent.glp`, `mediator.glp`, and `actors.glp` in parallel, as
   long as the `exported procedure` decls in each match the `imported
   procedure` decls referenced by `boot.glp`.  No one needs to ship
   the whole project to type-check a partial change.
2. **Source isolation in the type checker.**  The type checker can
   verify `boot.glp` in isolation — even if the actual implementations
   in sibling files are missing, broken, or under construction — as
   long as the imported decls match the declared interface.  This is
   the basis for "compile boot.glp without compiling agent.glp".
3. **Linear-time per-project type-checking.**  N independent file
   type-checks plus the load-time consistency check (each imported
   decl's signature must match the exported decl in its source module)
   gives O(N) total work — vs the O(N²) you would get if every file
   needed cross-references against every other file's source.
4. **Simple cross-module interface contract.**  The interface between
   modules is exactly the set of `imported procedure` / `exported
   procedure` decl pairs.  Nothing else is shared (no global state, no
   implicit dependencies, no cross-module variable scoping).

## Run the inspection demo

### Step 1 — Open the REPL

```bash
./glp_runtime/glp_repl.exe
```

### Step 2 — Load the cluster B project

```
D:/bstdev/research/glp/glp/olamni/tutorial/ch07/cssg-modules
```

Expected: `✓ Loaded project: …`.  Cross-check: trace's **Phase A**.
A clean load means the §7.4 + Formal 7.2 type-check passed for ALL
cross-module call sites in `boot.glp` using ONLY local imported decls.

### Step 3 — Set the goal limit and run play1

```
:limit 1000000
play1.
```

Expected: `→ suspended`.  Cross-check: trace's **Phase B**.  This
exercises 9 cross-module call sites (3 actor calls + 3 agent calls + 3
mediator calls — fewer than play4's 12 but the same type-check shape).
The `→ suspended` outcome is the §7.4 success signal: every cross-
module call type-checked locally AND dispatched correctly at runtime.

### Step 4 — (Optional) Try the `M # G` form at the goal prompt

```
agent # agent(alice, [], [], []).
```

Expected: `→ failed` plus a `[syntax]` error at column 7.  Cross-check:
trace's **Phase C**.  This confirms `M # G` is clause-body syntax only
— top-level goals must use the entry-point alias (`play1.`–`play7.`).

### Step 5 — Cross-check against the trace

Open `ex-11-repl-trace.md` and confirm the sequence Phase A (clean
project load) → Phase B (`→ suspended`) → Phase C (parser-level
rejection of `M # G` at top level).

## Multimodule-project-derivation note

ex-11's source canonical is `programs/cssg_modules/`.  Cluster B
inherits all six files BYTE-EXACT (no derivation), enforced by Section R
of `test/run_all_tests.sh` (per-file `diff` vs canonical).  ex-07
walked the project structure; ex-08 ran plays 1–3 (cold-call
befriending); ex-09 ran plays 4+5 (CSSG accept + reject); ex-10 ran
plays 6+7 (parent-mediated child intro variants); this ex-11 inspects
the §7.4 + Formal 7.2 cross-module type-check mechanic that underpins
all of those plays.  This is the last REPL drill of cluster B.

## What ex-12 brings next

Exercise 12 is the **CSSG-in-Flutter** pairing — the cluster B Flutter
walkthrough that runs `play1`–`play5` end to end inside the
multimodule Flutter app `glp_multiagent/lib/main_olamni_ch07_clusterB.dart`
(per Q4a: ex-12 plays = play1+play2+play3+play4+play5).  ex-12 mirrors
ex-06 (cluster A's Flutter pairing) but on the larger CSSG topology
covering the full §7.7 use-case set.
