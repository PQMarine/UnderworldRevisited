using UnityEngine;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME'S POINTER IN ITS MENUS (per user 2026-11-19: "the fitting pointer as in UW1" - with the gamepad the
/// main menu and the character creation showed only the plain system arrow): CURSORS.GR 0, the game's own pointer, drawn
/// transparent and scaled by whole steps as the game screen draws its pointers (UWGameScreenUw2.fScaledCursor), with the
/// hotspot in the middle as there. The main menu applies it every frame while the second game's menu, its save list or
/// its creation is shown; UWPointer then shows it instead of the provisional system arrow of 2026-10-29.
/// </summary>
public static class UWMenuPointerUw2
{
    /// <summary>CURSORS.GR's plain pointer (UWGameScreenUw2: 0 the pointer, 1 to 8 the arrows).</summary>
    private const int PointerPicture = 0;

    private static Texture2D msOScaled;

    private static DataImport msOData;

    private static int miScale;

    private static int miColourVersion = -1;

    /// <summary>The frame the picture was last applied: UWPointer keeps the system arrow away while it is fresh.</summary>
    private static int miAppliedFrame = -100;

    /// <summary>The menu's pointer was applied this frame or the last.</summary>
    public static bool IsActive => Time.frameCount - miAppliedFrame <= 1;

    /// <summary>The second game's pointer on the screen, built anew when the data, the scale or the colour help changed.</summary>
    public static void Apply(DataImport pOData)
    {
        if (pOData == null)
            return;

        int liScale = Mathf.Max(1, Mathf.FloorToInt(UWUiFit.Scale));

        if (msOScaled == null || msOData != pOData || miScale != liScale || miColourVersion != UWColourVision.Version)
        {
            fRelease();
            msOData = pOData;
            miScale = liScale;
            miColourVersion = UWColourVision.Version;
            msOScaled = fBuild(pOData, liScale);
        }

        if (msOScaled == null)
            return;

        miAppliedFrame = Time.frameCount;
        UWPointer.SetPicture(msOScaled, new Vector2(msOScaled.width * 0.5f, msOScaled.height * 0.5f));
    }

    private static Texture2D fBuild(DataImport pOData, int piScale)
    {
        UWTexture lOPicture = pOData.Textures != null ? pOData.Textures.GetTextureByType(UWTexture.TextureTypes.CURSORS, PointerPicture) : null;

        if (lOPicture == null || lOPicture.PaletteIndices == null || lOPicture.Width <= 0 || lOPicture.Height <= 0)
            return null;

        byte[] lyRgba = UWUploadPixels.FromTexture(lOPicture, UWColourVision.Filter);
        byte[] lyIndices = lOPicture.PaletteIndices;
        int liWidth = lOPicture.Width;
        int liHeight = lOPicture.Height;
        Color32[] lyTo = new Color32[liWidth * piScale * liHeight * piScale];

        // The source rows come bottom-up (UWUploadPixels); index 0 is clear, as the game screen draws its pointers.
        for (int liY = 0; liY < liHeight * piScale; liY++)
        {
            int liRow = liY / piScale;

            for (int liX = 0; liX < liWidth * piScale; liX++)
            {
                int liColumn = liX / piScale;
                int liAt = ((liRow * liWidth) + liColumn) * UWUploadPixels.BytesPerPixel;
                int liIndex = ((liHeight - 1 - liRow) * liWidth) + liColumn;
                bool lbClear = liIndex < lyIndices.Length && lyIndices[liIndex] == 0;

                lyTo[(liY * liWidth * piScale) + liX] = new Color32(lyRgba[liAt], lyRgba[liAt + 1], lyRgba[liAt + 2], lbClear ? (byte)0 : (byte)255);
            }
        }

        return UWTextureFactory.FromColours(lyTo, liWidth * piScale, liHeight * piScale, FilterMode.Point, TextureWrapMode.Clamp,
            "UW2 menu pointer");
    }

    private static void fRelease()
    {
        if (msOScaled == null)
            return;

        UWPointer.ReleasePicture(msOScaled);
        Object.Destroy(msOScaled);
        msOScaled = null;
    }
}
