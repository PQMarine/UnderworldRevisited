using System;
using System.Collections.Generic;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// EVERY ENGLISH LOOK SENTENCE, PINNED (translation wave 2, 2026-10-26): the look catalog
    /// (Tools/Shared/UWLookCatalog - names, items over quality, count, owner and detail, the
    /// enchantments and charges, identify_inv, creatures, scenery and doors, walls, keys and bones,
    /// the nine levels' objects) hashed per family against Pins/look-strings.txt. Recorded before the
    /// sentence building moved into the grammar (IUWGrammar), so English stays byte-exact through
    /// that move and after: a changed sentence shows as a changed family. Only hashes and counts
    /// are kept, no text of the game.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckLookCatalog(CheckContext pOContext)
        {
            Check.Group("Look catalog (every English look sentence, hashed per family)");

            if (pOContext.Data == null)
            {
                Check.Fail("look catalog: data", "not loaded");

                return;
            }

            List<UWLookCatalog.Line> lOLines = UWLookCatalog.Build(pOContext.Data);
            SortedDictionary<string, string> lONow = UWLookCatalog.Hashes(lOLines);

            fExpectBool("look catalog: every family has lines", lONow.Count == UWLookCatalog.Families.Length, true);
            fComparePins(pOContext, "look-strings.txt", "look sentences", lONow, psFamily => "families (" + lOLines.Count + " sentences)");
        }
    }
}
