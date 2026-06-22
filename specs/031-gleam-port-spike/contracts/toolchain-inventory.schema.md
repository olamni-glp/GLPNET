# Contract: Toolchain Inventory Schema

**Artifact**: `docs/research/gleam-atomvm/toolchain-inventory.md` (entity **E5**).

## Required fields

| Field | Form | Requirement |
|---|---|---|
| `gleam_version` | exact string (`gleam --version` output) | FR-003 — exact, not "latest" |
| `erlang_otp_version` | exact OTP / `erl +V` string | FR-003 |
| `atomvm_build` | prebuilt release tag **or** source-build commit/ref, **or** the recorded bring-up blocker | FR-003, FR-005 |
| `build_tooling` | `rebar3` + Gleam build-tool versions; JS toolchain (node) if exercised | FR-003 |
| `environment` | OS + arch actually verified on: `Windows` \| `WSL/Linux` \| `Mac`, including any fallback used | FR-003, research R1; Edge case |
| `setup_commands` | reproducible install/setup block | FR-003, SC-002 |
| `build_commands` | reproducible build block (BEAM; JS if applicable) | FR-003, SC-002 |
| `run_commands` | reproducible run block (Erlang; AtomVM attempt) | FR-003, SC-002 |

## Invariants

- **Exactness**: every version field is the observed exact version; the literal word "latest" is not an acceptable value. *(FR-003)*
- **Environment honesty**: if Windows-native failed and a fallback (WSL/Linux or Mac) was used, the inventory records *that*, as a first-class finding for downstream features. *(Edge case; FR-007)*
- **Reproducibility**: the setup+build+run blocks, followed on a clean checkout, reproduce the smoke's observed result for a second person. *(SC-002)*
- **Citations allowed** only where a value is documentary (e.g., a documented AtomVM subset limit); anything claimed as "works here" is command+observed-output. *(FR-009)*
