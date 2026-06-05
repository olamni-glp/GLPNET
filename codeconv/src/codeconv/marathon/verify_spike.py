"""FR-011 first-implementation verification spike (US4, SC-008).

Verifies — before the marathon relies on it — that the chosen resume
substrate delivers cached-prefix resume (unchanged prefix → cached;
execution resumes at the first changed/new step) and observable
``spent``/``remaining`` budget, and records the result durably as a
``verification_traces`` row (``subject=workflow-spike``). Implemented in
US4 (T014).
"""

from __future__ import annotations
