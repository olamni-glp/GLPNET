# Transport unit — file & loopback endpoints (ILLUSTRATIVE, PLAN-STAGE)

**Feature:** 025 multi-protocol peer-to-peer link layer.
**Scheme(s) this unit owns:** `loopback` (deterministic in-memory transport) and `file`
(binary + text file endpoints: read/write/search, plus a file-backed append/replay channel).
**Status: ILLUSTRATIVE. NOTHING here is runnable yet.** Every GLP clause below uses the
**PROPOSED** base link primitives (`link_setup`/`server_listener`/`client_connector`/
`request_link`/`accept_link`/`link_send`/`link_recv`/`link_monitor`/`link_close`) and the
PROPOSED host kernels (`'_link_setup'/5`, `'_link_send'/3`, `'_link_monitor'/2`,
`'_link_close'/2`) whose runtime support is **not built** and is **pending Gabi's
language-authority approval** (CLAUDE.md §Language Authority; DISCIPLINE §1.14). The tests are
**SPEC-LEVEL**: each is a realistic scenario + exemplar GLP + an expected *observable* outcome
+ a pass/fail oracle, made runnable once implementation lands. Every clause is SRSW- and
mode-checked by hand. Outputs are constructed in clause **heads** (writer-mode), never via `=`
in the body — GLP is not Prolog.

This unit is the **simplest leaf** and the **substrate** for the whole feature:

- **`loopback`** is the deterministic in-memory transport that makes the headline **SC-001**
  byte-identical split testable *hermetically* (no real OS sockets, no wall-clock sleeps), and
  is the **only** transport on which the wire-fault injector (drop / reorder / duplicate /
  delay / partition) runs (per the transport-author seam: wire faults are required of the
  deterministic in-memory transport; real leaves are tested for bind-reactivation feasibility +
  graceful/abrupt close only). It is the engine behind **SC-008** (dedup) and **SC-012**
  (reorder/loss recovery).
- **`file`** gives a *durable, replayable* leaf: the producer's frames are appended to a log
  file; the consumer opens the same file and replays it. This is the smallest leaf with real
  persistence and is where graceful close (`[]` stream-end => an end-of-log marker) vs abrupt
  close (`link_close` => truncated/aborted log) is naturally observable.

Both schemes are **co-located / in-process or same-host** by construction, so under FR-029 they
are NOT "inter-host" and may use the PLAIN (non-TLS) variant — loopback and file endpoints are
exactly the spec's named non-inter-host case ("loopback and co-located in-process links are not
inter-host and may use PLAIN variants").

---

## 1. Scenario — the web-researched realistic use cases

### 1a. Loopback: a deterministic two-instance producer/consumer split (the SC-001 headline)

The headline equivalence test (US1/SC-001) needs a transport that carries one writer->reader
bind across *two REPL instances* while being **perfectly reproducible** — so a CI gate can
assert byte-identical output run after run, and so injected faults (drop/reorder/dup) replay
identically for debugging. This is precisely the **deterministic simulation testing** discipline
pioneered by **FoundationDB**: run the real software in a single-threaded discrete-event
simulator where *all* nondeterminism (network, disk, time, RNG) is funnelled through one seed,
and swap an **in-memory simulated network** in for the real one behind a single interface
pointer. FoundationDB's `Sim2` replaces real TCP (`Net2`/Boost.ASIO) with `Sim2Conn` objects
backed by a `std::deque<uint8_t>` buffer; latency, packet loss and partitions are injected by
`delay()`/`connection_failed()`/clogging checks driven by `deterministicRandom()`, so "same
seed, same execution path, every single time." WarpStream and others have since generalized
this ("interface swapping lets the same code run in both production and simulation"). [1][2][3]

Our `loopback` leaf is the GLP analogue: it implements the **same `ILinkTransport`/
`ILinkEndpoint` seam** every real leaf implements, but its send/recv are an in-memory queue
(a deque of frames) driven by the harness's single run seed, and its fault hooks (drop /
reorder / duplicate / delay / partition) are the seeded chaos. Because it satisfies the identical
seam, the *exact same role-parameterized GLP program* that runs over `ws`/`file`/`coap` runs
over `loopback` unchanged (FR-006/FR-013) — which is what makes it a faithful stand-in for the
real wire and the natural host for SC-001, SC-008 and SC-012.

**Why this transport fits:** SC-001 demands a *byte-identical* assertion and SC-012 demands
*reorder/loss recovery proven against the in-order baseline*. Neither is reliably testable over
a real socket (timing-dependent, flaky, OS-scheduler-dependent). A deterministic in-memory
transport makes the oracle exact and the failure reproducible — the FoundationDB lesson applied
to the link layer's correctness gates.

### 1b. File: an append-only log channel (durable produce -> replay consume)

The second concrete deployment is a **file-backed append/replay channel**: the producer serializes
each frame and **appends** it to a log file; the consumer opens the same file and **replays** the
frames in order. This is the ubiquitous **append-only log** / **write-ahead log (WAL)** /
**event-sourcing** structure: "entries are sequentially added to the end of a persistent record
and never modified or deleted," giving "simple concurrency semantics, consistent durability
guarantees, and efficient sequential I/O," and "the current state can be reconstructed by
replaying the list of events in sequence." Databases recover by replaying the log; Redis's AOF,
Kafka's partition log, and Raft/Paxos logs are all this structure. [4][5]

Durability and ordering rest on real OS guarantees we lean on (and must respect):

- **Append atomicity.** POSIX: with `O_APPEND`, "the file offset shall be set to the end of the
  file prior to each write and no intervening file modification operation shall occur between
  changing the file offset and the write operation" — a single descriptor's append is offset+write
  as one step. (POSIX does **not** promise cross-*process* serialization for arbitrary-size
  `O_APPEND` writes, which is why our frames carry their own length prefix + per-link sequence —
  the reliability sublayer, not the filesystem, owns ordering/dedup.) [6]
- **FIFO ordering on the same descriptor stream** — sequential append + sequential replay is
  naturally first-in-first-out, matching FR-018 per-link FIFO; the byte stream is "the first byte
  written is the first byte read." [7]
- **Durability is `fsync`-gated** — append to the page cache is not yet durable; an explicit
  `fsync` (or a background fsync worker) is what survives a crash. The leaf flushes on close /
  on a configurable cadence. [4]

**Why this transport fits:** a log channel is the simplest leaf that exhibits *real persistence,
replay-after-the-fact, and a natural close distinction* — graceful close writes an end-of-log
marker (the `[]` stream-end), abrupt close (`link_close`) leaves the log truncated and surfaces
`permFail`. It also makes "binary + text file endpoints (read/write/search)" (FR-012 file
endpoints) concrete: the binary variant frames length-prefixed `PayloadSerializer` blobs; the
text variant is a newline-delimited, search-indexable log.

### Sources

- [1] Pierre Zemb, "Diving into FoundationDB's Simulation Framework" — https://pierrezemb.fr/posts/diving-into-foundationdb-simulation/
- [2] WarpStream, "Deterministic Simulation Testing for Our Entire SaaS" — https://www.warpstream.com/blog/deterministic-simulation-testing-for-our-entire-saas
- [3] Phil Eaton, "What's the big deal about Deterministic Simulation Testing?" — https://notes.eatonphil.com/2024-08-20-deterministic-simulation-testing.html
- [4] "How Append Only Logs achieve durability in Databases?" — https://sahilserver.substack.com/p/append-only-logs
- [5] "Logs in Distributed Systems: A Guide" — https://blog.calvinsd.in/logs-in-distributed-systems-a-guide
- [6] POSIX `write()` (The Open Group Base Specifications) — https://pubs.opengroup.org/onlinepubs/9699919799/functions/write.html
- [7] "Named pipe / FIFO" (FIFO byte-ordering of pipes/FIFOs) — https://en.wikipedia.org/wiki/Named_pipe and GNU libc "Pipes and FIFOs" — https://www.gnu.org/software/libc/manual/html_node/Pipes-and-FIFOs.html

---

## 2. Protocol mapping — the uniform seam onto loopback and file

Both schemes implement the architecture-context host seam (PROPOSED, host-language, NOT GLP)
`ILinkTransport` / `ILinkEndpoint` = **open / send-bytes / recv-bytes / close + fault** (FR-058).
Nothing protocol-specific leaks above the seam, so the GLP program in §3 is identical for both
(only the `Scheme` string inside `LinkId` changes).

| Seam operation | `loopback` mapping | `file` mapping |
|---|---|---|
| `ListenAsync` (server-listener) | register an in-memory endpoint under `LinkId` in a process-global seeded registry; the In-queue is the peer's Out-queue | `open(logPath, O_RDONLY)` and tail-follow for replay; or create the log if listener owns it |
| `ConnectAsync` (client-connector) | look up / create the paired endpoint under the same ground `LinkId`; cross-wire the two deques | `open(logPath, O_WRONLY|O_CREAT|O_APPEND)` (writer) / `O_RDONLY` (reader) |
| `SendBytesAsync(frame)` | `deque.push_back(frame)` (subject to the seeded fault hook) | length-prefixed `write()` (append); flush per cadence |
| `RecvBytesAsync(ct)` | `deque.pop_front()` when non-empty; otherwise the GLP reader stays suspended | read next length-prefixed record from current offset; at EOF, await more or see end-marker |
| `CloseAsync` | drain + drop the endpoint pair; emit fault signal | write end-of-log marker (graceful) OR stop mid-log (abrupt); `fsync`; close fd |
| `OnFault` | seeded chaos -> `tempFail`/`permFail` (drop/partition/delay) | I/O error / truncated-record / EOF-without-marker -> `tempFail`/`permFail` |

### Establishment path

**Path A (listen/connect — primary for both):** one end is `server_listener(LinkId, …)`, the
other `client_connector(LinkId, …)`. For `loopback`, "listen" and "connect" both resolve the same
ground `LinkId` in the seeded in-memory registry and cross-wire the two deques — establishment
order is immaterial (idempotent at link-identity, FR-007). For `file`, the listener is whichever
end creates/owns the log; either side may be the data writer (FR-004 — which end writes the log is
a program concern, not the establishment role).

**Path B (request/accept handshake):** also supported and useful precisely because `loopback` is
the cheapest place to exercise it without a network. `request_link(LinkId, Peer, …)` parks a ground
`request(LinkId)` token in the in-memory rendezvous; `accept_link(LinkId, RequestStream, …)`
matches it by `LinkId? =?= LinkId2?` and converges on the **same** `'_link_setup'` registry, so the
resulting Link is indistinguishable from path A (FR-002 "equivalent established link"). A
`unit-INT-05` below exercises path B on loopback.

### The B->A back-channel mechanism

A `Link(In, Out)` is bidirectional by construction (the `Channel(In, Out)` shape), so the reverse
B->A direction is always present:

- **`loopback`** — there are two deques per link, one per direction; the reverse deque is the B->A
  back-channel with no extra machinery. This is where the credit/back-channel bounded-pipe
  (DESIGN §3) is most naturally and cheaply tested: the consumer's reverse `[more|Credits]` stream
  rides the same reverse deque the producer reads its credits from (see §3c).
- **`file`** — a single log file is one-directional by nature, so a bidirectional file link uses
  **two log files** (`A_to_B.log`, `B_to_A.log`); the reverse log carries B->A frames and credits.
  (A single-file half-duplex variant is the degenerate produce->replay case in §3a where only A->B
  is used.)

### TLS / security variant

Neither scheme is inter-host, so **FR-029 does not force TLS** — both are the spec's explicit
PLAIN-allowed case. The harness still asserts the FR-029 contract at the seam: an attempt to open
either with `InterHost = true` and `Tls = false` must be `LinkRefused` (unit-INT-08), proving the
guard is wired even though loopback/file themselves are local. There is no transport-layer crypto
for loopback (in-memory) or file (local fd); confidentiality of a file log is a filesystem-ACL
concern below the seam, not a link-layer guarantee.

### MTU / fragmentation + reliability

- **`loopback`** — no MTU; a frame is one deque element, so no fragmentation is needed. The
  reliability sublayer above the seam still runs (sequence + dedup + reorder buffer) because the
  *fault injector deliberately drops/reorders/duplicates whole frames* — that is the entire point
  of this leaf for SC-008/SC-012. The version byte + length/CRC framing (FR-022) is exercised here
  even though the in-memory queue could not corrupt it, so the same framing path is shared with
  real leaves.
- **`file`** — each record is a length-prefixed frame (length + version byte + payload + CRC),
  so a record larger than any read-buffer is reassembled by reading `length` bytes; this is the
  file analogue of fragmentation/reassembly (the same code path CoAP/BLE will need, FR-022).
  Reliability: per-link sequence numbers are written *into* each record so a replay that skips or
  re-reads a record (e.g. a partial last record from a crashed writer) is detected (bad-CRC /
  short-read -> `tempFail`) and deduped (FR-020/FR-021), never silently materialized as a wrong
  result (FR-020 "detect corruption rather than silently build a wrong result").

### Graceful close (`[]`) vs abrupt close (`link_close`)

- **Graceful (default) = stream-end `[]`.** The producer binds its `Out` tail to `[]`.
  - `loopback`: a terminal end-of-stream frame is pushed; the consumer's `link_recv`/`consume([])`
    fires; host GC runs; the monitor emits `closed(LinkId, eos)`.
  - `file`: an **end-of-log marker record** is appended and `fsync`'d; a replaying consumer that
    reads the marker sees `[]` and `consume([])` fires; `closed(LinkId, eos)`.
- **Abrupt = `link_close(LinkId)` / `link_close(LinkId, Reason)`** (the 9th primitive).
  - `loopback`: the endpoint pair is dropped immediately regardless of queued frames; the monitor
    emits `permFail(LinkId, Reason)`.
  - `file`: the writer stops mid-log with **no end-of-log marker**; a replaying consumer that hits
    EOF *without* a marker classifies it as `tempFail` then (on give-up) `permFail` — a truncated
    log is exactly an abrupt close, distinguishable from a clean `[]`. This EOF-without-marker
    case is the file leaf's signature fault and a regression test (unit-INT-07).

---

## 3. Exemplar GLP (ILLUSTRATIVE, PROPOSED primitives)

One role-parameterized program (branch on ground `AgentId`, FR-011). The `Scheme` is the only
thing that changes between `loopback` and `file`; everything else is shared.

### 3a. The headline split — establish -> send/receive -> graceful close

```prolog
-module(file_loopback_split_demo).

% ---- the one ground link identity both nodes compile in (never reused) ----
% Scheme "loopback" for the hermetic SC-001 run; swap to "file" for the durable
% append/replay run (only this clause changes; FR-006/FR-013).
procedure demo_link(LinkId).
demo_link(link_id("loopback", ep("nodeB", 0), 1)).

% ---- one entry point; ground AgentId selects the role (FR-011, the @/boot idiom) ----
procedure main(AgentId?).

% PRODUCER node boots  main(producer):
main(Me) :-
    Me? =?= producer |
    demo_link(L),
    client_connector(L?, Link, Faults),          % establish: connector role
    run_producer(Link?, Faults?).

% CONSUMER node boots  main(consumer):
main(Me) :-
    Me? =?= consumer |
    demo_link(L),
    server_listener(L?, Link, Faults),           % establish: listener role
    run_consumer(Link?, Faults?).

% ---- producer side: ground-relay a stream of values, then graceful-close ([]) ----
procedure run_producer(Link(_, _)?, FaultStream?).
run_producer(ch(_, Out?), _) :- produce([10, 20, 30], Out).

procedure produce(Stream(Integer)?, Stream(Integer)).
produce([V|Vs], [V?|Out?]) :- ground(V?) | produce(Vs?, Out).   % cons ground V in HEAD
produce([], []).                                                 % graceful close: Out := []

% ---- consumer side: receive head-by-head; [] = graceful close detected ----
procedure run_consumer(Link(_, _)?, FaultStream?).
run_consumer(ch(In, []), _) :- consume(In?).                    % we never send B->A here: [] head-constructs the closed outbound

procedure consume(Stream(Integer)?).
consume([V|In]) :- ground(V?) | use_value(V?), consume(In?).
consume([]).                                                    % close detected (inbound [])

procedure use_value(Integer?).
use_value(V) :- ground(V?) | '_output'(V?).                     % '_output'/1 = self.glp:73
```

**SRSW / mode hand-check (per clause):**
- `main/1` clauses: `Me` writer-in-head, read once in the `=?=` guard (ground-implying relaxation
  permits the single reader use). `L` writer (`demo_link`) -> `L?` read once. `Link`,`Faults`
  writers from the establishment call, each read once in `run_*`. Clean.
- `run_producer(ch(_, Out?), _)`: producer that writes outbound + IGNORES inbound (canonical form:
  `prod(ch(_, Out?), _) :- gen(Vals, Out).`). Inbound channel slot is bare `_` (ignored, no reader
  needed); outbound is the **reader hole** `Out?` in the head + the single writer `Out` in the body
  `produce(…, Out)` — one writer, one reader. The unused fault arg is bare `_` (a named `_Faults`
  at an unused slot is rejected: "[codegen] Undefined variable: _Faults"). Clean.
- `produce([V|Vs], [V?|Out?])`: `V?` appears in guard `ground(V?)` and head cons `[V?|…]` — legal
  because `ground/1` certifies groundness (guards-reference §Ground Guards SRSW relaxation). `Vs`
  read once; `Out` is a reader hole in the head, written once in the recursive call. Clean.
- `produce([], [])`: facts; `Out` head-constructs `[]` (graceful close, no `=` in body). Clean.
- `run_consumer(ch(In, []), _)`: consumer that reads inbound + closes its outbound (canonical form:
  `cons(ch(In, []), _) :- rd(In?).`). `In` is a **writer** capturing the inbound stream, read once
  via `consume(In?)`; the outbound stream is head-constructed closed as `[]` (this end sends nothing
  back — half-duplex use), which the REPL accepts. The unused fault arg is bare `_`. Clean.
- `consume([V|In])`: `V?` in guard + `use_value(V?)` — `ground(V?)` relaxation; `In` read once.
  Clean. `consume([])`: fact. Clean.
- `use_value(V)`: `V?` in guard + `'_output'(V?)` — `ground/1` relaxation. Clean.

Unsplit baseline equivalent (same observable output `10 20 30`): `produce([10,20,30], S), consume(S?)`
in one heap. The split must reproduce exactly this.

### 3b. File append/replay variant — only `demo_link/1` changes

```prolog
% Durable append/replay channel: producer appends frames to the log; consumer replays.
% Identical program body as §3a; ONLY the scheme string changes (FR-006/FR-013).
procedure demo_link(LinkId).
demo_link(link_id("file", "channel_AtoB.log", 1)).
```

Producer's `produce([], [])` writes the **end-of-log marker** (graceful `[]`); a later/offline
consumer that opens `channel_AtoB.log` replays `10, 20, 30`, sees the marker as `[]`, and prints
identically — demonstrating *replay after the fact* (the consumer need not be live during produce).

### 3c. Bounded-pipe credit / back-channel over loopback (the reverse deque)

The bounded pipe (DESIGN §3) couples the forward data stream to a reverse credit stream; "bounded"
is GLP suspension, not a buffer. Over `loopback` the reverse deque carries the credits, so this is
the cheapest place to test the credit/back-channel unification (DESIGN OQ-F3).

```prolog
% Window-of-3 bounded pipe over a loopback link. The consumer issues credits on the
% reverse (B->A) direction; the producer spends one credit per element and SUSPENDS when
% none remain (head unification on [more|Credits]). Pure suspend-on-reader, no buffer object.

Credit ::= more.

procedure produce_bounded(Stream(Item)?, Stream(Credit)?, Stream(Item)).
produce_bounded([Item|Items], [more|Credits], [Item?|Data?]) :-
    ground(Item?) | produce_bounded(Items?, Credits?, Data).
produce_bounded([], _, []).                              % source done -> graceful close

procedure consume_bounded(Stream(Item)?, Stream(Credit)).
consume_bounded(Data, [more, more, more | Credits?]) :-         % open the window: 3 credits
    drain(Data?, Credits).

procedure drain(Stream(Item)?, Stream(Credit)).
drain([Item|Data], [more | Credits?]) :- use_value(Item?), drain(Data?, Credits).  % 1 credit per use
drain([], []).
```

**SRSW hand-check:** `produce_bounded` clause 1: `Item?` in guard + head cons (`ground/1`
relaxation); `Items`,`Credits`,`Data` read once each. `consume_bounded`: `Data` read once in
`drain`; the reverse credit stream is written in the head `[more,more,more|Credits?]` and threaded.
`drain` clause 1: `Item?` read once (`use_value`), `Data` once, `Credits` written in head/threaded.
Clean. Invariant maintained: `items_produced - items_consumed <= 3` (the window). The credit stream
*is* the same reverse direction as any B->A application reply — one mechanism (DESIGN KEY INSIGHT).

### 3d. Monitor + abrupt close (fault-reactive consumer)

```prolog
% A fault-aware consumer that also watches the monitor stream and can abrupt-close.
procedure run_consumer_monitored(Link(_, _)?, FaultStream?).
run_consumer_monitored(ch(In, []), Faults) :-
    consume(In?), watch(Faults?).

procedure watch(FaultStream?).
watch([ok|Rest])              :- watch(Rest?).
watch([closed(L, R)|_])       :- ground(L?) | note_closed(L?, R?).      % graceful eos or link_close reason
watch([tempFail(L, R)|Rest])  :- ground(L?) | note_temp(L?, R?), watch(Rest?).
watch([permFail(L, R)|_])     :- ground(L?) | give_up(L?, R?).

procedure note_closed(LinkId?, Reason?).
procedure note_temp(LinkId?, Reason?).
procedure give_up(LinkId?, Reason?).
give_up(L, _) :- ground(L?) | link_close(L?, abandoned).               % 9th primitive: abrupt teardown
```

**SRSW hand-check:** `run_consumer_monitored(ch(In, []), Faults)`: same consumer-close form as §3a
(`In` **writer** captures inbound, read once via `consume(In?)`; outbound head-constructs `[]`), but
here the fault arg is **named** `Faults` because it IS read once via `watch(Faults?)` — so it stays
named, not bare `_`. Clean. `watch` clauses each read the head term once; `L?` in guard + handler call
(`ground/1` relaxation). `Rest` read once where threaded. `give_up`: `L?` in guard + `link_close`
(relaxation). Clean. `closed/2` is a member of the `Fault` union (DESIGN §1, RULED 2026-06-06), so
matching `closed(L, R)` over the `Stream(Fault)` monitor is well-typed; `permFail/2` drives
`link_close/2` for an explicit abrupt teardown.

---

## 4. UNIT test specs (REPL Section-A runtime + Section-B/C type-check)

These exercise the **GLP-surface** pieces this unit touches: the establishment/send/recv/close
wrappers (composable GLP over `ground/1`, `=?=`, head unification) and the guards they lean on.
They are pure-REPL, single-instance (no transport) — they verify the *clauses* compile, type-check,
and reduce, given the PROPOSED primitives exist. Format: goal + expected outcome. Each is runnable
once the primitives land; until then it is the spec of what must pass.

### Section A — runtime (`programs/tests/typed/` new files, single REPL)

- **unit-A-01 (graceful-close stream shape).** Load §3a `produce`/`consume` (no link; wire
  `produce` to `consume` in one heap via a shared stream). Goal:
  `produce([10,20,30], S), consume(S?).`
  **Expected:** prints `10`, `20`, `30`; goal **succeeds** (the `produce([],[])` / `consume([])`
  pair closes the stream). Oracle: stdout `_output` sequence == `[10,20,30]`, terminal success.
- **unit-A-02 (suspend-not-fail on unarrived head).** Goal: `consume(S?).` with `S` an unbound
  reader (no producer yet). **Expected:** the goal **suspends** (Drive => `Suspended`), does NOT
  fail and does NOT deadlock-as-error. Then a sibling binds `S = [10|_]`; **Expected:** `consume`
  **reactivates exactly once**, prints `10`, re-suspends on the new tail. Oracle: `Suspended`
  before bind; exactly one `_output(10)` after; never `Failed`.
- **unit-A-03 (`link_send` ground gate).** With the PROPOSED `link_send/3`, goal:
  `link_send(42, ch(In?, Out), C1).` **Expected:** **succeeds**, `Out` head bound to `42`
  (`Out = [42|_]`). Goal with an unbound writer payload: `link_send(W, ch(In?, Out), C1).`
  (`W` a writer) **Expected:** **suspends** on `ground(W?)` (never fails). Oracle: success+cons for
  ground; `Suspended` for unbound.
- **unit-A-04 (`link_recv` decons).** Goal: `link_recv(V, ch([42|In], Out?), C1).`
  **Expected:** **succeeds** with `V = 42`. Goal `link_recv(V, ch(InHead, Out?), C1).` with
  `InHead` an unbound reader: **suspends**, then bind `InHead = 42` => reactivate once, `V = 42`.
  Oracle: `42` captured exactly once; `Suspended` before bind.
- **unit-A-05 (bounded-pipe window).** Load §3c. Goal:
  `produce_bounded([a,b,c,d,e], Cs?, Data), consume_bounded(Data?, Cs).`
  **Expected:** all five `use_value` outputs appear; at no point are more than 3 items produced
  ahead of consumption (the producer suspends on the 4th until a credit returns). Oracle: outputs
  `a..e` in order; success. (Window-bound is asserted structurally; full backpressure-bound is the
  integration SC-013, out of this unit's scope but seeded here.)
- **unit-A-06 (`link_close` reason term).** With PROPOSED `link_close/2`, goal:
  `link_close(link_id("loopback", ep("nodeB",0), 1), abandoned).`
  **Expected:** **succeeds** (lowers to `'_link_close'(LinkId?, abandoned)`); the established
  link's monitor (if read) yields a terminal `permFail(LinkId, abandoned)` (asserted at
  integration level). Oracle: guard `ground(LinkId?), ground(Reason?)` passes; success.
- **unit-A-07 (clean-close monitor term match).** Load §3d `watch/1`. Goal:
  `watch([ok, tempFail(link_id("file","channel_AtoB.log",1), eof), closed(link_id("file","channel_AtoB.log",1), eos)]).`
  **Expected:** `note_temp` then `note_closed` fire; goal **succeeds**. Oracle: handler call order
  observed; the `closed/2` clause matched before any `permFail`.

### Section B — positive type-check (must compile)

- **unit-B-01.** §3a/§3b whole program type-checks: `Link(In,Out)` unifies with `ch(In, Out?)`
  (the `Channel` type, self.glp:15); `produce`/`consume`/`use_value` `procedure` decls are
  consistent. **Expected:** loads clean (the REPL pipeline SRSW->PE->typecheck->compile passes).
- **unit-B-02 (ground-implying SRSW relaxation accepted).** A clause reading a `ground/1`-grounded
  var multiply (the `produce` cons `[V?|…]` after `ground(V?)`) **compiles** — SC-006 positive for
  the `ground` guard this unit relies on.
- **unit-B-03 (`@<` over a compound LinkId/peer-id, if used for ordering).** A clause
  `pick(P1, P2, P1) :- P1? @< P2? | true.` over `peer(String,Integer)` peer-ids **compiles** and
  marks both operands grounded (the `@<` family is the guards-unit's deliverable; referenced here
  because a sorted-peer rendezvous over loopback would use it). **Expected:** compiles; both
  operands ground-implied (SC-006).

### Section C — negative type-check (must be rejected)

- **unit-C-01 (SRSW violation without ground guard).** The §3c `produce_bounded` clause body with
  the `ground(Item?)` guard **removed** but `Item?` still used twice (guard-less double read)
  **MUST be rejected** by the SRSW analyzer. **Expected:** compile-time SRSW error (SC-006
  negative). Confirms the relaxation is guard-driven, never a flag.
- **unit-C-02 (declined guard in this unit's clauses).** A consumer clause written with `==`
  instead of `=?=` (e.g. `consume([V|In]) :- V == done | …`) **MUST be rejected** (FR-036 decline).
  **Expected:** "Cannot call `==` in guard position" / unknown-guard compile error.
- **unit-C-03 (writer on the wire shape rejected).** A `link_send` call gated by `known/1` instead
  of `ground/1` over a payload containing an embedded reader (`link_send([add|Xs?], …)`) is
  flagged: the base is strictly ground-relay (FR-010/OQ-7), so the unit's lint/type rule rejects a
  non-ground payload at this layer. **Expected:** rejected (or, if expressed as a runtime guard,
  suspends forever / never crosses) — asserting no `_w`/`_r`/embedded reader can reach the wire.

> Note: unit-C-03's exact mechanism (compile-time lint vs runtime ground-suspend) is an open item
> (§7) — the *observable contract* (no embedded reader crosses the base wire) is the assertion.

---

## 5. INTEGRATION test specs (cross-instance over loopback/file, via the harness)

Each test targets the PROPOSED harness interface (`StartInstances`/`OpenLink`/`Inject`/`Drive`/
`Capture`/`AssertEquiv`/`CloseLink`/`Stop`), C# reference shape with the Dart mirror
behaviour-identical, all randomness from one run seed. Format: name + setup + action + expected
**observable** outcome + the SC it satisfies. These are SPEC-LEVEL — runnable once the primitives
+ the loopback/file leaves + the reliability sublayer land.

- **unit-INT-01 — SC-001 headline split, Dart<->Dart (loopback).**
  *Setup:* capture the unsplit baseline = `Capture` of one instance running
  `produce([10,20,30],S), consume(S?)` (stdout `10 20 30`). Then `StartInstances(file_loopback_split_demo,
  [Inst(producer, Dart), Inst(consumer, Dart)], seed)`; `OpenLink(producer, consumer, "loopback",
  {InterHost:false, Tls:false})`.
  *Action:* `Drive(consumer, main(consumer), deadline)` and `Drive(producer, main(producer),
  deadline)` to quiescence; `Capture` both; merge observable output.
  *Expected:* `AssertEquiv(splitMerged, baseline, ByteIdentical)` — split stdout byte-identical to
  the unsplit `10 20 30`; consumer `DriveResult = Done`; monitor ends with `closed(LinkId, eos)`.
  *Satisfies:* **SC-001** (Dart<->Dart half) + **SC-003** (one writer->reader bind reactivates the
  suspended consumer) for the loopback leaf.

- **unit-INT-02 — SC-001 headline split, Dart<->C# cross-runtime (loopback).** *(the mandated
  parity gate)*
  *Setup:* identical to INT-01 but `[Inst(producer, Dart), Inst(consumer, Csharp)]` (then also the
  swap `[producer Csharp, consumer Dart]`).
  *Action:* same Drive-to-quiescence; `Capture`; merge.
  *Expected:* `AssertEquiv(splitMerged, baseline, ByteIdentical)` — byte-identical to BOTH the
  unsplit baseline AND the Dart<->Dart split (INT-01). Wire bytes identical across runtimes
  (FR-060).
  *Satisfies:* **SC-001** (Dart<->C# gate, the release gate) + **SC-002** (one complete
  writer->reader bind reconstructed equal across runtimes) + **SC-003**.

- **unit-INT-03 — SC-001/SC-003 over the `file` append/replay leaf (offline replay).**
  *Setup:* `StartInstances(..., [producer, consumer], seed)`; `OpenLink(producer, consumer, "file",
  {InterHost:false, Tls:false})` with `demo_link = link_id("file","channel_AtoB.log",1)`. Drive ONLY
  the producer to quiescence first (consumer not yet started / suspended), so the log is written and
  graceful-closed (end-of-log marker).
  *Action:* THEN Drive the consumer (it opens and replays the already-written log).
  *Expected:* consumer stdout byte-identical to baseline `10 20 30`; consumer reactivates on each
  replayed record (FR-017/FR-051 across a durable medium); `closed(LinkId, eos)` from the marker.
  `AssertEquiv(…, ByteIdentical)`.
  *Satisfies:* **SC-001** (file leaf) + **SC-003** (bind reactivation over file).

- **unit-INT-04 — SC-008 idempotent redelivery is a verified no-op (loopback Duplicate fault).**
  *Setup:* INT-01 rig; `Inject(FaultSpec(Duplicate, link, {everyNthFrame:1}))` so every frame is
  delivered twice (and re-injected a third time after entry removal).
  *Action:* Drive to quiescence.
  *Expected:* stdout still exactly `10 20 30` (each value used **once**); NO agent crash, NO error
  raised or swallowed, NO re-bind, NO goal re-enqueue. `AssertEquiv(…, ByteIdentical)`; consumer
  `Done`. (Baseline today a duplicate frame crashes the agent — this asserts that is closed.)
  *Satisfies:* **SC-008** (FR-021).

- **unit-INT-05 — SC-012 reorder/loss recovery (loopback Reorder + Drop faults).**
  *Setup:* INT-01 rig with a multi-frame dependent stream;
  `Inject(FaultSpec(Reorder, link, {seed}))` and `Inject(FaultSpec(Drop, link,
  {dropThenRetransmit:true}))` — frames arrive out of order and some are dropped-then-retransmitted,
  all seeded for replay.
  *Action:* Drive to quiescence with the reliability sublayer ENGAGED.
  *Expected:* `AssertEquiv(splitMerged, baseline, CausalInOrder)` (or `ByteIdentical` if the
  reorder buffer fully restores order) — result equals the in-order single-instance run. Then a
  second run with the sublayer DISABLED: the harness asserts **corruption is detected** (a
  `tempFail`/`permFail` or an explicit corruption verdict), NOT a silently-wrong result.
  *Satisfies:* **SC-012** (FR-020), and SC-003 reactivation under perturbation.

- **unit-INT-06 — request/accept establishment path B (loopback).**
  *Setup:* `StartInstances`; instead of listen/connect, instance A runs `request_link(LinkId, B,
  Link, Faults)` and instance B runs `accept_link(LinkId, RequestStream, Link, Faults)` over the
  in-memory rendezvous.
  *Action:* Drive both; run the §3a produce/consume over the resulting link.
  *Expected:* one writer->reader bind crosses and reactivates the consumer; observable output ==
  baseline. The established Link is indistinguishable from INT-01's listen/connect link (FR-002
  "equivalent established link").
  *Satisfies:* **SC-001/SC-003** via path B (FR-002 second establishment path).

- **unit-INT-07 — graceful `[]` vs abrupt `link_close` distinction (file leaf).**
  *Setup:* two sub-cases over `file`. (a) producer graceful-closes (`produce([],[])` writes the
  end-of-log marker). (b) producer is `PeerKill`'d mid-log (`Inject(FaultSpec(PeerKill, …))`) so the
  log ends WITHOUT a marker.
  *Action:* Drive the consumer to replay each log; `Capture` the monitor stream.
  *Expected:* (a) consumer sees `[]`, monitor terminal `closed(LinkId, eos)`, `Done`. (b) consumer
  hits EOF-without-marker => monitor `tempFail(LinkId, eof)` then (on `ClockJitter` give-up advance)
  `permFail(LinkId, …)`; the consumer's DATA goal stays `Suspended` (never spuriously `Failed`); a
  fault-guarded `watch` clause becomes reducible.
  *Satisfies:* graceful-vs-abrupt close contract (DESIGN §4) + **SC-010**-style fault liveness on a
  file leaf (the loopback PeerKill liveness is the dedicated SC-010 test in the failure-model unit).

- **unit-INT-08 — FR-029 TLS-by-default guard wired (both schemes).**
  *Setup:* attempt `OpenLink(a, b, "file", {InterHost:true, Tls:false})` and the same for
  `"loopback"`.
  *Action:* observe the open result.
  *Expected:* both `LinkRefused` (InterHost && !Tls => refused, FR-029); with `InterHost:false`
  (the real loopback/file case) both succeed PLAIN. Asserts the secure-default guard is enforced at
  the seam even for local leaves.
  *Satisfies:* the FR-029 contract slice (US2 acceptance scenario 3 analogue for the
  non-inter-host leaves).

> Cross-runtime note: per the transport-author seam, wire faults (INT-04/05) run on the
> **loopback** (deterministic in-memory) leaf only. The `file` leaf's faults are its *natural*
> ones (truncated/EOF-without-marker, bad-CRC short record) — INT-07 — not the synthetic wire
> injector. This keeps T4 "one-platform-per-leaf" cheap: loopback carries the synthetic-fault
> burden hermetically for everyone.

---

## 6. Regression — the permanent set and the baseline gate

The following become the **permanent regression set** for this unit and are tied to the baseline
gate FR-067 / SC-017 (`bash test/run_all_tests.sh` green before and after every core-touching
change; the `=\=`-gated prelude still loads):

- **Section A** unit-A-01..A-07 -> added to `test/run_all_tests.sh` **Section A (runtime)** once the
  primitives land (new files under `programs/tests/typed/`: `link_stream_close.glp`,
  `link_send_recv.glp`, `link_bounded_pipe.glp`, `link_monitor_close.glp`).
- **Section B** unit-B-01..B-03 -> **Section B (positive type-check)**.
- **Section C** unit-C-01..C-03 -> **Section C (negative type-check)** (`link_decline_bad.glp`,
  `link_srsw_bad.glp`).
- **Integration** unit-INT-01 (Dart<->Dart loopback), **unit-INT-02 (Dart<->C# loopback — the
  mandated parity release gate, FR-062)**, unit-INT-03 (file replay), unit-INT-04 (SC-008 dedup),
  unit-INT-05 (SC-012 reorder/loss) are the **headline permanent regressions**. INT-02 is a
  **ship-blocking** gate (cross-runtime parity, FR-062/SC-002).
- The deterministic loopback's single-seed replay (FoundationDB discipline) is itself a regression
  asset: any INT-04/05 failure is reproducible by re-running with the recorded seed.

**Baseline tie:** because the reliability-sublayer dedup fix (FR-021) and any guard/ingress edits
this feature needs touch core, every regression above is gated by the standing FR-067/SC-017
assertion — no merge over a red `run_all_tests.sh`, and `self.glp` must still load.

---

## 7. Open items specific to this transport

- **OQ-FL1 (loopback determinism scope).** Does the loopback leaf drive *all* nondeterminism off
  the single run seed (latency, fault selection, frame-interleave order — the full FoundationDB
  `deterministicRandom()` discipline [1]), or only the explicit fault injector, leaving scheduling
  to the GLP runtime's own quiescence order? The byte-identical SC-001 oracle needs the *output*
  deterministic; full execution-path determinism is stronger and may be needed for INT-05 replay.
  Recommendation: full single-seed determinism for the in-memory leaf (it is cheap and makes every
  fault reproducible).
- **OQ-FL2 (file half-duplex vs two-file full-duplex).** §2 maps a bidirectional file link to two
  log files. Confirm: is the file leaf required to be full-duplex (two logs + reverse credits), or
  is the canonical file use the half-duplex produce->replay channel (one log, no B->A), with
  full-duplex deferred? Recommendation: ship half-duplex (one log) as the SC-001/SC-003 file leaf;
  keep two-file full-duplex as an extension (the bounded-pipe credit test §3c lives on loopback
  where the reverse deque is free).
- **OQ-FL3 (file durability cadence).** When does the file leaf `fsync` — per frame (durable but
  slow), per close only, or a background fsync worker [4]? This is a tuning parameter, not a
  correctness condition, but it affects what "abrupt close = truncated log" looks like under a real
  crash vs a `PeerKill`. Recommendation: `fsync` on graceful close + on the end-of-log marker;
  per-frame fsync as an opt-in for crash-durability tests.
- **OQ-FL4 (end-of-log marker encoding).** The graceful-close `[]` becomes a distinguished marker
  record in the file leaf. Confirm its encoding (a zero-length frame? a reserved version-byte
  value? a typed `eos` payload) so a truncated last record (abrupt) is never mistaken for a marker
  (graceful). This is the file analogue of WS CLOSE vs a dropped socket. Recommendation: a typed
  `eos` payload with its own CRC, distinct from any data frame.
- **OQ-FL5 (file-endpoint search facet).** FR-012 lists file endpoints "supporting read, write,
  **and search**." This unit covers read (replay) + write (append) fully; the *search* facet (e.g.
  seek-to-sequence-number, grep-a-log-by-term) is named but not exercised by the SC-001/008/012
  tests. Confirm whether search is in this feature's MVP for the file leaf or a documented
  later-facet. Recommendation: ship read+write+append/replay for the MVP; record search as a
  file-leaf extension with a seek-by-sequence-number sketch, since the per-record sequence number
  (FR-020) already provides the index key.
- **OQ-FL6 (unit-C-03 enforcement mechanism).** Whether "no embedded reader on the base wire" is a
  compile-time lint, a type rule, or purely the runtime `ground/1`-suspend behavior (a non-ground
  payload simply never crosses) is unresolved (ties to DESIGN OQ-7 `ground/1` vs `no_readers/1`).
  The observable contract is fixed (base is ground-relay, FR-010); the enforcement layer is the
  open item.
