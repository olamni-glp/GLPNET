// Regression: aborted-transaction recovery over the EXTENDED query protocol.
//
// Root cause (verified by byte-level probe): @electric-sql/pglite@0.2.17's
// execProtocolRaw mishandles an extended-protocol ROLLBACK issued while the
// session is in the aborted state — it returns a malformed fragment with no
// ReadyForQuery, hanging any extended-protocol client (psycopg3 / Npgsql).
// An extended SAVEPOINT in that state hard-crashes the WASM session. Fixed by
// upgrading to 0.4.5; the bridge additionally coalesces 0.4.5's doubled
// trailing ReadyForQuery on the error path (`… E Z Z` -> `… E Z`) so Npgsql /
// psqlODBC do not desync.
//
// Test A would HANG forever against the 0.2.17 bug (the t.after timeout / the
// node:test default would fail). Test B asserts the coalesce directly.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import pg from 'pg';

import {
  makeTempDir, rmTempDir, spawnBridge, readReady, killAndWait,
} from './_helpers.mjs';

const TIMEOUT_MS = 15000; // a hang (the bug) blows well past this

test('extended-protocol ROLLBACK + SAVEPOINT recover after an aborted txn', async (t) => {
  const dir = makeTempDir();
  t.after(() => rmTempDir(dir));
  const bridge = spawnBridge(dir);
  const ready = await readReady(bridge, 30000);
  t.after(() => killAndWait(bridge, 'SIGTERM'));

  const client = new pg.Client({
    host: '127.0.0.1', port: ready.port,
    user: 'postgres', password: 'postgres', database: 'postgres',
    // node-postgres uses the extended protocol — the path that hung on 0.2.17.
    query_timeout: TIMEOUT_MS, statement_timeout: TIMEOUT_MS,
  });
  await client.connect();
  try {
    await client.query(
      'CREATE TABLE t (x int, CONSTRAINT t_chk CHECK (x > 0))');

    // 1. Error inside a txn -> aborted; then ROLLBACK (the historic hang).
    await client.query('BEGIN');
    await assert.rejects(
      () => client.query('INSERT INTO t VALUES ($1)', [-1]),
      /check constraint/i, 'CHECK violation should reject');
    await client.query('ROLLBACK'); // hung forever on 0.2.17
    const a = await client.query('SELECT 1 AS ok');
    assert.equal(Number(a.rows[0].ok), 1, 'session usable after ROLLBACK');

    // 2. Savepoint: error inside a subtransaction, ROLLBACK TO SAVEPOINT
    //    (hard WASM crash on 0.2.17), then continue and COMMIT.
    await client.query('BEGIN');
    await client.query('SAVEPOINT sp1');
    await assert.rejects(
      () => client.query('INSERT INTO t VALUES ($1)', [-5]),
      /check constraint/i);
    await client.query('ROLLBACK TO SAVEPOINT sp1');
    await client.query('INSERT INTO t VALUES ($1)', [7]);
    await client.query('COMMIT');
    const c = await client.query('SELECT count(*)::int AS n FROM t');
    assert.equal(c.rows[0].n, 1, 'committed row survives; subtxn rolled back');

    // 3. Repeated abort/rollback cycles do not desync the shared session.
    for (let i = 0; i < 3; i++) {
      await client.query('BEGIN');
      await assert.rejects(
        () => client.query('INSERT INTO t VALUES ($1)', [-9]),
        /check constraint/i);
      await client.query('ROLLBACK');
    }
    const s = await client.query('SELECT 42 AS v');
    assert.equal(Number(s.rows[0].v), 42, 'session still healthy');
  } finally {
    await client.end();
  }
});

test('error-path response carries exactly one trailing ReadyForQuery', async (t) => {
  const dir = makeTempDir();
  t.after(() => rmTempDir(dir));
  const bridge = spawnBridge(dir);
  const ready = await readReady(bridge, 30000);
  t.after(() => killAndWait(bridge, 'SIGTERM'));

  const sock = net.connect(ready.port, '127.0.0.1');
  t.after(() => sock.destroy());
  await new Promise((res, rej) => { sock.once('connect', res); sock.once('error', rej); });

  const cs = (s) => Buffer.from(s + '\0', 'utf8');
  const m = (tag, p) => {
    const o = Buffer.alloc(5 + p.length);
    o.writeUInt8(tag.charCodeAt(0), 0);
    o.writeUInt32BE(4 + p.length, 1);
    p.copy(o, 5);
    return o;
  };
  // StartupMessage: protocol 196608, user=postgres, database=postgres.
  const params = Buffer.from('user\0postgres\0database\0postgres\0\0', 'utf8');
  const startup = Buffer.alloc(8 + params.length);
  startup.writeUInt32BE(startup.length, 0);
  startup.writeUInt32BE(196608, 4);
  params.copy(startup, 8);

  const collect = (predicate, timeoutMs) => new Promise((resolve, reject) => {
    let buf = Buffer.alloc(0);
    const onData = (d) => {
      buf = Buffer.concat([buf, d]);
      if (predicate(buf)) { cleanup(); resolve(buf); }
    };
    const tmr = setTimeout(() => { cleanup(); reject(new Error('collect timeout')); }, timeoutMs);
    const cleanup = () => { clearTimeout(tmr); sock.off('data', onData); };
    sock.on('data', onData);
  });

  // Walk frames; return the list of message tags.
  const tagsOf = (buf) => {
    const tags = []; let off = 0;
    while (off + 5 <= buf.length) {
      const len = buf.readUInt32BE(off + 1);
      if (off + 1 + len > buf.length) break;
      tags.push(String.fromCharCode(buf[off]));
      off += 1 + len;
    }
    return tags;
  };

  sock.write(startup);
  await collect((b) => tagsOf(b).includes('Z'), 30000); // AuthOk..ReadyForQuery

  // Extended failing batch: Parse/Bind/Describe/Execute/Sync for an INSERT
  // that violates a CHECK. First create the table via a simple query.
  sock.write(m('Q', cs('CREATE TABLE z (x int, CONSTRAINT z_chk CHECK (x > 0))')));
  await collect((b) => tagsOf(b).includes('Z'), TIMEOUT_MS);

  const P = m('P', Buffer.concat([cs(''), cs('INSERT INTO z VALUES ($1)'), Buffer.from([0, 0])]));
  const vb = Buffer.from('-1', 'utf8'); const vl = Buffer.alloc(4); vl.writeInt32BE(vb.length, 0);
  const B = m('B', Buffer.concat([cs(''), cs(''), Buffer.from([0, 0]), Buffer.from([0, 1]), vl, vb, Buffer.from([0, 0])]));
  const D = m('D', Buffer.from('P\0', 'utf8'));
  const E = m('E', Buffer.concat([cs(''), Buffer.from([0, 0, 0, 0])]));
  const S = m('S', Buffer.alloc(0));
  sock.write(Buffer.concat([P, B, D, E, S]));

  const resp = await collect((b) => {
    const tg = tagsOf(b);
    return tg.includes('E') && tg[tg.length - 1] === 'Z';
  }, TIMEOUT_MS);

  const tags = tagsOf(resp);
  assert.ok(tags.includes('E'), `expected ErrorResponse, got ${tags.join('')}`);
  // The P2b coalesce: exactly ONE trailing ReadyForQuery, never `… Z Z`.
  assert.equal(tags[tags.length - 1], 'Z', 'response ends with ReadyForQuery');
  assert.notEqual(tags[tags.length - 2], 'Z',
    `error path must not emit a doubled ReadyForQuery; tags=${tags.join('')}`);
});
