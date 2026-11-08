using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>The help's SPELLS tab: the rune spells circle by circle, with their rune pictures, a
/// click to lay them into the casting hollow, and below them either the spells the manual does not
/// name that the character has LEARNED, the half known ones and how many are still to find
/// (UWKnowledge, GitHub issue 19, 2026-11-08) - or, with the Gameplay window's "Show every mantra and
/// spell", all of them folded away as before. Moved out of UWHelpWindow (cleanup 8.6, 2026-10-24).</summary>
public sealed class UWHelpSpellsTab : UWHelpTab
{
    /// <summary>The panel with what the manual does not name - closed until asked for.</summary>
    private bool mbShowMoreSpells;

    public override UWHelpTabId Id => UWHelpTabId.Spells;

    public override string TitleId => "help.tab.spells";

    /// <summary>The list in either form (IUWHelpFlow, cleanup 8b.3, 2026-10-24).</summary>
    protected override void OnFlow(IUWHelpFlow pOFlow)
    {
        DataImport lOData = fData();

        pOFlow.Line(UWText.T("help.spells.title"), UWHelpLookEnum.Heading);
        pOFlow.Line(UWText.T("help.spells.intro"), UWHelpLookEnum.Dim);

        fDrawSpellList(pOFlow, lOData, (pOSpell, pbNamed) => pbNamed, null);

        // The second game has no notes from the first game's manual: every spell stands in the one list.
        if (fSecondGame() != null)
            return;

        pOFlow.Space(16f);

        if (!UWUserSettings.KnowledgeShowAll)
        {
            fDrawLearned(pOFlow, lOData);

            return;
        }

        if (pOFlow.Fold(mbShowMoreSpells ? UWText.T("help.spells.hide_more")
                : UWText.T("help.spells.show_more")))
            mbShowMoreSpells = !mbShowMoreSpells;

        if (mbShowMoreSpells)
            fDrawSpellList(pOFlow, lOData, (pOSpell, pbNamed) => !pbNamed, null);
    }

    /// <summary>The hidden spells the character knows, each with where from; the half known ones with the
    /// runes known so far; how many are still to find.</summary>
    private void fDrawLearned(IUWHelpFlow pOFlow, DataImport pOData)
    {
        UWKnowledge lOKnown = UWKnowledge.Current;
        UWPlayerData lOBag = pOData != null ? pOData.InitialPlayer : null;
        int liLearned = 0;

        pOFlow.Line(UWText.T("help.knowledge.learned"), UWHelpLookEnum.SubHeading);

        fDrawSpellList(pOFlow, pOData,
            (pOSpell, pbNamed) => !pbNamed && lOKnown.SpellState(pOSpell.Index, lOBag, out UWKnowledge.Entry _) == UWKnowledge.SpellStateEnum.Known,
            pOSpell =>
            {
                lOKnown.SpellState(pOSpell.Index, lOBag, out UWKnowledge.Entry lOEntry);
                liLearned++;

                return UWHelpKnowledge.SourceText(lOEntry);
            });

        bool lbHalf = false;

        foreach (int liSpell in UWKnowledgeSources.HiddenSpells)
        {
            if (lOKnown.SpellState(liSpell, lOBag, out UWKnowledge.Entry _) != UWKnowledge.SpellStateEnum.Half
                || liSpell < 0 || liSpell >= UWRunicMagic.AllSpells.Count)
                continue;

            string lsName = pOData != null ? UWRunicMagic.GetName(UWRunicMagic.AllSpells[liSpell], pOData.Strings) : string.Empty;

            pOFlow.Line(string.Format("<b>{0}</b>   <color=#{1}>{2}</color>", lsName, UWGuiTheme.AccentHex,
                UWKnowledgeSources.HalfRunes(liSpell)), UWHelpLookEnum.Text);
            pOFlow.Line(UWText.T("help.spells.half"), UWHelpLookEnum.Dim);
            pOFlow.Space(EntrySpacing);
            lbHalf = true;
        }

        if (liLearned == 0 && !lbHalf)
            pOFlow.Line(UWText.T("help.knowledge.none_yet"), UWHelpLookEnum.Dim);

        if (liLearned < UWKnowledgeSources.HiddenSpells.Length)
            pOFlow.Line(UWText.F("help.knowledge.to_find", UWKnowledgeSources.HiddenSpells.Length - liLearned), UWHelpLookEnum.Dim);
    }

    /// <summary>
    /// The rune spells circle by circle - the ones the manual names, or the others.
    ///
    /// RUNE ICONS AND A CLICK TO EQUIP (feature wish of the user, 2026-09-27): each spell shows
    /// its runes as the game's rune pictures, grey where the rune is not in the bag yet (per
    /// user). A left click on the row lays the runes into the casting hollow, replacing what
    /// lay there - only when all of them are in the bag, as the shelf itself only offers what
    /// the bag holds. The spell lying in the hollow right now is marked.
    /// </summary>
    private void fDrawSpellList(IUWHelpFlow pOFlow, DataImport pOData, System.Func<UWRunicMagic.Spell, bool, bool> pFShow,
        System.Func<UWRunicMagic.Spell, string> pFUnderLine)
    {
        int liCircle = 0;
        UWPlayerData lOPlayer = pOData != null ? pOData.InitialPlayer : null;
        UWGameUI lOUi = UWScene.GameUi;
        IReadOnlyList<int> lOShelf = lOUi != null ? lOUi.Runes.Shelf.Selected : null;

        // THE SECOND GAME (per user, 2026-11-06: "for testing it would be cool if the spells worked in the help"):
        // its rune bag and hollow live in its record and on UWGameScreenUw2, not in UWPlayerData and UWGameUI; its
        // spell table is AllSpells already, and the first game's manual notes (keyed by the first game's spell
        // numbers) are not shown.
        UWPlayerFile lOSecond = fSecondGame();
        UWGameScreenUw2 lOScreenUw2 = lOSecond != null ? UWGameScreenUw2.Current : null;

        if (lOSecond != null)
            lOShelf = lOScreenUw2 != null ? lOScreenUw2.ShelfSelected : null;

        foreach (UWRunicMagic.Spell lOSpell in UWRunicMagic.AllSpells)
        {
            if (lOSpell.RuneSequence == UWRunicMagic.EmptySequence)
                continue;

            string lsNote = null;
            bool lbNamed = lOSecond == null && UWHelpContent.ManualSpellNotes.TryGetValue(lOSpell.Index, out lsNote);

            if (lOSecond == null && !pFShow(lOSpell, lbNamed))
                continue;

            if (lOSpell.Level != liCircle)
            {
                liCircle = lOSpell.Level;

                pOFlow.Space(10f);
                pOFlow.Line(UWText.F("help.spells.circle", liCircle, lOSpell.ManaCost), UWHelpLookEnum.SubHeading);
            }

            string lsName = pOData != null ? UWRunicMagic.GetName(lOSpell, pOData.Strings) : string.Empty;
            List<int> lORunes = UWRuneShelfModel.RunesOf(lOSpell.RuneSequence);
            bool lbAllInBag = lOSecond != null ? fHasRunesUw2(lOSecond, lORunes) : UWRuneShelfModel.HasRunes(lOSpell.RuneSequence, lOPlayer);

            bool lbOnShelf = fIsOnShelf(lOShelf, lORunes);

            mOIcons.Clear();

            foreach (int liRune in lORunes)
            {
                bool lbInBag = lOSecond != null ? UWPlayerRecordUw2.HasRune(lOSecond, liRune)
                    : lOPlayer != null && lOPlayer.HasRune((UWPlayerData.Rune)liRune);

                mOIcons.Add(fRuneIcon(pOData, liRune, !lbInBag));
            }

            // The rune pictures, then the name, the runes and whether they lie in the hollow; a press
            // on the row lays the runes there (IUWHelpFlow.RuneRow; the IMGUI calls are
            // UWHelpImguiFlow's since cleanup 8b.3).
            string lsText = string.Format("<b>{0}</b>   <color=#{1}>{2}</color>{3}",
                string.IsNullOrEmpty(lsName) ? "?" : lsName, UWGuiTheme.AccentHex,
                UWRunicMagic.DescribeRunes(lOSpell.RuneSequence),
                lbOnShelf ? "   <i>" + UWText.T("help.spells.in_hollow") + "</i>" : string.Empty);

            if (lOSecond != null)
            {
                if (pOFlow.RuneRow(mOIcons, lsText, lbAllInBag && lOScreenUw2 != null))
                    lOScreenUw2.PutOnShelf(lORunes);
            }
            else if (pOFlow.RuneRow(mOIcons, lsText, lbAllInBag && lOUi != null))
                lOUi.Runes.PutOnShelf(lORunes);

            if (lbNamed)
                pOFlow.Line(UWText.T(lsNote), UWHelpLookEnum.Dim);

            // Where a learned spell came from (fDrawLearned).
            if (pFUnderLine != null)
                pOFlow.Line(pFUnderLine(lOSpell), UWHelpLookEnum.Dim);

            // Room between the spells, so each stands apart (per user, 2026-09-26).
            pOFlow.Space(EntrySpacing);
        }
    }

    /// <summary>The second game's record while it is played, else null.</summary>
    private static UWPlayerFile fSecondGame()
    {
        return UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null;
    }

    private static bool fHasRunesUw2(UWPlayerFile pOFile, List<int> pORunes)
    {
        foreach (int liRune in pORunes)
        {
            if (!UWPlayerRecordUw2.HasRune(pOFile, liRune))
                return false;
        }

        return true;
    }

    private static bool fIsOnShelf(IReadOnlyList<int> pOShelf, List<int> pORunes)
    {
        if (pOShelf == null || pOShelf.Count != pORunes.Count)
            return false;

        for (int liAt = 0; liAt < pORunes.Count; liAt++)
        {
            if (pOShelf[liAt] != pORunes[liAt])
                return false;
        }

        return true;
    }

    /// <summary>One spell's rune pictures (UWHelpFlowText.RuneIconSize holds their size since 8b.3).</summary>
    private readonly List<Texture2D> mOIcons = new List<Texture2D>();

    private readonly Texture2D[] mORuneIcons = new Texture2D[UWRunicMagic.EmptyRune];

    private readonly Texture2D[] mOGreyRuneIcons = new Texture2D[UWRunicMagic.EmptyRune];

    /// <summary>A rune's picture (the rune stone object, 0xE8 + rune) in real colours - the
    /// panel's icon textures carry palette indices for a shader IMGUI does not have. Grey:
    /// desaturated and dimmed, for a rune not in the bag.</summary>
    private Texture2D fRuneIcon(DataImport pOData, int piRune, bool pbGrey)
    {
        if (pOData == null || piRune < 0 || piRune >= mORuneIcons.Length)
            return null;

        Texture2D[] lOCache = pbGrey ? mOGreyRuneIcons : mORuneIcons;

        if (lOCache[piRune] != null)
            return lOCache[piRune];

        UWTexture lOSource = pOData.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS,
            UWPlayerData.FirstRuneObjectId + piRune);

        if (lOSource == null)
            return null;

        Color32[] lOPixels = UWTextureFactory.Colours(lOSource);

        if (pbGrey)
        {
            for (int liAt = 0; liAt < lOPixels.Length; liAt++)
            {
                Color32 lOPixel = lOPixels[liAt];
                byte lyGrey = (byte)((((lOPixel.r * 30) + (lOPixel.g * 59) + (lOPixel.b * 11)) / 100) * 55 / 100);

                lOPixels[liAt] = new Color32(lyGrey, lyGrey, lyGrey, lOPixel.a);
            }
        }

        Texture2D lOTexture = UWTextureFactory.FromColours(lOPixels, lOSource.Width, lOSource.Height, FilterMode.Point,
            TextureWrapMode.Clamp, "UWHelpWindow rune " + piRune + (pbGrey ? " grey" : string.Empty));

        lOCache[piRune] = lOTexture;

        return lOTexture;
    }
}
