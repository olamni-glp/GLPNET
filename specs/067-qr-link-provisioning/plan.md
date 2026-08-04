<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 57a8b9fc-2569-4312-9cb0-17b9be75ff62
-->

# Implementation Plan: QR-code link + cert provisioning via generated PDF or hub display page

**Branch**: `067-qr-link-provisioning` | **Date**: 2026-08-04 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/067-qr-link-provisioning/spec.md`

## Summary

One-scan provisioning for the glp-quick QUIC+WS mesh: the hub (an already-provisioned host
holding the trunk material in `glpquick-cert/`) mints a short-lived, per-device, trunk-signed
derived certificate, encrypts it with a one-time passphrase, and renders it as chunked QR codes
on a display-only hub surface; a joining device scans, decrypts, and joins under the unchanged
036 manual-pin trust model. Producer and lifecycle (issue/audit/revoke) live in the Python
`glp_quick` control plane; the single C#-side change is extending the acceptance seam
(`QuicTransport` pin validation + a revocation check at the join seam) to accept trunk-signed
derived certificates in addition to the exact trunk pin. Trunk private-key material is never
rendered, persisted as imagery, or printed (spec Security Posture, mandatory first-class scope).

## Technical Context

**Language/Version**: Python 3.11+ (`glp_quick` package, typer CLI) for producer/lifecycle;
C# / .NET 10 (`csharp/glp_link`, `csharp/glp_quick_host`) for the acceptance seam
**Primary Dependencies**: existing — `typer`, `cryptography` (already used by
`glp_quick/src/glp_quick/cert.py` for the shared cert + SPKI pin); new — `segno` (pure-Python QR,
terminal/ANSI rendering, no image file needed), `fpdf2` (pure-Python PDF, non-secret page only).
No new C# packages — derived-cert verification uses `System.Security.Cryptography.X509Certificates`.
**Storage**: flat files under `glpquick-cert/provision/` (gitignored alongside the trunk
material): `audit.jsonl` (append-only), `revoked.jsonl` (append-only), issued-credential index
`issued.jsonl`. No PGLite/DB involvement — this feature never touches `<repo>/.pgdb/`.
**Testing**: `pytest` under `glp_quick/tests/{unit,integration}` (producer, payload codec,
lifecycle); `dotnet test csharp/glp_link.tests` (derived-cert acceptance, revocation refusal,
`ERR` token behaviour — `glp_quick_host` internals are tested from `glp_link.tests` via
`InternalsVisibleTo`, there is no separate host test project)
**Target Platform**: Windows 11 hosts on a LAN (same as 036/049); Android consumer is
out-of-repo (olamnit-assistant) and consumes only the published payload contract
**Project Type**: CLI tool (Python control plane) + library change (C# link layer)
**Performance Goals**: provisioning end-to-end < 5 min (SC-001); revocation enforced at the
join seam within 60 s (FR-009 default); QR bundle ≤ 8 chunks at version-25 QR capacity
**Constraints**: fail-closed loaders (no degraded/no-pin path, per `SharedCertMaterial`
contract); 036 manual-pin model unchanged for existing endpoints (FR-012); trunk key never
rendered/persisted-as-image/printed (FR-002/FR-005/FR-006); reject tokens stay in the existing
`ERR <token>` style so `glp_quick/src/glp_quick/stacks/csharp.py` exit-code mapping keeps working
**Scale/Scope**: LAN-scale mesh (4-slot accept capacity today); tens of devices, not thousands

## Constitution Check

*GATE: evaluated against constitution v1.1.0 before Phase 0; re-checked after Phase 1.*

- **I. Spec-First**: PASS — spec.md (clarified 2026-08-04) precedes this plan; the one spec
  refinement discovered during research (who mints the device keypair) is recorded in
  research.md R-003 and folded back into the spec's clarify trail at analyze if upheld.
- **II. Bug-Protocol / No-Workarounds**: PASS — plan adds no tolerance paths; loaders stay
  fail-closed; a validation failure is a loud `ERR` refusal, never a fallback.
- **III. SRSW**: PASS — no GLP code in this feature (Python + C# only); the forbidden
  SRSW-escape token appears in no artifact.
- **IV-a/IV-b. Language Authority / Preserve Internals**: PASS — no GLP language surface, no
  runtime internals touched.
- **V. Claude-Only LM**: PASS — no LM in the feature path at all.
- **VI-a. Additive-Only Persistence**: PASS — no DB migrations; the three JSONL stores are
  append-only by design (revocation and audit rows are never rewritten).
- **VI-b. Single PGLite Cluster**: PASS — no PGLite consumer added; provisioning state is flat
  files under the existing gitignored cert dir, not a second cluster.
- **VII. Test-Gated, Commit-Scoped Shipping**: PASS — plan defines per-story tests in both
  suites; ship via buildkit GitFlow at the chain's ship stage.
- **VIII. Single Source of Truth**: PASS — the payload contract lives in ONE place
  (`contracts/payload-contract.md`); the Android consumer references it; spec references 036
  rather than restating its trust model.

Post-Phase-1 re-check: unchanged — no violations introduced by the design; Complexity Tracking
is empty.

## Project Structure

### Documentation (this feature)

```text
specs/067-qr-link-provisioning/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── payload-contract.md    # versioned QR chunk + encryption envelope + test vectors (FR-011)
│   ├── cli-contract.md        # glp-quick provision CLI surface + exit codes
│   └── join-seam-contract.md  # C# acceptance-seam + revocation-list contract
└── tasks.md             # Phase 2 output (/bk-tasks — NOT created by /bk-plan)
```

### Source Code (repository root)

```text
glp_quick/src/glp_quick/
├── cert.py                    # EXISTS — gains derive_device_cert() (trunk-signed, TTL-bounded)
├── provision/                 # NEW package — producer + lifecycle
│   ├── __init__.py
│   ├── bundle.py              # bundle build/serialize, chunking, integrity (payload contract impl)
│   ├── crypto.py              # scrypt KDF + AES-GCM envelope; one-time passphrase generation
│   ├── session.py             # ProvisioningSession lifecycle (window, single-redemption, expiry)
│   ├── qr_render.py           # segno terminal/ANSI rendering (display-only; never writes images)
│   ├── pdf_render.py          # fpdf2 non-secret page (endpoint/pin/instructions/session ref)
│   ├── audit.py               # append-only audit.jsonl writer/query
│   ├── revoke.py              # append-only revoked.jsonl writer; issued.jsonl index
│   └── join.py                # consumer intake on the new host (paste/scan-file → cert dir)
├── cli.py                     # EXISTS — gains `provision` sub-app (session/pdf/revoke/audit/join)
└── terminal/pages.py          # EXISTS — hub display page hosts the QR render (display-only)

glp_quick/tests/
├── unit/test_provision_*.py       # bundle codec, crypto envelope, chunking, session state,
│                                  # audit/revoke stores, renderers (incl. no-image-persist checks)
├── integration/test_provision_flow.py  # mint → render → join → mesh accept (loopback)
└── vectors/provision/             # published decode test vectors (FR-011, consumed by contract)

csharp/glp_link/transports/
├── QuicTransport.cs           # EXISTS — PinValidationCallback extended: exact trunk pin OR
│                              # trunk-signed derived cert (validity window + revocation check)
└── DerivedCredentialValidator.cs  # NEW — trunk-signature check, window check, revocation lookup

csharp/glp_link.tests/
└── DerivedCredentialTests.cs  # NEW — accept/expire/revoke/refuse matrix + ERR token checks
```

**Structure Decision**: Producer, lifecycle, and both render surfaces live in the Python
control plane (`glp_quick`) where the trunk material is already generated and the only display
surface (terminal pages) exists; the C# change is confined to the acceptance seam
(`QuicTransport` + one new validator class) so `glp_quick_host` and the mesh router stay
untouched except for refusal tokens. No web server is introduced — the "hub display page" is a
terminal page (existing surface), which also satisfies never-persist-secret-images by
construction.

## Complexity Tracking

No constitution violations to justify — table intentionally empty.
