using UnityEngine;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN HUD'S DRAWN WEAPON (out of UWModernHud, cleanup 5.16, 2026-10-22): from the same
/// frames and Weapons.dat offsets as the classic view, the original's 172 x 112 viewport mapped
/// onto the screen (WeaponScale, the HUD's Inspector value). A frame a mod extends
/// (UWWeaponView.CurrentWeaponExtended, Docs/WIDESCREEN-WEAPONS-PLAN.md step 3) is drawn instead, placed
/// so that the original's part lies exactly where the original would.
/// </summary>
public class UWModernWeaponView : UWModernPart
{
    /// <summary>How large the weapon is drawn against the screen: 1 maps the game's view height (the first game's
    /// 112 rows, the second's 128; UWModernWeaponFrame.ViewHeight) onto the screen height - UWModernHud's
    /// Inspector value, handed in each frame.</summary>
    public float WeaponScale { get; set; } = 0.8f;

    private RawImage mOWeapon;

    /// <summary>The HUD's canvas, the weapon's parent outside Classic Wide.</summary>
    private Transform mORoot;

    /// <summary>The game's view on the screen for a weapon that is cut at its edges (UWModernWeaponFrame.ClipToView,
    /// stage U4 part A step 3: the second game's pictures reach beyond the view). Unused on the first game.</summary>
    private RectTransform mOClip;

    public UWModernWeaponView(UWModernHud pOHud) : base(pOHud)
    {
    }

    public override void Build(Transform pORoot)
    {
        mORoot = pORoot;
        mOClip = UWUiKit.CreateClipArea(pORoot, "Weapon view");
        mOWeapon = UWUiKit.CreateRawImage(pORoot, "Weapon", new Vector2(0f, 0f), new Vector2(0f, 1f));

        // The textures' look (UWTextureLook): smooth or sharp bilinear in Remastered.
        UWWeaponFilterUI.Apply(mOWeapon);
    }

    /// <summary>The drawn weapon: frame, offset, raise and sway from the session's art
    /// (IUWModernArt.TryGetWeapon), laid out as the classic view lays it out (UWGameUI.Update) with the original
    /// viewport's bottom on the screen's bottom and its centre on the screen's centre.</summary>
    protected override void fUpdate()
    {
        if (!mOSession.Art.TryGetWeapon(out UWModernWeaponFrame lOFrameNow))
        {
            mOWeapon.enabled = false;
            return;
        }

        Texture2D lOTexture = lOFrameNow.Texture;

        float lfScale = Screen.height / lOFrameNow.ViewHeight * WeaponScale;
        float lfCentreX = Screen.width * 0.5f;
        float lfBottom = 0f;

        // CLASSIC WIDE: the weapon at the frame's pixel size, its offsets from row 131 as the
        // classic scheme's (UWGameUI: wTopOffset), the view's middle across; it lies under the
        // frame (UWModernClassicFrame.WeaponLayer), which covers what reaches below the hole (per
        // user's screenshot, 2026-10-10: it sat too high at the hole's bottom).
        // The second game's pictures reach beyond its view (ClipToView): cut there in Classic Wide too, the view's width
        // round the hole's middle (UW2 plan stage U7 step 2) - the widened hole would show what the original cuts.
        UWModernClassicFrame lOFrame = UWModernClassicFrame.Instance;
        bool lbClip = lOFrameNow.ClipToView;
        Transform lOParent = lbClip ? mOClip
            : UWModernClassicFrame.IsActive && lOFrame != null && lOFrame.WeaponLayer != null ? lOFrame.WeaponLayer : mORoot;

        if (mOWeapon.transform.parent != lOParent)
            mOWeapon.transform.SetParent(lOParent, false);

        if (UWModernClassicFrame.IsActive)
        {
            lfScale = UWModernClassicFrame.RowScale;
            lfCentreX = UWModernClassicFrame.Hole.center.x;
            lfBottom = UWModernClassicFrame.TopY - (UWModernClassicFrame.WeaponRow * UWModernClassicFrame.RowScale);
        }

        float lfLeft = lfCentreX
            + ((lOFrameNow.OffsetX + lOFrameNow.Sway - (lOFrameNow.ViewWidth * 0.5f)) * lfScale * UWModernHudArt.PixelAspectX);

        // Cut at the view's edges: the view's rect on the screen, its bottom on the screen's and its middle on the
        // screen's, and the weapon placed inside it.
        if (lbClip)
        {
            float lfViewWidth = lOFrameNow.ViewWidth * lfScale * UWModernHudArt.PixelAspectX;
            float lfViewLeft = lfCentreX - (lfViewWidth * 0.5f);

            UWUiKit.SetRect(mOClip, lfViewLeft, lfBottom, lfViewWidth, lOFrameNow.ViewHeight * lfScale);
            lfLeft -= lfViewLeft;
        }
        // Sizes in the original's pixels, not the texture's (larger with the sprites' xBR, UWSpriteXbr.WeaponPasses).
        Vector2 lOSize = lOFrameNow.Size;
        // In the clip area its bottom is the view's (the second game's weapon in Classic Wide, where the view's bottom is
        // not the screen's: it stood a view's bottom too high, per user's screenshot, UW2 plan stage U7 step 2a).
        float lfTop = (lbClip ? 0f : lfBottom) + ((lOFrameNow.OffsetY - (lOSize.y * (1f - lOFrameNow.Ready))) * lfScale);

        // A mod's larger picture: moved left and up by where the original lies in it; the raise above
        // keeps the original's height, so the original's part moves as the original does.
        Texture2D lOExtended = lOFrameNow.Extended;

        if (lOExtended != null)
        {
            lfLeft -= lOFrameNow.ExtendedLeft * lfScale * UWModernHudArt.PixelAspectX;
            lfTop += lOFrameNow.ExtendedTop * lfScale;
            lOTexture = lOExtended;
            lOSize = lOFrameNow.ExtendedSize;
        }

        mOWeapon.texture = lOTexture;
        mOWeapon.enabled = true;

        RectTransform lORect = (RectTransform)mOWeapon.transform;
        lORect.anchoredPosition = new Vector2(lfLeft, lfTop);
        lORect.sizeDelta = new Vector2(lOSize.x * lfScale * UWModernHudArt.PixelAspectX, lOSize.y * lfScale);
    }
}
