# `glp_quick` tests — two-tier layout (feature 040)

The suite has **two tiers**, so a machine without the C# host can still run everything that does not
need a real QUIC endpoint:

| Tier | Location | Runs when | What it covers |
|---|---|---|---|
| **Host-free unit** | `tests/unit/` | **always** | The terminal's own behaviours (protocol codec, `@name` routing, page/state model, no-TTY fallback, receive thread-safety, link-drop surfacing, `/rcopy` filter/WAL/catalog/quota, joint/forms/keys/presentation) against in-memory doubles — no host, no socket (FR-045/SC-013). |
| **Host-gated integration** | `tests/integration/` | only when `csharp/glp_quick_host` is **built** | The real QUIC+WS mesh: page-transmit, `@name` delivery, `/rcopy` end-to-end (incl. WAL-loss recreate), REPL-page. |

Top-level `tests/*.py` (`test_mesh.py`, `test_cert.py`, …) are the feature-036 tests and stay in place.

## Gating

Every module under `tests/integration/` guards with the same idiom as `tests/test_mesh.py`:

```python
from glp_quick.stacks.csharp import host_dll_path

pytestmark = pytest.mark.skipif(
    not host_dll_path().exists(),
    reason="glp_quick_host.dll not built (run: dotnet build csharp/glp_quick_host)",
)
```

so the integration tier **skips cleanly** when the host dll is absent. The host-free unit tier must
**always** be green.

## Test doubles

`tests/_fakes.py` provides `FakeHandle` — an in-memory `stacks.base.Handle` (send capture, injected
inbound, injectable peer set, explicit close/fault injection). Import it as `from tests._fakes import
FakeHandle`.

## Running

From `glp_quick/`:

```
python -m pytest              # whole suite (unit + integration-if-built)
python -m pytest tests/unit   # host-free tier only
```

The `pythonpath = ["src", "."]` entry in `pyproject.toml`'s `[tool.pytest.ini_options]` puts both the
`glp_quick` package (`src/`) and the `tests` package (`.`) on `sys.path`, so no manual `PYTHONPATH` is
needed.
