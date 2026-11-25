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
///
/// PER GAME (UW2 plan, stage U5 step 1, 2026-11-23): the places, the colours, the paging strip and
/// whether the page is opaque are the running game's (UWStatsPageGeometry), the list is the
/// host's sheet (the second game's begins with the skill points). The first game's numbers are the
/// ones this page had as constants.
/// </summary>
public class UWModernStatsPage : IUWModernPart
{
    private const int SkillRows = UWStatsPageGeometry.SkillRows;

    private const int RowHeight = UWStatsPageGeometry.RowHeight;

    /// <summary>The running game's measures, as of the last drawing.</summary>
    private UWStatsPageGeometry mOGeometry = UWStatsPageGeometry.Uw1;

    private RawImage mOPage;

    private Texture2D mOPageTexture;

    /// <summary>Name, class, level, the six values, then six skill names and six skill values.</summary>
    private Text[] mOLines;

    /// <summary>What the page says, and the list's offset.</summary>
    private readonly UWCharacterSheet mOSheet = new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Original, SkillRows);

    private float miScale = 1f;

    /// <summary>Where it was drawn (bottom-left origin); empty while hidden.</summary>
    public Rect ScreenRect { get; private set; }

    public UWModernStatsPage()
    {
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
            mOLines[liAt] = UWUiKit.CreateText(pORoot, "Stats line " + liAt, TextAnchor.UpperLeft, Color.white, UWModernUi.TextUi, UWUiKit.StatsText);
    }

    /// <summary>Once a frame while the modern HUD shows; pfPixelScale is UWModernUi.PixelScale.</summary>
    public void Update(float pfPixelScale)
    {
        ScreenRect = Rect.zero;

        if (mOPage == null)
            return;

        UWModernClassicFrame.ApplySquash(mOContent);

        if (!UWModernLayout.IsStatsPanelShown || UWModernUi.Session == null || UWModernUi.Data == null)
        {
            Hide();
            return;
        }

        // The original's stats page, shared and following the colour help in place (UWGrTextures); the second game's
        // opaque everywhere (its near black is no clear colour).
        mOGeometry = UWStatsPageGeometry.Of(UWModernUi.Data.Textures);
        mOPageTexture = UWGrTextures.Get(UWModernUi.Data.Textures, UWTexture.TextureTypes.PANELS, 2, UWModernUi.Filter,
            mOGeometry.IsPageOpaque ? UWGrTextures.ModeEnum.Opaque : UWGrTextures.ModeEnum.Picture);

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

        UWCharacterSheet lOSheet = UWModernUi.Session.Character.BindSheet(mOSheet);
        int liFont = Mathf.Max(8, Mathf.RoundToInt(6.8f * miScale));
        UWStatsPageGeometry lOAt = mOGeometry;
        Color lOValue = UWUiKit.Colour(lOAt.ValueColour(UWModernUi.Data.Palettes));
        Color lOSkill = UWUiKit.Colour(lOAt.SkillColour(UWModernUi.Data.Palettes));

        fLine(0, lOSheet.Name, lOAt.NameTop, TextAnchor.MiddleCenter, lOAt.NameCentreX, lOValue, liFont, lOPlaced);
        fLine(1, lOSheet.ClassName, lOAt.ClassTop, TextAnchor.MiddleLeft, lOAt.LabelLeft, lOValue, liFont, lOPlaced);
        fLine(2, lOSheet.LevelText, lOAt.ClassTop, TextAnchor.MiddleRight, lOAt.ValueRight, lOValue, liFont, lOPlaced);

        for (int liRow = 0; liRow < UWCharacterSheet.ValueCount; liRow++)
            fLine(3 + liRow, lOSheet.Value(liRow), lOAt.ValueTop(liRow), TextAnchor.MiddleRight, lOAt.ValueRight, lOValue, liFont, lOPlaced);

        for (int liRow = 0; liRow < SkillRows; liRow++)
        {
            lOSheet.TryGetSkill(lOSheet.EntryAtRow(liRow), out string lsName, out string lsValue);

            int liY = lOAt.SkillTop(liRow);

            fLine(9 + liRow, lsName, liY, TextAnchor.MiddleLeft, lOAt.LabelLeft, lOSkill, liFont, lOPlaced);
            fLine(9 + SkillRows + liRow, lsValue, liY, TextAnchor.MiddleRight, lOAt.ValueRight, lOSkill, liFont, lOPlaced);
        }
    }

    /// <summary>The list's paging by the left button and the wheel - with the pointer free.</summary>
    public void UpdateClicks(UWControls pOControls)
    {
        if (pOControls == null || !UWPointer.HasMouse || ScreenRect.width <= 0f || !UWModernPointer.IsFree)
            return;

        Vector2 lOPointer = UWPointer.Position;

        // No paging while the Classic Wide panel turns (per user, 2026-10-20), as the classic
        // stats page (UWHudPanel asks UWPanelTurn.IsAtRestOn): the page stands squeezed then.
        if (!ScreenRect.Contains(lOPointer) || UWModernClassicFrame.IsTurning)
            return;

        int liStep = 0;
        float lfWheel = UWPointer.Scroll.y;

        if (Mathf.Abs(lfWheel) > 0.01f)
            liStep = lfWheel > 0f ? -1 : 1;
        else if (pOControls.Player.CursorDrag.WasPressedThisFrame() && !UWModernPointer.PanelsHeld)
        {
            float lfX = (lOPointer.x - ScreenRect.x) / (miScale * UWModernHudArt.PixelAspectX);
            float lfY = (ScreenRect.yMax - lOPointer.y) / miScale;

            // The running game's strip: the first game's bottom eight rows split at 37, the second game's
            // bottom ten of the panel's mouse region split at 43 (UWStatsPageGeometry).
            liStep = mOGeometry.PagingStepAt(lfX, lfY);
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

    /// <summary>A line at a page row: left from x, right-aligned to x, or centred on x.</summary>
    private void fLine(int piAt, string psText, int piY, TextAnchor peAlign, float pfX, Color pOColour, int piFont, Rect pOPage)
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
        float lfX = peAlign == TextAnchor.MiddleCenter ? pOPage.x + (pfX * lfAspect) - (pOPage.width * 0.5f)
            : peAlign == TextAnchor.MiddleRight ? pOPage.x + (pfX * lfAspect) - pOPage.width
            : pOPage.x + (pfX * lfAspect);

        UWUiKit.SetRect(lOText.rectTransform, lfX, lfTop - lfHeight, pOPage.width, lfHeight);
    }
}
