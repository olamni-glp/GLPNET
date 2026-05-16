// Postgres-wire bridge for PGLite, supporting Python (psycopg / SQLAlchemy /
// Alembic / DBOS) and .NET (Npgsql / psqlODBC) consumers.
//
// Lineage (see specs/011-prereq-patterns-catalog/pglite-merge-analysis.md for
// the full classification):
//   - Skeleton + hand-rolled startup + flush-boundary batching come from
//     glpnet's `docs/research/pgbridge-reference/bridge-direct.mjs`. That
//     investigation diagnosed two bugs and demonstrated that skipping
//     pg-gateway was the simplest, correct path: PGLite's implicit-Sync after
//     `execProtocolRaw` (fixed by buffering frames until a Sync/Flush/
//     Terminate/CopyDone/CopyFail boundary) and pg-gateway 0.3.0-beta.4
//     response-stream corruption (avoided by not using pg-gateway at all,
//     which transitively gave Npgsql / psqlODBC compatibility).
//   - Cross-connection serialisation (`globalWorkChain`) and session-state
//     safety (synthetic `ROLLBACK` on startup) come from a downstream
//     descendant of bridge-direct.mjs that PGLite's Python consumers
//     (psycopg / SQLAlchemy / DBOS) needed to keep responses ordered across
//     PGLite's single shared WASM session.
//   - Repo-wide single-bridge invariant (cross-process file lock, sidecar
//     JSON discovery, auto-spawn READY token, rotated --daemon log) comes
//     from feature 012 (specs/012-codeconv-runner/contracts/bridge_*.md).
//
// Pinned to `@electric-sql/pglite@0.2.17` (see ./package.json). The 0.2.x
// API surface is `PGlite.create()`, `pglite.exec()`, `pglite.execProtocolRaw()`.
//
// `COPY ... FROM STDIN` is FORBIDDEN with PGLite — full stop. PGLite WASM
// does not implement COPY-IN over the wire (it crashes the WASM session),
// and the merged bridge does not pretend to support it. Callers must not
// issue `COPY FROM STDIN` against PGLite under any circumstances. See
// specs/011-prereq-patterns-catalog/pglite-merge-analysis.md row A4.

import { createServer } from 'node:net';
import {
  existsSync,
  mkdirSync,
  open as fsOpen,
  readdirSync,
  readFileSync,
  renameSync,
  unlinkSync,
  writeFileSync,
  writeSync as fsWriteSync,
} from 'node:fs';
import { open as fsOpenAsync } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { PGlite } from '@electric-sql/pglite';
import lockfile from 'proper-lockfile';

import { createRotatingStream } from './log_rotator.mjs';

// --------- coordination protocol constants (recommended-set experiment) ---------
const SELF_PING_INTERVAL_MS    = 5000;   // SQL self-ping cadence (B liveness)
const ORPHAN_POLL_INTERVAL_MS  = 5000;   // poll .pgdb/consumers/ cadence (C)
const ORPHAN_LINGER_MS         = 30000;  // linger before graceful shutdown (D)
const SHUTDOWN_MARKER_POLL_MS  = 1000;   // poll .pgdb/.shutdown (D non-destructive force)

// Force synchronous (blocking) stdout writes so the parent (which is reading
// our stdout via a pipe) sees BRIDGE_READY immediately. Node block-buffers
// piped stdout by default; without this, Python clients can wait 30+ seconds
// for the line because Node only flushes on process exit. setBlocking(true)
// is a private libuv API but stable and widely used.
try {
  if (process.stdout && process.stdout._handle && typeof process.stdout._handle.setBlocking === 'function') {
    process.stdout._handle.setBlocking(true);
  }
  if (process.stderr && process.stderr._handle && typeof process.stderr._handle.setBlocking === 'function') {
    process.stderr._handle.setBlocking(true);
  }
} catch (_e) { /* tolerate older Node */ }

const args = parseArgs(process.argv);
if (!existsSync(args.pgdir)) mkdirSync(args.pgdir, { recursive: true });

const sidecarPath = join(args.pgdir, 'bridge.json');
// Lock is placed SIBLING to the data dir (e.g. `.pgdb.bridge.lock/` next to
// `.pgdb/`) rather than inside it: PGLite refuses to initialize a data-dir
// that has any non-PG file present at init time, and proper-lockfile creates
// its lock as a directory. See contracts/bridge_lifecycle.md "Lock semantics".
const lockPath = `${args.pgdir}.bridge.lock`;

// Acquire the cross-process bridge lock (FR-002, FR-003). On failure, emit
// the contracted BRIDGE_LOCK_HELD line and exit 5. The lock is kernel-released
// on process exit; we do NOT explicitly release it on graceful shutdown.
let lockRelease = null;
if (!args.noLock) {
  try {
    lockRelease = await lockfile.lock(args.pgdir, {
      lockfilePath: lockPath,
      retries: 0,
      stale: 1000,
      update: 500,
      realpath: false,
    });
  } catch (e) {
    let detail = 'pid=? (sidecar absent)';
    try {
      const existing = JSON.parse(readFileSync(sidecarPath, 'utf8'));
      if (existing && typeof existing.pid === 'number') {
        detail = `pid=${existing.pid} at ${existing.host}:${existing.port}`;
      }
    } catch { /* sidecar absent or malformed — leave default */ }
    console.error(`[bridge] BRIDGE_LOCK_HELD ${detail}`);
    process.exit(5);
  }
}

let pglite;
try {
  pglite = await PGlite.create(args.pgdir);
} catch (e) {
  console.error(`[bridge] BRIDGE_ERROR pglite_init_failed ${(e && e.message) || e}`);
  process.exit(1);
}
console.error(`[bridge] pglite ready data_dir=${args.pgdir}`);

const FLUSH_TAGS = new Set([0x51, 0x53, 0x58, 0x48, 0x63, 0x66]); // Q,S,X,H,c,f

// GLOBAL serialisation queue across all connections. PGLite has a single
// session shared by all clients; concurrent batches interleave their
// responses on the wire and corrupt psycopg's state machine (and would
// similarly corrupt any other client's). The matched fix on the client
// side is queue-of-one connection pool + disabled prepared-statement cache
// (see prereq-patterns/pglite/applicability.md).
let globalWorkChain = Promise.resolve();

// ---------------------------------------------------------------------------
// Protocol frame builders
// ---------------------------------------------------------------------------

function buildBackendMessage(tagChar, payload) {
  const tag = tagChar.charCodeAt(0);
  const out = Buffer.alloc(1 + 4 + payload.length);
  out.writeUInt8(tag, 0);
  out.writeUInt32BE(4 + payload.length, 1);
  payload.copy(out, 5);
  return out;
}

function buildAuthOk() {
  const p = Buffer.alloc(4); p.writeUInt32BE(0, 0);
  return buildBackendMessage('R', p);
}
function buildParameterStatus(name, value) {
  return buildBackendMessage('S', Buffer.from(`${name}\0${value}\0`, 'utf8'));
}
function buildBackendKeyData(pid, key) {
  const p = Buffer.alloc(8); p.writeUInt32BE(pid, 0); p.writeUInt32BE(key, 4);
  return buildBackendMessage('K', p);
}
function buildReadyForQuery(txnStatus = 'I') {
  return buildBackendMessage('Z', Buffer.from(txnStatus, 'utf8'));
}

// PGLite 0.4.x emits a DOUBLED trailing ReadyForQuery on the error path
// (response tags `… E Z Z`, verified by probe against
// @electric-sql/pglite@0.4.5). Real Postgres sends exactly ONE
// ReadyForQuery to terminate a query cycle. psycopg3 tolerates the
// duplicate, but Npgsql / psqlODBC historically desync on a doubled Z
// (the failure that drove the no-pg-gateway investigation). Collapse a
// run of consecutive trailing ReadyForQuery frames to a single one so
// every consumer sees correct wire framing. Only adjacent end-of-buffer
// Z frames (the duplicate-terminator signature) are touched; a legitimate
// multi-Sync response has data / CommandComplete BETWEEN its ReadyForQuery
// frames and is left intact. On any malformed framing we return the bytes
// unchanged — never corrupt a response we cannot fully parse.
const TAG_READY_FOR_QUERY = 0x5A; // 'Z'

function coalesceTrailingReadyForQuery(buf) {
  const starts = [];
  let off = 0;
  while (off + 5 <= buf.length) {
    const len = buf.readUInt32BE(off + 1);
    const end = off + 1 + len;
    if (len < 4 || end > buf.length) return buf; // malformed / truncated
    starts.push(off);
    off = end;
  }
  if (off !== buf.length || starts.length < 2) return buf;
  let cut = buf.length;
  let n = starts.length;
  while (
    n >= 2 &&
    buf[starts[n - 1]] === TAG_READY_FOR_QUERY &&
    buf[starts[n - 2]] === TAG_READY_FOR_QUERY
  ) {
    cut = starts[n - 1];
    n -= 1;
  }
  return cut === buf.length ? buf : buf.subarray(0, cut);
}

// ---------------------------------------------------------------------------
// Per-connection handler
// ---------------------------------------------------------------------------

const server = createServer(async (socket) => {
  let buffered = Buffer.alloc(0);
  let didStartup = false;
  const pending = [];
  let pendingLen = 0;

  function endsAtFlushBoundary() {
    if (pendingLen === 0) return false;
    const buf = Buffer.concat(pending, pendingLen);
    let off = 0, lastTag = null;
    while (off < buf.length) {
      if (off + 5 > buf.length) return false;
      const len = buf.readUInt32BE(off + 1);
      if (off + 1 + len > buf.length) return false;
      lastTag = buf[off];
      off += 1 + len;
    }
    return off === buf.length && FLUSH_TAGS.has(lastTag);
  }

  socket.on('data', async (chunk) => {
    buffered = Buffer.concat([buffered, chunk]);

    // Startup handshake (hand-rolled — no pg-gateway).
    if (!didStartup) {
      while (buffered.length >= 8) {
        const len = buffered.readUInt32BE(0);
        if (buffered.length < len) return;
        const code = buffered.readUInt32BE(4);
        if (code === 0x04D2162F) {
          // SSLRequest — say no.
          socket.write(Buffer.from('N', 'utf8'));
          buffered = buffered.subarray(len);
          continue;
        }
        // Treat as StartupMessage. Best-effort ROLLBACK keeps the shared
        // PGLite session clean if a prior client left a transaction in
        // error state.
        try { await pglite.exec('ROLLBACK'); } catch (_e) { /* ok */ }
        const reply = Buffer.concat([
          buildAuthOk(),
          buildParameterStatus('server_version', '16.0'),
          buildParameterStatus('server_encoding', 'UTF8'),
          buildParameterStatus('client_encoding', 'UTF8'),
          buildParameterStatus('DateStyle', 'ISO, MDY'),
          buildParameterStatus('integer_datetimes', 'on'),
          buildParameterStatus('standard_conforming_strings', 'on'),
          buildBackendKeyData(1, 1),
          buildReadyForQuery(),
        ]);
        socket.write(reply);
        buffered = buffered.subarray(len);
        didStartup = true;
        break;
      }
      if (!didStartup) return;
    }

    // Frame-level loop: buffer until flush boundary, then forward through
    // the global serialisation chain. (This is the implicit-Sync fix from
    // the glpnet investigation: never forward a half-batch into PGLite
    // mid-pipeline.)
    while (buffered.length >= 5) {
      const len = buffered.readUInt32BE(1);
      if (buffered.length < 1 + len) return;
      const frame = buffered.subarray(0, 1 + len);
      buffered = buffered.subarray(1 + len);

      pending.push(frame);
      pendingLen += frame.length;
      if (!endsAtFlushBoundary()) continue;

      const batch = Buffer.concat(pending, pendingLen);
      pending.length = 0; pendingLen = 0;

      globalWorkChain = globalWorkChain.then(async () => {
        try {
          const response = await pglite.execProtocolRaw(batch);
          socket.write(coalesceTrailingReadyForQuery(Buffer.from(response)));
        } catch (e) {
          console.error(`[bridge] forward_error ${e.message}`);
          socket.destroy();
        }
      });
    }
  });

  socket.on('error', (e) => console.error(`[bridge] socket_error ${e.message}`));
});

server.on('error', (e) => {
  console.error(`[bridge] BRIDGE_ERROR listen ${e.message}`);
  process.exit(e && e.code === 'EADDRINUSE' ? 5 : 2);
});

// --------- coordination state (problem B / C / D experiment) ---------
const startedAtIso = new Date().toISOString();
let resolvedHost = args.host;
let resolvedPort = 0;
let heartbeatSeq = 0;          // bumped only after a successful internal SELECT 1
let lastHeartbeatIso = startedAtIso;
let orphanedSinceMs = null;    // null = currently has consumers; ms timestamp = orphan-detected-at
let shuttingDown = false;
// Consumers registry placed SIBLING to the data dir (same reasoning as the
// bridge lock — PGLite refuses to init a data dir with non-PG files in it).
const consumersDir = `${args.pgdir}.consumers`;
const shutdownMarkerPath = `${args.pgdir}.shutdown`;

server.listen(args.port, args.host, () => {
  resolvedPort = server.address().port;

  // Side-effect ordering per bridge_lifecycle.md "Bridge startup":
  //   1. listen() resolves (above).
  //   2. Atomic-write sidecar JSON (now with heartbeat_seq, heartbeat_at).
  //   3. Emit BRIDGE_READY token on stdout.
  //   4. With --daemon: redirect console.* to size-rotated bridge.log.
  //   5. Start the SQL self-ping + orphan-poll + shutdown-marker timers.
  try {
    // consumersDir is sibling to args.pgdir, so creating it does not perturb
    // PGLite's data directory invariant.
    if (!existsSync(consumersDir)) mkdirSync(consumersDir, { recursive: true });
    writeSidecar();
  } catch (e) {
    console.error(`[bridge] BRIDGE_ERROR sidecar_write_failed ${(e && e.message) || e}`);
    process.exit(9);
  }

  console.error(`[bridge] start transport=tcp listen=${args.host}:${resolvedPort} data_dir=${args.pgdir}`);
  // Use fs.writeSync(1, ...) to flush BRIDGE_READY to fd 1 (stdout) immediately,
  // bypassing Node's userland write buffer. Without this, Node block-buffers
  // piped stdout until exit and Python clients wait 30+ seconds for the line.
  try {
    fsWriteSync(1, `BRIDGE_READY port=${resolvedPort} pid=${process.pid}\n`);
  } catch (_e) {
    console.log(`BRIDGE_READY port=${resolvedPort} pid=${process.pid}`);
  }

  if (args.daemon) {
    redirectConsoleToRotatingLog();
  }

  startSelfPingLoop();
  startOrphanPollLoop();
  startShutdownMarkerLoop();
});

function writeSidecar() {
  writeAtomicJson(sidecarPath, {
    host: resolvedHost,
    port: resolvedPort,
    pid: process.pid,
    started_at: startedAtIso,
    data_dir: args.pgdir,
    role: 'primary',
    managed_by: args.daemon ? 'auto-spawn' : 'manual',
    heartbeat_seq: heartbeatSeq,
    heartbeat_at: lastHeartbeatIso,
  });
}

// B liveness — bridge self-pings WASM and refreshes heartbeat fields ONLY on success.
// A hung WASM session stops refreshing the heartbeat, which clients can detect.
function startSelfPingLoop() {
  const tick = async () => {
    if (shuttingDown) return;
    try {
      await pglite.exec('SELECT 1');
      heartbeatSeq += 1;
      lastHeartbeatIso = new Date().toISOString();
      try { writeSidecar(); } catch (_e) { /* best effort */ }
    } catch (e) {
      console.error(`[bridge] self_ping_failed ${(e && e.message) || e}`);
      // Do NOT bump heartbeat on failure — that is the signal.
    }
    setTimeout(tick, SELF_PING_INTERVAL_MS).unref();
  };
  setTimeout(tick, SELF_PING_INTERVAL_MS).unref();
}

// Diagnostic: log the consumers-dir path at orphan-poll setup so we can verify
// the path the bridge polls matches the path consumers register at.

// C orphan detection — poll .pgdb/consumers/ for held fd-locks. A registration is
// "held" if we cannot acquire its file with FileShare.None (Windows) / open
// O_EXCL-equivalent (POSIX). Simpler portable test: try to open with 'r+' and
// flock-test; if the consumer holds the lock our open will succeed but the
// consumer's process is alive while it holds the kernel-released lock.
//
// Greenfield-experiment policy: a consumer registration file's *existence* +
// the consumer process being alive (lock held) means the consumer is registered.
// We rely on portalocker's lockfile-with-DELETE_ON_CLOSE semantics: the file
// goes away when the holder process exits, even on crash.
//
// Empirically simplest: count files under consumers/ whose contents include a
// pid we can verify alive. portalocker on the client writes the pid and holds
// an fd-lock; on crash the OS releases the lock; we then conclude the file is
// stale by attempting our own non-blocking lock acquisition.
function startOrphanPollLoop() {
  const tick = async () => {
    if (shuttingDown) return;
    let liveCount = 0;
    try {
      if (existsSync(consumersDir)) {
        for (const name of readdirSync(consumersDir)) {
          if (!name.endsWith('.lock')) continue;
          const lockPath = join(consumersDir, name);
          if (await isHeldByOtherProcess(lockPath)) {
            liveCount += 1;
          } else {
            // Stale — best-effort cleanup.
            try { unlinkSync(lockPath); } catch (_e) { /* tolerate */ }
          }
        }
      }
    } catch (e) {
      console.error(`[bridge] orphan_poll_error ${(e && e.message) || e}`);
    }

    if (liveCount === 0) {
      if (orphanedSinceMs === null) {
        orphanedSinceMs = Date.now();
        console.error(`[bridge] orphan_detected linger_ms=${ORPHAN_LINGER_MS}`);
      } else if (Date.now() - orphanedSinceMs >= ORPHAN_LINGER_MS) {
        console.error('[bridge] orphan_linger_expired graceful_shutdown');
        gracefulExit(0);
        return;
      }
    } else if (orphanedSinceMs !== null) {
      console.error(`[bridge] orphan_recovered live_consumers=${liveCount}`);
      orphanedSinceMs = null;
    }
    setTimeout(tick, ORPHAN_POLL_INTERVAL_MS).unref();
  };
  // First poll happens after one interval to give consumers time to register
  // after their bridge spawn.
  setTimeout(tick, ORPHAN_POLL_INTERVAL_MS).unref();
}

// Probes whether some other process holds an exclusive lock on the lockfile.
// We attempt our own non-blocking exclusive acquire; if it succeeds, no one
// else holds the lock (file is stale or never-acquired). If it fails with
// EBUSY / EAGAIN / EACCES, the lock is held. We always release ours immediately.
async function isHeldByOtherProcess(lockPath) {
  // The consumer file's first line carries the consumer's pid. We test
  // liveness purely by pid lookup (process.kill(pid, 0)), which is reliable
  // cross-platform in Node and does not depend on whether the consumer's
  // locking primitive interferes with our open.
  let pidStr;
  try {
    pidStr = readFileSync(lockPath, 'utf8').trim();
  } catch (_e) {
    return false;
  }
  const pid = parseInt(pidStr, 10);
  if (!Number.isFinite(pid) || pid <= 0) return false;
  return isPidAlive(pid);
}

function isPidAlive(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch (e) {
    if (e.code === 'EPERM') return true; // exists but we can't signal
    return false;
  }
}

// D — non-destructive force shutdown via .pgdb/.shutdown marker file. Operator
// (or a `codeconv bridge stop` style command) writes the marker; bridge polls
// and exits gracefully on next tick.
function startShutdownMarkerLoop() {
  const tick = () => {
    if (shuttingDown) return;
    if (existsSync(shutdownMarkerPath)) {
      console.error('[bridge] shutdown_marker_seen graceful_shutdown');
      try { unlinkSync(shutdownMarkerPath); } catch (_e) { /* ok */ }
      gracefulExit(0);
      return;
    }
    setTimeout(tick, SHUTDOWN_MARKER_POLL_MS).unref();
  };
  setTimeout(tick, SHUTDOWN_MARKER_POLL_MS).unref();
}

if (!args.daemon) {
  process.stdin.on('end', () => gracefulExit(0));
  process.stdin.resume();
}
process.on('SIGTERM', () => gracefulExit(0));
process.on('SIGINT', () => gracefulExit(0));
process.on('beforeExit', () => { try { unlinkSync(sidecarPath); } catch { /* best effort */ } });

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function writeAtomicJson(path, obj) {
  const tmp = `${path}.tmp.${process.pid}`;
  writeFileSync(tmp, JSON.stringify(obj, null, 2) + '\n', { encoding: 'utf8' });
  renameSync(tmp, path);
}

function redirectConsoleToRotatingLog() {
  // Inline 5MB×3 rotation per FR-030 + R9. We override console.log and
  // console.error rather than re-pointing fd 1/2 because Node's stdout/
  // stderr are not trivially re-pointable on Windows.
  const log = createRotatingStream(join(args.pgdir, 'bridge.log'));
  const fmt = (level) => (...parts) => {
    const text = parts.map((p) => (typeof p === 'string' ? p : String(p))).join(' ');
    const ts = new Date().toISOString();
    log.write(`${ts} [${level}] ${text}\n`);
  };
  console.log = fmt('log');
  console.error = fmt('err');
  console.info = fmt('log');
  console.warn = fmt('err');
}

function gracefulExit(code) {
  if (shuttingDown) return;
  shuttingDown = true;
  try { server.close(); } catch { /* ok */ }
  try { unlinkSync(sidecarPath); } catch { /* best effort */ }
  // Lock release is kernel-managed on process exit; we deliberately do NOT
  // call lockRelease() here — proper-lockfile's exit hook handles cleanup,
  // and explicitly releasing race-conditions with new clients trying to
  // acquire as we shut down.
  process.exit(code);
}

function parseArgs(argv) {
  const a = { pgdir: null, port: 0, host: '127.0.0.1', daemon: false, noLock: false };
  for (let i = 2; i < argv.length; i++) {
    const v = argv[i];
    if (v === '--data-dir') a.pgdir = resolve(argv[++i]);
    else if (v === '--port') a.port = parseInt(argv[++i], 10);
    else if (v === '--host') a.host = argv[++i];
    else if (v === '--daemon') a.daemon = true;
    else if (v === '--no-lock') a.noLock = true;
    else if (v === '--transport') argv[++i]; // accepted, currently ignored
  }
  if (!a.pgdir) { console.error('[bridge] BRIDGE_ERROR missing --data-dir'); process.exit(1); }
  if (Number.isNaN(a.port) || a.port < 0) { console.error('[bridge] BRIDGE_ERROR invalid --port'); process.exit(1); }
  return a;
}
