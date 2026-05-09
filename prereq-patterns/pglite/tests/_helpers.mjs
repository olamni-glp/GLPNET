// Shared helpers for bridge tests. Spawn helper, READY parse, exit wait,
// temp dir creation. Kept minimal and dependency-free.

import { spawn } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = fileURLToPath(new URL('.', import.meta.url));
export const BRIDGE_PATH = join(HERE, '..', 'pglite_bridge.mjs');

export function makeTempDir(prefix = 'pglite-bridge-test-') {
  return mkdtempSync(join(tmpdir(), prefix));
}

export function rmTempDir(dir) {
  try { rmSync(dir, { recursive: true, force: true }); } catch { /* tests must not fail on cleanup */ }
}

export function spawnBridge(dataDir, extraArgs = []) {
  // --daemon disables the stdin-end-exit handler. Necessary because we spawn
  // with stdio[0]='ignore', which delivers EOF on stdin immediately and would
  // otherwise terminate the bridge right after emitting BRIDGE_READY (per
  // contracts/bridge_cli.md "--daemon" semantics).
  const args = [BRIDGE_PATH, '--data-dir', dataDir, '--port', '0', '--host', '127.0.0.1', '--daemon', ...extraArgs];
  return spawn(process.execPath, args, { stdio: ['ignore', 'pipe', 'pipe'] });
}

export function readReady(child, timeoutMs = 30000) {
  return new Promise((resolve, reject) => {
    let stdoutBuf = '';
    let stderrBuf = '';
    const t = setTimeout(() => {
      reject(new Error(`READY timeout after ${timeoutMs}ms; stdout=${JSON.stringify(stdoutBuf)} stderr=${JSON.stringify(stderrBuf)}`));
    }, timeoutMs);
    child.stdout.on('data', (chunk) => {
      stdoutBuf += chunk.toString('utf8');
      const m = stdoutBuf.match(/BRIDGE_READY port=(\d+) pid=(\d+)/);
      if (m) {
        clearTimeout(t);
        resolve({ port: Number(m[1]), pid: Number(m[2]) });
      }
    });
    child.stderr.on('data', (chunk) => { stderrBuf += chunk.toString('utf8'); });
    child.on('exit', (code) => {
      clearTimeout(t);
      reject(new Error(`bridge exited code=${code} before READY; stderr=${JSON.stringify(stderrBuf)}`));
    });
  });
}

export function waitExit(child, timeoutMs = 5000) {
  return new Promise((resolve, reject) => {
    if (child.exitCode != null) return resolve({ code: child.exitCode, signal: null });
    const t = setTimeout(() => reject(new Error(`exit timeout after ${timeoutMs}ms`)), timeoutMs);
    child.on('exit', (code, signal) => {
      clearTimeout(t);
      resolve({ code, signal });
    });
  });
}

export function killAndWait(child, signal = 'SIGTERM') {
  if (child.exitCode != null) return Promise.resolve({ code: child.exitCode, signal: null });
  child.kill(signal);
  return waitExit(child, 5000);
}

export async function readyOrExit(child, timeoutMs = 5000) {
  const readyP = readReady(child, timeoutMs)
    .then((r) => ({ kind: 'ready', ...r }))
    .catch((e) => { throw e; });
  const exitP = waitExit(child, timeoutMs)
    .then((r) => ({ kind: 'exit', ...r }));
  return Promise.race([
    readyP.catch(() => exitP),
    exitP.catch(() => readyP),
  ]).then((winner) => winner);
}
