---
path: test_archive/select_dispatch_test.dart
name: select_dispatch_test.dart
purpose: Unit tests that the compiler emits a _select/1 dispatch-table label exactly when a module declares exported procedures.
key_idea: 'Compiles four module sources and asserts presence/absence of the _select/1 label: absent for a no-export module and an imported-only module; present for one export and for multiple exports.'
dependencies:
- lib/compiler/compiler.dart
callers: []
mtime: '2026-05-21T12:38:16.840Z'
sha256: 0850eb60ea5b08019cf1cf83493ba567c5582dedce4ab0e570effe622777b80e
target_path: test_archive/select_dispatch_test.cs
purpose_source: inferred
key_idea_source: inferred
---

Unit tests that the compiler emits a _select/1 dispatch-table label exactly when a module declares exported procedures.
