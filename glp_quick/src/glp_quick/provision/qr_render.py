"""Display-only QR rendering (067 FR-005).

Renders each chunk to the terminal as Unicode/ANSI blocks via ``segno``. There is deliberately
**no function here that writes an image file** — FR-005's "no secret-bearing image is persisted"
holds by construction rather than by remembering to clean up.
"""

from __future__ import annotations

import io
from typing import Iterable

import segno

#: QR error-correction level. "m" (~15% recovery) balances density against camera robustness at
#: display distance; the chunk sizing in bundle.py assumes it.
ERROR_LEVEL = "m"


def chunk_to_terminal(line: str, border: int = 2) -> str:
    """Render one chunk line as a terminal QR code (string, not written anywhere)."""
    qr = segno.make(line, error=ERROR_LEVEL)
    buf = io.StringIO()
    qr.terminal(out=buf, border=border)
    return buf.getvalue()


def render_page(
    chunks: Iterable[str],
    *,
    session_id: str,
    device_label: str,
    passphrase: str,
    expires_at: str,
) -> str:
    """Compose the hub display page: QR codes + the one-time passphrase shown once.

    The returned string is for immediate display. Callers must not write it to a file — it
    carries the passphrase, and pairing it with the codes on disk would defeat the out-of-band
    channel that FR-004 depends on.
    """
    chunks = list(chunks)
    out = [
        "",
        "=" * 72,
        f"  GLP-QUICK PROVISIONING SESSION {session_id}   device: {device_label}",
        f"  expires: {expires_at}   chunks: {len(chunks)}",
        "=" * 72,
        "",
        "  Scan every code below, in any order. Then run on the joining host:",
        "      glp-quick provision join --input <chunks.txt>",
        "",
    ]
    for idx, line in enumerate(chunks, start=1):
        out.append(f"  --- chunk {idx}/{len(chunks)} ---")
        out.append(chunk_to_terminal(line))
    out += [
        "",
        "-" * 72,
        f"  ONE-TIME PASSPHRASE (say it aloud / send out-of-band — NOT with the codes):",
        "",
        f"      {passphrase}",
        "",
        "  It is shown once and is not stored anywhere. Without it the codes are useless.",
        "-" * 72,
        "",
    ]
    return "\n".join(out)
