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
///
/// THE LIST PAGES as in the original (ovr145_445): a left click in the page's bottom eight rows,
/// left of x 37 back, from 37 on forward, one entry per click; the wheel over the page does the
/// same. With the pointer free; the page counts as interface (UWModernHud.IsOverSpellIcons).
/// </summary>
public class UWModernStatsPage
{
    private const int NameY = 7;

    private const int ClassY = 14;

    private const int FirstRowY = 21;

    private const int RowHeight = 7;

    private const int LabelLeft = 6;

    private const int ValueRight = 76;

    private const int SkillFirstRowY = 64;

    private const int SkillRows = 6;

    private const int SkillCount = 20;

    /// <summary>The paging: the bottom eight rows, split at x 37.</summary>
    private const int PagingTop = 106;

    private const int PagingSplit = 37;

    private static readonly Color msValue = Color.black;

    private static readonly Color msSkill = new Color(0x62 / 255f, 0x64 / 255f, 0x7F / 255f, 1f);

    private readonly UWGameUI mOUi;

    private RawImage mOPage;

    private Texture2D mOPageTexture;

    private int miArtVersion = -1;

    /// <summary>Name, class, level, the six values, then six skill names and six skill values.</summary>
    private Text[] mOLines;

    private int miOffset;

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
        GameObject lOContent = new GameObject("Stats page content", typeof(RectTransform));
        lOContent.transform.SetParent(pOParent, false);
        mOContent = (RectTransform)lOContent.transform;
        mOContent.anchorMin = Vector2.zero;
        mOContent.anchorMax = Vector2.zero;
        mOContent.pivot = Vector2.zero;
        mOContent.sizeDelta = Vector2.zero;

        Transform pORoot = mOContent;

        mOPage = fCreateImage(pORoot, "Stats page");
        UWPixelArtUI.Apply(mOPage);
        mOLines = new Text[9 + (2 * SkillRows)];

        for (int liAt = 0; liAt < mOLines.Length; liAt++)
            mOLines[liAt] = fCreateText(pORoot, "Stats line " + liAt);
    }

    /// <summary>Once a frame while the modern HUD shows; pfPixelScale is UWModernHud.PixelScale.</summary>
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

        if (mOPageTexture == null || miArtVersion != UWColourVision.Version)
        {
            if (mOPageTexture != null)
                Object.Destroy(mOPageTexture);

            mOPageTexture = UWModernHudArt.BuildPanelPage(mOUi.mOUWData.Textures, 2, mOUi.TextureFilterMode);
            miArtVersion = UWColourVision.Version;
        }

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
        fSetRect(mOPage.rectTransform, lOPlaced.x, lOPlaced.y, lOPlaced.width, lOPlaced.height);

        UWCharacter lOCharacter = mOUi.mCharacter;
        UWPlayerData lOPlayer = mOUi.mOUWData.InitialPlayer;
        int liFont = Mathf.Max(8, Mathf.RoundToInt(6.8f * miScale));

        fLine(0, fName(lOPlayer), NameY, TextAnchor.MiddleCenter, 0, msValue, liFont, lOPlaced);
        fLine(1, fClass(lOPlayer), ClassY, TextAnchor.MiddleLeft, LabelLeft, msValue, liFont, lOPlaced);
        fLine(2, fLevel(lOCharacter.Level), ClassY, TextAnchor.MiddleRight, ValueRight, msValue, liFont, lOPlaced);

        string[] lsValues =
        {
            lOCharacter.Strength.ToString(),
            lOCharacter.Dexterity.ToString(),
            lOCharacter.Intelligence.ToString(),
            Mathf.RoundToInt(lOCharacter.CurrentHP) + "/" + Mathf.RoundToInt(lOCharacter.MaxHP),
            Mathf.RoundToInt(lOCharacter.CurrentMana) + "/" + Mathf.RoundToInt(lOCharacter.MaxMana),
            (lOCharacter.Experience / 10).ToString()
        };

        for (int liRow = 0; liRow < lsValues.Length; liRow++)
            fLine(3 + liRow, lsValues[liRow], FirstRowY + (liRow * RowHeight), TextAnchor.MiddleRight, ValueRight, msValue, liFont, lOPlaced);

        for (int liRow = 0; liRow < SkillRows; liRow++)
        {
            fSkill(miOffset + liRow, out string lsName, out string lsValue);

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

        if (!ScreenRect.Contains(lOPointer))
            return;

        int liStep = 0;
        float lfWheel = lOMouse.scroll.ReadValue().y;

        if (Mathf.Abs(lfWheel) > 0.01f)
            liStep = lfWheel > 0f ? -1 : 1;
        else if (pOControls.Player.CursorDrag.WasPressedThisFrame())
        {
            float lfX = (lOPointer.x - ScreenRect.x) / (miScale * UWModernHudArt.PixelAspectX);
            float lfY = (ScreenRect.yMax - lOPointer.y) / miScale;

            if (lfY >= PagingTop)
                liStep = lfX < PagingSplit ? -1 : 1;
        }

        if (liStep != 0)
            miOffset = Mathf.Clamp(miOffset + liStep, 0, SkillCount - SkillRows);
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

        fSetRect(lOText.rectTransform, lfX, lfTop - lfHeight, pOPage.width, lfHeight);
    }

    private string fName(UWPlayerData pOPlayer)
    {
        return pOPlayer != null && !string.IsNullOrEmpty(pOPlayer.Name) ? pOPlayer.Name.ToUpper() : "AVATAR";
    }

    /// <summary>The class from string block 2 from 0x18 on (UWHudPanel.fGetClassName).</summary>
    private string fClass(UWPlayerData pOPlayer)
    {
        try
        {
            return pOPlayer != null ? mOUi.mOUWData.Strings.Blocks[2].Strings[0x18 + pOPlayer.CharacterClass].Trim().ToUpper() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string fLevel(int piLevel)
    {
        int liLastTwo = piLevel % 100;
        int liLast = piLevel % 10;
        string lsSuffix = "TH";

        if (liLastTwo < 11 || liLastTwo > 13)
        {
            if (liLast == 1)
                lsSuffix = "ST";
            else if (liLast == 2)
                lsSuffix = "ND";
            else if (liLast == 3)
                lsSuffix = "RD";
        }

        return piLevel + lsSuffix;
    }

    /// <summary>An entry of the list: attack, defence, then the skills (string block 2, as
    /// UWHudPanel.fGetSkillEntry).</summary>
    private void fSkill(int piEntry, out string psName, out string psValue)
    {
        psName = string.Empty;
        psValue = string.Empty;

        if (piEntry < 0 || piEntry >= SkillCount)
            return;

        try
        {
            psName = mOUi.mOUWData.Strings.Blocks[2].Strings[UWPlayerData.SkillNameStringIndex + piEntry].Trim();
        }
        catch
        {
            return;
        }

        UWCharacter lOCharacter = mOUi.mCharacter;

        psValue = piEntry == 0 ? lOCharacter.Attack.ToString()
            : piEntry == 1 ? lOCharacter.Defence.ToString()
            : lOCharacter.GetSkill((UWPlayerData.Skill)(piEntry - 2)).ToString();
    }

    private RawImage fCreateImage(Transform pOParent, string psName)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        RawImage lOImage = lOObject.GetComponent<RawImage>();
        lOImage.raycastTarget = false;
        lOImage.enabled = false;

        return lOImage;
    }

    private Text fCreateText(Transform pOParent, string psName)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Text));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        Text lOText = lOObject.GetComponent<Text>();
        lOText.font = UWInterfaceFont.Font;
        UWUiFonts.Register(lOText, mOUi);
        lOText.raycastTarget = false;
        lOText.horizontalOverflow = HorizontalWrapMode.Overflow;
        lOText.verticalOverflow = VerticalWrapMode.Overflow;
        lOText.enabled = false;

        return lOText;
    }

    private static void fSetRect(RectTransform pORect, float pfX, float pfY, float pfWidth, float pfHeight)
    {
        pORect.anchoredPosition = new Vector2(pfX, pfY);
        pORect.sizeDelta = new Vector2(pfWidth, pfHeight);
    }
}
