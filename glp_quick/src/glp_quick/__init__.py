"""glp_quick — the single Python control-plane tool behind the ``/GLP-Quick`` skill (feature 036).

One tool hosts both roles (``--server`` / ``--client``) and the LAN-IP conformance demo
(FR-007). It owns the *control plane* only — operator CLI, shared self-signed certificate
generation + out-of-band trust pinning, launch/supervision of the per-stack data-plane
transport runtime (C#/.NET reference, then Gleam), and the GLP-REPL <-> link bridge.

Python is **never** the QUIC endpoint: the genuine QUIC/HTTP-3 handshake + WebSocket link
live in the data-plane stacks (``glp_quick.stacks``), reusing spec 025's link seam, so the
C#-vs-Gleam comparison (FR-009/FR-010) stays meaningful (plan.md "Technical approach").
"""

__version__ = "0.1.0"
