# Bridge daemon coordination — external problem formulations

**Author**: Claude (assisted)
**Date**: 2026-05-10
**Status**: DRAFT for Gabi review (step 2 of 7-step design plan; step 1 = `01-problem-specification.md`)

This document maps each of our five internal problems (`01-problem-specification.md`) to externally documented formulations of the same problem. The goal is shared vocabulary: when we shortlist candidate protocols in step 4, we want to be picking from a well-understood landscape, not reinventing terminology.

For each external formulation: a short summary, a citation, and a "why this matches our problem" note. The citations are intentionally a mix of well-known systems (Postgres, SQLite, ZooKeeper, etcd, Kubernetes, systemd, D-Bus) and primary-source articles.

---

## Cross-cutting: atomic FS primitives

Two filesystem operations are atomic across Windows and POSIX with no admin privileges and no external services. They will appear in many of the per-problem mappings below, so they get their own subsection.

### Atomic `mkdir`

`mkdir(2)` is atomic on POSIX and Windows. Concurrent callers each get either success (one of them) or `EEXIST` (the rest). Whoever succeeds is the leader. ([linuxvox.com — atomic file create](https://linuxvox.com/blog/atomic-create-file-if-not-exists-from-bash-script/), [github — NamedAtomicLock uses POSIX `mkdir`](https://github.com/kata198/NamedAtomicLock).) `proper-lockfile` (used by our bridge today) is built on this.

### Atomic `O_CREAT|O_EXCL` (POSIX) / `CREATE_NEW` (Windows)

`open(path, O_CREAT|O_EXCL)` succeeds at-most-once for concurrent callers. Windows' `CreateFileW` with `CREATE_NEW` is the equivalent. ([rcrowley — things UNIX can do atomically](https://rcrowley.org/2010/01/06/things-unix-can-do-atomically.html).)

Both primitives are documented as "the foundation for implementing robust distributed locking and leader election protocols without requiring complex consensus algorithms" ([linuxvox]).

---

## Problem A — Spawn race

### External formulation A1 — "Single-instance application"

A long-standing desktop / utility-software pattern: when a user double-clicks the app icon a second time, the second invocation should defer to the first. Implementations: D-Bus's "well-known name request" returning `NameInUse`; Linux abstract-namespace Unix-domain sockets (`bind` returns `EADDRINUSE`); Wails' named mutex on Windows / macOS; Tcl / .NET / Python / Java cookbooks all map to a single primitive: "atomic claim of a globally-unique name."

- D-Bus: ["Bus names can be used as a simple way to implement single-instance applications (second instances detect that the bus name is already taken)"](https://en.wikipedia.org/wiki/D-Bus). Atomic name acquisition via `RequestOwnership()`.
- Linux abstract Unix-domain sockets: ["The first instance listens on the socket while all other instances fail to bind to that socket"](https://blog.petrzemek.net/2017/07/24/ensuring-that-a-linux-program-is-running-at-most-once-by-using-abstract-sockets/). `EADDRINUSE` is the loser signal.
- Wails (Electron-like): ["Single instance lock is implemented using a named mutex, with the mutex name generated from the unique id that you provide"](https://wails.io/docs/guides/single-instance-lock/) — works on Windows + macOS.
- ActiveState Python: [cross-platform via fcntl + Win32 mutex](https://code.activestate.com/recipes/578453-python-single-instance-cross-platform/).

**Why this matches our problem**: the textbook formulation IS our spawn-race problem with a different framing — second-instance-launch becomes "second-consumer-trying-to-spawn-bridge" — and we want exactly the same outcome (second one detects, defers).

### External formulation A2 — "Leader election with atomic store"

Generalised version: multiple processes attempt to write their identity to a single conditional-write store; whoever wins becomes the leader. Lease-based variants add automatic re-election when the leader fails to renew. The Amazon Builders' Library article and Google's Cloud-Storage-as-leader-election guide both name this pattern.

- AWS Builders' Library: ["A distributed database record or file can be used to name the current leader, and when a leader has renewed its leadership lock"](https://aws.amazon.com/builders-library/leader-election-in-distributed-systems/) — file-as-record is exactly our case.
- Google Cloud Blog: [Leader Election on Google Cloud Storage](https://cloud.google.com/blog/topics/developers-practitioners/implementing-leader-election-google-cloud-storage) — single object's atomic write becomes the election.
- Gunnar Morling on S3 conditional writes: [Leader Election With S3 Conditional Writes](https://www.morling.dev/blog/leader-election-with-s3-conditional-writes/) — same pattern, different store.

**Why this matches**: same pattern, with the added lease/expiration concept that may map to our problem B (liveness).

### External formulation A3 — "Postgres `postmaster.pid`"

Postgres handles startup races against a fixed data-directory by writing `postmaster.pid` and refusing to start if a non-stale one is found. This is the closest external analogue to our bridge daemon's lockfile, because the failure mode is identical (single shared resource that does not tolerate two writers). Documented races: ["postmaster's startup sequence cleans up old temporary files… delaying the writing to the PID file. This delay caused pg_ctl to timeout, leaving behind an orphaned postgres.exe process"](https://access.redhat.com/solutions/906013) — same risk we have with PGLite cold init.

**Why this matches**: it's literally the same problem with the same data shape (single data directory + lockfile + pid). The Postgres community has 25+ years of bug reports on this race; we should learn from the failure modes documented there.

### External formulation A4 — "Mutual exclusion via mkdir / link"

The minimal academic formulation. POSIX guarantees `link(2)` is atomic; combined with `mkdir`, this gives mutex on top of nothing more than the filesystem. ([rcrowley](https://rcrowley.org/2010/01/06/things-unix-can-do-atomically.html), [pwillis-els gist](https://gist.github.com/pwillis-els/b01b22f1b967a228c31db3cf2789ee13).) Used by procmail, `mutt`, and historically every C program needing a lockfile.

**Why this matches**: this is the most parsimonious formulation. Our existing `proper-lockfile` is one wrapper on top of this; we could use it directly.

---

## Problem B — Daemon liveness

### External formulation B1 — "Heartbeat-with-stale-detection"

A widely cited pattern: the holder of a resource (lock, role, lease) periodically refreshes a timestamp; observers consider it dead if the timestamp is older than `stale_ms`. proper-lockfile's `update: 500, stale: 1000` is one instance.

- QNX docs: ["Clients can assert 'liveness' properties by actively sending heartbeats… when a process deadlocks or starves and makes no progress, it will no longer heartbeat"](http://www.qnx.com/developers/docs/qnxcar2/topic/com.qnx.doc.ham_en.dev_guide/topic/examples_LIVENESS.html).
- ProxySQL watchdog: heartbeat threads, watchdog raises alarms on timestamp staleness.
- Generic file-system version: ["have processes put current timestamps in a shared location and have a watchdog daemon check those timestamps in its main loop, raising an alarm if one of them is staled"](https://proxysql.com/documentation/watchdog/).

**Why this matches**: directly addresses our problem-B-case-1 (Node process dead, lock mtime no longer refreshing). Cheap, portable. **Limitation**: doesn't catch case 2 (Node alive, WASM hung).

### External formulation B2 — "Watchdog timer with self-petting"

Linux kernel and systemd's `WatchdogSec`: the daemon must call `sd_notify(WATCHDOG=1)` periodically OR the supervisor restarts it. The petting itself can be made conditional on an end-to-end check.

- systemd: ["WatchdogSec enables a heartbeat mechanism where the service must send sd_notify(STATUS=WATCHDOG=1)… or systemd kills and restarts it. WatchdogSec allows services to report their own health… useful when a service might be alive as a process but stuck and unresponsive"](https://oneuptime.com/blog/post/2026-03-02-configure-systemd-restartsec-watchdogsec-ubuntu/view).
- Linux kernel: ["if any CPU in the system does not receive any hrtimer interrupt (heartbeat) during the 'watchdog_thresh' window, the 'hardlockup detector' will generate a kernel warning or call panic"](https://docs.kernel.org/admin-guide/lockup-watchdogs.html).

**Why this matches**: addresses problem-B-case-2 specifically. The petting being conditional on a real internal check (e.g., `await pglite.exec('SELECT 1')` succeeding) means a hung WASM session stops petting.

### External formulation B3 — "Kubernetes liveness / readiness / startup probes"

K8s splits "is alive" into three probes:
- **startup**: long timeout while the container initialises (matches our PGLite cold-init).
- **readiness**: routes traffic when ready, removes when not (matches "is the daemon serving?")
- **liveness**: restart on persistent failure (matches "is the daemon stuck?").

> "Once the startup probe has succeeded once, the liveness probe takes over to provide a fast response to container deadlocks." ([k8s docs](https://kubernetes.io/docs/concepts/configuration/liveness-readiness-startup-probes/)).

**Why this matches**: the three-probe split is conceptually exactly what our problem B asks for. The startup probe in particular acknowledges "cold init takes a while" without making the liveness deadline lax forever.

### External formulation B4 — "Lease with TTL"

etcd's lease primitive: TTL granted at creation, holder must `KeepAlive` to extend, expiry deletes any keys attached.

- etcd: ["the cluster grants leases with a time-to-live and a lease expires if the etcd cluster does not receive a keepAlive within a given TTL period"](https://etcd.io/docs/v3.4/learning/api/).
- CNCF: [Lease implementation mechanism](https://www.cncf.io/blog/2023/11/01/mechanism-and-implementation-of-lease/).

**Why this matches**: lease-with-TTL is heartbeat-with-stale-detection in disguise, but with cleaner semantics (the lease IS the lock; expiry IS the failure detection). Composes well with reference counting.

---

## Problem C — Consumer liveness (orphan detection)

### External formulation C1 — "ZooKeeper ephemeral nodes"

The canonical reference-counting + liveness primitive: each connected client owns ephemeral ZNodes; the cluster deletes them when the client's session expires.

- ZK docs: ["Ephemeral znodes exist as long as the session that created the znode is active, and when the session ends the znode is deleted"](https://zookeeper.apache.org/doc/r3.4.13/zookeeperProgrammers.html). Session expiry: ["At session expiration the cluster will delete any/all ephemeral nodes owned by that session and immediately notify any/all connected clients of the change"](https://zookeeper.apache.org/doc/r3.2.2/zookeeperOver.html).
- Known failure modes: orphaned ephemeral nodes when network partitions during creation (ZOOKEEPER-1367, -2355, -3018) — relevant to our "registration must be atomic" challenge in problem E.

**Why this matches**: ephemeral-node-on-server is exactly our "registration that should auto-clean when consumer dies." The per-shell-lockfile design from my earlier proposal is a poor-man's version of this.

### External formulation C2 — "TCP connection counting"

Network daemons (HTTP servers, postgres backends, sshd) commonly count active TCP connections as a liveness/orphan signal. SSH idle-timeout uses it: ["if the maximum count is reached, sshd will disconnect the idle session"](https://help.strongdm.com/hc/en-us/articles/20854428921613-SSH-Idle-Connection-Timeout).

**Why this matches**: TCP-connection-count gives "is anyone actively connected?" for free. **Limitation**: a consumer might disconnect for 30 s during normal work and the daemon would incorrectly call it orphaned.

### External formulation C3 — "Reference-counted COM / Unix processes / smart pointers"

Foundational pattern: every owner increments a refcount, every release decrements, the resource self-destructs at zero. Found in COM (`AddRef`/`Release`), kernel reference counting, C++ `shared_ptr`, etc. Translation to our setting: each consumer creates a registration file; daemon polls; emptiness ⇒ refcount zero.

**Why this matches**: the simple version of C1 without ZooKeeper's session-expiry magic. Stale registrations require explicit cleanup logic.

### External formulation C4 — "Kernel-released ephemeral artefact"

A subset of the lock primitives that auto-release on process exit:
- `fcntl(F_SETLK)` advisory locks: released when the holding fd is closed (which the kernel does on process exit).
- Linux `flock` system call: same.
- Windows `LockFileEx`: same.
- proper-lockfile's "stale" detection is an emulation in user space.

**Why this matches**: if each consumer holds an OS-level fd-lock at a unique path, the kernel auto-cleans on crash. Daemon polls "are any unique paths locked?" as the orphan signal. Cross-platform, no admin, robust to crash.

---

## Problem D — Orphan shutdown policy

### External formulation D1 — "Idle timeout"

After N seconds of no work, shut down. SSH, mod_wsgi, AWS SDK's IdleConnectionReaper, Dask scheduler all do this. Note the Dask issue exposing the failure mode: ["Idle shutdown only happens if no workers are processing any tasks and there are no unrunnable tasks"](https://github.com/dask/distributed/issues/5675) — i.e., even Dask conflates "idle" with "orphaned" and gets bug reports.

**Why this matches**: the most common policy. Easy to implement. **Risk**: false positives when the user is just thinking between commands.

### External formulation D2 — "Linger / TIME_WAIT"

TCP's `TIME_WAIT` and SO_LINGER: don't tear down immediately; wait long enough that any racing handshake from a new connection is acknowledged. Translation: daemon detects orphan, waits `linger_seconds`, re-checks, then shuts.

**Why this matches**: directly addresses the cold-init-penalty concern. The linger window is the "user is between two consumers" gap.

### External formulation D3 — "Graceful drain + shutdown"

K8s pre-stop hook + `terminationGracePeriodSeconds`; nginx's graceful reload. Daemon refuses new consumers, waits for outstanding work, then shuts.

**Why this matches**: addresses problem-D-final-shutdown-obligations. Distinguishes "stop accepting new" from "kill in-flight."

### External formulation D4 — "Lease-driven shutdown"

When the daemon's own renewal lease expires (e.g., it was meant to stay alive only as long as the orchestrator told it to), it self-terminates. etcd's `RevokeLease` is the explicit version.

**Why this matches**: ties shutdown to a fail-safe expiry rather than only to the orphan check; protects against "no consumers AND orphan-poll has been broken for some reason → bridge stays up forever."

---

## Problem E — Consumer startup protocol

### External formulation E1 — "Sidecar pattern + service discovery"

K8s sidecar pattern + DNS-SD / mDNS: a well-known location publishes "where is the service?"; clients read, connect, register.

**Why this matches**: our `.pgdb/bridge.json` is exactly the sidecar discovery file. The startup sequence is read sidecar → ping → connect → register, which is the standard composition.

### External formulation E2 — "Postgres `pg_ctl start`"

Postgres' canonical `pg_ctl start`:
1. Read `postmaster.pid` if present.
2. If process is alive → already running, exit.
3. Else → remove stale pid file, start.
4. Wait for ready signal (the `postmaster.pid` reaches a "ready" state).

The known race ([Postgres list](https://groups.google.com/g/pgsql.general/c/ZpFpRYQc260)) is exactly our race: "lock file already exists but actually no process is using it."

**Why this matches**: closest external analogue. Same data shape, same race surface, same recovery protocol.

### External formulation E3 — "Connect-with-fallback (database connection pool)"

A connection pool's `acquire()` is the canonical "discover-or-spawn" composition: try a cached connection, ping it, fail-fast on dead, replace with new. Maps to "discover-or-spawn-bridge" with "connection" → "bridge".

**Why this matches**: a generalisation of E1 that explicitly handles the "discovered endpoint is dead" path.

### External formulation E4 — "ZooKeeper client session establishment"

The opposite end of C1: ZK clients establish a session, are issued an ephemeral identity, and re-register if disconnected. The session is the unit of liveness.

**Why this matches**: each Claude shell starting up is the analogue of "establishing a session"; subsequent in-shell tools "join" the session rather than registering separately.

---

## Summary table — internal-to-external mapping

| Our problem | Best-matching external formulations | Why |
|---|---|---|
| A. Spawn race | A3 (postmaster.pid), A1 (single-instance), A4 (mkdir) | A3 is structurally identical; A1 + A4 are well-documented primitives. |
| B. Daemon liveness | B3 (k8s startup+liveness probes), B2 (watchdog-with-self-petting) | B3 splits cold-init from runtime correctly; B2 catches WASM hangs. |
| C. Orphan detection | C4 (kernel-released fd-locks), C1 (ZK ephemeral nodes) | C4 is the cross-platform / no-external-service version of C1. |
| D. Orphan shutdown | D2 (linger / TIME_WAIT), D3 (graceful drain) | D2 directly addresses our cold-init concern; D3 covers shutdown obligations. |
| E. Consumer startup | E2 (pg_ctl start), E1 (sidecar+discovery) | E2 is the operational template; E1 is our sidecar.json mechanism. |

---

## Open invitations to Gabi

- **Did I miss an analogue?** In particular: I considered but did not include Raft or Paxos (overkill for one-machine, no quorum), DRBD / Pacemaker (HA cluster software, wrong scale), and SQLite WAL locking (single-process or in-process file locking, not cross-process daemon coordination — but worth flagging as the closest "single-writer-multi-reader-on-one-machine" analogue). Tell me if any of these belong, or if there are domains I haven't tapped (e.g. game-engine asset-server patterns, MUD daemon patterns, JVM-based singleton servers like Java `RMI`).
- **Are any of these the wrong match?** I rated B3 (k8s probes) as the best for problem B; you may prefer B2 (watchdog) or B4 (lease) — the three are not mutually exclusive but the choice affects the design's centre of gravity.
- **Hybrid framings.** It is plausible that our "best fit" formulation is a hybrid (e.g., postmaster.pid for spawn race + k8s probes for liveness + kernel-released fd-lock for orphan detection + linger for shutdown). I have not yet committed to any combination — that is the next round.

---

## Sources

Atomic FS primitives + spawn race:
- [linuxvox — atomic create file](https://linuxvox.com/blog/atomic-create-file-if-not-exists-from-bash-script/)
- [pwillis-els — atomic file locking gist](https://gist.github.com/pwillis-els/b01b22f1b967a228c31db3cf2789ee13)
- [rcrowley — things UNIX can do atomically](https://rcrowley.org/2010/01/06/things-unix-can-do-atomically.html)
- [github — NamedAtomicLock (mkdir-based)](https://github.com/kata198/NamedAtomicLock)
- [AWS Builders' Library — leader election](https://aws.amazon.com/builders-library/leader-election-in-distributed-systems/)
- [Google Cloud Blog — leader election on GCS](https://cloud.google.com/blog/topics/developers-practitioners/implementing-leader-election-google-cloud-storage)
- [Gunnar Morling — S3 conditional writes leader election](https://www.morling.dev/blog/leader-election-with-s3-conditional-writes/)
- [Wails — single-instance lock](https://wails.io/docs/guides/single-instance-lock/)
- [ActiveState — Python single-instance recipe](https://code.activestate.com/recipes/578453-python-single-instance-cross-platform/)
- [Petr Zemek — abstract Unix-domain-socket single instance](https://blog.petrzemek.net/2017/07/24/ensuring-that-a-linux-program-is-running-at-most-once-by-using-abstract-sockets/)
- [D-Bus — Wikipedia article on bus names](https://en.wikipedia.org/wiki/D-Bus)
- [Postgres — postmaster.pid stale errors article](https://mccarthydanielle.medium.com/postgres-postmaster-pid-errors-5bdf3c2522fd)
- [Red Hat — postmaster dead but pid file exists](https://access.redhat.com/solutions/906013)
- [Postgres mailing list — startup race report](https://groups.google.com/g/pgsql.general/c/ZpFpRYQc260)

Liveness:
- [QNX — heartbeating clients](http://www.qnx.com/developers/docs/qnxcar2/topic/com.qnx.doc.ham_en.dev_guide/topic/examples_LIVENESS.html)
- [ProxySQL — watchdog](https://proxysql.com/documentation/watchdog/)
- [Linux kernel — softlockup / hardlockup detector](https://docs.kernel.org/admin-guide/lockup-watchdogs.html)
- [oneuptime — systemd WatchdogSec](https://oneuptime.com/blog/post/2026-03-02-configure-systemd-restartsec-watchdogsec-ubuntu/view)
- [k8s docs — liveness/readiness/startup probes](https://kubernetes.io/docs/concepts/configuration/liveness-readiness-startup-probes/)
- [etcd v3.4 — leases API](https://etcd.io/docs/v3.4/learning/api/)
- [CNCF — Lease mechanism](https://www.cncf.io/blog/2023/11/01/mechanism-and-implementation-of-lease/)

Consumer / orphan:
- [ZooKeeper — programmer's guide (3.4.13)](https://zookeeper.apache.org/doc/r3.4.13/zookeeperProgrammers.html)
- [ZooKeeper overview — ephemeral nodes](https://zookeeper.apache.org/doc/r3.2.2/zookeeperOver.html)
- [ZOOKEEPER-3018 — ephemeral node not deleted](https://issues.apache.org/jira/browse/ZOOKEEPER-3018)
- [ZOOKEEPER-1367 — orphaned ephemeral nodes](https://issues.apache.org/jira/browse/ZOOKEEPER-1367)
- [strongDM — SSH idle connection timeout](https://help.strongdm.com/hc/en-us/articles/20854428921613-SSH-Idle-Connection-Timeout)
- [Dask — idle shutdown bug report](https://github.com/dask/distributed/issues/5675)

Shutdown / drain / linger:
- [oneuptime — systemd RestartSec / WatchdogSec](https://oneuptime.com/blog/post/2026-03-02-how-to-configure-systemd-watchdog-for-service-health-checks-on-ubuntu/view)
- [Apache mod_wsgi — WSGIDaemonProcess directives](https://modwsgi.readthedocs.io/en/master/configuration-directives/WSGIDaemonProcess.html)
- [AWS Java SDK — IdleConnectionReaper](https://docs.aws.amazon.com/AWSJavaSDK/latest/javadoc/com/amazonaws/http/IdleConnectionReaper.html)

SQLite WAL locking (closest single-machine writer-coordination analogue, kept on the list for completeness):
- [SQLite WAL documentation](https://sqlite.org/wal.html)
- [SQLite locking v3](https://sqlite.org/lockingv3.html)
- [oldmoe — single-writer DB architecture with SQLite](https://oldmoe.blog/2024/07/08/the-write-stuff-concurrent-write-transactions-in-sqlite/)
