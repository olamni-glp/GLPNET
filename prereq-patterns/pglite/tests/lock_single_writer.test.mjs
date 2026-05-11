// SC-001 — exactly one of two parallel-spawned bridges wins the lock;
// the loser exits with code 5 within ~1 s.
// FR-002, FR-003, contract bridge_lifecycle.md "Lock semantics".

import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  makeTempDir, rmTempDir,
  spawnBridge, readReady, waitExit, killAndWait,
} from './_helpers.mjs';

test('two parallel bridges: exactly one wins; loser exits 5', async (t) => {
  const dir = makeTempDir();
  t.after(() => rmTempDir(dir));

  // Capture stdout+stderr of both children for diagnostic purposes; helpful
  // when proper-lockfile's mkdir-based race resolves unexpectedly.
  const a = spawnBridge(dir);
  const b = spawnBridge(dir);
  const traces = { a: '', b: '' };
  a.stdout.on('data', (c) => { traces.a += c.toString('utf8'); });
  a.stderr.on('data', (c) => { traces.a += c.toString('utf8'); });
  b.stdout.on('data', (c) => { traces.b += c.toString('utf8'); });
  b.stderr.on('data', (c) => { traces.b += c.toString('utf8'); });

  const aReady = readReady(a, 15000).then((r) => ({ which: 'a', ok: true, r })).catch((e) => ({ which: 'a', ok: false, e }));
  const bReady = readReady(b, 15000).then((r) => ({ which: 'b', ok: true, r })).catch((e) => ({ which: 'b', ok: false, e }));
  const aExit = waitExit(a, 15000).catch(() => null);
  const bExit = waitExit(b, 15000).catch(() => null);

  const [aR, bR, aX, bX] = await Promise.all([aReady, bReady, aExit, bExit]);

  const winners = [aR, bR].filter((x) => x.ok);
  const losers = [aR, bR].filter((x) => !x.ok);

  if (winners.length !== 1) {
    throw new Error(`expected exactly one winner; got ${winners.length}.\nA trace:\n${traces.a}\nB trace:\n${traces.b}`);
  }
  assert.equal(losers.length, 1);

  const loserExit = losers[0].which === 'a' ? aX : bX;
  assert.ok(loserExit, 'losing bridge must have exited');
  assert.equal(loserExit.code, 5, `expected losing bridge to exit code 5; got ${loserExit.code}`);

  // Cleanup the winner.
  const winnerChild = winners[0].which === 'a' ? a : b;
  await killAndWait(winnerChild, 'SIGTERM');
});
