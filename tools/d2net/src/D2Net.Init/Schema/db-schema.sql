-- D2NET workspace database schema (embedded SQLite, single-user)
-- Authoritative DDL for D2NET.Init. Apply once on a fresh SQLite database.
-- See specs/002-d2net-init/data-model.md for column-level rules and invariants.
-- Note: the .NET caller wraps these CREATE statements in its own SqliteTransaction;
-- the script itself contains no BEGIN/COMMIT.

-- 1. setting (FR-012): flat key/value configuration store.
CREATE TABLE setting (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- 2. excluded_directories (FR-013): one row per approved exclusion.
--    `path` is forward-slash relative path under <source_dir>.
--    `kind` records why the exclusion exists.
CREATE TABLE excluded_directories (
    path TEXT PRIMARY KEY,
    kind TEXT NOT NULL CHECK (kind IN ('tool', 'pattern', 'manual'))
);

-- 3. dart_files (FR-014): one row per .dart file in the non-excluded source tree.
--    full_path is relative to repo root, forward slashes on every OS.
CREATE TABLE dart_files (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    filename  TEXT NOT NULL,
    full_path TEXT NOT NULL UNIQUE
);

-- 4. phase_sequence (FR-015): created empty by Init.
CREATE TABLE phase_sequence (
    phase    TEXT PRIMARY KEY,
    sequence INTEGER NOT NULL
);

-- 5. phase_status (FR-016): created empty by Init.
--    last_updated stored as ISO-8601 UTC text per FR-019.
CREATE TABLE phase_status (
    phase        TEXT PRIMARY KEY,
    status       TEXT NOT NULL,
    last_updated TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
);
