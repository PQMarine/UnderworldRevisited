using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// UWStackRules, UWText and UWItemMessages (2026-10-11): the one stack definition
        /// against the original's reading (is_quant and not the 0x200 bit; a 0 stays 0 in the
        /// raw count; the enchanted flag plays no part), the safe lookups, and the sentences
        /// built around a name. The last pass counts the enchanted is_quant objects below 512
        /// in the shipped levels - the only objects on which the old enchanted test and the
        /// original's threshold would differ.
        /// </summary>
        private static void fCheckStackRules(DataImport pOData)
        {
            Check.Group("Stack rules (UWStackRules)");

            UWObject lOPile = new UWObject(0x10) { HasQuantity = true, Quantity = 7 };
            UWObject lOSingle = new UWObject(0x10) { HasQuantity = true, Quantity = 1 };
            UWObject lOZero = new UWObject(0x10) { HasQuantity = true, Quantity = 0 };
            UWObject lOLinked = new UWObject(0x80) { HasQuantity = false, Quantity = 300 };
            UWObject lOSpecial = new UWObject(0x20) { HasQuantity = true, Quantity = 719, IsEnchanted = true };
            UWObject lOEnchantedPile = new UWObject(0x10) { HasQuantity = true, Quantity = 3, IsEnchanted = true };

            fExpectInt("stack: a pile of seven counts seven", UWStackRules.Count(lOPile), 7);
            fExpectInt("stack: a single piece counts one", UWStackRules.Count(lOSingle), 1);
            fExpectInt("stack: a field of 0 stays 0 in the raw count (chkTenacious, the barter value)", UWStackRules.Count(lOZero), 0);
            fExpectInt("stack: ... and is one piece where a number is shown", UWStackRules.CountAtLeastOne(lOZero), 1);
            fExpectInt("stack: a link is one piece", UWStackRules.Count(lOLinked), 1);
            fExpectInt("stack: a special property (719, the enchanted cap of SAVE4) is one piece", UWStackRules.Count(lOSpecial), 1);
            fExpectBool("stack: ... and is not stackable", UWStackRules.IsStackable(lOSpecial), false);
            fExpectBool("stack: ... but has a special property", UWStackRules.HasSpecialProperty(lOSpecial), true);
            fExpectBool("stack: the enchanted flag alone does not stop a count (USEITEMS.C tests only 0x200)",
                UWStackRules.IsStackable(lOEnchantedPile), true);
            fExpectInt("stack: ... so an enchanted pile of three counts three", UWStackRules.Count(lOEnchantedPile), 3);
            fExpectBool("stack: a pile is a stack", UWStackRules.IsStack(lOPile), true);
            fExpectBool("stack: a single piece is not", UWStackRules.IsStack(lOSingle), false);
            fExpectBool("stack: a special property is not", UWStackRules.IsStack(lOSpecial), false);
            fExpectBool("stack: null is nothing", UWStackRules.IsStack(null) || UWStackRules.IsStackable(null), false);
            fExpectInt("stack: ... and counts one", UWStackRules.CountAtLeastOne(null), 1);

            fExpectBool("take one: off a pile of seven", UWStackRules.TryTakeOne(lOPile), true);
            fExpectInt("take one: ... leaves six", lOPile.Quantity, 6);
            fExpectBool("take one: a single piece goes whole (invRemoveObject)", UWStackRules.TryTakeOne(lOSingle), false);
            fExpectInt("take one: ... and is untouched", lOSingle.Quantity, 1);
            fExpectBool("take one: a special property goes whole, its spell untouched", UWStackRules.TryTakeOne(lOSpecial), false);
            fExpectInt("take one: ... 719 stays", lOSpecial.Quantity, 719);
            fExpectInt("stack: the cap is 511, the port's deliberate deviation", UWStackRules.MaxStackCount, 511);

            Check.Group("Compass needle: the view's heading byte (UWHeadings)");

            fExpectInt("compass: 11.2 degrees truncates to byte 7 (still north)", UWHeadings.DegreesToHeadingByte(11.2f), 7);
            fExpectInt("compass: 11.3 degrees is byte 8 (the next step)", UWHeadings.DegreesToHeadingByte(11.3f), 8);

            Check.Group("Safe string lookups (UWText)");

            Check.Str("text: a missing block gives the fallback", UWText.Block(pOData, 999, 0, "none"), "none");
            Check.Str("text: a missing entry gives the fallback", UWText.General(pOData, 1000000, "none"), "none");
            Check.Str("text: no data gives the fallback", UWText.ObjectName(null, 1, false, false, "item"), "item");
            fExpectBool("text: a general message is trimmed of CR/LF", UWText.General(pOData, 96).TrimEnd('\r', '\n') == UWText.General(pOData, 96)
                && UWText.General(pOData, 96).Length > 0, true);

            int liPoleId = -1;
            int liBadNames = 0;
            int liNamed = 0;

            for (int liId = 0; liId < 464; liId++)
            {
                string lsRaw = UWText.RawObjectName(pOData, liId, null);

                if (lsRaw == null)
                    continue;

                liNamed++;

                if (lsRaw == "a_pole")
                    liPoleId = liId;

                string lsName = UWText.ObjectName(pOData, liId);
                string lsPlural = UWText.ObjectName(pOData, liId, true);
                string lsWithArticle = UWText.ObjectName(pOData, liId, false, true);

                if (lsName.Length == 0 || lsName.IndexOf('_') >= 0 || lsName.IndexOf('&') >= 0
                    || lsPlural.IndexOf('&') >= 0 || lsWithArticle.IndexOf('_') >= 0 || !lsWithArticle.EndsWith(lsName))
                    liBadNames++;
            }

            if (liBadNames == 0)
                fPass("text: every object name resolves its & and _", liNamed + " names");
            else
                fFail("text: every object name resolves its & and _", liBadNames + " of " + liNamed + " do not");

            if (liPoleId >= 0)
            {
                Check.Str("text: the pole's bare name", UWText.ObjectName(pOData, liPoleId), "pole");
                Check.Str("text: ... with its article", UWText.ObjectName(pOData, liPoleId, false, true), "a pole");
                Check.Str("messages: the pole's refusal is the original's string 0x9E (ours 159)",
                    UWItemMessages.CannotBeUsedOnThat(pOData, liPoleId), UWText.General(pOData, 159));
            }
            else
                fFail("text: the pole's bare name", "no a_pole in block 4");

            Check.Group("SpecialLook lines (UWItemDescriptions.SpecialLookLine)");

            UWObject lOKey = new UWObject(0x100) { Owner = 1 };
            UWObject lOPick = new UWObject(0x101);
            UWObject lOSkull = new UWObject(0xC2) { Owner = 7 };
            UWObject lOBonePile = new UWObject(0xC6) { Owner = 0x3F };
            UWObject lONobody = new UWObject(0xC4) { Owner = 0x28 };

            Check.Str("look: a key with lock 1 says block 5 line 0x64 + 1 (KeyLook)",
                UWItemDescriptions.SpecialLookLine(lOKey, pOData), UWText.Block(pOData, 5, 0x64 + 1 + 1));
            fExpectBool("look: a key without a line of its own says nothing",
                UWItemDescriptions.SpecialLookLine(lOPick, pOData) == null, true);
            Check.Str("look: a skull of creature 0x47 (BonesLook, 0x16)",
                UWItemDescriptions.SpecialLookLine(lOSkull, pOData),
                UWText.General(pOData, 0x16 + 1) + UWText.ObjectName(pOData, 0x47, false, true) + ".");
            Check.Str("look: a pile of an adventurer's bones (0x17, the literal)",
                UWItemDescriptions.SpecialLookLine(lOBonePile, pOData), UWText.General(pOData, 0x17 + 1) + "an adventurer.");
            fExpectBool("look: owner 0x28 says nothing",
                UWItemDescriptions.SpecialLookLine(lONobody, pOData) == null, true);

            Check.Group("Item messages (UWItemMessages)");

            UWObject lOCase = new UWObject(0x80);

            fExpectBool("messages: a fitting item gives no message",
                UWItemMessages.ContainerRejection(UWContainerCapacity.ResultEnum.Fits, lOCase, pOData) == null, true);
            fExpectBool("messages: the rune bag's message is a game string",
                UWItemMessages.ContainerRejection(UWContainerCapacity.ResultEnum.RunesOnly, lOCase, pOData).Length > 0, true);
            fExpectBool("messages: a too-full container without the container gives nothing",
                UWItemMessages.ContainerRejection(UWContainerCapacity.ResultEnum.TooHeavy, null, pOData) == null, true);
            fExpectBool("messages: a too-full container names itself",
                UWItemMessages.ContainerRejection(UWContainerCapacity.ResultEnum.TooHeavy, lOCase, pOData)
                    == "The " + UWText.ObjectName(pOData, 0x80) + " is too full.", true);
            Check.Str("messages: a missing name is \"item\", never empty",
                UWItemMessages.IsTooFull(null, 0x80), "The item is too full.");
            fExpectBool("messages: locked ends with the original's full stop (UseCont)",
                UWItemMessages.IsLocked(pOData, 0x80).EndsWith(" is locked."), true);

            // THE DATA: on which objects would the old enchanted test and the original's
            // threshold differ? Only the records a tile reaches count (the free records of
            // LEV.ARK carry junk); doors reuse the enchanted bit as a state bit and are never
            // counted; anything else enchanted with a count below 512 is reported.
            int liDiffering = 0;
            List<string> lODiffering = new List<string>();

            if (pOData.Levels != null)
            {
                for (int liLevel = 0; liLevel < pOData.Levels.Count; liLevel++)
                {
                    UWLevel lOLevel = pOData.Levels[liLevel];

                    if (lOLevel == null || lOLevel.Masterlist == null || lOLevel.TileData == null)
                        continue;

                    HashSet<int> lOSeen = new HashSet<int>();
                    Stack<int> lOToVisit = new Stack<int>();

                    foreach (UWTile lOTile in lOLevel.TileData)
                        if (lOTile != null && lOTile.FirstObjectIndex != 0)
                            lOToVisit.Push(lOTile.FirstObjectIndex);

                    while (lOToVisit.Count > 0)
                    {
                        int liAt = lOToVisit.Pop();

                        if (liAt <= 0 || liAt >= lOLevel.Masterlist.Count || !lOSeen.Add(liAt))
                            continue;

                        UWObject lOObject = lOLevel.Masterlist[liAt];

                        if (lOObject == null)
                            continue;

                        if (lOObject.Link != 0)
                            lOToVisit.Push(lOObject.Link);

                        if (!lOObject.HasQuantity && lOObject.Quantity != 0)
                            lOToVisit.Push(lOObject.Quantity);

                        if (lOObject.IsEnchanted && UWStackRules.IsStackable(lOObject)
                            && lOObject.GetCategory() != UWObject.ObjectCategoryEnum.Doors)
                        {
                            liDiffering++;

                            if (lODiffering.Count < 12)
                                lODiffering.Add(string.Format("level {0} 0x{1:X3} {2} x{3}", liLevel + 1, lOObject.ID,
                                    UWText.ObjectName(pOData, lOObject.ID), lOObject.Quantity));
                        }
                    }
                }
            }

            fPass("stack: enchanted is_quant objects below 512 reachable from a tile (doors aside)",
                liDiffering + (liDiffering > 0 ? ": " + string.Join(", ", lODiffering) : string.Empty));
        }

        /// <summary>
        /// WHO MINDS A THEFT (UWCritterRules.MindsTheft, AngerNPCByIllegalAction_ovr104_C37) and
        /// what becomes of the owner (ovr104_E4C) - the cases of the disassembly, and the three
        /// messages by attitude from the game's own strings.
        /// </summary>
        private static void fCheckTheft(DataImport pOData)
        {
            Check.Group("Theft");

            fExpectBool("theft: a goblin minds the goblins' chest",
                UWCritterRules.MindsTheft(6, 6, false, 0), true);
            fExpectBool("theft: another race does not",
                UWCritterRules.MindsTheft(6, 7, false, 0), false);
            fExpectBool("theft: a locked goblin does not, without bit 5",
                UWCritterRules.MindsTheft(6, 6, true, 0), false);
            fExpectBool("theft: with bit 5 it does",
                UWCritterRules.MindsTheft(0x26, 6, true, 0), true);
            fExpectBool("theft: bit 5 still takes the unlocked ones",
                UWCritterRules.MindsTheft(0x26, 6, false, 0), true);
            fExpectBool("theft: owner 0x20 exactly, only the locked",
                UWCritterRules.MindsTheft(0x20, 0, false, 0), false);
            fExpectBool("theft: owner 0x20 exactly, a locked one of race 0",
                UWCritterRules.MindsTheft(0x20, 0, true, 0), true);
            fExpectBool("theft: the knights mind it while you are none",
                UWCritterRules.MindsTheft(0x0D, 0x0D, false, 2), true);
            fExpectBool("theft: and not once you are one",
                UWCritterRules.MindsTheft(0x0D, 0x0D, false, 3), false);
            fExpectBool("theft: no owner, nobody",
                UWCritterRules.MindsTheft(0, 0, false, 0), false);

            // The area: seven back and eight forward, the far end included (RunCodeOnObjectsInArea).
            fExpectInt("theft: the area reaches seven tiles back", UWCritterRules.TheftAreaBack, 7);
            fExpectInt("theft: and eight forward", UWCritterRules.TheftAreaForward, 8);

            fExpectInt("theft: goodwill drops by one", UWCritterRules.LowerAttitudeForTheft(2), 1);
            fExpectInt("theft: and not below hostile", UWCritterRules.LowerAttitudeForTheft(0), 0);

            fExpectBool("theft: the owner goes up to 0x1B",
                UWCritterRules.ClearsOwnerAfterTheft(0x1B), true);
            fExpectBool("theft: and stays from 0x1C",
                UWCritterRules.ClearsOwnerAfterTheft(0x1C), false);
            // Where and how far a creature looks (AngerNPCByIllegalAction_ovr104_C37): at the item's
            // zpos + height + 12, and the eighths divided by 8 before squaring.
            fExpectInt("theft: sight height of a fish on floor 12", UWCritterRules.GetTheftSightZ(96, 4), 112);
            fExpectBool("theft: seven tiles away in range 7", UWCritterRules.IsWithinTheftSight(56, 0, 7), true);
            fExpectBool("theft: 63 eighths still count as seven tiles", UWCritterRules.IsWithinTheftSight(63, 0, 7), true);
            fExpectBool("theft: eight tiles away out of range 7", UWCritterRules.IsWithinTheftSight(-64, 0, 7), false);
            fExpectBool("theft: five and five is beyond seven", UWCritterRules.IsWithinTheftSight(40, 40, 7), false);

            fExpectBool("theft: the limit looks at the race bits only",
                UWCritterRules.ClearsOwnerAfterTheft(0x3B), true);

            string[] lsExpected = { "angered", "annoyed", "notes" };

            for (int liAttitude = 0; liAttitude < lsExpected.Length; liAttitude++)
            {
                string lsText = pOData.GetGeneralMessage(UWCritterRules.TheftMessageBase + liAttitude) ?? string.Empty;

                fExpectBool("theft: attitude " + liAttitude + " says \"" + lsText.Trim() + "\"",
                    lsText.Contains(lsExpected[liAttitude]), true);
            }
        }
    }
}
