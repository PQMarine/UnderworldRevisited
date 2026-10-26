using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE LOOK TEXTS (cleanup step 5.13, 2026-10-22: out of Interaction, which keeps the public
/// surface and forwards): the look at a thing in the world or at a wall or floor, the description
/// of an item with its detail by place (always with detail in the world, the Lore roll in the
/// inventory, UWItemDescriptions), SpecialLook's line, the doors' names and conditions, the
/// gravestone, the books and scrolls, Name Enchantment's identification, the modern Q over the
/// interface, and the trap found on a look with its yes/no question. Everything is written to
/// the message log the way it was from Interaction.
/// </summary>
public sealed class UWLookService
{
    private readonly Interaction mOInteraction;

    private readonly UWMessageLog mOLog;

    public UWLookService(Interaction pOInteraction, UWMessageLog pOLog)
    {
        mOInteraction = pOInteraction;
        mOLog = pOLog;
    }

    private UWDataImport.DataImport fData()
    {
        return mOInteraction.GameData;
    }

    private UWLevelLoader fLoader()
    {
        return mOInteraction.LevelLoader;
    }

    private UWCharacter fCharacter()
    {
        return mOInteraction.Character;
    }

    private UWGameUI fUi()
    {
        return mOInteraction.GameUi;
    }

    private UWInventory fInventory()
    {
        return mOInteraction.Inventory;
    }

    /// <summary>String block 1: "You see nothing." - the look at what is not there.</summary>
    private const int NothingToSeeMessage = 155;

    // ------------------------------------------------- The look in the world

    /// <summary>Look: the description of the thing, or of the wall or floor texture.</summary>
    public void LookAtTarget(UWEntityInfo pOTarget, string psChunkDescription)
    {
        if (pOTarget != null)
        {
            // UWObjectDescriptionFormatter builds the "You see ..." message from the raw
            // original string (see there for the "_"/"&" structure) - applies only to
            // object/door descriptions (string block 4), not to the chunk description (wall/floor
            // texture, string block 10) - that one gets the same sentence frame below.
            // Wall inscriptions already carry their finished wording and must not be embedded
            // in "You see ..." (see UWEntityInfo). A gravestone shows its inscription and its
            // picture instead of the description (reference look.cs -> gravestone.Use).
            if (!TryShowGravestone(pOTarget))
            {
                mOLog.AddLine(fGetLookDescription(pOTarget));

                // SpecialLook (LOOK.C) after the look line: a closed door with its spike bit adds
                // "The door is spiked." (string 0x83) - per user on the original, 2026-10-13, the
                // door's own sentence comes first.
                string lsSpecial = fGetSpecialLookLine(pOTarget);

                if (lsSpecial != null)
                    mOLog.AddLine(lsSpecial);
            }

            // If an a_look trigger hangs on the look target, its trap text belongs directly
            // after the look message - for the orb on tile 58/13 first "You see an orb.", then
            // the crystal ball vision (checked by user in the original, 2026-08-28). The orb can
            // NOT be used, the chain deliberately hangs on looking. Deliberately NOT limited to
            // mfUseRange: looking has no range limit in the original, and the text belongs to
            // looking. A HIDDEN trigger additionally requires a Search skill check - the
            // difficulty is stored in the trigger itself (see UWTriggerSystem).
            UWTriggerSystem.TryFireLookTrigger(pOTarget.ObjectData, fLoader(), mOInteraction,
                fCharacter() != null ? fCharacter().Vitals.GetSkill(UWPlayerData.Skill.Search) : 0);

            // And a trap is spotted in the process, if one notices it - see fTryDetectTrap.
            fTryDetectTrap(pOTarget);

            // A window additionally shows a picture in the view window - the deeper the level,
            // the more lava (uw-formats.txt on CS400.N01: "look" graphics for windows to abyss
            // volcano core). ONLY FROM CLOSE BY: RectLook shows it for a look with detail, which
            // player_3dlook gives within 0x48 only (UWReachRules.LookDetailDistanceSquared,
            // 2026-10-15; until then from any distance).
            if (pOTarget.IsWindow && fUi() != null && fLoader() != null
                && mOInteraction.IsWithinLookDetail(pOTarget))
                fUi().Pictures.ShowWindowPicture(fLoader().CurrentLevelIndex);
        }
        else if (!string.IsNullOrEmpty(psChunkDescription))
        {
            // WITH "You see": walls and floors report like this in the original too (per user,
            // 2026-09-14: "You see" was missing for the floor). The texture names in block 10
            // already carry their article ("a stone wall", "nothing").
            mOLog.AddLine(UWItemDescriptions.DescribeChunk(psChunkDescription, fData()));
        }
        else
        {
            // NOTHING THERE: too dark to see that far, or the eye goes past everything. The
            // original has its own sentence for it (per user, 2026-09-18), it does not stay silent.
            mOInteraction.AddGeneralMessage(NothingToSeeMessage);
        }
    }

    /// <summary>The look sentence for a thing in the world, without the look's triggers and
    /// trap search - for Name Enchantment on something that cannot be identified (see
    /// UWMiscSpell).</summary>
    public string GetLookDescription(UWEntityInfo pOTarget)
    {
        return pOTarget != null ? fGetLookDescription(pOTarget) : null;
    }

    /// <summary>Q over the interface: the thing in the slot under the pointer, or nothing.</summary>
    public void LookAtUi(Vector2 pOPointer)
    {
        if (UWModernBags.Instance != null && UWModernBags.Instance.TryLookAt(pOPointer))
            return;

        if (UWModernActionBar.Instance != null)
            UWModernActionBar.Instance.TryLookAt(pOPointer);
    }

    /// <summary>Inscription and picture of a gravestone (reference gravestone.DisplayGrave):
    /// string block 8, picture from CS401.N01 via GRAVE.DAT. Returns false if it is not a
    /// gravestone with a grave number.</summary>
    public bool TryShowGravestone(UWEntityInfo pOTarget)
    {
        int liGrave = pOTarget != null ? UWEndgame.GetGraveNumber(pOTarget.ObjectData) : -1;

        if (liGrave < 0 || fData() == null)
            return false;

        string lsText = UWEndgame.GetGraveText(fData(), liGrave);

        if (!string.IsNullOrEmpty(lsText))
            mOInteraction.AddMessage(lsText);

        if (fUi() != null)
            fUi().Pictures.ShowGravePicture(UWEndgame.GetGraveFrame(fData(), liGrave));

        return true;
    }

    // ------------------------------------------------- Look texts and descriptions

    /// <summary>
    /// Look message of a target. Doors get their condition inserted - checked by
    /// user in the original (2026-08-27): "Right-click look shows the condition of the
    /// door. Sturdy, badly damaged, broken."
    /// </summary>
    private string fGetLookDescription(UWEntityInfo pOTarget)
    {
        // Wall inscriptions already carry their finished wording (see UWEntityInfo).
        if (pOTarget.ShowDescriptionVerbatim)
            return pOTarget.Description;

        // AN ITEM ON THE FLOOR is described like one in the backpack. This shows with
        // stacks: the original counts them in the world too ("13 arrows" instead of "an
        // arrow", per user, 2026-09-07). With the same message the condition word and
        // enchantment come along - the same information as in the inventory, which should
        // be no different from there.
        //
        // Only for pickable things: creatures, doors and fixed scenery do carry
        // object data too, but no quantity and no item condition.
        if (pOTarget.CanBePickedUp && pOTarget.ObjectData != null)
        {
            string lsItemMessage = DescribeInventoryItem(pOTarget.ObjectData, WorldLookDetail);

            if (!string.IsNullOrEmpty(lsItemMessage))
                return lsItemMessage;
        }

        string lsRaw = pOTarget.Description;

        // A CREATURE WITH ATTITUDE: "You see a mellow outcast named Bragit." (per user
        // in the original, 2026-09-12). The word comes from string block 5 from 96 (hostile, upset,
        // mellow, friendly), the article follows it, the name is in block 7 and
        // appears immediately, without having to have talked - as in the reference
        // (npc.RegularNPCDescription).
        // Only for a creature that is not an item (the wording is in UWItemDescriptions).
        string lsCreature = pOTarget.CanBePickedUp
            ? null
            : UWItemDescriptions.DescribeCreature(pOTarget.ObjectData as UWNpc, lsRaw, fData());

        if (lsCreature != null)
            return lsCreature;

        string lsCondition = null;

        if (pOTarget.IsDoor)
        {
            // A SPIKED DOOR is described like any door; "The door is spiked." follows as a line of
            // its own (fGetSpecialLookLine). Until 2026-10-13 ours said only that, after the
            // reference's door.LookAt, which leaves before the description (per user on the
            // original: the door's sentence first, then the spike).

            // OPEN OR CLOSED comes from the door itself, not from the text frozen at spawn time:
            // the object id only changes when the world is captured, the leaf swings long before.
            // Block 4 names the closed doors "a_door", the open ones "an_open door" - the door on
            // level 2, tile 61/6 said "open door" whether open or not (per user, 2026-09-18).
            IUsableDoor lOMover = pOTarget.GetComponentInParent<IUsableDoor>();

            // A DOOR ON ITS WAY IS "a moving door": the original swaps it for object 0x1CF
            // while it swings or the grate runs (OpenDoor_seg040_352B_21E6 through
            // seg040_352B_20CD), and looking at that names only it - no condition, no owner
            // (per user, 2026-09-27; ours named the door as it stood before).
            if (lOMover != null && lOMover.IsMoving)
            {
                string lsMoving = fGetObjectName(MovingDoorObjectId);

                if (!string.IsNullOrEmpty(lsMoving))
                    return UWItemDescriptions.DescribeScenery(lsMoving, null, null, fData());
            }

            string lsStateName = lOMover != null && pOTarget.ObjectData != null
                ? fGetDoorName(pOTarget.ObjectData.ID, lOMover.IsClosed) : null;

            if (!string.IsNullOrEmpty(lsStateName))
                lsRaw = lsStateName;

            UWDamageable lODamageable = pOTarget.GetComponentInParent<UWDamageable>();

            lsCondition = lODamageable == null ? null : fGetConditionWord(lODamageable);
        }

        // WHO OWNS IT belongs on fixed scenery too: the original says "You see a sturdy chest
        // belonging to a green goblin." for barrels and chests (per user with a screenshot,
        // 2026-09-16). Items on the floor get it through DescribeInventoryItem above. The door's
        // condition word goes before its name (UWItemDescriptions.DescribeScenery).
        return UWItemDescriptions.DescribeScenery(lsRaw, lsCondition, pOTarget.ObjectData, fData());
    }

    /// <summary>
    /// SpecialLook (LOOK.C), the line after the look sentence: for a closed door at rest whose
    /// owner bit 0 is set, "The door is spiked." (string 0x83); for a key or bones the line of
    /// UWItemDescriptions.SpecialLookLine. Null when there is none.
    /// </summary>
    private string fGetSpecialLookLine(UWEntityInfo pOTarget)
    {
        if (pOTarget == null || pOTarget.ObjectData == null)
            return null;

        // A key, bones: UWItemDescriptions.SpecialLookLine - but in the world only with detail.
        // player_3dlook (INTERACT.C) passes LookAt a lore of 1 only for a thing that cannot be
        // picked up and lies in reach, else 0, and KeyLook and BonesLook print only above 0.
        // Every key and every skull or bone can be picked up, so in the world neither says
        // more; the inventory look does (per user on the original, 2026-10-13: the key's line
        // only in the inventory). The spiked door's line asks for no detail.
        if (!pOTarget.IsDoor)
            return pOTarget.CanBePickedUp ? null
                : UWItemDescriptions.SpecialLookLine(pOTarget.ObjectData, fData());

        if (!UWObjectMechanics.IsDoorSpiked(pOTarget.ObjectData))
            return null;

        IUsableDoor lOMover = pOTarget.GetComponentInParent<IUsableDoor>();

        if (lOMover != null && (!lOMover.IsClosed || lOMover.IsMoving))
            return null;

        return mOInteraction.GetGeneralMessage(UWItemApplications.DoorIsSpikedMessage);
    }

    /// <summary>The block 4 name of a door in its current state: the closed doors are 320 to
    /// 327, the same doors open lie eight higher (see UWWorldCapture). Null without data.</summary>
    private string fGetDoorName(int piObjectId, bool pbClosed)
    {
        const int liOpenOffset = UWWorldCapture.FirstOpenDoorId - UWWorldCapture.FirstClosedDoorId;

        if (fData() == null || piObjectId < UWWorldCapture.FirstClosedDoorId || piObjectId > UWWorldCapture.LastOpenDoorId)
            return null;

        int liClosedId = piObjectId >= UWWorldCapture.FirstOpenDoorId ? piObjectId - liOpenOffset : piObjectId;

        return UWText.RawObjectName(fData(), pbClosed ? liClosedId : liClosedId + liOpenOffset, null);
    }

    /// <summary>"a_moving door", the class-7 object a door becomes while it moves.</summary>
    private const int MovingDoorObjectId = 0x1CF;

    /// <summary>The raw block 4 name of an object, or null without data.</summary>
    private string fGetObjectName(int piObjectId)
    {
        return UWText.RawObjectName(fData(), piObjectId, null);
    }

    /// <summary>Condition word from string block 5 - broken, badly damaged, damaged, sturdy,
    /// massive - for a door or another thing with health (see UWItemDescriptions).</summary>
    private string fGetConditionWord(UWDamageable pODamageable)
    {
        return UWItemDescriptions.GetConditionWord(pODamageable.GetConditionStringIndex(), fData());
    }

    /// <summary>
    /// The look message for an item in the inventory or on the floor, with condition word,
    /// quantity, enchantment and owner - the wording is in UWItemDescriptions (P3 of the
    /// engine separation, 2026-09-18).
    /// </summary>
    public string DescribeInventoryItem(UWObject pOItem, int piDetail = UWItemDescriptions.DetailFromLoreCheck)
    {
        if (pOItem == null || fData() == null)
            return null;

        return UWItemDescriptions.DescribeItem(pOItem, fData(),
            fCharacter() != null ? fCharacter().Vitals.GetSkill(UWPlayerData.Skill.Lore) : 0,
            fGetItemObjectList(), piDetail);
    }

    /// <summary>The detail level of a look at an item in the world: always 1, no Lore roll
    /// (Look_seg024_24DC_D20, see UWItemDescriptions.DetailFromLoreCheck).</summary>
    private const int WorldLookDetail = 1;

    /// <summary>Looking at an item in the inventory: its look sentence, then SpecialLook's line
    /// (UWItemDescriptions.SpecialLookLine) - both schemes.</summary>
    public void AddItemLookMessages(UWObject pOItem)
    {
        mOInteraction.AddMessage(DescribeInventoryItem(pOItem));

        string lsSpecial = UWItemDescriptions.SpecialLookLine(pOItem, fData());

        if (lsSpecial != null)
            mOInteraction.AddMessage(lsSpecial);
    }

    /// <summary>
    /// Identifies an item permanently and describes it right away - the spell Name
    /// Enchantment (see UWLoreCheck.Identify and UWMiscSpell).
    ///
    /// Returns false if the item cannot be identified; then the
    /// caller reports "The spell has no discernable effect.".
    /// </summary>
    public bool IdentifyItem(UWObject pOItem)
    {
        if (pOItem == null || fData() == null)
            return false;

        if (!UWLoreCheck.Identify(pOItem, fData().CommonObjectProperties))
            return false;

        // LookAt(target, 3) (obj_spells, UnderworldExhumed combat/SPELLS.C): the look sentence and
        // SpecialLook's line at full detail - a book or scroll is read, a key or bones say their
        // line, in the world as carried (2026-10-15; until then the sentence alone, reference
        // audit round 2, A50).
        if (!TryReadBook(pOItem, true))
            AddItemLookMessages(pOItem);

        return true;
    }

    /// <summary>SpecialLook's line for a thing in the world that is looked at with full detail
    /// (Name Enchantment on a door: "The door is spiked."). Null when there is none.</summary>
    public string GetSpecialLookLine(UWEntityInfo pOTarget)
    {
        return fGetSpecialLookLine(pOTarget);
    }

    /// <summary>The object list that holds inventory items - needed to find the linked
    /// spell object from a wand. If the game state comes from a save file,
    /// it is that file's record list, otherwise the level's.</summary>
    private System.Collections.Generic.List<UWObject> fGetItemObjectList()
    {
        return UWItemDescriptions.ItemObjectList(fData(),
            fLoader() != null ? fLoader().CurrentLevel : null);
    }

    // ------------------------------------------------- Books and scrolls

    /// <summary>
    /// The line before the text: "You read the " + the item's name + "...", for books and
    /// scrolls alike (UseReadable_seg040_352B_19A8 and Read_ovr122_586, which put the item name
    /// between two strings of UW.EXE; "UNNAMED" when it has none). CONFIRMED per user on the
    /// original 2026-09-25: a right click gives "You see a ragged scroll.", "You read the
    /// scroll...", then the text; a left click the last two only. Until then ours said "The
    /// scroll reads:" and "The book reads:" - lines that exist nowhere in UW1. The sentence is
    /// UWItemMessages.ReadHeader's.
    /// </summary>
    private string fReadHeader(UWObject pOItem)
    {
        return UWItemMessages.ReadHeader(fData(), pOItem.ID);
    }

    /// <summary>
    /// Reads a book or scroll. Returns false if the object is neither or
    /// has no text - then the caller should carry on as before.
    ///
    /// Without text are the picture parchments and the map. In the original they have their own
    /// display, which we do not have yet.
    ///
    /// Three parts are printed: the look message, the reading hint and the text. With
    /// short texts all three are in the box at once, with long ones the first
    /// two scroll out (per user on the original).
    /// </summary>
    public bool TryReadBook(UWObject pOItem, bool pbLooking)
    {
        if (pOItem == null || fData() == null
            || pOItem.GetCategory() != UWObject.ObjectCategoryEnum.BooksAndScrolls)
            return false;

        // An ENCHANTED scroll is a spell scroll and has no text: its quantity field
        // carries the spell, not a string pointer. In the original it only shows the
        // look message "You see a scroll of ..." (per user, 2026-09-03).
        //
        // Read from the user's save game: the four spell scrolls all carry the bit,
        // the pure text scrolls none. Without this check one of them produced a
        // plausible-looking but wrong text.
        if (pOItem.IsEnchanted)
            return false;

        string lsLook = string.Empty;

        try
        {
            lsLook = DescribeInventoryItem(pOItem);
        }
        catch
        {
            // A missing string entry is not an error - then the line is dropped.
        }

        // THE RECIPE is not read but used: with a bowl of the right things it becomes the
        // rotworm stew (UWStewRules, per user 2026-09-28). Only USING it mixes - a right click
        // reads its text as with every scroll (per user on the original, the same day).
        if (!pbLooking && UWStewRules.IsRecipe(pOItem))
        {
            int liLine = UWStewRules.TryMix(fInventory() != null ? fInventory().Model : null, fData());

            mOInteraction.AddGeneralMessage(liLine);

            return true;
        }

        // The map has its own sentence and NO reading hint before it (per user on the
        // original: only the look message and "Enscribed upon the scroll is your map.").
        // Its entry in block 3 would be "The pages are blank." and thus wrong.
        if (pOItem.ID == UWObjectMechanics.MapObjectId)
        {
            mOInteraction.ShowPagedText(fData().GetGeneralMessage(UWObjectMechanics.MapMessageIndex),
                pbLooking ? lsLook : null);

            return true;
        }

        // THE MAP PIECE plays its cutscene instead of letters, a plan in the view window as a
        // window shows (per user with a screenshot from the original, 2026-09-03): the flag
        // decides, not an empty text (UWObjectMechanics.GetBookCutscene, 2026-10-15).
        //
        // The look message belongs ONLY to the right click. On left click the
        // original only shows the picture and writes nothing to the log.
        int liCutscene = UWObjectMechanics.GetBookCutscene(pOItem);

        if (liCutscene >= 0)
        {
            if (fUi() == null || !fUi().Pictures.ShowScrollPicture(liCutscene))
                return false;

            if (pbLooking && !string.IsNullOrEmpty(lsLook))
                mOInteraction.AddMessage(lsLook);

            return true;
        }

        string lsText = fData().GetBookText(UWObjectMechanics.GetBookStringIndex(pOItem));

        // A TEXT NUMBER FROM 0x100 ON (the recipe, looked at) stands alone, without "You read
        // the ..." (BookLook, obj/LOOK.C, 2026-10-15; until then ours put the header before it).
        string lsPrefix = UWObjectMechanics.ReadsWithHeader(pOItem) ? fReadHeader(pOItem) : null;

        // An empty text still gets its header, as UseBook prints it (none such is known).
        if (string.IsNullOrEmpty(lsText) || string.IsNullOrEmpty(lsText.Trim()))
        {
            if (pbLooking && !string.IsNullOrEmpty(lsLook))
                mOInteraction.AddMessage(lsLook);

            if (!string.IsNullOrEmpty(lsPrefix))
                mOInteraction.AddMessage(lsPrefix);

            return true;
        }

        // The look message belongs only to the right click. The reading hint and the text
        // appear with both buttons (per user, 2026-09-03).
        if (pbLooking)
            mOInteraction.ShowPagedText(lsText, lsLook, lsPrefix);
        else
            mOInteraction.ShowPagedText(lsText, lsPrefix);

        return true;
    }

    // ------------------------------------------------- The trap found on a look

    /// <summary>
    /// Is a trap noticed when looking?
    ///
    /// THE CHECK IS SEARCH against eight (reference: trapdisarming.DetectTrapTrigger and
    /// DoTrapSkillCheck). If you spot it, the original asks whether you want to disarm it
    /// - with a pre-typed "Yes".
    ///
    /// Detection happens ANEW EACH TIME. Whoever notices nothing can look again; there is no
    /// memory list.
    ///
    /// ONLY WITH THE LOOK ICON LIT (Look_seg024_24DC_D20 calls SearchForTrap_ovr143_1829 only
    /// while the mode word dseg 268C is 3; the plain right-click look, mode 0, looks and then
    /// may pick up instead). Per user, 2026-09-30: the poisoned potion on level 2, 14/51, asked
    /// the trap question now and then in ours, never in 20 right-click looks in the original.
    ///
    /// THE MODERN SCHEME has no look icon: its look key (Q) and the context menu's Look do
    /// nothing but look, the deliberate look of the icon, so they search as it does (per user,
    /// 2026-10-13: the question never came there).
    /// </summary>
    private void fTryDetectTrap(UWEntityInfo pOTarget)
    {
        if (pOTarget == null || pOTarget.ObjectData == null || mOInteraction.IsAskingYesNo
            || (mOInteraction.CommandMode != UWCommandMode.Look && UWControlScheme.Ui.Policy.TrapSearchNeedsLookMode)
            || fLoader() == null || fLoader().CurrentLevel == null)
            return;

        if (!UWTrapDisarming.Detect(pOTarget.ObjectData, fLoader().CurrentLevel.Masterlist,
            fCharacter() != null ? fCharacter().Vitals.GetSkill(UWPlayerData.Skill.Search) : 0))
            return;

        // Asked like every yes/no question (per user, 2026-09-30) - see UWPromptController.AskYesNo.
        mOInteraction.AskYesNo(mOInteraction.GetGeneralMessage(UWTrapDisarming.TrapFoundMessage) + " ", pbYes =>
        {
            if (pbYes)
                mOInteraction.DisarmTrap(pOTarget);
        });
    }
}
