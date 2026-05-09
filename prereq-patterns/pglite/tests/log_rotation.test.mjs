// FR-030 + R9 — bridge.log rotates at ~5 MB across 3 backup files.
// Tests the rotator helper directly. The bridge's --daemon mode wires this
// helper to its console.* output; that wiring is exercised end-to-end by
// the sidecar/lock tests indirectly.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

import { createRotatingStream } from '../log_rotator.mjs';
import { makeTempDir, rmTempDir } from './_helpers.mjs';

test('rotating stream produces .log + .log.1 + .log.2 + .log.3', async (t) => {
  const dir = makeTempDir();
  t.after(() => rmTempDir(dir));

  const path = join(dir, 'bridge.log');
  const maxSize = 64 * 1024;        // 64 KB rollover for fast test
  const maxFiles = 3;
  const stream = createRotatingStream(path, { maxSize, maxFiles });

  const chunk = Buffer.alloc(8 * 1024, 0x58);   // 8 KB of 'X'
  // Write 320 KB total = 5x maxSize; should produce current + .1 + .2 + .3.
  for (let i = 0; i < 40; i++) stream.write(chunk);
  stream.close();

  assert.ok(existsSync(path), 'current bridge.log exists');
  assert.ok(existsSync(`${path}.1`), 'bridge.log.1 exists');
  assert.ok(existsSync(`${path}.2`), 'bridge.log.2 exists');
  assert.ok(existsSync(`${path}.3`), 'bridge.log.3 exists');
  assert.ok(!existsSync(`${path}.4`), `bridge.log.4 must NOT exist (maxFiles=${maxFiles})`);

  // Each rotated file must be ≤ maxSize.
  for (const i of [1, 2, 3]) {
    const sz = statSync(`${path}.${i}`).size;
    assert.ok(sz <= maxSize, `bridge.log.${i} size ${sz} must be ≤ maxSize ${maxSize}`);
  }
});

test('rotating stream caps backup count at maxFiles', async (t) => {
  const dir = makeTempDir();
  t.after(() => rmTempDir(dir));

  const path = join(dir, 'bridge.log');
  const stream = createRotatingStream(path, { maxSize: 1024, maxFiles: 3 });

  const chunk = Buffer.alloc(512, 0x59); // 512 B of 'Y'
  for (let i = 0; i < 50; i++) stream.write(chunk); // 25 KB total => >> 4*1024
  stream.close();

  const files = readdirSync(dir).sort();
  // Expected: bridge.log, bridge.log.1, bridge.log.2, bridge.log.3 only.
  const rotated = files.filter((f) => /^bridge\.log(\.\d+)?$/.test(f));
  assert.deepEqual(
    rotated.sort(),
    ['bridge.log', 'bridge.log.1', 'bridge.log.2', 'bridge.log.3'].sort(),
    `unexpected rotation file set: ${rotated.join(', ')}`,
  );
});
