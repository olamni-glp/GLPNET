# Sources — secure-signatures

The AIGRID prereq-patterns catalog is glpnet's consolidating upstream for this pattern. AIGRID's `prereq-patterns/secure-signatures/` is itself a research-grounded pattern whose load-bearing references are predominantly published web standards (IETF RFCs, NIST FIPS publications, HL7 FHIR specs) plus one internal upstream — the sipdem 074 spec's deferred PQC clause and the BNF-MANIFEST-001 SHA-256 manifest precedent. Glpnet has no own implementation today; the AIGRID-side catalog files carry the deeper upstream identities (web standards + sipdem reference).

## Index

| Path | Upstream | Action | Summary |
|---|---|---|---|
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/secure-signatures/description.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's pattern description — three-method interface, JSON envelope shape, chosen Ed25519 scheme, supported artefact types, substitution surface to HSM / PQC / PGP. |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/secure-signatures/applicability.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's consumer-class notes for signing-a-data-export, signing-a-code-release, verifying-on-the-consuming-side, and key-rotation (scheduled / compromise / algorithm-bump) cases. |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/secure-signatures/sources.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's underlying-source citations: IETF RFC 8032 (EdDSA / Ed25519), RFC 7515 (JWS), RFC 4880 (OpenPGP, legacy substitution), RFC 6979 (deterministic ECDSA, alternative); NIST FIPS 186-5 (DSS), 204 (ML-DSA), 205 (SLH-DSA); HL7 FHIR Signature element; plus sipdem 074-ca001-s6-sec1 spec + S535 design-spec for the deferred-PQC forward gap and the SHA-256 streaming integrity precedent. |

## Per-source notes

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/secure-signatures/description.md`

- The three-method interface (`sign / verify / rotate`) and the JSON envelope `{scheme, public_key_id, signature, signed_at, envelope_version}` are the load-bearing claims a glpnet adopter MUST honour. The envelope is deliberately minimal — anything depending on a versioned schema (commit SHA, build number, signer identity) belongs in a separate manifest that is itself signed.
- The v1 chosen scheme (Ed25519 detached signatures wrapped in the JSON envelope) and the v1 forbidden list (raw RSA-PKCS#1-v1.5; ECDSA without RFC-6979 deterministic nonces; any non-constant-time reference implementation) are mirrored in this pattern's [description.md § "v1 concrete signature scheme"](./description.md). Bind the v1 implementation to `pynacl.signing.SigningKey` / `pynacl.signing.VerifyKey` (or `cryptography.hazmat.primitives.asymmetric.ed25519`); both are stable-API and constant-time.
- The substitution surface (HSM / PQC / PGP) is forward-compatible by construction: the envelope's `scheme` field is a closed enum at v1 and extends at v2 to add ML-DSA / SLH-DSA / hybrid pairs; the verifier MUST refuse any unknown scheme as `ok=False, reason="scheme_unsupported"` and never silently fall back.

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/secure-signatures/applicability.md`

- Per-consumer H3s cover four cases. Signing-a-data-export (stream the file through the signer for artefacts > ~1 GiB; publish the public-key fingerprint via a separate channel from the artefact + envelope, otherwise verification reduces to "the file matches its own self-attested public key"). Signing-a-code-release (artefact is small; verifier's `ok=True` is a precondition on `tar -xzf` / `unzip` / `pip install`; the bundle's internal integrity — Python wheel `RECORD`, npm `package-lock.json` — is the bundle's own concern). Verifying-on-the-consuming-side (resolve `envelope.public_key_id` against the consumer's trust anchor; failed verification returns `ok=False`, never raises; envelope.signed_at supports a freshness policy). Key-rotation (scheduled, compromise, algorithm-bump variants — distinct grace-period file handling for each).

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/secure-signatures/sources.md`

- Cites IETF RFC 8032 (EdDSA, Ed25519 specification — § 5.1 modes, § 5.1.6 verification returns Boolean not exception, § 8 deterministic-signing rationale), RFC 7515 (JWS conventions — `alg` / `kid` field-naming inherited as `scheme` / `public_key_id` in this pattern's envelope; § 10 algorithm-confusion-attack guard), RFC 4880 (OpenPGP detached signatures — model for the v2 PGP substitution path), RFC 6979 (deterministic ECDSA — the only acceptable v1 NIST-curve substitution).
- Cites NIST FIPS 186-5 (DSS umbrella standard naming Ed25519 as approved), FIPS 204 (ML-DSA / Dilithium — v2 PQC target; ML-DSA-65 for security-strength-3, ML-DSA-87 for long-archive-life data), FIPS 205 (SLH-DSA / SPHINCS+ — hash-based-only PQC fallback if ML-DSA's lattice security argument weakens).
- Cites HL7 FHIR R5 Signature element — the building block for signed FHIR Bundles in NHS interoperability flows. This pattern's envelope is a strict subset that omits FHIR-specific machinery (`Signature.who`, `Signature.type` Coding) because those belong inside the FHIR Bundle, not to a generic data file. Both layers are independent and both are required for end-to-end NHS-data trust.
- Cites sipdem 074-ca001-s6-sec1 spec (§ Prompt 3 IR-004 SHA-256 streaming-chunk re-verification at every raw-load step; the explicit "PQC signature verification — deferred" forward gap) and S535 infosec design-spec (§ 2 Prompt 3 with the no-degraded-mode invariant: no `--skip-verify`, no `--force`, no trust-on-first-use cache; a `verify()` returning `ok=False` is the only correct response). Together these establish the integrity precedent above which this pattern's signature adds the authenticity layer.
