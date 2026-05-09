# secure-signatures

Status: draft

## What this produces

A `sign / verify / rotate` interface for cryptographically signing data files and code artefacts at a security level appropriate for sensitive NHS-data flows, plus a v1 concrete realisation using **Ed25519 detached signatures** wrapped in a small JSON envelope persisted alongside the artefact at `<artefact_path>.sig.json`. Consumers bind to the three-method interface, never to the underlying scheme; a v2 backend (HSM-resident keys, hardware tokens, or post-quantum schemes such as ML-DSA / SLH-DSA from NIST FIPS 204 / 205) is a backend swap, not a consumer rewrite.

The pattern is **research-grounded**: the chosen scheme draws on NHS Digital data-pipeline practice (signed manifests on data deliveries; HL7 FHIR Signature element compatibility; the SHA-256-manifest discipline already exercised in the sipdem BNF data pipeline at `BNF-MANIFEST-001`). It is intentionally drafted: at v1 the scheme is settled but the exact published-standard mapping to a specific NHS Digital information-standard ID (e.g. DCB-class clinical-risk standards, DAPB-class data-extract standards) is left to the consumer's implementation pass to pin against the version of the relevant NHS Digital catalogue active at adoption time.

## Why it matters

Once data and code artefacts begin to flow across trust boundaries — between this repo and a downstream NHS-data consumer; between an analyst's workstation and a published dataset; between a release tarball and a deploying operator — the pipeline needs an unforgeable answer to "did this come from where it claims to?". A SHA-256 manifest answers integrity (the bytes match) but not authenticity (the bytes came from the named producer). Signatures close that second hole. Without a curated answer, every consumer re-derives the choice of scheme, the encoding, and the rotation protocol — and the NHS-data threat model penalises divergence sharply (a verifier that accepts an obsolete-curve signature is a verifier that accepts a forgery).

The sipdem `BNF-MANIFEST-001` pipeline (138 files / 840 GiB, manifest-verified at every raw load via SHA-256 streaming-chunk re-verification per IR-004) is the closest existing model in the catalog's reference orbit. The 074 spec's § Prompt 3 names the explicit forward gap: SHA-256 integrity is in scope; signature authenticity is deferred. This pattern is the catalog's home for filling that gap — at v1 with Ed25519, with the substitution surface holding the door open for the PQC scheme that follows.

## v1 concrete signature scheme

**Ed25519 (RFC 8032), detached signature over the artefact's raw bytes, wrapped in a small JSON envelope** persisted at `<artefact_path>.sig.json`. The envelope carries `{scheme, public_key_id, signature, signed_at, envelope_version}` — deliberately minimal; anything that depends on a versioned schema (commit SHA, build number, signer identity) belongs in a separate manifest that is itself signed. The chosen scheme is driven by four constraints — NHS data-pipeline lineage (HL7 FHIR Signature element practice has converged on RFC-defined modern-curve signatures for new-build flows), side-channel and small-key properties (32-byte public keys, 64-byte signatures, deterministic signing, constant-time reference implementations), library availability (`pynacl` and `cryptography` ship Ed25519 in stable APIs without optional builds), and substitution-surface readiness (Ed25519 is the natural pre-quantum companion to NIST FIPS 204 ML-DSA / FIPS 205 SLH-DSA, and a v2 hybrid envelope `{ed25519_signature, mldsa_signature}` carries both signatures forward-compatibly).

Forbidden as v1 schemes: raw RSA-PKCS#1-v1.5 (malleability + small-exponent attacks); ECDSA over secp256r1 with a non-deterministic-RFC-6979 implementation (per-signature nonce-reuse risk); any scheme whose published reference implementation is not constant-time. RSA-PSS and deterministic ECDSA-RFC-6979 are acceptable substitutions if a consumer has a hard requirement for a different curve, but Ed25519 is the v1 default.

## The substitution surface

A v2 backend MUST implement the same three-method interface and MUST honour the same envelope shape (or extend it forward-compatibly via `envelope_version`). Concrete substitution points:

- **Hardware-backed keys** (HSM, YubiKey, Apple Secure Enclave, Windows TPM via NCrypt). The private key never leaves the hardware boundary; `sign()` becomes an HSM API call with the Ed25519 key handle as input. The envelope is unchanged. The [`local-secrets-store`](../local-secrets-store/description.md) `fetch(name)` returns an opaque key handle (a URI or PKCS#11 label) instead of raw key bytes.
- **Post-quantum schemes** (NIST FIPS 204 ML-DSA, FIPS 205 SLH-DSA, CNSA 2.0 transition). The envelope's `scheme` field is a closed enum at v1 (`"Ed25519"` only); v2 extends it with `"ML-DSA-65"` / `"SLH-DSA-SHA2-128s"` / etc., and the envelope MAY carry a hybrid `{ed25519_signature, mldsa_signature}` pair to satisfy both pre- and post-quantum verifiers during a migration window. `verify()` at v2 succeeds if every signature listed in the envelope verifies; absence of either half is itself a verification failure.
- **Detached PGP signatures** (RFC 4880 / RFC 9580 successor). For interoperability with legacy NHS S/MIME-or-PGP flows. The envelope is replaced by a sibling `<artefact>.sig.asc` file; this pattern's `verify()` recognises both the JSON envelope and the legacy `.sig.asc` suffix and dispatches to the appropriate verifier.

## How a feature uses this pattern

This pattern is `Status: draft` — no glpnet feature has yet adopted it. The full implementation surface (the three-method interface in detail; the JSON envelope shape; the per-artefact-type guidance for data files, code release bundles, and multi-file delivery manifests; the trust-anchor publication discipline; the rotation-event audit machinery) is consolidated in the curated upstream catalog, grounded in NHS Digital data-pipeline practice plus the published web standards (RFC 8032, RFC 7515, NIST FIPS 186-5 / 204 / 205, HL7 FHIR Signature element); see [sources.md](./sources.md) for the citations. Bind private-key material via the [`local-secrets-store`](../local-secrets-store/description.md) pattern's `fetch(name)` interface — never read key files directly inside this pattern.

When the first glpnet feature adopts this pattern, that feature's PR is responsible for promoting `Status:` to `active` (after at least one downstream consumer has signed + verified a real NHS-style data delivery and a second consumer has exercised the `rotate()` path across a real key-rotation event), fleshing out [applicability.md](./applicability.md) with substantive `### <consumer-name>` sections, and updating [../directory.md](../directory.md)'s suffix.

## Cross-cutting policies

This pattern is NOT on either policy's `Applies to` list at v1. It interacts with [Policy 1](../policies.md#policy-1--no-cleartext-auth-tokens-fr-cc-1) indirectly — the private signing key is a secret, fetched via [`local-secrets-store`](../local-secrets-store/description.md) — but the realisation is in `local-secrets-store/description.md`, not here. If a future revision routes signature-event telemetry to the off-repo glpnet datalake destination, that revision adds `secure-signatures` to [Policy 2](../policies.md#policy-2--non-config-history-off-repo-to-glpnet-datalake-fr-cc-2)'s `Applies to` list and adds the cross-link from this `description.md` then.
