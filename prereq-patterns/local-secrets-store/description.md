# local-secrets-store

Status: draft

## What this produces

A standardised four-method local-secrets-store interface — `store(name, secret) / fetch(name) / rotate(name, new_secret) / delete(name)` — backed at v1 by a home-directory file store with owner-only permissions and atomic-rename writes. Consumers (the [`background-task-manager`](../background-task-manager/description.md) registry's `secrets_ref` lookups, any pattern that needs a credential or token on the developer's machine, the eventual [`secure-signatures`](../secure-signatures/description.md) `sign()` private-key fetch) bind to the four method signatures, never to the on-disk layout. A v2 backend (OS keyring, cloud secret manager, encrypted file at a different root) is a backend swap, not a consumer rewrite.

This pattern carries the **concrete realisation of [Policy 1](../policies.md#policy-1--no-cleartext-auth-tokens-fr-cc-1)** — the chosen v1 hash algorithm and parameters for any secret material that must persist as a hash rather than as cleartext. Other patterns on Policy 1's `Applies to` list (`dbos`, `flask-sqlalchemy-alembic-api`, `background-task-manager`) cross-link to the policy and to this pattern; they do not name the algorithm themselves.

## Why it matters

Every pattern in the catalog that touches an authentication-bearing context — DBOS workflow inputs, Flask app credentials, the registry's `secrets_ref` pointer, signing-key material — eventually needs somewhere to put a secret on the developer's machine. Without a single curated answer each consumer hand-rolls its own (plaintext in the repo, ad-hoc env var, shell-out to `pass(1)` only on POSIX), and that spread is exactly the surface where Policy 1 violations leak in. This pattern is the curated answer; the interface is small enough that consumers cannot accidentally bypass it, and the v1 backend is concrete enough that a developer can adopt it inside ten minutes.

The hatzinor `ulpani_lms_credentials.py` reference (cited through the upstream catalog; see [sources.md](./sources.md)) is the closest existing on-disk model: per-user JSON credentials at `<user-home>/ulpani/lms/admin.json`, owner-only permissions tightened with `os.chmod(0o600)` on POSIX and `icacls /inheritance:r /grant:r <user>:F` on Windows, atomic-rename writes that tighten permissions on the temp file before the rename, and password material persisted as Argon2id hashes via `argon2-cffi`. The sibling-clone references (`sipdem`'s Three-Rule Secret-Handling Contract, `ospark`'s `.env.template` + Kubernetes Opaque Secret) frame the discipline boundary: secrets enter the process from a named, audited source; never appear in `repr()` / logs / error messages; never commit to this repo.

## Chosen v1 hash algorithm and parameters (Policy 1 concrete realisation)

When a secret must be persisted as a hash rather than as cleartext (typical case: a credential-presentation challenge where the registry verifies a presented credential without ever reading the secret back), this pattern uses:

**Argon2id (RFC 9106), memory cost m = 64 MiB (65536 KiB), time cost t = 3, parallelism p = 4, hash length 32 bytes, random salt 16 bytes per call.**

These are the `argon2.PasswordHasher()` defaults shipped by `argon2-cffi` ≥ 21.x. The choice is driven by three constraints:

1. [Policy 1](../policies.md#policy-1--no-cleartext-auth-tokens-fr-cc-1) names the minimum-bar family `{Argon2id, scrypt, bcrypt}`. Argon2id is the RFC 9106 winner and the only family of the three that is hybrid-mode-by-construction (data-dependent + data-independent), giving it both side-channel and GPU/ASIC resistance properties.
2. The single-developer workstation threat model fits the RFC 9106 "second recommended option" (m=64 MiB, t=3, p=4) — strong against an offline brute-force attempt while keeping single-hash latency under ~200 ms on commodity hardware.
3. `argon2-cffi`'s defaults match this option exactly, so an implementation reduces to `PasswordHasher().hash(secret)` / `.verify(stored_hash, presented_secret)` with no parameter override required. The persisted hash carries the parameters in its PHC string (`$argon2id$v=19$m=65536,t=3,p=4$<salt>$<hash>`), so a future parameter bump is detected by a presented-secret-fails-verify path and triggers a re-hash on the next successful presentation.

Forbidden algorithms for any secret material in this catalog (per [Policy 1](../policies.md#policy-1--no-cleartext-auth-tokens-fr-cc-1) `**Specifics.**`): raw SHA-1, SHA-2 (SHA-256/384/512), SHA-3, MD5. This pattern names them only to be explicit about the rule it is bound by; the canonical statement is in `policies.md` and this pattern does not restate it.

## v1 home-directory layout

Persisted artefact path: `~/.glpnet/secrets/<name>.secret` (glpnet-local equivalent of the upstream catalog's home-directory secrets convention; see [sources.md](./sources.md) for the upstream reference). The basename is the secret's logical name; allowed characters `[A-Za-z0-9_.+@-]+`. Atomic-write discipline: write to `<path>.tmp`, set owner-only permissions on the temp file, `os.replace()` into place, re-set permissions on the final file. The path MUST resolve OUTSIDE this repo's working tree — hatzinor's `_assert_outside_repo()` invariant is the reference: the persisted artefact's resolved path MUST NOT be a child of the repo root.

Encryption-at-rest stance for v1: **plaintext owner-only file**. The single-developer workstation threat model relies on OS-level home-directory access control; encryption-at-rest does not strengthen that boundary materially while it does add a passphrase-management surface. v2 lifts encryption-at-rest into the backend (OS keyring's per-user encrypted store; cloud secret manager's at-rest encryption); v1 does not pay v2 complexity for the v1 threat.

## How a feature uses this pattern

This pattern is `Status: draft` — no glpnet feature has yet adopted it. The full implementation surface (the path-resolver, the cross-platform owner-only-permissions helper, the atomic-rename writer, the Argon2id wrapper, the Three-Rule frozen-dataclass discipline for in-memory secret containers) is consolidated in the curated upstream catalog, which itself reaches into hatzinor / sipdem / ospark; see [sources.md](./sources.md) for the citations. When the first glpnet feature adopts this pattern, that feature's PR is responsible for promoting `Status:` to `active` (after at least one downstream consumer has exercised `store / fetch / rotate / delete` end-to-end and a second consumer has exercised the v2 substitution surface without a consumer-side rewrite), fleshing out [applicability.md](./applicability.md) with substantive `### <consumer-name>` sections, and updating [../directory.md](../directory.md)'s suffix.
