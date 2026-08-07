// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// ResultsWriter (feature 069, T006) — render a reviewable per-input parity table to RESULTS.md so the
// whole result set is auditable without re-running the harness (FR-009, SC-006, contract P5).

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GlpGrammarSpike.Parity
{
    public static class ResultsWriter
    {
        // Write (overwrite) a RESULTS.md section for a completed parity run.
        public static void Write(string resultsPath, string title, IReadOnlyList<ParityResult> results, string toolchain)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!--");
            sb.AppendLine("SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK");
            sb.AppendLine("SPDX-License-Identifier: MIT");
            sb.AppendLine("-->");
            sb.AppendLine();
            sb.AppendLine("# IL-parity results — SC-002 IL-parity bridge (feature 069)");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(toolchain))
            {
                sb.AppendLine("**Toolchain**: " + toolchain);
                sb.AppendLine();
            }
            sb.AppendLine("## " + title);
            sb.AppendLine();
            sb.AppendLine("| # | input | verdict | first-diff | cause |");
            sb.AppendLine("|---|-------|---------|-----------|-------|");
            int i = 0, match = 0;
            foreach (var r in results)
            {
                i++;
                if (r.IsMatch) match++;
                string diff = r.FirstDiffOffset >= 0 ? ("@" + r.FirstDiffOffset) : "";
                string cause = r.Cause == null ? "" : r.Cause.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
                sb.AppendLine("| " + i + " | " + r.InputId + " | " + (r.IsMatch ? "MATCH" : "DIVERGE") + " | " + diff + " | " + cause + " |");
            }
            sb.AppendLine();
            sb.AppendLine("**Totals**: " + match + "/" + results.Count + " MATCH.");
            int unCaused = 0;
            foreach (var r in results) if (!r.IsMatch && string.IsNullOrEmpty(r.Cause)) unCaused++;
            sb.AppendLine();
            sb.AppendLine("Un-caused divergences (defects — FR-008): " + unCaused + ".");

            File.WriteAllText(resultsPath, sb.ToString());
        }
    }
}
