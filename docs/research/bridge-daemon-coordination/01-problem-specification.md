# Bridge daemon coordination — problem specification

**Author**: Claude (assisted)
**Date**: 2026-05-10
**Status**: DRAFT for Gabi review (step 1 of 7-step design plan)

This document specifies the coordination problems that the unified PGLite bridge daemon faces in glpnet. It is the first artifact in the workflow:

1. **(this doc)** Problem spec
2. Web-search for similar problems and how they are formulated externally
3. Gabi reviews, edits, agrees on problem definitions
4. Candidate-protocol shortlist (~3 per problem)
5. Gabi picks (possibly hybrid / modified)
6. Implementation plan (Gabi reviews + approves)
7. Code + tests

---

## 0. Terminology

| Term | Meaning |
|---|---|
| **bridge daemon** | The long-lived Node process that embeds the PGLite WASM session and serves the Postgres wire protocol on a TCP loopback port. The unit (Node + WASM together). |
| **WASM session** | The PGLite-internal SQL engine state inside the daemon. Distinct from the Node process — Node may be alive while WASM is hung. |
| **sidecar** | The discovery file `.pgdb/bridge.json` that names the daemon's host/port/pid. Not the daemon itself. |
| **consumer** | Any process invoked from this repo that connects to the bridge daemon. Examples: a Claude Code shell session, a tool launched by such a shell (`codeconv discover`, `D2NET-init`), an agent, an engineer-facing CLI. |
| **registration** | Atomic act by which a consumer announces "I am alive and using the bridge." |
| **orphaned** | A bridge daemon with zero registered consumers. |

All consumers — including engineer-launched tools alongside a shell — MUST abide by the protocol defined here. This is a contract.

---

## 1. Context constraints

These are facts about our environment. The candidate solutions in step 4 will be filtered against them.

- **Single WASM session per repo.** PGLite is single-session WASM. At most one bridge daemon per repo at any moment.
- **Cross-language clients.** Python (codeconv, future tools), .NET (D2Net.Init, D2Net.Scaffold), Node (bridge daemon itself, future Node tools). Any coordination primitive must be implementable in all three.
- **Cross-platform.** Windows 10/11 (primary, Gabi's environment) + macOS / Linux (the sibling GLP repo). No admin privileges assumed.
- **No external services.** No etcd, no ZooKeeper, no real Postgres, no Redis. The repo's filesystem and the bridge's TCP socket are the only coordination substrates.
- **PGLite cold init ~7 s on Windows** (memory `project_pglite_cold_init_windows.md`). Any timing window for declaring a starting bridge "failed" must accommodate this.
- **Filesystem semantics differ.** `mkdir` is atomic across all three OS classes. `O_CREAT|O_EXCL` is atomic on POSIX and on Windows (`CreateFileW` with `CREATE_NEW`). `proper-lockfile` (Node) uses mkdir; .NET's `FileShare.None` and Python's `portalocker` use byte-range / file-handle locking. These mechanisms do **not** interoperate at the same path.
- **No admin / no service manager.** Solutions cannot rely on systemd, launchd, or Windows Services.
- **Sleep / hibernate.** A laptop may sleep for hours; mtime-based heartbeats may falsely report "stale" on resume. Solutions should degrade gracefully.

---

## 2. The five coordination problems

Each problem is named, given a one-line definition, given an invariant we want to hold, and given the specific challenges that distinguish it from the textbook version.

### Problem A — Spawn race

**Definition**: When multiple consumers concurrently observe "no bridge daemon is running" and each attempts to ensure one exists, exactly one must end up starting the daemon; the others must defer.

**Invariant we want**:
- At any moment in repo time, the number of bridge daemons attempting to bind PGLite at `.pgdb/` is ≤ 1.
- Every losing racer eventually sees a working daemon.

**Specific challenges**:
- **Race window vs cold init.** Winner's daemon takes ~7 s to be reachable. Losers must wait that long before declaring "no winner."
- **Cross-language atomic primitive.** Coordination must work between Python / Node / .NET concurrently. The lock primitive each language can implement portably is not the same one that the others can.
- **Identity of winner is irrelevant.** The protocol only needs to elect *some* leader; we don't need fairness, priority, or stable identity.
- **No second-leader-after-first-fails recovery in the spawn-race protocol itself.** If the elected leader fails to actually start the daemon (e.g., Node missing from PATH, PGLite WASM init crash), losers must observe that failure and re-race. The race protocol must therefore include a notion of "leader's attempt failed, resume contention."

### Problem B — Daemon liveness

**Definition**: Given a sidecar pointing at a host/port (and possibly a pid + lock artefacts), determine with high confidence whether the bridge daemon is still functioning.

**Invariant we want**:
- A bridge daemon reporting "alive" can serve a `SELECT 1` over TCP within bounded time.
- A non-functional daemon (Node process dead, WASM hung, listener crashed) is detected and not used.

**Specific challenges**:
- **Three failure modes, three detection signals.**
  - Node process exits → kernel releases OS-level lock, but sidecar lingers.
  - Node process alive, WASM session corrupted/hung → lock heartbeat keeps refreshing, but `execProtocolRaw` stalls.
  - Node process alive, WASM healthy, TCP listener crashed → lock heartbeat fine, WASM fine, no clients can connect.
- **One signal alone is insufficient.** mtime heartbeat catches case 1 only. TCP ping catches case 3. End-to-end SQL roundtrip catches case 2 (and transitively the others).
- **Cost.** A liveness check fires on every consumer-startup AND periodically by the daemon itself. End-to-end SQL is cheaper than TCP-from-client because the daemon's self-ping reuses the in-process WASM session.
- **Sleep / suspend.** A laptop returning from suspend has stale mtimes everywhere; the daemon may legitimately have been frozen for hours. The protocol must distinguish "process is dead" from "process was suspended and is now resuming."
- **Time-source.** Wall-clock can jump (NTP, manual change, suspend/resume). `monotonic` is per-process. Cross-process freshness checks have to use wall-clock-with-tolerance OR a non-time signal (counter delta).

### Problem C — Consumer liveness (orphan detection)

**Definition**: Given a running bridge daemon, determine whether it still has at least one live registered consumer.

**Invariant we want**:
- The daemon's internal "live consumer count" is a sound under-approximation of reality (no false "orphaned" while a real consumer is alive).
- The daemon's count is also a tight enough over-approximation that an orphaned daemon is detected within minutes, not hours.

**Specific challenges**:
- **Crash leaves stale registration.** A consumer that does `os._exit()` or is `taskkill /F`'d cannot remove its own registration record.
- **Cross-platform "is pid X alive" is fragile.** On Windows, `OpenProcess(pid)` may return a handle to a *different* process if the OS has reused the pid. POSIX has the same hazard with shorter pid recycle.
- **Race: register-then-bridge-poll.** Consumer registers, atomically (mkdir or O_CREAT|O_EXCL), then connects. Bridge daemon's polling cycle must not happen between "consumer A is gone" and "consumer B is registered" without seeing B.
- **Should new consumers always register, even short-lived ones?** A subprocess of a Claude shell that runs `SELECT 1` and exits — does it need to register? Probably yes, otherwise its connection-bound use of the daemon won't extend the daemon's life beyond the parent shell's natural exit.
- **Granularity.** Per-process registration vs per-shell-session registration vs per-tool-invocation. Each has trade-offs in liveness fidelity and book-keeping cost.

### Problem D — Orphan shutdown policy

**Definition**: When the bridge daemon detects it is orphaned (problem C), decide when (and whether) to shut itself down gracefully.

**Invariant we want**:
- An orphaned daemon eventually shuts down (so it does not consume RAM forever after the last shell exits).
- A daemon that *will become non-orphaned soon* (because the user just opened a new shell that is about to register) does not shut down between consumer A's exit and consumer B's registration.

**Specific challenges**:
- **Cold-init penalty.** Shutting down "too eagerly" forces the next consumer to pay another ~7 s of cold init. There's a real cost to false orphan declarations.
- **Idle vs orphan are different concepts.** A daemon with one registered consumer and no in-flight queries is **not** orphaned. Idle timeout for "no work" is a separate thing.
- **Repo-close vs shell-exit.** Closing one Claude shell is not the same as closing the repo. There may be sibling processes launched by that shell still using the bridge.
- **Final shutdown obligations.** Release WASM session cleanly, close TCP, remove sidecar, kernel-release lock. None of these can be skipped without leaking.

### Problem E — Consumer startup protocol

**Definition**: When a process is invoked from this repo, it must end up either (a) connected to a live bridge daemon and registered as a consumer, or (b) raising a clearly-classified error explaining why this could not happen.

**Invariant we want**:
- A successful startup leaves the consumer registered.
- A failed startup does not leave a stale registration.
- Startup is composed of well-defined sub-steps, each idempotent so retries are safe.

**Specific challenges**:
- **Discovery uses problem B.** Stale sidecar must be detected.
- **Spawn uses problem A.** If discovery fails, race resolution starts.
- **Registration ordering.** Register-before-connect (so the daemon never serves an unregistered consumer)? Or connect-first-then-register (so registration failure cannot leave a "registered but never connected" entry)? They have different failure surfaces.
- **Daemon-shutting-down race.** Discovery succeeds, ping succeeds, but between ping and registration the daemon decides it's orphaned and starts shutting down. Consumer must retry the whole startup.
- **Composability.** A tool launched by a Claude shell may inherit the shell's registration, OR may need its own. Both designs are workable; the difference is where shutdown obligations live.

---

## 3. Cross-cutting properties we want from any solution

These are NOT problems in themselves but criteria for grading the solutions:

- **Cross-language portability.** Implementable in Python, Node, .NET (and ideally future Go / Rust without redesign).
- **Cross-platform portability.** No admin, no daemon-manager, works on Windows and POSIX with the same logic.
- **No external services.** Filesystem + TCP socket only.
- **Bounded recovery time.** From any failure (crash, kill, sleep), the system reaches a clean state in O(seconds), not O(minutes).
- **No false-positive shutdown.** A live consumer never has its bridge yanked.
- **Minimal cold-init penalty.** Avoid double cold inits where possible.
- **Diagnosable failure modes.** Exit codes / log lines / sidecar fields that let an engineer determine which problem occurred.
- **Stateless after exit.** No persistent state that requires manual cleanup if everything is killed.

---

## 4. What this document does NOT yet specify

These are intentionally deferred to step 4 (candidate solutions) and step 6 (implementation plan):

- The choice of coordination primitive (atomic file create vs lock vs leader election vs lease).
- The choice of liveness signal (heartbeat mtime vs counter delta vs end-to-end ping).
- The orphan threshold and shutdown delay numbers.
- The granularity of registration (per-process vs per-shell vs per-tool-invocation).
- The exact sidecar shape (we have one today; it may need additional fields).
- Whether D2NET / .NET tools also adopt the new protocol or keep their existing flow.
- Whether feature 012's contract documents (`bridge_lifecycle.md`, `bridge_cli.md`, `data-model.md`) get retroactively rewritten or only forward-amended.

---

## 5. Out of scope

These are explicitly NOT problems we are trying to solve here:

- **Multi-repo coordination.** Each repo gets its own bridge daemon; cross-repo coordination is not a goal.
- **Replicas / failover.** One bridge daemon per repo. No primary/secondary, no quorum.
- **Cross-machine.** All consumers and the daemon live on the same machine.
- **Authentication / authorization.** The daemon listens on loopback only; any local process with FS access can use it.
- **Capacity / scale.** PGLite is a single-developer DB; we are not optimizing for QPS or thousands of consumers.

---

## 6. Open questions for Gabi (please review before step 2)

1. **Are there problems above that you do NOT consider in scope?** I have included Problem D (orphan shutdown policy) as a separate problem from Problem C (orphan detection). It is possible you'd prefer to merge them. Let me know.
2. **Are there problems missing?** I have NOT included e.g. "graceful daemon upgrade" (replacing the daemon while consumers are connected) or "version skew between daemon and consumer," because nothing in our context requires them yet.
3. **Granularity of registration.** Do you have a preference between per-process, per-shell-session, per-tool-invocation? This affects the texture of Problems C/D heavily.
4. **Idle-vs-orphan separation.** Some prior art conflates them ("shut down after N seconds idle"). I have intentionally kept them separate. Confirm that is what you want.
5. **Cold-init penalty tolerance.** What is your acceptable rate of "extra ~7 s cold init due to false orphan declaration"? The shutdown delay number flows from this.
