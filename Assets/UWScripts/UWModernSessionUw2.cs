using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME BEHIND THE MODERN INTERFACE'S ENTRY (Docs/UW2-PLAN.md, "The modern interface for UW2: one
/// interface, two hosts", stage U4): IUWModernSession over the second game's screen (UWGameScreenUw2), its player
/// record, its world view and its attack host. Made by the screen and set on
/// UWModernUi.Session while the screen runs the modern scheme.
///
/// IT GREW PART BY PART (stage U4, parts A to E). Has says which parts of the first game's windows stand on the second
/// game; since part E step 4 every part of the preset Modern does, and the screen's stopgap HUD (UWModernHudUw2) is
/// gone. A member left empty names the stage it belongs to (the classic kinds' pieces, U5 to U7) or its reason.
///
/// PART A STEP 2 (2026-11-23): the message box and the crosshair with its target's name. The lines are the
/// screen's scroll (one time for all of them: the screen keeps when the last was said, so they fade together as
/// the stopgap's did); the crosshair is the game's pointer in the view's middle while the look is locked and no
/// window is open, the aim's while a bow or a missile spell waits, the enchanter's for a target spell.
///
/// PART B STEP 2 (2026-11-23): the character panel. The sheet binds the record's name and class
/// (UWPlayerData.FromSecondGame) and the live numbers (UWVitalsUw2); the paperdoll's figure is the record's body
/// and the worn armour's pictures as the classic page picks them (ARMOR_F or ARMOR_M by type and condition);
/// Sleep makes camp as F10 does. Tracking is not built on the second game, so the panel shows no button for it.
///
/// PART B STEP 3 (2026-11-23): the bag windows. The model is the screen's; using, looking, eating from the pointer,
/// dropping and what opening a container sets off are the classic page's own routines on the screen (ModernUse and
/// the rest), so nothing about them differs between the schemes. A thing that asks for its target waits in the first
/// game's bag use mode (UWAttackUw2.AskUseOn hands it over; UseOn and UseOnWorld hand the click back to the rule,
/// per user 2026-11-23 at test list 182: until then it waited on the attack host and the right button did not end it).
///
/// PART B STEP 4 (2026-11-23): the first game's action bar. Its places point at carried things as before, kept in
/// UWR.json the same way; a weapon goes into the hand and is drawn or put away there (ToggleFightMode), anything
/// else is the classic page's use. A tool whose target stands in the world (UWToolUseUw2.TargetsWorld) goes on the
/// crosshair's target at once, without the question (per user at test list 181, as the first game's bar).
///
/// PART C STEP 2 (2026-11-23): the first game's rune panel, and spells on the action bar. The magic half answers from
/// the record's bag (UWPlayerRecordUw2.HasRune), the screen's shelf and its rules (a rune after a cast starts a new
/// shelf, the fourth pushes the oldest off; mous_in_rune and try_cast, as the classic page), the cast through
/// UWSpellCasting with this game's spell host, and the spell's picture by this game's tables (parse_aspells, its
/// missiles). While another input mode holds the game (GameInputMode not 0: a thing on the pointer, a target or a
/// missile spell waiting, a thing used on another, the roaming view) no rune click and no cast acts, as in both
/// games' originals (UWGameScreenUw2.IsInAnotherInputMode).
/// </summary>
public sealed class UWModernSessionUw2 : IUWModernSession, IUWModernCharacter, IUWModernInventory, IUWModernMagic, IUWModernMessages,
    IUWModernWorld, IUWModernArt
{
    private readonly UWGameScreenUw2 mOScreen;

    public UWModernSessionUw2(UWGameScreenUw2 pOScreen)
    {
        mOScreen = pOScreen;
    }

    /// <summary>The character's record of the running session, null without one.</summary>
    private static UWPlayerFile fFile => UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null;

    // ------------------------------------------------- IUWModernSession

    public DataImport Data => mOScreen.ModernData;

    /// <summary>As the screen draws: hard pixels.</summary>
    public FilterMode Filter => FilterMode.Point;

    /// <summary>None: the first game's interface does not run here; UWUiKit makes the texts without it (the interface's
    /// own rule, IUWModernSession.TextUi).</summary>
    public UWGameUI TextUi => null;

    public IUWModernCharacter Character => this;

    public IUWModernInventory Inventory => this;

    public IUWModernMagic Magic => this;

    public IUWModernMessages Messages => this;

    public IUWModernWorld World => this;

    public IUWModernArt Art => this;

    private IUWModernConversationHost mOConversation;

    public IUWModernConversationHost Conversation => mOConversation ?? (mOConversation = new UWModernConversationHostUw2(mOScreen));

    public bool Has(UWModernPartEnum pePart)
    {
        switch (pePart)
        {
            case UWModernPartEnum.Messages:
            case UWModernPartEnum.Crosshair:
            case UWModernPartEnum.Vitals:
            case UWModernPartEnum.Weapon:
            case UWModernPartEnum.CharacterPanel:
            case UWModernPartEnum.Bags:
            case UWModernPartEnum.ActionBar:
            case UWModernPartEnum.RunePanel:
            case UWModernPartEnum.ActiveSpells:
            case UWModernPartEnum.Heading:
            case UWModernPartEnum.HudInput:
            case UWModernPartEnum.Minimap:
            case UWModernPartEnum.Question:
            case UWModernPartEnum.GameMenu:
            case UWModernPartEnum.HudPointer:
            case UWModernPartEnum.PadHints:
            case UWModernPartEnum.LayoutEditor:
            // The original's stats and character pages on this game's art (stage U5): the own layout offers them.
            case UWModernPartEnum.StatsPage:
            case UWModernPartEnum.CharacterPage:
            // The compass with the power gem and the rune hollow, the marble ledge's recesses (stage U6 step 1).
            case UWModernPartEnum.Compass:
            case UWModernPartEnum.RuneHollow:
            // The stone shelf grown from the marble ledge (stage U6 step 2a).
            case UWModernPartEnum.StoneShelf:
            // The active spells in the ledge's left hollow (stage U6 step 2c).
            case UWModernPartEnum.SpellHollow:
            // The generated backs in this game's colours and walls (stage U6 step 2d).
            case UWModernPartEnum.Backs:
            // Classic Wide on this game's frame (stage U7 step 2).
            case UWModernPartEnum.ClassicFrame:
                return true;

            default:
                return false;
        }
    }

    /// <summary>Every preset: Modern, the own layout (the game's own since part U4e step 3), Classic Modular from this
    /// game's pieces (the compass with its gem, the hollows, the shelf, the scroll, the pages; stage U6, its places as the
    /// user laid them out, UWLayoutPresets.ClassicModularLayoutUw2) and Classic Wide on this game's frame (stage U7 step
    /// 2, UWClassicWideGeometry).</summary>
    public bool Supports(UWModernLayout.PresetEnum pePreset)
    {
        return true;
    }

    // ------------------------------------------------- IUWModernCharacter

    public int HitPoints => fFile != null ? UWPlayerRecordUw2.Vitality(fFile) : 0;

    public int MaxHitPoints => fFile != null ? UWPlayerRecordUw2.MaxVitality(fFile) : 0;

    public int Mana => fFile != null ? UWPlayerRecordUw2.Mana(fFile) : 0;

    public int MaxMana => fFile != null ? UWPlayerRecordUw2.MaxMana(fFile) : 0;

    /// <summary>The classic flask's words (the screen's flask_info).</summary>
    public void DescribeFlask(UWClickRules.FlaskPart pePart)
    {
        if (pePart == UWClickRules.FlaskPart.Vitality || pePart == UWClickRules.FlaskPart.Mana)
            mOScreen.SayFlask(pePart == UWClickRules.FlaskPart.Vitality);
    }

    /// <summary>With the heading strip and the compass (part D).</summary>
    /// <summary>print_info, as a click on the classic compass (the heading strip's click, part U4d step 2).</summary>
    public void ReportStatus()
    {
        mOScreen.ModernReportStatus();
    }

    public bool IsLoaded => fFile != null;

    /// <summary>The record's most (player->weight_max, as the classic page's weight left and the walk's load).</summary>
    public int MaxLoad => fFile != null ? UWPlayerRecordUw2.MaxWeight(fFile) : 0;

    private UWPlayerFile mOSheetFile;

    private UWPlayerData mOSheetPlayer;

    /// <summary>The name and the class from the record, read once for a save, and the live numbers of the second
    /// game's vitals - as the help window's Stats tab binds them (UWHelpStatsTab); the original's page with this
    /// game's list (the skill points first, stage U5 step 1).</summary>
    public UWCharacterSheet BindSheet(UWCharacterSheet pOSheet)
    {
        UWPlayerFile lOFile = fFile;
        DataImport lOData = Data;

        if (lOFile != mOSheetFile)
        {
            mOSheetFile = lOFile;
            mOSheetPlayer = lOFile != null ? UWPlayerData.FromSecondGame(lOFile) : null;
        }

        return pOSheet.Bind(lOData != null ? lOData.Strings : null, mOSheetPlayer, UWVitalsUw2.Current != null ? UWVitalsUw2.Current.Vitals : null,
            UWGame.Uw2);
    }

    // ------------------------------------------------- IUWModernInventory (part B step 3)

    public UWInventoryModel Model => mOScreen.Inventory;

    public bool CanUse => mOScreen.Inventory != null;

    /// <summary>The classic page's use (DoSpecialActions, UseObj), wherever the windows ask from.</summary>
    public bool Use(UWObject pOItem, UWCarriedItemUse.Context peContext)
    {
        // THE ACTION BAR'S TOOLS act on the crosshair's target at once, as the first game's (UWCarriedItemUse); one for
        // a carried thing opens the bags for its click.
        if (peContext == UWCarriedItemUse.Context.ActionBar && UWAttackUw2.Current != null)
            return UWAttackUw2.Current.UseFrom(UWToolUseUw2.TargetsWorld(pOItem) ? UWAttackUw2.UseOnFromEnum.Crosshair
                : UWAttackUw2.UseOnFromEnum.World, () => mOScreen.ModernUse(pOItem));

        return mOScreen.ModernUse(pOItem);
    }

    // THE FIRST GAME'S USE MODE holds the thing (UWAttackUw2.AskUseOn); its click comes back to the rule here.

    /// <summary>The rule on a carried thing; the mode ends (no thing keeps it on this game).</summary>
    public bool UseOn(UWObject pOItem, UWObject pOCarriedTarget)
    {
        if (UWAttackUw2.Current != null)
            UWAttackUw2.Current.ApplyHandedOn(pOItem, pOCarriedTarget);

        return false;
    }

    public void UseOnWorld(UWObject pOItem, Vector2 pOScreenPoint)
    {
        if (UWAttackUw2.Current != null)
            UWAttackUw2.Current.ApplyHandedAt(pOItem, pOScreenPoint);
    }

    /// <summary>UseThing's "Use &lt;name&gt; on what?".</summary>
    public void SayUsePrompt(UWObject pOItem)
    {
        DataImport lOData = Data;

        if (UWAttackUw2.Current != null && lOData != null && pOItem != null)
            UWAttackUw2.Current.AddMessage(UWItemMessages.UseOnWhat(lOData, pOItem.ID));
    }

    public void ContainerOpened(UWObject pOContainer)
    {
        mOScreen.ModernContainerOpened(pOContainer);
    }

    public bool TryConsumePointerItem()
    {
        return mOScreen.ModernEatFromPointer();
    }

    public bool DropOrThrowPointerItem(Vector2 pOScreenPoint)
    {
        return mOScreen.ModernDrop(pOScreenPoint);
    }

    /// <summary>Only the first game's conversation shows a look in its own place; this game's look speaks itself.</summary>
    public string Describe(UWObject pOItem)
    {
        return string.Empty;
    }

    /// <summary>The look reads a book or scroll itself (SayLook).</summary>
    public bool TryRead(UWObject pOItem, bool pbLooking)
    {
        return false;
    }

    /// <summary>inv_look: the look triggers, the sentences and a book's or scroll's text.</summary>
    public void SayLook(UWObject pOItem)
    {
        mOScreen.ModernLook(pOItem);
    }

    /// <summary>A target spell waits for what it is cast on (nail_1area), and a carried thing can take it.</summary>
    public bool IsItemSpellPending => UWAttackUw2.Current != null && UWAttackUw2.Current.IsTargetSpellWaiting;

    /// <summary>The modern rule: on no thing the spell keeps waiting.</summary>
    public UWPendingSpellState.InventoryClickOutcome CastPendingSpellOn(UWObject pOItem)
    {
        if (!IsItemSpellPending)
            return UWPendingSpellState.InventoryClickOutcome.NotWaiting;

        return pOItem != null && UWAttackUw2.Current.ApplyTargetToItem(pOItem)
            ? UWPendingSpellState.InventoryClickOutcome.CastOnItem
            : UWPendingSpellState.InventoryClickOutcome.KeepsWaiting;
    }

    // ------------------------------------------------- IUWModernMagic (the runes, the shelf and the cast since part C; the
    // active spells' text, look and cancel with their row, part D)

    /// <summary>The record's words folded into one number: it changes when a spell comes, goes or changes place.</summary>
    public int ActiveSpellVersion
    {
        get
        {
            UWPlayerFile lOFile = fFile;

            if (lOFile == null)
                return 0;

            int liCount = UWPlayerRecordUw2.ActiveSpellCount(lOFile);
            int liVersion = liCount;

            for (int liAt = 0; liAt < liCount; liAt++)
                liVersion = (liVersion * 31) + UWPlayerRecordUw2.ActiveSpell(lOFile, liAt) + 1;

            return liVersion;
        }
    }

    public int ActiveSpellCount => fFile != null ? UWPlayerRecordUw2.ActiveSpellCount(fFile) : 0;

    /// <summary>The classic page's picture for the record's word (parse_aspells, as UWGameScreenUw2.fShowRunes and
    /// the stopgap's row work it out).</summary>
    public int ActiveSpellPicture(int piSlot)
    {
        UWPlayerFile lOFile = fFile;

        if (lOFile == null || piSlot < 0 || piSlot >= UWPlayerRecordUw2.ActiveSpellCount(lOFile))
            return -1;

        int liWord = UWPlayerRecordUw2.ActiveSpell(lOFile, piSlot);
        int liPicture = (UWClassicLayoutUw2.Runes.SpellClassFirst[liWord & 0xF] + ((liWord & 0xF0) >> 4)) & 0xFF;

        return liPicture < UWClassicLayoutUw2.Runes.NoSpellPicture ? liPicture : -1;
    }

    /// <summary>try_clear's right button: the spell's name and how long it has left (part U4d step 2).</summary>
    public string ActiveSpellText(int piSlot)
    {
        return mOScreen.ActiveSpellLine(piSlot);
    }

    public void DescribeActiveSpell(int piSlot)
    {
        mOScreen.ModernDescribeActiveSpell(piSlot);
    }

    /// <summary>try_clear's left button: dispel_spell.</summary>
    public void CancelActiveSpell(int piSlot)
    {
        mOScreen.ModernCancelActiveSpell(piSlot);
    }

    /// <summary>The screen's record stands (the shelf and the bag are read from it).</summary>
    public bool IsReady => mOScreen.ModernRunesReady;

    /// <summary>GameInputMode is not 0: mous_in_rune and try_cast return (uw2 RUNES.C:99, :172).</summary>
    public bool IsHeldByAnotherInput => mOScreen.IsInAnotherInputMode;

    public int RuneCount => UWPlayerRecordUw2.RuneCount;

    /// <summary>FIRST_RUNESTONE plus the rune (items.h).</summary>
    public int RuneObject(int piRune)
    {
        return UWClassicLayoutUw2.Runes.FirstRunestone + piRune;
    }

    /// <summary>player->runebag's bit.</summary>
    public bool HasRune(int piRune)
    {
        UWPlayerFile lOFile = fFile;

        return lOFile != null && piRune >= 0 && piRune < UWPlayerRecordUw2.RuneCount && UWPlayerRecordUw2.HasRune(lOFile, piRune);
    }

    public bool HasRunes(int piSequence)
    {
        if (fFile == null)
            return false;

        foreach (int liRune in UWRuneShelfModel.RunesOf(piSequence))
        {
            if (!HasRune(liRune))
                return false;
        }

        return true;
    }

    /// <summary>The character's level halved, rounded up (player_cast, the same rule in both games).</summary>
    public int Circle => UWVitalsUw2.Current != null && UWVitalsUw2.Current.Vitals != null ? UWSpellCasting.CircleOf(UWVitalsUw2.Current.Vitals.Level) : 0;

    /// <summary>The bag first (the first game's order, UWSpellCasting.CanCast), then UWSpellCasting.CanCast with the
    /// level and the whole mana the cast itself asks (UWSpellHostUw2).</summary>
    public UWSpellCasting.CastCheckEnum CastCheck(int piSequence)
    {
        UWVitalsUw2 lOPlayer = UWVitalsUw2.Current;

        if (lOPlayer == null || lOPlayer.Vitals == null || !HasRunes(piSequence))
            return UWSpellCasting.CastCheckEnum.RuneMissing;

        return UWSpellCasting.CanCast(piSequence, lOPlayer.Vitals.Level, lOPlayer.Vitals.CurrentMana, null);
    }

    /// <summary>player->shelf, as the screen keeps it; null before the record stands.</summary>
    public IReadOnlyList<int> Shelf => mOScreen.ModernRunesReady ? mOScreen.ShelfSelected : null;

    /// <summary>mous_in_rune's left button.</summary>
    public void AddToShelf(int piRune)
    {
        mOScreen.ModernAddRune(piRune);
    }

    public void PutOnShelf(IReadOnlyList<int> pORunes)
    {
        if (!mOScreen.IsInAnotherInputMode)
            mOScreen.PutOnShelf(pORunes);
    }

    /// <summary>mous_in_rune's clear strip.</summary>
    public void ClearShelf()
    {
        if (!mOScreen.IsInAnotherInputMode)
            mOScreen.ClearShelf();
    }

    /// <summary>mous_in_rune's right button.</summary>
    public void LookAtRune(int piRune)
    {
        mOScreen.ModernLookAtRune(piRune);
    }

    /// <summary>try_cast.</summary>
    public void CastShelf()
    {
        mOScreen.ModernCastShelf();
    }

    /// <summary>The action bar's spell, by the same rules as the shelf's cast, the shelf left as it is (as the
    /// first game's UWHudRunes.CastSequence).</summary>
    public void CastSequence(int piSequence)
    {
        mOScreen.ModernCastSequence(piSequence);
    }

    /// <summary>
    /// The spell's picture by this game's tables: a missile's from IUWMagicVariant.GetProjectileId (this game has six);
    /// a lasting spell's (classes 0 to 3, and class 11's Speed, Roaming Sight, Telekinesis and Freeze Time by the
    /// effect they add, UWExtraSpellRulesUw2) as parse_aspells finds the active spell's: the class's first SPELLS.GR
    /// picture plus the effect, in a byte (the motion class's 0xFF wraps), none from 0x1E on.
    /// </summary>
    public UWTexture SpellPicture(int piSequence)
    {
        DataImport lOData = Data;

        if (lOData == null || UWGameSession.Current == null || !UWRunicMagic.TryGetSpell(piSequence, out UWRunicMagic.Spell lOSpell))
            return null;

        int liMinor = UWRunicMagic.GetEffectMinor(lOSpell.MinorClass);

        if (lOSpell.MajorClass == UWSpellCasting.ProjectileMajorClass)
        {
            int liMissile = UWGameSession.Current.Game.Magic.GetProjectileId(liMinor);

            return liMissile >= 0 ? lOData.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, liMissile) : null;
        }

        if (lOSpell.MajorClass == UWExtraSpellRulesUw2.MajorClass)
        {
            switch (liMinor)
            {
                case UWExtraSpellRulesUw2.SpeedSpell: liMinor = UWMiscSpellRules.SpeedEffectMinor; break;
                case UWExtraSpellRulesUw2.RoamingSightSpell: liMinor = UWMiscSpellRules.RoamingSightEffectMinor; break;
                case UWExtraSpellRulesUw2.TelekinesisSpell: liMinor = UWMiscSpellRules.TelekinesisEffectMinor; break;
                case UWExtraSpellRulesUw2.FreezeTimeSpell: liMinor = UWMiscSpellRules.FreezeTimeEffectMinor; break;
                default: return null;
            }
        }
        else if (lOSpell.MajorClass > UWSpellCasting.LastLastingMajorClass)
            return null;

        if (lOSpell.MajorClass >= UWClassicLayoutUw2.Runes.SpellClassFirst.Length)
            return null;

        int liPicture = (UWClassicLayoutUw2.Runes.SpellClassFirst[lOSpell.MajorClass] + (liMinor & 0xF)) & 0xFF;
        List<UWTexture> lOIcons = lOData.Textures.GetTexturesByType(UWTexture.TextureTypes.SPELLS);

        return liPicture < UWClassicLayoutUw2.Runes.NoSpellPicture && lOIcons != null && liPicture < lOIcons.Count ? lOIcons[liPicture] : null;
    }

    public int ShownHiddenSpells => (1 << UWKnowledgeSources.HiddenSpells.Length) - 1;

    // ------------------------------------------------- IUWModernMessages

    public IReadOnlyList<string> Lines => UWGameScreenUw2.SaidLines;

    private readonly List<float> mOLineTimes = new List<float>();

    private readonly List<string> mOTimedLines = new List<string>();

    private readonly List<float> mONewTimes = new List<float>();

    /// <summary>
    /// EACH LINE ITS OWN TIME, as the first game's log keeps them (per user 2026-11-23, "the same mechanisms as UW1":
    /// until then every line carried the last line's time and the box's lines faded together). The screen keeps the lines
    /// only, the newest last, the oldest dropped at the front, the last one rewritten while a count or a name is typed:
    /// the lines still there keep their times, the ones after them are new and carry the time the screen said last.
    /// </summary>
    public IReadOnlyList<float> LineTimes
    {
        get
        {
            IReadOnlyList<string> lOLines = UWGameScreenUw2.SaidLines;
            float lfSaid = UWGameScreenUw2.LastSaidAt;
            int liKept = 0;
            int liFrom = mOTimedLines.Count;

            // The fewest lines dropped at the front such that the rest still begins the screen's list.
            for (int liDrop = 0; liDrop <= mOTimedLines.Count; liDrop++)
            {
                int liRest = mOTimedLines.Count - liDrop;

                if (liRest > lOLines.Count)
                    continue;

                bool lbSame = true;

                for (int liAt = 0; liAt < liRest && lbSame; liAt++)
                    lbSame = string.Equals(mOTimedLines[liDrop + liAt], lOLines[liAt]);

                if (lbSame)
                {
                    liFrom = liDrop;
                    liKept = liRest;

                    break;
                }
            }

            mONewTimes.Clear();

            for (int liAt = 0; liAt < lOLines.Count; liAt++)
                mONewTimes.Add(liAt < liKept ? mOLineTimes[liFrom + liAt] : lfSaid);

            mOLineTimes.Clear();
            mOLineTimes.AddRange(mONewTimes);
            mOTimedLines.Clear();

            for (int liAt = 0; liAt < lOLines.Count; liAt++)
                mOTimedLines.Add(lOLines[liAt]);

            return mOLineTimes;
        }
    }

    /// <summary>[MORE] stands (the scroll's pending lines), not the input held until the buttons are up after the last page
    /// (UWGameScreenUw2.IsMoreShown).</summary>
    public bool IsWaitingForPage => UWGameScreenUw2.IsMoreShown;

    /// <summary>The screen types its lines (a count, a save's name) into the last line of its scroll.</summary>
    public int PromptLine => mOScreen.IsTypingLine || UWGameScreenUw2.IsAskingYesNo ? UWGameScreenUw2.SaidLines.Count - 1 : -1;

    public bool ShowsPromptCursor => mOScreen.IsTypingLine;

    public bool IsAskingYesNo => UWGameScreenUw2.IsAskingYesNo;

    /// <summary>wyorn's question (the screen's UWYesNoPrompt, the first game's class) for the first game's question box
    /// (part U4d step 4); the scroll's line still shows it and the answer, as the first game's messages do.</summary>
    public string YesNoQuestion => mOScreen.ModernYesNoQuestion;

    public void AnswerYesNo(bool pbYes)
    {
        mOScreen.ModernAnswerYesNo(pbYes);
    }

    // THE MANTRA STAYS EMPTY, for a reason: the second game has no shrines to chant at; it trains with teachers (the
    // help's Teachers tab), so the question box never asks for a mantra here.

    public bool IsChanting => false;

    public string MantraPrompt => string.Empty;

    public string MantraText => string.Empty;

    public void FinishMantra(bool pbCancel)
    {
    }

    public bool IsPromptActive => mOScreen.IsTypingLine || UWGameScreenUw2.IsAskingYesNo;

    public void Say(string psMessage)
    {
        UWGameScreenUw2.Say(psMessage);
    }

    /// <summary>A general sentence by the first game's number, which the windows name: the second game's own place of
    /// the same sentence (UWStringIds.InGame), nothing for one that has none here.</summary>
    public void SayGeneral(int piIndex)
    {
        string lsText = General(piIndex);

        if (!string.IsNullOrEmpty(lsText))
            UWGameScreenUw2.Say(lsText);
    }

    public string General(int piIndex)
    {
        int liOwn = UWStringIds.InGame(UWGame.Uw2, piIndex);
        DataImport lOData = Data;

        return liOwn >= 0 && lOData != null ? UWText.General(lOData, liOwn) : string.Empty;
    }

    // ------------------------------------------------- IUWModernWorld

    /// <summary>The big map, or the classic screen over the view (the options panel in the modern scheme): the
    /// modern HUD is not shown under either, as the screen hid its stopgap.</summary>
    public bool IsMapOpen => UWMapScreenUw2.IsOpen || mOScreen.IsClassicCovering;

    public bool IsConversationOpen => UWConversationScreenUw2.IsOpen;

    /// <summary>The name of what the view's middle is on, as the first game's (the free pointer's thing is not named;
    /// per user 2026-11-24): its crosshair name (UWModernWorldInput.CrosshairName) with a capital.</summary>
    public string TargetName
    {
        get
        {
            DataImport lOData = Data;
            UWEntityInfo lOThing = UWWorldViewUw2.Current != null ? UWWorldViewUw2.Current.PointedEntity() : null;

            // A closed secret door and a wall panel are wall on the crosshair, as on the first game (GitHub issue 31).
            if (lOThing != null && lOThing.ObjectData != null
                && (UWModernWorldInput.HidesFromCrosshair(lOThing.ObjectData.ID) || UWModernWorldInput.IsWallPanel(lOThing.ObjectData.ID)))
                return null;

            string lsName = lOData != null ? UWModernWorldInput.CrosshairName(lOData, lOThing) : null;

            return string.IsNullOrEmpty(lsName) ? null : UWText.Capitalised(lsName);
        }
    }

    /// <summary>A locked door under the crosshair and the key at hand that fits (UWWorldViewUw2.CrosshairLock; the automatic
    /// key, part U4e step 2).</summary>
    public bool TryGetTargetLock(out int piKeyId)
    {
        piKeyId = -1;

        return UWWorldViewUw2.Current != null && UWWorldViewUw2.Current.CrosshairLock(out piKeyId);
    }

    /// <summary>What E would do with what the crosshair is on (UWWorldViewUw2.CrosshairPadVerb).</summary>
    public string TargetPadVerb => UWWorldViewUw2.Current != null ? UWWorldViewUw2.Current.CrosshairPadVerb() : null;

    /// <summary>The hold of E for the direct use (UWWorldViewUw2, the first game's UWHoldTap).</summary>
    public float UseHoldProgress => UWWorldViewUw2.Current != null ? UWWorldViewUw2.Current.UseHoldProgress : -1f;

    public bool IsFightMode => UWGameScreenUw2.Command == UWGameScreenUw2.CommandEnum.Fight;

    public void ToggleFightMode()
    {
        mOScreen.ModernToggleFight();
    }

    /// <summary>A missile spell waits for its aim or a target spell (or a thing used on another) for its target, on the
    /// attack host (part U4e step 1; the root asked the host itself until then).</summary>
    public bool IsSpellAiming => UWAttackUw2.Current != null && UWAttackUw2.Current.IsAnySpellWaiting;

    /// <summary>Escape's first link (UWModernUi's chain): the missile lets go, else the target spell, their mana unpaid.</summary>
    public void CancelSpellAim()
    {
        UWAttackUw2 lOAttack = UWAttackUw2.Current;

        if (lOAttack == null)
            return;

        if (lOAttack.IsSpellWaiting)
            lOAttack.CancelSpellMissile();
        else if (lOAttack.IsTargetSpellWaiting)
            lOAttack.CancelSpellTarget();
    }

    // The world menu of the free pointer (UWModernBags.OpenWorldMenu, opened by UWWorldViewUw2 as by the first game's).

    public void UseThing(UWEntityInfo pOTarget, UWEntityInfo pOPickableBehind)
    {
        if (UWWorldViewUw2.Current != null)
            UWWorldViewUw2.Current.UseFromMenu(pOTarget);
    }

    public void LookAt(UWEntityInfo pOTarget)
    {
        if (UWWorldViewUw2.Current != null)
            UWWorldViewUw2.Current.LookAtThing(pOTarget);
    }

    public bool TakeToPointer(UWEntityInfo pOTarget)
    {
        return UWWorldViewUw2.Current != null && UWWorldViewUw2.Current.TakeToPointer(pOTarget);
    }

    /// <summary>The map carried and the automap on (UWAutomapStateUw2): with it off the character does not know where
    /// they are - the stopgap minimap's rule, which the first game's minimap keeps (part U4d step 3).</summary>
    public bool CarriesMap => mOScreen.ModernCarriesMap;

    /// <summary>The big map, as the carried map's use opens it.</summary>
    public bool OpenMap()
    {
        return mOScreen.ModernOpenMap();
    }

    private UWAutomapPicturesUw2 mOAutomap;

    public IUWMinimapPictures Automap => mOAutomap ?? (mOAutomap = new UWAutomapPicturesUw2(() => Data));

    /// <summary>set_compass's heading in sixteen steps, as the classic screen's compass reads the view.</summary>
    public int CompassStep => mOScreen.CompassStepNow;

    /// <summary>
    /// The second game's eyes (adjust_eyes, the classic screen's) for the heading strip. THE STRIP'S TIME IS THE FIRST
    /// GAME'S (the same mechanism, UWEyesDisplay): lit for the eyes' seconds after the hit, fading in and out; the
    /// pictures are this game's - the blink while it runs, then its widest frame of the condition (adjust_eyes' hold).
    /// </summary>
    public UWModernFoeEyes FoeEyes => mOScreen.ModernFoeEyes();

    /// <summary>Empty, for a reason: reading tracks is not built on the second game (OffersTrack is false, so the panel
    /// offers no button and nothing calls this).</summary>
    public void Track()
    {
    }

    public void Camp()
    {
        if (CanCamp)
            UWSleepUw2.Current.Camp(mOScreen.Inventory);
    }

    /// <summary>do_option_shortcut and the options icon: not with a thing on the pointer nor in the roaming view.</summary>
    public bool RefusesMenu()
    {
        return mOScreen.ModernRefusesMenu();
    }

    /// <summary>check_save: not while GameInputMode is set (uw2 GAMESCR.C:178).</summary>
    public bool RefusesToSave()
    {
        return mOScreen.ModernRefusesToSave();
    }

    /// <summary>While the bag windows draw the pointer's thing, the screen's own pointer shows nothing.</summary>
    public void ShowPointerForBags(bool pbShow)
    {
        mOScreen.ModernThingIsPointer(!pbShow);
    }

    /// <summary>The mouse buttons act in the world (UWModernWorldInput.MouseInWorld): the pointer locked, held to look
    /// on the world, or free on the world with the weapon drawn - not on a window (UWAttackUw2's blow asks it).</summary>
    public bool IsMouseInWorld => !UWModernPointer.IsFree || UWModernPointer.WorldTakesClicks() || (IsFightMode && UWModernPointer.FreePointerOnWorld());

    public bool IsPointerFree => UWModernPointer.IsFree;

    /// <summary>Paralysed or asleep in the Void, the panel takes nothing (mous_in_panel).</summary>
    public bool HoldsPanels => UWVitalsUw2.PlayerParalysed || UWVitalsUw2.PlayerAsleepInVoid;

    /// <summary>player_key_sleep, as F10 (UWWorldViewUw2.fCampByKey).</summary>
    public bool CanCamp => UWSleepUw2.Current != null && !UWVitalsUw2.PlayerParalysed && !UWVitalsUw2.PlayerAsleepInVoid;

    public bool OffersCamp => true;

    /// <summary>Reading tracks is not built on the second game.</summary>
    public bool OffersTrack => false;

    // The game menu's saves, the options panel's own pieces (UWSavegameSlots, UWWorldViewUw2.Save, UWGameStartUw2),
    // part U4d step 4.

    public string SaveSlotName(int piSlot)
    {
        return mOScreen.ModernSaveSlotName(piSlot);
    }

    public bool HasSave(int piSlot)
    {
        return mOScreen.ModernHasSave(piSlot);
    }

    public string WriteSave(int piSlot, string psName)
    {
        return mOScreen.ModernWriteSave(piSlot, psName);
    }

    /// <summary>DoSaveRest's outcome alone in the scroll, as the options panel says it.</summary>
    public void ReportSave(string psError)
    {
        mOScreen.ModernReportSave(psError);
    }

    public void ReportNoSave()
    {
        mOScreen.ModernReportNoSave();
    }

    /// <summary>RestoreGame: the session starts again from the slot (check_rest lets an action in progress go).</summary>
    public void LoadSave(int piSlot)
    {
        mOScreen.ModernLoadSave(piSlot);
    }

    /// <summary>Quit, yes: the port leaves to the menu, as the options panel's quit.</summary>
    public void QuitToMainMenu()
    {
        mOScreen.ModernQuit();
    }

    // ------------------------------------------------- IUWModernArt

    // The flasks, the gem and the weapon (part A step 3, 2026-11-23).

    /// <summary>The flasks as full as the record says, the vitality flask green while poisoned, one picture per
    /// state and kept, so the vitals get the same object for the same state.</summary>
    private readonly Dictionary<int, Texture2D> mOFlasks = new Dictionary<int, Texture2D>();

    private readonly Dictionary<int, Texture2D> mOGems = new Dictionary<int, Texture2D>();

    private int miArtColours = -1;

    private DataImport mOArtData;

    /// <summary>The kept pictures go when the colour help or the data changes.</summary>
    private void fCheckArt()
    {
        DataImport lOData = Data;

        if (miArtColours == UWColourVision.Version && mOArtData == lOData)
            return;

        miArtColours = UWColourVision.Version;
        mOArtData = lOData;

        foreach (Texture2D lOOld in mOFlasks.Values)
            Object.Destroy(lOOld);

        foreach (Texture2D lOOld in mOGems.Values)
            Object.Destroy(lOOld);

        mOFlasks.Clear();
        mOGems.Clear();
    }

    public Texture2D HealthFlask => fFlask(true);

    public Texture2D ManaFlask => fFlask(false);

    /// <summary>
    /// A flask as one picture (UWFlasksUw2: the empty flask and the slices up to value * 12 / most, the black round
    /// it clear, the hatch on the liquid while poisoned where the player switched it on), kept per state.
    /// </summary>
    private Texture2D fFlask(bool pbVitality)
    {
        DataImport lOData = Data;
        UWPlayerFile lOFile = fFile;

        if (lOData == null || lOData.Textures == null)
            return null;

        fCheckArt();

        int liValue = lOFile == null ? 0 : pbVitality ? UWPlayerRecordUw2.Vitality(lOFile) : UWPlayerRecordUw2.Mana(lOFile);
        int liMost = lOFile == null ? 0 : pbVitality ? UWPlayerRecordUw2.MaxVitality(lOFile) : UWPlayerRecordUw2.MaxMana(lOFile);
        bool lbPoisoned = pbVitality && lOFile != null && UWPlayerRecordUw2.IsPoisoned(lOFile);
        bool lbHatch = lbPoisoned && UWUserSettings.MarkPoison;
        int liFirst = !pbVitality ? UWClassicLayoutUw2.Flasks.ManaFirst
            : lbPoisoned ? UWClassicLayoutUw2.Flasks.PoisonFirst : UWClassicLayoutUw2.Flasks.HealthFirst;
        int liLevel = liMost > 0 ? Mathf.Clamp(liValue * UWClassicLayoutUw2.Flasks.Levels / liMost, 0, UWClassicLayoutUw2.Flasks.Levels) : 0;
        int liKey = (liFirst << 8) | (lbHatch ? 0x80 : 0) | liLevel;

        if (mOFlasks.TryGetValue(liKey, out Texture2D lOKept) && lOKept != null)
            return lOKept;

        UWTexture lOComposed = UWFlasksUw2.Compose(lOData.Textures, liFirst, liLevel, lbHatch);

        if (lOComposed == null)
            return null;

        Texture2D lOFlask = UWTextureFactory.Upload(UWFlasksUw2.ToRgba(lOComposed, UWColourVision.Filter), lOComposed.Width, lOComposed.Height,
            FilterMode.Point, TextureWrapMode.Clamp, "UW2 modern flask " + liKey);

        mOFlasks[liKey] = lOFlask;

        return lOFlask;
    }

    private static UWTexture fPicture(DataImport pOData, UWTexture.TextureTypes peType, int piId)
    {
        try
        {
            UWTexture lOSource = pOData.Textures.GetTextureByType(peType, piId);

            return lOSource != null && lOSource.PaletteIndices != null && lOSource.Width > 0 && lOSource.Height > 0 ? lOSource : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The power gem's picture for the screen's frame (POWER.GR), kept per frame.</summary>
    public Texture2D PowerGem
    {
        get
        {
            DataImport lOData = Data;

            if (lOData == null || lOData.Textures == null)
                return null;

            fCheckArt();

            int liFrame = mOScreen.PowerFrame;

            if (!mOGems.TryGetValue(liFrame, out Texture2D lOGem) || lOGem == null)
            {
                lOGem = UWScreenUi.BuildPicture(lOData.Textures, UWTexture.TextureTypes.POWER, liFrame);
                mOGems[liFrame] = lOGem;
            }

            return lOGem;
        }
    }

    /// <summary>The gap between the flasks and the gem, in the original's pixels.</summary>
    private const int VitalsGap = 4;

    /// <summary>
    /// NO SHELF YET: the second game's frame has no stone shelf to cut as the first game's has (its look comes
    /// with the art of part B on). The flasks and the gem stand free in a row - flask, gem, flask - their bottoms
    /// on one line, on a clear picture as wide and high as the row; the pictures come cut free, so no masks.
    /// pbGemApart (the gem in the compass since stage U6 step 1): the flasks alone, side by side.
    /// </summary>
    public UWModernVitalsArt BuildVitalsArt(bool pbGemApart)
    {
        DataImport lOData = Data;
        UWTexture lOFlask = lOData != null && lOData.Textures != null ? fPicture(lOData, UWTexture.TextureTypes.FLASKS, UWClassicLayoutUw2.Flasks.Empty) : null;
        UWTexture lOGem = lOData != null && lOData.Textures != null ? fPicture(lOData, UWTexture.TextureTypes.POWER, 0) : null;
        int liFlaskWidth = lOFlask != null ? lOFlask.Width : 0;
        int liFlaskHeight = lOFlask != null ? lOFlask.Height : 0;
        int liGemWidth = lOGem != null ? lOGem.Width : 0;
        int liGemHeight = lOGem != null ? lOGem.Height : 0;
        int liHeight = Mathf.Max(1, Mathf.Max(liFlaskHeight, liGemHeight));
        UWModernVitalsArt lOArt = new UWModernVitalsArt();

        // Without the gem in the row its room goes: the flasks a gap apart.
        int liGemRoom = pbGemApart ? 0 : liGemWidth + VitalsGap;

        lOArt.Layout = new UWModernHudArt.ShelfLayout
        {
            HealthFlask = new Vector2Int(0, liHeight - liFlaskHeight),
            Gem = new Vector2Int(liFlaskWidth + VitalsGap, liHeight - liGemHeight),
            ManaFlask = new Vector2Int(liFlaskWidth + VitalsGap + liGemRoom, liHeight - liFlaskHeight),
            Width = Mathf.Max(1, (2 * liFlaskWidth) + VitalsGap + liGemRoom),
            Height = liHeight
        };

        // A clear picture: the vitals keep and release it as the first game's shelf.
        lOArt.Shelf = UWTextureFactory.Upload(new byte[UWUploadPixels.BytesPerPixel], 1, 1, FilterMode.Point, TextureWrapMode.Clamp, "UW2 modern shelf (clear)");

        return lOArt;
    }

    /// <summary>The weapon as the screen draws it this frame (fShowWeapon): its picture, weap_x with the sway in it
    /// and weap_y, in the second game's 208 x 128 view, cut at the view's edges as the view cuts it.</summary>
    public bool TryGetWeapon(out UWModernWeaponFrame pOFrame)
    {
        pOFrame = default;

        if (!mOScreen.TryGetModernWeapon(out Sprite lOPicture, out float lfX, out int liY) || lOPicture.texture == null)
            return false;

        pOFrame.Texture = lOPicture.texture;
        pOFrame.ViewWidth = UWClassicLayoutUw2.Frame.ViewWindowWidth;
        pOFrame.ViewHeight = UWClassicLayoutUw2.Frame.ViewWindowHeight;
        pOFrame.ClipToView = true;
        // The original's size: a scaled picture (the sprites' xBR) has as many pixels per unit as its scale.
        pOFrame.Size = new Vector2(lOPicture.rect.width / lOPicture.pixelsPerUnit, lOPicture.rect.height / lOPicture.pixelsPerUnit);
        pOFrame.OffsetX = Mathf.FloorToInt(lfX);
        pOFrame.Sway = lfX - Mathf.FloorToInt(lfX);
        pOFrame.OffsetY = liY;
        pOFrame.Ready = 1f;

        return true;
    }

    private Texture2D mOPointerPlain;

    private Texture2D mOPointerAim;

    private Texture2D mOPointerTarget;

    private DataImport mOPointerData;

    /// <summary>
    /// The game's pointer for the view's middle, as the stopgap drew it: only in the locked look with no bags or
    /// runes open; the aim's picture while a drawn bow or a missile spell waits for its aim, the enchanter's for a
    /// target spell, else the plain centre pointer.
    /// </summary>
    public Texture2D Crosshair(bool pbLocked)
    {
        DataImport lOData = Data;

        if (!pbLocked || lOData == null || lOData.Textures == null || !UWPointer.IsLookLocked)
            return null;

        if (mOPointerData != lOData)
        {
            mOPointerData = lOData;
            mOPointerPlain = null;
            mOPointerAim = null;
            mOPointerTarget = null;
        }

        UWAttackUw2 lOAttack = UWAttackUw2.Current;

        if (lOAttack != null && (lOAttack.IsAiming || lOAttack.IsSpellWaiting))
            return mOPointerAim ?? (mOPointerAim = fPointer(lOData, UWCursors.CursorEnum.Aim));

        if (lOAttack != null && lOAttack.IsTargetSpellWaiting)
            return mOPointerTarget ?? (mOPointerTarget = fPointer(lOData, UWCursors.CursorEnum.Enchant));

        return mOPointerPlain ?? (mOPointerPlain = fPointer(lOData, UWCursors.CursorEnum.Center));
    }

    private static Texture2D fPointer(DataImport pOData, UWCursors.CursorEnum pePointer)
    {
        return UWScreenUi.BuildTexture(pOData.Textures, UWTexture.TextureTypes.CURSORS, (int)pePointer, FilterMode.Point, true);
    }

    /// <summary>
    /// THE COMPASS OF A STEP (stage U6 step 1): this game's face and needle over the box of all its pictures
    /// (UWCompassDiscBuilder.ComposeUw2Compass), whole as the classic screen draws them - laid on the recess, they cover
    /// the cross baked into it (UWCompassGeometry) - or pbFreed the cross and the needle's tip alone. A new texture,
    /// through the colour help; the power gem in its middle is the compass element's own (UWModernCompass).
    /// </summary>
    public Texture2D Compass(int piStep, bool pbFreed)
    {
        DataImport lOData = Data;
        UWPicture lOPicture = lOData != null && lOData.Textures != null
            ? UWCompassDiscBuilder.ComposeUw2Compass(lOData.Textures, piStep, pbFreed)
            : null;

        return lOPicture != null
            ? UWTextureFactory.Upload(UWUploadPixels.FromPicture(lOPicture, UWColourVision.Filter), lOPicture.Width, lOPicture.Height, Filter,
                TextureWrapMode.Clamp, "UW2 modern compass " + piStep)
            : null;
    }

    /// <summary>The view's picture in the classic frame: Classic Wide's, stage U7 (Has(ViewPicture) stays off).</summary>
    public Sprite ViewPicture => null;

    /// <summary>The first game's frame has dragons, this game's has none: Classic Wide builds none here
    /// (UWClassicWideGeometry.HasDragons).</summary>
    public Sprite DragonSprite(int piIndex)
    {
        return null;
    }

    // The paperdoll's figure (part B step 2, 2026-11-23).

    /// <summary>BODIES.GR's picture: female * 5 + body (load_inventory_pix, as the classic page).</summary>
    public int FigureBody
    {
        get
        {
            UWPlayerFile lOFile = fFile;

            return lOFile != null ? (UWPlayerRecordUw2.IsFemale(lOFile) ? FemaleBodies : 0) + UWPlayerRecordUw2.Body(lOFile) : 0;
        }
    }

    private const int FemaleBodies = 5;

    private static readonly UWArmorItemMap.BodySlot[] msFigureSlots =
    {
        UWArmorItemMap.BodySlot.Helmet, UWArmorItemMap.BodySlot.Gloves, UWArmorItemMap.BodySlot.Legs, UWArmorItemMap.BodySlot.Chest,
        UWArmorItemMap.BodySlot.Boots
    };

    /// <summary>The armour picture a worn piece shows (DisplayInvSpecial: the type and the condition, condition 3
    /// above type 14), -1 for an empty place.</summary>
    private int fArmourPicture(UWArmorItemMap.BodySlot peSlot)
    {
        UWInventoryModel lOInventory = mOScreen.Inventory;
        UWObject lOItem = lOInventory != null ? lOInventory.GetEquipped(peSlot) : null;

        if (lOItem == null)
            return -1;

        int liType = lOItem.ID & 0x1F;
        int liCondition = liType > UWClassicLayoutUw2.Inventory.LastConditionedType ? 3 : lOItem.Quality >> 4;

        return liType + (liCondition * UWClassicLayoutUw2.Inventory.ArmourPicturesPerCondition);
    }

    /// <summary>The body and the five pictures folded into one number: it changes when the figure's look does.</summary>
    public int FigureVersion
    {
        get
        {
            int liVersion = FigureBody;

            foreach (UWArmorItemMap.BodySlot leSlot in msFigureSlots)
                liVersion = (liVersion * 131) + fArmourPicture(leSlot) + 1;

            return liVersion;
        }
    }

    /// <summary>The second game draws every pair of leggings at one place.</summary>
    public int FigureLegsDrop => 0;

    public UWTexture FigureSource(UWModernFigurePiece pePiece)
    {
        DataImport lOData = Data;
        UWPlayerFile lOFile = fFile;
        int liPiece = (int)pePiece - (int)UWModernFigurePiece.Helmet;

        if (lOData == null || lOData.Textures == null || lOFile == null || liPiece < 0 || liPiece >= msFigureSlots.Length)
            return null;

        int liPicture = fArmourPicture(msFigureSlots[liPiece]);

        return liPicture >= 0
            ? fPicture(lOData, UWPlayerRecordUw2.IsFemale(lOFile) ? UWTexture.TextureTypes.ARMOR_F : UWTexture.TextureTypes.ARMOR_M, liPicture)
            : null;
    }

    /// <summary>
    /// The body's and the armour's pictures as the classic page draws them (fCachedPicture: clear in their own colour
    /// 0), shared and following the colour help (UWGrTextures) - for the original character page (stage U5 step 3; null
    /// until then, so the page showed no figure, per user at the test of 197). The modern panel builds its figure from
    /// FigureSource and FigureBody instead.
    /// </summary>
    public Texture2D FigureTexture(UWModernFigurePiece pePiece)
    {
        DataImport lOData = Data;
        UWPlayerFile lOFile = fFile;

        if (lOData == null || lOData.Textures == null || lOFile == null)
            return null;

        if (pePiece == UWModernFigurePiece.Body)
            return UWGrTextures.Get(lOData.Textures, UWTexture.TextureTypes.BODIES, FigureBody, Filter, UWGrTextures.ModeEnum.ZeroClear);

        int liPiece = (int)pePiece - (int)UWModernFigurePiece.Helmet;

        if (liPiece < 0 || liPiece >= msFigureSlots.Length)
            return null;

        int liPicture = fArmourPicture(msFigureSlots[liPiece]);

        return liPicture >= 0
            ? UWGrTextures.Get(lOData.Textures, UWPlayerRecordUw2.IsFemale(lOFile) ? UWTexture.TextureTypes.ARMOR_F : UWTexture.TextureTypes.ARMOR_M,
                liPicture, Filter, UWGrTextures.ModeEnum.ZeroClear)
            : null;
    }
}

/// <summary>
/// THE SECOND GAME'S MINIMAP PICTURES (IUWMinimapPictures, UW2 plan part U4d step 3): the whole map screen as the big map
/// and the help's map tab compose it (UWHelpMapUw2, UWMapScreenUw2.ComposeScreen: the blank map with the seen tiles, in
/// the map's palette), without the map's arrow - the minimap draws its own - and the notes in the map's ink. The
/// character's pixel is worked out anew at every ask (UWMapScreenUw2.PlayerPixelNow), so the minimap sees each step.
/// </summary>
public sealed class UWAutomapPicturesUw2 : IUWMinimapPictures
{
    private readonly System.Func<DataImport> mFData;

    private readonly UWHelpMapUw2 mOMap = new UWHelpMapUw2();

    public UWAutomapPicturesUw2(System.Func<DataImport> pFData)
    {
        mFData = pFData;
    }

    public Rect TilesRect => new Rect(0f, 0f, UWScreenUi.ScreenWidth, UWScreenUi.ScreenHeight);

    public bool TryGetPlayerPixel(out Vector2 pOPixel)
    {
        return UWMapScreenUw2.PlayerPixelNow(mFData(), out pOPixel);
    }

    public Texture2D BuildTiles(bool pbPlayerMark)
    {
        DataImport lOData = mFData();
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOData == null || lOLoader == null || !mOMap.Build(lOData, lOLoader.CurrentLevelIndex + 1, pbPlayerMark))
            return null;

        return mOMap.Picture;
    }

    /// <summary>None: the tiles' picture is the whole map screen.</summary>
    public Texture2D GetParchment()
    {
        return null;
    }

    /// <summary>The notes the last BuildTiles laid out (UWHelpMapUw2: the small font, the map's ink).</summary>
    public System.Collections.Generic.List<UWAutomapPictures.Note> GetNotes()
    {
        return mOMap.Notes;
    }
}
