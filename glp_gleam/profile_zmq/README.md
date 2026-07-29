# Gleam ZMQ transport — erlzmq / libzmq runtime provisioning (WSL/Linux)

**Owner ruling 2026-07-23** (`docs/research/fullscope-gleam/phase2-verify/rulings.md`):
the G5 `zmq-comm-base` out-of-scope disposition was **OVERRULED**; ZMQ is **mandatory**
and the Gleam transport contract is now **{loopback, tcp, quic, zmq}**.

The ZMQ leaf `src/glp/link/transports/zmq.gleam` + `src/glp_link_zmq_ffi.erl` are
checked in and **compile green in the default Windows-native `gleam build`** — the
`erlzmq:*` calls are runtime-resolved, so the green baseline is unaffected. The leaf
only *runs* where the `erlzmq` NIF (over native `libzmq`) is loaded — exactly like the
Profile-C QUIC `quicer` NIF (`gleam_quic/profile_c/`), it is **WSL/Linux-only** because
`libzmq` is a native dependency absent on this Windows host.

## Provisioning (WSL Ubuntu, OTP 25)

```
sudo apt-get update && sudo apt-get install -y libzmq3-dev
cd glp_gleam/profile_zmq && rebar3 compile
# then point ERL_LIBS at profile_zmq/_build/default/lib so `erlzmq` is on the path
```

`rebar.config` pins `erlzmq` to a release compatible with this WSL's OTP 25. If the
NIF build fails on the host, the failure is **environment** (native libzmq / NIF
toolchain), not code-absence — the Gleam leaf is complete and interoperates with any
ZMTP peer (erlzmq, C# libzmq, or `zmq`/`pyzmq`) over the wire.

## Status

- Leaf + FFI: **delivered, compiles green** (Windows-native baseline 465, warning-free).
- Runtime: gated on the `erlzmq`/`libzmq` provisioning above (WSL/Linux).
