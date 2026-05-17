---
path: lib/multiagent/archive-irma-2026-01-30/irma_context-phase4.dart
name: irma_context-phase4.dart
purpose: 'irmaGLP Agent Context


  Extends GLP runtime with V_p (Variable Table) and M_p (Message Queue)

  for multiagent communication.


  Specification: /docs/ma/irmaGLP-spec.md


  Integration approach: Uses heap onBind callbacks to observe variable bindings.

  When a writer in V_p is bound, the callback queues assignment messages.

  This decouples the GLP runtime from network transport.

  '
key_idea: 'irmaGLP Agent Context


  Extends GLP runtime with V_p (Variable Table) and M_p (Message Queue)

  for multiagent communication.


  Specification: /docs/ma/irmaGLP-spec.md


  Integration approach: Uses heap onBind callbacks to observe variable bindings.

  When a writer in V_p is bound, the callback queues assignment messages.

  This decouples the GLP runtime from network transport.

  '
dependencies:
- lib/multiagent/global_send.dart
- lib/multiagent/global_writers_table.dart
- lib/multiagent/helpers.dart
- lib/multiagent/mad_helpers.dart
- lib/multiagent/message_queue.dart
- lib/multiagent/payload_serializer.dart
- lib/multiagent/variable_table.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/suspension.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-17T10:36:35.217Z'
sha256: fd04669c92ea09c63813226bc607f7ea2641f1a17569bc834d8e717be7170cc8
target_path: lib/multiagent/archive-irma-2026-01-30/irma_context-phase4.cs
---

irmaGLP Agent Context

Extends GLP runtime with V_p (Variable Table) and M_p (Message Queue)
for multiagent communication.

Specification: /docs/ma/irmaGLP-spec.md

Integration approach: Uses heap onBind callbacks to observe variable bindings.
When a writer in V_p is bound, the callback queues assignment messages.
This decouples the GLP runtime from network transport.
