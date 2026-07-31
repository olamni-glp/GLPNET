------------------------------ MODULE CrashRestore ------------------------------
(***************************************************************************)
(* Feature 061 US4 (T035, FR-040): the crash/restore/resume state machine  *)
(* of the split engine host, checked for at-most-once committed-stream     *)
(* consistency over ALL crash points (FR-032/SC-002).                      *)
(*                                                                         *)
(* Abstraction (mapped to the implementation):                             *)
(*  - Values are identified by the DRIVER's monotone issue order 1..N      *)
(*    (each peer command is issued exactly once — the no-replay crash      *)
(*    boundary: a restored engine's In cursor sits at the snapshot         *)
(*    position, the dead connection's frames are gone, and the peer does   *)
(*    not re-send; in-flight-request replay is the deferred DEF-X3).      *)
(*  - `chain`     = the engine's bound Out stream (heap state).            *)
(*  - `peer`      = the peer-observable shipped stream. Produce appends to *)
(*    BOTH atomically because LinkEgress.ShipGround runs SYNCHRONOUSLY     *)
(*    inside the bind's OnBind callback on the runner thread — a bound     *)
(*    value has been handed to the transport before any other engine step  *)
(*    (snapshot included) can run. AsyncShip = TRUE negates exactly this   *)
(*    (negative control: the value sits `pending` between Bind and Ship).  *)
(*  - `snapChain` = the last complete snapshot (quiescence-gated: it       *)
(*    copies `chain` between steps, never mid-step).                       *)
(*  - Crash may strike between ANY two steps; Restore reloads snapChain    *)
(*    and re-arms egress at the first UNSHIPPED tail (RewireHandle's walk  *)
(*    past every bound cell) — so a correct restore re-ships NOTHING.      *)
(*    RearmAtZero = TRUE negates that (negative control: heap.OnBind on a  *)
(*    bound writer fires immediately, re-shipping the committed chain).    *)
(***************************************************************************)
EXTENDS Naturals, Sequences

CONSTANTS MaxVals,      \* driver issues values 1..MaxVals
          MaxCrashes,   \* crash budget (bounds the statespace)
          AsyncShip,    \* negative control: bind and ship as separate steps
          RearmAtZero   \* negative control: restore re-ships the committed chain

VARIABLES engine,    \* "serving" | "down"
          chain,     \* engine's bound Out stream (value ids, in bind order)
          pending,   \* bound-but-unshipped values (nonempty only when AsyncShip)
          hasSnap,   \* a complete snapshot exists
          snapChain, \* the snapshotted chain
          peer,      \* the peer-observable shipped stream
          nextVal,   \* next driver-issued value id
          crashes    \* crashes so far

vars == <<engine, chain, pending, hasSnap, snapChain, peer, nextVal, crashes>>

Range(s) == { s[i] : i \in DOMAIN s }

TypeOK ==
    /\ engine \in {"serving", "down"}
    /\ chain \in Seq(1..MaxVals)
    /\ pending \in Seq(1..MaxVals)
    /\ hasSnap \in BOOLEAN
    /\ snapChain \in Seq(1..MaxVals)
    /\ peer \in Seq(1..MaxVals)
    /\ nextVal \in 1..(MaxVals + 1)
    /\ crashes \in 0..MaxCrashes

Init ==
    /\ engine = "serving"
    /\ chain = <<>> /\ pending = <<>>
    /\ hasSnap = FALSE /\ snapChain = <<>>
    /\ peer = <<>> /\ nextVal = 1 /\ crashes = 0

(* The implemented semantics: bind ⇒ shipped, one atomic runner-thread step. *)
ProduceAtomic ==
    /\ ~AsyncShip
    /\ engine = "serving" /\ nextVal <= MaxVals
    /\ chain' = Append(chain, nextVal)
    /\ peer' = Append(peer, nextVal)
    /\ nextVal' = nextVal + 1
    /\ UNCHANGED <<engine, pending, hasSnap, snapChain, crashes>>

(* Negative-control split: the bind lands on the heap ... *)
Bind ==
    /\ AsyncShip
    /\ engine = "serving" /\ nextVal <= MaxVals
    /\ chain' = Append(chain, nextVal)
    /\ pending' = Append(pending, nextVal)
    /\ nextVal' = nextVal + 1
    /\ UNCHANGED <<engine, hasSnap, snapChain, peer, crashes>>

(* ... and the ship happens some steps later (a snapshot or crash may intervene). *)
Ship ==
    /\ AsyncShip
    /\ engine = "serving" /\ pending # <<>>
    /\ peer' = Append(peer, Head(pending))
    /\ pending' = Tail(pending)
    /\ UNCHANGED <<engine, chain, hasSnap, snapChain, nextVal, crashes>>

(* Quiescence-gated capture: between steps, a consistent copy of the chain. *)
Snapshot ==
    /\ engine = "serving"
    /\ hasSnap' = TRUE
    /\ snapChain' = chain
    /\ UNCHANGED <<engine, chain, pending, peer, nextVal, crashes>>

(* The process dies at an arbitrary point; anything bound-but-unshipped dies with it. *)
Crash ==
    /\ engine = "serving" /\ crashes < MaxCrashes
    /\ engine' = "down"
    /\ pending' = <<>>
    /\ crashes' = crashes + 1
    /\ UNCHANGED <<chain, hasSnap, snapChain, peer, nextVal>>

(* Supervised restart: reload the last complete snapshot (fresh when none),
   re-wire, and re-arm egress at the first unshipped tail — shipping nothing.
   RearmAtZero models the wrong arming (OnBind on bound cells): the whole
   committed chain is re-shipped on restore. *)
Restore ==
    /\ engine = "down"
    /\ engine' = "serving"
    /\ chain' = IF hasSnap THEN snapChain ELSE <<>>
    /\ peer' = IF RearmAtZero /\ hasSnap THEN peer \o snapChain ELSE peer
    /\ UNCHANGED <<pending, hasSnap, snapChain, nextVal, crashes>>

Next == ProduceAtomic \/ Bind \/ Ship \/ Snapshot \/ Crash \/ Restore

Spec == Init /\ [][Next]_vars
             /\ WF_vars(ProduceAtomic) /\ WF_vars(Bind) /\ WF_vars(Ship)
             /\ WF_vars(Restore)

(***************************************************************************)
(* The SC-002 consistency properties: the peer-observable committed stream *)
(* is exactly an uninterrupted run's stream.                               *)
(***************************************************************************)

(* No committed value is ever observed twice (at-most-once). *)
NoDup == \A i, j \in DOMAIN peer : i # j => peer[i] # peer[j]

(* No reordering: the peer sees values in issue order. *)
Ordered == \A i, j \in DOMAIN peer : i < j => peer[i] < peer[j]

(* No snapshot-committed value is ever lost: whatever the last complete
   snapshot says was produced reaches the peer. *)
NoCommittedLoss ==
    \A v \in 1..MaxVals :
        [](( hasSnap /\ v \in Range(snapChain) ) => <>( v \in Range(peer) ))

(* Progress: the full driver sequence is eventually observed (crashes are
   bounded, the supervisor always restores). *)
EventuallyAllObserved == <>( Range(peer) = 1..MaxVals )

===============================================================================
