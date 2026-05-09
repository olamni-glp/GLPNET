# Applicability — pglite-backup-restore

Universally applicable: no glpnet consumers yet — applicability TBD when first glpnet feature adopts this pattern.

The upstream catalog's `applicability.md` covers consumer-class adaptation for four DB topology cases: an idle pglite database, a database under live DBOS workflow load (with the at-most-once-after-restore caveat for non-idempotent steps), a database serving a Flask API (the `globalWorkChain`-mediated serialisation that lets backups complete without blocking the API indefinitely), and a database being upgraded between pglite versions (the manifest-vs-live version-mismatch refusal). See [sources.md](./sources.md) for the citation. When a glpnet consumer arrives, that feature's PR adds substantive `### <consumer-name>` sections here.
