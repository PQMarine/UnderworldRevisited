using System;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Single items and what becomes of them: the remains in water, the bowl and the stew, the
    /// glowing rock, the exploding book, the equipment's wear, the moonstone, the item spells of
    /// class 13 and the locks.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>
        /// Why a drowned creature leaves nothing, measured by the user in the original on
        /// 2026-09-20: the death removal runs as after any other death (asm lines 53055-53080
        /// call DropNPCLoot, DropNPCRemains and SpillCritterInventory whatever killed the
        /// creature), and the WATER swallows what it puts down. Three measurements: drowned in
        /// the middle of the water nothing at all is left; drowned close to the shore a piece
        /// of meat stayed lying on the land while what fell into the water was gone, and
        /// without a sound of its own - the single splash of a drowning is the creature's own;
        /// loot of a creature killed at the water's EDGE that falls in splashes on landing.
        ///
        /// What decides in the port is UWCommonObjectProperties.SinksInLiquid (COMOBJ byte 9
        /// below 40, from the user's throwing tests of 2026-08-31), applied in
        /// UWLevelLoader.fSpawnObjectAt - the one place every dropped, thrown, scattered and
        /// spilled object passes. Pinned here for what a death actually puts down.
        /// </summary>
        private static void fCheckWaterSwallowsRemains(DataImport pOData)
        {
            Check.Group("Water and what a death leaves");

            if (pOData.CommonObjectProperties == null)
                return;

            // The goblin's row: blood index 5 and corpse index 6 (OBJECTS.DAT byte 8 bits 5-7
            // and byte 0x0A bits 2-4 of critter 7, item id 71 - the creature the user drowned).
            UWObjectClassProperties.Critter lOGoblin;

            fExpectBool("water: the goblin's critter row is there",
                pOData.ObjectClassProperties.TryGetCritter(0x47, out lOGoblin), true);
            fExpectInt("water: the goblin's blood is object 221",
                UWCritterCombat.GetBloodObjectId(lOGoblin.BloodIndex), 0xDD);
            fExpectInt("water: its corpse is object 198",
                UWCritterCombat.GetCorpseObjectId(lOGoblin.CorpseIndex), 0xC6);

            foreach (int liObjectId in new[] { 0xDD, 0xC6, MeatObjectId })
            {
                UWCommonObjectProperties.Entry lOEntry;

                fExpectBool("water: object " + liObjectId + " is in COMOBJ",
                    pOData.CommonObjectProperties.TryGet(liObjectId, out lOEntry), true);
                fExpectBool("water: object " + liObjectId + " sinks, so deep water swallows it",
                    lOEntry.SinksInLiquid, true);
            }

            // The counter-case of the near-shore drowning: the same objects on DRY LAND stay,
            // because only a water or lava tile destroys (UWLevelLoader.IsDestructiveTile).
            fExpectBool("water: nothing sinks on dry land - the tile decides, not the object",
                UWCommonObjectProperties.LiquidSurvivalThreshold > 0, true);

            // THE CULLING, pinned against the user's throwing tests in the original: ten wands
            // and ten emeralds sank, eight of 27 keys stayed (2026-09-21), sixteen stacks of five
            // emeralds all stayed (2026-10-03), and the heavy things of 2026-08-31 stayed. Range
            // 10 to 12, a thrown object tested twice in water. The ids are the port's (one below the string entry):
            // 153 wand, 167 emerald, 258 key, 168 large gem.
            foreach (var lOCase in new[]
            {
                new { Id = 153, Priority = 10, Name = "wand" },
                new { Id = 167, Priority = 11, Name = "emerald" },
                new { Id = 258, Priority = 12, Name = "key" },
                new { Id = 168, Priority = 15, Name = "large gem" }
            })
            {
                UWCommonObjectProperties.Entry lOEntry;

                if (pOData.CommonObjectProperties.TryGet(lOCase.Id, out lOEntry))
                    fExpectInt("culling: the " + lOCase.Name + " has priority " + lOCase.Priority,
                        lOEntry.CullingPriority, lOCase.Priority);
            }

            fExpectInt("culling: the range starts at the callers' 0x0A",
                UWLiquidCulling.RangeBase, 10);
            fExpectInt("culling: a thrown object is tested twice in water",
                UWLiquidCulling.ThrownIntoWaterTests, 2);
            fExpectBool("culling: the wand always goes under",
                UWLiquidCulling.AlwaysSwallows(10), true);
            fExpectBool("culling: the emerald not always",
                UWLiquidCulling.AlwaysSwallows(11), false);
            fExpectBool("culling: the emerald at the lowest range stays",
                UWLiquidCulling.Swallows(11, 1, UWLiquidCulling.RangeBase), false);
            fExpectBool("culling: the key at the middle range stays",
                UWLiquidCulling.Swallows(12, 1, UWLiquidCulling.RangeBase + 1), false);
            fExpectBool("culling: the key at the highest range goes under",
                UWLiquidCulling.Swallows(12, 1, UWLiquidCulling.RangeBase + 2), true);
            fExpectBool("culling: the large gem never goes under",
                UWLiquidCulling.NeverSwallows(15), true);

            // Lava spares quality class 3 and fire-resistant objects before it culls (value 6 of
            // PlacedObjectCollison, read 2026-09-24): Tybal's keys, incense and thread stay,
            // a scroll burns (per user in the original).
            foreach (var lOCase in new[]
            {
                new { Id = 260, Spared = true, Name = "Tybal's first key" },
                new { Id = 278, Spared = true, Name = "block of incense" },
                new { Id = 284, Spared = true, Name = "strong thread" },
                new { Id = 314, Spared = false, Name = "scroll" }
            })
            {
                UWCommonObjectProperties.Entry lOEntry;

                if (pOData.CommonObjectProperties.TryGet(lOCase.Id, out lOEntry))
                    fExpectBool("lava: the " + lOCase.Name + (lOCase.Spared ? " is spared" : " is culled"),
                        UWLiquidCulling.SparedByLava(lOEntry.QualityClass, lOEntry.Resistances), lOCase.Spared);
            }

            fExpectBool("lava: fire resistance alone spares",
                UWLiquidCulling.SparedByLava(0, UWLiquidCulling.FireResistanceBit), true);

            // The stack bonus of ObjectCullingTest: half the EXTRA count counts as priority - a
            // stack of five emeralds is 13 and survives every range (the measurement of
            // 2026-10-03).
            fExpectBool("culling: five of the emerald survive the highest range",
                UWLiquidCulling.Swallows(11, 5, UWLiquidCulling.RangeBase + 2), false);
            fExpectBool("culling: a single one does not",
                UWLiquidCulling.Swallows(11, 1, UWLiquidCulling.RangeBase + 2), true);

            // The second roll of the routine, read out 2026-09-22: it can save nothing at any
            // range the water landing produces, which is why it is not built.
            fExpectInt("culling: the second roll runs over ten values",
                UWLiquidCulling.SecondChanceDice, 10);
            fExpectBool("culling: at the lowest range it already saves nothing",
                UWLiquidCulling.SecondChanceCouldSave(UWLiquidCulling.RangeBase), false);
            fExpectBool("culling: it would only matter below ten",
                UWLiquidCulling.SecondChanceCouldSave(UWLiquidCulling.SecondChanceDice - 1), true);

            // THE SPLASH of something that falls in (built 2026-09-21): class-7 offset 6, so
            // 0x1C6, and string block 4 must name it. The same picture serves the drowning and
            // the falling object - the two places the original spawns it.
            fExpectInt("splash: the picture is object 454",
                UWObjectMechanics.SplashEffectObjectId, 0x1C6);
            fExpectInt("splash: which is the class-7 base plus six",
                UWObjectMechanics.SplashEffectObjectId - UWObjectMechanics.BloodEffectObjectId, 6);

            UWStringBlock lOObjectNames;

            if (pOData.Strings != null && pOData.Strings.Blocks != null
                && pOData.Strings.Blocks.TryGetValue(4, out lOObjectNames)
                && lOObjectNames.Strings != null
                && lOObjectNames.Strings.Count > UWObjectMechanics.SplashEffectObjectId)
            {
                fExpectBool("splash: entry 454 of string block 4 names it",
                    lOObjectNames.Strings[UWObjectMechanics.SplashEffectObjectId]
                        .IndexOf("splash", StringComparison.OrdinalIgnoreCase) >= 0, true);
            }
        }

        /// <summary>"a_piece of meat", the loot that stayed on the land in the user's
        /// near-shore drowning test of 2026-09-20.</summary>
        private const int MeatObjectId = 0xB1;

        /// <summary>
        /// THE BOWL AND THE STEW (read 2026-09-28): the container masks as the original tests them
        /// (UWContainerCapacity) and the recipe's mixing (UWStewRules).
        /// </summary>
        private static void fCheckBowlAndStew(DataImport pOData)
        {
            UWObject fItem(int piId)
            {
                return new UWObject((ushort)piId);
            }

            fExpectBool("bowl: takes the dead rotworm",
                UWContainerCapacity.Check(fItem(UWStewRules.BowlObjectId), fItem(0xD9), pOData) == UWContainerCapacity.ResultEnum.Fits, true);
            fExpectBool("bowl: takes a plant (0xCE)",
                UWContainerCapacity.Check(fItem(UWStewRules.BowlObjectId), fItem(0xCE), pOData) == UWContainerCapacity.ResultEnum.Fits, true);
            fExpectBool("bowl: refuses a key (0x100)",
                UWContainerCapacity.Check(fItem(UWStewRules.BowlObjectId), fItem(0x100), pOData) == UWContainerCapacity.ResultEnum.WrongType, true);
            fExpectBool("quiver: refuses the plain stone (0x13)",
                UWContainerCapacity.Check(fItem(0x8D), fItem(0x13), pOData) == UWContainerCapacity.ResultEnum.WrongType, true);
            fExpectBool("quiver: takes an arrow (0x12)",
                UWContainerCapacity.Check(fItem(0x8D), fItem(0x12), pOData) == UWContainerCapacity.ResultEnum.Fits, true);

            UWObject fBowl(params int[] piContents)
            {
                UWObject lOBowl = fItem(UWStewRules.BowlObjectId);

                lOBowl.EnsureContentsLoaded(null);

                foreach (int liId in piContents)
                    lOBowl.Contents.Add(fItem(liId));

                return lOBowl;
            }

            int fMix(UWObject pOBowl)
            {
                UWInventoryModel lOInventory = new UWInventoryModel(() => pOData, () => null);

                lOInventory.Backpack[0] = pOBowl;

                return UWStewRules.TryMix(lOInventory, pOData);
            }

            UWObject lORight = fBowl(0xBE, 0xD9, 0xB8, 0xB8);

            fExpectInt("stew: rotworm, mushroom and port mix", fMix(lORight), UWStewRules.MixedMessage);
            fExpectInt("stew: the bowl becomes the stew", lORight.ID, UWStewRules.RotwormStewObjectId);
            fExpectInt("stew: the ingredients are gone", lORight.Contents.Count, 0);
            fExpectInt("stew: without the port nothing", fMix(fBowl(0xD9, 0xB8)), UWStewRules.WrongIngredientsMessage);
            fExpectInt("stew: a stranger in the bowl spoils it", fMix(fBowl(0xD9, 0xB8, 0xBE, 0xB0)), UWStewRules.WrongIngredientsMessage);
            fExpectInt("stew: no bowl", fMix(null), UWStewRules.NoBowlMessage);

            UWObject lORecipe = fItem(0x13E);

            lORecipe.Quantity = 0x301;
            lORecipe.HasQuantity = true;
            fExpectBool("stew: the scroll with link 0x301 is the recipe", UWStewRules.IsRecipe(lORecipe), true);
            lORecipe.Quantity = 0x220;
            fExpectBool("stew: a scroll with a text below 0x100 is not", UWStewRules.IsRecipe(lORecipe), false);
        }

        /// <summary>UWGlowingRockRules: the glowing rock is not solid, and walked over it joins the
        /// carried one - at the pointer first, else in the inventory; without one nothing.</summary>
        private static void fCheckGlowingRock(DataImport pOData)
        {
            UWObject fRock(int piQuantity)
            {
                UWObject lORock = new UWObject((ushort)UWGlowingRockRules.GlowingRockId);

                lORock.HasQuantity = true;
                lORock.Quantity = (ushort)piQuantity;

                return lORock;
            }

            if (pOData.CommonObjectProperties != null
                && pOData.CommonObjectProperties.TryGet(UWGlowingRockRules.GlowingRockId, out UWCommonObjectProperties.Entry lOEntry))
            {
                fExpectBool("glowing rock: has a height in COMOBJ", lOEntry.Height > 0, true);
                fExpectBool("glowing rock: but is not solid", lOEntry.IsSolid, false);
                fExpectBool("glowing rock: uses itself on whoever touches it", lOEntry.UsedWhenThrown, true);
            }

            UWInventoryModel lOEmpty = new UWInventoryModel(() => pOData, () => null);

            fExpectBool("glowing rock: without a carried one nothing happens",
                UWGlowingRockRules.TryGather(fRock(1), lOEmpty, null), false);

            UWInventoryModel lOInventory = new UWInventoryModel(() => pOData, () => null);
            UWObject lOCarried = fRock(2);

            lOInventory.Backpack[3] = lOCarried;
            fExpectBool("glowing rock: joins the one in the backpack",
                UWGlowingRockRules.TryGather(fRock(3), lOInventory, null), true);
            fExpectInt("glowing rock: the quantities add up", lOCarried.Quantity, 5);

            UWObject lOAtPointer = fRock(1);

            lOInventory.PickUpToCursor(lOAtPointer);
            UWGlowingRockRules.TryGather(fRock(4), lOInventory, null);
            fExpectInt("glowing rock: the one at the pointer takes it first", lOAtPointer.Quantity, 5);
            fExpectInt("glowing rock: the backpack one stays", lOCarried.Quantity, 5);
        }

        /// <summary>Bronus' book opened from the backpack: the sentence, quest flag 8, the curse
        /// with three dice, and the book is gone - a bag beside it stays.</summary>
        private static void fCheckExplodingBook(DataImport pOData)
        {
            UWInventoryModel lOInventory = new UWInventoryModel(() => pOData, () => null);
            UWObject lOBag = new UWObject(130);
            UWObject lOBook = new UWObject((ushort)UWExplodingBook.BookObjectId);
            BookHost lOHost = new BookHost();

            lOInventory.Backpack[0] = lOBag;
            lOInventory.Backpack[3] = lOBook;
            UWQuestFlags.Set(UWExplodingBook.QuestFlag, 0);

            fExpectBool("exploding book: used", UWItemUse.TryUseImmediately(lOBook, pOData, null, lOInventory, lOHost), true);
            fExpectBool("exploding book: the sentence", lOHost.Messages.Contains("The book explodes in your face!"), true);
            fExpectInt("exploding book: quest flag 8", UWQuestFlags.Get(UWExplodingBook.QuestFlag), 1);
            fExpectInt("exploding book: curse dice", lOHost.CurseDice, 3);
            fExpectBool("exploding book: the book is gone", lOHost.Consumed && lOInventory.Backpack[0] == lOBag, true);

            UWQuestFlags.Set(UWExplodingBook.QuestFlag, 0);
        }

        /// <summary>UWEquipmentWear (2026-09-28): the door roll, the weapon and armour tests, and
        /// the dagger of the golem test - quality 15, seven points of wear, 8 left.</summary>
        private static void fCheckEquipmentWear(DataImport pOData)
        {
            // frp_check (COMBAT.C): a blow at a thing always lands, never critical (B2, per user on
            // the original 2026-10-14: Attack and Unarmed 0, every blow at a door hit).
            int liHits = 0;
            int liCriticals = 0;

            for (int liTry = 0; liTry < 200; liTry++)
            {
                UWPlayerAttack.MeleeResult lOBlow = UWPlayerAttack.ResolveMelee(pOData, UWCombat.FistObjectId,
                    UWCombat.AttackKind.Bash, 1f, 10, 0, 10, 0, null, UWAttackResolution.ObjectDefender,
                    UWAttackResolution.NoPart);

                if (lOBlow.Hit)
                    liHits++;

                if (lOBlow.Check == UWSkillCheck.ResultEnum.CriticalSuccess)
                    liCriticals++;
            }

            fExpectInt("blow at a thing: 200 of 200 land with Attack 0", liHits, 200);
            fExpectInt("blow at a thing: never critical", liCriticals, 0);

            Check.Group("Equipment wear");

            fExpectBool("wear: a door with id and 7 = 0 never wears the weapon", UWEquipmentWear.WearsWeaponOnDoor(0x140, 0), false);
            fExpectBool("wear: door 0x147 at the highest roll (11 < 14)", UWEquipmentWear.WearsWeaponOnDoor(0x147, 0x7FFF), true);
            fExpectBool("wear: door 0x141 at a roll of 4 (4 < 2 fails)", UWEquipmentWear.WearsWeaponOnDoor(0x141, 0x2AAB), false);
            fExpectBool("wear: door 0x141 at the lowest roll", UWEquipmentWear.WearsWeaponOnDoor(0x141, 0), true);
            fExpectBool("wear: a chest is no door", UWEquipmentWear.WearsWeaponOnDoor(0x15D, 0), false);
            fExpectBool("wear: the dagger is a weapon", UWEquipmentWear.IsWearableWeapon(new UWObject(3)), true);
            fExpectBool("wear: a torch in the weapon hand is not", UWEquipmentWear.IsWearableWeapon(new UWObject(0x95)), false);
            fExpectBool("wear: the shield hand without a shield takes nothing",
                UWEquipmentWear.IsWearableArmour(new UWObject(3), UWArmorItemMap.BodySlot.LeftHandSlot), false);
            fExpectBool("wear: the helmet slot takes what is in it",
                UWEquipmentWear.IsWearableArmour(new UWObject(3), UWArmorItemMap.BodySlot.Helmet), true);

            // ObjWorn (INVDATA.C): the shield hand counts 0x2B-0x2F as worn too, 0x2A not.
            fExpectBool("wear: the shield hand takes a shield",
                UWEquipmentWear.IsWearableArmour(new UWObject(0x3B), UWArmorItemMap.BodySlot.LeftHandSlot), true);
            fExpectBool("wear: ... and a plate helmet",
                UWEquipmentWear.IsWearableArmour(new UWObject(0x2E), UWArmorItemMap.BodySlot.LeftHandSlot), true);
            fExpectBool("wear: ... and dragon skin boots",
                UWPlayerCritterRow.IsWornInShieldHand(0x2F), true);
            fExpectBool("wear: ... not leather boots 0x2A", UWPlayerCritterRow.IsWornInShieldHand(0x2A), false);
            fExpectBool("wear: ... not a ring 0x36", UWPlayerCritterRow.IsWornInShieldHand(0x36), false);
            fExpectInt("wear: savegame slot 8 is the left hand", (int)UWInventoryModel.FromSavegameSlot(8),
                (int)UWArmorItemMap.BodySlot.LeftHandSlot);

            UWObject lODagger = new UWObject(3) { Quality = 15 };

            fExpectInt("wear: the dagger is damaged by 7",
                (int)UWEquipmentWear.Wear(lODagger, 7, pOData, out string lsMessage), (int)UWEquipmentWear.Outcome.Damaged);
            fExpectInt("wear: 15 - 7 leaves 8", lODagger.Quality, 8);
            fExpectBool("wear: Your dagger was damaged.", lsMessage == "Your dagger was damaged.", true);
            fExpectInt("wear: 9 more destroy it",
                (int)UWEquipmentWear.Wear(lODagger, 9, pOData, out lsMessage), (int)UWEquipmentWear.Outcome.Destroyed);
            fExpectBool("wear: Your dagger was destroyed.", lsMessage == "Your dagger was destroyed.", true);
        }

        private static void fCheckMoonstone(DataImport pOData)
        {
            Check.Group("Moonstone");

            int liSaved = UWGameFlags.MoonstoneLevel;

            // A bag inside a bag, chained the way the level data are: the container's quantity
            // field points at its first content, the link at the next one.
            UWObject lOStone = new UWObject((ushort)UWMoonstoneRules.MoonstoneId);
            UWObject lOCandle = new UWObject(0x80) { Link = 1 };
            UWObject lOBag = new UWObject(0x80) { Quantity = 2 };
            UWObject lOOuter = new UWObject(0x80) { Quantity = 3 };
            System.Collections.Generic.List<UWObject> lOMaster = new System.Collections.Generic.List<UWObject>
                { new UWObject(0), lOStone, lOCandle, lOBag };
            lOOuter.EnsureContentsLoaded(lOMaster);
            lOBag.EnsureContentsLoaded(lOMaster);

            fExpectBool("moonstone: the stone itself counts", UWMoonstoneRules.IsOrContainsMoonstone(lOStone), true);
            fExpectBool("moonstone: a bag inside a bag holding it counts", UWMoonstoneRules.IsOrContainsMoonstone(lOOuter), true);
            fExpectBool("moonstone: an empty bag does not", UWMoonstoneRules.IsOrContainsMoonstone(new UWObject(0x80)), false);

            UWGameFlags.MoonstoneLevel = 2;
            UWMoonstoneRules.OnTakenFromWorld(lOOuter);
            fExpectInt("moonstone: taken from the world, nobody knows where", UWGameFlags.MoonstoneLevel, 0);
            UWMoonstoneRules.OnPutIntoWorld(lOOuter, 5);
            fExpectInt("moonstone: put down on level 5, Gate Travel goes there", UWGameFlags.MoonstoneLevel, 5);
            UWMoonstoneRules.OnPutIntoWorld(new UWObject(0x80), 3);
            fExpectInt("moonstone: something else put down changes nothing", UWGameFlags.MoonstoneLevel, 5);

            UWGameFlags.MoonstoneLevel = liSaved;

            fExpectBool("moonstone: level 2 holds it at the start",
                pOData.Levels != null && pOData.Levels.Count > 1 && UWMoonstoneRules.FindHolderInLevel(pOData.Levels[1]) != null, true);
        }

        private static void fCheckItemClassSpells(DataImport pOData)
        {
            Check.Group("Item spells of class 13");

            // The red potion on level 3 at 49/52 and the wand of the Frog on level 4 at 46/47 -
            // see UWItemSpellRules.
            fExpectInt("item spell: the red potion on level 3 carries Hallucination 13/5",
                fItemClassSpellAt(pOData, 2, 49, 52, 0xBB), 13 * 100 + 5);
            fExpectInt("item spell: the wand on level 4 carries the bullfrog reset 13/3",
                fItemClassSpellAt(pOData, 3, 46, 47, 0x9A), 13 * 100 + 3);

            // A rune spell is none of these: the potion of Lesser Heal form, 512 + 3.
            UWObject lOPotion = new UWObject(0xBB) { HasQuantity = true, Flags = 4 | UWObjectRecordCodec.EnchantedFlag, Quantity = 512 + 3 };
            int liMajor;
            int liMinor;
            fExpectBool("item spell: a rune spell is not a class spell",
                UWObjectMechanics.TryGetItemClassSpell(lOPotion, null, out liMajor, out liMinor), false);
        }

        /// <summary>Major * 100 + minor of the first object of that id in the tile, or -1.</summary>
        private static int fItemClassSpellAt(DataImport pOData, int piLevel, int piX, int piY, int piId)
        {
            UWLevel lOLevel = pOData.Levels != null && pOData.Levels.Count > piLevel ? pOData.Levels[piLevel] : null;
            UWTile lOTile = lOLevel != null ? lOLevel.GetTile(piX, piY) : null;

            if (lOTile == null || lOTile.ObjectsInTile == null)
                return -1;

            foreach (UWObject lOObject in lOTile.ObjectsInTile)
            {
                int liMajor;
                int liMinor;

                if (lOObject != null && lOObject.ID == piId
                    && UWObjectMechanics.TryGetItemClassSpell(lOObject, lOLevel.Masterlist, out liMajor, out liMinor))
                    return liMajor * 100 + liMinor;
            }

            return -1;
        }

        private static void fCheckLocks()
        {
            // THE DOOR CAPTURE LIFTS BOTH KINDS (2026-10-06): a door and a portcullis open 24
            // above their closed height and close back onto it - the original's animo raises the
            // grate by the same 0x18 that OpenDoor adds to a door at once (locks-traps notes).
            UWObject lODoor = new UWObject(0x141) { ZPos = 56 };
            UWWorldCapture.CaptureDoor(null, lODoor, true, true, false, 0, 0, null);
            fExpectInt("door capture: an opened door is the open id", lODoor.ID, 0x149);
            fExpectInt("door capture: ... 24 higher", lODoor.ZPos, 80);
            UWWorldCapture.CaptureDoor(null, lODoor, true, false, false, 0, 0, null);
            fExpectInt("door capture: ... and back on the floor when closed", lODoor.ZPos, 56);

            UWObject lOGrate = new UWObject(0x146) { ZPos = 96 };
            UWWorldCapture.CaptureDoor(null, lOGrate, true, true, false, 0, 0, null);
            fExpectInt("door capture: an opened portcullis is 0x14E", lOGrate.ID, 0x14E);
            fExpectInt("door capture: ... 24 higher as well (level 1 tile 9/35: floor 96, grate 120)", lOGrate.ZPos, 120);
            UWWorldCapture.CaptureDoor(null, lOGrate, true, false, false, 0, 0, null);
            fExpectInt("door capture: ... and down again when closed", lOGrate.ZPos, 96);

            Check.Group("Locks (UnlockDoor)");

            // A chest with a locked lock of id 5, difficulty 0, without the keep bit.
            UWObject lOLock = new UWObject((ushort)UWObjectMechanics.LockObjectId) { Flags = 1, Quantity = 5 };
            UWObject lOChest = new UWObject(0x15D) { Quantity = 1 };
            lOChest.EnsureContentsLoaded(new System.Collections.Generic.List<UWObject> { new UWObject(0), lOLock });

            fExpectInt("lock: a pick of skill 30 opens difficulty 0",
                (int)UWLockRules.Unlock(null, lOChest, lOLock, false, -30), (int)UWLockRules.Outcome.Unlocked);
            fExpectBool("lock: without the keep bit the lock is gone", lOChest.Contents.Contains(lOLock), false);

            UWObject lOKept = new UWObject((ushort)UWObjectMechanics.LockObjectId) { Flags = 3, Quantity = 5 };
            UWObject lOBarrel = new UWObject(0x15D) { Quantity = 1 };
            lOBarrel.EnsureContentsLoaded(new System.Collections.Generic.List<UWObject> { new UWObject(0), lOKept });

            fExpectInt("lock: a wrong key does not fit",
                (int)UWLockRules.Unlock(null, lOBarrel, lOKept, false, 4), (int)UWLockRules.Outcome.Failed);
            fExpectInt("lock: the right key unlocks",
                (int)UWLockRules.Unlock(null, lOBarrel, lOKept, false, 5), (int)UWLockRules.Outcome.Unlocked);
            fExpectBool("lock: with the keep bit the lock stays", lOBarrel.Contents.Contains(lOKept), true);
            fExpectInt("lock: a pick on an unlocked lock is 'not locked'",
                (int)UWLockRules.Unlock(null, lOBarrel, lOKept, false, -30), (int)UWLockRules.Outcome.NotLocked);
            fExpectInt("lock: the key does not lock an open container",
                (int)UWLockRules.Unlock(null, lOBarrel, lOKept, true, 5), (int)UWLockRules.Outcome.NotLocked);
            fExpectInt("lock: the key locks it again",
                (int)UWLockRules.Unlock(null, lOBarrel, lOKept, false, 5), (int)UWLockRules.Outcome.Locked);

            UWObject lOHard = new UWObject((ushort)UWObjectMechanics.LockObjectId) { Flags = 3, Quantity = 0, ZPos = 0xF };
            fExpectInt("lock: difficulty 15 withstands even the Unlock spell",
                (int)UWLockRules.Unlock(null, lOBarrel, lOHard, false, -UWLockRules.UnlockSpellSkill), (int)UWLockRules.Outcome.Failed);
            fExpectInt("lock: a lock of id 0 takes no key",
                (int)UWLockRules.Unlock(null, lOBarrel, lOHard, false, 1), (int)UWLockRules.Outcome.Failed);
        }
    }
}
