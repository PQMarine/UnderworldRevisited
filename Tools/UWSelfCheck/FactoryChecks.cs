using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The fresh object (Docs/CLEANUP-PLAN.md, phase 2 step "UWObjectFactory"): CreateObj's
    /// fields against the real COMOBJ.DAT, the pictures UWObjectAppearance hands out, the
    /// create-object trap's copy, and where UWInventoryModel.GiveToPlayer puts a thing.
    /// </summary>
    public static partial class Program
    {
        /// <summary>CreateObj (MAPADDR.C): quality 40, owner 0, link 0, z 0, heading 0, fine
        /// position 3/3, flags and id bits clear; the quantity bit with a link of 1 for COMOBJ
        /// stack classes 0 and 2 only. Pinned on every id of the shipped table.</summary>
        private static void fCheckFactory(CheckContext pOContext)
        {
            Check.Group("Factory (UWObjectFactory, UWObjectAppearance: CreateObj's fresh object)");

            DataImport lOData = pOContext.Data;
            UWCommonObjectProperties lOCommon = lOData.CommonObjectProperties;

            if (lOCommon == null || lOData.Textures == null)
            {
                Check.Fail("factory", "no COMOBJ.DAT or no textures");

                return;
            }

            UWObject lODagger = UWObjectFactory.Create(1, lOCommon);

            Check.Int("factory: quality 40", lODagger.Quality, UWObjectFactory.DefaultQuality);
            Check.Int("factory: owner 0", lODagger.Owner, 0);
            Check.Int("factory: next link 0", lODagger.Link, 0);
            Check.Int("factory: z 0", lODagger.ZPos, 0);
            Check.Int("factory: heading 0", lODagger.Heading, 0);
            Check.Int("factory: fine x 3", lODagger.XPos, UWObjectFactory.DefaultFinePosition);
            Check.Int("factory: fine y 3", lODagger.YPos, UWObjectFactory.DefaultFinePosition);
            Check.Int("factory: flags 0", lODagger.Flags, 0);
            Check.Bool("factory: not enchanted", lODagger.IsEnchanted, false);
            Check.Bool("factory: door direction clear", lODagger.DoorDirection, false);
            Check.Bool("factory: not invisible", lODagger.IsHidden, false);
            Check.Bool("factory: no picture without textures", lODagger.Texture == null, true);

            // The stack rule on every id the table has.
            int[] liPerClass = new int[4];
            int liAgainstRule = 0;
            int liCreatures = 0;

            for (int liId = 0; liId < 512; liId++)
            {
                UWCommonObjectProperties.Entry lOEntry;

                if (!lOCommon.TryGet(liId, out lOEntry))
                    break;

                liPerClass[lOEntry.QuantityClass]++;

                UWObject lOFresh = UWObjectFactory.Create(liId, lOCommon);
                bool lbStack = lOEntry.QuantityClass == 0 || lOEntry.QuantityClass == 2;

                if (lOFresh.HasQuantity != lbStack || lOFresh.Quantity != (lbStack ? 1 : 0))
                    liAgainstRule++;

                if (UWObjectIds.IsCreature(liId) && lOFresh is UWNpc)
                    liCreatures++;
            }

            Check.Int("factory: every id follows the stack rule (0 exceptions)", liAgainstRule, 0);
            Check.Int("factory: a creature id gives the mobile record (64 of them)", liCreatures, 64);
            // The shipped COMOBJ.DAT: 257 ids of class 0, 237 of class 1, none of class 2 and
            // 18 of class 3 (read 2026-10-12).
            Check.Int("factory: ids of stack class 0 (a count)", liPerClass[0], 257);
            Check.Int("factory: ids of stack class 1", liPerClass[1], 237);
            Check.Int("factory: ids of stack class 2 (a count, none shipped)", liPerClass[2], 0);
            Check.Int("factory: ids of stack class 3", liPerClass[3], 18);

            UWObject lOStones = UWObjectFactory.Create(UWObjectMechanics.SlingStoneObjectId, lOCommon);

            Check.Bool("factory: a sling stone starts as a stack", lOStones.HasQuantity, true);
            Check.Int("factory: ... of one", lOStones.Quantity, 1);
            Check.Bool("factory: a dagger has no count", lODagger.HasQuantity, false);
            Check.Bool("factory: a fish starts as a stack of one (the catch)",
                UWObjectFactory.Create(0xB6, lOCommon).HasQuantity, true);
            Check.Bool("factory: the silver seed does", UWObjectFactory.Create(UWObjectIds.SilverSeed, lOCommon).HasQuantity, true);
            Check.Bool("factory: the silver tree does (SAVE3)", UWObjectFactory.Create(UWObjectIds.SilverTree, lOCommon).HasQuantity, true);
            Check.Bool("factory: the Cup of Wonder does too", UWObjectFactory.Create(UWObjectIds.CupOfWonder, lOCommon).HasQuantity, true);
            Check.Bool("factory: the Key of Truth does too", UWObjectFactory.Create(UWObjectIds.KeyOfTruth, lOCommon).HasQuantity, true);
            Check.Bool("factory: a move trigger has no count (its word is a link)",
                UWObjectFactory.Create(UWObjectMechanics.MoveTriggerId, lOCommon).HasQuantity, false);
            Check.Bool("factory: a ward trap has no count",
                UWObjectFactory.Create(UWObjectMechanics.WardTrapId, lOCommon).HasQuantity, false);

            // The caller's own count and quality.
            UWObject lOThree = UWObjectFactory.Create(UWObjectMechanics.SlingStoneObjectId, lOCommon, 3, 5);

            Check.Bool("factory: a count of three sets the bit", lOThree.HasQuantity, true);
            Check.Int("factory: ... and the count", lOThree.Quantity, 3);
            Check.Int("factory: a quality of five is kept", lOThree.Quality, 5);
            Check.Bool("factory: a count of zero gives no bit even for a stone",
                UWObjectFactory.Create(UWObjectMechanics.SlingStoneObjectId, lOCommon, 0).HasQuantity, false);
            Check.Int("factory: a quality beyond six bits is masked", UWObjectFactory.Create(1, lOCommon, UWObjectFactory.Unset, 0x7F).Quality, 0x3F);
            Check.Int("factory: NewQuantity of a stone", UWObjectFactory.NewQuantity(lOCommon, UWObjectMechanics.SlingStoneObjectId), 1);
            Check.Int("factory: NewQuantity of a dagger", UWObjectFactory.NewQuantity(lOCommon, 1), 0);
            Check.Bool("factory: an unknown id starts with none", UWObjectFactory.StartsWithQuantity(lOCommon, 5000), false);
            Check.Bool("factory: no table, no count", UWObjectFactory.StartsWithQuantity(null, 16), false);

            // The pictures.
            UWObject lOPictured = UWObjectFactory.Create(1, lOData);

            Check.Bool("appearance: a dagger from the data has its picture", lOPictured != null && lOPictured.Texture != null, true);
            Check.Bool("appearance: id 461 has none, so the data overload gives null", UWObjectFactory.Create(461, lOData) == null, true);

            UWObject lOPillar = UWObjectFactory.Create(352, lOCommon);

            lOPillar.Flags = 2;
            Check.Bool("appearance: the pillar wears a TMOBJ texture", UWObjectAppearance.Apply(lOPillar, lOData.Textures, null), true);
            Check.Bool("appearance: ... and keeps its OBJECTS.GR icon", lOPillar.Icon != null && lOPillar.Icon != lOPillar.Texture, true);
            Check.Bool("appearance: the pillar's texture is TMOBJ 2",
                lOPillar.Texture == lOData.Textures.GetTextureByType(UWTexture.TextureTypes.TMOBJ, 2), true);

            UWLevel lOLevel = pOContext.Level(0);
            UWObject lODoor = UWObjectFactory.Create(UWObjectIds.FirstClosedDoor, lOCommon);

            Check.Bool("appearance: a door away from a level keeps its icon as the picture",
                UWObjectAppearance.Apply(lODoor, lOData.Textures, null) && lODoor.Texture == lODoor.Icon, true);

            if (lOLevel != null)
            {
                Check.Bool("appearance: on level 1 the door takes the level's door texture",
                    UWObjectAppearance.Apply(lODoor, lOData.Textures, lOLevel.TextureInfos)
                    && lODoor.Texture == lOData.Textures.GetTextureByType(UWTexture.TextureTypes.DOORS,
                        lOLevel.TextureInfos[UWObjectAppearance.DoorTextureTableStart]), true);

                // The loader comes through the same code: the first door of level 1 wears what
                // Apply gives a fresh door with the same id.
                UWObject lOLoaded = null;

                foreach (UWObject lOAt in lOLevel.Masterlist)
                {
                    if (lOAt != null && lOAt.ID == UWObjectIds.FirstClosedDoor)
                    {
                        lOLoaded = lOAt;

                        break;
                    }
                }

                Check.Bool("appearance: the loader's door 320 wears the same", lOLoaded != null && lOLoaded.Texture == lODoor.Texture, true);
            }

            Check.Bool("appearance: Picture of an id beyond the file is null", UWObjectAppearance.Picture(lOData.Textures, 10000) == null, true);

            fCheckClone();
            fCheckGiveToPlayer(lOData);
        }

        /// <summary>UWInventoryModel.GiveToPlayer (B8 of the plan): the pointer when free, else
        /// the backpack, else the floor; with pbPreferBags the backpack first.</summary>
        private static void fCheckGiveToPlayer(DataImport pOData)
        {
            UWInventoryModel lOModel = new UWInventoryModel(() => pOData, () => null);
            UWObject lOFish = UWObjectFactory.Create(0xB6, pOData.CommonObjectProperties);
            UWObject lOCup = UWObjectFactory.Create(UWObjectIds.CupOfWonder, pOData.CommonObjectProperties);
            UWObject lOKey = UWObjectFactory.Create(UWObjectIds.KeyOfTruth, pOData.CommonObjectProperties);
            UWObject lOSeed = UWObjectFactory.Create(UWObjectIds.SilverSeed, pOData.CommonObjectProperties);

            Check.Bool("give: nothing goes nowhere", lOModel.GiveToPlayer(null, false) == UWInventoryModel.GiveOutcome.Floor, true);
            Check.Bool("give: a free pointer takes the fish", lOModel.GiveToPlayer(lOFish, false) == UWInventoryModel.GiveOutcome.Hand, true);
            Check.Bool("give: ... it hangs there", lOModel.CursorItem == lOFish, true);
            Check.Bool("give: with the pointer full the cup goes into the backpack",
                lOModel.GiveToPlayer(lOCup, false) == UWInventoryModel.GiveOutcome.Stored, true);
            Check.Bool("give: ... first place", lOModel.Backpack[0] == lOCup, true);
            Check.Bool("give: ... and the fish stays on the pointer", lOModel.CursorItem == lOFish, true);

            for (int liSlot = 0; liSlot < lOModel.Backpack.Length; liSlot++)
                if (lOModel.Backpack[liSlot] == null)
                    lOModel.Backpack[liSlot] = new UWObject(1);

            Check.Bool("give: pointer and backpack full, the key goes to the floor",
                lOModel.GiveToPlayer(lOKey, false) == UWInventoryModel.GiveOutcome.Floor, true);
            Check.Bool("give: ... nothing in the inventory changed", lOModel.CursorItem == lOFish && lOModel.Backpack[0] == lOCup, true);

            // Bags first (the modern scheme's seed).
            lOModel.TakeCursorItem();
            Check.Bool("give: bags first with a full backpack and a free pointer takes the pointer",
                lOModel.GiveToPlayer(lOSeed, true) == UWInventoryModel.GiveOutcome.Hand, true);
            Check.Bool("give: ... the seed hangs there", lOModel.CursorItem == lOSeed, true);
            Check.Bool("give: bags first with everything full goes to the floor",
                lOModel.GiveToPlayer(lOKey, true) == UWInventoryModel.GiveOutcome.Floor, true);

            lOModel.TakeCursorItem();
            lOModel.Backpack[5] = null;
            Check.Bool("give: bags first with room puts the seed away",
                lOModel.GiveToPlayer(lOSeed, true) == UWInventoryModel.GiveOutcome.Stored, true);
            Check.Bool("give: ... in the free place", lOModel.Backpack[5] == lOSeed, true);
            Check.Bool("give: ... and the pointer stays free", lOModel.CursorItem == null, true);

            lOModel.Backpack[5] = null;
            lOModel.BeginDragFromExternal(lOFish);
            Check.Bool("give: bags first with the pointer full still finds the backpack",
                lOModel.GiveToPlayer(lOKey, true) == UWInventoryModel.GiveOutcome.Stored, true);
            Check.Bool("give: ... there", lOModel.Backpack[5] == lOKey, true);
        }

        /// <summary>The create-object trap's copy (TRIGGER.C TRAP_CREATE_OBJECT, UWObject.Clone):
        /// the record, and of the template's chain only the first item, copied, with its own
        /// chain cut. On a hand-built object list: record 1 a chest whose chain starts at 2 (a
        /// stack of five stones), then 3 (a dagger); record 4 a wand whose chain is the spell at
        /// 5; record 6 a bag holding the wand.</summary>
        private static void fCheckClone()
        {
            List<UWObject> lOMaster = new List<UWObject>();

            for (int liAt = 0; liAt < 8; liAt++)
                lOMaster.Add(null);

            UWObject lOChest = new UWObject(0x80) { Quantity = 2, Quality = 7, Flags = 3 };
            UWObject lOStones = new UWObject(0x10) { HasQuantity = true, Quantity = 5, Link = 3 };
            UWObject lODagger = new UWObject(1);
            UWObject lOWand = new UWObject(0x98) { Quantity = 5 };
            UWObject lOSpell = new UWObject(0x120);
            UWObject lOBag = new UWObject(0x80) { Quantity = 4 };

            lOMaster[1] = lOChest;
            lOMaster[2] = lOStones;
            lOMaster[3] = lODagger;
            lOMaster[4] = lOWand;
            lOMaster[5] = lOSpell;
            lOMaster[6] = lOBag;

            UWObject lOCopy = UWObject.Clone(lOChest, lOMaster);

            Check.Bool("clone: a new record", lOCopy != null && lOCopy != lOChest, true);
            Check.Int("clone: the fields come along (quality)", lOCopy.Quality, 7);
            Check.Int("clone: ... (flags)", lOCopy.Flags, 3);
            Check.Bool("clone: the template's chain was resolved for it", lOChest.Contents != null && lOChest.Contents.Count == 2, true);
            Check.Bool("clone: the copy has a list of its own", lOCopy.Contents != null && lOCopy.Contents != lOChest.Contents, true);
            Check.Int("clone: ... holding the first item only", lOCopy.Contents.Count, 1);
            Check.Bool("clone: ... which is a copy, not the template's stones", lOCopy.Contents[0] != lOStones, true);
            Check.Int("clone: ... of the same kind", lOCopy.Contents[0].ID, 0x10);
            Check.Int("clone: ... and count", lOCopy.Contents[0].Quantity, 5);
            Check.Int("clone: ... with its next link cut", lOCopy.Contents[0].Link, 0);
            Check.Int("clone: the copy's own link word is clear (the writer rebuilds it)", lOCopy.Quantity, 0);
            Check.Bool("clone: the template keeps both items", lOChest.Contents.Count == 2 && lOChest.Contents[1] == lODagger, true);

            // A bag holding a wand: the wand comes along, its spell does not.
            UWObject lOBagCopy = UWObject.Clone(lOBag, lOMaster);

            Check.Int("clone: the bag's copy holds the wand", lOBagCopy.Contents.Count == 1 ? lOBagCopy.Contents[0].ID : -1, 0x98);
            Check.Int("clone: ... without its spell (link word cut)", lOBagCopy.Contents[0].Quantity, 0);
            Check.Bool("clone: ... and an empty list of its own", lOBagCopy.Contents[0].Contents != null && lOBagCopy.Contents[0].Contents.Count == 0, true);
            Check.Bool("clone: the template's wand still has its spell", lOWand.Quantity == 5 && lOWand.Contents == null, true);

            // A stack is copied as it is; without the list a template with an unresolved chain
            // keeps its fields (the door trap's lock).
            UWObject lOStonesCopy = UWObject.Clone(lOStones);

            Check.Bool("clone: a stack copies as it is", lOStonesCopy.HasQuantity && lOStonesCopy.Quantity == 5 && lOStonesCopy.Contents == null, true);

            UWObject lOBlind = UWObject.Clone(lOWand);

            Check.Bool("clone: without the list an unresolved chain is left alone", lOBlind.Quantity == 5 && lOBlind.Contents == null, true);

            // A creature's nineteen bytes are its own, and its inventory follows the same rule.
            UWNpc lOGoblin = new UWNpc(0x40) { Quantity = 2, RawNpcBytes = new byte[19] };

            lOGoblin.RawNpcBytes[3] = 9;

            UWNpc lOGoblinCopy = UWObject.Clone(lOGoblin, lOMaster) as UWNpc;

            Check.Bool("clone: a creature gives a creature", lOGoblinCopy != null, true);
            Check.Bool("clone: ... with bytes of its own", lOGoblinCopy.RawNpcBytes != lOGoblin.RawNpcBytes && lOGoblinCopy.RawNpcBytes[3] == 9, true);
            Check.Int("clone: ... carrying the first item of its template only", lOGoblinCopy.Contents.Count, 1);
        }
    }
}
