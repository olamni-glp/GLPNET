---
path: lib/multiagent/archive-irma-2026-01-30/irma_context.dart
name: irma_context.dart
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
- lib/multiagent/helpers.dart
- lib/multiagent/message_queue.dart
- lib/multiagent/payload_serializer.dart
- lib/multiagent/variable_table.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/suspension.dart
- lib/runtime/terms.dart
callers:
- lib/multiagent/archive-irma-2026-01-30/irma_agent.dart
- lib/multiagent/archive-irma-2026-01-30/mad_agent.dart
mtime: '2026-05-21T12:38:13.913Z'
sha256: e4276d9672760688ceaea82fe840494edcccf6c27f17f1c192edeb095cf07736
target_path: lib/multiagent/archive-irma-2026-01-30/irma_context.cs
purpose_source: doc
key_idea_source: doc
---

irmaGLP Agent Context

Extends GLP runtime with V_p (Variable Table) and M_p (Message Queue)
for multiagent communication.

Specification: /docs/ma/irmaGLP-spec.md

Integration approach: Uses heap onBind callbacks to observe variable bindings.
When a writer in V_p is bound, the callback queues assignment messages.
This decouples the GLP runtime from network transport.
