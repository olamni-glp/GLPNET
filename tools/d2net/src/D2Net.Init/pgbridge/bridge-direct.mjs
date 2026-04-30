// Production vendored copy of the PGLite Postgres-wire bridge for D2NET.Init.
// Source:    docs/research/pgbridge-reference/bridge-direct.mjs
// RCA:       docs/research/pglite-pg-gateway-odbc-failure-analysis.md
// Spec:      specs/005-d2net-pglite-bridge/contracts/pgbridge-contract.md
//
// Sanctioned divergence from the reference (analysis finding C1):
//   - The smoke-seed block (CREATE TABLE IF NOT EXISTS t; DELETE FROM t; INSERT ...)
//     is REMOVED. The reference seeds a tiny `t (x INT)` debug table on every spawn,
//     but that mutates the data tree on every bridge startup, including inspection
//     invocations - violating shipped 002 SC-009 ("inspection modifies zero bytes")
//     which 005 FR-013 preserves. A single `console.error('[pglite] ready')` line
//     replaces the seed block so startup logging stays informative.
// Any other modification to this file MUST be justified against the RCA document.

import { createServer } from 'node:net';
import { existsSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { PGlite } from '@electric-sql/pglite';

const args = parseArgs(process.argv);
if (!existsSync(args.pgdir)) mkdirSync(args.pgdir, { recursive: true });

let pglite;
try {
  pglite = await PGlite.create(args.pgdir);
} catch (e) {
  console.log(`BRIDGE_ERROR pglite_init_failed ${(e && e.message) || e}`);
  process.exit(1);
}
console.error(`[pglite] ready`);

const FLUSH_TAGS = new Set([0x51, 0x53, 0x58, 0x48, 0x63, 0x66]);

function buildBackendMessage(tagChar, payload) {
  const tag = tagChar.charCodeAt(0);
  const out = Buffer.alloc(1 + 4 + payload.length);
  out.writeUInt8(tag, 0);
  out.writeUInt32BE(4 + payload.length, 1);
  payload.copy(out, 5);
  return out;
}

function buildAuthOk() {
  const p = Buffer.alloc(4); p.writeUInt32BE(0, 0); // AuthenticationOk = 0
  return buildBackendMessage('R', p);
}
function buildParameterStatus(name, value) {
  const p = Buffer.from(`${name}\0${value}\0`, 'utf8');
  return buildBackendMessage('S', p);
}
function buildBackendKeyData(pid, key) {
  const p = Buffer.alloc(8); p.writeUInt32BE(pid, 0); p.writeUInt32BE(key, 4);
  return buildBackendMessage('K', p);
}
function buildReadyForQuery() {
  const p = Buffer.from('I', 'utf8'); // 'I' = idle
  return buildBackendMessage('Z', p);
}

const server = createServer(async (socket) => {
  console.error(`[server] new client`);
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

    // Phase 1: handle StartupMessage and possible SSLRequest.
    if (!didStartup) {
      // StartupMessage: 4-byte length + 4-byte protocol version + ...
      // SSLRequest:    4-byte length=8 + 4-byte magic 0x04D2162F
      while (buffered.length >= 8) {
        const len = buffered.readUInt32BE(0);
        if (buffered.length < len) return;            // wait for more
        const code = buffered.readUInt32BE(4);
        if (code === 0x04D2162F) {
          // SSLRequest - say no.
          socket.write(Buffer.from('N', 'utf8'));
          buffered = buffered.subarray(len);
          continue;
        }
        // Treat as StartupMessage. Send AuthOk + ParameterStatus + BackendKeyData + ReadyForQuery.
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

    // Phase 2: forward client frames to PGLite, batched until a flush-worthy frame.
    while (buffered.length >= 5) {
      const len = buffered.readUInt32BE(1);
      if (buffered.length < 1 + len) return;        // wait for more
      const frame = buffered.subarray(0, 1 + len);
      buffered = buffered.subarray(1 + len);
      pending.push(frame);
      pendingLen += frame.length;
      if (endsAtFlushBoundary()) {
        const batch = Buffer.concat(pending, pendingLen);
        pending.length = 0; pendingLen = 0;
        try {
          const response = await pglite.execProtocolRaw(batch);
          socket.write(Buffer.from(response));
        } catch (e) {
          console.error(`[forward error] ${e.message}`);
          socket.destroy();
          return;
        }
      }
    }
  });

  socket.on('close', () => console.error(`[server] client disconnected`));
  socket.on('error', (e) => console.error(`[server] socket error ${e.message}`));
});

server.on('error', (e) => { console.log(`BRIDGE_ERROR listen ${e.message}`); process.exit(2); });
server.listen(args.port, args.bind, () => {
  console.log(`BRIDGE_READY port=${args.port} pid=${process.pid}`);
});

process.stdin.on('end', () => process.exit(0));
process.stdin.resume();
process.on('SIGTERM', () => process.exit(0));
process.on('SIGINT', () => process.exit(0));

function parseArgs(argv) {
  const a = { pgdir: null, port: null, bind: '127.0.0.1' };
  for (let i = 2; i < argv.length; i++) {
    if (argv[i] === '--pgdir') a.pgdir = resolve(argv[++i]);
    else if (argv[i] === '--port') a.port = parseInt(argv[++i], 10);
    else if (argv[i] === '--bind') a.bind = argv[++i];
  }
  if (!a.pgdir) { console.log('BRIDGE_ERROR missing --pgdir'); process.exit(1); }
  if (!a.port) { console.log('BRIDGE_ERROR missing --port'); process.exit(1); }
  return a;
}
