---
path: lib/multiagent/payload_serializer.dart
name: payload_serializer.dart
purpose: 'Payload Serialization for irmaGLP


  Serializes terms and messages to bytes for inter-agent transport.

  Uses global variable IDs (creator:localId) for cross-agent routing.


  Specification: /docs/ma/irmaGLP-spec.md Section 6 and 8.3

  '
key_idea: 'Payload Serialization for irmaGLP


  Serializes terms and messages to bytes for inter-agent transport.

  Uses global variable IDs (creator:localId) for cross-agent routing.


  Specification: /docs/ma/irmaGLP-spec.md Section 6 and 8.3

  '
dependencies:
- lib/multiagent/mad_helpers.dart
- lib/multiagent/message_queue.dart
- lib/runtime/terms.dart
callers:
- lib/multiagent/agent_runtime.dart
- lib/multiagent/isolate_manager.dart
- lib/multiagent/mad_context.dart
mtime: '2026-04-27T09:23:50.000Z'
sha256: 6291cb396efe81564618f2dd1e207ebda0a7fd3e01e918356a0e2f62282655e0
---

Payload Serialization for irmaGLP

Serializes terms and messages to bytes for inter-agent transport.
Uses global variable IDs (creator:localId) for cross-agent routing.

Specification: /docs/ma/irmaGLP-spec.md Section 6 and 8.3
