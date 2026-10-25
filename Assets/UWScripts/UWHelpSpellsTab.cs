using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>The help's SPELLS tab: the rune spells circle by circle, with their rune pictures, a
/// click to lay them into the casting hollow, and the spells the manual does not name folded
/// away. Moved out of UWHelpWindow (cleanup 8.6, 2026-10-24).</summary>
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

        fDrawSpellList(pOFlow, lOData, true);

        pOFlow.Space(16f);

        if (pOFlow.Fold(mbShowMoreSpells ? UWText.T("help.spells.hide_more")
                : UWText.T("help.spells.show_more")))
            mbShowMoreSpells = !mbShowMoreSpells;

        if (mbShowMoreSpells)
            fDrawSpellList(pOFlow, lOData, false);
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
    private void fDrawSpellList(IUWHelpFlow pOFlow, DataImport pOData, bool pbManual)
    {
        int liCircle = 0;
        UWPlayerData lOPlayer = pOData != null ? pOData.InitialPlayer : null;
        UWGameUI lOUi = UWScene.GameUi;
        IReadOnlyList<int> lOShelf = lOUi != null ? lOUi.Runes.Shelf.Selected : null;

        foreach (UWRunicMagic.Spell lOSpell in UWRunicMagic.AllSpells)
        {
            if (lOSpell.RuneSequence == UWRunicMagic.EmptySequence)
                continue;

            bool lbNamed = UWHelpContent.ManualSpellNotes.TryGetValue(lOSpell.Index, out string lsNote);

            if (lbNamed != pbManual)
                continue;

            if (lOSpell.Level != liCircle)
            {
                liCircle = lOSpell.Level;

                pOFlow.Space(10f);
                pOFlow.Line(UWText.F("help.spells.circle", liCircle, lOSpell.ManaCost), UWHelpLookEnum.SubHeading);
            }

            string lsName = pOData != null ? UWRunicMagic.GetName(lOSpell, pOData.Strings) : string.Empty;
            List<int> lORunes = UWRuneShelfModel.RunesOf(lOSpell.RuneSequence);
            bool lbAllInBag = UWRuneShelfModel.HasRunes(lOSpell.RuneSequence, lOPlayer);

            bool lbOnShelf = fIsOnShelf(lOShelf, lORunes);

            mOIcons.Clear();

            foreach (int liRune in lORunes)
            {
                bool lbInBag = lOPlayer != null && lOPlayer.HasRune((UWPlayerData.Rune)liRune);

                mOIcons.Add(fRuneIcon(pOData, liRune, !lbInBag));
            }

            // The rune pictures, then the name, the runes and whether they lie in the hollow; a press
            // on the row lays the runes there (IUWHelpFlow.RuneRow; the IMGUI calls are
            // UWHelpImguiFlow's since cleanup 8b.3).
            string lsText = string.Format("<b>{0}</b>   <color=#{1}>{2}</color>{3}",
                string.IsNullOrEmpty(lsName) ? "?" : lsName, UWGuiTheme.AccentHex,
                UWRunicMagic.DescribeRunes(lOSpell.RuneSequence),
                lbOnShelf ? "   <i>" + UWText.T("help.spells.in_hollow") + "</i>" : string.Empty);

            if (pOFlow.RuneRow(mOIcons, lsText, lbAllInBag && lOUi != null))
                lOUi.Runes.PutOnShelf(lORunes);

            if (lbNamed)
                pOFlow.Line(UWText.T(lsNote), UWHelpLookEnum.Dim);

            // Room between the spells, so each stands apart (per user, 2026-09-26).
            pOFlow.Space(EntrySpacing);
        }
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
