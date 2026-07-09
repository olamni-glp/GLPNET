"""Side-process stand-in for the T028 / FR-015 #7 regression (feature 049).

Emits, on stdout, exactly what the real glp_quick_host would: one control line (no ``{`` prefix)
and one >1 MiB data envelope line. Binary writes — no platform CRLF translation.
"""
import sys

sys.stdout.buffer.write(b"READY test\n")
sys.stdout.buffer.write(b"{" + b"x" * 2097152 + b"}\n")  # 2 MiB body > the relay's 1 MiB line buffer
sys.stdout.buffer.flush()
