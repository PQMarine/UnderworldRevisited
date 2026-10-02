using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UWDataImport;
using System.Collections.Generic;

/// <summary>
/// The HUD section "the compass with its needle and the status report on a click".
/// Its own class since 2026-09-18 (stage two of the HUD rebuild); the owner hands in the game
/// data, the frame, the character, the interaction and the inventory through mOUi. UWGameUI
/// keeps the public entry points as forwards.
/// </summary>
public sealed class UWHudCompass
{
    private readonly UWGameUI mOUi;

    internal UWHudCompass(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    /// <summary>
    /// The compass: a disc with four background pictures and sixteen needles, all in
    /// COMPASS.GR (pictures 0 to 3 the background, 4 to 19 the needles).
    ///
    /// THE POSITION OF EACH NEEDLE IS MEASURED INDIVIDUALLY. They do not stand on a common
    /// centre - each picture is only a few pixels in size and sits where the tip
    /// belongs. The numbers come from the reference's scene (Underworld.tscn, there at
    /// four times the size), converted to the 320x200 of the original.
    ///
    /// Which needle applies is computed by fGetCompassStepFromView like the reference: the view direction in
    /// 256 steps, plus eight for rounding, then divided by sixteen.
    /// </summary>
    internal void Build()
    {
        if (mOUi.mOUWData == null)
            return;

        mCompassBackgrounds = new Image[CompassBackgroundCount];

        for (int liAt = 0; liAt < mCompassBackgrounds.Length; liAt++)
        {
            mCompassBackgrounds[liAt] = UWGameUI.fCreateImage("CompassBg" + liAt, mOUi.mGameFrame,
                compassLeft, -compassTop, 1f, 1f, Color.white);

            fSetCompassSprite(mCompassBackgrounds[liAt], liAt);
        }

        mCompassNeedles = new Image[CompassNeedleCount];

        for (int liAt = 0; liAt < mCompassNeedles.Length; liAt++)
        {
            mCompassNeedles[liAt] = UWGameUI.fCreateImage("CompassNeedle" + liAt, mOUi.mGameFrame,
                msCompassNeedleLeft[liAt], -msCompassNeedleTop[liAt], 1f, 1f, Color.white);

            fSetCompassSprite(mCompassNeedles[liAt], CompassBackgroundCount + liAt);
        }
    }

    /// <summary>Puts a picture from COMPASS.GR onto an Image and adjusts its size.
    /// The same path as for all other GR pictures in the frame: get pixels from the palette,
    /// flip vertically, apply as sprite.</summary>
    private void fSetCompassSprite(Image pOImage, int piIndex)
    {
        if (pOImage == null)
            return;

        UWDataImport.UWData.UWTexture lOSource = null;

        try
        {
            lOSource = mOUi.mOUWData.Textures.GetTextureByType(
                UWDataImport.UWData.UWTexture.TextureTypes.COMPASS, piIndex);
        }
        catch
        {
            lOSource = null;
        }

        if (lOSource == null || lOSource.Width <= 0 || lOSource.Height <= 0)
        {
            pOImage.enabled = false;

            return;
        }

        Texture2D lOTexture = new Texture2D(lOSource.Width, lOSource.Height, TextureFormat.RGBA32, false);
        lOTexture.name = "UWGameUI.cs:6147";
        lOTexture.filterMode = mOUi.TextureFilterMode;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(UWGameUI.fGetTextureInvert(lOSource));
        lOTexture.Apply(false, false);

        pOImage.sprite = Sprite.Create(lOTexture, new Rect(0f, 0f, lOTexture.width, lOTexture.height),
            new Vector2(0f, 1f), 1f);

        ((RectTransform)pOImage.transform).sizeDelta = new Vector2(lOTexture.width, lOTexture.height);

        pOImage.enabled = false;
    }

    /// <summary>
    /// Holds the compass fixed instead of letting it follow the view direction - on level 9, where
    /// in the original only the effects of the void throw it off (see UWVoidEffects). When
    /// freezing, the direction currently in effect stays.
    /// </summary>
    public void FreezeCompass(bool pbFrozen)
    {
        if (pbFrozen == mbCompassFrozen)
            return;

        mbCompassFrozen = pbFrozen;

        if (pbFrozen)
            miFrozenCompassStep = fGetCompassStepFromView();
    }

    /// <summary>Sets the frozen compass to one of the 16 directions.</summary>
    public void SetFrozenCompassStep(int piStep)
    {
        miFrozenCompassStep = ((piStep % 16) + 16) % 16;
    }

    private bool mbCompassFrozen;

    private int miFrozenCompassStep;

    /// <summary>The view direction as compass step 0 to 15, as in the reference.</summary>
    private int fGetCompassStepFromView()
    {
        Camera lOCamera = Camera.main;

        if (lOCamera == null)
            return 0;

        // View direction in 256 steps, plus eight for rounding to the nearest sixteenth.
        int liHeading = Mathf.RoundToInt(
            Mathf.Repeat(lOCamera.transform.eulerAngles.y, 360f) * 256f / 360f);

        return ((liHeading + 8) & 0xFF) >> 4;
    }

    /// <summary>Shows the background and the needle belonging to the view direction (or the
    /// frozen step) - all others are off. Exactly the reference's procedure.</summary>
    internal void Update()
    {
        // Do NOT check for the level loader - it is only looked up on the first opening of the map
        // (fEnsureLevelLoader), and the check for it made the compass never appear in
        // normal play (per user, 2026-09-07). For the view direction
        // it is not needed anyway.
        if (mCompassNeedles == null || mCompassBackgrounds == null)
            return;

        if (Camera.main == null)
            return;

        int liStep = mbCompassFrozen ? miFrozenCompassStep : fGetCompassStepFromView();

        for (int liAt = 0; liAt < mCompassBackgrounds.Length; liAt++)
            mCompassBackgrounds[liAt].enabled = (liStep % CompassBackgroundCount) == liAt
                && mCompassBackgrounds[liAt].sprite != null;

        for (int liAt = 0; liAt < mCompassNeedles.Length; liAt++)
            mCompassNeedles[liAt].enabled = liStep == liAt && mCompassNeedles[liAt].sprite != null;
    }

    /// <summary>
    /// Does a screen position lie on the compass? For the click that outputs the state of the
    /// character (see UWStatusReport). THE ORIGINAL'S AREA since 2026-09-26
    /// (UWClickRules.Compass, x 122 to 152, rows 136 to 151) - until then the whole disc picture
    /// (x 112 to 164, rows 131 to 157) answered.
    /// </summary>
    public bool IsScreenPositionOnCompass(Vector2 pOScreenPos)
    {
        return mOUi.IsPointerInOriginalArea(pOScreenPos, UWClickRules.Compass, out int _, out int _);
    }

    /// <summary>
    /// The four lines of the original on a click on the compass - state, level, day
    /// and time of day (see UWStatusReport). Both mouse buttons do the same, just as in the
    /// original.
    /// </summary>
    public void ReportStatus()
    {
        // The level line needs the loader node, which is only looked up when needed.
        if (mOUi.mOUWData == null || mOUi.mOInteraction == null || mOUi.mCharacter == null || !mOUi.Map.fEnsureLevelLoader())
            return;

        // First wipe, then the four lines - see Interaction.ClearMessages.
        mOUi.mOInteraction.ClearMessages();

        mOUi.mOInteraction.AddMessage(UWStatusReport.GetConditionLine(mOUi.mOUWData, mOUi.mCharacter.Hunger, mOUi.mCharacter.Fatigue));
        mOUi.mOInteraction.AddMessage(UWStatusReport.GetLevelLine(mOUi.mOUWData, mOUi.Map.mOLevelLoader.CurrentLevelIndex + 1));
        mOUi.mOInteraction.AddMessage(UWStatusReport.GetDayLine(mOUi.mOUWData, mOUi.mCharacter.ClockValue));
        mOUi.mOInteraction.AddMessage(UWStatusReport.GetTimeLine(mOUi.mOUWData, mOUi.mCharacter.ClockValue));
    }

    /// <summary>
    /// THE THREE ARROWS UNDER THE COMPASS - the easy movement (UWEasyMovement). The pictures
    /// are part of the panel art, so only the three hit areas are ours.
    ///
    /// THE ORIGINAL registers them as three rectangles on the same handler as the keys
    /// (RegisterEvent_seg010_83, 383700-383745): x 0x6B to 0x7B with -1, 0x82 to 0x92 with 0,
    /// 0x9B to 0xAA with +1, and y 0x21 to 0x2F for the outer two, 0x1F to 0x2C for the middle
    /// one, which is why it sits two pixels higher and ends three pixels higher.
    ///
    /// THE COORDINATE WAS PINNED DOWN ON 2026-09-22 while reading the area table: the record
    /// keeps x0, y0, x1, y1 and the dispatcher (seg010_308 to 365, 58768-58825) lets a click
    /// through when x0 &lt;= x &lt;= x1 and y0 &lt;= y &lt;= y1, both ends included - and the Y IS
    /// COUNTED FROM THE BOTTOM of the 200 line screen. That falls into place across five areas
    /// at once: the compass disc lands on 136 to 151 from the top, inside our disc at 131 to
    /// 157; the flasks on 120 to 156; the message scroll on 170 to 199; the column of command
    /// icons on -6 to 116; and these three arrows on 153 to 167, the middle one three pixels
    /// lower at 156 to 169. Until then the y was anchored under the compass by eye, which
    /// worked (per user) but sat up to four pixels off.
    ///
    /// ONE ROW HIGHER SINCE 2026-09-26: the original's y runs 0 to 199 (clamped at 0x2580 =
    /// 199, and the pointer key "up" puts it at 199), so the row from the top is 199 - y, not
    /// 200 - y - the arrows lie on rows 152 to 166, the middle one on 155 to 168. They are now
    /// tested through the same table as the other areas (UWClickRules.EasyArrows,
    /// UWGameUI.IsPointerInOriginalArea).
    /// </summary>
    internal bool TryGetEasyMovementCommand(Vector2 pOScreenPos, out int piCommand)
    {
        piCommand = 0;

        for (int liAt = 0; liAt < UWClickRules.EasyArrows.Length; liAt++)
        {
            if (!mOUi.IsPointerInOriginalArea(pOScreenPos, UWClickRules.EasyArrows[liAt], out int _, out int _))
                continue;

            piCommand = miArrowCommand[liAt];

            return true;
        }

        return false;
    }

    /// <summary>The command of each arrow, in the order of UWClickRules.EasyArrows (the
    /// original registers them with -1, 0 and +1).</summary>
    private static readonly int[] miArrowCommand =
    {
        UWEasyMovement.CommandTurnLeft,
        UWEasyMovement.CommandStepForward,
        UWEasyMovement.CommandTurnRight
    };

    internal Image[] mCompassBackgrounds;

    internal Image[] mCompassNeedles;

    private const int CompassBackgroundCount = 4;

    private const int CompassNeedleCount = 16;

    // Position and size of the compass disc, converted from the reference's scene.
    private const float compassLeft = 112f;
    private const float compassTop = 131f;

    // The sixteen needle positions, north first, then clockwise.
    private static readonly float[] msCompassNeedleLeft =
        { 136f, 128f, 120f, 116f, 112f, 112f, 116f, 124f, 136f, 144f, 156f, 160f, 160f, 156f, 152f, 144f };

    private static readonly float[] msCompassNeedleTop =
        { 131f, 133f, 134f, 137f, 141f, 146f, 147f, 150f, 152f, 150f, 147f, 146f, 141f, 137f, 134f, 133f };
}
