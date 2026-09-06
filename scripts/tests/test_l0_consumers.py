# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""
Assertions for the production-vs-test consumer classifier.

WRITTEN BEFORE THE CODE, DELIBERATELY. @gavriella-olamnit 2026-09-06T21:15Z:

    "a verifier written AFTER the artefact it checks tends to read that artefact and agree with it.
     Three of four P1s raised against feature 066 here were one defect - the gate trusted the
     generator it was written to check. Write the assertions against a NON-EXISTENT artefact and
     watch them fail first."

So these ran and failed with ImportError before `classify_project` existed. That failure is the
evidence that they are testing something, and it is the only reason to trust them afterwards.

THE DEFECT BEING CLOSED
    `l0-consumers.py` classified any hit under a directory containing a .csproj as a CONSUMER.
    A test project has a .csproj. So a seam whose ONLY callers are its own unit tests reported
    "CONSUMED", which is precisely the state gavriella-olamnit measured and named:

    "A seam is verified by its own unit tests, which construct their own consumer. So a seam with
     zero PRODUCTION consumers is indistinguishable, on every dashboard the fleet owns, from a
     seam that is fully wired."

    This lane's instrument had that defect and this lane nearly published a refutation on its
    output. Test-only closure is now a distinct verdict, not a pass.
"""

from __future__ import annotations

import importlib.util
import os
import sys
import tempfile
import unittest

_HERE = os.path.dirname(os.path.abspath(__file__))
_TARGET = os.path.join(os.path.dirname(_HERE), "l0-consumers.py")

_spec = importlib.util.spec_from_file_location("l0_consumers", _TARGET)
_mod = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_mod)


def _project(root: str, name: str, body: str) -> str:
    """Create <root>/<name>/<name>.csproj with `body`, and return the directory."""
    d = os.path.join(root, name)
    os.makedirs(d, exist_ok=True)
    with open(os.path.join(d, f"{name}.csproj"), "w", encoding="utf-8") as handle:
        handle.write(body)
    return d


_PROD = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup></PropertyGroup></Project>"
_TEST = ("<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>"
         "<PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.0.0\" />"
         "<PackageReference Include=\"xunit\" Version=\"2.4.2\" />"
         "</ItemGroup></Project>")


class ClassifyProject(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = self._tmp.name
        self.addCleanup(self._tmp.cleanup)

    def test_production_project_is_production(self):
        d = _project(self.root, "Olamnit.Kernel", _PROD)
        self.assertEqual(_mod.classify_project(os.path.join(d, "DurableQF.cs")), "production")

    def test_test_sdk_reference_makes_it_a_test_project(self):
        """The strong signal: the csproj itself pulls a test SDK. Naming is not required."""
        d = _project(self.root, "Olamnit.Kernel.Verification", _TEST)
        self.assertEqual(_mod.classify_project(os.path.join(d, "Stage2KernelTests.cs")), "test")

    def test_tests_suffix_makes_it_a_test_project_even_without_an_sdk_reference(self):
        """A project named *.Tests with a bare csproj must not be counted as production."""
        d = _project(self.root, "Olamnit.Shared.Tests", _PROD)
        self.assertEqual(_mod.classify_project(os.path.join(d, "RedactionTests.cs")), "test")

    def test_a_file_under_no_csproj_is_unbuildable(self):
        """A source projection contains no .csproj by construction; it can never close a seam."""
        d = os.path.join(self.root, "l0", "kernel")
        os.makedirs(d, exist_ok=True)
        self.assertEqual(_mod.classify_project(os.path.join(d, "Hooks.cs")), "unbuildable")

    def test_nearest_project_wins_not_the_outermost(self):
        """A test project nested inside a production tree must classify as test, not production."""
        outer = _project(self.root, "Olamnit", _PROD)
        inner = _project(outer, "Olamnit.Tests", _TEST)
        self.assertEqual(_mod.classify_project(os.path.join(inner, "T.cs")), "test")
        self.assertEqual(_mod.classify_project(os.path.join(outer, "P.cs")), "production")


class Verdict(unittest.TestCase):
    """The verdict must treat test-only closure as NOT closed - the whole point of the change."""

    def test_production_consumer_closes_the_seam(self):
        self.assertEqual(_mod.verdict(production=2, test=0), "CONSUMED")

    def test_test_only_is_not_closure(self):
        self.assertEqual(_mod.verdict(production=0, test=5), "TEST-ONLY")

    def test_no_consumer_anywhere_is_zero(self):
        self.assertEqual(_mod.verdict(production=0, test=0), "ZERO")

    def test_a_single_production_consumer_outweighs_many_tests(self):
        self.assertEqual(_mod.verdict(production=1, test=99), "CONSUMED")


if __name__ == "__main__":
    unittest.main(verbosity=2)
