using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 2.4 (2026-10-12): UWItemTargetRules (which carried item asks for which
    /// target and what it does there) and UWWorldUseRules (the own effect of a thing used in the
    /// world), pinned with the answers the four id lists of UWItemDrag and Interaction gave
    /// before they were folded into the two tables, and the wording of the messages they print.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckItemTargetRules(DataImport pOData)
        {
            Check.Group("Item target rules (UWItemTargetRules, UWWorldUseRules)");

            fCheckItemTargetClassify(pOData);
            fCheckItemTargetTable();
            fCheckWorldUseTable();

            Check.Str("message 158", UWText.General(pOData, UWItemTargetRules.PoleTriggersSwitchMessage),
                "Using the pole you trigger the switch.");
            Check.Str("message 153", UWText.General(pOData, UWClickRules.CannotUseThatMessage), "You cannot use that.");
            Check.Str("message 124", UWText.General(pOData, UWWorldUseRules.LightsOnlyEquippedMessage),
                "Lights may only be used if equipped.");
            Check.Str("message 275", UWText.General(pOData, UWObjectMechanics.CauldronMessageIndex), "The cauldron is empty.");
            Check.Str("refusal with the item's name", UWItemMessages.CannotBeUsedOnThat(pOData, UWObjectMechanics.SpikeObjectId),
                "The spike cannot be used on that.");

            // The tools' own refusals (C4, 2026-10-15).
            Check.Str("message 159, the pole's", UWText.General(pOData, UWItemTargetRules.PoleCannotBeUsedMessage),
                "The pole cannot be used on that.");
            Check.Str("message 129, the spike's", UWText.General(pOData,
                UWItemApplications.SpikeDoor(new UWObject(0x80), false).MessageIndex), "You can only spike closed doors.");
            Check.Str("message 4, the lockpick's on a thing without a lock", UWText.General(pOData,
                UWLockRules.PickLock(null, new UWObject(0x80), UWLockRules.FindLoadedLock(new UWObject(0x80)), 30).MessageIndex),
                "There is no lock on that.");
            Check.Int("... and the key's the same line", UWLockRules.ToggleWithKey(null, null, null, false,
                new UWObject(ItemTargetKeyId)), 4);
            Check.Str("message 133, bones, orb rock, Key of Infinity", UWText.General(pOData, UWStringIds.NoEffectMessage),
                "It seems to have no effect.");
            fExpectBool("use mode: the pole on a carried thing stays",
                UWItemTargetRules.KeepsUseMode(UWItemTargetRules.Action.PoleOnCarried), true);
            fExpectBool("use mode: the spike on a carried thing ends it",
                UWItemTargetRules.KeepsUseMode(UWItemTargetRules.Action.Spike), false);
            fExpectBool("use mode: the pole on a world thing ends it",
                UWItemTargetRules.KeepsUseMode(UWItemTargetRules.Action.FireSwitch), false);
        }

        private const int ItemTargetKeyId = 0x102;

        private const int ItemTargetSwordId = 0x04;

        private static void fCheckItemTargetClassify(DataImport pOData)
        {
            fExpectClassify(pOData, "pole", UWObjectMechanics.PoleObjectId, UWItemTargetRules.TargetKind.World);
            fExpectClassify(pOData, "spike", UWObjectMechanics.SpikeObjectId, UWItemTargetRules.TargetKind.World);
            fExpectClassify(pOData, "rock hammer", UWObjectMechanics.RockHammerObjectId, UWItemTargetRules.TargetKind.World);
            fExpectClassify(pOData, "orb rock", UWObjectMechanics.OrbRockObjectId, UWItemTargetRules.TargetKind.World);
            fExpectClassify(pOData, "Key of Infinity", UWObjectMechanics.KeyOfInfinityObjectId, UWItemTargetRules.TargetKind.World);
            fExpectClassify(pOData, "bones", UWEndgameRules.FirstBonesObjectId, UWItemTargetRules.TargetKind.World);
            fExpectClassify(pOData, "a key", ItemTargetKeyId, UWItemTargetRules.TargetKind.World);
            fExpectClassify(pOData, "the lockpick", UWObjectMechanics.LockpickObjectId, UWItemTargetRules.TargetKind.World);
            fExpectClassify(pOData, "the oil flask", UWObjectMechanics.OilFlaskObjectId, UWItemTargetRules.TargetKind.Carried);
            fExpectClassify(pOData, "the lock object", UWObjectMechanics.LockObjectId, UWItemTargetRules.TargetKind.None);
            fExpectClassify(pOData, "the anvil, carried", UWObjectMechanics.AnvilObjectId, UWItemTargetRules.TargetKind.None);
            fExpectClassify(pOData, "a torch (at once)", UWObjectMechanics.TorchObjectId, UWItemTargetRules.TargetKind.None);
            fExpectClassify(pOData, "a sword", ItemTargetSwordId, UWItemTargetRules.TargetKind.None);
            fExpectInt("classify: nothing asks for nothing", (int)UWItemTargetRules.Classify(null, pOData),
                (int)UWItemTargetRules.TargetKind.None);

            fExpectBool("use mode: the oil flask takes a carried target",
                UWItemTargetRules.TakesCarriedTarget(UWObjectMechanics.OilFlaskObjectId), true);
            fExpectBool("use mode: the anvil too", UWItemTargetRules.TakesCarriedTarget(UWObjectMechanics.AnvilObjectId), true);
            fExpectBool("use mode: the pole does not", UWItemTargetRules.TakesCarriedTarget(UWObjectMechanics.PoleObjectId), false);

            fExpectInt("sort: a door", (int)UWItemTargetRules.SortOf(0x140, true), (int)UWItemTargetRules.TargetSort.Door);
            fExpectInt("sort: a chest", (int)UWItemTargetRules.SortOf(UWObjectMechanics.ChestObjectId, false),
                (int)UWItemTargetRules.TargetSort.WorldContainer);
            fExpectInt("sort: the nightstand", (int)UWItemTargetRules.SortOf(UWObjectMechanics.NightstandObjectId, false),
                (int)UWItemTargetRules.TargetSort.WorldContainer);
            fExpectInt("sort: a sack is a thing", (int)UWItemTargetRules.SortOf(0x80, false), (int)UWItemTargetRules.TargetSort.Thing);
        }

        private static void fExpectClassify(DataImport pOData, string psName, int piId, UWItemTargetRules.TargetKind peExpected)
        {
            fExpectInt("classify: " + psName + " asks for " + peExpected,
                (int)UWItemTargetRules.Classify(new UWObject((ushort)piId), pOData), (int)peExpected);
        }

        /// <summary>Every item against every sort of target, as fTryUseItemOnTarget and the anvil
        /// and oil branches answered.</summary>
        private static void fCheckItemTargetTable()
        {
            UWItemTargetRules.TargetSort[] lOSorts =
            {
                UWItemTargetRules.TargetSort.Thing, UWItemTargetRules.TargetSort.Door,
                UWItemTargetRules.TargetSort.WorldContainer, UWItemTargetRules.TargetSort.Carried
            };

            // The tools' own refusals on carried things too (C4, decided 2026-10-15): mous_in_inv
            // calls the same second half as mous_in_3d.
            fExpectRow("pole", UWObjectMechanics.PoleObjectId, lOSorts, UWItemTargetRules.Action.FireSwitch,
                UWItemTargetRules.Action.FireSwitch, UWItemTargetRules.Action.FireSwitch, UWItemTargetRules.Action.PoleOnCarried);
            fExpectRow("lockpick", UWObjectMechanics.LockpickObjectId, lOSorts, UWItemTargetRules.Action.PickLock,
                UWItemTargetRules.Action.PickLock, UWItemTargetRules.Action.PickLock, UWItemTargetRules.Action.PickLock);
            fExpectRow("key", ItemTargetKeyId, lOSorts, UWItemTargetRules.Action.ToggleLock,
                UWItemTargetRules.Action.ToggleLock, UWItemTargetRules.Action.ToggleLock, UWItemTargetRules.Action.ToggleLock);
            fExpectRow("Key of Infinity", UWObjectMechanics.KeyOfInfinityObjectId, lOSorts, UWItemTargetRules.Action.KeyOfInfinity,
                UWItemTargetRules.Action.KeyOfInfinity, UWItemTargetRules.Action.KeyOfInfinity, UWItemTargetRules.Action.NoEffect);
            fExpectRow("bones", UWEndgameRules.FirstBonesObjectId, lOSorts, UWItemTargetRules.Action.Bones,
                UWItemTargetRules.Action.Bones, UWItemTargetRules.Action.Bones, UWItemTargetRules.Action.NoEffect);
            fExpectRow("spike", UWObjectMechanics.SpikeObjectId, lOSorts, UWItemTargetRules.Action.Spike,
                UWItemTargetRules.Action.Spike, UWItemTargetRules.Action.Spike, UWItemTargetRules.Action.Spike);
            fExpectRow("rock hammer", UWObjectMechanics.RockHammerObjectId, lOSorts, UWItemTargetRules.Action.BreakRock,
                UWItemTargetRules.Action.BreakRock, UWItemTargetRules.Action.BreakRock, UWItemTargetRules.Action.Ignore);
            fExpectRow("orb rock", UWObjectMechanics.OrbRockObjectId, lOSorts, UWItemTargetRules.Action.OrbRock,
                UWItemTargetRules.Action.OrbRock, UWItemTargetRules.Action.OrbRock, UWItemTargetRules.Action.NoEffect);
            fExpectRow("anvil", UWObjectMechanics.AnvilObjectId, lOSorts, UWItemTargetRules.Action.Ignore,
                UWItemTargetRules.Action.Ignore, UWItemTargetRules.Action.Ignore, UWItemTargetRules.Action.Repair);
            fExpectRow("oil flask", UWObjectMechanics.OilFlaskObjectId, lOSorts, UWItemTargetRules.Action.Ignore,
                UWItemTargetRules.Action.Ignore, UWItemTargetRules.Action.Ignore, UWItemTargetRules.Action.Oil);
            fExpectRow("sword", ItemTargetSwordId, lOSorts, UWItemTargetRules.Action.NotApplicable,
                UWItemTargetRules.Action.NotApplicable, UWItemTargetRules.Action.NotApplicable, UWItemTargetRules.Action.Ignore);
        }

        private static void fExpectRow(string psName, int piId, UWItemTargetRules.TargetSort[] pOSorts,
            params UWItemTargetRules.Action[] pOExpected)
        {
            UWObject lOItem = new UWObject((ushort)piId);

            for (int liAt = 0; liAt < pOSorts.Length; liAt++)
            {
                fExpectInt("on target: " + psName + " on " + pOSorts[liAt] + " -> " + pOExpected[liAt],
                    (int)UWItemTargetRules.OnTarget(lOItem, pOSorts[liAt]), (int)pOExpected[liAt]);
            }
        }

        /// <summary>The cases of the former Interaction.fTryUseObjectEffect, and the use trigger
        /// that a container leaves to its spill.</summary>
        private static void fCheckWorldUseTable()
        {
            fExpectWorldUse("gravestone", UWEndgameRules.GravestoneObjectId, UWWorldUseRules.Effect.Gravestone);
            fExpectWorldUse("fountain", UWObjectMechanics.FountainObjectId, UWWorldUseRules.Effect.Fountain);
            fExpectWorldUse("fountain water", UWObjectMechanics.FountainWaterObjectId, UWWorldUseRules.Effect.Fountain);
            fExpectWorldUse("silver tree", UWSilverTreeRules.TreeObjectId, UWWorldUseRules.Effect.SilverTree);
            fExpectWorldUse("cauldron", UWObjectMechanics.CauldronObjectId, UWWorldUseRules.Effect.Cauldron);
            fExpectWorldUse("shrine", UWShrineRules.ShrineObjectId, UWWorldUseRules.Effect.Shrine);
            fExpectWorldUse("anvil", UWObjectMechanics.AnvilObjectId, UWWorldUseRules.Effect.UseModeFromWorld);
            fExpectWorldUse("pole on the floor", UWObjectMechanics.PoleObjectId, UWWorldUseRules.Effect.UseModeFromWorld);
            fExpectWorldUse("barrel", UWObjectMechanics.BarrelObjectId, UWWorldUseRules.Effect.Spill);
            fExpectWorldUse("chest", UWObjectMechanics.ChestObjectId, UWWorldUseRules.Effect.Spill);
            fExpectWorldUse("nightstand (does nothing)", UWObjectMechanics.NightstandObjectId, UWWorldUseRules.Effect.None);
            fExpectWorldUse("a sack", 0x80, UWWorldUseRules.Effect.Spill);
            fExpectWorldUse("a container of major class 2 (0x8B)", 0x8B, UWWorldUseRules.Effect.Spill);
            fExpectWorldUse("an unlit torch", UWObjectMechanics.TorchObjectId, UWWorldUseRules.Effect.LightsOnlyEquipped);
            fExpectWorldUse("a lit torch", UWObjectMechanics.TorchObjectId + UWObjectMechanics.LitLightOffset,
                UWWorldUseRules.Effect.LightsOnlyEquipped);
            fExpectWorldUse("a sword", ItemTargetSwordId, UWWorldUseRules.Effect.None);
            fExpectWorldUse("the bedroll (does nothing)", UWItemUse.BedrollObjectId, UWWorldUseRules.Effect.None);
            fExpectWorldUse("a glowing rock (gathered, A48)", UWGlowingRockRules.GlowingRockId, UWWorldUseRules.Effect.GlowingRock);
            fExpectWorldUse("burning incense (its vision, A48)", UWItemUse.BurningIncenseObjectId,
                UWWorldUseRules.Effect.BurningIncense);

            fExpectBool("use trigger: a switch's (0x170) fires first",
                UWWorldUseRules.UseTriggerFiresFirst(0x170, new UWObject(0x170).GetCategory()), true);
            fExpectBool("use trigger: a chest's waits for the spill",
                UWWorldUseRules.UseTriggerFiresFirst(UWObjectMechanics.ChestObjectId,
                    new UWObject((ushort)UWObjectMechanics.ChestObjectId).GetCategory()), false);
            fExpectBool("use trigger: so does a sack's",
                UWWorldUseRules.UseTriggerFiresFirst(0x80, new UWObject(0x80).GetCategory()), false);
            fExpectBool("use trigger: and the nightstand's",
                UWWorldUseRules.UseTriggerFiresFirst(UWObjectMechanics.NightstandObjectId,
                    new UWObject((ushort)UWObjectMechanics.NightstandObjectId).GetCategory()), false);
        }

        private static void fExpectWorldUse(string psName, int piId, UWWorldUseRules.Effect peExpected)
        {
            fExpectInt("world use: " + psName + " -> " + peExpected,
                (int)UWWorldUseRules.Classify(piId, new UWObject((ushort)piId).GetCategory()), (int)peExpected);
        }
    }
}
