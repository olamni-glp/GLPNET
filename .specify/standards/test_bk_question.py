# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT

"""Regression tests for the three BK-STD-2 defects found by codexreview on 2026-09-01.

stdlib only (unittest), single file, no repo imports — same portability contract as
`bk_question.py` itself, so a lane adopts both by copying them.

    python .specify/standards/test_bk_question.py
"""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import bk_question as bkq  # noqa: E402


def _q(**over):
    """A conformant question; `over` replaces fields."""
    q = {
        "qid": "Q-T-01",
        "header": "Test",
        "block": "Work cannot proceed, because the thing is undecided",
        "origin": "measurement",
        "severity": "high",
        "background": ["Line one of evidence.", "Line two of evidence."],
        "impact_if_unanswered": "Nothing moves.",
        "options": [
            {"key": "a", "label": "Do A", "consequence": "A happens.",
             "size": "nano", "reversibility": "reversible"},
            {"key": "b", "label": "Do B", "consequence": "B happens.",
             "size": "mini", "reversibility": "reversible"},
        ],
        "recommendation": {"option": "a", "because": "A is cheaper."},
    }
    q.update(over)
    return q


class NonObjectEntry(unittest.TestCase):
    """Finding 1 — line 66: `[null]` raised AttributeError and exited 1."""

    def test_null_entry_is_reported_not_raised(self):
        problems = bkq.validate([None])
        self.assertTrue(problems)
        self.assertIn("must be an object", problems[0])

    def test_scalar_entry_is_reported(self):
        self.assertTrue(bkq.validate(["not-a-question"]))

    def test_good_entries_alongside_a_bad_one_still_checked(self):
        problems = bkq.validate([None, _q()])
        self.assertEqual(len(problems), 1, problems)

    def test_interactive_skips_non_objects_without_raising(self):
        self.assertEqual(len(bkq.interactive_payload([None, _q()])), 1)


class DecisionValidation(unittest.TestCase):
    """Finding 2 — lines 141-143: any dict passed, and then HID the question."""

    def test_unknown_option_is_rejected(self):
        problems = bkq.validate([_q(decision={
            "option": "typo", "date": "2026-09-01", "rationale": "x"})])
        self.assertTrue(any("names no option" in p for p in problems), problems)

    def test_missing_rationale_is_rejected(self):
        problems = bkq.validate([_q(decision={"option": "a", "date": "2026-09-01"})])
        self.assertTrue(any("rationale" in p for p in problems), problems)

    def test_missing_date_is_rejected(self):
        problems = bkq.validate([_q(decision={"option": "a", "rationale": "x"})])
        self.assertTrue(any("date" in p for p in problems), problems)

    def test_non_iso_date_is_rejected(self):
        problems = bkq.validate([_q(decision={
            "option": "a", "date": "yesterday", "rationale": "x"})])
        self.assertTrue(any("ISO" in p for p in problems), problems)

    def test_valid_decision_passes(self):
        self.assertEqual(bkq.validate([_q(decision={
            "option": "a", "date": "2026-09-01", "rationale": "because"})]), [])

    def test_malformed_decision_does_NOT_hide_the_question(self):
        """The load-bearing regression: a bad decision must not suppress."""
        payload = bkq.interactive_payload([_q(decision={"option": "typo"})])
        self.assertEqual(len(payload), 1,
                         "a malformed decision silently hid an unanswered question")

    def test_valid_decision_does_hide_the_question(self):
        payload = bkq.interactive_payload([_q(decision={
            "option": "a", "date": "2026-09-01", "rationale": "because"})])
        self.assertEqual(payload, [])


class SentenceShape(unittest.TestCase):
    """Finding 3 — lines 77-78: counting periods was wrong in both directions."""

    def test_two_exclamation_sentences_are_rejected(self):
        problems = bkq.validate([_q(block="First is blocked! Second cannot proceed!")])
        self.assertTrue(any("ONE sentence" in p for p in problems), problems)

    def test_one_sentence_with_a_filename_is_accepted(self):
        self.assertEqual(
            bkq.validate([_q(block="The gate refuses, because spec.md is absent")]), [])

    def test_one_sentence_with_a_version_is_accepted(self):
        self.assertEqual(
            bkq.validate([_q(block="The cut is unqualified, because v2026.08.31.1 is the last tag")]), [])

    def test_two_period_sentences_are_still_rejected(self):
        problems = bkq.validate([_q(block="One thing broke. Another thing broke too.")])
        self.assertTrue(any("ONE sentence" in p for p in problems), problems)

    def test_counter_directly(self):
        self.assertEqual(bkq._sentence_count("a b c"), 0)
        self.assertEqual(bkq._sentence_count("a b c."), 1)
        self.assertEqual(bkq._sentence_count("spec.md is here."), 1)
        self.assertEqual(bkq._sentence_count("One! Two!"), 2)


class RoundTrip(unittest.TestCase):
    """`decide` must write a record its own validator accepts."""

    def test_decide_then_validate_is_clean(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "q.json"
            p.write_text(json.dumps([_q()]), encoding="utf-8")
            self.assertEqual(bkq.decide(p, "Q-T-01", "a", "sound reason", None), 0)
            self.assertEqual(bkq.validate(bkq.load(p)), [])
            self.assertEqual(bkq.interactive_payload(bkq.load(p)), [])


if __name__ == "__main__":
    unittest.main(verbosity=2)
