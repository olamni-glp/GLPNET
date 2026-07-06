# Blind re-scan record — BB-HDR-4 (identity law: name = link-authenticated identity)

**042 pass (FR-004/FR-014, R4 protocol)** · 2026-07-04 · baseline HEAD(6ff3a8c9)
**Original family**: C (repo, claim C-12; 040 owner-ruled). **Re-scanned**: A (F1 only), B (F2 only). Blind: topic only — "binding of peer names/identities to authenticated links; name ownership, duplicate-name handling".

## Family A scan
1. glpnet keys identity to link authentication: "feature-036 authenticated `PeerId` keying all permission/quota/landing decisions … the glpnet analogue of macaroon/amulet" (F1 L49). HIGH.
2. The authenticated-link substrate is SPKI-SHA-256 cert pinning over QUIC (F1 L48). MED.
3. §12 rates the identity-keyed approach S (PeerId) for S2 (F1 L204, L52). HIGH.
Silent on: name-as-identity laws, first-come ownership, duplicate-name handling (nearest: olamnit NameId degrade, L145 — routing behavior only).

## Family B scan
1. Linked local namespaces rooted in keys replace global PKI (SDSI, F2 L423). HIGH.
2. Keys as principals with key-to-key delegation (SPKI RFC 2693, F2 L420). HIGH.
3. did:key-anchored identity for local-first delegation (UCAN, F2 L411). MED.
4. Self-verifying offline identity from signed statements (Vouchsafe, F2 L432). MED.
5. Auditable name→key binding via verifiable directories (CONIKS, F2 L487). MED.
No evidence on link-bound names, first-come ownership, or dup-name handling.

## Curator verdict (T018)
**CONFIRMED.** The block's core — a peer's name IS its link-authenticated identity, with
capabilities layered above — is corroborated by family A (F1's independent record of the
PeerId-keyed design) and grounded by family B's key-centric-naming lineage. The specific
ownership rules (first-come owns; dup-id tracked-never-addressable; incumbent-keeps-route)
remain 040-owner-ruled repo authority — consistent with the method's authority order (owner
ruling > repo head > F1 > F2), not a gap. E4's per-peer key enrolment (propagated by RP-042-05)
strengthens the identity binding. No conflict; no escalation.
