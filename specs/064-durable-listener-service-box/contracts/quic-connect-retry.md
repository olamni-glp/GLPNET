<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract: QUIC connector dial-retry (064 / FR-008, US3)

## Requirement

`QuicTransport.ConnectAsync` MUST retry a refused/absent-listener dial with
back-off until the caller's CancellationToken cancels — exact parity with
`TcpTransport.cs:57-74` ("retry connection-refused until ct … listener not up
yet — back off and retry"). Today's implementation (`QuicTransport.cs:147-175`)
is single-shot with no catch: a connector goal run before the listener arms
fails immediately. This is the C# mirror of the obligation recorded fleet-wide
as D-9 item 2 (link-primitives-port.md).

## Shape

```
while (true) {
    ct.ThrowIfCancellationRequested();
    try { …QuicConnection.ConnectAsync(clientOptions, ct)…; break; }
    catch (<refused/unreachable QUIC connect failure>) {
        await Task.Delay(100, ct);   // listener not up yet — back off and retry
    }
}
```

- Budget: the kernel's existing 120 s connect ct (`LinkSetupKernel.cs:62-63`);
  no new timeout knob.
- Only pre-establishment connect failures retry; post-establishment stream/
  bootstrap failures keep today's fail-fast semantics.
- Exhausted budget keeps today's surface: graceful `Abort`
  ("transport establishment failed for {id}") — never an instant hard failure
  while budget remains (US3 scenario 2).

## Acceptance

- Dial starts before the listener arms; listener arms within budget ⇒ connect
  succeeds, no dialer-side action (US3 scenario 1; SC-003 drill 100%).
- Listener never arms ⇒ failure only at ct exhaustion with existing fault
  reporting (US3 scenario 2).
