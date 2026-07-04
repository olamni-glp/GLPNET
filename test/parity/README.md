# Codec parity vectors — crdtmsg-mvp (T004)

Gleam/Dart codec parity vectors that share the **same golden corpus** as
`csharp/glp_crdtmsg.tests/goldens/` (single truth runtime, 038 discipline). A Gleam or Dart
decoder run against these vectors must reproduce the C# bytes exactly (T055).

Populated alongside the US1 conformance matrix (T010) and validated by T055.
