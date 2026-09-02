# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT

"""BK-TAKT-1 — the cross-repo / cross-host ERA TAKT BOARD, over the DuckLake.

Engineer ruling ``Q-glpnetshiras-16`` (2026-09-01), option A: **extend the existing
lake layout and ship a versioned view contract — do NOT author a new schema.**

The premise that no cross-repo/cross-host schema existed was refuted by measurement:
every ``kind`` in the lake already carries a ``repo`` column, and ``host``/``date``/
``kind`` are already Hive partitions. 5167 rows across four hosts were already there.
A new schema would have stranded every one of them.

    lake layout (EXISTING, not invented here)
        <fleet-root>/takt/kind=<k>/host=<h>/date=<YYYY-MM-DD>/*.parquet
        k in {era, era_step, stage, tokens, token}

THE CRDT CONTRACT (this file is the executable half)
----------------------------------------------------
The lake is an append-only, multi-writer, partition-per-writer store — a G-Set of
immutable records that converges without coordination because no writer ever touches
another writer's partition. Reading it correctly requires three rules, and getting any
of them wrong silently double-counts:

1. **Identity is ``record_id``.** Not the file, not the row offset. The same logical
   measurement can legitimately appear in more than one file (a local write plus a
   fleet sync of that same write — both roots hold it, which is exactly the S21
   symptom).
2. **Conflict resolution is last-writer-wins on ``recorded_at``**, tie-broken by the
   ``kind`` rank in :data:`KIND_RANK` so the resolution is *total* and therefore
   deterministic on every host. A non-total order would let two hosts render two
   different boards from identical bytes.
3. **``token`` and ``tokens`` are ONE logical kind.** ``kind=token`` (17 rows) is a
   legacy sibling of ``kind=tokens`` (658 rows) with a near-identical schema. Reading
   only one of them under-reports; reading both without rule 1 double-counts. This is
   the most likely mechanism behind backlog item S21, where rows written by
   ``emit_tokens`` are on disk at BOTH roots yet ``phase_token_rollup`` surfaces
   neither.

``repo`` is a COLUMN, not a partition key, so cross-repo comparison currently
full-scans. That is a known cost recorded here rather than hidden; adding ``repo`` to
the partition key is a forward-only change (old readers keep working) and is part of
the same ruling.

Read-only. This module NEVER writes to the lake.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

CONTRACT_VERSION = "BK-TAKT-1"

#: Total tie-break order for rule 2. `tokens` outranks the legacy `token`.
KIND_RANK = {"era": 0, "era_step": 0, "stage": 0, "tokens": 0, "token": 1}

#: The engineer-declared takt band for one feature/era, in hours (BK-STD-1).
BAND_LOW_H, BAND_HIGH_H = 1.5, 6.0

DEFAULT_FLEET_ROOT = "/mnt/gavri/d/coop/_takt-lake"


def _resolve_root(explicit: str | None, project_root: Path) -> str:
    """Same precedence the shipped CLI uses: env -> config.local.json -> default."""
    if explicit:
        return explicit
    env = os.environ.get("BUILDKIT_TAKT_LAKE_FLEET")
    if env and env.strip():
        return env.strip()
    cfg = project_root / "config.local.json"
    try:
        if cfg.is_file():
            raw = json.loads(cfg.read_text(encoding="utf-8")).get("takt_lake_fleet_root")
            if isinstance(raw, str) and raw.strip():
                return raw.strip()
    except Exception:
        pass
    return DEFAULT_FLEET_ROOT


#: Only the columns each board query needs. Projecting at the scan is what keeps this
#: memory-bounded: the lake is thousands of small parquet files on a network mount, and
#: a `SELECT *` with `union_by_name` over all of them was measured to be OOM-killed
#: (exit 137) on this host. Narrow projection + an on-disk cache is the fix.
COLS = {
    "era": ("feature_id", "size", "total_seconds", "measurable",
            "unmeasurable_steps", "reason", "repo", "record_id", "host",
            "recorded_at", "date"),
    "era_step": ("feature_id", "step_name", "phase", "seconds", "repo",
                 "record_id", "host", "recorded_at", "date"),
    "stage": ("tool", "verb", "feature_id", "phase", "from_state", "to_state",
              "outcome", "seconds", "actor", "repo", "record_id", "host",
              "recorded_at", "date"),
    "tokens": ("phase", "feature_id", "input_tokens", "output_tokens",
               "total_tokens", "capture_method", "model", "actor", "repo",
               "record_id", "host", "recorded_at", "date"),
}


#: MEASURED LAKE DEFECT (2026-09-01, this host). The same logical column has
#: DIFFERENT PARQUET TYPES across hosts — e.g. `repo` is JSON in
#: ariellas/date=2026-08-24/*tokens*.parquet and VARCHAR elsewhere:
#:
#:     Conversion Error: failed to cast column "repo" from type VARCHAR to JSON
#:
#: `union_by_name=true` unifies by NAME and does not reconcile TYPE, so one
#: divergent file aborts a whole-kind scan. Every column the board reads as text
#: is therefore cast at the scan, and scans are done per (kind, host) with
#: per-file fallback so ONE bad file can never silently take out a host.
TEXTISH = ("repo", "reason", "size", "phase", "step_name", "tool", "verb",
           "from_state", "to_state", "outcome", "actor", "capture_method",
           "model", "feature_id", "record_id", "host", "recorded_at", "date")


def _sel(cols) -> str:
    """Project the needed columns, casting divergent-typed ones to VARCHAR."""
    out = []
    for c in cols:
        out.append(f"CAST({c} AS VARCHAR) AS {c}" if c in TEXTISH else c)
    return ", ".join(out)


def _scan_into(con, table: str, kind: str, root: str, cols, extra_sql: str,
               skipped: list) -> int:
    """Load one kind into `table`, isolating unreadable files instead of aborting.

    Tries the whole kind, then per-host, then per-file. Anything still unreadable
    is APPENDED TO `skipped` and reported on the board — never dropped in silence.
    """
    base = Path(root) / "takt" / f"kind={kind}"
    if not base.is_dir():
        return 0
    sel = _sel(cols)

    def attempt(glob: str) -> bool:
        try:
            con.execute(
                f"INSERT INTO {table} SELECT {sel}{extra_sql} FROM read_parquet("
                f"'{glob}', hive_partitioning=true, union_by_name=true)")
            return True
        except Exception:
            return False

    if attempt(f"{base}/**/*.parquet"):
        return 1
    for hostdir in sorted(p for p in base.iterdir() if p.is_dir()):
        if attempt(f"{hostdir}/**/*.parquet"):
            continue
        for f in sorted(hostdir.rglob("*.parquet")):
            if not attempt(str(f)):
                skipped.append(f"{kind}: {f}")
    return 1


def _build_cache(con, root: str) -> list:
    """Materialise the CRDT-resolved views ONCE into on-disk tables.

    Rules 1-3 of the contract are applied here, so every downstream query reads
    already-converged rows and no reader can get them wrong.
    """
    rank = " ".join(f"WHEN '{k}' THEN {v}" for k, v in KIND_RANK.items())
    skipped: list = []

    # rule 3: token + tokens are ONE logical kind, unioned BEFORE dedup.
    tok = COLS["tokens"]
    coldefs = ", ".join(
        f"{c} VARCHAR" if c in TEXTISH else f"{c} BIGINT" for c in tok)
    con.execute(f"CREATE OR REPLACE TABLE _tokens_raw ({coldefs}, kind VARCHAR)")
    _scan_into(con, "_tokens_raw", "tokens", root, tok, ", 'tokens'", skipped)
    _scan_into(con, "_tokens_raw", "token", root, tok, ", 'token'", skipped)

    # rules 1+2: identity = record_id; LWW on recorded_at; TOTAL tie-break on kind
    # rank so every host renders a byte-identical board from identical bytes.
    con.execute(f"""
        CREATE OR REPLACE TABLE v_tokens AS
            SELECT * EXCLUDE (_rn) FROM (
                SELECT *, row_number() OVER (
                    PARTITION BY record_id
                    ORDER BY recorded_at DESC,
                             CASE kind {rank} ELSE 99 END ASC) AS _rn
                FROM _tokens_raw) WHERE _rn = 1
    """)
    con.execute("DROP TABLE IF EXISTS _tokens_raw")

    numeric = {"total_seconds": "DOUBLE", "seconds": "DOUBLE",
               "unmeasurable_steps": "BIGINT", "measurable": "BOOLEAN"}
    for kind in ("era", "era_step", "stage"):
        cols = COLS[kind]
        coldefs = ", ".join(
            f"{c} VARCHAR" if c in TEXTISH else f"{c} {numeric.get(c, 'DOUBLE')}"
            for c in cols)
        con.execute(f"CREATE OR REPLACE TABLE _raw ({coldefs})")
        _scan_into(con, "_raw", kind, root, cols, "", skipped)
        con.execute(f"""
            CREATE OR REPLACE TABLE v_{kind} AS
                SELECT * EXCLUDE (_rn) FROM (
                    SELECT *, row_number() OVER (
                        PARTITION BY record_id ORDER BY recorded_at DESC) AS _rn
                    FROM _raw) WHERE _rn = 1
        """)
        con.execute("DROP TABLE IF EXISTS _raw")

    con.execute("CREATE OR REPLACE TABLE _skipped (path VARCHAR)")
    for s in skipped:
        con.execute("INSERT INTO _skipped VALUES (?)", [s])
    return skipped


#: MEASURED LAKE DEFECT #2 (2026-09-01). `repo` is written as an ABSOLUTE,
#: HOST-SPECIFIC PATH, so the SAME repo has a different identity on every host and
#: cross-host comparison of one repo is impossible. GLPNET alone appears as:
#:
#:     shiras     /mnt/biwin/D_DRIVE/BSTDEV/research/crucible/glp/GLPNET
#:     gavriella  D:\BSTDEV\research\GLP\GLPNET
#:     ariellas   D:\BSTDEV\research\glp\GLPNET
#:
#: and a few writers emit a bare name ("yngenios", "olamnit-assistant") instead —
#: so the column is not even consistently one shape. That is why the lake reports
#: 39 distinct "repos" for a fleet of about a dozen.
#:
#: The view contract normalises on READ: last path segment, lowercased. This is a
#: read-side repair; the durable fix is for writers to emit a repo NAME, which is
#: part of the same ruling (Q-glpnetshiras-16) and is forward-only.
REPO_NORM = ("lower(regexp_extract(replace(repo, '\\', '/'), '([^/]+)$', 1))")


def _table(rows, headers) -> str:
    if not rows:
        return "  (no rows)"
    cols = [str(h) for h in headers]
    w = [len(c) for c in cols]
    body = []
    for r in rows:
        cells = ["" if v is None else str(v) for v in r]
        body.append(cells)
        for i, c in enumerate(cells):
            if i < len(w):
                w[i] = max(w[i], len(c))
    out = ["| " + " | ".join(c.ljust(w[i]) for i, c in enumerate(cols)) + " |",
           "|" + "|".join("-" * (w[i] + 2) for i in range(len(cols))) + "|"]
    for cells in body:
        out.append("| " + " | ".join(c.ljust(w[i]) for i, c in enumerate(cells)) + " |")
    return "\n".join(out)


def _q(con, sql, title, note=None):
    try:
        cur = con.execute(sql)
        rows = cur.fetchall()
        heads = [d[0] for d in cur.description]
    except Exception as e:
        return f"### {title}\n\n  QUERY FAILED: {str(e)[:300]}\n"
    s = f"### {title}\n\n"
    if note:
        s += note + "\n\n"
    return s + _table(rows, heads) + "\n"


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(prog="bk_takt_board", description=__doc__)
    ap.add_argument("--root", default=None, help="fleet lake root")
    ap.add_argument("--repo", default="glpnet", help="the repo this board is FOR")
    ap.add_argument("--host", default="shiras", help="the host this board is FOR")
    ap.add_argument("--project-root", default=".")
    ap.add_argument("--cache", default="~/.cache/bk-takt-board/lake.duckdb",
                    help="on-disk cache of the CRDT-resolved views")
    ap.add_argument("--refresh", action="store_true", help="rebuild the cache")
    ap.add_argument("--memory", default="1GB")
    ap.add_argument("--threads", type=int, default=2)
    args = ap.parse_args(argv)

    try:
        import duckdb
    except Exception:
        print("duckdb is not importable; install the [co] extra", file=sys.stderr)
        return 2

    root = _resolve_root(args.root, Path(args.project_root).resolve())
    if not Path(root).is_dir():
        print(f"fleet lake root does not exist: {root}", file=sys.stderr)
        return 2

    cache = Path(args.cache).expanduser()
    cache.parent.mkdir(parents=True, exist_ok=True)
    fresh = args.refresh or not cache.exists()
    con = duckdb.connect(str(cache))
    con.execute("SET enable_progress_bar=false")
    con.execute(f"SET memory_limit='{args.memory}'")
    con.execute(f"SET threads={args.threads}")
    con.execute(f"SET temp_directory='{cache.parent / 'duck-tmp'}'")
    if fresh:
        _build_cache(con, root)
    try:
        skipped = [r[0] for r in con.execute("SELECT path FROM _skipped").fetchall()]
    except Exception:
        skipped = []

    R, H = args.repo, args.host
    RN = REPO_NORM
    parts = [
        f"# ERA TAKT BOARD — `{R}` @ `{H}`",
        "",
        f"    contract:   {CONTRACT_VERSION} (engineer ruling Q-glpnetshiras-16, option A)",
        f"    lake root:  {root}",
        f"    band:       {BAND_LOW_H}-{BAND_HIGH_H}h ELAPSED per feature/era (BK-STD-1)",
        "    identity:   record_id · LWW on recorded_at · token+tokens unioned as ONE kind",
        "    read-only:  this board never writes to the lake",
        "",
        "> ELAPSED is the ONLY column a CPM/PERT duration may be built from.",
        "> `effort` is the sum of step durations and is NOT a phase duration.",
        "",
    ]
    if skipped:
        parts += [
            f"> 🔴 **{len(skipped)} parquet file(s) UNREADABLE and EXCLUDED** — every figure",
            "> below is over the remainder. Listed in full at the end; never dropped silently.",
            "",
        ]
    else:
        parts += ["> ✅ Every parquet file in the lake was read. No file was excluded.", ""]

    parts.append(_q(con, """
        SELECT kind, count(*) AS rows, count(DISTINCT host) AS hosts,
               count(DISTINCT repo) AS repos, min(date) AS first, max(date) AS last
        FROM (SELECT 'era' AS kind, host, repo, date FROM v_era
              UNION ALL SELECT 'era_step', host, repo, date FROM v_era_step
              UNION ALL SELECT 'stage', host, repo, date FROM v_stage
              UNION ALL SELECT 'tokens', host, repo, date FROM v_tokens)
        GROUP BY kind ORDER BY kind
    """, "0 · Lake census (after CRDT dedup)"))

    parts.append(_q(con, f"""
        SELECT feature_id, size,
               round(total_seconds/3600.0, 2) AS elapsed_h,
               measurable, unmeasurable_steps AS unmeas, reason, date
        FROM v_era WHERE {RN} = '{R}' AND host = '{H}'
        ORDER BY date DESC, feature_id LIMIT 25
    """, f"1 · This repo, this host — eras on `{R}` @ `{H}`"))

    parts.append(_q(con, f"""
        SELECT host,
               count(*) AS eras,
               sum(CASE WHEN measurable THEN 1 ELSE 0 END) AS measurable,
               sum(CASE WHEN measurable THEN 0 ELSE 1 END) AS unmeasurable,
               round(100.0*sum(CASE WHEN measurable THEN 1 ELSE 0 END)/count(*), 1) AS pct_meas,
               round(median(CASE WHEN measurable THEN total_seconds/3600.0 END), 2) AS p50_h,
               round(max(CASE WHEN measurable THEN total_seconds/3600.0 END), 2) AS max_h
        FROM v_era WHERE {RN} = '{R}'
        GROUP BY host ORDER BY eras DESC
    """, f"2 · SAME REPO `{R}`, ACROSS HOSTS",
        "The comparison asked for: how this repo's era takt differs by host."))

    parts.append(_q(con, f"""
        SELECT {RN} AS repo,
               count(*) AS eras,
               sum(CASE WHEN measurable THEN 1 ELSE 0 END) AS measurable,
               sum(CASE WHEN measurable THEN 0 ELSE 1 END) AS unmeasurable,
               round(100.0*sum(CASE WHEN measurable THEN 1 ELSE 0 END)/count(*), 1) AS pct_meas,
               round(median(CASE WHEN measurable THEN total_seconds/3600.0 END), 2) AS p50_h
        FROM v_era WHERE host = '{H}'
        GROUP BY {RN} ORDER BY eras DESC
    """, f"3 · ALL REPOS ON THIS HOST `{H}`"))

    parts.append(_q(con, f"""
        SELECT host, {RN} AS repo,
               count(*) AS eras,
               sum(CASE WHEN measurable THEN 1 ELSE 0 END) AS meas,
               round(100.0*sum(CASE WHEN measurable THEN 1 ELSE 0 END)/count(*), 1) AS pct_meas,
               round(median(CASE WHEN measurable THEN total_seconds/3600.0 END), 2) AS p50_h
        FROM v_era GROUP BY host, {RN}
        HAVING count(*) > 0 ORDER BY eras DESC LIMIT 40
    """, "4 · ALL REPOS ON ALL HOSTS — the fleet grid"))

    parts.append(_q(con, f"""
        SELECT CASE WHEN measurable THEN 'measurable' ELSE 'UNMEASURABLE' END AS state,
               coalesce(nullif(reason,''), '(no reason recorded)') AS reason,
               count(*) AS eras, count(DISTINCT host) AS hosts, count(DISTINCT repo) AS repos
        FROM v_era GROUP BY 1, 2 ORDER BY eras DESC LIMIT 15
    """, "5 · WHY eras are unmeasurable — fleet-wide",
        "The single largest obstacle to a CPM/PERT plan built on real durations."))

    parts.append(_q(con, f"""
        SELECT phase,
               count(*) AS steps,
               round(median(seconds/3600.0), 3) AS p50_h,
               round(sum(seconds)/3600.0, 2) AS effort_h
        FROM v_era_step WHERE {RN} = '{R}'
        GROUP BY phase ORDER BY effort_h DESC NULLS LAST LIMIT 15
    """, f"6 · Per-phase step takt for `{R}` (all hosts)"))

    parts.append(_q(con, f"""
        SELECT host, {RN} AS repo, count(*) AS rows, sum(total_tokens) AS total_tokens,
               count(DISTINCT phase) AS phases, count(DISTINCT capture_method) AS methods
        FROM v_tokens GROUP BY host, {RN} ORDER BY total_tokens DESC NULLS LAST LIMIT 20
    """, "7 · Per-phase TOKEN USE — token+tokens unioned as ONE kind (S21)",
        "Reading only `kind=tokens` under-reports; reading both without record_id dedup double-counts."))

    if skipped:
        parts.append("### 8 · UNREADABLE FILES — excluded from every figure above\n\n"
                     "Cause measured on this host: the same column has different parquet\n"
                     "types across hosts (`repo` is JSON in some files, VARCHAR in others),\n"
                     "and `union_by_name` reconciles names but not types.\n\n"
                     + "\n".join(f"- `{s}`" for s in skipped))

    print("\n\n".join(parts))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
