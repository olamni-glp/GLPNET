// Tracing pgbridge: prints every Postgres-wire frame in both directions and
// pg-gateway onMessage call boundaries. Used to diagnose the extended-protocol
// "ReadyForQuery while expecting BindCompleteMessage" failure.
import { createServer } from 'node:net';
import { existsSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { PGlite } from '@electric-sql/pglite';
import { fromNodeSocket } from 'pg-gateway/node';

const args = parseArgs(process.argv);
if (!existsSync(args.pgdir)) mkdirSync(args.pgdir, { recursive: true });

const FRONTEND_TAGS = {
  0x51: 'Q  Query',
  0x50: 'P  Parse',
  0x42: 'B  Bind',
  0x44: 'D  Describe',
  0x45: 'E  Execute',
  0x46: 'F  FunctionCall',
  0x48: 'H  Flush',
  0x43: 'C  Close',
  0x53: 'S  Sync',
  0x58: 'X  Terminate',
  0x70: 'p  Password/SASL',
  0x64: 'd  CopyData',
  0x63: 'c  CopyDone',
  0x66: 'f  CopyFail',
};

const BACKEND_TAGS = {
  0x52: 'R  AuthenticationResponse',
  0x4b: 'K  BackendKeyData',
  0x53: 'S  ParameterStatus',
  0x5a: 'Z  ReadyForQuery',
  0x44: 'D  DataRow',
  0x54: 'T  RowDescription',
  0x43: 'C  CommandComplete',
  0x49: 'I  EmptyQueryResponse',
  0x45: 'E  ErrorResponse',
  0x4e: 'N  NoticeResponse',
  0x31: '1  ParseComplete',
  0x32: '2  BindComplete',
  0x33: '3  CloseComplete',
  0x6e: 'n  NoData',
  0x73: 's  PortalSuspended',
  0x74: 't  ParameterDescription',
};

function dumpFrames(buf, dictionary, label) {
  let off = 0;
  const out = [];
  while (off < buf.length) {
    if (off + 5 > buf.length) {
      out.push(`${label} INCOMPLETE-frame ${buf.subarray(off).toString('hex')}`);
      break;
    }
    const tag = buf[off];
    const len = buf.readUInt32BE(off + 1);
    if (off + 1 + len > buf.length) {
      out.push(`${label} INCOMPLETE tag=${tagOf(tag, dictionary)} declared-len=${len} have=${buf.length - off - 1}`);
      break;
    }
    const tagName = tagOf(tag, dictionary);
    const payloadHex = buf.subarray(off + 5, off + 1 + len).toString('hex');
    const payloadLen = len - 4;
    out.push(`${label} ${tagName} len=${len} payloadHex=${payloadHex.slice(0, 200)}${payloadHex.length > 200 ? '…' : ''}`);
    off += 1 + len;
  }
  for (const line of out) console.error(line);
}

function tagOf(tag, dict) {
  return dict[tag] || `?(0x${tag.toString(16).padStart(2, '0')} ${String.fromCharCode(tag)})`;
}

let pglite;
try {
  pglite = await PGlite.create(args.pgdir);
} catch (e) {
  console.log(`BRIDGE_ERROR pglite_init_failed ${(e && e.message) || e}`);
  process.exit(1);
}
console.error(`[pglite] ready, schema applying`);
await pglite.exec("CREATE TABLE IF NOT EXISTS t (x INT);");
await pglite.exec("DELETE FROM t; INSERT INTO t VALUES (1), (2), (3);");
console.error(`[pglite] schema seeded with table t`);

const server = createServer(async (socket) => {
  console.error(`[server] new client from ${socket.remoteAddress}:${socket.remotePort}`);
  let onMessageCount = 0;
  await fromNodeSocket(socket, {
    serverVersion: () => '16.0',
    auth: { method: 'trust' },
    async onMessage(data, { isAuthenticated }) {
      onMessageCount++;
      console.error(`\n[onMessage #${onMessageCount}] isAuthenticated=${isAuthenticated} bytes=${data.length}`);
      dumpFrames(Buffer.from(data), FRONTEND_TAGS, '  CLIENT->BRIDGE');
      if (!isAuthenticated) return;
      const t0 = Date.now();
      const response = await pglite.execProtocolRaw(data);
      const dt = Date.now() - t0;
      console.error(`  PGLite returned ${response.length} bytes (${dt}ms)`);
      dumpFrames(Buffer.from(response), BACKEND_TAGS, '  BRIDGE->CLIENT');
      return response;
    },
  });
  console.error(`[server] client disconnected (${onMessageCount} messages handled)`);
});

server.on('error', (e) => {
  console.log(`BRIDGE_ERROR listen ${e && e.message}`);
  process.exit(2);
});
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
