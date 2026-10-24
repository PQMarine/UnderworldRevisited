using UnityEngine;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN HUD'S DRAWN WEAPON (out of UWModernHud, cleanup 5.16, 2026-10-22): from the same
/// frames and Weapons.dat offsets as the classic view, the original's 172 x 112 viewport mapped
/// onto the screen (WeaponScale, the HUD's Inspector value).
/// </summary>
public class UWModernWeaponView : UWModernPart
{
    /// <summary>The original viewport: its left edge in the frame (52, as UWGameUI places the
    /// weapon), its width and height.</summary>
    private const float ViewportLeft = 52f;

    private const float ViewportWidth = 172f;

    private const float ViewportHeight = 112f;

    /// <summary>How large the weapon is drawn against the screen: 1 maps the original's viewport
    /// height (112 rows) onto the screen height - UWModernHud's Inspector value, handed in each
    /// frame.</summary>
    public float WeaponScale { get; set; } = 0.8f;

    private RawImage mOWeapon;

    /// <summary>The HUD's canvas, the weapon's parent outside Classic Wide.</summary>
    private Transform mORoot;

    public UWModernWeaponView(UWModernHud pOHud) : base(pOHud)
    {
    }

    public override void Build(Transform pORoot)
    {
        mORoot = pORoot;
        mOWeapon = UWUiKit.CreateRawImage(pORoot, "Weapon", new Vector2(0f, 0f), new Vector2(0f, 1f));
    }

    /// <summary>The drawn weapon: frame, offset and raise from UWCharacter, the sway from
    /// UWGameUI, laid out as the classic view lays it out (UWGameUI.Update) with the original
    /// viewport's bottom on the screen's bottom and its centre on the screen's centre.</summary>
    protected override void fUpdate()
    {
        Texture2D lOTexture = mOCharacter.Weapon.CurrentWeaponTexture;

        if (!mOCharacter.Weapon.DrawWWeapon || lOTexture == null)
        {
            mOWeapon.enabled = false;
            return;
        }

        float lfScale = Screen.height / ViewportHeight * WeaponScale;
        float lfCentreX = Screen.width * 0.5f;
        float lfBottom = 0f;

        // CLASSIC WIDE: the weapon at the frame's pixel size, its offsets from row 131 as the
        // classic scheme's (UWGameUI: wTopOffset), the view's middle across; it lies under the
        // frame (UWModernClassicFrame.WeaponLayer), which covers what reaches below the hole (per
        // user's screenshot, 2026-10-10: it sat too high at the hole's bottom).
        UWModernClassicFrame lOFrame = UWModernClassicFrame.Instance;
        Transform lOParent = UWModernClassicFrame.IsActive && lOFrame != null && lOFrame.WeaponLayer != null
            ? lOFrame.WeaponLayer : mORoot;

        if (mOWeapon.transform.parent != lOParent)
            mOWeapon.transform.SetParent(lOParent, false);

        if (UWModernClassicFrame.IsActive)
        {
            lfScale = UWModernClassicFrame.RowScale;
            lfCentreX = UWModernClassicFrame.Hole.center.x;
            lfBottom = Screen.height - (UWModernClassicFrame.WeaponRow * UWModernClassicFrame.RowScale);
        }

        WeaponCoordinate lOOffset = mOCharacter.Weapon.GetWeaponOffset();
        float lfLeft = lfCentreX
            + ((ViewportLeft + lOOffset.X + mOUi.WeaponSway.Offset() - (ViewportLeft + (ViewportWidth * 0.5f))) * lfScale * UWModernHudArt.PixelAspectX);
        float lfTop = lfBottom + ((lOOffset.Y - (lOTexture.height * (1f - mOCharacter.Weapon.CurrentReadyWeaponTime))) * lfScale);

        mOWeapon.texture = lOTexture;
        mOWeapon.enabled = true;

        RectTransform lORect = (RectTransform)mOWeapon.transform;
        lORect.anchoredPosition = new Vector2(lfLeft, lfTop);
        lORect.sizeDelta = new Vector2(lOTexture.width * lfScale * UWModernHudArt.PixelAspectX, lOTexture.height * lfScale);
    }
}
