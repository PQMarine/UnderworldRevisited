using System;
using System.Collections.Generic;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The look sentences (translation wave 2, 2026-10-26; UWLookCatalog): every sentence the game
    /// builds from the data when the player looks - names, items, piles, owners, enchantments,
    /// identify_inv's names, creatures, scenery, walls, keys and bones, the levels' objects. With
    /// --strings and a game-text pack the same sentences in that language and its grammar, so a
    /// translator sees what the names and their format give.
    /// </summary>
    public static partial class Program
    {
        /// <summary>
        /// uwdump looks [path] [family ...] [--hashes]: the lines "family key text", every family or
        /// the ones named; with --hashes only the count and hash per family, as the self-check pins
        /// them (Pins/look-strings.txt).
        /// </summary>
        private static int fLooks(DumpContext pOContext, List<string> pOArgs)
        {
            bool lbHashes = fTakeFlag(pOArgs, "--hashes");
            HashSet<string> lOWanted = new HashSet<string>(pOArgs, StringComparer.OrdinalIgnoreCase);

            foreach (string lsFamily in lOWanted)
            {
                if (Array.IndexOf(UWLookCatalog.Families, lsFamily.ToLowerInvariant()) < 0)
                {
                    Console.Error.WriteLine("No family " + lsFamily + ". The families: " + string.Join(", ", UWLookCatalog.Families));

                    return 1;
                }
            }

            List<UWLookCatalog.Line> lOLines = UWLookCatalog.Build(pOContext.Data);

            if (lbHashes)
            {
                foreach (KeyValuePair<string, string> lOPair in UWLookCatalog.Hashes(lOLines))
                {
                    if (lOWanted.Count == 0 || lOWanted.Contains(lOPair.Key))
                        Console.WriteLine(lOPair.Key + " = " + lOPair.Value);
                }

                return 0;
            }

            foreach (UWLookCatalog.Line lOLine in lOLines)
            {
                if (lOWanted.Count == 0 || lOWanted.Contains(lOLine.Family))
                    Console.WriteLine(lOLine.Family + " " + lOLine.Key + " " + fOneLine(lOLine.Text ?? "(none)"));
            }

            return 0;
        }
    }
}
