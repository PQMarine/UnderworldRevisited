using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// USING A THING IN THE WORLD (cleanup step 5.14, 2026-10-22: out of Interaction, which keeps the
/// public surface and forwards): the reach and the sight for a use, the doors (UWDoorRules), the
/// conversation partners and TalkTo's refusals, the switches and levers through their use
/// triggers, the objects with an effect of their own (UWWorldUseRules: gravestone, fountain,
/// silver tree, cauldron, shrine, the use mode from the world, the barrel's and chest's spill,
/// the glowing rock, the incense), the inscription, and the spell a thing carries. Both
/// schemes' gestures resolve to UseTarget, TalkTo and TalkToThing; the conversation traps and the
/// talking door start their conversations here too.
/// </summary>
public sealed class UWWorldUse
{
    private readonly Interaction mOInteraction;

    public UWWorldUse(Interaction pOInteraction)
    {
        mOInteraction = pOInteraction;
    }

    private UWDataImport.DataImport fData()
    {
        return mOInteraction.GameData;
    }

    private UWLevelLoader fLoader()
    {
        return mOInteraction.LevelLoader;
    }

    private UWInventory fInventory()
    {
        return mOInteraction.Inventory;
    }

    /// <summary>
    /// Use: door, conversation partner, switch, object effect, inscription. The pickup is not
    /// here: Original drags, Modern takes the thing before it comes this far (UWModernWorldInput.fModernUseOn).
    /// Only within reach (see UWReachRules) - looking still works at any distance. Out of reach
    /// the original answers "You are unable to use that from here." (per user, 2026-09-18;
    /// until then we silently did nothing).
    /// </summary>
    public void UseTarget(UWEntityInfo pOTarget)
    {
        if (mOInteraction.LogUseAttempts)
        {
            Debug.Log(string.Format("[Use] Target={0} Id={1} IsDoor={2}",
                pOTarget == null ? "none" : pOTarget.name,
                pOTarget == null || pOTarget.ObjectData == null ? -1 : pOTarget.ObjectData.ID,
                pOTarget != null && pOTarget.IsDoor));
        }

        // ADDRESSING A CREATURE HAS NO REACH OF ITS OWN (per user on the original, 2026-09-19):
        // it works as far as the eye sees, like the look command, not the twelve eighths of
        // the hand. Beyond the light's band the creature is not drawn and cannot be clicked at
        // all, so out of sight simply nothing happens - no refusal.
        bool lbCreature = Interaction.IsCreatureEntity(pOTarget);

        if (lbCreature)
        {
            if (!mOInteraction.IsWithinSight(pOTarget.transform.position))
                return;
        }
        else if (pOTarget != null && !mOInteraction.TryReachTarget(pOTarget, null))
        {
            return;
        }

        if (pOTarget?.IsDoor == true)
        {
            fTryUseDoor(pOTarget);

            // A DOOR DOES NOT SWALLOW ITS a_use trigger. The reference calls the trigger
            // unconditionally after the door handling (use.cs). For us this went unnoticed so far
            // because of 216 reachable doors in the whole game EXACTLY ONE has an a_use trigger in
            // its chain - the talking door on level 6, tile 4/2 (see
            // UWObjectMechanics.DoTrapConversationAction).
            UWTriggerSystem.TryFireSwitch(pOTarget.ObjectData, fLoader(), mOInteraction);
        }
        // An NPC with its own conversation slot is talked to instead of used. The slot is
        // stored on the object (npc_whoami); 0 means "none" and applies to all animals - on
        // level 1 exactly the three outcasts and one goblin have their own slot.
        else if (TalkTo(pOTarget))
        {
        }
        // A barrel, chest or nightstand fires its use trigger while spilling (see
        // SpillWorldContainer) - its first chain link must not swallow the use here
        // (UWWorldUseRules.UseTriggerFiresFirst).
        else if (pOTarget != null
            && UWWorldUseRules.UseTriggerFiresFirst(pOTarget.ObjectData.ID, pOTarget.ObjectData.GetCategory())
            && UWTriggerSystem.TryFireSwitch(pOTarget.ObjectData, fLoader(), mOInteraction))
        {
            // Original: a used switch shows no message of its own, the door swinging shut is
            // the only feedback.
        }
        else if (fTryUseObjectEffect(pOTarget))
        {
            // The object's own effect (fountain, cauldron) - the message comes from there.
        }
        // A wall inscription shows its wording on USE too, not only on look (confirmed by
        // user, 2026-08-28).
        else if (pOTarget != null && pOTarget.ShowDescriptionVerbatim)
        {
            mOInteraction.Log.AddLine(pOTarget.Description);
        }

        if (!lbCreature && pOTarget != null && !pOTarget.IsDoor)
            fCastThingSpell(pOTarget.ObjectData);
    }

    /// <summary>
    /// THE LAST STEP OF A USE FROM THE WORLD: a thing carrying a charged spell casts it with
    /// itself as the caster and spends a charge (UseObj's checkSpell with how 0, UnderworldExhumed
    /// obj/USEITEMS.C; UWItemSpellRules.TryCastFromWorldObject, 2026-10-15) - the sceptre on
    /// level 5 at 59/54 and the ruby on level 6 at 14/3. Not after food, a readable or a wand,
    /// which UseObj leaves to their own handlers.
    /// </summary>
    private void fCastThingSpell(UWObject pOThing)
    {
        if (pOThing == null || fLoader() == null || fLoader().CurrentLevel == null)
            return;

        UWObject.ObjectCategoryEnum leCategory = pOThing.GetCategory();

        if (leCategory == UWObject.ObjectCategoryEnum.Comestibles || leCategory == UWObject.ObjectCategoryEnum.BooksAndScrolls)
            return;

        if (UWItemSpellRules.TryCastFromWorldObject(pOThing, fLoader().CurrentLevel.Masterlist, true,
            out int liMajor, out int liMinor))
            UWTriggerSystem.CastSpellFromThing(pOThing, liMajor, liMinor, fLoader(), mOInteraction);
    }

    /// <summary>Talk: the conversation with the thing, if it has one ("You get no response."
    /// for creatures without). False for anything that is not a creature.</summary>
    public bool TalkTo(UWEntityInfo pOTarget)
    {
        return fTryTalk(pOTarget);
    }

    /// <summary>Talk mode over a thing that is no creature - the shrine, Arial on her wall, or
    /// the refusal (UWClickRules.TalkToThing).</summary>
    public void TalkToThing(UWEntityInfo pOTarget)
    {
        int liObjectId = pOTarget != null && pOTarget.ObjectData != null ? pOTarget.ObjectData.ID : -1;

        switch (UWClickRules.TalkToThing(liObjectId, pOTarget != null && pOTarget.IsChainedPrincess))
        {
            case UWClickRules.ThingTalkAnswer.ChantMantra:
                mOInteraction.Prompts.BeginMantra();
                break;

            case UWClickRules.ThingTalkAnswer.NoReactionFromPrincess:
                mOInteraction.AddGeneralMessage(UWClickRules.NoReactionFromPrincessMessage);
                break;

            case UWClickRules.ThingTalkAnswer.CannotTalk:
                AddConversationBlockMessage(UWClickRules.CannotTalkToThatLine);
                break;
        }
    }

    /// <summary>
    /// Talks to an NPC if there is a conversation for it.
    ///
    /// The slot is stored in the object itself (npc_whoami). With 0 the original takes the
    /// generic conversation of its kind: slot 256 + (object number - 64) (reference:
    /// conversationinitialisation.GetConversationNumber). Until 2026-09-13, 0 meant
    /// "none" for us - the wisp on level 4 (0x7A, conversation 314) stayed silent, in the original it
    /// talks (per user). If there is no conversation for the kind (animals), the caller continues with
    /// looking.
    /// </summary>
    private bool fTryTalk(UWEntityInfo pOTarget)
    {
        if (pOTarget == null || mOInteraction.ConversationScreen == null || fData() == null)
            return false;

        UWNpc lONpc = pOTarget.ObjectData as UWNpc;
        int liSlot = GetConversationSlot(lONpc);

        if (fRefusesTalk(lONpc, liSlot))
            return true;

        return liSlot > 0 && mOInteraction.ConversationScreen.Begin(fData(), lONpc, liSlot);
    }

    /// <summary>
    /// TALKTO'S REFUSALS (TalkTo_ovr100_0, CONVERSE.C), the one home for every way a conversation
    /// with a creature starts - the player's click and, since 2026-10-15, a conversation trap or a
    /// critter that comes to talk (TryTalkToNpcData; gronkify_talkto_player and crit_talk call
    /// TalkTo too, ours skipped the refusal there). True when it refuses, with "You get no
    /// response." (block 7, entry 1 in game):
    ///   - FIGHTING OR FLEEING THE PLAYER, OR HOSTILE, it does not answer (UWClickRules.
    ///     CreatureAnswers; per user, 2026-10-01: Oradinar, sent off by his own conversation,
    ///     cannot be talked to in the original);
    ///   - NO CONVERSATION: none for the slot, its code empty, or whoami 255 - the Slasher of
    ///     Veils and the animals (per user in the original, 2026-09-14).
    /// </summary>
    private bool fRefusesTalk(UWNpc pONpc, int piSlot)
    {
        if (pONpc == null || !UWObjectIds.IsCreature(pONpc.ID))
            return false;

        if (piSlot > 0 && pONpc.NPCwhoami != NoResponseWhoami && !fHasEmptyConversation(piSlot)
            && UWClickRules.CreatureAnswers(pONpc.NPCwhoami, pONpc.NPCGoal, pONpc.NPCGTarg, pONpc.NPCAttitude, pONpc.NPCIsAlly))
            return false;

        fAddNoResponse();

        return true;
    }

    /// <summary>whoami 255: this creature never talks (reference).</summary>
    private const int NoResponseWhoami = 255;

    /// <summary>"You get no response." - block 7, entry 1 in game, for us as usual one
    /// higher.</summary>
    private const int NoResponseStringIndex = 1 + 1;

    private bool fHasEmptyConversation(int piSlot)
    {
        UWConversations.Conversation lOConversation = fData().Conversations.GetConversation(piSlot);

        return lOConversation == null || lOConversation.Code == null || lOConversation.Code.Length == 0;
    }

    private void fAddNoResponse()
    {
        AddConversationBlockMessage(NoResponseStringIndex);
    }

    /// <summary>A line of the conversation block (7) into the message scroll - "You get no
    /// response.", "You cannot talk to that!".</summary>
    public void AddConversationBlockMessage(int piIndex)
    {
        try
        {
            string lsMessage = fData().Strings.Blocks[UWConversations.PartnerNameStringBlock].Strings[piIndex];

            if (!string.IsNullOrWhiteSpace(lsMessage))
                mOInteraction.AddMessage(lsMessage.TrimEnd('\r', '\n'));
        }
        catch
        {
            // a missing entry is not an error
        }
    }

    /// <summary>First slot of the generic conversations - one per creature kind.</summary>
    private const int GenericConversationBase = 256;

    /// <summary>Which conversation belongs to this creature, or 0 - see fTryTalk.</summary>
    public int GetConversationSlot(UWNpc pONpc)
    {
        if (pONpc == null || fData() == null || fData().Conversations == null)
            return 0;

        int liSlot = pONpc.NPCwhoami != 0
            ? pONpc.NPCwhoami
            : GenericConversationBase + (pONpc.ID - UWObjectClassProperties.CritterFirstId);

        if (pONpc.NPCwhoami == 0 && (pONpc.ID < UWObjectClassProperties.CritterFirstId || pONpc.ID > 127))
            return 0;

        return liSlot < fData().Conversations.SlotCount
            && fData().Conversations.GetConversation(liSlot) != null ? liSlot : 0;
    }
    /// <summary>
    /// THE FOUNTAIN, Fountain_seg040_352B_1816 (read 2026-10-08; per user on the original: not
    /// every fountain heals - "The water refreshes you." and nothing else - and the one on level 1
    /// not fully; until then every fountain healed fully): its enchantment (UWObjectMechanics.
    /// TryGetEnchantmentClass) is cast on the user as a spell trap casts, and only the healing
    /// class says "The waters of the fountain renew your strength."; the healing class's minor
    /// class decides how much (UWSpellCasting). The water above the basin uses the fountain
    /// linked to it (seg040_352B_2A0).
    /// </summary>
    private void fUseFountain(UWObject pOObject)
    {
        List<UWObject> lOMaster = fLoader() != null && fLoader().CurrentLevel != null ? fLoader().CurrentLevel.Masterlist : null;
        UWObject lOFountain = pOObject != null && pOObject.ID == UWObjectMechanics.FountainWaterObjectId
            ? fLinkedFountain(pOObject, lOMaster)
            : pOObject;

        if (lOFountain != null && UWObjectMechanics.TryGetEnchantmentClass(lOFountain, lOMaster, out int liMajor, out int liMinor)
            && liMajor >= 0)
        {
            UWGameUI lOUi = UWScene.GameUi;

            if (lOUi != null)
                lOUi.Runes.CastSpellByClass(liMajor, liMinor);

            mOInteraction.AddGeneralMessage(liMajor == UWObjectMechanics.FountainHealMajorClass
                ? UWObjectMechanics.FountainMessageIndex
                : UWObjectMechanics.FountainRefreshMessageIndex);

            return;
        }

        mOInteraction.AddGeneralMessage(UWObjectMechanics.FountainRefreshMessageIndex);
    }

    /// <summary>The fountain the water above it links to - its contents link, else the next one.</summary>
    private static UWObject fLinkedFountain(UWObject pOWater, List<UWObject> pOMaster)
    {
        if (pOMaster == null)
            return null;

        int liContents = pOWater.HasQuantity ? 0 : pOWater.Quantity;

        foreach (int liAt in new[] { liContents, (int)pOWater.Link })
        {
            if (liAt > 0 && liAt < pOMaster.Count && pOMaster[liAt] != null && pOMaster[liAt].ID == UWObjectMechanics.FountainObjectId)
                return pOMaster[liAt];
        }

        return null;
    }

    /// <summary>Objects with their own effect when used that are neither door nor switch - which
    /// thing has which effect is UWWorldUseRules (cleanup step 2.4, 2026-10-12), here it is
    /// carried out. False when the thing has none (or the gravestone no grave number, or the
    /// silver tree was not taken) - then UseTarget goes on.</summary>
    private bool fTryUseObjectEffect(UWEntityInfo pOTarget)
    {
        if (pOTarget == null || pOTarget.ObjectData == null || fData() == null)
            return false;

        switch (UWWorldUseRules.Classify(pOTarget.ObjectData.ID, pOTarget.ObjectData.GetCategory()))
        {
            // Using a gravestone shows the same as looking (reference use.cs, case 5).
            case UWWorldUseRules.Effect.Gravestone:
                return mOInteraction.Look.TryShowGravestone(pOTarget);

            case UWWorldUseRules.Effect.Fountain:
                fUseFountain(pOTarget.ObjectData);
                return true;

            // Reaching for the silver tree takes it back as a seed (see UWSilverTree).
            case UWWorldUseRules.Effect.SilverTree:
                return UWSilverTree.TryTakeTree(fLoader(), pOTarget.ObjectData, fInventory(), mOInteraction);

            case UWWorldUseRules.Effect.Cauldron:
                mOInteraction.AddGeneralMessage(UWObjectMechanics.CauldronMessageIndex);
                return true;

            case UWWorldUseRules.Effect.Shrine:
                mOInteraction.Prompts.BeginMantra();
                return true;

            // The anvil asks WHAT to work on - answered with a click in the backpack (see
            // UWItemTargetUse); the pole lying on the floor asks the same and is then applied in
            // the world, from where it lies.
            case UWWorldUseRules.Effect.UseModeFromWorld:
                fEnterUseModeFromWorld(pOTarget);
                return true;

            // Barrel, chest and the containers of major class 2 spill (the nightstand does
            // nothing, see UWWorldUseRules).
            case UWWorldUseRules.Effect.Spill:
                SpillWorldContainer(pOTarget);
                return true;

            case UWWorldUseRules.Effect.LightsOnlyEquipped:
                mOInteraction.AddGeneralMessage(UWWorldUseRules.LightsOnlyEquippedMessage);
                return true;

            // The glowing rock joins the carried one, as walking over it does (UWTouchGather).
            case UWWorldUseRules.Effect.GlowingRock:
                if (fInventory() != null && fLoader() != null
                    && UWGlowingRockRules.TryGather(pOTarget.ObjectData, fInventory().Model, fLoader().CurrentLevel))
                {
                    fLoader().RemoveObjectFromWorld(pOTarget.ObjectData);
                    fInventory().Model.NotifyChanged();
                }

                return true;

            case UWWorldUseRules.Effect.BurningIncense:
                fBurnIncenseInWorld(pOTarget.ObjectData);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Burning incense on the floor: the vision plays and the block becomes debris where
    /// it lies (UWItemUse.BurnIncense) - the record keeps its place, its picture is put down anew,
    /// and its place in the tile's list too (UseUnique's SET_ITEM; UWWorldEdit.Arrival.InPlace,
    /// 2026-10-18).</summary>
    private void fBurnIncenseInWorld(UWObject pOIncense)
    {
        UWItemDrag lOHost = UWScene.ItemDrag;

        if (pOIncense == null || lOHost == null || fLoader() == null)
            return;

        int liTileX = pOIncense.TileX;
        int liTileY = pOIncense.TileY;

        int liListIndex = fLoader().WorldEdit != null ? fLoader().WorldEdit.ListIndexOf(pOIncense) : -1;

        UWItemUse.BurnIncense(pOIncense, fData(), null, lOHost.UseHostForWorldThing());

        if (fLoader().RemoveObjectFromWorld(pOIncense))
            fLoader().PlaceRestingObject(pOIncense, liTileX, liTileY, UWWorldEdit.Arrival.InPlace, liListIndex);
    }

    /// <summary>A thing in the world enters use mode as if it had been clicked in the pack:
    /// its icon hangs on the pointer and the game asks "Use ... on what?" - the prompt and its
    /// wording are UWClassicUseMode's (ReportPrompt), the answer UWClassicUseMode.Apply.</summary>
    private void fEnterUseModeFromWorld(UWEntityInfo pOTarget)
    {
        if (fInventory() == null)
            return;

        UWItemDrag lOItemDrag = UWScene.ItemDrag;

        if (lOItemDrag != null)
            lOItemDrag.BeginUseModeFromWorld(pOTarget.ObjectData);
        else
            fInventory().EnterUseMode(pOTarget.ObjectData);
    }

    /// <summary>Barrel, chest or nightstand in the world (see SpillWorldContainer): the 3D-model
    /// containers of major class 5, minor class 1 (barrel 0x15B, chest 0x15D, nightstand 0x15E,
    /// which does nothing when used - UWWorldUseRules).</summary>
    public static bool IsWorldContainer(UWEntityInfo pOTarget)
    {
        int liId = pOTarget != null && pOTarget.ObjectData != null ? pOTarget.ObjectData.ID : -1;
        return UWObjectMechanics.IsWorldContainer(liId);
    }

    /// <summary>
    /// Using a barrel, chest or nightstand in the world puts its contents openly onto its tile
    /// instead of opening a container panel (per user on the original, 2026-09-15). The
    /// original's UseCont -> DumpTheBag -> drop_link_chain (USEITEMS.C, TREASURE.C): a locked
    /// container reports "The ... is locked"; otherwise the chain goes down around the
    /// container, 6 eighths each way, which can reach the next tile (read 2026-09-25, see
    /// UWScatterRules; until then ours kept to the tile) and NOT onto the containers standing
    /// there (per user, 2026-09-16; a THROWN item still may). The whole chain goes down, lock and
    /// triggers too, and a used container fires no use trigger (decision 25, per user on the
    /// original 2026-10-12; UWSpillRules, UWSpill). A smashed or burnt container comes through
    /// here with pbFromDamage and no message of its own: damage_object sets off the use triggers
    /// first, before the spill. A smashed chest or barrel (UWContainerDamage) then loses ONE lock
    /// (remove_object's remove_lock(obj, 0), the lock freed, not spilled) and is used like a
    /// player's use, so a second, locked lock still says "The ... is locked." and keeps the
    /// contents (2026-10-12; until then every lock was ignored and the lock went down with the
    /// rest). A bag destroyed by fire (UWTileBlast) ignores the lock (DumpTheBag).
    /// </summary>
    public void SpillWorldContainer(UWEntityInfo pOTarget, bool pbIgnoreLock = false,
        bool pbAnnounceEmpty = true, bool pbFromDamage = false)
    {
        if (fLoader() == null || fLoader().CurrentLevel == null)
            return;

        UWObject lOContainer = pOTarget.ObjectData;
        lOContainer.EnsureContentsLoaded(fLoader().CurrentLevel.Masterlist);

        // Smashed or burnt: the use triggers fire while the chain is whole (damage_object's
        // checkTrap kind 4 before remove_object), whether a lock then holds the contents or not.
        if (pbFromDamage)
        {
            FireDestroyedUseTriggers(lOContainer);

            if (UWObjectDamageRules.IsUsedWhenDestroyed(lOContainer.ID))
                UWSpillRules.TakeFirstLock(lOContainer.Contents);
        }

        if (!pbIgnoreLock && UWSpillRules.FindLockedLock(lOContainer.Contents) != null)
        {
            mOInteraction.AddMessage(UWItemMessages.IsLocked(fData(), lOContainer.ID));
            return;
        }

        UWTilePos lOTile = fLoader().WorldPositionToTile(pOTarget.transform.position);
        int liSpilled = UWSpill.Apply(fLoader(), lOContainer, UWSpillRules.Partition(lOContainer.Contents),
            pOTarget.transform.position, lOTile, true);

        // NOTHING CAME OUT: the original then says so. UW.EXE builds the sentence from three
        // pieces (DumpTheBag: "The ", the bare item name, " is empty.") and only when nothing
        // was spilled and the player did the using - which is why smashing a container stays
        // silent (per user, 2026-09-16: using an empty chest or barrel prints something in the
        // original, ours printed nothing).
        if (UWSpillRules.AnnouncesEmpty(liSpilled, pbAnnounceEmpty))
            mOInteraction.AddMessage(UWItemMessages.IsEmpty(fData(), lOContainer.ID));
    }

    /// <summary>The use triggers in a destroyed object's chain, in order - damage_object's
    /// checkTrap kind 4 for every static object it destroys (DAMAGE.C), before remove_object
    /// spills or frees the chain.</summary>
    public void FireDestroyedUseTriggers(UWObject pOObject)
    {
        if (pOObject == null || fLoader() == null || fLoader().CurrentLevel == null)
            return;

        pOObject.EnsureContentsLoaded(fLoader().CurrentLevel.Masterlist);

        foreach (UWObject lOTrigger in UWSpillRules.UseTriggers(pOObject.Contents))
            UWTriggerSystem.FireTraps(lOTrigger, pOObject, fLoader(), mOInteraction);
    }

    /// <summary>Confirmed by original test (see UWDoor): a locked door cannot
    /// be opened manually, but can be closed at any time (the lock check only applies to
    /// opening). A switch/trigger/trap (UWTriggerSystem) bypasses this check completely
    /// and does not change the lock state either - both confirmed original behaviour.
    /// The decision is UWDoorRules.Use (2026-10-12), shared with the creatures' door use.
    ///
    /// Key usage deliberately differs per scheme. Modern has no dragging,
    /// hence an automatic backpack check (incl. containers, recursive - a key
    /// did NOT work in the first test when it lay in a container instead of directly in the
    /// backpack, see UWObjectMechanics.FindMatchingKey) - if it finds a matching
    /// key, the door is unlocked AND opened in one step.
    ///
    /// Original has NO automatic check - dragging the key onto the door was
    /// tried and explicitly confirmed as wrong by the user ("does not work in the
    /// original"). The real mechanism is the general use mode (see
    /// UWInventory.UseModeItem/UWClassicUseMode.Update): right click on the key in the
    /// backpack attaches its icon to the cursor (item stays in place), right click on the door
    /// in the view window applies it - see UWItemTargetUse. This unlocks (or
    /// locks again) ONLY the lock state, it does NOT automatically open the door (confirmed
    /// by user) - unlike the Modern path here.</summary>
    private void fTryUseDoor(UWEntityInfo pOTarget)
    {
        UWDoor lODoor = pOTarget.GetComponentInParent<UWDoor>();

        if (lODoor == null)
            return;

        // The modern scheme's automatic key: asked only when it can matter (a closed, locked
        // door), so the backpack is not searched for every click on a door.
        bool lbKeyAtHand = UWControlScheme.Ui.Policy.KeyUsedAutomatically && lODoor.IsClosed && lODoor.IsLocked
            && fInventory() != null && fLoader() != null && fLoader().CurrentLevel != null
            && UWObjectMechanics.FindMatchingKey(fInventory().Backpack, lODoor.LockId, fLoader().CurrentLevel.Masterlist) != null;

        switch (UWDoorRules.Use(lODoor.IsClosed, lODoor.IsSpiked, lODoor.IsLocked, true, lbKeyAtHand))
        {
            case UWDoorRules.UseOutcome.Locked:
                mOInteraction.AddMessage(UWText.T("msg.door.locked"));
                return;

            case UWDoorRules.UseOutcome.Spiked:
                // Not for the player any more (2026-10-12: UseRect lets him open a spiked door,
                // OpenDoor takes the spike with it - the spike keeps creatures out); kept for
                // completeness.
                mOInteraction.ReportSpikedDoor();
                return;

            case UWDoorRules.UseOutcome.UnlockAndOpen:
                lODoor.Unlock();
                break;
        }

        // The open trigger (an_open in the door's chain) is UWDoor.Toggle's, fired on OPENING.
        lODoor.Toggle(UWTrapRules.Setter.Player);
    }

    /// <summary>Starts a conversation with an NPC the player did not click on -
    /// for the a_do trap 50, which knows its conversation partner via its object slot
    /// (see UWTriggerSystem.fFireConversationTrap), a critter that comes to talk and
    /// Garamon's burial. TalkTo's refusals apply as to a click (fRefusesTalk).</summary>
    public bool TryTalkToNpcData(UWObject pONpcData)
    {
        UWNpc lONpc = pONpcData as UWNpc;

        if (mOInteraction.ConversationScreen == null || UWConversationController.IsAnyOpen)
            return false;

        int liSlot = GetConversationSlot(lONpc);

        if (fRefusesTalk(lONpc, liSlot) || liSlot <= 0)
            return false;

        return mOInteraction.ConversationScreen.Begin(fData(), lONpc, liSlot);
    }

    /// <summary>Starts a conversation without a counterpart in the world - for the talking door
    /// (a_do trap 42), whose conversation slot is hard-wired and belongs to no
    /// NPC.</summary>
    public bool BeginConversation(int piSlot)
    {
        return mOInteraction.ConversationScreen != null && fData() != null
            && !UWConversationController.IsAnyOpen
            && mOInteraction.ConversationScreen.Begin(fData(), null, piSlot);
    }
}
