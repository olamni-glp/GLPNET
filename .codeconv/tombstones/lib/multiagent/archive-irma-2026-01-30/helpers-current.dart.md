---
path: lib/multiagent/archive-irma-2026-01-30/helpers-current.dart
name: helpers-current.dart
purpose: "Helper Routines for irmaGLP Transactions\n\nImplements abandon, request, export, and reactivate helpers\nas specified in irmaGLP-spec.md Section 4.\n\nImplementation notes:\n- abandon() takes READER as parameter (not variable)\n- Only readers can be abandoned\n- export() creates relay via RelaySetup (callback-based)\n  Implements: export_reader(Y?, Z) :- Z = Y?.\n"
key_idea: "Helper Routines for irmaGLP Transactions\n\nImplements abandon, request, export, and reactivate helpers\nas specified in irmaGLP-spec.md Section 4.\n\nImplementation notes:\n- abandon() takes READER as parameter (not variable)\n- Only readers can be abandoned\n- export() creates relay via RelaySetup (callback-based)\n  Implements: export_reader(Y?, Z) :- Z = Y?.\n"
dependencies:
- lib/multiagent/message_queue.dart
- lib/multiagent/payload_serializer.dart
- lib/multiagent/variable_table.dart
- lib/runtime/machine_state.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:13.820Z'
sha256: 4699c9a2961396743f442ef6e1e2aeccbc95790e9b0b1f08a9af8b24e34f8328
target_path: lib/multiagent/archive-irma-2026-01-30/helpers-current.cs
---

Helper Routines for irmaGLP Transactions

Implements abandon, request, export, and reactivate helpers
as specified in irmaGLP-spec.md Section 4.

Implementation notes:
- abandon() takes READER as parameter (not variable)
- Only readers can be abandoned
- export() creates relay via RelaySetup (callback-based)
  Implements: export_reader(Y?, Z) :- Z = Y?.
