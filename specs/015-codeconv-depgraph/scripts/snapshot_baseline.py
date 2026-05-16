"""T004/T005 baseline snapshot for feature 015-codeconv-depgraph.

Produces:
- specs/015-codeconv-depgraph/baseline.json (counts: files, imports, isolated)
- specs/015-codeconv-depgraph/pre_feature_schema_snapshot.txt (\\dn, \\dt outputs)

Reads from the live bridge (sidecar at <data-dir>/bridge.json).
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import psycopg

from codeconv.bridge_client import acquire_or_discover


def main() -> int:
    data_dir = Path("C:/pglite/research/glpnet")
    repo_root = Path(__file__).resolve().parents[3]
    endpoint = acquire_or_discover(
        repo_root,
        data_dir=str(data_dir),
        bridge_script=str(
            repo_root / "prereq-patterns" / "pglite" / "pglite_bridge.mjs"
        ),
    )
    dsn = (
        f"host={endpoint.host} port={endpoint.port} "
        "dbname=postgres user=postgres"
    )

    specs_dir = Path("specs/015-codeconv-depgraph")
    specs_dir.mkdir(parents=True, exist_ok=True)

    with psycopg.connect(dsn) as conn:
        with conn.cursor() as cur:
            cur.execute("SELECT COUNT(*) FROM codeconv.dart_files")
            n_files = cur.fetchone()[0]
            cur.execute("SELECT COUNT(*) FROM codeconv.dart_imports")
            n_imports = cur.fetchone()[0]
            cur.execute(
                """
                SELECT COUNT(*) FROM codeconv.dart_files f
                WHERE NOT EXISTS (
                    SELECT 1 FROM codeconv.dart_imports i
                    WHERE i.from_path = f.path OR i.to_path = f.path
                )
                """
            )
            n_isolated = cur.fetchone()[0]

            cur.execute(
                "SELECT schema_name FROM information_schema.schemata "
                "WHERE schema_name NOT LIKE 'pg_%' AND schema_name <> 'information_schema' "
                "ORDER BY schema_name"
            )
            schemas = [row[0] for row in cur.fetchall()]

            tables_by_schema: dict[str, list[str]] = {}
            for sch in schemas:
                cur.execute(
                    "SELECT table_name FROM information_schema.tables "
                    "WHERE table_schema = %s AND table_type='BASE TABLE' "
                    "ORDER BY table_name",
                    (sch,),
                )
                tables_by_schema[sch] = [row[0] for row in cur.fetchall()]

    baseline = {
        "feature": "015-codeconv-depgraph",
        "snapshot_at": endpoint.heartbeat_at_iso,
        "data_dir": str(data_dir),
        "counts": {
            "dart_files": n_files,
            "dart_imports": n_imports,
            "isolated_files": n_isolated,
        },
        "expected_per_spec": {
            "dart_files": 128,
            "dart_imports": 443,
            "isolated_files": 6,
        },
    }
    (specs_dir / "baseline.json").write_text(
        json.dumps(baseline, indent=2) + "\n", encoding="utf-8"
    )

    lines = ["# pre-feature-015 schema snapshot — produced by snapshot_baseline.py", ""]
    lines.append("## \\dn (schemas, excluding pg_* / information_schema)")
    for sch in schemas:
        lines.append(f"- {sch}")
    lines.append("")
    for sch in schemas:
        lines.append(f"## \\dt {sch}.*")
        for tbl in tables_by_schema[sch]:
            lines.append(f"- {sch}.{tbl}")
        lines.append("")
    (specs_dir / "pre_feature_schema_snapshot.txt").write_text(
        "\n".join(lines), encoding="utf-8"
    )

    print(f"dart_files: {n_files}")
    print(f"dart_imports: {n_imports}")
    print(f"isolated_files: {n_isolated}")
    print(f"schemas: {schemas}")
    print(f"wrote {specs_dir / 'baseline.json'}")
    print(f"wrote {specs_dir / 'pre_feature_schema_snapshot.txt'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
