using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN HUD'S HEADING (out of UWModernHud, cleanup 5.16, 2026-10-22): top centre the heading
/// in sixteen points, as the compass needle has, in the heading strip that opens on a hit
/// (fUpdateStrip); in Classic Wide only the gargoyle's eyes on the frame's head
/// (fUpdateStripClassic). ScreenRect is the heading as a button (per user, 2026-10-04): the classic
/// compass's click reports the status (UWGameActions.ReportStatus); here a click on the heading does
/// (UWModernHudInput).
/// </summary>
public class UWModernHeadingStrip : UWModernPart
{
    private Text mOHeading;

    /// <summary>The heading under the free pointer: gold (UWModernHudInput sets it).</summary>
    public bool IsHovered { get; set; }

    private RawImage mOStrip;

    private RawImage mOStripEyes;

    private Text mOFoeName;

    private Text mOFoeState;

    private UWEyesDisplay mOEyes;

    /// <summary>0 closed (the heading alone) to 1 open (the gargoyle and the foe).</summary>
    private float mfStripOpen;

    /// <summary>The open strip's half width, original pixels - from the foe's name, fixed while
    /// the strip moves so the brand stands still.</summary>
    private int miStripOpenHalf = UWModernHudArt.StripMinOpenHalf;

    /// <summary>The foe's text, from the heading's centre.</summary>
    private const float FoeFromCentre = 14f;

    /// <summary>Per width and open half (width * 1000 + half), each built for the colours
    /// (UWArtCache).</summary>
    private readonly UWArtCache<int> mOStripTextures = new UWArtCache<int>();

    /// <summary>Seconds the strip takes to open or close.</summary>
    private const float StripSeconds = 0.25f;

    private static readonly Color[] msFoeColours =
    {
        new Color32(120, 210, 110, 255), new Color32(230, 200, 90, 255), new Color32(230, 90, 70, 255)
    };

    /// <summary>The foe's states and the sixteen headings by their ids in the text table (UWText,
    /// cleanup step 8.5, 2026-10-24).</summary>
    private static readonly string[] msFoeStates = { "hud.foe.healthy", "hud.foe.wounded", "hud.foe.critical" };

    private static readonly string[] msHeadings =
    {
        "hud.heading.n", "hud.heading.nne", "hud.heading.ne", "hud.heading.ene", "hud.heading.e", "hud.heading.ese",
        "hud.heading.se", "hud.heading.sse", "hud.heading.s", "hud.heading.ssw", "hud.heading.sw", "hud.heading.wsw",
        "hud.heading.w", "hud.heading.wnw", "hud.heading.nw", "hud.heading.nnw"
    };

    /// <summary>The original's compass, which stands in for the heading's text while it shows.</summary>
    private readonly UWModernCompass mOCompass;

    public UWModernHeadingStrip(UWModernHud pOHud, UWModernCompass pOCompass) : base(pOHud)
    {
        mOCompass = pOCompass;
    }

    public override void Build(Transform pORoot)
    {
        // The heading strip under the heading (fUpdateStrip).
        mOStrip = UWUiKit.CreateRawImage(pORoot, "Heading strip", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        UWPixelArtUI.Apply(mOStrip);
        mOStripEyes = UWUiKit.CreateRawImage(pORoot, "Gargoyle eyes", new Vector2(0.5f, 1f), new Vector2(0f, 1f));
        UWPixelArtUI.Apply(mOStripEyes);
        mOFoeName = UWUiKit.CreateText(pORoot, "Foe", new Vector2(0.5f, 1f), new Vector2(0f, 1f), TextAnchor.MiddleLeft, UWModernPalette.HudText, mOUi, UWUiKit.HudText);
        mOFoeState = UWUiKit.CreateText(pORoot, "Foe state", new Vector2(0.5f, 1f), new Vector2(0f, 1f), TextAnchor.MiddleLeft, UWModernPalette.HudText, mOUi, UWUiKit.HudText);

        mOHeading = UWUiKit.CreateText(pORoot, "Heading", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), TextAnchor.MiddleCenter, UWModernPalette.HudText, mOUi, UWUiKit.HudText);
    }

    public override void OnPixelScale(float pfPixelScale)
    {
        base.OnPixelScale(pfPixelScale);

        float lfMargin = 6f * pfPixelScale;
        int liFontSize = FontSize;

        mOHeading.fontSize = Mathf.RoundToInt(liFontSize * 1.1f);
        ((RectTransform)mOHeading.transform).anchoredPosition = new Vector2(0f, -lfMargin);
        ((RectTransform)mOHeading.transform).sizeDelta = new Vector2(400f, liFontSize * 2f);
    }

    protected override void fUpdate()
    {
        fUpdateHeading();
        fUpdateStrip();
    }

    /// <summary>After the compass's frame: the heading's text gives way to the compass while it
    /// shows (UWModernHud.fUpdateCompass).</summary>
    public void AfterCompass()
    {
        mOHeading.enabled = mOCompass.ScreenRect.width <= 0f;
    }

    /// <summary>CLASSIC WIDE: the gargoyle is the frame's; the strip, the heading and the foe's
    /// name stay away, and the eyes alone are drawn on its head, lit as the strip would show them.</summary>
    private void fUpdateStripClassic()
    {
        mOStrip.enabled = false;
        mOHeading.enabled = false;
        mOFoeName.enabled = false;
        mOFoeState.enabled = false;
        ScreenRect = Rect.zero;

        Texture2D lOEyes = mOEyes != null && mOEyes.IsLit ? mOEyes.CurrentFrame : null;

        mOStripEyes.enabled = lOEyes != null;

        if (lOEyes == null)
            return;

        Rect lORect = UWModernClassicFrame.EyesRect(lOEyes.width, lOEyes.height);
        RectTransform lOTransform = (RectTransform)mOStripEyes.transform;

        mOStripEyes.texture = lOEyes;
        mOStripEyes.color = new Color(1f, 1f, 1f, mOEyes.Alpha);
        // Anchored at the top centre, its pivot top left (Build).
        lOTransform.anchoredPosition = new Vector2(lORect.x - (Screen.width * 0.5f), -(Screen.height - lORect.yMax));
        lOTransform.sizeDelta = lORect.size;
    }

    /// <summary>The heading in sixteen points, as many as the compass needle has (UWHudCompass).</summary>
    private void fUpdateHeading()
    {
        Camera lOCamera = Camera.main;

        if (lOCamera == null)
            return;

        // The classic compass's step (UWHudCompass.CurrentStep): the view's in sixteen points, and
        // in the void the frozen one, as the original's compass shows it - until 2026-10-09 the
        // heading followed the view there.
        int liStep = mOUi.Compass.CurrentStep;

        mOHeading.text = UWText.T(msHeadings[liStep]);

        // Its button area around the word, and gold under the free pointer.
        float lfHeadingWidth = Mathf.Max(mOHeading.preferredWidth, FontSize * 2f) + FontSize;
        float lfHeadingHeight = FontSize * 1.8f;

        ScreenRect = new Rect((Screen.width - lfHeadingWidth) * 0.5f, Screen.height - (6f * miPixelScale) - lfHeadingHeight,
            lfHeadingWidth, lfHeadingHeight);
        mOHeading.color = IsHovered ? UWModernPalette.Gold : UWModernPalette.HudText;
    }

    /// <summary>
    /// THE HEADING STRIP (UWModernHudArt.BuildHeadingStrip, per user on mockups, 2026-10-04):
    /// closed around the heading; when a hit lights the gargoyle's eyes (UWEyesDisplay) it opens
    /// to both sides - the burned gargoyle with the original's eyes at the left, the foe's name and
    /// condition at the right - and closes again once they have faded. A click on it reports the
    /// status, as the heading did.
    /// </summary>
    private void fUpdateStrip()
    {
        if (mOEyes == null)
            mOEyes = mOHud.GetComponent<UWEyesDisplay>();

        if (UWModernClassicFrame.IsActive)
        {
            fUpdateStripClassic();
            return;
        }

        bool lbLit = mOEyes != null && mOEyes.IsLit;
        float lfElement = UWModernLayout.Scale(UWModernLayout.ElementEnum.Heading);
        float lfScale = miPixelScale * lfElement;
        string lsName = lbLit ? mOEyes.TargetName : null;
        int liGroup = mOEyes != null ? Mathf.Clamp(mOEyes.Group, 0, 2) : 0;

        // The texts first: their width decides how far the strip opens - decided while it is
        // closed, so the brand does not move while it opens.
        mOFoeName.text = lsName ?? string.Empty;
        mOFoeName.fontSize = Mathf.Max(9, Mathf.RoundToInt(FontSize * lfElement));
        mOFoeState.text = UWText.T(msFoeStates[liGroup]);
        mOFoeState.fontSize = Mathf.Max(9, Mathf.RoundToInt(FontSize * 0.75f * lfElement));
        mOHeading.fontSize = Mathf.RoundToInt(FontSize * 1.1f * lfElement);

        if (lbLit && mfStripOpen <= 0f)
        {
            float lfText = Mathf.Max(mOFoeName.preferredWidth, mOFoeState.preferredWidth) / lfScale;

            miStripOpenHalf = Mathf.Clamp(Mathf.CeilToInt(FoeFromCentre + lfText + 6f), UWModernHudArt.StripMinOpenHalf, 100);
        }

        mfStripOpen = Mathf.MoveTowards(mfStripOpen, lbLit ? 1f : 0f, Time.unscaledDeltaTime / StripSeconds);

        float lfEase = mfStripOpen * mfStripOpen * (3f - (2f * mfStripOpen));
        int liWidth = Mathf.RoundToInt(Mathf.Lerp(UWModernHudArt.StripClosedWidth, 2 * miStripOpenHalf, lfEase));

        // WHILE THE COMPASS STANDS IN FOR THE TEXT (per user, 2026-10-09: the closed strip was
        // empty then) the gargoyle sits in the closed strip's middle and moves to its place at the
        // left as the strip opens; without it the brand shows only on the open strip, as before.
        // The compass's rect is the last frame's (UWModernHud.fUpdateCompass runs after this).
        bool lbCompass = mOCompass != null && mOCompass.ScreenRect.width > 0f;

        // THE GARGOYLE AT THE ORIGINAL'S PIXEL PROPORTION (UWModernHudArt.PixelAspectX): the strip
        // is built that much wider in its own pixels and shown that much narrower, so it keeps its
        // width on the screen (the text's room) while the head and its eyes are narrowed.
        int liTexWidth = Mathf.RoundToInt(liWidth / UWModernHudArt.PixelAspectX);
        int liTexOpenHalf = Mathf.RoundToInt(miStripOpenHalf / UWModernHudArt.PixelAspectX);
        int liBrandLeft = UWModernHudArt.BrandLeft(liTexWidth, liTexOpenHalf);

        if (lbCompass)
            liBrandLeft = Mathf.RoundToInt(Mathf.Lerp(UWHudArt.BrandCentredLeft(liTexWidth), liBrandLeft, lfEase));

        // The brand's place follows from the width, the open half and the compass.
        int liKey = (((liTexWidth * 1000) + liTexOpenHalf) * 2) + (lbCompass ? 1 : 0);

        UWArtStamp lOStripStamp = UWVersionedTexture.Colours();

        if (!mOStripTextures.TryGet(liKey, lOStripStamp, out Texture2D lOTexture))
        {
            lOTexture = mOStripTextures.Set(liKey, lOStripStamp, lbCompass
                ? UWModernHudArt.BuildHeadingStripWithBrandAt(mOUi.mOUWData.Textures, liTexWidth, liBrandLeft, mOUi.TextureFilterMode)
                : UWModernHudArt.BuildHeadingStrip(mOUi.mOUWData.Textures, liTexWidth, liTexOpenHalf, mOUi.TextureFilterMode,
                    Mathf.RoundToInt(UWModernHudArt.StripClosedWidth / UWModernHudArt.PixelAspectX)));
        }

        float lfTop = 4f * miPixelScale;
        float lfHeight = UWModernHudArt.StripHeight * lfScale;
        float lfWidth = liTexWidth * lfScale * UWModernHudArt.PixelAspectX;

        // Its own place (UWModernLayout): the strip's rect, the heading's offset from the top
        // centre all its parts are anchored at.
        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.Heading,
            new Rect((Screen.width - lfWidth) * 0.5f, Screen.height - lfTop - lfHeight, lfWidth, lfHeight));
        float lfShiftX = Mathf.Round(lOPlaced.center.x - (Screen.width * 0.5f));

        lfTop = Screen.height - lOPlaced.yMax;
        UWModernLayout.Report(UWModernLayout.ElementEnum.Heading, lOPlaced);

        mOStrip.texture = lOTexture;
        mOStrip.enabled = true;
        ((RectTransform)mOStrip.transform).anchoredPosition = new Vector2(lfShiftX, -lfTop);
        ((RectTransform)mOStrip.transform).sizeDelta = new Vector2(lfWidth, lfHeight);

        // The heading in the strip's middle, and its click area the strip.
        ((RectTransform)mOHeading.transform).anchoredPosition = new Vector2(lfShiftX, -lfTop);
        ((RectTransform)mOHeading.transform).sizeDelta = new Vector2(UWModernHudArt.StripClosedWidth * lfScale, lfHeight);
        ScreenRect = lOPlaced;

        // The eyes in the brand, once the strip shows them.
        Texture2D lOEyes = lbLit ? mOEyes.CurrentFrame : null;
        int liEyesX = liBrandLeft + UWModernHudArt.EyesLeftInBrand;

        mOStripEyes.enabled = lOEyes != null && liEyesX >= UWModernHudArt.LeatherLeft;

        if (mOStripEyes.enabled)
        {
            mOStripEyes.texture = lOEyes;
            mOStripEyes.color = new Color(1f, 1f, 1f, mOEyes.Alpha);
            ((RectTransform)mOStripEyes.transform).anchoredPosition = new Vector2(lfShiftX + ((liEyesX - (liTexWidth * 0.5f)) * lfScale * UWModernHudArt.PixelAspectX),
                -lfTop - ((UWModernHudArt.BrandTop + UWModernHudArt.EyesTopInBrand) * lfScale));
            ((RectTransform)mOStripEyes.transform).sizeDelta = new Vector2(lOEyes.width * lfScale * UWModernHudArt.PixelAspectX, lOEyes.height * lfScale);
        }

        // The foe at the right, once the strip is open.
        float lfFoe = Mathf.InverseLerp(0.85f, 1f, mfStripOpen) * (mOEyes != null ? mOEyes.Alpha : 0f);
        float lfFoeX = lfShiftX + (FoeFromCentre * lfScale);

        mOFoeName.enabled = lfFoe > 0f && !string.IsNullOrEmpty(lsName);
        mOFoeState.enabled = mOFoeName.enabled;

        if (mOFoeName.enabled)
        {
            mOFoeName.color = UWModernPalette.WithAlpha(UWModernPalette.Text, lfFoe);
            ((RectTransform)mOFoeName.transform).anchoredPosition = new Vector2(lfFoeX, -lfTop - (3f * lfScale));
            ((RectTransform)mOFoeName.transform).sizeDelta = new Vector2((miStripOpenHalf - FoeFromCentre) * lfScale, 9f * lfScale);

            Color lOState = msFoeColours[liGroup];

            lOState.a = lfFoe;
            mOFoeState.color = lOState;
            ((RectTransform)mOFoeState.transform).anchoredPosition = new Vector2(lfFoeX, -lfTop - (12f * lfScale));
            ((RectTransform)mOFoeState.transform).sizeDelta = new Vector2((miStripOpenHalf - FoeFromCentre) * lfScale, 8f * lfScale);
        }
    }
}
