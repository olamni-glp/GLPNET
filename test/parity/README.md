# test/parity — two parity toolsets share this directory

1. **Codec parity vectors — crdtmsg-mvp** (below, original README).
2. **Feature 050 corpus parity** (Full-Gleam instance): `corpus-manifest.md` (pinned case
   list), `goldens/` (recorded Dart reference outcomes — the ONE recording location),
   `record_dart_goldens.sh`, `run_gleam_corpus.sh`, `run_differential.sh` (MISS-04),
   `lib/normalize.sh` (shared normalization). See
   `specs/050-full-gleam-combined/contracts/corpus-parity.md`.

---

# Codec parity vectors — crdtmsg-mvp (T004 / T055)

Gleam/Dart codec parity vectors that share the **same golden corpus** as
`csharp/glp_crdtmsg.tests/goldens/` (single truth runtime, 038 discipline). A Gleam or Dart
decoder run against these vectors must reproduce the C# bytes exactly.

## Truth runtime & the vectors

**C# is the truth runtime** for the MVP (spec Clarification #1). The parity vectors are the
**canonical binary encodings** (`MessageCodec.Canonical`, i.e. the `BinaryTermCodec` output) of the
sample corpus in `csharp/glp_crdtmsg.tests/SampleMessages.cs`:

| Vector | Shape |
|--------|-------|
| `minimal`   | empty policy, no sections, `crdt_model = none` |
| `rich`      | populated policy, 3 sections (incl. a full-byte-range section + a greasing section), `op_based` |
| `v2-no-cap` | `schema_version = 2`, non-ASCII UTF-8 header fields, greasing section, `state_based` |

The C# side already proves that all four surfaces (binary/JSON/YAML/CBOR) decode-encode to the
identical canonical form (SC-001, the 16-cell conformance matrix — 48 cells green including these
vectors). A Gleam/Dart port validates by decoding each surface's bytes and re-deriving the same
canonical binary.

## Status

- **C# self-parity (all four surfaces agree): GREEN** — `ConformanceMatrixTests`.
- **Cross-runtime Gleam/Dart decode-against-goldens: host-blocked** (same environment constraint as
  036 Profile C / two-host; see `docs/known-issues.md` § Feature 041). Not code-blocked — the vectors
  are defined and stable; the Gleam/Dart run is the only pending step.
