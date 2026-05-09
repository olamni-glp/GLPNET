# Applicability — background-task-manager

Universally applicable: no glpnet consumers yet — applicability TBD when first glpnet feature adopts this pattern.

The upstream catalog's `applicability.md` covers consumer-class adaptation for four topology shapes: bootstrapping just pglite alone (the minimal case — the registry contains only the pglite sidecar's row); adding a single dependent service (with `secrets_ref` resolution at fire-up time); adding two services with a dependency edge between them (the realistic case — topological-order traversal over the prereq DAG); and the safe-shutdown traversal under each topology (mirror-image reverse-topological with the no-running-dependents guard). See [sources.md](./sources.md) for the citation. When a glpnet consumer arrives, that feature's PR adds substantive `### <consumer-name>` sections here.
