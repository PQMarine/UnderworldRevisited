using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;
using UnderworldRevisited;

/// <summary>
/// The screen behind VERY HIGH in the detail menu: every Remastered effect on its own (per user,
/// 2026-09-17), drawn in the style of the original's options panel rather than as a Unity
/// window - the colours of the marbled stone of OPTBTNS.GR, lettering in the built-in font of the menu bar, in the colour of
/// the original's button lettering (0x62, OPTBTNS 38) with a black shadow, and the
/// original ON, OFF and DONE buttons.
///
/// It is one 320x200 picture over the whole frame, in palette indices like every other
/// interface picture (UWIconPalette). Changes apply at once: the values go to
/// UWGraphicsDetail.CustomValues and from there into the settings, which the Remastered
/// renderer reads every frame. DONE or Escape closes it and saves.
/// </summary>
public class UWEffectsScreen
{
    private const int ScreenWidth = 320;

    private const int ScreenHeight = 200;

    // Colours of the button lettering (OPTBTNS 38).
    private const byte TextColour = 0x62;

    private const byte ShadowColour = 0x01;

    private const int TitleTop = 5;

    private const int FirstRowTop = 17;

    private const int RowPitch = 8;

    private const int LabelLeft = 12;

    private const int ControlLeft = 176;

    private const int BarWidth = 90;

    private const int BarHeight = 5;

    private const int ValueLeft = ControlLeft + BarWidth + 6;

    private const int ButtonTop = 181;

    private const int AllLabelLeft = 12;

    private const int OnButtonLeft = 52;

    private const int OffButtonLeft = 87;

    private const int DoneButtonLeft = 277;

    // OPTBTNS.GR
    private const int OnButtonImage = 20;

    private const int OffButtonImage = 22;

    private const int DoneButtonImage = 26;

    private const int QuitBackgroundImage = 3;

    /// <summary>The empty marble below the NO button of the quit panel - the colours the
    /// background noise is drawn from (see fDrawBackground).</summary>
    private const int MarbleLeft = 3;

    private const int MarbleTop = 64;

    private const int MarbleWidth = 29;

    private const int MarbleHeight = 38;

    private enum KindEnum
    {
        Toggle,
        Slider,
        Steps,
        Tonemapping
    }

    private class Row
    {
        public string Label;
        public KindEnum Kind;
        public float Min;
        public float Max;
        public Func<UWGraphicsDetail.EffectValues, float> Get;
        public Action<UWGraphicsDetail.EffectValues, float> Set;
    }

    private readonly List<Row> mORows = new List<Row>();

    private readonly DataImport mOData;

    private readonly RectTransform mOFrame;

    private RawImage mOImage;

    private Texture2D mOTexture;

    private readonly byte[] myPixels = new byte[ScreenWidth * ScreenHeight];

    private bool mbDirty = true;

    private int miDragRow = -1;

    private int miHoveredButton = -1;

    public bool IsOpen { get; private set; }

    public UWEffectsScreen(DataImport pOData, RectTransform pOFrame)
    {
        mOData = pOData;
        mOFrame = pOFrame;

        fAddToggle("Torch and fire lights", v => v.TorchLights, (v, b) => v.TorchLights = b);
        fAddToggle("Torch shadows", v => v.TorchShadows, (v, b) => v.TorchShadows = b);
        fAddRow("Shadows at once", KindEnum.Steps, 0f, 8f, v => v.MaxShadowLights, (v, f) => v.MaxShadowLights = Mathf.RoundToInt(f));
        fAddToggle("Lava lights", v => v.LavaLights, (v, b) => v.LavaLights = b);
        fAddRow("Light brightness", KindEnum.Slider, 0f, 3f, v => v.LightBrightness, (v, f) => v.LightBrightness = f);
        fAddRow("Relief", KindEnum.Slider, 0f, 4f, v => v.NormalStrength, (v, f) => v.NormalStrength = f);
        fAddRow("Shine", KindEnum.Slider, 0f, 2f, v => v.SpecularStrength, (v, f) => v.SpecularStrength = f);
        fAddRow("Wall depth", KindEnum.Slider, 0f, 0.2f, v => v.ParallaxDepth, (v, f) => v.ParallaxDepth = f);
        fAddRow("Relief shadows", KindEnum.Slider, 0f, 1f, v => v.SelfShadow, (v, f) => v.SelfShadow = f);
        fAddRow("Dark joints", KindEnum.Slider, 0f, 1f, v => v.Cavity, (v, f) => v.Cavity = f);
        fAddRow("Lava glow", KindEnum.Slider, 0f, 4f, v => v.EmissiveStrength, (v, f) => v.EmissiveStrength = f);
        fAddRow("Bloom", KindEnum.Slider, 0f, 3f, v => v.BloomIntensity, (v, f) => v.BloomIntensity = f);
        fAddToggle("Ambient occlusion", v => v.AmbientOcclusion, (v, b) => v.AmbientOcclusion = b);
        fAddRow("Tonemapping", KindEnum.Tonemapping, 0f, 2f, v => (float)v.Tonemapping,
            (v, f) => v.Tonemapping = (UWSettings.RemasterTonemappingEnum)Mathf.RoundToInt(f));
        fAddRow("Sprite glow", KindEnum.Slider, 0f, 3f, v => v.SpriteGlow, (v, f) => v.SpriteGlow = f);
        fAddRow("Fire flicker", KindEnum.Slider, 0f, 0.8f, v => v.FireFlicker, (v, f) => v.FireFlicker = f);
        fAddRow("Sparks", KindEnum.Slider, 0f, 3f, v => v.FireSparks, (v, f) => v.FireSparks = f);
        fAddRow("Smoke", KindEnum.Slider, 0f, 1f, v => v.FireSmoke, (v, f) => v.FireSmoke = f);
        fAddRow("Dust", KindEnum.Slider, 0f, 3f, v => v.Dust, (v, f) => v.Dust = f);
        fAddRow("Ground shadows", KindEnum.Slider, 0f, 1f, v => v.GroundShadow, (v, f) => v.GroundShadow = f);
    }

    private void fAddRow(string psLabel, KindEnum peKind, float pfMin, float pfMax,
        Func<UWGraphicsDetail.EffectValues, float> pOGet, Action<UWGraphicsDetail.EffectValues, float> pOSet)
    {
        mORows.Add(new Row { Label = psLabel, Kind = peKind, Min = pfMin, Max = pfMax, Get = pOGet, Set = pOSet });
    }

    private void fAddToggle(string psLabel, Func<UWGraphicsDetail.EffectValues, bool> pOGet,
        Action<UWGraphicsDetail.EffectValues, bool> pOSet)
    {
        fAddRow(psLabel, KindEnum.Toggle, 0f, 1f, v => pOGet(v) ? 1f : 0f, (v, f) => pOSet(v, f >= 0.5f));
    }

    public void Open()
    {
        if (mOFrame == null || mOData == null)
            return;

        if (mOImage == null)
        {
            GameObject lOObject = new GameObject("EffectsScreen", typeof(RectTransform), typeof(RawImage));
            lOObject.transform.SetParent(mOFrame, false);

            RectTransform lORect = (RectTransform)lOObject.transform;
            lORect.anchorMin = new Vector2(0f, 1f);
            lORect.anchorMax = new Vector2(0f, 1f);
            lORect.pivot = new Vector2(0f, 1f);
            lORect.anchoredPosition = Vector2.zero;
            lORect.sizeDelta = new Vector2(ScreenWidth, ScreenHeight);

            mOImage = lOObject.GetComponent<RawImage>();
            mOImage.raycastTarget = false;

            mOTexture = new Texture2D(ScreenWidth, ScreenHeight, TextureFormat.RGBA32, false, true);
            mOTexture.name = "UWEffectsScreen";
            mOTexture.filterMode = FilterMode.Point;

            mOImage.texture = mOTexture;
            UWIconPalette.Apply(mOImage);
        }

        mOImage.transform.SetAsLastSibling();
        mOImage.gameObject.SetActive(true);

        IsOpen = true;
        miDragRow = -1;
        mbDirty = true;

        fRedraw();
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        miDragRow = -1;

        if (mOImage != null)
            mOImage.gameObject.SetActive(false);

        UWGraphicsDetail.Save();
    }

    /// <summary>Mouse and keys. Call every frame while open.</summary>
    public void Update()
    {
        if (!IsOpen)
            return;

        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard != null && lOKeyboard.escapeKey.wasPressedThisFrame)
        {
            Close();

            return;
        }

        Mouse lOMouse = Mouse.current;

        if (lOMouse == null)
            return;

        int liX;
        int liY;

        if (!fToScreenPixel(lOMouse.position.ReadValue(), out liX, out liY))
        {
            liX = -1;
            liY = -1;
        }

        UWGraphicsDetail.EffectValues lOValues = UWGraphicsDetail.CustomValues;

        if (lOValues == null)
            return;

        int liButton = fButtonAt(liX, liY);

        if (liButton != miHoveredButton)
        {
            miHoveredButton = liButton;
            mbDirty = true;
        }

        if (!lOMouse.leftButton.isPressed)
            miDragRow = -1;

        if (miDragRow >= 0)
        {
            fSetFromBar(mORows[miDragRow], lOValues, liX);
        }
        else if (lOMouse.leftButton.wasPressedThisFrame)
        {
            if (liButton == DoneButtonImage)
            {
                Close();

                return;
            }

            if (liButton == OnButtonImage || liButton == OffButtonImage)
            {
                UWGraphicsDetail.EffectValues lOPreset = liButton == OnButtonImage
                    ? UWGraphicsDetail.ShippedValues
                    : UWGraphicsDetail.ShippedValues.AllOff();

                foreach (Row lORow in mORows)
                    lORow.Set(lOValues, lORow.Get(lOPreset));

                fChanged();
            }
            else
            {
                fClickRow(lOValues, liX, liY);
            }
        }

        fRedraw();
    }

    private void fClickRow(UWGraphicsDetail.EffectValues pOValues, int piX, int piY)
    {
        int liRow = fRowAt(piY);

        if (liRow < 0 || piX < ControlLeft - 2 || piX > ValueLeft + 30)
            return;

        Row lORow = mORows[liRow];

        switch (lORow.Kind)
        {
            case KindEnum.Toggle:
                lORow.Set(pOValues, lORow.Get(pOValues) >= 0.5f ? 0f : 1f);
                fChanged();
                break;

            case KindEnum.Tonemapping:
                lORow.Set(pOValues, (lORow.Get(pOValues) + 1f) % 3f);
                fChanged();
                break;

            default:
                miDragRow = liRow;
                fSetFromBar(lORow, pOValues, piX);
                break;
        }
    }

    private void fSetFromBar(Row pORow, UWGraphicsDetail.EffectValues pOValues, int piX)
    {
        if (piX < 0)
            return;

        float lfFraction = Mathf.Clamp01((piX - ControlLeft) / (float)(BarWidth - 1));
        float lfValue = Mathf.Lerp(pORow.Min, pORow.Max, lfFraction);

        if (pORow.Kind == KindEnum.Steps)
            lfValue = Mathf.Round(lfValue);

        if (Mathf.Approximately(lfValue, pORow.Get(pOValues)))
            return;

        pORow.Set(pOValues, lfValue);
        fChanged();
    }

    private void fChanged()
    {
        UWGraphicsDetail.ApplyCustom();
        mbDirty = true;
    }

    private static int fRowAt(int piY)
    {
        if (piY < FirstRowTop - 2)
            return -1;

        return (piY - (FirstRowTop - 2)) / RowPitch;
    }

    private int fButtonAt(int piX, int piY)
    {
        if (piY < ButtonTop || piY >= ButtonTop + 14)
            return -1;

        if (piX >= OnButtonLeft && piX < OnButtonLeft + 31)
            return OnButtonImage;

        if (piX >= OffButtonLeft && piX < OffButtonLeft + 31)
            return OffButtonImage;

        if (piX >= DoneButtonLeft && piX < DoneButtonLeft + 31)
            return DoneButtonImage;

        return -1;
    }

    /// <summary>The mouse in pixels of the 320x200 frame, from the top left.</summary>
    private bool fToScreenPixel(Vector2 pOScreen, out int piX, out int piY)
    {
        piX = -1;
        piY = -1;

        Vector2 lOLocal;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mOFrame, pOScreen, null, out lOLocal))
            return false;

        Rect lORect = mOFrame.rect;

        piX = Mathf.FloorToInt(lOLocal.x - lORect.xMin);
        piY = Mathf.FloorToInt(lORect.yMax - lOLocal.y);

        return piX >= 0 && piX < ScreenWidth && piY >= 0 && piY < ScreenHeight;
    }

    // ------------------------------------------------- Drawing

    private void fRedraw()
    {
        if (!mbDirty || mOTexture == null)
            return;

        mbDirty = false;

        UWGraphicsDetail.EffectValues lOValues = UWGraphicsDetail.CustomValues;

        fDrawBackground();
        fDrawTextCentred(TitleTop, "Remastered effects");

        for (int liRow = 0; liRow < mORows.Count && lOValues != null; liRow++)
        {
            Row lORow = mORows[liRow];
            int liTop = FirstRowTop + (liRow * RowPitch);
            float lfValue = lORow.Get(lOValues);

            fDrawText("label" + liRow, LabelLeft, liTop, lORow.Label);

            switch (lORow.Kind)
            {
                case KindEnum.Toggle:
                    fDrawText("value" + liRow, ControlLeft, liTop, lfValue >= 0.5f ? "On" : "Off");
                    break;

                case KindEnum.Tonemapping:
                    fDrawText("value" + liRow, ControlLeft, liTop, fTonemappingName(Mathf.RoundToInt(lfValue)));
                    break;

                default:
                    fDrawBar(liTop, Mathf.InverseLerp(lORow.Min, lORow.Max, lfValue));
                    fDrawText("value" + liRow, ValueLeft, liTop, lORow.Kind == KindEnum.Steps
                        ? Mathf.RoundToInt(lfValue).ToString()
                        : lfValue.ToString(lORow.Max < 0.5f ? "0.000" : "0.00", System.Globalization.CultureInfo.InvariantCulture));
                    break;
            }
        }

        fDrawText("all", AllLabelLeft, ButtonTop + 5, "All:");
        fDrawButton(OnButtonLeft, OnButtonImage);
        fDrawButton(OffButtonLeft, OffButtonImage);
        fDrawButton(DoneButtonLeft, DoneButtonImage);

        fUpload();
    }

    private static string fTonemappingName(int piValue)
    {
        switch (piValue)
        {
            case 1: return "Neutral";
            case 2: return "ACES";
            default: return "Off";
        }
    }

    /// <summary>
    /// A marble without tiles: soft value noise in three octaves, turned into colours of the
    /// quit panel's marble. Its pixels are sorted by brightness, and the noise picks from that
    /// list - so the grain has the marble's colours and roughly its mix, but no seams. Tiling the
    /// marble piece itself showed every edge (per user, 2026-09-17: "not made for repeat").
    /// </summary>
    private void fDrawBackground()
    {
        byte[] lyRamp = fGetMarbleRamp();

        for (int liY = 0; liY < ScreenHeight; liY++)
        {
            for (int liX = 0; liX < ScreenWidth; liX++)
            {
                byte lyColour = 0x24;

                if (lyRamp != null)
                {
                    float lfNoise = (fValueNoise(liX, liY, 23f, 11) * 0.45f)
                        + (fValueNoise(liX, liY, 7f, 23) * 0.35f)
                        + (fValueNoise(liX, liY, 2f, 37) * 0.2f);

                    // The sum crowds around the middle; stretch it back out. Only the darker
                    // two thirds of the ramp: the light lettering needs the contrast, and the full
                    // range looked cloudy (checked on an offline render, 2026-09-17).
                    float lfSpread = Mathf.Clamp01(((lfNoise - 0.5f) * 1.8f) + 0.5f);
                    float lfQuantile = RampFrom + (lfSpread * RampSpan);

                    lyColour = lyRamp[Mathf.Min(lyRamp.Length - 1, (int)(lfQuantile * lyRamp.Length))];
                }

                bool lbEdge = liX == 0 || liY == 0 || liX == ScreenWidth - 1 || liY == ScreenHeight - 1;

                myPixels[(liY * ScreenWidth) + liX] = lbEdge ? ShadowColour : lyColour;
            }
        }
    }

    private const float RampFrom = 0.05f;

    private const float RampSpan = 0.6f;

    private byte[] mbyMarbleRamp;

    /// <summary>The marble pixels of the quit panel, darkest first.</summary>
    private byte[] fGetMarbleRamp()
    {
        if (mbyMarbleRamp != null)
            return mbyMarbleRamp;

        byte[] lyMarble = fGetIndices(QuitBackgroundImage, out int liWidth, out int liHeight);

        if (lyMarble == null || mOData.Palettes == null)
            return null;

        UWPalette lOPalette = mOData.Palettes.GetPalette(0);
        List<byte> lyPixels = new List<byte>();

        for (int liY = MarbleTop; liY < MarbleTop + MarbleHeight && liY < liHeight; liY++)
            for (int liX = MarbleLeft; liX < MarbleLeft + MarbleWidth && liX < liWidth; liX++)
            {
                byte lyIndex = lyMarble[(liY * liWidth) + liX];

                // Without the green moss spots - as dark blobs they stood out.
                if (lOPalette != null && lOPalette.Green[lyIndex] <= lOPalette.Red[lyIndex])
                    lyPixels.Add(lyIndex);
            }

        if (lyPixels.Count == 0 || lOPalette == null)
            return null;

        lyPixels.Sort((a, b) => fLuminance(lOPalette, a).CompareTo(fLuminance(lOPalette, b)));

        mbyMarbleRamp = lyPixels.ToArray();

        return mbyMarbleRamp;
    }

    private static int fLuminance(UWPalette pOPalette, byte pyIndex)
    {
        return (pOPalette.Red[pyIndex] * 3) + (pOPalette.Green[pyIndex] * 6) + pOPalette.Blue[pyIndex];
    }

    /// <summary>Smooth value noise between 0 and 1: random values on a grid of the given cell
    /// size, blended with a smoothstep. The seed keeps the picture the same on every open.</summary>
    private static float fValueNoise(int piX, int piY, float pfCell, int piSeed)
    {
        float lfX = piX / pfCell;
        float lfY = piY / pfCell;

        int liX = Mathf.FloorToInt(lfX);
        int liY = Mathf.FloorToInt(lfY);

        float lfFractionX = lfX - liX;
        float lfFractionY = lfY - liY;

        lfFractionX = lfFractionX * lfFractionX * (3f - (2f * lfFractionX));
        lfFractionY = lfFractionY * lfFractionY * (3f - (2f * lfFractionY));

        float lfTop = Mathf.Lerp(fHash(liX, liY, piSeed), fHash(liX + 1, liY, piSeed), lfFractionX);
        float lfBottom = Mathf.Lerp(fHash(liX, liY + 1, piSeed), fHash(liX + 1, liY + 1, piSeed), lfFractionX);

        return Mathf.Lerp(lfTop, lfBottom, lfFractionY);
    }

    private static float fHash(int piX, int piY, int piSeed)
    {
        unchecked
        {
            uint luHash = (uint)((piX * 374761393) + (piY * 668265263) + (piSeed * 982451653));

            luHash = (luHash ^ (luHash >> 13)) * 1274126177u;
            luHash ^= luHash >> 16;

            return (luHash & 0xFFFF) / 65535f;
        }
    }

    private void fDrawBar(int piTop, float pfFraction)
    {
        int liFilled = Mathf.RoundToInt(Mathf.Clamp01(pfFraction) * (BarWidth - 2));

        for (int liY = 0; liY < BarHeight; liY++)
        {
            for (int liX = 0; liX < BarWidth; liX++)
            {
                bool lbEdge = liX == 0 || liY == 0 || liX == BarWidth - 1 || liY == BarHeight - 1;
                bool lbFilled = !lbEdge && (liX - 1) < liFilled;

                if (lbEdge)
                    fPut(ControlLeft + liX, piTop - 1 + liY, ShadowColour);
                else if (lbFilled)
                    fPut(ControlLeft + liX, piTop - 1 + liY, TextColour);
            }
        }
    }

    private void fDrawButton(int piLeft, int piImage)
    {
        int liImage = piImage + (miHoveredButton == piImage ? 1 : 0);
        byte[] lyIndices = fGetIndices(liImage, out int liWidth, out int liHeight);

        if (lyIndices == null)
            return;

        for (int liY = 0; liY < liHeight; liY++)
            for (int liX = 0; liX < liWidth; liX++)
                fPut(piLeft + liX, ButtonTop + liY, lyIndices[(liY * liWidth) + liX]);
    }

    private void fDrawTextCentred(int piTop, string psText)
    {
        fSetText("title", 0, piTop, ScreenWidth, psText, TextAnchor.MiddleCenter);
    }

    private void fDrawText(string psKey, int piLeft, int piTop, string psText)
    {
        fSetText(psKey, piLeft, piTop, ScreenWidth - piLeft, psText, TextAnchor.MiddleLeft);
    }

    /// <summary>
    /// The lettering is not in the picture but a Unity Text over it, in the same built-in font as
    /// the menu bar - the button font of the original did not go with the rest of the new menu
    /// (per user, 2026-09-17). Rendered at screen resolution, so it stays sharp; the light colour
    /// of the original's button lettering and a black shadow are kept.
    /// </summary>
    private void fSetText(string psKey, int piLeft, int piTop, int piWidth, string psText, TextAnchor peAlignment)
    {
        if (mOImage == null)
            return;

        Text lOText;

        if (!mOTexts.TryGetValue(psKey, out lOText) || lOText == null)
        {
            GameObject lOObject = new GameObject("Text " + psKey, typeof(RectTransform), typeof(Text));
            lOObject.transform.SetParent(mOImage.transform, false);

            RectTransform lORect = (RectTransform)lOObject.transform;
            lORect.anchorMin = new Vector2(0f, 1f);
            lORect.anchorMax = new Vector2(0f, 1f);
            lORect.pivot = new Vector2(0f, 1f);

            lOText = lOObject.GetComponent<Text>();
            lOText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lOText.fontSize = TextSize;
            lOText.horizontalOverflow = HorizontalWrapMode.Overflow;
            lOText.verticalOverflow = VerticalWrapMode.Overflow;
            lOText.raycastTarget = false;
            lOText.color = fGetTextColour();

            Shadow lOShadow = lOObject.AddComponent<Shadow>();
            lOShadow.effectColor = Color.black;
            lOShadow.effectDistance = new Vector2(0.5f, -0.5f);

            mOTexts[psKey] = lOText;
        }

        RectTransform lOTextRect = (RectTransform)lOText.transform;

        // Centred on where the four pixel high lettering used to stand.
        lOTextRect.anchoredPosition = new Vector2(piLeft, -(piTop - 2));
        lOTextRect.sizeDelta = new Vector2(piWidth, RowPitch);

        lOText.alignment = peAlignment;
        lOText.text = psText;
    }

    private const int TextSize = 7;

    private readonly Dictionary<string, Text> mOTexts = new Dictionary<string, Text>();

    private Color32 fGetTextColour()
    {
        UWPalette lOPalette = mOData != null && mOData.Palettes != null ? mOData.Palettes.GetPalette(0) : null;

        return lOPalette != null
            ? new Color32(lOPalette.Red[TextColour], lOPalette.Green[TextColour], lOPalette.Blue[TextColour], 255)
            : new Color32(230, 200, 180, 255);
    }

    private void fPut(int piX, int piY, byte pyColour)
    {
        if (piX < 0 || piY < 0 || piX >= ScreenWidth || piY >= ScreenHeight)
            return;

        myPixels[(piY * ScreenWidth) + piX] = pyColour;
    }

    private byte[] fGetIndices(int piImage, out int piWidth, out int piHeight)
    {
        piWidth = 0;
        piHeight = 0;

        try
        {
            UWTexture lOTexture = mOData.Textures.GetTextureByType(UWTexture.TextureTypes.OPTBTNS, piImage);

            if (lOTexture == null)
                return null;

            piWidth = lOTexture.Width;
            piHeight = lOTexture.Height;

            return lOTexture.GetMainPaletteIndices();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Indices into red, rows flipped - the same form as UWIconTextureBuilder.</summary>
    private void fUpload()
    {
        Color32[] lyColours = new Color32[ScreenWidth * ScreenHeight];

        for (int liY = 0; liY < ScreenHeight; liY++)
        {
            int liSourceRow = (ScreenHeight - 1 - liY) * ScreenWidth;
            int liTargetRow = liY * ScreenWidth;

            for (int liX = 0; liX < ScreenWidth; liX++)
                lyColours[liTargetRow + liX] = new Color32(myPixels[liSourceRow + liX], 0, 0, 255);
        }

        mOTexture.SetPixels32(lyColours);
        mOTexture.Apply();
    }
}
