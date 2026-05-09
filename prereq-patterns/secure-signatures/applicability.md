# Applicability — secure-signatures

Universally applicable: no glpnet consumers yet — applicability TBD when first glpnet feature adopts this pattern.

The upstream catalog's `applicability.md` covers consumer-class adaptation for signing a single data export, signing a code artefact at release, verifying on the consuming side, and the key-rotation path (scheduled / compromise / algorithm-bump variants) — including NHS-data-specific notes on HL7 FHIR Signature element interaction (see [sources.md](./sources.md) for the citation). When a glpnet consumer arrives, that feature's PR adds substantive `### <consumer-name>` sections here.
