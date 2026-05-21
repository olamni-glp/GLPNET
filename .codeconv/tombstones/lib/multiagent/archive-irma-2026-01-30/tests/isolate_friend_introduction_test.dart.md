---
path: lib/multiagent/archive-irma-2026-01-30/tests/isolate_friend_introduction_test.dart
name: isolate_friend_introduction_test.dart
purpose: "Isolate-Based Friend-Mediated Introduction Test\n\nTests the friend-mediated introduction protocol across three Dart isolates.\nBob introduces Alice to Charlie by creating a shared channel.\n\nProtocol (per irmaGLP spec Section 6.2):\n1. Bob creates channel variables: CA (Alice→Charlie writer), CA? (reader)\n                                  AC (Charlie→Alice writer), AC? (reader)\n2. Bob sends ch(AC?, CA) to Alice via cold-call (Alice gets reader from Charlie, writer to Charlie)\n3. Bob sends ch(CA?, AC) to Charlie via cold-call (Charlie gets reader from Alice, writer to Alice)\n4. Alice binds CA = \"hello_charlie\" (sends to Charlie)\n5. Bob routes: receives assignment for CA?, forwards to Charlie\n6. Charlie receives \"hello_charlie\" on CA?\n7. Charlie binds AC = \"hello_alice\" (sends to Alice)\n8. Bob routes: receives assignment for AC?, forwards to Alice\n9. Alice receives \"hello_alice\" on AC?\n\nKey insight: Bob is creator of all channel variables and serves as routing hub.\n"
key_idea: "Isolate-Based Friend-Mediated Introduction Test\n\nTests the friend-mediated introduction protocol across three Dart isolates.\nBob introduces Alice to Charlie by creating a shared channel.\n\nProtocol (per irmaGLP spec Section 6.2):\n1. Bob creates channel variables: CA (Alice→Charlie writer), CA? (reader)\n                                  AC (Charlie→Alice writer), AC? (reader)\n2. Bob sends ch(AC?, CA) to Alice via cold-call (Alice gets reader from Charlie, writer to Charlie)\n3. Bob sends ch(CA?, AC) to Charlie via cold-call (Charlie gets reader from Alice, writer to Alice)\n4. Alice binds CA = \"hello_charlie\" (sends to Charlie)\n5. Bob routes: receives assignment for CA?, forwards to Charlie\n6. Charlie receives \"hello_charlie\" on CA?\n7. Charlie binds AC = \"hello_alice\" (sends to Alice)\n8. Bob routes: receives assignment for AC?, forwards to Alice\n9. Alice receives \"hello_alice\" on AC?\n\nKey insight: Bob is creator of all channel variables and serves as routing hub.\n"
dependencies:
- lib/multiagent/irma_context.dart
- lib/multiagent/message_queue.dart
- lib/multiagent/payload_serializer.dart
- lib/multiagent/variable_table.dart
- lib/runtime/runtime.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:14.465Z'
sha256: 85d2594c2f18a6810f71ecb6533ad092f19761ec3b7c2cfef29c55b227114e11
target_path: lib/multiagent/archive-irma-2026-01-30/tests/isolate_friend_introduction_test.cs
---

Isolate-Based Friend-Mediated Introduction Test

Tests the friend-mediated introduction protocol across three Dart isolates.
Bob introduces Alice to Charlie by creating a shared channel.

Protocol (per irmaGLP spec Section 6.2):
1. Bob creates channel variables: CA (Alice→Charlie writer), CA? (reader)
                                  AC (Charlie→Alice writer), AC? (reader)
2. Bob sends ch(AC?, CA) to Alice via cold-call (Alice gets reader from Charlie, writer to Charlie)
3. Bob sends ch(CA?, AC) to Charlie via cold-call (Charlie gets reader from Alice, writer to Alice)
4. Alice binds CA = "hello_charlie" (sends to Charlie)
5. Bob routes: receives assignment for CA?, forwards to Charlie
6. Charlie receives "hello_charlie" on CA?
7. Charlie binds AC = "hello_alice" (sends to Alice)
8. Bob routes: receives assignment for AC?, forwards to Alice
9. Alice receives "hello_alice" on AC?

Key insight: Bob is creator of all channel variables and serves as routing hub.
