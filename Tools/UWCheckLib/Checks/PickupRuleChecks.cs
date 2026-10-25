using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 2.3 (2026-10-12): UWPickupRules, the one check behind taking a thing from the
    /// world and a bought good from the trade - the original's order (player_3dget: creature,
    /// reach, then the weight of what is taken), the messages by id and their wording, and
    /// decision 14 (the count-aware weight check, AskHowMany cuts the stack before EncumCheck).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckPickupRules(DataImport pOData)
        {
            Check.Group("Pickup rules (UWPickupRules: creature, reach, weight of what is taken)");

            UWCommonObjectProperties lOProperties = pOData.CommonObjectProperties;

            if (lOProperties == null || pOData.InitialPlayer == null)
            {
                Check.Fail("pickup rules", "no COMOBJ.DAT or no player");

                return;
            }

            // The template PLAYER.DAT carries no capacity; a character with 400 tenth-stones stands
            // in, as in the inventory model check. The runner reloads the game state afterwards.
            byte[] lyPlain = (byte[])pOData.InitialPlayer.PlainBytes.Clone();

            UWBinary.Write16(lyPlain, 0x4C, 400);
            pOData.ReplaceInitialPlayer(UWPlayerData.FromPlain(lyPlain));

            UWInventoryModel lOModel = new UWInventoryModel(() => pOData, () => null);
            int liStone = UWObjectMechanics.SlingStoneObjectId;
            int liCapacity = pOData.InitialPlayer.MaxWeight - lOModel.GetCarriedTenthStones(lOProperties);
            UWCommonObjectProperties.Entry lOEntry;

            if (!lOProperties.TryGet(liStone, out lOEntry) || lOEntry.MassTenthStones <= 0 || liCapacity <= 0)
            {
                Check.Fail("pickup rules", "no sling stone mass or no capacity");

                return;
            }

            int liTooMany = (liCapacity / lOEntry.MassTenthStones) + 2;
            UWObject lOHeap = fStack(liStone, liTooMany);
            UWObject lOOne = fStack(liStone, 1);
            UWObject lOCreature = new UWObject((ushort)UWObjectIds.FirstCreature);

            UWPickupRules.Result lOResult = UWPickupRules.Check(lOOne, 1, true, lOModel);

            fExpectBool("pickup: one stone in reach is taken", lOResult.CanTake, true);
            fExpectInt("pickup: ... without a message", lOResult.MessageIndex, -1);

            lOResult = UWPickupRules.Check(lOCreature, 1, true, lOModel);
            fExpectInt("pickup: a creature is never releaseable",
                (int)lOResult.Outcome, (int)UWPickupRules.Outcome.NotPickable);
            fExpectInt("pickup: ... and says nothing", lOResult.MessageIndex, -1);
            fExpectInt("pickup: nothing is not pickable either",
                (int)UWPickupRules.Check(null, 1, true, lOModel).Outcome, (int)UWPickupRules.Outcome.NotPickable);

            lOResult = UWPickupRules.Check(lOOne, 1, false, lOModel);
            fExpectInt("pickup: out of reach is too far", (int)lOResult.Outcome, (int)UWPickupRules.Outcome.TooFar);
            fExpectInt("pickup: ... with message 94", lOResult.MessageIndex, UWReachRules.TooFarToTakeMessage);

            lOResult = UWPickupRules.Check(lOHeap, PickupWholeStack, true, lOModel);
            fExpectInt("pickup: a heap over the capacity, whole, is too heavy",
                (int)lOResult.Outcome, (int)UWPickupRules.Outcome.TooHeavy);
            fExpectInt("pickup: ... with message 96", lOResult.MessageIndex, UWStringIds.TooHeavyToPickUpMessage);
            fExpectInt("pickup: the whole count weighs as much as the whole stack",
                (int)UWPickupRules.Check(lOHeap, liTooMany, true, lOModel).Outcome, (int)UWPickupRules.Outcome.TooHeavy);

            // DECISION 14: AskHowMany cuts the stack before EncumCheck - one stone of the heap fits.
            fExpectBool("pickup: one stone of the heap is weighed alone (count-aware, decision 14)",
                UWPickupRules.Check(lOHeap, 1, true, lOModel).CanTake, true);

            // The original's order: the creature before the reach, the reach before the weight.
            fExpectInt("pickup: a creature out of reach is still only not pickable",
                (int)UWPickupRules.Check(lOCreature, 1, false, lOModel).Outcome, (int)UWPickupRules.Outcome.NotPickable);
            fExpectInt("pickup: a heap out of reach is too far, not too heavy",
                (int)UWPickupRules.Check(lOHeap, PickupWholeStack, false, lOModel).Outcome, (int)UWPickupRules.Outcome.TooFar);

            // The trade: only the weight, with its own sentence.
            lOResult = UWPickupRules.CheckFromTrade(lOHeap, PickupWholeStack, lOModel);
            fExpectInt("trade: a heavy good is too heavy to take", (int)lOResult.Outcome, (int)UWPickupRules.Outcome.TooHeavy);
            fExpectInt("trade: ... with message 253", lOResult.MessageIndex, UWStringIds.TooHeavyToTakeMessage);
            fExpectBool("trade: one piece of it can be taken (UseTradeSlot_ovr097_6E8 weighs the cut part)",
                UWPickupRules.CheckFromTrade(lOHeap, 1, lOModel).CanTake, true);

            // The wording of the sentences by id (block 1, our numbering).
            Check.Str("message 94", UWText.General(pOData, UWReachRules.TooFarToTakeMessage), "That is too far away to take.");
            Check.Str("message 96", UWText.General(pOData, UWStringIds.TooHeavyToPickUpMessage), "That is too heavy for you to pick up.");
            Check.Str("message 253", UWText.General(pOData, UWStringIds.TooHeavyToTakeMessage), "That is too heavy to take.");
            Check.Str("message 259, a pack without room", UWText.General(pOData, UWStringIds.NoPlaceToPutMessage),
                "There is no place to put that.");
        }

        /// <summary>The count that weighs a stack whole (UWWorldPickup.WholeStack on the Unity side).</summary>
        private const int PickupWholeStack = 0;
    }
}
