"""Host-gated mesh-integration tier — skipped when ``csharp/glp_quick_host`` is not built.

Each module guards with ``pytest.mark.skipif(not host_dll_path().exists(), ...)`` (matches
``tests/test_mesh.py``), so the suite is green on a host without the C# dll while still exercising the
real QUIC+WS mesh where it is built.
"""
