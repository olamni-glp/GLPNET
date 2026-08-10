// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// ResultsWriter (feature 069, T006 + T018) — render reviewable parity/fuzz results to RESULTS.md so the
// whole result set is auditable without re-running the harness (FR-009, SC-006, contract P5).
//
// Sections are keyed by their `## <title>` heading and UPSERTED: a run replaces its own section and
// leaves the others intact, so the SC-001 representative-corpus table, an SC-002 expanded-corpus table,
// and the SC-003 bounded-fuzz summary coexist in one file (T018 — earlier this method overwrote the
// whole file, which clobbered the SC-001 section on any --corpus/--fuzz run).

using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GlpGrammarSpike.Parity
{
    public static class ResultsWriter
    {
        // Per-input parity table (--parity / --parity --corpus).
        public static void Write(string resultsPath, string title, IReadOnlyList<ParityResult> results, string toolchain)
        {
            var sb = new StringBuilder();
            sb.AppendLine("| # | input | verdict | first-diff | cause |");
            sb.AppendLine("|---|-------|---------|-----------|-------|");
            int i = 0, match = 0, unCaused = 0;
            foreach (var r in results)
            {
                i++;
                if (r.IsMatch) match++;
                if (!r.IsMatch && string.IsNullOrEmpty(r.Cause)) unCaused++;
                string diff = r.FirstDiffOffset >= 0 ? ("@" + r.FirstDiffOffset) : "";
                sb.AppendLine("| " + i + " | " + r.InputId + " | " + (r.IsMatch ? "MATCH" : "DIVERGE") + " | " + diff + " | " + Escape(r.Cause) + " |");
            }
            sb.AppendLine();
            sb.AppendLine("**Totals**: " + match + "/" + results.Count + " MATCH. Un-caused divergences (defects — FR-008): " + unCaused + ".");
            Upsert(resultsPath, title, sb.ToString(), toolchain);
        }

        // Bounded-fuzz summary (--fuzz).
        public static void WriteFuzz(string resultsPath, string title, int budget, int matched, int bothReject,
            int divergeIndex, ParityResult diverge, uint seed, string reproPath, string toolchain)
        {
            var sb = new StringBuilder();
            sb.AppendLine("**Seed**: " + seed + " · **Budget**: " + budget + " (deterministic: source = f(index, seed) — F3).");
            sb.AppendLine();
            sb.AppendLine("Targets (contract F1): variable-versus-comparison dispatch (IL parity) + deep type-alternative "
                + "nesting (parse-acceptance parity). Operators are always space-separated, which excludes the "
                + "documented glued-minus lexer corner; only infix `mod` is generated (the `mod(...)` call form is the "
                + "§1.14-gated T016 case).");
            sb.AppendLine();
            if (diverge == null)
            {
                sb.AppendLine("**Verdict**: PASS — full budget completed with **0 un-caused IL divergences** (SC-003 / F2).");
                sb.AppendLine();
                sb.AppendLine("- valid IL-parity MATCH: " + matched);
                sb.AppendLine("- both-reject (invalid generated program — not a divergence): " + bothReject);
            }
            else
            {
                sb.AppendLine("**Verdict**: HALT — un-caused divergence at index " + divergeIndex + " (FR-008 / F4).");
                sb.AppendLine();
                string off = diverge.FirstDiffOffset >= 0 ? (" (first-diff @" + diverge.FirstDiffOffset + ")") : "";
                sb.AppendLine("- cause: " + Escape(diverge.Cause) + off);
                if (!string.IsNullOrEmpty(reproPath)) sb.AppendLine("- repro: `" + reproPath + "`");
                sb.AppendLine("- valid IL-parity MATCH before halt: " + matched + "; both-reject: " + bothReject);
            }
            Upsert(resultsPath, title, sb.ToString(), toolchain);
        }

        // ── section upsert ────────────────────────────────────────────────────────
        private static void Upsert(string resultsPath, string title, string body, string toolchain)
        {
            var sections = new List<KeyValuePair<string, string>>();
            if (File.Exists(resultsPath))
            {
                string curTitle = null;
                var cur = new StringBuilder();
                foreach (var line in File.ReadAllLines(resultsPath))
                {
                    if (line.StartsWith("## "))
                    {
                        if (curTitle != null) sections.Add(new KeyValuePair<string, string>(curTitle, cur.ToString()));
                        cur.Clear();
                        curTitle = line.Substring(3).Trim();
                    }
                    else if (curTitle != null)
                    {
                        cur.AppendLine(line);
                    }
                }
                if (curTitle != null) sections.Add(new KeyValuePair<string, string>(curTitle, cur.ToString()));
            }

            int found = -1;
            for (int i = 0; i < sections.Count; i++) if (sections[i].Key == title) { found = i; break; }
            var entry = new KeyValuePair<string, string>(title, body);
            if (found >= 0) sections[found] = entry; else sections.Add(entry);

            var sb = new StringBuilder();
            sb.Append(BuildHeader(toolchain));
            foreach (var s in sections)
            {
                sb.AppendLine("## " + s.Key);
                sb.AppendLine();
                sb.AppendLine(s.Value.Trim('\r', '\n'));
                sb.AppendLine();
            }
            File.WriteAllText(resultsPath, sb.ToString());
        }

        private static string BuildHeader(string toolchain)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!--");
            sb.AppendLine("SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK");
            sb.AppendLine("SPDX-License-Identifier: MIT");
            sb.AppendLine("-->");
            sb.AppendLine();
            sb.AppendLine("# IL-parity results — SC-002 IL-parity bridge (feature 069)");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(toolchain)) { sb.AppendLine("**Toolchain**: " + toolchain); sb.AppendLine(); }
            return sb.ToString();
        }

        private static string Escape(string s)
            => s == null ? "" : s.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }
}
