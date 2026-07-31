// 061 T030 — supervision timing properties (SC-003, FR-023, FR-040).

// Q1 — no deadlock: the composed system can always act or let time pass
//      (Stopped is time-divergent by construction, not a deadlock).
A[] not deadlock

// Q2 — no silent death: every engine death is followed by serving again OR a
//      loud DEF-F2 taxonomy stop — never an unhandled dead engine.
Engine.Down --> (Engine.Serving || Supervisor.Stopped)

// Q3 — SC-003 detect→restart→restore bound, per recovery cycle: while the
//      engine is not serving and the supervisor has not classified the state
//      unrecoverable, at most BOUND time units have passed since the most
//      recent death.
A[] ((!Engine.Serving && !Supervisor.Stopped) imply gdead <= BOUND)

// Q4 — the taxonomy stop only fires at the recorded threshold (FR-023).
A[] (Supervisor.Stopped imply crashes >= THRESHOLD)
