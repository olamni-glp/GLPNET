<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: QR-code link + cert provisioning via generated PDF or hub display page

**Input**: Design documents from `specs/067-qr-link-provisioning/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (3), quickstart.md
**Tests**: INCLUDED — the mandatory security posture demands negative tests (SC-003) and the
repo discipline is test-gated (Constitution VII).

## Phase 1: Setup

- [ ] T001 Add `segno` and `fpdf2` to `glp_quick/pyproject.toml` dependencies; create package
      skeleton `glp_quick/src/glp_quick/provision/__init__.py`; install into `glp_quick/.venv`
- [ ] T002 Verify `glpquick-cert/` (and add `glpquick-derived/`) are gitignored; create the
      `glpquick-cert/provision/` store path convention in `glp_quick/src/glp_quick/provision/__init__.py`
      (paths only — no secret ever lands in git)

## Phase 2: Foundational (blocking all user stories)

- [ ] T003 [P] Implement `derive_device_cert()` in `glp_quick/src/glp_quick/cert.py`: mint
      device keypair + trunk-signed TTL-bounded cert (default 30 days) + SPKI fingerprint,
      reusing the existing pin math; private key returned in memory only (research R-003)
- [ ] T004 [P] Implement scrypt+AES-256-GCM envelope + 6-word one-time passphrase generation in
      `glp_quick/src/glp_quick/provision/crypto.py` per contracts/payload-contract.md §2
- [ ] T005 [P] Implement bundle build/serialize + GQP1 chunking + order-independent assembly
      with named errors (missing/corrupt/conflicting/version_mismatch) in
      `glp_quick/src/glp_quick/provision/bundle.py` per contracts/payload-contract.md §§1,3,4
- [ ] T006 Implement append-only JSONL stores (audit/revoked/issued, per data-model.md) in
      `glp_quick/src/glp_quick/provision/audit.py` and `glp_quick/src/glp_quick/provision/revoke.py`
      — rows carry fingerprints/labels/timestamps only, never key material
- [ ] T007 [P] Foundational unit tests in `glp_quick/tests/unit/test_provision_crypto.py` and
      `glp_quick/tests/unit/test_provision_bundle.py`: envelope roundtrip, wrong-passphrase
      no-partial-plaintext, chunk assembly matrix with contract error tokens

**Checkpoint**: payload codec + credential mint + stores stand alone and green.

## Phase 3: User Story 1 — One-scan desktop host onboarding (P1) 🎯 MVP

**Goal**: hub session renders encrypted chunked QR on the terminal display page; new host joins
with derived material; trunk key never rendered.
**Independent test**: two-host (or loopback) provision per quickstart.md — join succeeds with
zero manual cert copying; no trunk key bytes in any rendered output.

- [ ] T008 [US1] Implement `ProvisioningSession` lifecycle (open→rendered→redeemed/expired/
      aborted, 10-min default window, crash-recovery marks interrupted sessions expired) in
      `glp_quick/src/glp_quick/provision/session.py`
- [ ] T009 [P] [US1] Implement segno terminal/ANSI QR rendering on a hub display page
      (display-only, passphrase shown once alongside, zero image-file writes) in
      `glp_quick/src/glp_quick/provision/qr_render.py` (+ page wiring in
      `glp_quick/src/glp_quick/terminal/pages.py`)
- [ ] T010 [US1] Add `provision` typer sub-app with `session` and `join` commands (exit codes
      per contracts/cli-contract.md) in `glp_quick/src/glp_quick/cli.py`
- [ ] T011 [US1] Implement consumer intake → `glpquick-derived/` writer (abort-safe: no
      plaintext on disk before final decrypt) in `glp_quick/src/glp_quick/provision/join.py`
- [ ] T012 [US1] Implement `DerivedCredentialValidator` (trunk-signature check against pinned
      trunk SPKI, validity window ±90 s skew bound, revocation-set membership) in
      `csharp/glp_link/transports/DerivedCredentialValidator.cs` per contracts/join-seam-contract.md
- [ ] T013 [US1] Extend `PinValidationCallback` (trunk pin OR valid derived cert) + new
      `ERR cert_expired` / `ERR cert_revoked` tokens in
      `csharp/glp_link/transports/QuicTransport.cs` (fail-closed preserved; `cert_mismatch`
      semantics unchanged)
- [ ] T014 [US1] Plumb `--derived-dir` through client role + map new `ERR` tokens to exit codes
      in `glp_quick/src/glp_quick/stacks/csharp.py` and `glp_quick/src/glp_quick/cli.py`
- [ ] T015 [P] [US1] C# acceptance tests (trunk accepted; derived valid/expired/not-yet-valid/
      self-signed matrix) in `csharp/glp_link.tests/DerivedCredentialTests.cs`
- [ ] T016 [US1] Loopback integration test mint→render→join→mesh-accept in
      `glp_quick/tests/integration/test_provision_flow.py`
- [ ] T017 [US1] Negative render-path tests (SC-003): no trunk-key bytes in QR payloads,
      terminal output, audit rows, or any store, in
      `glp_quick/tests/unit/test_provision_posture.py`

**Checkpoint**: US1 independently demonstrable — MVP.

## Phase 4: User Story 2 — Credential lifecycle: audit and revocation (P2)

**Goal**: every render/issue/redeem/expire/refuse/revoke audited; revocation enforced ≤ 60 s.
**Independent test**: provision→revoke→rejoin-refused (`cert_revoked`); second device
unaffected; audit answers SC-004 alone.

- [ ] T018 [US2] Add `revoke` and `audit` commands (issued⋈revoked join, `--json`) in
      `glp_quick/src/glp_quick/cli.py` + `glp_quick/src/glp_quick/provision/revoke.py`
- [ ] T019 [US2] Revocation enforcement in the listener: mtime-triggered reload re-checked per
      accept (≥ every 10 s), corrupt-file ⇒ derived path fail-closed while trunk path stays up,
      in `csharp/glp_link/transports/DerivedCredentialValidator.cs`
- [ ] T020 [US2] Single-redemption/replay refusal (`ERR session_replayed`) + `PROVISION_REDEEMED
      <fingerprint>` event line consumed by the Python session in
      `csharp/glp_quick_host/Program.cs` and `glp_quick/src/glp_quick/provision/session.py`
- [ ] T021 [P] [US2] Tests: revoke-mid-listen enforced ≤ 60 s, re-provision-after-revoke, audit
      completeness (SC-004), replay refusal, in `csharp/glp_link.tests/DerivedCredentialTests.cs`
      and `glp_quick/tests/unit/test_provision_lifecycle.py`

## Phase 5: User Story 3 — Android consumer contract (P3)

**Goal**: contract + vectors sufficient for an out-of-repo consumer, no live mesh needed.
**Independent test**: decode all vectors per contracts/payload-contract.md §5 alone.

- [ ] T022 [US3] Generate the six vector files (synthetic throwaway material only) into
      `glp_quick/tests/vectors/provision/` via a deterministic generator in
      `glp_quick/tests/vectors/provision/generate_vectors.py`
- [ ] T023 [P] [US3] Vector self-conformance test (valid vectors assemble byte-identically incl.
      shuffled order; each invalid vector yields its named error) in
      `glp_quick/tests/unit/test_provision_vectors.py`

## Phase 6: User Story 4 — Printable non-secret hand-off (P4)

- [ ] T024 [US4] Implement non-secret PDF page (endpoint/pin/session/instructions; API accepts
      only non-secret fields; posture-citing refusal otherwise, audited) in
      `glp_quick/src/glp_quick/provision/pdf_render.py` + `pdf` command in
      `glp_quick/src/glp_quick/cli.py`
- [ ] T025 [P] [US4] Tests: generated PDF byte-scan contains no key material or envelope chunks;
      secret-content request refuses + audits, in `glp_quick/tests/unit/test_provision_pdf.py`

## Phase 7: Polish & Cross-Cutting

- [ ] T026 Validate quickstart.md end-to-end on loopback; fold any friction back into the
      contracts (single source of truth) in `specs/067-qr-link-provisioning/quickstart.md`
- [ ] T027 Full suites green + baseline compare: `glp_quick` pytest (unit+integration) and
      `dotnet test csharp/glp_link.tests`; record counts in the marathon trace

## Dependencies

- Phase 2 blocks all stories. US1 (Phase 3) blocks US2 (T019/T020 build on T012/T013).
- US3 depends only on Phase 2 (T005 codec). US4 depends only on Phase 2 + T008 (session ref).
- Story order for delivery: US1 → US2 → US3 → US4; US3/US4 can run parallel to US2.

## Parallel Examples

- Phase 2: T003 ∥ T004 ∥ T005 (different files); T007 after its targets.
- Phase 3: T009 ∥ T012 (Python render vs C# validator); T015 ∥ T016 once T012-T014 land.
- Post-US1: T021 ∥ T022/T023 ∥ T024/T025.

## Implementation Strategy

MVP = Phase 1+2+3 (US1): one-scan onboarding with the full mandatory posture (derived
credential, encryption, display-only render, mint/render/issue audit rows come with T006/T008).
US2 completes the lifecycle preconditions (revocation+audit surfaces), then US3 unblocks the
Android consumer, then US4 convenience. Each phase ends green on both suites before the next.
