"""Non-secret printable hand-off (067 FR-006, US4).

The PDF carries endpoint, trunk SPKI pin, session reference and instructions — **never** a
derived credential, an encrypted payload chunk, or trunk key material.

That is enforced structurally: :func:`render_non_secret_pdf` accepts only the non-secret fields,
so there is no parameter through which a secret could arrive. :func:`assert_non_secret` is the
belt-and-braces check for text assembled from elsewhere, and it *refuses* rather than redacting —
a caller passing secret material to the printable path is a bug in the caller.
"""

from __future__ import annotations

from pathlib import Path

from fpdf import FPDF

from . import PostureError

_FORBIDDEN = ("-----BEGIN", "PRIVATE KEY", "GQP1|")


def assert_non_secret(text: str) -> None:
    """Refuse any string carrying key material or an encrypted payload chunk."""
    for marker in _FORBIDDEN:
        if marker in text:
            raise PostureError(
                f"refusing to render {marker!r} to a printable/persistable artifact — the PDF path "
                f"carries non-secret material only (067 FR-006; printed output is forbidden for "
                f"trunk and secret-bearing material)"
            )


def render_non_secret_pdf(
    out_path: Path,
    *,
    session_id: str,
    device_label: str,
    endpoint_host: str,
    endpoint_port: int,
    trunk_spki_pin: str,
) -> Path:
    """Write the non-secret hand-off page. Returns the path written."""
    for field in (session_id, device_label, endpoint_host, trunk_spki_pin):
        assert_non_secret(str(field))

    pdf = FPDF()
    pdf.add_page()
    pdf.set_font("Helvetica", "B", 16)
    pdf.cell(0, 10, "GLP-Quick provisioning - endpoint details", new_x="LMARGIN", new_y="NEXT")
    pdf.ln(2)

    pdf.set_font("Helvetica", "", 11)
    for label, value in (
        ("Session", session_id),
        ("Device", device_label),
        ("Endpoint", f"{endpoint_host}:{endpoint_port}"),
        ("Trunk SPKI pin", trunk_spki_pin),
    ):
        pdf.cell(38, 8, f"{label}:", new_x="END", new_y="LAST")
        pdf.set_font("Courier", "", 10)
        pdf.multi_cell(0, 8, value, new_x="LMARGIN", new_y="NEXT")
        pdf.set_font("Helvetica", "", 11)

    pdf.ln(4)
    pdf.set_font("Helvetica", "B", 12)
    pdf.cell(0, 8, "How to join", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(
        0,
        6,
        "This sheet carries NO secret material. It is deliberately incomplete: the credential "
        "itself is only ever shown on the hub display page as encrypted QR codes, and the "
        "one-time passphrase travels separately.\n\n"
        "1. Ask the hub operator to open the provisioning session for this device.\n"
        "2. Scan every QR code shown on the hub display, in any order.\n"
        "3. Run: glp-quick provision join --input <chunks.txt>\n"
        "4. Enter the one-time passphrase you were given out-of-band.",
        new_x="LMARGIN",
        new_y="NEXT",
    )

    out_path = Path(out_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    pdf.output(str(out_path))
    return out_path
