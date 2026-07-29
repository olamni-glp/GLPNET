"""DuckLake aging tier behind a narrow seam (research R6) — implemented by T021.

Periodic migration of aged metadata (> aging window, configurable) from the
PGlite hot tier to DuckDB-over-parquet under the gitignored
``ms_message/.data/lake/`` dir; catch-up queries UNION hot + lake. If the
``duckdb`` dependency misbehaves on a host, this seam degrades LOUDLY to
PGlite-only — a named warning, never silent (all contract guarantees except
aged-tier query locality are preserved).
"""
