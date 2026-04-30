// Fixed bridge: buffers incoming frames until a Sync (S) or simple Query (Q)
// or Terminate (X) is seen, then forwards the entire batch to PGLite as a
// single execProtocolRaw call. Aligns PGLite's implicit final-Sync with the
// client's explicit Sync so Npgsql / psqlODBC see canonical Postgres-wire
// frames.
import { createServer } from 'node:net';
import { existsSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { PGlite } from '@electric-sql/pglite';
import { fromNodeSocket } from 'pg-gateway/node';

const args = parseArgs(process.argv);
if (!existsSync(args.pgdir)) mkdirSync(args.pgdir, { recursive: true });

let pglite;
try {
  pglite = await PGlite.create(args.pgdir);
} catch (e) {
  console.log(`BRIDGE_ERROR pglite_init_failed ${(e && e.message) || e}`);
  process.exit(1);
}
console.error(`[pglite] ready, seeding test schema`);
await pglite.exec("CREATE TABLE IF NOT EXISTS t (x INT);");
await pglite.exec("DELETE FROM t; INSERT INTO t VALUES (1), (2), (3);");

// Frames whose arrival means "the client has finished its current request and
// is now waiting for a response." We forward to PGLite when we see one of
// these. Q (simple Query), S (Sync), X (Terminate), H (Flush), c/d/f (Copy
// terminators) all qualify.
const FLUSH_TAGS = new Set([
  0x51, // Q  Query (simple)
  0x53, // S  Sync (extended protocol terminator)
  0x58, // X  Terminate
  0x48, // H  Flush
  0x63, // c  CopyDone
  0x66, // f  CopyFail
]);

class FrameBuffer {
  constructor() { this.chunks = []; this.totalLen = 0; }
  push(buf) { this.chunks.push(buf); this.totalLen += buf.length; }
  drain() {
    if (this.chunks.length === 0) return Buffer.alloc(0);
    const out = Buffer.concat(this.chunks, this.totalLen);
    this.chunks.length = 0; this.totalLen = 0;
    return out;
  }
  /**
   * Returns true iff the data already buffered ends on a "flush-worthy"
   * frame boundary (Q, S, X, H, c, f). All these messages are tagged and
   * length-prefixed; we walk frames to find the last complete one.
   */
  endsAtFlushBoundary() {
    if (this.chunks.length === 0) return false;
    let off = 0;
    const buf = Buffer.concat(this.chunks, this.totalLen);
    let lastTag = null;
    while (off < buf.length) {
      if (off + 5 > buf.length) return false;     // partial header
      const tag = buf[off];
      const len = buf.readUInt32BE(off + 1);
      if (off + 1 + len > buf.length) return false;  // partial payload
      lastTag = tag;
      off += 1 + len;
    }
    if (off !== buf.length) return false;          // trailing partial frame
    return lastTag !== null && FLUSH_TAGS.has(lastTag);
  }
}

const server = createServer(async (socket) => {
  console.error(`[server] new client`);
  const pending = new FrameBuffer();
  await fromNodeSocket(socket, {
    serverVersion: () => '16.0',
    auth: { method: 'trust' },
    async onMessage(data, { isAuthenticated }) {
      if (!isAuthenticated) return;  // let pg-gateway handle the handshake
      pending.push(Buffer.from(data));
      const buf = Buffer.from(data);
      console.error(`[onMessage] arrived bytes=${buf.length} firstTag=0x${buf[0].toString(16)} (${String.fromCharCode(buf[0])}) endsAtFlush=${pending.endsAtFlushBoundary()}`);
      if (!pending.endsAtFlushBoundary()) {
        return;
      }
      const batch = pending.drain();
      console.error(`[forward] batch ${batch.length} bytes hex=${batch.toString('hex').slice(0, 200)}`);
      const response = await pglite.execProtocolRaw(batch);
      console.error(`[forward] PGLite returned ${response.length} bytes`);
      // Walk the response and list every frame tag.
      const respBuf = Buffer.from(response);
      const tags = [];
      let off = 0;
      while (off + 5 <= respBuf.length) {
        const tag = respBuf[off];
        const len = respBuf.readUInt32BE(off + 1);
        if (off + 1 + len > respBuf.length) { tags.push(`?incomplete-tag=${tag.toString(16)}`); break; }
        tags.push(`${String.fromCharCode(tag)}(${tag.toString(16)})/${len}`);
        off += 1 + len;
      }
      console.error(`[forward] response frames: ${tags.join(' ')}`);
      console.error(`[forward] response hex: ${respBuf.toString('hex')}`);
      return response;
    },
  });
  console.error(`[server] client disconnected`);
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
