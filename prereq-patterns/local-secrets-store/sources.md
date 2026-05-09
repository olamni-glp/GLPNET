# Sources — local-secrets-store

The AIGRID prereq-patterns catalog is glpnet's consolidating upstream for this pattern. Its `prereq-patterns/local-secrets-store/` index reaches into hatzinor's `ulpani_lms_credentials.py` (per-user credentials file, owner-only permissions, Argon2id), sipdem's Three-Rule Secret-Handling Contract (env-only sourcing, default-credential rejection, no-log-value invariant via redacted `__repr__` + `scrub_secrets()`), ospark's `.env.template` + Kubernetes Opaque Secret discipline, and IETF RFC 9106 (the Argon2 specification). Glpnet has no own implementation today; the AIGRID-side citations carry the deeper upstream identities.

## Index

| Path | Upstream | Action | Summary |
|---|---|---|---|
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/local-secrets-store/description.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's pattern description — four-method interface, v1 home-directory layout, chosen Argon2id parameters, substitution surface to OS keyring / cloud secret manager / encrypted file. |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/local-secrets-store/applicability.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's consumer-class notes for developer-laptop, CI-runner-ephemeral-home, multi-user-shared-host, and follow-on-backend-swap cases. |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/local-secrets-store/sources.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's underlying-source citations into hatzinor `ulpani_lms_credentials.py`, sipdem 074-ca001-s6-sec1 spec + `primitives.py` + S535 design-spec, ospark env-templates + K8s Opaque Secret, plus IETF RFC 9106 (Argon2). |

## Per-source notes

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/local-secrets-store/description.md`

- The four-method interface (`store / fetch / rotate / delete`) and the substitution surface (OS keyring, cloud secret manager, encrypted file at a different root) are the load-bearing claims a glpnet adopter MUST honour. Adopt the interface verbatim; keep the v2 backend swap consumer-rewrite-free.
- The chosen v1 hash algorithm and parameters (Argon2id with `argon2-cffi`'s defaults: m=64 MiB, t=3, p=4, hash_len=32, salt_len=16) are mirrored verbatim in this pattern's [description.md § "Chosen v1 hash algorithm and parameters"](./description.md). Do NOT diverge — Policy 1's allocation discipline binds the choice to a single canonical statement, and any drift between AIGRID's value and glpnet's value is a defect.
- The v1 plaintext-owner-only-file stance (no encryption-at-rest at v1; encryption-at-rest is a v2 backend property) and the `_assert_outside_repo()` invariant are the load-bearing simplicity guarantees. Adopt them verbatim.

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/local-secrets-store/applicability.md`

- Per-consumer H3s cover four cases. The developer-laptop case is the default; the CI-runner-ephemeral-home case requires a CI-shim that converts CI-provider env vars into `store()` calls at job start (do NOT short-circuit by reading env vars directly inside the consumer); the multi-user-shared-host case tightens hatzinor's `_set_owner_only()` from "best-effort soft warn" to "hard error"; the follow-on-backend-swap case is the v2 substitution path with concrete adaptation notes for OS keyring (Python `keyring` library; `SERVICE` constant), cloud secret manager (Vault / AWS / GCP; per-secret ARN/path/mount), and encrypted-file-at-different-root (e.g. `age`-encrypted under a project path).

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/local-secrets-store/sources.md`

- Cites hatzinor `ulpani_lms_credentials.py` (Action: Model) — the closest existing on-disk model. Key references: `_assert_outside_repo()` (lines 73-82, the safety guard against `Path.home()` redirection); `_set_owner_only()` (lines 95-111, cross-platform owner-only-permissions helper with POSIX `0o600` and Windows `icacls /inheritance:r /grant:r <user>:F`); `_write_credentials_file()` (lines 160-179, the atomic-rename reference that tightens permissions on the temp file BEFORE the rename); `argon2.PasswordHasher().hash(...)` (line 198 inside `_RESET_INNER_SCRIPT`, the canonical Argon2id usage with library defaults).
- Cites sipdem `primitives.py` (Action: Model) plus three sipdem spec / design-spec files (Action: Read) — the Three-Rule Secret-Handling Contract: presence + non-default + redacted-`__repr__` / `scrub_secrets()`. The frozen-dataclass shape with overridden `__repr__` returning the literal string `"Secrets(<redacted>)"` is the load-bearing structural defence against accidental value-emission via `print(secrets)` / `f"{secrets}"` / `logger.info(secrets)`. The 10-token default-credential rejection set (`{"postgres", "password", "admin", "root", "changeme", "minioadmin", "minio", "secret", "default", "test"}`) is the right starting set for any consuming pattern's CI-shim.
- Cites ospark `.env.template` + `osmaoz/.env.example` + `secrets.yml` (all Action: Read) — operator-side env-template discipline (header comment `# SECURITY: .env is gitignored. Never commit credentials.`, reject-by-default `changeme_*_2026` placeholders matching the sipdem rejection set) and Kubernetes Opaque Secret as production-equivalent. The `secrets.yml` file's "for production, use a secrets manager (Vault, AWS Secrets Manager, etc.)" comment is the canonical forward-pointer to this pattern's v2 substitution surface.
- Cites IETF RFC 9106 (Action: Read) — the Argon2 specification; the source of the "second recommended option" parameter set (m=64 MiB, t=3, p=4) this pattern adopts. § 4 (Parameter Choice) is the trade-off space; § 9 (Security Considerations) explains why hybrid Argon2id is the right choice for password hashing where side-channel resistance matters; § 7 (PHC string format) defines the `$argon2id$v=19$m=<MB>,t=<T>,p=<P>$<salt>$<hash>` encoding the persisted hash carries.
