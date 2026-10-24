using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.6 (2026-10-20): small rules both interface schemes print or show, each given
    /// one engine-free home - the picture of an open container (UWObjectAppearance), the remaining
    /// carry weight (UWInventoryWeight), the body slot of a place on the inventory page
    /// (UWClickRules.BodySlotOf) and the class and skill names and the level ordinal
    /// (UWCharacterText). Pinned with what the copies they replace computed.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckUiHelpers()
        {
            Check.Group("Interface helpers (open picture, remaining weight, body slots, ordinal - cleanup step 5.6)");

            // The open variant: sack to gold coffer with bit 0 set ("| 1", an open number stays).
            fExpectInt("open picture: a sack (128) opens to 129", UWObjectIds.OpenPicture(128), 129);
            fExpectInt("open picture: an already open sack stays 129", UWObjectIds.OpenPicture(129), 129);
            fExpectInt("open picture: the gold coffer (138) opens to 139", UWObjectIds.OpenPicture(138), 139);
            fExpectInt("open picture: urn, quiver and bowl show themselves",
                UWObjectIds.OpenPicture(140) + UWObjectIds.OpenPicture(141) + UWObjectIds.OpenPicture(142), 140 + 141 + 142);
            fExpectBool("open picture: no container, no picture",
                UWObjectAppearance.OpenContainerPicture(null, new UWObject(128)) == null
                && UWObjectAppearance.OpenContainerPicture(null, null) == null, true);

            // Without a player there is no capacity: the classic circle showed 0 then.
            fExpectInt("weight left: no player, no capacity", UWInventoryWeight.RemainingTenthStones(null, 30), -30);
            fExpectInt("weight left: no player shows 0", UWInventoryWeight.RemainingStones(null, 30), 0);
            fExpectInt("weight left: 12.0 stones show 12", UWInventoryWeight.RemainingStones(null, -120), 12);

            // The body slot of a spot, as UWHudInventory.GetSlotOfSpot had it: by where the spot
            // lies, the screen's left being the character's right.
            fExpectBodySlot("body slot: helmet", UWClickRules.InventorySpot.Helmet, UWArmorItemMap.BodySlot.Helmet);
            fExpectBodySlot("body slot: chest", UWClickRules.InventorySpot.Chest, UWArmorItemMap.BodySlot.Chest);
            fExpectBodySlot("body slot: gloves", UWClickRules.InventorySpot.Gloves, UWArmorItemMap.BodySlot.Gloves);
            fExpectBodySlot("body slot: legs", UWClickRules.InventorySpot.Legs, UWArmorItemMap.BodySlot.Legs);
            fExpectBodySlot("body slot: boots", UWClickRules.InventorySpot.Boots, UWArmorItemMap.BodySlot.Boots);
            fExpectBodySlot("body slot: the left shoulder is the right off hand", UWClickRules.InventorySpot.ShoulderLeft,
                UWArmorItemMap.BodySlot.RightOffHandSlot);
            fExpectBodySlot("body slot: the right shoulder is the left off hand", UWClickRules.InventorySpot.ShoulderRight,
                UWArmorItemMap.BodySlot.LeftOffHandSlot);
            fExpectBodySlot("body slot: the left hand spot is the right hand", UWClickRules.InventorySpot.HandLeft,
                UWArmorItemMap.BodySlot.RightHandSlot);
            fExpectBodySlot("body slot: the right hand spot is the left hand", UWClickRules.InventorySpot.HandRight,
                UWArmorItemMap.BodySlot.LeftHandSlot);
            fExpectBodySlot("body slot: the left ring spot is the right ring", UWClickRules.InventorySpot.RingLeft,
                UWArmorItemMap.BodySlot.RightRing);
            fExpectBodySlot("body slot: the right ring spot is the left ring", UWClickRules.InventorySpot.RingRight,
                UWArmorItemMap.BodySlot.LeftRing);
            fExpectBool("body slot: none, the backpack, the container icon and the arrows have none",
                UWClickRules.BodySlotOf(UWClickRules.InventorySpot.None).HasValue
                || UWClickRules.BodySlotOf(UWClickRules.InventorySpot.Backpack0).HasValue
                || UWClickRules.BodySlotOf(UWClickRules.InventorySpot.Backpack7).HasValue
                || UWClickRules.BodySlotOf(UWClickRules.InventorySpot.ContainerIcon).HasValue
                || UWClickRules.BodySlotOf(UWClickRules.InventorySpot.ScrollLeft).HasValue
                || UWClickRules.BodySlotOf(UWClickRules.InventorySpot.ScrollRight).HasValue, false);
            fExpectBool("body slot: a point on the helmet (277, row 15) is the helmet slot",
                UWClickRules.BodySlotOf(UWClickRules.InventorySpotAt(277, 199 - 15)) == UWArmorItemMap.BodySlot.Helmet, true);

            // The ordinal of the original's stats page, as UWHudPanel and UWModernStatsPage wrote it.
            Check.Str("ordinal: 1ST 2ND 3RD 4TH",
                UWCharacterText.Ordinal(1) + " " + UWCharacterText.Ordinal(2) + " " + UWCharacterText.Ordinal(3) + " "
                + UWCharacterText.Ordinal(4), "1ST 2ND 3RD 4TH");
            Check.Str("ordinal: 11TH 12TH 13TH take TH",
                UWCharacterText.Ordinal(11) + " " + UWCharacterText.Ordinal(12) + " " + UWCharacterText.Ordinal(13), "11TH 12TH 13TH");
            Check.Str("ordinal: 16TH, the highest level", UWCharacterText.Ordinal(16), "16TH");
            Check.Str("ordinal: 21ST 22ND 23RD 101ST 111TH",
                UWCharacterText.Ordinal(21) + " " + UWCharacterText.Ordinal(22) + " " + UWCharacterText.Ordinal(23) + " "
                + UWCharacterText.Ordinal(101) + " " + UWCharacterText.Ordinal(111), "21ST 22ND 23RD 101ST 111TH");
            Check.Str("title case: as the help window wrote it", UWCharacterText.TitleCase(" FIGHTER\n"), "Fighter");
            Check.Str("title case: null is empty", UWCharacterText.TitleCase(null), string.Empty);
            fExpectInt("skill list: Attack, Defense and eighteen skills", UWCharacterText.SkillEntryCount, 20);
            Check.Str("names: no strings, the fallback", UWCharacterText.ClassName(null, 0, "x") + UWCharacterText.SkillName(null, 0, "y")
                + UWCharacterText.ClassName((UWStrings)null, (UWPlayerData)null), "xy");
        }

        private static void fExpectBodySlot(string psName, UWClickRules.InventorySpot peSpot, UWArmorItemMap.BodySlot peSlot)
        {
            UWArmorItemMap.BodySlot? leSlot = UWClickRules.BodySlotOf(peSpot);

            fExpectBool(psName, leSlot.HasValue && leSlot.Value == peSlot, true);
        }

        private static void fCheckUiHelpersData(DataImport pOData)
        {
            Check.Group("Interface helpers with the game's data (cleanup step 5.6)");

            UWTextures lOTextures = pOData.Textures;

            // The picture the classic frame showed above the open container: OBJECTS.GR at the
            // open variant - the modern original page shows it too since B29.
            fExpectBool("open picture: a sack shows OBJECTS.GR 129",
                UWObjectAppearance.OpenContainerPicture(lOTextures, new UWObject(128))
                == lOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, 129), true);
            fExpectBool("open picture: a bowl shows its own picture 142",
                UWObjectAppearance.OpenContainerPicture(lOTextures, new UWObject(142))
                == lOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, 142), true);
            fExpectBool("open picture: the open sack's picture is not the closed one",
                UWObjectAppearance.OpenContainerPicture(lOTextures, new UWObject(128))
                != lOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, 128), true);

            // The remaining weight, as the classic circle, the modern page, the help window and
            // CanCarry computed it: capacity minus load; the circle in whole stones, never below 0.
            // The template PLAYER.DAT carries no capacity, so a copy with 420 tenth-stones (strength
            // 21) stands in, as in fCheckInventoryModel - the loaded data is not touched.
            if (pOData.InitialPlayer != null)
            {
                byte[] lyPlain = (byte[])pOData.InitialPlayer.PlainBytes.Clone();

                UWBinary.Write16(lyPlain, 0x4C, 420);

                UWPlayerData lOPlayer = UWPlayerData.FromPlain(lyPlain);
                int liMax = lOPlayer.MaxWeight;

                fExpectInt("weight left: the stand-in carries 42 stones", liMax, 420);

                fExpectInt("weight left: nothing carried leaves the whole capacity",
                    UWInventoryWeight.RemainingTenthStones(lOPlayer, 0), liMax);
                fExpectInt("weight left: 7.9 stones left show as 7",
                    UWInventoryWeight.RemainingStones(lOPlayer, liMax - 79), 7);
                fExpectInt("weight left: exactly full shows 0", UWInventoryWeight.RemainingStones(lOPlayer, liMax), 0);
                fExpectInt("weight left: overloaded by half a stone is -5 for CanCarry",
                    UWInventoryWeight.RemainingTenthStones(lOPlayer, liMax + 5), -5);
                fExpectInt("weight left: overloaded shows 0, not a negative number",
                    UWInventoryWeight.RemainingStones(lOPlayer, liMax + 15), 0);
            }

            // The class and skill words: every page trimmed them with Trim() (spaces too), character
            // creation and the shrine only of CR/LF (UWText.Block). Pinned that no word carries a
            // space at either end and none is empty, so routing all of them through
            // UWCharacterText changes no shown word.
            UWStrings lOStrings = pOData.Strings;
            bool lbClassesClean = true;
            bool lbSkillsClean = true;

            for (int liClass = 0; liClass < UWCharacterGeneration.ClassCount; liClass++)
            {
                string lsRaw = UWText.Block(lOStrings, UWCharacterText.CharacterWordsBlock, UWStringIds.ClassNameBase + liClass);

                lbClassesClean &= lsRaw.Length > 0 && lsRaw == lsRaw.Trim() && UWCharacterText.ClassName(lOStrings, liClass) == lsRaw;
            }

            for (int liEntry = 0; liEntry < UWCharacterText.SkillEntryCount; liEntry++)
            {
                string lsRaw = UWText.Block(lOStrings, UWCharacterText.CharacterWordsBlock, UWStringIds.SkillNameBase + liEntry);

                lbSkillsClean &= lsRaw.Length > 0 && lsRaw == lsRaw.Trim() && UWCharacterText.SkillName(lOStrings, liEntry) == lsRaw;
            }

            fExpectBool("names: the eight class words are set and need no trimming", lbClassesClean, true);
            fExpectBool("names: the twenty skill words are set and need no trimming", lbSkillsClean, true);
            Check.Str("names: class 0 upper-cased for the original's page", UWCharacterText.ClassName(lOStrings, 0).ToUpper(), "FIGHTER");
            Check.Str("names: skill entries 0, 1 and 2 in title case",
                UWCharacterText.TitleCase(UWCharacterText.SkillName(lOStrings, 0)) + " "
                + UWCharacterText.TitleCase(UWCharacterText.SkillName(lOStrings, 1)) + " "
                + UWCharacterText.TitleCase(UWCharacterText.SkillName(lOStrings, 2)), "Attack Defense Unarmed");
            Check.Str("names: the last skill entry is swimming",
                UWCharacterText.TitleCase(UWCharacterText.SkillName(lOStrings, UWCharacterText.SkillEntryCount - 1)), "Swimming");
        }
    }
}
