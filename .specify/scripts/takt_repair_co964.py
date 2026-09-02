"""Repair the three host=ariellas takt era parquet files whose `size` column is
typed JSON instead of VARCHAR (shiras P0, co #964, 2026-09-02T05:20Z).

Lossless by construction: every column is carried through unchanged except
`size`, which is CAST to VARCHAR. Row count is asserted equal before/after.
Only host=ariellas files are touched - a peer's published rows are never rewritten.
"""
import duckdb, os, shutil, sys, datetime

ERA = '//192.168.0.108/GAVRI_D/coop/_takt-lake/takt/kind=era'
BAK = 'D:/BSTDEV/research/glp/GLPNET/.specify/takt-repair-backup-20260902'

TARGETS = [
    'host=ariellas/date=2026-08-23/ariellas-20260823t185326488411.parquet',
    'host=ariellas/date=2026-08-23/ariellas-20260823t185402773273.parquet',
    'host=ariellas/date=2026-08-24/ariellas-era-080-yngenios-20260824t012500.parquet',
]

con = duckdb.connect()
os.makedirs(BAK, exist_ok=True)


def strict_union():
    """The read shiras reports as throwing: no union_by_name."""
    try:
        n = con.execute(
            "SELECT count(*) FROM read_parquet(?)", [ERA + '/**/*.parquet']).fetchone()[0]
        return ('OK', n)
    except Exception as e:
        return ('THROWS', str(e).split('\n')[0][:150])


def measurable():
    try:
        return con.execute(
            "SELECT count(*) FROM read_parquet(?, union_by_name=true) "
            "WHERE hours IS NOT NULL", [ERA + '/**/*.parquet']).fetchone()[0]
    except Exception as e:
        return 'ERR ' + str(e)[:80]


print('BEFORE  strict union :', strict_union())
print('BEFORE  measurable   :', measurable())

for rel in TARGETS:
    src = ERA + '/' + rel
    if not os.path.exists(src):
        print('SKIP (absent):', rel)
        continue
    before = con.execute("SELECT count(*) FROM read_parquet(?)", [src]).fetchone()[0]
    cols = [r[0] for r in con.execute("DESCRIBE SELECT * FROM read_parquet(?)", [src]).fetchall()]

    # keep a byte copy of the original before touching it
    shutil.copy2(src, os.path.join(BAK, os.path.basename(src)))

    tmp = src + '.fixed'
    con.execute(
        "COPY (SELECT * REPLACE (CAST(size AS VARCHAR) AS size) FROM read_parquet(?)) "
        "TO ? (FORMAT PARQUET)", [src, tmp])

    after = con.execute("SELECT count(*) FROM read_parquet(?)", [tmp]).fetchone()[0]
    newcols = [r[0] for r in con.execute("DESCRIBE SELECT * FROM read_parquet(?)", [tmp]).fetchall()]
    newtype = {r[0]: r[1] for r in con.execute(
        "DESCRIBE SELECT * FROM read_parquet(?)", [tmp]).fetchall()}['size']

    assert before == after, f'ROW LOSS on {rel}: {before} -> {after}'
    assert cols == newcols, f'COLUMN DRIFT on {rel}'
    assert newtype == 'VARCHAR', f'size still {newtype} on {rel}'

    os.replace(tmp, src)
    print(f'REPAIRED {rel}  rows {before}->{after}  size JSON->VARCHAR  cols {len(cols)} unchanged')

print('AFTER   strict union :', strict_union())
print('AFTER   measurable   :', measurable())
print('backup dir:', BAK)
