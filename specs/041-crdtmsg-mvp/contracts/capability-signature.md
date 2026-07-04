# Contract — Capability & Signature

Traces: FR-017..FR-022, FR-035; SC-005/006/011.

## C14. Macaroon verify-before-act (FR-017)
- **Invariant**: every routed action is gated by a macaroon whose caveats are **fail-closed** — an unsatisfiable OR un-understood caveat → fail. Verification happens **before** acting.
- **Invariant**: a refusal is a **distinct, recorded** provenance outcome — never a silent drop (SC-006).

## C15. Amulet slot (FR-018)
- **Invariant**: reserved token slot of the Amoeba 4-field shape `{Port 48b, ObjNum 24b, Rights 8b, Check ≥128b}`; literal 16-byte fidelity rejected. Rights-bit semantics deferred (does not block the wire slot).

## C16. Membership vs identity (FR-019)
- **Invariant**: SPKI-pin shared cert = layer-0 **membership only** (possession=membership), never per-peer identity. Per-peer identity comes from the enrolled Ed25519 key (C17).

## C17. Whole + sub-content signatures (FR-020/022; OC-2)
- **Invariant**: `whole_content_sig` (Ed25519 over the deterministic binary term encoding) + `sub_content_seals[]` (per-block COSE_Sign in a Biscuit-style append-only chain). Per-peer key enrolled at mesh join, bound to peer-name.
- **Invariant**: any single-byte tamper, or any removal/reorder of a signed sub-block, → verification fails (SC-005, 100%).
- **Invariant**: signatures still verify after lossless transcode across all 4 surfaces (SC-011).

## C18. Two signature classes (FR-021)
- **Invariant**: content attestation (Ed25519) and capability (macaroon HMAC) are never conflated; attenuating a capability does not invalidate content history.

## C19. Provenance (FR-035)
- **Invariant**: 100% of operations including refusals produce a durable `{peer, target, timestamps, sha256, outcome∈closed-enum}` keyed to authenticated identity.
