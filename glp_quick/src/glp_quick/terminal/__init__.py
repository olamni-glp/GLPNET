"""Host-free, unit-testable terminal behaviours for the virtual 3270 terminal (feature 040).

The ``--tui`` view (``glp_quick.tui``) and the plain fallback (``glp_quick.link_console``) are thin
wirings over these modules so both share one model and cannot drift (FR-026):

- :mod:`glp_quick.terminal.protocol` — the ``tmsg(...)`` ground-term codec (single encode/decode point).
- :mod:`glp_quick.terminal.state`    — shared terminal state + loop-serialized receive-path mutation (R4).
- :mod:`glp_quick.terminal.routing`  — ``@name`` resolution against the authenticated peer set (R3).

Nothing here imports prompt_toolkit or opens a link: every entity is a plain Python value object so
each is testable without a host (FR-045/SC-013). Later user stories add ``pages``/``presentation``/
``keys``/``joint``/``forms``/``replpage`` alongside these.
"""
