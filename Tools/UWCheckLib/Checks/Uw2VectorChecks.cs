using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// UE's UW2.EXE TEST VECTORS (Docs/UW2-PLAN.md phase 1 step 6, decision 3; Tools/Fixtures/uw2-vectors,
    /// see its README): every table is read and its rows counted; the routines both games share are run
    /// through our rule code with the game's generator started from each row's state (seed), and the
    /// result and the state afterwards (seed_out) compared - the original's own outputs, so a difference
    /// is ours. check_res, debris_type and is_sharp differ between the games (UE's list of differences)
    /// and are held to the second game's variants in phase 4.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>The game's generator from a recorded state; every draw one roll of rand(), n == 0 included,
        /// as UWSessionRandom draws.</summary>
        private sealed class VectorRandom : IUWRandom
        {
            public readonly UWOriginalRandom Generator;

            public VectorRandom(uint puState)
            {
                Generator = UWOriginalRandom.FromState(puState);
            }

            public int Next(int piMaxExclusive)
            {
                int liRoll = Generator.Next();

                return piMaxExclusive <= 0 ? 0 : liRoll % piMaxExclusive;
            }

            public int Next(int piMin, int piMaxExclusive) => piMin + Next(piMaxExclusive - piMin);

            public double NextDouble() => Generator.Next() / (double)UWOriginalRandom.Range;
        }

        private static void fCheckUw2Vectors(CheckContext pOContext)
        {
            Check.Group("UE's UW2.EXE test vectors (UW2 plan, phase 1 step 6)");

            string lsFolder = CheckContext.ProjectRoot == null ? null : Path.Combine(CheckContext.ProjectRoot, "Tools", "Fixtures", "uw2-vectors");

            if (lsFolder == null || !Directory.Exists(lsFolder))
            {
                Check.Pass("vectors", "no source tree here: skipped");

                return;
            }

            Dictionary<string, List<Dictionary<string, long>>> lOTables = new Dictionary<string, List<Dictionary<string, long>>>();
            List<string> lsCounts = new List<string>();

            foreach (string lsFile in Directory.GetFiles(lsFolder, "*.csv"))
            {
                List<Dictionary<string, long>> lORows = fReadVectors(lsFile);

                lOTables[Path.GetFileNameWithoutExtension(lsFile)] = lORows;
            }

            foreach (string lsName in new[] { "check_res", "compute_hitangle", "debris_type", "is_sharp", "move_along", "pickloc", "rollem", "sincos", "skill_check", "sqrt" })
                lsCounts.Add(lsName + ":" + (lOTables.TryGetValue(lsName, out List<Dictionary<string, long>> lORows) ? lORows.Count : -1));

            Check.Str("vectors: the ten tables and their rows", string.Join(" ", lsCounts),
                "check_res:400 compute_hitangle:200 debris_type:300 is_sharp:300 move_along:400 pickloc:300 rollem:300 sincos:400 skill_check:300 sqrt:300");

            IUWRandom lIBefore = UWGameSession.Current.Random.Active;

            try
            {
                fCheckVectorTable(lOTables, "skill_check", "the skill check (UWSkillCheck.Check)", lORow =>
                {
                    VectorRandom lORandom = fUseVectorRandom(lORow);
                    int liResult = (int)UWSkillCheck.Check((int)lORow["value"], (int)lORow["target"]);

                    return liResult == lORow["result"] && lORandom.Generator.State == (uint)lORow["seed_out"];
                });

                // Both games' rollem scales each draw, rand() * sides / 0x8000 (UE uw1 sys/UTIL.C:72, uw2
                // sys/UTIL.C:73); UWRandom.RollDice took rand() % sides until 2026-10-29 (44 of 300 rows agreed).
                fCheckVectorTable(lOTables, "rollem", "the dice (UWRandom.RollDice)", lORow =>
                {
                    VectorRandom lORandom = fUseVectorRandom(lORow);
                    int liTotal = UWRandom.RollDice((int)lORow["dice"], (int)lORow["sides"]);

                    return liTotal == lORow["total"] && lORandom.Generator.State == (uint)lORow["seed_out"];
                });

                fCheckVectorTable(lOTables, "pickloc", "the body part a blow lands on (UWArmourProtection.PickBodyPart)", lORow =>
                {
                    VectorRandom lORandom = fUseVectorRandom(lORow);
                    int liPart = UWArmourProtection.PickBodyPart((int)lORow["dz"], (int)lORow["dtop"], (int)lORow["az"], (int)lORow["atop"]);

                    return liPart == lORow["loc"] && lORandom.Generator.State == (uint)lORow["seed_out"];
                });
            }
            finally
            {
                UWRandom.Use(ReferenceEquals(lIBefore, UWGameSession.Current.Random) ? null : lIBefore);
            }

            fCheckVectorTable(lOTables, "move_along", "a step along a heading (UWAreaSpellRules.MoveAlong)", lORow =>
            {
                UWTilePos lOTo = UWAreaSpellRules.MoveAlong(new UWTilePos((int)lORow["x"], (int)lORow["y"]), (int)lORow["heading"], (int)lORow["dist"]);

                return (short)lOTo.X == lORow["x_out"] && (short)lOTo.Y == lORow["y_out"];
            });

            fCheckVectorTable(lOTables, "sincos", "sine and cosine (UWMotionTables.SinCos)", lORow =>
            {
                UWMotionTables.SinCos((int)lORow["angle"], out int liSin, out int liCos);

                return liSin == lORow["a"] && liCos == lORow["b"];
            });

            fCheckVectorTable(lOTables, "sqrt", "the square root (UWVanillaMath.Sqrt)", lORow => UWVanillaMath.Sqrt((int)lORow["value"]) == lORow["root"]);

            fCheckVectorTable(lOTables, "compute_hitangle", "the flank (UWCritterRules.GetFlankingBonus, headings in bits 7-9)", lORow =>
                UWCritterRules.GetFlankingBonus((int)(lORow["att_pos"] >> 7) & 7, (int)(lORow["def_pos"] >> 7) & 7) == lORow["hitangle"]);
        }

        /// <summary>The game's generator at the row's state, put in front of the session's.</summary>
        private static VectorRandom fUseVectorRandom(Dictionary<string, long> pORow)
        {
            VectorRandom lORandom = new VectorRandom((uint)pORow["seed"]);

            UWRandom.Use(lORandom);

            return lORandom;
        }

        /// <summary>One table through our code: how many rows agree, the first rows that do not.</summary>
        private static void fCheckVectorTable(Dictionary<string, List<Dictionary<string, long>>> pOTables, string psName, string psWhat,
            Func<Dictionary<string, long>, bool> pFAgrees, string psExpected = null)
        {
            if (!pOTables.TryGetValue(psName, out List<Dictionary<string, long>> lORows))
                return;

            int liAgree = 0;
            List<string> lsFirst = new List<string>();

            for (int liAt = 0; liAt < lORows.Count; liAt++)
            {
                if (pFAgrees(lORows[liAt]))
                    liAgree++;
                else if (lsFirst.Count < 3)
                    lsFirst.Add("row " + (liAt + 1));
            }

            Check.Str("vectors: " + psName + ", " + psWhat + ", rows agreeing", liAgree + "/" + lORows.Count
                + (lsFirst.Count > 0 ? " (" + string.Join(", ", lsFirst) + ")" : string.Empty), psExpected ?? lORows.Count + "/" + lORows.Count);
        }

        /// <summary>A table: the header's names, then a row of decimal numbers each.</summary>
        private static List<Dictionary<string, long>> fReadVectors(string psPath)
        {
            List<Dictionary<string, long>> lORows = new List<Dictionary<string, long>>();
            string[] lsLines = File.ReadAllLines(psPath);

            if (lsLines.Length == 0)
                return lORows;

            string[] lsNames = lsLines[0].Trim().Split(',');

            for (int liLine = 1; liLine < lsLines.Length; liLine++)
            {
                if (lsLines[liLine].Trim().Length == 0)
                    continue;

                string[] lsValues = lsLines[liLine].Trim().Split(',');
                Dictionary<string, long> lORow = new Dictionary<string, long>();

                for (int liAt = 0; liAt < lsNames.Length && liAt < lsValues.Length; liAt++)
                    lORow[lsNames[liAt]] = long.Parse(lsValues[liAt], System.Globalization.CultureInfo.InvariantCulture);

                lORows.Add(lORow);
            }

            return lORows;
        }
    }
}
