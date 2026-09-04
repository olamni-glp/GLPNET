#!/usr/bin/env bash
# SPDX-License-Identifier: MIT
#
# Provision the Linux native QUIC libraries for ynet_transport's fallback chain.
#
#   tier 1  libmsquic.so.2            (Microsoft feed — NOT in Ubuntu apt, NOT bundled with .NET)
#   tier 2  libngtcp2 + crypto_ossl   (Ubuntu universe — the distro-native ultimate fallback)
#
# WHY THIS EXISTS RATHER THAN A RUNBOOK STEP
#   Measured on shiras 2026-09-04: the same binary reported QuicListener.IsSupported=False on the
#   default loader path and True only under LD_LIBRARY_PATH=$HOME/.local/lib. A systemd unit does
#   not inherit an interactive shell's LD_LIBRARY_PATH, so that env var greens the tests and leaves
#   every service deaf. This script installs into a directory the LOADER finds on its own, and can
#   stage the libraries beside a build output so they travel with it.
#
# ROOTLESS BY DEFAULT
#   `apt-get download` needs no elevation, so the ngtcp2 tier provisions with no sudo at all.
#
# Usage:
#   scripts/provision-quic-native-linux.sh [--stage <publish-dir>] [--check]
#
#     --stage <dir>   also copy the libraries into <dir>/runtimes/<rid>/native/ (rid derived from
#                     uname -m), which QuicNativeLoader probes first — no environment variable needed.
#     --check         probe only; install nothing. Exit 0 iff ngtcp2 is loadable from ANY location
#                     the runtime loader searches: $YNET_QUIC_LIBDIR, the --stage dir (if given),
#                     the system loader (apt install / ldconfig).

set -euo pipefail

LIBDIR="${YNET_QUIC_LIBDIR:-$HOME/.local/lib/ynet-quic}"
# The RID must match what QuicNativeLoader.RuntimeIdentifier() computes for THIS process, or a
# staged library is invisible to the service (linux-x64 was hard-coded once; ARM64 hosts got nothing).
case "$(uname -m)" in
  x86_64)        RID=linux-x64 ;;
  aarch64|arm64) RID=linux-arm64 ;;
  i?86)          RID=linux-x86 ;;
  *)             RID="linux-$(uname -m)" ;;
esac
NGTCP2_PACKAGES=(libngtcp2-16 libngtcp2-crypto-ossl0 libnghttp3-9)
STAGE_DIR=""
CHECK_ONLY=0

while [ $# -gt 0 ]; do
  case "$1" in
    --stage) STAGE_DIR="${2:?--stage needs a directory}"; shift 2 ;;
    --check) CHECK_ONLY=1; shift ;;
    -h|--help) sed -n '3,26p' "$0"; exit 0 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

probe_ngtcp2() {
  # ORDER IS LOAD-BEARING: libngtcp2_crypto_ossl.so.0 carries DT_NEEDED libngtcp2.so.16, and this
  # directory is not on the loader search path. Loading the engine FIRST puts it in the link map,
  # which then satisfies the crypto library's dependency by soname. Probing them in separate
  # processes reports a false "crypto missing" — verified on shiras 2026-09-04.
  # Mirror the runtime's search locations: $LIBDIR, the staged RID dir, then the system loader
  # ("" = ask ld.so by soname). The first location where BOTH libraries load wins.
  local locations=("$LIBDIR")
  [ -n "$STAGE_DIR" ] && locations+=("$STAGE_DIR/runtimes/$RID/native")
  locations+=("")
  python3 - "${locations[@]}" <<'PYPROBE'
import ctypes, os, sys
ENGINE = ("libngtcp2.so.16", ["ngtcp2_version", "ngtcp2_conn_client_new_versioned",
                              "ngtcp2_conn_server_new_versioned", "ngtcp2_conn_read_pkt_versioned",
                              "ngtcp2_conn_writev_stream_versioned"])
CRYPTO = ("libngtcp2_crypto_ossl.so.0", ["ngtcp2_crypto_ossl_init", "ngtcp2_crypto_ossl_ctx_new",
                                         "ngtcp2_crypto_ossl_configure_client_session"])

def load(d, name, syms):
    path = os.path.join(d, name) if d else name
    try:
        h = ctypes.CDLL(path)
    except OSError as e:
        print(f"    {name}: LOAD FAILED ({e})"); return False
    missing = [s for s in syms if not hasattr(h, s)]
    if missing:
        print(f"    {name}: loaded but MISSING {', '.join(missing)} (ABI mismatch)"); return False
    print(f"    {name}: OK ({len(syms)} required exports present)"); return True

for d in sys.argv[1:]:
    print(f"  location: {d or '(system loader)'}")
    if load(d, *ENGINE) and load(d, *CRYPTO):
        sys.exit(0)
print("  ngtcp2 not loadable from any runtime search location")
sys.exit(1)
PYPROBE
}

probe_msquic() {
  for candidate in "$LIBDIR/libmsquic.so.2" "$HOME/.local/lib/libmsquic.so.2"; do
    if python3 -c "
import ctypes,sys
try: h=ctypes.CDLL('$candidate')
except OSError: sys.exit(1)
sys.exit(0 if hasattr(h,'MsQuicOpenVersion') else 1)" 2>/dev/null; then
      echo "  $candidate: OK"; return 0
    fi
  done
  echo "  libmsquic.so.2: absent — tier 1 unavailable on this host (expected: it is not in apt)"
  return 1
}

check() {
  local rc=0
  echo "== ngtcp2 (tier 2, distro-native ultimate fallback) =="
  probe_ngtcp2 || rc=1
  echo "== msquic (tier 1) =="
  probe_msquic || true   # tier 1 absence is not a provisioning failure; tier 2 IS the fallback
  return $rc
}

if [ "$CHECK_ONLY" = 1 ]; then
  check
  exit $?
fi

mkdir -p "$LIBDIR"
workdir="$(mktemp -d)"
trap 'rm -rf "$workdir"' EXIT

echo "fetching ${NGTCP2_PACKAGES[*]} (no elevation required) ..."
( cd "$workdir" && for p in "${NGTCP2_PACKAGES[@]}"; do apt-get download "$p" >/dev/null; done )
for deb in "$workdir"/*.deb; do dpkg-deb -x "$deb" "$workdir/root"; done

# Copy the real files and re-create the soname symlinks the loader resolves by.
find "$workdir/root" -name '*.so.*' -type f -exec cp -f {} "$LIBDIR/" \;
( cd "$LIBDIR"
  for real in libngtcp2.so.*.*.* libngtcp2_crypto_ossl.so.*.*.* libnghttp3.so.*.*.*; do
    [ -e "$real" ] || continue
    soname="$(echo "$real" | sed -E 's/\.so\.([0-9]+)\..*/.so.\1/')"
    ln -sf "$real" "$soname"
  done )

echo "installed into $LIBDIR:"
ls -la "$LIBDIR" | sed 's/^/  /'

if [ -n "$STAGE_DIR" ]; then
  native="$STAGE_DIR/runtimes/$RID/native"
  mkdir -p "$native"
  cp -a "$LIBDIR"/lib*.so.* "$native/"
  # msquic, if the host has it, travels the same way so the service needs no env var.
  [ -e "$HOME/.local/lib/libmsquic.so.2" ] && cp -aL "$HOME/.local/lib/libmsquic.so.2" "$native/" || true
  echo "staged beside build output: $native"
fi

echo
check
