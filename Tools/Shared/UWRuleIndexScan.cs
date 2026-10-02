using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Builds the content of Docs/RULES-INDEX.md: which place of the ORIGINAL each of our
    /// files implements. The tool `Tools/UWRuleIndex` writes it to disk, the self-check runs
    /// the same scan and fails when a place turns up in two rule classes or when the file on
    /// disk no longer matches the sources.
    ///
    /// WHY IT EXISTS. Todo.md says what is still missing; it does not say where a rule already
    /// lives, and that is what one needs before building something new. Twice now the same rule
    /// was written a second time in another class because nobody could see the first one (the
    /// user's rule of 2026-09-21: "so that it does not happen again that the same thing is built
    /// in several places").
    ///
    /// HOW IT WORKS. Our comments name the original's routines and labels the way the
    /// disassembly does - AttackerAppliesFinalDamage, seg007_1798_3383, ovr133_347. The tool
    /// collects those tokens out of the sources and turns them round: one entry per routine,
    /// with the files that cite it. A routine cited from TWO engine-free classes is the signal
    /// for a rule implemented twice; a rules class plus its Unity host is the normal case.
    ///
    /// IT IS GENERATED, never edited, so it cannot go stale like a hand-kept list:
    ///
    ///   dotnet run --project Tools/UWRuleIndex -- [project root]
    /// </summary>
    internal static class UWRuleIndexScan
    {
        /// <summary>
        /// A routine or label of the original: an optional readable name, then the segment
        /// (seg044) or overlay (ovr133) and at least one hexadecimal part. That is exactly how
        /// the disassembly names them, and how our comments quote them.
        /// </summary>
        private static readonly Regex mORoutine = new Regex(
            @"(?<name>[A-Za-z][A-Za-z0-9]*(?:_[A-Za-z0-9]+)*?_)?(?<core>(?:seg|ovr)[0-9]{3}(?:_[0-9A-Fa-f]{1,6})+)",
            RegexOptions.Compiled);

        /// <summary>Where the sources live, relative to the project root.</summary>
        private static readonly string[] msSourceFolders = { "Assets", "Tools", "Core", "Docs" };

        /// <summary>Folders that hold generated or foreign code. UWOriginalCoverage is skipped
        /// as well: it TALKS ABOUT the original, its judgement table names routines it does not
        /// implement, and counting those as implementations would spoil the index.</summary>
        private static readonly string[] msSkipFolders =
        {
            "obj", "bin", "Library", "Temp", "Logs", "StandardAssets", "UWOriginalCoverage"
        };

        /// <summary>One citation.</summary>
        private sealed class Citation
        {
            public string File;

            public int Line;

            public string Token;

            /// <summary>Whether the line only POINTS at the place instead of implementing it -
            /// see fIsReference.</summary>
            public bool IsReference;
        }

        /// <summary>
        /// Words with which our comments point somewhere else. A line that carries one of them
        /// does not implement the place, it refers to whoever does - without that distinction
        /// every well cross-referenced rule looks like a duplicate.
        /// </summary>
        private static readonly string[] msReferenceWords =
        {
            "see ", "see:", "as in ", "like ", "compare ", "cf. ", "same as", "unlike ",
            "agrees with", "counterpart", "described in", "explained in"
        };

        /// <summary>
        /// A citation that only points elsewhere: the line uses one of the reference words, or
        /// the file is an INTERFACE, which by its nature only describes what the host must do.
        /// </summary>
        private static bool fIsReference(string psFile, string psLine)
        {
            string lsName = Path.GetFileName(psFile);

            if (lsName.Length > 1 && lsName[0] == 'I' && char.IsUpper(lsName[1]))
                return true;

            string lsLower = psLine.ToLowerInvariant();

            foreach (string lsWord in msReferenceWords)
            {
                if (lsLower.Contains(lsWord))
                    return true;
            }

            return false;
        }

        /// <summary>The project root from the tool's own position, for a run without arguments.</summary>
        private static string fFindRoot()
        {
            DirectoryInfo lODirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (lODirectory != null)
            {
                if (Directory.Exists(Path.Combine(lODirectory.FullName, "Assets")))
                    return lODirectory.FullName;

                lODirectory = lODirectory.Parent;
            }

            return null;
        }

        /// <summary>Every citation in every source file.</summary>
        private static List<Citation> fCollect(string psRoot)
        {
            List<Citation> lOCitations = new List<Citation>();

            foreach (string lsFolder in msSourceFolders)
            {
                string lsFull = Path.Combine(psRoot, lsFolder);

                if (!Directory.Exists(lsFull))
                    continue;

                foreach (string lsFile in Directory.EnumerateFiles(lsFull, "*.cs", SearchOption.AllDirectories))
                {
                    if (fIsSkipped(lsFile))
                        continue;

                    string lsRelative = lsFile.Substring(psRoot.Length).TrimStart('\\', '/').Replace('\\', '/');
                    string[] lsLines = File.ReadAllLines(lsFile);

                    for (int liLine = 0; liLine < lsLines.Length; liLine++)
                    {
                        foreach (Match lOMatch in mORoutine.Matches(lsLines[liLine]))
                        {
                            lOCitations.Add(new Citation
                            {
                                File = lsRelative,
                                Line = liLine + 1,
                                Token = lOMatch.Value,
                                IsReference = fIsReference(lsRelative, lsLines[liLine])
                            });
                        }
                    }
                }
            }

            return lOCitations;
        }

        private static bool fIsSkipped(string psFile)
        {
            string lsPath = psFile.Replace('\\', '/');

            foreach (string lsSkip in msSkipFolders)
            {
                if (lsPath.Contains("/" + lsSkip + "/"))
                    return true;
            }

            return false;
        }

        /// <summary>The routine without its readable name: seg044_A33 out of
        /// SpawnClass7Object_seg044_A33.</summary>
        private static string fCore(string psToken)
        {
            Match lOMatch = mORoutine.Match(psToken);

            return lOMatch.Success ? lOMatch.Groups["core"].Value : psToken;
        }

        /// <summary>The readable name a citation carries, or an empty string.</summary>
        private static string fName(string psToken)
        {
            Match lOMatch = mORoutine.Match(psToken);
            string lsName = lOMatch.Success ? lOMatch.Groups["name"].Value : string.Empty;

            return lsName.TrimEnd('_');
        }

        /// <summary>
        /// The files whose job is to DESCRIBE the game's tables and records rather than to hold
        /// a rule. They name a routine to prove what a byte means, which is why they turn up
        /// beside the rule that uses the byte - a split by intent, not a duplicate.
        /// </summary>
        private static readonly string[] msDescribers =
        {
            "UWObjectClassProperties.cs", "UWCommonObjectProperties.cs", "UWCritterRecord.cs",
            "UWPlayerData.cs", "UWObjectMechanics.cs", "UWCritterAnimations.cs"
        };

        private static bool fIsDescriber(string psFile)
        {
            return msDescribers.Contains(Path.GetFileName(psFile));
        }

        /// <summary>
        /// Pairs of files that SHARE a place of the original on purpose, with the reason. They
        /// are the architecture, not a duplicate, and without this list the shortlist is nothing
        /// but the architecture: on the first run 32 of 38 shared places were the pair
        /// "brain and rules" and their like.
        ///
        /// A NEW PAIR MAY ONLY BE ADDED WITH A REASON. That is the whole point: whatever is not
        /// declared here shows up, and then someone has to look.
        /// </summary>
        private static readonly string[,] msExpectedPairs =
        {
            // The creature AI of phase P4: the flow in the brain, the numbers in the rules,
            // the clock and the alarm beside them, the blows in the combat class.
            { "UWCritterBrain.cs", "UWCritterRules.cs" },
            // The brain asks whether a named creature refuses its death, UWSpecialDeaths answers
            // it - both quote ProcessDeath, one as the caller and one as the case behind it.
            { "UWCritterBrain.cs", "UWSpecialDeaths.cs" },
            { "UWCritterCombat.cs", "UWCritterRules.cs" },
            { "UWCritterClock.cs", "UWCritterRules.cs" },
            { "UWCritterAlarm.cs", "UWCritterRules.cs" },
            { "UWCritterAlarm.cs", "UWCritterBrain.cs" },
            { "UWCritterBrain.cs", "UWCritterCombat.cs" },
            { "UWCritterClock.cs", "UWCritterBrain.cs" },

            // The player's own row is the defender of the same combat routines.
            { "UWCritterCombat.cs", "UWPlayerCritterRow.cs" },
            { "UWCritterRules.cs", "UWPlayerCritterRow.cs" },
            { "UWArmourProtection.cs", "UWPlayerCritterRow.cs" },

            // The player tick: its length alone, its block in the vitals, the enchantments
            // that hang on it, and the status pass that sets their bits.
            { "UWPlayerVitals.cs", "UWPlayerTick.cs" },
            { "UWPlayerVitals.cs", "UWWornRegeneration.cs" },
            { "UWPlayerTick.cs", "UWWornRegeneration.cs" },
            { "UWArmourProtection.cs", "UWWornRegeneration.cs" },
            { "UWArmourProtection.cs", "UWCritterRules.cs" },
            // The same status pass, two aspects: it clears the regeneration bits, and it also
            // holds the players loudness and visibility, which the creature rules read.
            { "UWCritterRules.cs", "UWWornRegeneration.cs" },
            // SkillGain_ovr143_271 raises a skill, which is UWPlayerVitals; UWLoreCheck names
            // it only for the two lines at its end that reset the identification and then
            // write a per-level record nothing reads.
            { "UWLoreCheck.cs", "UWPlayerVitals.cs" },

            // The blow execution of AttackerAppliesFinalDamage_seg022_8A5 lives in
            // UWCritterCombat; UWCombat names it only for the ORDER inside it - the hit sound
            // is played before the armour is subtracted, which is what the player's own blow
            // takes its volume from. UWPlayerCritterRow names it as the row the armour is
            // read from.
            { "UWCombat.cs", "UWCritterCombat.cs" },
            { "UWCombat.cs", "UWPlayerCritterRow.cs" },

            // Neither of the two implements RegisterEventHandler_seg010_105 - UWClickRules
            // names it as the place where the original's click areas are registered,
            // UWEasyMovement as the place where its own three arrows are.
            { "UWClickRules.cs", "UWEasyMovement.cs" },

            // The spell class itself lives in UWMiscSpellRules; UWPlayerCritterRow only names
            // MajorSpellClassB_seg038_1645 in its list of the 26 places from which the original
            // refills the row. A cross-reference, not a second implementation.
            { "UWMiscSpellRules.cs", "UWPlayerCritterRow.cs" },

            // Experience: the vitals hold the store, UWExperience the rule, and the kill
            // reward itself sits once in the combat class (UWExperience.GetKillReward only
            // hands it through).
            { "UWPlayerVitals.cs", "UWExperience.cs" },
            { "UWCritterCombat.cs", "UWExperience.cs" },
            { "UWCritterRules.cs", "UWExperience.cs" },
            { "UWPlayerVitals.cs", "UWCritterCombat.cs" },
            { "UWPlayerVitals.cs", "UWCritterRules.cs" },

            // What the liquid swallows: the roll in UWLiquidCulling, the query that applies it
            // to an object and its contents in UWTileQueries.
            { "UWLiquidCulling.cs", "UWTileQueries.cs" },

            // The map: the level data on one side, the rule for revealing on the other.
            { "UWExplorationRules.cs", "UWLevel.cs" },

            // CalculateAttackResults_seg022_230E_6B9 has two halves: the hit check with the
            // critical doubling, which the combat classes and the player's row hold, and the
            // three branches that hand a piece of equipment to ovr120_C0F, which UWEquipmentWear
            // holds (2026-09-28).
            { "UWCritterCombat.cs", "UWEquipmentWear.cs" },
            { "UWPlayerCritterRow.cs", "UWEquipmentWear.cs" },
        };

        /// <summary>Whether these two files are declared as an intended pair.</summary>
        private static bool fIsExpectedPair(string psLeft, string psRight)
        {
            string lsLeft = Path.GetFileName(psLeft);
            string lsRight = Path.GetFileName(psRight);

            for (int liAt = 0; liAt < msExpectedPairs.GetLength(0); liAt++)
            {
                string lsA = msExpectedPairs[liAt, 0];
                string lsB = msExpectedPairs[liAt, 1];

                if ((lsA == lsLeft && lsB == lsRight) || (lsA == lsRight && lsB == lsLeft))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a place is shared only in ways that were declared: every pair of rule files
        /// on it stands in msExpectedPairs. Then it is architecture and no finding.
        /// </summary>
        private static bool fIsDeclaredSplit(List<string> pORuleFiles)
        {
            for (int liLeft = 0; liLeft < pORuleFiles.Count; liLeft++)
            {
                for (int liRight = liLeft + 1; liRight < pORuleFiles.Count; liRight++)
                {
                    if (!fIsExpectedPair(pORuleFiles[liLeft], pORuleFiles[liRight]))
                        return false;
                }
            }

            return true;
        }

        /// <summary>Engine-free means: under Assets/UWDataImport. Those are the rules; the rest
        /// is host, tool or check.</summary>
        private static bool fIsRules(string psFile)
        {
            return psFile.StartsWith("Assets/UWDataImport/", StringComparison.Ordinal);
        }

        /// <summary>
        /// The engine-free files that IMPLEMENT a place, rather than pointing at it.
        ///
        /// TWO FILTERS, both learned from the first run, which called fifty places suspicious
        /// and was therefore useless: a line that points somewhere else does not count
        /// (fIsReference), and a file that names the place only ONCE does not count either as
        /// long as another file names it repeatedly. Whoever writes a rule quotes its routine
        /// several times - once per rule of it; whoever only borrows a fact quotes it once.
        /// </summary>
        private static List<string> fImplementers(List<Citation> pOCitations)
        {
            Dictionary<string, int> lOCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (Citation lOCitation in pOCitations)
            {
                if (lOCitation.IsReference || !fIsRules(lOCitation.File))
                    continue;

                lOCounts.TryGetValue(lOCitation.File, out int liCount);
                lOCounts[lOCitation.File] = liCount + 1;
            }

            int liMost = lOCounts.Count == 0 ? 0 : lOCounts.Values.Max();

            return lOCounts
                .Where(pOPair => liMost < 2 || pOPair.Value > 1)
                .Select(pOPair => pOPair.Key)
                .OrderBy(psFile => psFile, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// The places worth looking at: named from at least two DIFFERENT engine-free files.
        /// One rules class plus a host, a tool or a check is the normal split and no suspect.
        /// </summary>
        private static List<KeyValuePair<string, List<Citation>>> fSuspects(
            Dictionary<string, List<Citation>> pOByRoutine)
        {
            return pOByRoutine
                .Where(pOPair => fImplementers(pOPair.Value).Count > 1)
                .OrderByDescending(pOPair => pOPair.Value.Select(pOCitation => pOCitation.File).Distinct().Count())
                .ThenBy(pOPair => pOPair.Key, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>The whole file.</summary>
        private static string fBuild(Dictionary<string, List<Citation>> pOByRoutine)
        {
            StringBuilder lOOut = new StringBuilder();

            lOOut.AppendLine("# Where the original is implemented");
            lOOut.AppendLine();
            lOOut.AppendLine("GENERATED by `Tools/UWRuleIndex` - do not edit. Rebuild with:");
            lOOut.AppendLine();
            lOOut.AppendLine("```");
            lOOut.AppendLine("dotnet run --project Tools/UWRuleIndex");
            lOOut.AppendLine("```");
            lOOut.AppendLine();
            lOOut.AppendLine("Every routine and label of UW.EXE that our sources quote, with the files that");
            lOOut.AppendLine("quote it. Look here BEFORE building a rule: if the place is already listed, it");
            lOOut.AppendLine("has an owner. Todo.md says what is missing, this says where something lives.");
            lOOut.AppendLine();
            lOOut.AppendLine("The disassembly itself is not part of the repository and must not be published;");
            lOOut.AppendLine("what stands here are names and labels, never code.");
            lOOut.AppendLine();

            List<KeyValuePair<string, List<Citation>>> lOAll = fSuspects(pOByRoutine);
            List<KeyValuePair<string, List<Citation>>> lOSuspects = lOAll
                .Where(pOPair => fIsSuspect(pOPair.Value))
                .ToList();
            List<KeyValuePair<string, List<Citation>>> lOSplit = lOAll.Except(lOSuspects).ToList();

            lOOut.AppendLine("## Named from more than one engine-free class");
            lOOut.AppendLine();
            lOOut.AppendLine("The signal for a rule implemented twice. Counted are only lines that do NOT point");
            lOOut.AppendLine("somewhere else (\"see\", \"as in\", \"like\" and the rest of msReferenceWords) and do not");
            lOOut.AppendLine("sit in an interface - otherwise every well cross-referenced rule would show up here.");
            lOOut.AppendLine("A rules class together with its Unity host, a tool or a self-check is the normal");
            lOOut.AppendLine("split and does not appear either.");
            lOOut.AppendLine();

            lOOut.AppendLine("### Look at these first");
            lOOut.AppendLine();
            lOOut.AppendLine("Two or more RULE classes on the same place of the original, and their pairing is");
            lOOut.AppendLine("not declared as intended in msExpectedPairs. Everything here wants a look: either");
            lOOut.AppendLine("the rule is written twice, or the pair belongs in that list with its reason.");
            lOOut.AppendLine();
            fTable(lOOut, lOSuspects);
            lOOut.AppendLine();
            lOOut.AppendLine("### Declared splits and table descriptions");
            lOOut.AppendLine();
            lOOut.AppendLine("Expected: either the pairing stands in msExpectedPairs with its reason, or one of");
            lOOut.AppendLine("the files only describes what a byte of the game's tables means (msDescribers).");
            lOOut.AppendLine();
            fTable(lOOut, lOSplit);

            lOOut.AppendLine();
            lOOut.AppendLine("## All places, by segment");
            lOOut.AppendLine();

            foreach (IGrouping<string, KeyValuePair<string, List<Citation>>> lOGroup in pOByRoutine
                .GroupBy(pOPair => pOPair.Key.Substring(0, 6))
                .OrderBy(pOGroup => pOGroup.Key, StringComparer.Ordinal))
            {
                lOOut.AppendLine("### " + lOGroup.Key);
                lOOut.AppendLine();
                lOOut.AppendLine("| Place | Files |");
                lOOut.AppendLine("|---|---|");

                foreach (KeyValuePair<string, List<Citation>> lOPair in lOGroup
                    .OrderBy(pOPair => pOPair.Key, StringComparer.Ordinal))
                {
                    lOOut.AppendLine("| " + fLabel(lOPair) + " | "
                        + string.Join(", ", lOPair.Value.Select(pO => pO.File).Distinct()
                            .OrderBy(pO => pO, StringComparer.Ordinal).Select(fShort)) + " |");
                }

                lOOut.AppendLine();
            }

            return lOOut.ToString();
        }

        /// <summary>
        /// A place worth looking at: more than one RULE file on it, and at least one of the
        /// pairs among them is not declared in msExpectedPairs.
        /// </summary>
        private static bool fIsSuspect(List<Citation> pOCitations)
        {
            List<string> lORules = fImplementers(pOCitations)
                .Where(psFile => !fIsDescriber(psFile))
                .ToList();

            return lORules.Count > 1 && !fIsDeclaredSplit(lORules);
        }

        /// <summary>One of the two tables of the head section.</summary>
        private static void fTable(StringBuilder pOOut, List<KeyValuePair<string, List<Citation>>> pORows)
        {
            if (pORows.Count == 0)
            {
                pOOut.AppendLine("None.");

                return;
            }

            pOOut.AppendLine("| Place of the original | Engine-free files |");
            pOOut.AppendLine("|---|---|");

            foreach (KeyValuePair<string, List<Citation>> lOPair in pORows)
            {
                pOOut.AppendLine("| " + fLabel(lOPair) + " | "
                    + string.Join(", ", fImplementers(lOPair.Value).Select(fShort)) + " |");
            }
        }

        /// <summary>The routine with the readable name our comments give it, if any does.</summary>
        private static string fLabel(KeyValuePair<string, List<Citation>> pOPair)
        {
            string lsName = pOPair.Value.Select(pO => fName(pO.Token))
                .FirstOrDefault(pO => !string.IsNullOrEmpty(pO));

            return string.IsNullOrEmpty(lsName)
                ? "`" + pOPair.Key + "`"
                : "`" + lsName + "` (" + pOPair.Key + ")";
        }

        /// <summary>The file without its folder, which is enough to recognise it.</summary>
        private static string fShort(string psFile)
        {
            return Path.GetFileName(psFile);
        }
        /// <summary>What one scan found - see Scan.</summary>
        internal sealed class Result
        {
            /// <summary>The whole RULES-INDEX.md, ready to write.</summary>
            public string Markdown;

            /// <summary>How many times the sources name a place of the original.</summary>
            public int Citations;

            /// <summary>How many places that is.</summary>
            public int Places;

            /// <summary>Places named from more than one engine-free file.</summary>
            public int Shared;

            /// <summary>Of those, the ones worth looking at - see fIsSuspect. Each entry is
            /// the place with the files that implement it.</summary>
            public List<string> Suspects = new List<string>();
        }

        /// <summary>
        /// Reads the sources under the project root and builds everything the index needs. No
        /// file is written; the caller decides what to do with it.
        /// </summary>
        internal static Result Scan(string psRoot)
        {
            List<Citation> lOCitations = fCollect(psRoot);

            if (lOCitations.Count == 0)
                return null;

            // Group by the routine itself, without the readable name: the same place is quoted
            // once as seg007_1798_2C4A and once as NPCBehaviours_seg007_1798_2C4A.
            Dictionary<string, List<Citation>> lOByRoutine = new Dictionary<string, List<Citation>>(StringComparer.Ordinal);

            foreach (Citation lOCitation in lOCitations)
            {
                string lsKey = fCore(lOCitation.Token);

                if (!lOByRoutine.TryGetValue(lsKey, out List<Citation> lOList))
                {
                    lOList = new List<Citation>();
                    lOByRoutine[lsKey] = lOList;
                }

                lOList.Add(lOCitation);
            }

            List<KeyValuePair<string, List<Citation>>> lOShared = fSuspects(lOByRoutine);

            Result lOResult = new Result
            {
                Markdown = fBuild(lOByRoutine),
                Citations = lOCitations.Count,
                Places = lOByRoutine.Count,
                Shared = lOShared.Count
            };

            foreach (KeyValuePair<string, List<Citation>> lOPair in lOShared)
            {
                if (fIsSuspect(lOPair.Value))
                {
                    lOResult.Suspects.Add(lOPair.Key + " in "
                        + string.Join(", ", fImplementers(lOPair.Value).Select(fShort)));
                }
            }

            return lOResult;
        }

        /// <summary>
        /// Every place of the original our sources name, without the readable prefix. The
        /// coverage tool asks for this to see what of UW.EXE we have never touched.
        /// </summary>
        internal static HashSet<string> CollectCitedCores(string psRoot)
        {
            HashSet<string> lOCores = new HashSet<string>(StringComparer.Ordinal);

            foreach (Citation lOCitation in fCollect(psRoot))
                lOCores.Add(fCore(lOCitation.Token));

            return lOCores;
        }

        /// <summary>Where the index belongs.</summary>
        internal static string GetIndexPath(string psRoot)
        {
            return Path.Combine(psRoot, "Docs", "RULES-INDEX.md");
        }

        /// <summary>The project root from the caller's position - the folder with Assets.
        /// Public because the self-check has to find it too.</summary>
        internal static string FindProjectRoot()
        {
            return fFindRoot();
        }
    }
}
