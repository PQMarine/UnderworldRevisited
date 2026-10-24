using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN HUD'S FLASKS AND POWER GEM (out of UWModernHud, cleanup 5.16, 2026-10-22): bottom
/// left the two flasks with their values and the power gem between them, standing free on the
/// original's stone shelf (UWModernHudArt.BuildShelf, look per user 2026-10-03); the gem on a
/// stand of its own and the flasks freed one by one where the layout says so (Classic+).
///
/// Built in two steps (Build, BuildFlasks): the rune hollow's images lie between the shelf and the
/// flasks on the HUD's canvas, as they were created in one go before.
/// </summary>
public class UWModernVitals : UWModernPart
{
    /// <summary>Original pixels between the shelf and the screen's edges.</summary>
    public const int ShelfMargin = 4;

    private RawImage mOHealthFlask;

    private RawImage mOManaFlask;

    private RawImage mOPowerGem;

    /// <summary>The stone shelf under the flasks and the gem (UWModernHudArt).</summary>
    private RawImage mOShelf;

    /// <summary>The shelf, built for the colours and whether the gem stands apart, with the gem's
    /// stand and the masks (fEnsureArt) - its stamp says whether they are current.</summary>
    private readonly UWVersionedTexture mOShelfArt = new UWVersionedTexture();

    private UWModernHudArt.ShelfLayout mOShelfLayout;

    /// <summary>Which pixels of the flask and gem pictures to keep - their black backdrop goes.</summary>
    private bool[] mbFlaskMask;

    private bool[] mbGemMask;

    /// <summary>The flasks standing free (UWModernLayout.IsFlasksApart): which pixels to keep, and
    /// the keyed copies.</summary>
    private bool[] mbFreeFlaskMask;

    private readonly System.Collections.Generic.Dictionary<Texture2D, Texture2D> mOKeyedFree
        = new System.Collections.Generic.Dictionary<Texture2D, Texture2D>();

    /// <summary>The power gem apart (UWModernLayout.IsGemApart): its stand, and where the gem lies on it.</summary>
    private RawImage mOGemStand;

    private Texture2D mOGemStandTexture;

    private Vector2Int mOGemOnStand;

    private bool mbGemApart;

    /// <summary>The keyed copies of the flask and gem pictures, by the picture UWCharacter
    /// shows (it caches those itself, so this stays small).</summary>
    private readonly System.Collections.Generic.Dictionary<Texture2D, Texture2D> mOKeyed
        = new System.Collections.Generic.Dictionary<Texture2D, Texture2D>();

    private Text mOHealthText;

    private Text mOManaText;

    public UWModernVitals(UWModernHud pOHud) : base(pOHud)
    {
    }

    /// <summary>The shelf and the gem's stand - under the rune hollow (UWModernHud.fBuild).</summary>
    public override void Build(Transform pORoot)
    {
        mOShelf = UWUiKit.CreateRawImage(pORoot, "Shelf", new Vector2(0f, 0f), new Vector2(0f, 0f));
        mOGemStand = UWUiKit.CreateRawImage(pORoot, "Power gem stand", new Vector2(0f, 0f), new Vector2(0f, 0f));
        UWPixelArtUI.Apply(mOGemStand);
    }

    /// <summary>The flasks, the gem and the values - over the rune hollow (UWModernHud.fBuild).</summary>
    public void BuildFlasks(Transform pORoot)
    {
        mOHealthFlask = UWUiKit.CreateRawImage(pORoot, "Vitality", new Vector2(0f, 0f), new Vector2(0f, 0f));
        mOPowerGem = UWUiKit.CreateRawImage(pORoot, "Power gem", new Vector2(0f, 0f), new Vector2(0f, 0f));
        mOManaFlask = UWUiKit.CreateRawImage(pORoot, "Mana", new Vector2(0f, 0f), new Vector2(0f, 0f));

        // The free scale: hard texels at any size (UWPixelArtUI).
        UWPixelArtUI.Apply(mOShelf);
        UWPixelArtUI.Apply(mOHealthFlask);
        UWPixelArtUI.Apply(mOPowerGem);
        UWPixelArtUI.Apply(mOManaFlask);
        mOHealthText = UWUiKit.CreateText(pORoot, "Vitality value", new Vector2(0f, 0f), new Vector2(0.5f, 0f), TextAnchor.LowerCenter, UWModernPalette.HudText, mOUi, UWUiKit.HudText);
        mOManaText = UWUiKit.CreateText(pORoot, "Mana value", new Vector2(0f, 0f), new Vector2(0.5f, 0f), TextAnchor.LowerCenter, UWModernPalette.HudText, mOUi, UWUiKit.HudText);
    }

    public override void OnPixelScale(float pfPixelScale)
    {
        base.OnPixelScale(pfPixelScale);

        int liFontSize = FontSize;

        mOHealthText.fontSize = liFontSize;
        mOManaText.fontSize = liFontSize;
    }

    /// <summary>The HUD goes: the shelf's art and the keyed copies with it.</summary>
    public void Release()
    {
        mOShelfArt.Release();

        foreach (Texture2D lOOld in mOKeyed.Values)
            Object.Destroy(lOOld);

        foreach (Texture2D lOOld in mOKeyedFree.Values)
            Object.Destroy(lOOld);
    }

    /// <summary>The flasks with their values and the power gem between them, bottom left on
    /// the stone shelf.</summary>
    protected override void fUpdate()
    {
        fEnsureArt();

        // Its own size and place (UWModernLayout): the shelf with the values above it.
        float liScale = fScaleOf(UWModernLayout.ElementEnum.Vitals);
        float lfMargin = ShelfMargin * liScale;
        UWModernHudArt.ShelfLayout lOLayout = mOShelfLayout;
        int liTextSize = fFontOf(UWModernLayout.ElementEnum.Vitals);
        float lfTextHeight = liTextSize * 1.5f;
        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.Vitals, new Rect(lfMargin, lfMargin, lOLayout.Width * liScale * UWModernHudArt.PixelAspectX,
            ((lOLayout.Height + 1) * liScale) + lfTextHeight));
        Vector2 lOOrigin = lOPlaced.position;
        bool lbFlasksApart = UWModernLayout.IsFlasksApart;

        // The flasks apart: no shelf, and the vitals' place only gives the flasks' default places.
        if (!lbFlasksApart)
            UWModernLayout.Report(UWModernLayout.ElementEnum.Vitals, lOPlaced);

        mOShelf.texture = mOShelfArt.Texture;
        mOShelf.enabled = mOShelfArt.Texture != null && !lbFlasksApart;

        RectTransform lOShelfRect = (RectTransform)mOShelf.transform;
        lOShelfRect.anchoredPosition = lOOrigin;
        lOShelfRect.sizeDelta = new Vector2(lOLayout.Width * liScale * UWModernHudArt.PixelAspectX, lOLayout.Height * liScale);

        if (lbFlasksApart)
        {
            fUpdateFlaskApart(UWModernLayout.ElementEnum.HealthFlask, mOHealthFlask, mOCharacter.Flasks.HealthFlaskTeture, mOHealthText,
                Mathf.RoundToInt(mOCharacter.Vitals.CurrentHP) + "/" + Mathf.RoundToInt(mOCharacter.Vitals.MaxHP),
                new Vector2(lOOrigin.x + (lOLayout.HealthFlask.x * liScale * UWModernHudArt.PixelAspectX), lOOrigin.y));
            fUpdateFlaskApart(UWModernLayout.ElementEnum.ManaFlask, mOManaFlask, mOCharacter.Flasks.ManaFlaskTeture, mOManaText,
                Mathf.RoundToInt(mOCharacter.Vitals.CurrentMana) + "/" + Mathf.RoundToInt(mOCharacter.Vitals.MaxMana),
                new Vector2(lOOrigin.x + (lOLayout.ManaFlask.x * liScale * UWModernHudArt.PixelAspectX), lOOrigin.y));
        }
        else
        {
            fPlaceOnShelf(mOHealthFlask, fKeyed(mOCharacter.Flasks.HealthFlaskTeture, mbFlaskMask), lOLayout.HealthFlask, lOOrigin, liScale);
            fPlaceOnShelf(mOManaFlask, fKeyed(mOCharacter.Flasks.ManaFlaskTeture, mbFlaskMask), lOLayout.ManaFlask, lOOrigin, liScale);
        }

        if (mbGemApart)
            fUpdateGemStand(lOPlaced);
        else
        {
            mOGemStand.enabled = false;
            fPlaceOnShelf(mOPowerGem, fKeyed(mOCharacter.Weapon.PowerGemTeture, mbGemMask), lOLayout.Gem, lOOrigin, liScale);
        }

        if (lbFlasksApart)
            return;

        mOHealthText.enabled = true;
        mOManaText.enabled = true;
        mOHealthText.fontSize = liTextSize;
        mOManaText.fontSize = liTextSize;

        float lfTextY = lOOrigin.y + ((lOLayout.Height + 1) * liScale);
        float lfHealthCentre = lOOrigin.x + ((lOLayout.HealthFlask.x + (fWidth(mOCharacter.Flasks.HealthFlaskTeture) * 0.5f)) * liScale * UWModernHudArt.PixelAspectX);
        float lfManaCentre = lOOrigin.x + ((lOLayout.ManaFlask.x + (fWidth(mOCharacter.Flasks.ManaFlaskTeture) * 0.5f)) * liScale * UWModernHudArt.PixelAspectX);

        mOHealthText.text = Mathf.RoundToInt(mOCharacter.Vitals.CurrentHP) + "/" + Mathf.RoundToInt(mOCharacter.Vitals.MaxHP);
        mOManaText.text = Mathf.RoundToInt(mOCharacter.Vitals.CurrentMana) + "/" + Mathf.RoundToInt(mOCharacter.Vitals.MaxMana);

        ((RectTransform)mOHealthText.transform).anchoredPosition = new Vector2(lfHealthCentre, lfTextY);
        ((RectTransform)mOHealthText.transform).sizeDelta = new Vector2(120f, mOHealthText.fontSize * 1.5f);
        ((RectTransform)mOManaText.transform).anchoredPosition = new Vector2(lfManaCentre, lfTextY);
        ((RectTransform)mOManaText.transform).sizeDelta = new Vector2(120f, mOManaText.fontSize * 1.5f);
    }

    /// <summary>
    /// A FLASK APART (Classic+, per user 2026-10-10: "the flasks freed one by one"): an element of
    /// its own with the flask standing free on its gold foot (UWHudArt.BuildFreeFlaskMask) and its
    /// value above it, as on the shelf; by default where the shelf would hold it (pODefault, its
    /// bottom left).
    /// </summary>
    private void fUpdateFlaskApart(UWModernLayout.ElementEnum peElement, RawImage pOImage, Texture2D pOSource, Text pOText,
        string psValue, Vector2 pODefault)
    {
        Texture2D lOFlask = fKeyed(pOSource, mbFreeFlaskMask, mOKeyedFree);

        if (lOFlask == null)
        {
            pOImage.enabled = false;
            pOText.enabled = false;
            return;
        }

        float lfScale = fScaleOf(peElement);
        int liTextSize = fFontOf(peElement);
        float lfFlaskWidth = lOFlask.width * lfScale * UWModernHudArt.PixelAspectX;
        float lfFlaskHeight = lOFlask.height * lfScale;
        Rect lOPlaced = fPlaceAndReport(peElement,
            new Rect(pODefault.x, pODefault.y, lfFlaskWidth, lfFlaskHeight + lfScale + (liTextSize * 1.5f)));

        pOImage.texture = lOFlask;
        pOImage.enabled = true;

        RectTransform lORect = (RectTransform)pOImage.transform;
        lORect.anchoredPosition = lOPlaced.position;
        lORect.sizeDelta = new Vector2(lfFlaskWidth, lfFlaskHeight);

        // No values in Classic Wide: the original shows none (the stats page has them).
        pOText.enabled = !UWModernClassicFrame.IsActive;
        pOText.fontSize = liTextSize;
        pOText.text = psValue;
        ((RectTransform)pOText.transform).anchoredPosition = new Vector2(lOPlaced.center.x, lOPlaced.y + lfFlaskHeight + lfScale);
        ((RectTransform)pOText.transform).sizeDelta = new Vector2(120f, liTextSize * 1.5f);
    }

    /// <summary>
    /// THE POWER GEM APART (Classic+, per user, 2026-10-09: "in the original the gem stands alone" -
    /// we had put it between the flasks): its own element (UWModernLayout.ElementEnum.PowerGem) on
    /// the original's stone stand, by default right of the shelf, the gem picture (inactive,
    /// charging, full) laid on the stand as on the shelf.
    /// </summary>
    private void fUpdateGemStand(Rect pOVitals)
    {
        float liScale = fScaleOf(UWModernLayout.ElementEnum.PowerGem);
        float lfWidth = mOGemStandTexture.width * liScale * UWModernHudArt.PixelAspectX;
        float lfHeight = mOGemStandTexture.height * liScale;
        Rect lOPlaced = fPlaceAndReport(UWModernLayout.ElementEnum.PowerGem,
            new Rect(pOVitals.xMax + (ShelfMargin * miPixelScale), pOVitals.y, lfWidth, lfHeight));

        mOGemStand.texture = mOGemStandTexture;
        mOGemStand.enabled = true;
        ((RectTransform)mOGemStand.transform).anchoredPosition = lOPlaced.position;
        ((RectTransform)mOGemStand.transform).sizeDelta = lOPlaced.size;

        Texture2D lOGem = fKeyed(mOCharacter.Weapon.PowerGemTeture, mbGemMask);

        if (lOGem == null)
        {
            mOPowerGem.enabled = false;
            return;
        }

        mOPowerGem.texture = lOGem;
        mOPowerGem.enabled = true;

        RectTransform lORect = (RectTransform)mOPowerGem.transform;
        lORect.anchoredPosition = new Vector2(lOPlaced.x + (mOGemOnStand.x * liScale * UWModernHudArt.PixelAspectX),
            lOPlaced.y + ((mOGemStandTexture.height - mOGemOnStand.y - lOGem.height) * liScale));
        lORect.sizeDelta = new Vector2(lOGem.width * liScale * UWModernHudArt.PixelAspectX, lOGem.height * liScale);
    }

    /// <summary>The shelf and the masks, built once and again when the colour help changes or the
    /// gem goes apart or back.</summary>
    private void fEnsureArt()
    {
        bool lbGemApart = UWModernLayout.IsGemApart;
        UWArtStamp lOStamp = UWVersionedTexture.Colours(lbGemApart ? 1 : 0);

        if (mOShelfArt.IsCurrent(lOStamp))
            return;

        mbGemApart = lbGemApart;

        if (mOGemStandTexture != null)
            Object.Destroy(mOGemStandTexture);

        foreach (Texture2D lOOld in mOKeyed.Values)
            Object.Destroy(lOOld);

        mOKeyed.Clear();

        foreach (Texture2D lOOld in mOKeyedFree.Values)
            Object.Destroy(lOOld);

        mOKeyedFree.Clear();

        DataImport lOData = mOUi.mOUWData;

        mOShelfArt.Set(lOStamp, UWModernHudArt.BuildShelf(lOData.Textures, mOUi.TextureFilterMode, out mOShelfLayout, mbGemApart));
        mOGemStandTexture = UWModernHudArt.BuildGemStand(lOData.Textures, mOUi.TextureFilterMode, out mOGemOnStand);
        mbFlaskMask = UWModernHudArt.BuildFlaskMask(lOData.Flasks);
        mbFreeFlaskMask = UWModernHudArt.BuildFreeFlaskMask(lOData.Flasks);
        mbGemMask = UWModernHudArt.BuildGemMask(lOData.PowerGem);
    }

    /// <summary>A flask or gem picture with its backdrop keyed out, kept per picture (in
    /// pOCache, mOKeyed unless given - the free flasks keep theirs apart).</summary>
    private Texture2D fKeyed(Texture2D pOSource, bool[] pbMask,
        System.Collections.Generic.Dictionary<Texture2D, Texture2D> pOCache = null)
    {
        if (pOSource == null)
            return null;

        System.Collections.Generic.Dictionary<Texture2D, Texture2D> lOCache = pOCache ?? mOKeyed;
        Texture2D lOKeyed;

        if (lOCache.TryGetValue(pOSource, out lOKeyed) && lOKeyed != null)
            return lOKeyed;

        // UWCharacter rebuilds its pictures when the colour help changes, so old keys die off;
        // a bound keeps them from piling up in between.
        if (lOCache.Count > 256)
        {
            foreach (Texture2D lOOld in lOCache.Values)
                Object.Destroy(lOOld);

            lOCache.Clear();
        }

        lOKeyed = UWTextureFactory.ApplyMask(pOSource, pbMask);
        lOCache[pOSource] = lOKeyed;

        return lOKeyed;
    }

    /// <summary>Lays a picture onto the shelf, its top-left corner at a shelf pixel (rows from
    /// the top).</summary>
    private void fPlaceOnShelf(RawImage pOImage, Texture2D pOTexture, Vector2Int pOAt, Vector2 pOOrigin, float liScale)
    {
        if (pOTexture == null)
        {
            pOImage.enabled = false;
            return;
        }

        pOImage.texture = pOTexture;
        pOImage.enabled = true;

        RectTransform lORect = (RectTransform)pOImage.transform;
        lORect.anchoredPosition = new Vector2(pOOrigin.x + (pOAt.x * liScale * UWModernHudArt.PixelAspectX),
            pOOrigin.y + ((mOShelfLayout.Height - pOAt.y - pOTexture.height) * liScale));
        lORect.sizeDelta = new Vector2(pOTexture.width * liScale * UWModernHudArt.PixelAspectX, pOTexture.height * liScale);
    }

    private static float fWidth(Texture2D pOTexture) => pOTexture != null ? pOTexture.width : 0f;
}
