using UnityEngine;
using UWDataImport.UWData;

/// <summary>The help's MAP tab - see OnDrawKit. Moved out of UWHelpWindow (cleanup 8.6,
/// 2026-10-24); its zoom is kept in the settings (UWUserSettings.HelpMapSpan).</summary>
public sealed class UWHelpMapTab : UWHelpTab
{
    /// <summary>How many tiles the map tab shows across; the wheel changes it.</summary>
    private int miMapSpan = 20;

    private const int MapSpanStep = 4;

    private const int MinMapSpan = 8;

    private const int MaxMapSpan = 64;

    /// <summary>How often the tiles are redrawn while standing still - the automap also fills in
    /// from what is seen, not only where one walks.</summary>
    private const float MapRefreshSeconds = 0.5f;

    private Rect mOPendingMapRect;

    private Texture2D mOMapTiles;

    private Vector2 mOMapBuiltAt = new Vector2(-1f, -1f);

    private float mfMapBuiltTime = -1f;

    public override UWHelpTabId Id => UWHelpTabId.Map;

    public override string TitleId => "help.tab.map";

    public override bool Scrolls => false;

    /// <summary>Made in the window's Awake: the zoom the player left (UWUserSettings).</summary>
    public UWHelpMapTab()
    {
        miMapSpan = Mathf.Clamp(UWUserSettings.HelpMapSpan, MinMapSpan, MaxMapSpan);
    }

    /// <summary>The wheel zooms by MapSpanStep tiles, out when turned towards the player (IMGUI's
    /// delta above 0, the Input System's below), kept in the settings.</summary>
    private void fZoom(int piOut)
    {
        miMapSpan = Mathf.Clamp(miMapSpan + (piOut > 0 ? MapSpanStep : -MapSpanStep), MinMapSpan, MaxMapSpan);

        if (UWUserSettings.HelpMapSpan != miMapSpan)
        {
            UWUserSettings.HelpMapSpan = miMapSpan;
            UWUserSettings.Save();
        }
    }

    /// <summary>
    /// THE MAP TAB (feature wish of the user, 2026-09-26): a cut-out of the map around the
    /// character, in the original's look - the parchment, the discovered tiles, the notes -, kept
    /// up to date while the help is open. Only with the map carried, as the real one. Notes are
    /// only shown: they are written and erased on the real map alone (per user).
    ///
    /// ON THE KIT (cleanup 8b.3, 2026-10-24): the same lines and room as the IMGUI form (gone since
    /// 8b.4), the map in the room left below them at whole screen pixels per map pixel (the placement
    /// of its pixel pass, fPlaceMap), cut at the room's edges and at the sliding frame's; the wheel
    /// over it zooms.
    /// </summary>
    protected override void OnDrawKit(UWHelpKitFlow pOFlow)
    {
        UWGameUI lOUi = UWScene.GameUi;
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOUi == null || lOLoader == null)
        {
            pOFlow.Line(UWText.T("help.map.title"), UWHelpLookEnum.Heading);
            pOFlow.Line(UWText.T("help.map.no_level"), UWHelpLookEnum.Dim);

            return;
        }

        if (!lOUi.IsCarryingMap)
        {
            pOFlow.Line(UWText.T("help.map.title"), UWHelpLookEnum.Heading);
            pOFlow.Line(UWText.T("help.map.no_map"), UWHelpLookEnum.Dim);

            return;
        }

        pOFlow.Line(UWText.F("help.map.level",
            lOLoader.CurrentLevelIndex + 1), UWHelpLookEnum.Dim);
        pOFlow.Space(6f);

        Rect lOSpace = pOFlow.Rest();

        if (lOSpace.width < 4f || lOSpace.height < 4f)
            return;

        UWKitRows lOKit = pOFlow.Kit;
        float lfWheel = lOKit.WheelIn(lOSpace);

        if (lfWheel != 0f)
            fZoom(lfWheel < 0f ? 1 : -1);

        // The room in whole screen pixels, rounded as the IMGUI form rounds it (the flow's units are
        // the canvas', from the screen's top-left corner).
        float lfScale = mOContext.Scale;
        float lfLeft = Mathf.Round(lOSpace.x * lfScale);
        float lfTop = Mathf.Round(lOSpace.y * lfScale);

        mOPendingMapRect = new Rect(lfLeft, lfTop, Mathf.Round(lOSpace.xMax * lfScale) - lfLeft, Mathf.Round(lOSpace.yMax * lfScale) - lfTop);

        UWAutomapPictures lOMap = lOUi.AutomapPictures;

        if (!fPlaceMap(lOMap, out Rect lOClip, out int liScale, out float lfOriginX, out float lfOriginY))
            return;

        Rect lOClipUnits = fUnits(lOClip, lfScale);

        lOKit.BeginClipArea(lOClipUnits);
        lOKit.Fill(lOClipUnits, UWGuiTheme.Background);

        Texture2D lOParchment = lOMap.GetParchment();

        if (lOParchment != null)
            lOKit.Picture(fUnits(new Rect(lOClip.x + lfOriginX, lOClip.y + lfOriginY, lOParchment.width * liScale,
                lOParchment.height * liScale), lfScale), lOParchment);

        if (mOMapTiles != null)
        {
            Vector2 lOTiles = UWAutomapPictures.TilesTopLeft;
            float lfSize = UWAutomapPictures.TilesSize * liScale;

            lOKit.Picture(fUnits(new Rect(lOClip.x + lfOriginX + (lOTiles.x * liScale), lOClip.y + lfOriginY + (lOTiles.y * liScale),
                lfSize, lfSize), lfScale), mOMapTiles);
        }

        foreach (UWAutomapPictures.Note lONote in lOMap.GetNotes())
        {
            lOKit.Picture(fUnits(new Rect(lOClip.x + lfOriginX + (lONote.TopLeft.x * liScale), lOClip.y + lfOriginY + (lONote.TopLeft.y * liScale),
                lONote.Texture.width * liScale, lONote.Texture.height * liScale), lfScale), lONote.Texture);
        }

        lOKit.EndClipArea();
    }

    /// <summary>A rectangle of screen pixels in the kit's units.</summary>
    private static Rect fUnits(Rect pOPixels, float pfScale)
    {
        return new Rect(pOPixels.x / pfScale, pOPixels.y / pfScale, pOPixels.width / pfScale, pOPixels.height / pfScale);
    }

    /// <summary>
    /// Where the map goes in its room (mOPendingMapRect, screen pixels; cleanup 8b.3, moved out of the
    /// IMGUI form's pixel pass with its numbers): the room cut at the panel's left edge (pOClip), the
    /// whole pixels per map pixel (piScale) and the parchment's corner inside the clip, centred on the
    /// character. False while there is nothing to draw. The tiles are built anew when the character
    /// moved or MapRefreshSeconds passed.
    /// </summary>
    private bool fPlaceMap(UWAutomapPictures pOMap, out Rect pOClip, out int piScale, out float pfOriginX, out float pfOriginY)
    {
        pOClip = Rect.zero;
        piScale = 1;
        pfOriginX = 0f;
        pfOriginY = 0f;

        if (!pOMap.TryGetPlayerPixel(out Vector2 lOPlayer))
            return false;

        if (mOMapTiles == null || lOPlayer != mOMapBuiltAt || Time.unscaledTime - mfMapBuiltTime >= MapRefreshSeconds)
        {
            mOMapTiles = pOMap.BuildTiles();
            mOMapBuiltAt = lOPlayer;
            mfMapBuiltTime = Time.unscaledTime;
        }

        float lfLeft = Mathf.Max(mOPendingMapRect.x, mOContext.ClipPixels);

        pOClip = new Rect(lfLeft, mOPendingMapRect.y, mOPendingMapRect.xMax - lfLeft, mOPendingMapRect.height);

        if (pOClip.width <= 0f || pOClip.height <= 0f)
            return false;

        piScale = Mathf.Max(1, Mathf.RoundToInt(mOPendingMapRect.width / (miMapSpan * UWAutomapPictures.TilePixels)));
        pfOriginX = Mathf.Round(mOPendingMapRect.center.x - lfLeft - (lOPlayer.x * piScale));
        pfOriginY = Mathf.Round((mOPendingMapRect.height / 2f) - (lOPlayer.y * piScale));

        return true;
    }
}
