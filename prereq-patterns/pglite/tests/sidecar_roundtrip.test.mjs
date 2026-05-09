// Sidecar JSON discovery + Postgres-wire roundtrip (SELECT 1).
// FR-006, contract bridge_lifecycle.md "Sidecar trust rule",
// data-model.md § 2 ".pgdb/bridge.json".

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import pg from 'pg';

import {
  makeTempDir, rmTempDir,
  spawnBridge, readReady, killAndWait,
} from './_helpers.mjs';

test('sidecar JSON has correct shape and serves SELECT 1 over TCP', async (t) => {
  const dir = makeTempDir();
  t.after(() => rmTempDir(dir));

  const bridge = spawnBridge(dir);
  const ready = await readReady(bridge, 30000); // cold PGLite init up to ~7s

  const sidecar = JSON.parse(readFileSync(join(dir, 'bridge.json'), 'utf8'));
  assert.equal(sidecar.host, '127.0.0.1');
  assert.equal(sidecar.port, ready.port, 'sidecar port matches READY port');
  assert.equal(sidecar.pid, ready.pid, 'sidecar pid matches READY pid');
  assert.ok(typeof sidecar.started_at === 'string' && sidecar.started_at.length > 0);
  assert.ok(typeof sidecar.data_dir === 'string' && sidecar.data_dir.length > 0);
  assert.equal(sidecar.role, 'primary');
  // Spawned with --daemon by spawnBridge() helper, so managed_by is 'auto-spawn'.
  assert.equal(sidecar.managed_by, 'auto-spawn');

  const client = new pg.Client({
    host: sidecar.host,
    port: sidecar.port,
    user: 'postgres',
    password: 'postgres',
    database: 'postgres',
  });
  await client.connect();
  try {
    const res = await client.query('SELECT 1 AS one');
    assert.equal(res.rows.length, 1);
    assert.equal(Number(res.rows[0].one), 1);
  } finally {
    await client.end();
  }

  await killAndWait(bridge, 'SIGTERM');
});
