// SC-002 — after a force-kill (SIGKILL on POSIX, TerminateProcess on Windows),
// a fresh bridge start succeeds without manual lock cleanup.
// FR-002 + bridge_lifecycle.md "Lock semantics: kernel-managed on process exit".

import { test } from 'node:test';
import assert from 'node:assert/strict';

import {
  makeTempDir, rmTempDir,
  spawnBridge, readReady, waitExit, killAndWait,
} from './_helpers.mjs';

test('after SIGKILL on the bridge, a fresh bridge starts without manual cleanup', async (t) => {
  const dir = makeTempDir();
  t.after(() => rmTempDir(dir));

  const a = spawnBridge(dir);
  await readReady(a, 30000); // cold PGLite init can take ~7s on this Windows box

  a.kill('SIGKILL');
  await waitExit(a, 5000);

  // Allow proper-lockfile's stale window to elapse. Default in our config is
  // 1000 ms; we give a slight margin.
  await new Promise((r) => setTimeout(r, 1200));

  const b = spawnBridge(dir);
  // Warm PGLite start (PG cluster already initialised) is ≤2 s; allow margin.
  const bReady = await readReady(b, 10000);
  assert.ok(bReady.port > 0, 'fresh bridge should report a positive port');
  assert.ok(bReady.pid > 0, 'fresh bridge should report a positive pid');

  await killAndWait(b, 'SIGTERM');
});
