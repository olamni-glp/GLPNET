# Corpus — C#/.NET stack — Cluster 05: shared cert + fingerprint pinning + TLS 1.3

2026-06-27

Scope: how a single shared self-signed certificate plus fingerprint/public-key pinning becomes the **only** trust anchor (no CA) on BOTH a C#/.NET QUIC peer (System.Net.Quic / System.Net.Security) and the Python `cryptography` cert-generation tool. Covers `RemoteCertificateValidationCallback` pinning on client and server, `SslServerAuthenticationOptions` / `SslClientAuthenticationOptions`, QUIC option plumbing (ALPN + `ClientAuthenticationOptions`/`ServerAuthenticationOptions`), the minimal X.509 profile TLS-1.3/QUIC needs, and the Python self-signed cert + key + PFX/PEM + thumbprint/SPKI-pin recipe.

Informs primarily: **FR-003** (authenticate the link via a shared self-signed cert), **SC-005** (the shared cert is the ONLY trust anchor — no CA), **FR-001** (the link itself).

---

### [1] QUIC configuration options in .NET
- **URL**: https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-options
- **Type / version / date**: Microsoft Learn conceptual; System.Net.Quic (stable from .NET 9); page dated 2025-01-11.
- **Architectural concern**: cert-trust (also ALPN / handshake plumbing)
- **Close-read findings**:
  - QUIC TLS is configured exactly via SslStream-style options: `QuicClientConnectionOptions.ClientAuthenticationOptions` is an `SslClientAuthenticationOptions` (identical to `SslStream.AuthenticateAsClient`); `QuicServerConnectionOptions.ServerAuthenticationOptions` is an `SslServerAuthenticationOptions`. So **every cert-pinning mechanism that works on SslStream works verbatim on QUIC** — including `RemoteCertificateValidationCallback`.
  - Server-side TLS options are valid only if at least one of `ServerCertificateSelectionCallback`, `ServerCertificateContext`, or `ServerCertificate` yields a certificate, AND at least one `ApplicationProtocols` (ALPN) value is present, AND `EncryptionPolicy != NoEncryption`.
  - `QuicListenerOptions.ApplicationProtocols` (ALPN, RFC 7301) is mandatory and must contain ≥1 value; the per-connection `ServerAuthenticationOptions.ApplicationProtocols` must be a subset. Client `ClientAuthenticationOptions.ApplicationProtocols` likewise mandatory.
  - `ConnectionOptionsCallback` (mandatory on the listener) is handed `SslClientHelloInfo` (the SNI/server name from the client) and returns the `QuicServerConnectionOptions` per incoming connection — the hook where the server can attach its `RemoteCertificateValidationCallback` for mutual pinning.
  - If `CipherSuitesPolicy` is set it must include one of the three TLS-1.3 AEAD suites (`TLS_AES_128_GCM_SHA256`, `TLS_AES_256_GCM_SHA384`, `TLS_CHACHA20_POLY1305_SHA256`); default `null` lets MsQuic use OS QUIC-compatible suites.
- **Informs**: FR-003 (client + server auth options carry the pin), SC-005 (cert provided directly, not from a CA chain), FR-001 (ALPN names the channel-link protocol).
- **Confidence**: high

### [2] SslClientAuthenticationOptions.RemoteCertificateValidationCallback Property
- **URL**: https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslclientauthenticationoptions.remotecertificatevalidationcallback?view=net-8.0
- **Type / version / date**: Microsoft Learn API ref; applies net-5.0…net-11.0; page dated 2025-07-01.
- **Architectural concern**: cert-trust
- **Close-read findings**:
  - Property type `RemoteCertificateValidationCallback?`; "responsible for validating the certificate supplied by the remote party." When set, it overrides/augments the default chain validation — the boolean it returns is authoritative for the handshake.
  - Source-linked to dotnet/runtime `SslClientAuthenticationOptions.cs`; this is the canonical client-side pin attachment point that flows straight into `QuicClientConnectionOptions.ClientAuthenticationOptions`.
  - Companion knobs on the same options object: `TargetHost` (SNI; the client sends it but with pinning it need NOT match the cert SAN for *trust*), `EnabledSslProtocols` (leave `None` to defer to OS / negotiate TLS 1.3), `CertificateChainPolicy` (alternative customization path — `X509ChainPolicy.CustomTrustStore`).
- **Informs**: FR-003 (client trusts the pinned server cert), SC-005 (no CA — callback is the trust decision).
- **Confidence**: high

### [3] RemoteCertificateValidationCallback Delegate
- **URL**: https://learn.microsoft.com/en-us/dotnet/api/system.net.security.remotecertificatevalidationcallback?view=net-7.0
- **Type / version / date**: Microsoft Learn API ref; applies through net-11.0; dated 2025-07-01.
- **Architectural concern**: cert-trust
- **Close-read findings**:
  - Exact signature: `delegate bool RemoteCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)`. Return `true` = accept this peer cert; `false` = abort the handshake (`AuthenticationException`).
  - `sslPolicyErrors` carries the SSPI-reported errors; the relevant values are `None`, `RemoteCertificateNotAvailable`, `RemoteCertificateNameMismatch`, `RemoteCertificateChainErrors`. A self-signed cert with no OS-trusted CA normally produces `RemoteCertificateChainErrors` (and `RemoteCertificateNameMismatch` if SAN/host differs) — both must be tolerated *because we pin instead*.
  - Reference example returns `true` only when `sslPolicyErrors == SslPolicyErrors.None`, else `false` — the safe default we deliberately replace with an identity-pinning check.
  - Used with `SslStream`; since QUIC's auth options are the same `Ssl*AuthenticationOptions`, the same delegate is reused unchanged.
- **Informs**: FR-003, SC-005 (the callback, not a CA, decides trust).
- **Confidence**: high

### [4] QUIC support in .NET (overview)
- **URL**: https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-overview
- **Type / version / date**: Microsoft Learn conceptual; System.Net.Quic public + stable since .NET 9 (preview in 7/8); MsQuic-backed.
- **Architectural concern**: handshake (TLS-1.3 mandate) / packaging
- **Close-read findings**:
  - QUIC (RFC 9000) **mandates TLS 1.3** (RFC 9001) and has stream multiplexing at the transport layer — there is no opt-out to a lower TLS version, so the cert profile only needs to satisfy TLS-1.3/QUIC, not legacy TLS.
  - Always guard with `QuicListener.IsSupported` / `QuicConnection.IsSupported` first — these are false if libmsquic is missing OR "TLS 1.3 might not be supported." Packaging note: msquic.dll ships with .NET on **Windows 11 / Server 2022+**; Linux needs `libmsquic` 2.2+ (apt/apk/dnf), macOS via Homebrew (`brew install libmsquic`) + `DYLD_FALLBACK_LIBRARY_PATH`.
  - Minimal server example: `QuicServerConnectionOptions` with `DefaultStreamErrorCode`, `DefaultCloseErrorCode`, and `ServerAuthenticationOptions = new SslServerAuthenticationOptions { ApplicationProtocols = [new SslApplicationProtocol("protocol-name")], ServerCertificate = serverCertificate }`; listener with `ListenEndPoint`, `ApplicationProtocols`, `ConnectionOptionsCallback`.
  - Minimal client example sets `ClientAuthenticationOptions = new SslClientAuthenticationOptions { ApplicationProtocols = [...], TargetHost = "" }` — `TargetHost` can be empty/arbitrary precisely because trust comes from pinning, not hostname.
  - `QuicConnection.RemoteCertificate` exposes the peer cert after connect — useful for post-handshake assertions/logging of the pinned identity.
- **Informs**: FR-001 (the link is QUIC+TLS1.3), FR-003, SC-005.
- **Confidence**: high

### [5] TLS/SSL best practices (SslStream) — incl. certificate pinning + custom trust
- **URL**: https://learn.microsoft.com/en-us/dotnet/core/extensions/sslstream-best-practices
- **Type / version / date**: Microsoft Learn conceptual; updated 2026-04-29 (notes .NET 11 behavior).
- **Architectural concern**: cert-trust / failure-modes
- **Close-read findings**:
  - **Official pinning recipe (authoritative for this cluster):** a `RemoteCertificateValidationCallback` that (a) rejects if any error *other than* `RemoteCertificateChainErrors` is present — `if ((sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateChainErrors) != 0) return false;` — then (b) returns `certificate.GetPublicKeyString().Equals(ExpectedPublicKey)`. i.e. **tolerate only the no-CA chain error, then verify the exact pinned public key.** This is exactly "trust this fingerprint only."
  - **Alternative no-callback path**: set `CertificateChainPolicy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, CustomTrustStore = { customIssuerCert } }`. With a self-signed leaf used as its own root, the cert can be placed in `CustomTrustStore` so it is the *only* accepted anchor — no OS store, no other app affected. Works on both `SslClientAuthenticationOptions` and `SslServerAuthenticationOptions`.
  - Server cert supply: prefer `ServerCertificateContext` (`SslStreamCertificateContext`) — building the `X509Chain` is CPU-intensive, so build once and reuse across connections; also enables TLS session resumption on Linux. `ServerCertificate` and `ServerCertificateSelectionCallback` are the other two routes.
  - TLS version: leave `EnabledSslProtocols = None` (default) to defer to the OS and get TLS 1.3; do not pin a cipher suite (`CipherSuitesPolicy` unsupported on Windows).
  - Client-cert (mutual) caution: building the client `X509Chain` may trigger AIA fetches / revocation lookups (DoS surface) — constrain via `CertificateChainPolicy`. .NET 11 disables server-side AIA downloads by default. For a pinned self-signed pair there is no chain to fetch, so disable revocation/AIA in the chain policy.
- **Informs**: FR-003 (concrete pin callback), SC-005 (CustomRootTrust = single anchor, no CA), failure-mode hardening.
- **Confidence**: high

### [6] SslServerAuthenticationOptions — RemoteCertificateValidationCallback / ClientCertificateRequired (mutual pinning)
- **URL**: https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslserverauthenticationoptions.remotecertificatevalidationcallback?view=net-7.0
- **Type / version / date**: Microsoft Learn API ref (cross-checked with `SslServerAuthenticationOptions` class page).
- **Architectural concern**: cert-trust (mutual/peer auth) / failure-modes
- **Close-read findings**:
  - The server side has the **same** `RemoteCertificateValidationCallback` to validate the *client's* cert — so a symmetric pin (both ends present and verify the one shared self-signed cert) is implemented by attaching the identical callback on `ServerAuthenticationOptions`.
  - `ClientCertificateRequired = true` only *asks* the client for a cert; if none is sent the server still completes the handshake unless the callback rejects. Therefore enforcement of "client must present the pinned cert" must live in `RemoteCertificateValidationCallback` (reject when `certificate` is null or thumbprint mismatches).
  - Custom validation can be partially done via `CertificateChainPolicy`, or fully via `RemoteCertificateValidationCallback` (per the property remarks).
- **Informs**: FR-003 (mutual authentication via shared cert), SC-005 (server also has no CA — pins the client identity).
- **Confidence**: high

### [7] QuicClientConnectionOptions.ClientAuthenticationOptions (API)
- **URL**: https://learn.microsoft.com/en-us/dotnet/api/system.net.quic.quicclientconnectionoptions.clientauthenticationoptions?view=net-9.0
- **Type / version / date**: Microsoft Learn API ref; net-9.0 (stable).
- **Architectural concern**: ALPN / cert-trust plumbing
- **Close-read findings**:
  - `ClientAuthenticationOptions` "contains the TLS setting for the client connection… same as used in `SslStream.AuthenticateAsClient(SslClientAuthenticationOptions)`." Property is **mandatory** — omitting it throws on `ConnectAsync`.
  - Confirms the pin callback set on a plain `SslClientAuthenticationOptions` is the exact object QUIC consumes; no QUIC-specific validation API exists or is needed.
- **Informs**: FR-003, FR-001 (mandatory ALPN + TLS options on connect).
- **Confidence**: high

### [8] cryptography.io — X.509 tutorial (self-signed cert generation)
- **URL**: https://cryptography.io/en/latest/x509/tutorial/
- **Type / version / date**: pyca/cryptography official docs (latest, 50.x line).
- **Architectural concern**: packaging (cert/key generation)
- **Close-read findings**:
  - Key gen: `rsa.generate_private_key(public_exponent=65537, key_size=2048)` or `ec.generate_private_key(ec.SECP256R1())`. EC P-256 is TLS-1.3-friendly and smaller.
  - Cert build chain: `x509.CertificateBuilder().subject_name(name).issuer_name(name)` (subject == issuer for self-signed) `.public_key(key.public_key()).serial_number(x509.random_serial_number()).not_valid_before(now).not_valid_after(now + timedelta(days=N)).add_extension(...).sign(key, hashes.SHA256())`.
  - Extensions available and relevant: `x509.SubjectAlternativeName([x509.DNSName("localhost")])`, `x509.BasicConstraints(ca=False, path_length=None)`, `x509.KeyUsage(...)` (set `digital_signature=True`, `key_encipherment=True` for RSA server certs), `x509.ExtendedKeyUsage([x509.ExtendedKeyUsageOID.SERVER_AUTH, x509.ExtendedKeyUsageOID.CLIENT_AUTH])`, `x509.SubjectKeyIdentifier.from_public_key(key.public_key())`.
  - PEM serialization: `cert.public_bytes(serialization.Encoding.PEM)`; key `key.private_bytes(encoding=PEM, format=PrivateFormat.TraditionalOpenSSL or PKCS8, encryption_algorithm=...)`.
- **Informs**: SC-005 (the tool emits the single shared cert), FR-003 (cert profile the C# stack consumes).
- **Confidence**: high

### [9] cryptography.io — serialization (PKCS12/PFX export + SPKI for pinning)
- **URL**: https://cryptography.io/en/latest/hazmat/primitives/asymmetric/serialization/
- **Type / version / date**: pyca/cryptography official docs (latest).
- **Architectural concern**: packaging (interchange format for C#) / cert-trust (pin value)
- **Close-read findings**:
  - PFX export for direct C# `X509Certificate2` load: `pkcs12.serialize_key_and_certificates(name=b"glplink", key=private_key, cert=certificate, cas=None, encryption_algorithm=BestAvailableEncryption(b"password"))` → bytes written to `.pfx`. (Use `NoEncryption()` for an unencrypted PFX.)
  - SPKI pin value (DER of `SubjectPublicKeyInfo`, RFC 5280): `spki_der = public_key.public_bytes(encoding=Encoding.DER, format=PublicFormat.SubjectPublicKeyInfo)`; pin = `base64.b64encode(hashlib.sha256(spki_der).digest())`. This SPKI-SHA256 is the format-stable pin (survives cert re-issue with same key) and matches OWASP "pin-sha256".
  - The DER SPKI bytes (hex) correspond to what C# reads via `certificate.GetPublicKeyString()` (used in the MS pinning sample) — so the Python tool can emit the exact expected-public-key string the C# callback compares against.
- **Informs**: FR-003 (both stacks consume the same key material), SC-005, FR-001.
- **Confidence**: high (PFX API exact; the GetPublicKeyString↔SPKI correspondence is reasoned, verify byte-for-byte at impl time)

### [10] X509Certificate2 thumbprint / GetCertHashString (SHA-256 pin computation in C#)
- **URL**: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509certificate.getcerthashstring?view=net-8.0 (+ roxeem.com SHA-256 thumbprints article: https://roxeem.com/2025/08/22/beyond-sha-1-finding-certificates-by-sha-256-and-sha-3-thumbprints-in-net-10/)
- **Type / version / date**: Microsoft Learn API ref + 2025-08 technical article; net-8.0/net-10.0.
- **Architectural concern**: cert-trust (pin value computation)
- **Close-read findings**:
  - Whole-cert (DER) SHA-256 fingerprint in C#: `cert.GetCertHash(HashAlgorithmName.SHA256)` → byte[]; `cert.GetCertHashString(HashAlgorithmName.SHA256)` → hex string. `X509Certificate2.Thumbprint` is **SHA-1** (legacy) — do NOT pin on it; use the SHA-256 overload.
  - .NET 10 adds a `Thumbprint256` property and `FindByThumbprint(HashAlgorithmName, ...)` overloads; pre-.NET-10 you enumerate/compare `GetCertHash(HashAlgorithmName.SHA256)` yourself.
  - Two valid pin strategies: **(a) whole-cert SHA-256** (`GetCertHashString(SHA256)` in C# ↔ `hashlib.sha256(cert_der)` of `cert.public_bytes(Encoding.DER)` in Python) — simplest, but breaks on any re-issue; **(b) SPKI public-key pin** (`GetPublicKeyString()`/SPKI-SHA256 ↔ source [9]) — survives re-issue with the same key. The MS best-practices sample pins on the public key.
- **Informs**: FR-003 (pin value), SC-005 (the comparison that *is* the trust decision).
- **Confidence**: high

### [11] cryptography.io serialization — PEM/DER encodings (cross-stack interchange)
- **URL**: https://cryptography.io/en/latest/x509/tutorial/ (Serialization section) + serialization reference
- **Type / version / date**: pyca/cryptography official docs (latest).
- **Architectural concern**: packaging
- **Close-read findings**:
  - C# `X509Certificate2` loads: `.pfx`/PKCS12 (cert + private key, via `X509CertificateLoader.LoadPkcs12FromFile` / ctor) — the server needs the private key, so PFX is the right interchange for the cert *holder*. PEM cert-only (`X509Certificate2.CreateFromPemFile` / `CreateFromPem`) suffices for the *pinning peer* (which only needs the public cert to compute the expected pin).
  - The Python tool should therefore emit: one **PFX** (cert+key, password) for the cert-bearing peer(s), one **PEM** cert (no key) for distributing the public identity, and a precomputed **pin string** (SHA-256 of DER, plus SPKI-SHA256) for embedding in the C# callback.
- **Informs**: FR-003, SC-005, FR-001 (cert distribution shape).
- **Confidence**: med (loader API names current as of .NET 9/10; confirm exact loader at impl time)

---

## Cluster feasibility verdict

- **Exact C# "trust this fingerprint only" mechanism (client AND server, identical):** attach a `RemoteCertificateValidationCallback` to `SslClientAuthenticationOptions` (→ `QuicClientConnectionOptions.ClientAuthenticationOptions`) and to `SslServerAuthenticationOptions` (→ `QuicServerConnectionOptions.ServerAuthenticationOptions`). Body, per MS best-practices: reject if `(sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateChainErrors) != 0` (i.e. tolerate ONLY the "no CA chain" error — and treat name-mismatch as acceptable too since we don't pin on hostname), then return `true` iff the presented cert matches the pin — either `certificate.GetPublicKeyString().Equals(ExpectedPublicKey)` (SPKI/public-key pin, preferred) or `cert.GetCertHashString(HashAlgorithmName.SHA256).Equals(ExpectedSha256)` (whole-cert pin). Equivalent no-callback option: `CertificateChainPolicy = X509ChainPolicy { TrustMode = CustomRootTrust, CustomTrustStore = { sharedSelfSignedCert } }`.
- **What the Python `cryptography` tool must emit:** (1) a self-signed leaf via `CertificateBuilder` signed `SHA256`, subject==issuer, `BasicConstraints(ca=False)`, `KeyUsage(digital_signature, key_encipherment)`, `ExtendedKeyUsage([SERVER_AUTH, CLIENT_AUTH])`, a SAN (DNSName, e.g. `localhost`), reasonable `not_valid_before/after`; key = RSA-2048 or EC P-256. (2) a **PFX** (`pkcs12.serialize_key_and_certificates`) for the cert holder(s) — C# needs the private key for the server cert. (3) a **PEM** public cert for the pinning peer. (4) precomputed **pin strings**: whole-cert `sha256(cert.public_bytes(DER))` and SPKI `sha256(public_key.public_bytes(DER, SubjectPublicKeyInfo))` — embedded as the C# `Expected*` constant.
- **Disabling validation is NOT acceptable:** a callback that `return true;` unconditionally accepts ANY cert (full MITM exposure). SC-005 requires the pinned cert be the *only* trust anchor, so the callback MUST positively verify the pinned identity (public-key or SHA-256 match) and reject everything else; it may only *waive the absence of a CA chain*, never the identity check.
- **TLS-1.3 / QUIC cert constraints:** QUIC mandates TLS 1.3 (RFC 9001) — leave `EnabledSslProtocols = None` to negotiate it via the OS; gate everything on `QuicConnection.IsSupported` / `QuicListener.IsSupported` (false without libmsquic or TLS 1.3). `ApplicationProtocols` (ALPN) is mandatory on listener, server options, and client options and identifies the channel-link protocol. The X.509 profile needs `EKU=serverAuth` (+ `clientAuth` for mutual), a valid `notBefore/notAfter` window, and a SAN, but **SAN/hostname is irrelevant to trust because we pin** — `TargetHost` on the client may be empty/arbitrary; the name-mismatch SslPolicyError is expected and tolerated.
- **Hostname/SAN clarification:** since trust = pin, no DNS/SAN match is required; this removes the need for the cert's CN/SAN to track real endpoints and lets one shared cert serve all peers. Still include a SAN to keep some stacks/parsers happy.
- **Failure-mode / hardening note:** for mutual pinning, `ClientCertificateRequired = true` only *requests* a client cert — enforce presence+match in the server's `RemoteCertificateValidationCallback` (reject null/ mismatch). Disable revocation/AIA chain lookups (no CA to reach) via `CertificateChainPolicy` (`RevocationMode = NoCheck`); .NET 11 already disables server-side AIA by default.

## Sources
- https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-options
- https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-overview
- https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslclientauthenticationoptions.remotecertificatevalidationcallback?view=net-8.0
- https://learn.microsoft.com/en-us/dotnet/api/system.net.security.remotecertificatevalidationcallback?view=net-7.0
- https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslserverauthenticationoptions.remotecertificatevalidationcallback?view=net-7.0
- https://learn.microsoft.com/en-us/dotnet/api/system.net.quic.quicclientconnectionoptions.clientauthenticationoptions?view=net-9.0
- https://learn.microsoft.com/en-us/dotnet/core/extensions/sslstream-best-practices
- https://cryptography.io/en/latest/x509/tutorial/
- https://cryptography.io/en/latest/hazmat/primitives/asymmetric/serialization/
- https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509certificate.getcerthashstring?view=net-8.0
- https://roxeem.com/2025/08/22/beyond-sha-1-finding-certificates-by-sha-256-and-sha-3-thumbprints-in-net-10/
</content>
</invoke>
