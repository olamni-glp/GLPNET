-- D2NET workspace database schema (PGLite WASM, single-user, accessed via bridge-direct.mjs).
-- Authoritative DDL for D2NET.Init. Apply once on a fresh PGLite database via Npgsql.
-- See specs/005-d2net-pglite-bridge/data-model.md for column-level rules and invariants.
-- See research.md R3 for the SQLite -> PostgreSQL/PGLite type translation table.
-- Note: the .NET caller wraps these CREATE statements in its own NpgsqlTransaction;
-- the script itself contains no BEGIN/COMMIT.

-- 1. setting (FR-012 of 002 + FR-009/FR-010 of 005): flat key/value configuration store.
CREATE TABLE setting (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- 2. excluded_directories (FR-013 of 002): one row per approved exclusion.
--    `path` is forward-slash relative path under <source_dir>.
--    `kind` records why the exclusion exists.
CREATE TABLE excluded_directories (
    path TEXT PRIMARY KEY,
    kind TEXT NOT NULL CHECK (kind IN ('tool', 'pattern', 'manual'))
);

-- 3. dart_files (FR-014 of 002): one row per .dart file in the non-excluded source tree.
--    full_path is relative to repo root, forward slashes on every OS.
--    BIGSERIAL replaces the shipped SQLite "INTEGER PRIMARY KEY AUTOINCREMENT" --
--    semantics are equivalent (monotonic 64-bit auto-id).
CREATE TABLE dart_files (
    id        BIGSERIAL PRIMARY KEY,
    filename  TEXT NOT NULL,
    full_path TEXT NOT NULL UNIQUE
);

-- 4. phase_sequence (FR-015 of 002): created empty by Init.
CREATE TABLE phase_sequence (
    phase    TEXT PRIMARY KEY,
    sequence INTEGER NOT NULL
);

-- 5. phase_status (FR-016 of 002): created empty by Init.
--    last_updated stored as TIMESTAMPTZ; rendered as ISO-8601 UTC text on read
--    via to_char(last_updated AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"')
--    in CurrentPhaseInspector to preserve FR-019's wire format.
CREATE TABLE phase_status (
    phase        TEXT PRIMARY KEY,
    status       TEXT NOT NULL,
    last_updated TIMESTAMPTZ NOT NULL DEFAULT now()
);
