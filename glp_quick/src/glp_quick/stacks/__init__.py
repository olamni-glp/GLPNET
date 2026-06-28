"""Per-stack data-plane adapters behind the uniform ``StackAdapter`` contract (FR-009/FR-010).

``base`` defines the ABC; ``csharp`` is the cross-platform reference (must reach the full
real-QUIC LAN demo first, FR-010); ``gleam`` is the staged second stack in two deployment
profiles (A: AtomVM + native QUIC side-process; C: full BEAM + ``quicer``/MsQuic in-process).
"""
