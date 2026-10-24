using UnityEngine;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// THE ORIGINAL'S STATS PANEL IN THE MODERN INTERFACE (Classic+, per user 2026-10-10: the
/// original panels "each a single panel", "always visible"): the page PANELS.GR 2 as an element
/// of its own (UWModernLayout.ElementEnum.StatsPanel), placed by the layout editor - by default at
/// the right edge, above the middle.
///
/// On it, as the classic frame's page (UWHudPanel.fRefreshStats) has them, at its places: the
/// name centred, the class and the level, the six values right-aligned beside the labels the
/// page carries (strength, dexterity, intelligence, vitality, mana, experience), and the skill
/// list - six rows of twenty entries (attack, defence, the eighteen skills), names in the
/// skills' blue-grey. The texts are the interface's (UWUiFonts: modern or the original's font).
/// What they say, the list's offset and its paging are UWCharacterSheet's in the original's
/// spelling, as on the classic page (cleanup 5.9, 2026-10-20).
///
/// THE LIST PAGES as in the original (ovr145_445): a left click in the page's bottom eight rows,
/// left of x 37 back, from 37 on forward, one entry per click; the wheel over the page does the
/// same. With the pointer free; the page counts as interface (UWModernHud.IsOverParts).
/// </summary>
public class UWModernStatsPage : IUWModernPart
{
    private const int NameY = 7;

    private const int ClassY = 14;

    private const int FirstRowY = 21;

    private const int RowHeight = 7;

    private const int LabelLeft = 6;

    private const int ValueRight = 76;

    private const int SkillFirstRowY = 64;

    private const int SkillRows = 6;

    /// <summary>The paging: the bottom eight rows, split at x 37.</summary>
    private const int PagingTop = 106;

    private const int PagingSplit = 37;

    private static readonly Color msValue = Color.black;

    private static readonly Color msSkill = new Color(0x62 / 255f, 0x64 / 255f, 0x7F / 255f, 1f);

    private readonly UWGameUI mOUi;

    private RawImage mOPage;

    private Texture2D mOPageTexture;

    /// <summary>Name, class, level, the six values, then six skill names and six skill values.</summary>
    private Text[] mOLines;

    /// <summary>What the page says, and the list's offset.</summary>
    private readonly UWCharacterSheet mOSheet = new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Original, SkillRows);

    private float miScale = 1f;

    /// <summary>Where it was drawn (bottom-left origin); empty while hidden.</summary>
    public Rect ScreenRect { get; private set; }

    public UWModernStatsPage(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    /// <summary>The page's parts in one group, which the classic frame's panel turn squeezes
    /// (UWModernClassicFrame.ApplySquash).</summary>
    private RectTransform mOContent;

    public void Build(Transform pOParent)
    {
        mOContent = UWUiKit.CreateGroup(pOParent, "Stats page content");
        mOContent.sizeDelta = Vector2.zero;

        Transform pORoot = mOContent;

        mOPage = UWUiKit.CreateRawImage(pORoot, "Stats page");
        UWPixelArtUI.Apply(mOPage);
        mOLines = new Text[9 + (2 * SkillRows)];

        // Unity's own alignment and colour: each line gets its own when it is placed (fLine).
        for (int liAt = 0; liAt < mOLines.Length; liAt++)
            mOLines[liAt] = UWUiKit.CreateText(pORoot, "Stats line " + liAt, TextAnchor.UpperLeft, Color.white, mOUi, UWUiKit.StatsText);
    }

    /// <summary>Once a frame while the modern HUD shows; pfPixelScale is UWModernUi.PixelScale.</summary>
    public void Update(float pfPixelScale)
    {
        ScreenRect = Rect.zero;

        if (mOPage == null)
            return;

        UWModernClassicFrame.ApplySquash(mOContent);

        if (!UWModernLayout.IsStatsPanelShown || mOUi == null || mOUi.mOUWData == null || mOUi.mCharacter == null)
        {
            Hide();
            return;
        }

        // The original's stats page, shared and following the colour help in place (UWGrTextures).
        mOPageTexture = UWGrTextures.Get(mOUi.mOUWData.Textures, UWTexture.TextureTypes.PANELS, 2, mOUi.TextureFilterMode,
            UWGrTextures.ModeEnum.Picture);

        if (mOPageTexture == null)
        {
            Hide();
            return;
        }

        miScale = pfPixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.StatsPanel);

        float lfWidth = mOPageTexture.width * miScale * UWModernHudArt.PixelAspectX;
        float lfHeight = mOPageTexture.height * miScale;
        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.StatsPanel,
            new Rect(Screen.width - lfWidth - (4f * pfPixelScale), (Screen.height * 0.5f) + (4f * pfPixelScale), lfWidth, lfHeight));

        UWModernLayout.Report(UWModernLayout.ElementEnum.StatsPanel, lOPlaced);
        ScreenRect = lOPlaced;

        mOPage.texture = mOPageTexture;
        mOPage.enabled = true;
        UWUiKit.SetRect(mOPage.rectTransform, lOPlaced.x, lOPlaced.y, lOPlaced.width, lOPlaced.height);

        UWCharacterSheet lOSheet = mOSheet.Bind(mOUi.mOUWData.Strings, mOUi.mOUWData.InitialPlayer, mOUi.mCharacter.Vitals);
        int liFont = Mathf.Max(8, Mathf.RoundToInt(6.8f * miScale));

        fLine(0, lOSheet.Name, NameY, TextAnchor.MiddleCenter, 0, msValue, liFont, lOPlaced);
        fLine(1, lOSheet.ClassName, ClassY, TextAnchor.MiddleLeft, LabelLeft, msValue, liFont, lOPlaced);
        fLine(2, lOSheet.LevelText, ClassY, TextAnchor.MiddleRight, ValueRight, msValue, liFont, lOPlaced);

        for (int liRow = 0; liRow < UWCharacterSheet.ValueCount; liRow++)
            fLine(3 + liRow, lOSheet.Value(liRow), FirstRowY + (liRow * RowHeight), TextAnchor.MiddleRight, ValueRight, msValue, liFont, lOPlaced);

        for (int liRow = 0; liRow < SkillRows; liRow++)
        {
            lOSheet.TryGetSkill(lOSheet.EntryAtRow(liRow), out string lsName, out string lsValue);

            int liY = SkillFirstRowY + (liRow * RowHeight);

            fLine(9 + liRow, lsName, liY, TextAnchor.MiddleLeft, LabelLeft, msSkill, liFont, lOPlaced);
            fLine(9 + SkillRows + liRow, lsValue, liY, TextAnchor.MiddleRight, ValueRight, msSkill, liFont, lOPlaced);
        }
    }

    /// <summary>The list's paging by the left button and the wheel - with the pointer free.</summary>
    public void UpdateClicks(UWControls pOControls)
    {
        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        if (pOControls == null || lOMouse == null || ScreenRect.width <= 0f || !UWModernPointer.IsFree)
            return;

        Vector2 lOPointer = lOMouse.position.ReadValue();

        // No paging while the Classic Wide panel turns (per user, 2026-10-20), as the classic
        // stats page (UWHudPanel asks UWPanelTurn.IsAtRestOn): the page stands squeezed then.
        if (!ScreenRect.Contains(lOPointer) || UWModernClassicFrame.IsTurning)
            return;

        int liStep = 0;
        float lfWheel = lOMouse.scroll.ReadValue().y;

        if (Mathf.Abs(lfWheel) > 0.01f)
            liStep = lfWheel > 0f ? -1 : 1;
        else if (pOControls.Player.CursorDrag.WasPressedThisFrame() && !UWModernPointer.PanelsHeld)
        {
            float lfX = (lOPointer.x - ScreenRect.x) / (miScale * UWModernHudArt.PixelAspectX);
            float lfY = (ScreenRect.yMax - lOPointer.y) / miScale;

            if (lfY >= PagingTop)
                liStep = lfX < PagingSplit ? -1 : 1;
        }

        if (liStep != 0)
            mOSheet.PageSkills(liStep);
    }

    public void Hide()
    {
        ScreenRect = Rect.zero;

        if (mOPage != null)
            mOPage.enabled = false;

        if (mOLines == null)
            return;

        foreach (Text lOLine in mOLines)
            lOLine.enabled = false;
    }

    /// <summary>A line at a page row: left from x, right-aligned to x, or centred on the page.</summary>
    private void fLine(int piAt, string psText, int piY, TextAnchor peAlign, int piX, Color pOColour, int piFont, Rect pOPage)
    {
        Text lOText = mOLines[piAt];

        lOText.enabled = !string.IsNullOrEmpty(psText);

        if (!lOText.enabled)
            return;

        lOText.text = psText;
        lOText.color = pOColour;
        lOText.fontSize = piFont;
        lOText.alignment = peAlign;

        float lfAspect = miScale * UWModernHudArt.PixelAspectX;
        float lfTop = pOPage.yMax - (piY * miScale);
        float lfHeight = RowHeight * miScale;
        float lfX = peAlign == TextAnchor.MiddleCenter ? pOPage.x
            : peAlign == TextAnchor.MiddleRight ? pOPage.x + (piX * lfAspect) - pOPage.width
            : pOPage.x + (piX * lfAspect);

        UWUiKit.SetRect(lOText.rectTransform, lfX, lfTop - lfHeight, pOPage.width, lfHeight);
    }
}
