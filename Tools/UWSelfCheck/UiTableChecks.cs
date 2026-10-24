using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.1 (2026-10-19): the tables both interface schemes read moved into the
    /// engine-free layer - the hand weapons (UWObjectIds.IsHandWeapon, from the modern bags and
    /// action bar), the paper doll's places and icon slots (UWPaperdollLayout, from four copies in
    /// the classic frame, the modern panel and the modern original page) and the save and load
    /// message numbers both schemes' menus print (UWStringIds). Pinned with the values the copies
    /// had, and the messages' wording against the game's strings.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckUiTables()
        {
            Check.Group("Interface tables (UWObjectIds weapons, UWPaperdollLayout - cleanup step 5.1)");

            // The weapons the hand takes: 0x00-0x0F and 0x18-0x1F, not the ammunition between.
            fExpectBool("weapons: 0x00 and 0x0F are hand weapons",
                UWObjectIds.IsHandWeapon(0x00) && UWObjectIds.IsHandWeapon(0x0F), true);
            fExpectBool("weapons: 0x10 and 0x17 (ammunition, spell missiles) are not",
                UWObjectIds.IsHandWeapon(0x10) || UWObjectIds.IsHandWeapon(0x17), false);
            fExpectBool("weapons: 0x18 and 0x1F (the launchers) are",
                UWObjectIds.IsHandWeapon(0x18) && UWObjectIds.IsHandWeapon(0x1F), true);
            fExpectBool("weapons: 0x20 is not", UWObjectIds.IsHandWeapon(0x20), false);
            fExpectBool("weapons: a melee weapon ends at 0x0F",
                UWObjectIds.IsMeleeWeapon(0x0F) && !UWObjectIds.IsMeleeWeapon(0x10), true);
            fExpectBool("weapons: only a melee weapon wears in the hand (UWEquipmentWear, as the 0x10 limit did)",
                UWEquipmentWear.IsWearableWeapon(new UWObject(0x0F)) && !UWEquipmentWear.IsWearableWeapon(new UWObject(0x10))
                && !UWEquipmentWear.IsWearableWeapon(new UWObject(0x18)), true);

            // The paper doll's places, as UWGameUI, UWModernPanel and UWModernInventoryPage had them.
            fExpectBool("paperdoll: the body at 260/11, the same as UWHudArt's",
                UWPaperdollLayout.BodyX == 260 && UWPaperdollLayout.BodyY == 11
                && UWPaperdollLayout.BodyX == UWHudArt.PaperdollBodyX && UWPaperdollLayout.BodyY == UWHudArt.PaperdollBodyY, true);
            fExpectBool("paperdoll: helmet 267/10 (one pixel up, per user 2026-09-23), gloves 261/42",
                UWPaperdollLayout.HelmetX == 267 && UWPaperdollLayout.HelmetY == 10
                && UWPaperdollLayout.GlovesX == 261 && UWPaperdollLayout.GlovesY == 42, true);
            fExpectBool("paperdoll: chest 262/22, legs 268/24, boots 266/66",
                UWPaperdollLayout.ChestX == 262 && UWPaperdollLayout.ChestY == 22
                && UWPaperdollLayout.LegsX == 268 && UWPaperdollLayout.LegsY == 24
                && UWPaperdollLayout.BootsX == 266 && UWPaperdollLayout.BootsY == 66, true);

            // The six icon slots in UWHudInventory.ArmorHitZoneSlots' order, with the exact floats.
            UWPaperdollLayout.IconSlot[] lOSlots = UWPaperdollLayout.IconSlots;

            fExpectInt("icon slots: six", lOSlots.Length, 6);
            fExpectIconSlot("icon slots: left ring", lOSlots[0], UWArmorItemMap.BodySlot.LeftRing, 291.03f, 56.9f, 4f);
            fExpectIconSlot("icon slots: right ring", lOSlots[1], UWArmorItemMap.BodySlot.RightRing, 260f, 56.9f, 4f);
            fExpectIconSlot("icon slots: right hand (drawn left)", lOSlots[2], UWArmorItemMap.BodySlot.RightHandSlot, 242f, 34.82566f, 16f);
            fExpectIconSlot("icon slots: left hand", lOSlots[3], UWArmorItemMap.BodySlot.LeftHandSlot, 296f, 34.826f, 16f);
            fExpectIconSlot("icon slots: left shoulder", lOSlots[4], UWArmorItemMap.BodySlot.LeftOffHandSlot, 294f, 12.792999f, 16f);
            fExpectIconSlot("icon slots: right shoulder", lOSlots[5], UWArmorItemMap.BodySlot.RightOffHandSlot, 244.9f, 12.793f, 16f);
            fExpectBool("icon slots: a ring's icon is 16 wide, 0.8 up",
                UWPaperdollLayout.RingIconSize == 16f && UWPaperdollLayout.RingIconYOffset == 0.8f, true);

            (UWArmorItemMap.BodySlot leSlot, float lfX, float lfY, float lfSize) = lOSlots[2];

            fExpectBool("icon slots: deconstruct as the tuples did",
                leSlot == UWArmorItemMap.BodySlot.RightHandSlot && lfX == 242f && lfY == 34.82566f && lfSize == 16f, true);
        }

        private static void fExpectIconSlot(string psName, UWPaperdollLayout.IconSlot pOSlot, UWArmorItemMap.BodySlot peSlot,
            float pfX, float pfY, float pfSize)
        {
            fExpectBool(psName, pOSlot.Slot == peSlot && pOSlot.X == pfX && pOSlot.Y == pfY && pOSlot.Size == pfSize, true);
        }

        private static void fCheckUiMessageIds(DataImport pOData)
        {
            Check.Group("Interface message ids (UWStringIds - cleanup step 5.1)");

            Check.Str("message 162", UWText.General(pOData, UWStringIds.NoSaveGameThereMessage), "No save game there.");
            Check.Bool("message 165, with its colour code", UWText.General(pOData, UWStringIds.SaveGameFailedMessage)
                .StartsWith("\\4Save Game Failed.", System.StringComparison.Ordinal), true);
            Check.Bool("message 166, with its colour code", UWText.General(pOData, UWStringIds.SaveGameSucceededMessage)
                .StartsWith("\\6Save Game Succeeded.", System.StringComparison.Ordinal), true);
            Check.Str("message 187", UWText.General(pOData, UWStringIds.CannotBarterContainerMessage),
                "You cannot barter a container.  Instead, remove the contents you want to trade.");
        }
    }
}
