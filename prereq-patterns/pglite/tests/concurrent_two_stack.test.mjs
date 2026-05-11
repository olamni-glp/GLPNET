// SC-003 (smoke, Node-only) — 100 sequential SELECT 1 round-trips against a
// single bridge to verify the global serialisation chain doesn't drop or
// reorder responses. The full SC-003 requires Python + .NET clients firing
// in parallel; that integration test lives in Phase 7 (T084).
//
// "psql-based" was the original task spec; we use the in-process `pg`
// driver because (a) it is already a devDependency, (b) it speaks the same
// wire protocol as psql, and (c) avoiding a psql subprocess per cycle keeps
// the smoke harness ~5x faster.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import pg from 'pg';

import {
  makeTempDir, rmTempDir,
  spawnBridge, readReady, killAndWait,
} from './_helpers.mjs';

test('100 sequential SELECT 1 round-trips return 1 each time', async (t) => {
  const dir = makeTempDir();
  t.after(() => rmTempDir(dir));

  const bridge = spawnBridge(dir);
  await readReady(bridge, 30000);
  const sidecar = JSON.parse(readFileSync(join(dir, 'bridge.json'), 'utf8'));

  const client = new pg.Client({
    host: sidecar.host,
    port: sidecar.port,
    user: 'postgres',
    password: 'postgres',
    database: 'postgres',
  });
  await client.connect();
  try {
    for (let i = 0; i < 100; i++) {
      const res = await client.query(`SELECT ${i + 1} AS n`);
      assert.equal(res.rows.length, 1, `iteration ${i}`);
      assert.equal(Number(res.rows[0].n), i + 1, `iteration ${i}`);
    }
  } finally {
    await client.end();
  }

  await killAndWait(bridge, 'SIGTERM');
});
