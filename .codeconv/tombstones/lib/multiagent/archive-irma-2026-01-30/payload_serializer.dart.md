---
path: lib/multiagent/archive-irma-2026-01-30/payload_serializer.dart
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
- lib/multiagent/message_queue.dart
- lib/runtime/terms.dart
callers:
- lib/multiagent/archive-irma-2026-01-30/irma_agent.dart
- lib/multiagent/archive-irma-2026-01-30/mad_agent.dart
mtime: '2026-05-21T12:38:14.021Z'
sha256: be3ca7386ed70c952198ae01a350b1695b095084c5ba50ca1cfc0a3cca119921
target_path: lib/multiagent/archive-irma-2026-01-30/payload_serializer.cs
---

Payload Serialization for irmaGLP

Serializes terms and messages to bytes for inter-agent transport.
Uses global variable IDs (creator:localId) for cross-agent routing.

Specification: /docs/ma/irmaGLP-spec.md Section 6 and 8.3
