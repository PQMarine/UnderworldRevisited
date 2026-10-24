using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// The two dragons on the HUD, after UW.EXE (UpdateUIAnim_seg036_3087_3DF mode 4 for the
/// requests, Dragons_seg036_3087_A20 for the state machine, UI update seg036_3087_5A5 for the
/// timing; per user, 2026-09-14: "build the dragons after UW.EXE").
///
/// MAIN.BYT only holds the green bodies. Neck pieces, resting heads and tails come from
/// DRAGONS.GR and are drawn as separate pictures (UW.EXE seg036_3087_16E).
///
/// THE FRAME LOGIC - the three head animations, the tail wag, the requests and the order of the
/// random draws - is UWDragonAnimator's (engine-free, cleanup 5.18, 2026-10-23); this class keeps
/// the pictures, the clock-seeded generator and the step timing: one step every 64 ticks of the
/// 256 Hz game timer, 0.25 s, at most one step per frame. The dragons only run on the main game
/// screen; on the map and in conversations they stand still and continue afterwards.
/// </summary>
public class UWHudDragons : UWDragonAnimator.IPictures
{
    public const int ScrollAnimation = UWDragonAnimator.ScrollAnimation;

    public const int NodAnimation = UWDragonAnimator.NodAnimation;

    public const int CoverAnimation = UWDragonAnimator.CoverAnimation;

    /// <summary>0.25 s: 64 ticks of the 256 Hz timer.</summary>
    private const float StepsPerSecond = 4f;

    /// <summary>Right dragon images are the left ones plus this.</summary>
    private const int RightImageOffset = UWDragonAnimator.RightImageOffset;

    private const int NeckImage = UWDragonAnimator.NeckImage;

    private const int HeadImage = UWDragonAnimator.HeadImage;

    private const int TailRestImage = UWDragonAnimator.TailRestImage;

    // Screen top-left corners in 320x200 reference pixels (UW.EXE EXE y converted: top = 199 - y),
    // UWClassicLayout.Dragons.
    private static readonly Vector2Int[] msNeckPosition = fPoints(UWClassicLayout.Dragons.Neck);

    private static readonly Vector2Int[] msHeadPosition = fPoints(UWClassicLayout.Dragons.Head);

    private static readonly Vector2Int[] msTailPosition = fPoints(UWClassicLayout.Dragons.Tail);

    private static Vector2Int[] fPoints((int X, int Y)[] pyPoints)
    {
        return Array.ConvertAll(pyPoints, pyPoint => new Vector2Int(pyPoint.X, pyPoint.Y));
    }

    /// <summary>Every pair built - the classic frame's and Classic Wide's (UWModernClassicFrame);
    /// a request reaches them all. A pair leaves it when its owner replaces it (Release) or when its
    /// pictures are gone with the scene (fForgetGone) - until 2026-10-19 it was never emptied, and
    /// every scene load and every Classic Wide resize added a pair (B33, cleanup step 5.5).</summary>
    private static readonly List<UWHudDragons> msInstances = new List<UWHudDragons>();

    private readonly Func<int, Sprite> mOSpriteFor;

    private readonly Image[] mONeck = new Image[2];

    private readonly Image[] mOHead = new Image[2];

    private readonly Image[] mOTail = new Image[2];

    private readonly Image[] mOOverlay = new Image[2];

    /// <summary>The frame logic, with this pair's generator.</summary>
    private readonly UWDragonAnimator mOAnimator;

    private long miLastStep = -1;

    private readonly System.Random mORandom = new System.Random();

    public UWHudDragons(RectTransform pOParent, Func<int, Sprite> pOSpriteFor, int piRightShift = 0)
    {
        mOSpriteFor = pOSpriteFor;
        mOAnimator = new UWDragonAnimator(this, piRightShift, mORandom.Next);
        fForgetGone();
        msInstances.Add(this);

        for (int liSide = 0; liSide < 2; liSide++)
        {
            int liOffset = liSide * RightImageOffset;
            Vector2Int lOShift = new Vector2Int(liSide * piRightShift, 0);

            // Draw order as the UW.EXE sprite layers: tail, neck, head, animation overlay.
            mOTail[liSide] = fCreate("DragonTail" + liSide, pOParent, msTailPosition[liSide] + lOShift, TailRestImage + liOffset);
            mONeck[liSide] = fCreate("DragonNeck" + liSide, pOParent, msNeckPosition[liSide] + lOShift, NeckImage + liOffset);
            mOHead[liSide] = fCreate("DragonHead" + liSide, pOParent, msHeadPosition[liSide] + lOShift, HeadImage + liOffset);
            mOOverlay[liSide] = fCreate("DragonOverlay" + liSide, pOParent, msHeadPosition[liSide] + lOShift, -1);
        }
    }

    /// <summary>
    /// A request from the game - UW.EXE UpdateUIAnim_seg036_3087_3DF case 4. Every pair picks its
    /// dragon (UWDragonAnimator.Request).
    /// </summary>
    public static void Request(int piAnimation)
    {
        fForgetGone();

        foreach (UWHudDragons lODragons in msInstances)
            lODragons.mOAnimator.Request(piAnimation);
    }

    /// <summary>The pair no longer takes requests - its owner builds a new one (Classic Wide on a
    /// resize; the old pictures are destroyed only at the end of the frame).</summary>
    public void Release()
    {
        msInstances.Remove(this);
    }

    /// <summary>Drops the pairs whose pictures were destroyed (Unity's null: the scene went).</summary>
    private static void fForgetGone()
    {
        msInstances.RemoveAll(lODragons => lODragons.mONeck[0] == null);
    }

    /// <summary>Sibling index of the lowest dragon picture - pictures that must lie below the
    /// dragons are inserted there.</summary>
    public int FirstSiblingIndex
    {
        get
        {
            int liLowest = int.MaxValue;

            foreach (Image[] lOGroup in new[] { mOTail, mONeck, mOHead, mOOverlay })
                foreach (Image lOImage in lOGroup)
                    if (lOImage != null)
                        liLowest = Mathf.Min(liLowest, lOImage.transform.GetSiblingIndex());

            return liLowest == int.MaxValue ? 0 : liLowest;
        }
    }

    /// <summary>Hides or shows all dragon pictures - the conversation frame has them baked in.
    /// </summary>
    public void SetVisible(bool pbVisible)
    {
        for (int liSide = 0; liSide < 2; liSide++)
        {
            fSetEnabled(mOTail[liSide], pbVisible);
            fSetEnabled(mONeck[liSide], pbVisible);
            fSetEnabled(mOHead[liSide], pbVisible && !mOAnimator.IsCovering(liSide));
            fSetEnabled(mOOverlay[liSide], pbVisible && mOOverlay[liSide].sprite != null && mOAnimator.IsOverlayActive(liSide));
        }
    }

    /// <summary>Called every frame while the main game screen is active.</summary>
    public void Tick()
    {
        long liStep = (long)(Time.unscaledTime * StepsPerSecond);

        if (liStep == miLastStep)
            return;

        miLastStep = liStep;

        mOAnimator.Step();
    }

    // ------------------------------------------------- The pictures (UWDragonAnimator.IPictures)

    void UWDragonAnimator.IPictures.ShowTail(int piSide, int piImage)
    {
        fShow(mOTail[piSide], piImage);
    }

    void UWDragonAnimator.IPictures.ShowOverlay(int piSide, int piImage)
    {
        fShow(mOOverlay[piSide], piImage);
    }

    void UWDragonAnimator.IPictures.HideOverlay(int piSide)
    {
        fSetEnabled(mOOverlay[piSide], false);
    }

    void UWDragonAnimator.IPictures.PlaceOverlay(int piSide, int piX, int piY)
    {
        RectTransform lORect = (RectTransform)mOOverlay[piSide].transform;

        lORect.anchoredPosition = new Vector2(piX, -piY);
    }

    void UWDragonAnimator.IPictures.ShowHead(int piSide, int piImage)
    {
        fShow(mOHead[piSide], piImage);
    }

    void UWDragonAnimator.IPictures.HideHead(int piSide)
    {
        fSetEnabled(mOHead[piSide], false);
    }

    private Image fCreate(string psName, RectTransform pOParent, Vector2Int pOCorner, int piImage)
    {
        Image lOImage = UWUiKit.CreateImage(pOParent, psName, UWUiKit.TopLeft, UWUiKit.TopLeft, Color.white);
        lOImage.rectTransform.anchoredPosition = new Vector2(pOCorner.x, -pOCorner.y);

        if (piImage >= 0)
            fShow(lOImage, piImage);
        else
            lOImage.enabled = false;

        return lOImage;
    }

    private void fShow(Image pOImage, int piImage)
    {
        Sprite lOSprite = mOSpriteFor(piImage);

        if (lOSprite == null)
        {
            pOImage.enabled = false;
            return;
        }

        pOImage.sprite = lOSprite;
        ((RectTransform)pOImage.transform).sizeDelta = new Vector2(lOSprite.rect.width, lOSprite.rect.height);
        pOImage.enabled = true;
    }

    private static void fSetEnabled(Image pOImage, bool pbEnabled)
    {
        if (pOImage != null)
            pOImage.enabled = pbEnabled;
    }
}
