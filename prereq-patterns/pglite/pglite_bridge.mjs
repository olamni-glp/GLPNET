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
  readFileSync,
  renameSync,
  unlinkSync,
  writeFileSync,
} from 'node:fs';
import { join, resolve } from 'node:path';
import { PGlite } from '@electric-sql/pglite';
import lockfile from 'proper-lockfile';

import { createRotatingStream } from './log_rotator.mjs';

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
          socket.write(Buffer.from(response));
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

server.listen(args.port, args.host, () => {
  const resolvedPort = server.address().port;

  // Side-effect ordering per bridge_lifecycle.md "Bridge startup":
  //   1. listen() resolves (above).
  //   2. Atomic-write sidecar JSON.
  //   3. Emit BRIDGE_READY token on stdout.
  //   4. With --daemon: redirect console.* to size-rotated bridge.log.
  try {
    writeAtomicJson(sidecarPath, {
      host: args.host,
      port: resolvedPort,
      pid: process.pid,
      started_at: new Date().toISOString(),
      data_dir: args.pgdir,
      role: 'primary',
      managed_by: args.daemon ? 'auto-spawn' : 'manual',
    });
  } catch (e) {
    console.error(`[bridge] BRIDGE_ERROR sidecar_write_failed ${(e && e.message) || e}`);
    process.exit(9);
  }

  console.error(`[bridge] start transport=tcp listen=${args.host}:${resolvedPort} data_dir=${args.pgdir}`);
  console.log(`BRIDGE_READY port=${resolvedPort} pid=${process.pid}`);

  if (args.daemon) {
    redirectConsoleToRotatingLog();
  }
});

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
