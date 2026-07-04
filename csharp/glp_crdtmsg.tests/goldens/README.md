# Golden corpus — crdtmsg-mvp

Golden vectors for the conformance matrix (SC-001), loud-fail fuzz (SC-002), signature
transcode-survival (SC-011), and rich-text convergence (SC-012/013). Authored from ONE truth
runtime's output (038 golden discipline) and shared with `test/parity/` for Gleam/Dart parity.

Populated by the US1+ implementation tasks (T010, T036, T055). This file also ensures the
`goldens/` directory exists so the csproj `CopyToOutputDirectory` glob has a target.
