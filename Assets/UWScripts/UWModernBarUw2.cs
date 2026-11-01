using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE ACTION BAR ON THE SECOND GAME (UW2 plan phase 3 step 8): ten places at the bottom's middle on the keys 1 to
/// 9 and 0, as the first game's (UWModernActionBar), which stands on UWGameUI and Interaction and so does not run
/// here. A place points at one carried piece, by the same rule (UWModernActionBar.CanBind) and kept the same way
/// (UWHelpNotes, UWInventoryPaths). Filled by putting a thing from the pointer on it with the bags open; a right
/// click empties it. Using is the inventory's own use (UWGameScreenUw2), so what cannot be used there yet cannot
/// be here either. Spells come with the second game's magic in phase 4.
/// </summary>
public sealed class UWModernBarUw2
{
    public const int SlotCount = UWControls.ModernSlotCount;

    private const float CellSize = 20f;

    private const float CellGap = 1f;

    private const float Margin = 4f;

    /// <summary>A place's alpha while its piece hangs on the pointer.</summary>
    private const float DimAlpha = 0.35f;

    private static readonly Color CellColour = new Color(0f, 0f, 0f, 0.45f);

    private readonly UWObject[] mOSlots = new UWObject[SlotCount];

    private readonly Image[] mOCells = new Image[SlotCount];

    private readonly RawImage[] mOPictures = new RawImage[SlotCount];

    private readonly RawImage[] mOCounts = new RawImage[SlotCount];

    private readonly RawImage[] mOKeys = new RawImage[SlotCount];

    private readonly Rect[] mORects = new Rect[SlotCount];

    private UWInventoryModel mOWatched;

    /// <summary>UWHelpNotes.LoadCount and the model's when the paths were last read into pieces.</summary>
    private int miLoadCount = -1;

    private int miModelLoadCount = -1;

    public UWModernBarUw2(Canvas pOCanvas)
    {
        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            mOCells[liSlot] = UWUiKit.CreateImage(pOCanvas.transform, "Bar " + liSlot, UWUiKit.BottomLeft, UWUiKit.BottomLeft, CellColour);
            mOCells[liSlot].raycastTarget = false;
            mOPictures[liSlot] = fImage(pOCanvas, "Bar thing " + liSlot);
            UWPixelArtUI.Apply(mOPictures[liSlot]);
            mOCounts[liSlot] = fImage(pOCanvas, "Bar count " + liSlot);
            mOKeys[liSlot] = fImage(pOCanvas, "Bar key " + liSlot);
        }
    }

    private static RawImage fImage(Canvas pOCanvas, string psName)
    {
        RawImage lOImage = UWUiKit.CreateRawImage(pOCanvas.transform, psName, UWUiKit.BottomLeft, UWUiKit.BottomLeft);

        lOImage.raycastTarget = false;

        return lOImage;
    }

    /// <summary>Nothing of the bar shows (the next Tick brings it back).</summary>
    public void Hide()
    {
        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            mOCells[liSlot].enabled = false;
            mOPictures[liSlot].enabled = false;
            UWTextLabel.Hide(mOCounts[liSlot]);
            UWTextLabel.Hide(mOKeys[liSlot]);
            mORects[liSlot] = Rect.zero;
        }
    }

    /// <summary>The bar's left edge on the screen.</summary>
    public static float LeftEdge(float pfScale)
    {
        return (Screen.width - (SlotCount * (CellSize + CellGap) * pfScale)) * 0.5f;
    }

    /// <summary>The place under a screen point, -1 for none.</summary>
    public int SlotAt(Vector2 pOPointer)
    {
        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            if (mORects[liSlot].Contains(pOPointer))
                return liSlot;
        }

        return -1;
    }

    /// <summary>The piece on a place when it is carried now, else null.</summary>
    public UWObject Carried(int piSlot, UWInventoryModel pOInventory)
    {
        UWObject lOItem = mOSlots[piSlot];

        return lOItem != null && pOInventory != null && UWInventoryPaths.PathOf(pOInventory, lOItem) != null ? lOItem : null;
    }

    /// <summary>A thing onto a place; a piece stands on one place only.</summary>
    public bool Bind(int piSlot, UWObject pOItem)
    {
        if (!UWModernActionBar.CanBind(pOItem))
            return false;

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            if (mOSlots[liSlot] == pOItem)
                mOSlots[liSlot] = null;
        }

        mOSlots[piSlot] = pOItem;

        return true;
    }

    public void Clear(int piSlot)
    {
        mOSlots[piSlot] = null;
    }

    private void fOnMerged(UWObject pOFrom, UWObject pOInto)
    {
        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            if (mOSlots[liSlot] == pOFrom)
                mOSlots[liSlot] = pOInto;
        }
    }

    /// <summary>The places as paths and object numbers into UWHelpNotes for saving; not read yet since the last
    /// load, what was read stays (UWModernActionBar.CaptureSlots).</summary>
    public void Capture(UWInventoryModel pOInventory)
    {
        if (pOInventory == null || miLoadCount != UWHelpNotes.LoadCount)
            return;

        string[] lsPaths = new string[SlotCount];
        int[] liIds = new int[SlotCount];

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            string lsPath = mOSlots[liSlot] != null ? UWInventoryPaths.PathOf(pOInventory, mOSlots[liSlot]) : null;

            lsPaths[liSlot] = lsPath ?? string.Empty;
            liIds[liSlot] = lsPath != null ? mOSlots[liSlot].ID : -1;
        }

        UWHelpNotes.ActionBarPaths = lsPaths;
        UWHelpNotes.ActionBarIds = liIds;
    }

    /// <summary>After a load the paths read into pieces once, when the inventory has come in.</summary>
    private void fResolveIfLoaded(UWInventoryModel pOInventory)
    {
        if (miLoadCount == UWHelpNotes.LoadCount && miModelLoadCount == pOInventory.LoadCount)
            return;

        miLoadCount = UWHelpNotes.LoadCount;
        miModelLoadCount = pOInventory.LoadCount;

        string[] lsPaths = UWHelpNotes.ActionBarPaths ?? new string[0];
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        List<UWObject> lOMasterlist = lOLoader != null && lOLoader.CurrentLevel != null ? lOLoader.CurrentLevel.Masterlist : null;

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            UWObject lOItem = liSlot < lsPaths.Length && !string.IsNullOrEmpty(lsPaths[liSlot])
                ? UWInventoryPaths.ResolveChecked(pOInventory, lsPaths[liSlot], UWHelpNotes.IdAt(UWHelpNotes.ActionBarIds, liSlot), UWHelpNotes.IsStale, lOMasterlist)
                : null;

            if (lOItem != null && System.Array.IndexOf(mOSlots, lOItem, 0, liSlot) >= 0)
                lOItem = null;

            mOSlots[liSlot] = UWModernActionBar.CanBind(lOItem) ? lOItem : null;
        }
    }

    /// <summary>The bar for this frame; pFPicture gives a thing's picture.</summary>
    public void Tick(UWInventoryModel pOInventory, DataImport pOData, float pfScale, Color32 pOInk, System.Func<int, Texture2D> pFPicture)
    {
        if (pOInventory != mOWatched)
        {
            if (mOWatched != null)
                mOWatched.ItemMerged -= fOnMerged;

            mOWatched = pOInventory;

            if (mOWatched != null)
                mOWatched.ItemMerged += fOnMerged;
        }

        if (pOInventory != null)
            fResolveIfLoaded(pOInventory);

        float lfCell = CellSize * pfScale;
        float lfStep = (CellSize + CellGap) * pfScale;
        float lfLeft = LeftEdge(pfScale);
        UWFont lOSmall = pOData.Fonts != null ? pOData.Fonts.Get(UWFonts.FontType.Small) : null;

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            Rect lORect = new Rect(lfLeft + (liSlot * lfStep), Margin * pfScale, lfCell, lfCell);

            mORects[liSlot] = lORect;
            mOCells[liSlot].rectTransform.sizeDelta = lORect.size;
            mOCells[liSlot].rectTransform.anchoredPosition = lORect.position;
            mOCells[liSlot].enabled = true;

            bool lbHeld = pOInventory != null && mOSlots[liSlot] != null && pOInventory.CursorItem == mOSlots[liSlot];
            UWObject lOItem = lbHeld ? mOSlots[liSlot] : Carried(liSlot, pOInventory);
            Texture2D lOPicture = lOItem != null ? pFPicture(lOItem.ID) : null;
            RawImage lOImage = mOPictures[liSlot];

            if (lOPicture != null)
            {
                lOImage.texture = lOPicture;
                lOImage.enabled = true;
                lOImage.color = new Color(1f, 1f, 1f, lbHeld ? DimAlpha : 1f);
                lOImage.rectTransform.sizeDelta = new Vector2(lOPicture.width * UWModernHudArt.PixelAspectX * pfScale, lOPicture.height * pfScale);
                lOImage.rectTransform.anchoredPosition = new Vector2(lORect.x + ((lfCell - lOImage.rectTransform.sizeDelta.x) * 0.5f),
                    lORect.y + ((lfCell - lOImage.rectTransform.sizeDelta.y) * 0.5f));
            }
            else
                lOImage.enabled = false;

            fSmallText(mOKeys[liSlot], lOSmall, ((liSlot + 1) % SlotCount).ToString(), pOInk, pfScale, lORect.x + pfScale, lORect.yMax - (6f * pfScale));

            int liCount = lOItem != null ? UWStackRules.Count(lOItem) : 0;

            fSmallText(mOCounts[liSlot], lOSmall, liCount > 1 ? liCount.ToString() : null, pOInk, pfScale, lORect.x + pfScale, lORect.y + pfScale);
        }
    }

    private static void fSmallText(RawImage pOImage, UWFont pOFont, string psText, Color32 pOInk, float pfScale, float pfX, float pfY)
    {
        if (pOFont == null || psText == null)
        {
            UWTextLabel.Hide(pOImage);

            return;
        }

        UWTextLabel.Get(pOImage).SetLine(pOFont, psText, UWTextRaster.TightWidth(pOFont, psText), pOInk, false, FilterMode.Point);
        pOImage.rectTransform.localScale = new Vector3(pfScale, pfScale, 1f);
        pOImage.rectTransform.anchoredPosition = new Vector2(pfX, pfY);
    }
}
