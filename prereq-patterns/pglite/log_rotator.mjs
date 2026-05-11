// Size-rotated append-only log writer for the bridge's --daemon mode.
//
// FR-030 + research R9: rotate at ~5 MB across 3 backup files
// (bridge.log -> bridge.log.1 -> bridge.log.2 -> bridge.log.3, oldest discarded).
// Inline implementation; no external log library.

import {
  closeSync,
  existsSync,
  openSync,
  renameSync,
  statSync,
  unlinkSync,
  writeSync,
} from 'node:fs';

const DEFAULT_MAX_SIZE = 5 * 1024 * 1024;
const DEFAULT_MAX_FILES = 3;

export function createRotatingStream(path, opts = {}) {
  const maxSize = opts.maxSize ?? DEFAULT_MAX_SIZE;
  const maxFiles = opts.maxFiles ?? DEFAULT_MAX_FILES;

  let fd = openSync(path, 'a');
  let written = existsSync(path) ? statSync(path).size : 0;

  function rotate() {
    if (fd != null) { closeSync(fd); fd = null; }

    const oldest = `${path}.${maxFiles}`;
    if (existsSync(oldest)) unlinkSync(oldest);

    for (let i = maxFiles - 1; i >= 1; i--) {
      const src = `${path}.${i}`;
      const dst = `${path}.${i + 1}`;
      if (existsSync(src)) renameSync(src, dst);
    }

    if (existsSync(path)) renameSync(path, `${path}.1`);

    fd = openSync(path, 'a');
    written = 0;
  }

  function write(chunk) {
    const buf = typeof chunk === 'string' ? Buffer.from(chunk, 'utf8') : chunk;
    if (written > 0 && written + buf.length > maxSize) {
      rotate();
    }
    writeSync(fd, buf);
    written += buf.length;
  }

  function close() {
    if (fd != null) { closeSync(fd); fd = null; }
  }

  return {
    write,
    close,
    rotate,
    get currentSize() { return written; },
    get maxSize() { return maxSize; },
    get maxFiles() { return maxFiles; },
  };
}
