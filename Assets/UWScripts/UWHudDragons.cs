using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The two dragons on the HUD, after UW.EXE (UpdateUIAnim_seg036_3087_3DF mode 4 for the
/// requests, Dragons_seg036_3087_A20 for the state machine, UI update seg036_3087_5A5 for the
/// timing; per user, 2026-09-14: "build the dragons after UW.EXE").
///
/// MAIN.BYT only holds the green bodies. Neck pieces, resting heads and tails come from
/// DRAGONS.GR and are drawn as separate pictures (UW.EXE seg036_3087_16E).
///
/// THREE HEAD ANIMATIONS, requested by the game:
///   1 scroll  the message scroll moves on         frames 2..5 twice, then hidden
///   2 nod     the player kills a creature         frames 6,7,8,9,8,9,8,9,8,7,6
///   3 cover   heavy damage or damage below 16 HP  frames 10..13, hold 6 steps, back to 10
/// The right dragon uses the same frames plus 18. A request for an animation that already runs
/// or waits on either dragon is ignored; each dragon has one waiting slot, and a waiting request
/// cuts the running animation short (scroll finishes its pass, nod plays backward, cover ends
/// its hold).
///
/// THE TAIL WAG (frames 14,15,16,17,16,15,14) is rolled at random, about 5 % per step. UW.EXE
/// sets the wrong redraw bit for it (the flask bit instead of the dragon's), so the wag only
/// plays while that dragon's state machine runs anyway - during or right after a head
/// animation. That quirk is kept, and so is the shared tail frame counter of both dragons.
///
/// TIMING: one step every 64 ticks of the 256 Hz game timer, 0.25 s, at most one step per
/// frame. The dragons only run on the main game screen; on the map and in conversations they
/// stand still and continue afterwards.
/// </summary>
public class UWHudDragons
{
    public const int ScrollAnimation = 1;

    public const int NodAnimation = 2;

    public const int CoverAnimation = 3;

    /// <summary>0.25 s: 64 ticks of the 256 Hz timer.</summary>
    private const float StepsPerSecond = 4f;

    /// <summary>Right dragon images are the left ones plus this.</summary>
    private const int RightImageOffset = 18;

    private const int NeckImage = 0;

    private const int HeadImage = 1;

    private const int TailRestImage = 14;

    /// <summary>RNG() &lt; 0x666 of 0..32767, UW.EXE seg036_3087_5A5.</summary>
    private const int TailRollThreshold = 0x666;

    private static readonly int[] msTailSequence = { 14, 15, 16, 17, 16, 15, 14 };

    // Screen top-left corners in 320x200 reference pixels (UW.EXE EXE y converted: top = 199 - y).
    private static readonly Vector2Int[] msNeckPosition = { new Vector2Int(36, 134), new Vector2Int(228, 134) };

    private static readonly Vector2Int[] msHeadPosition = { new Vector2Int(36, 145), new Vector2Int(204, 145) };

    private static readonly Vector2Int[] msTailPosition = { new Vector2Int(40, 65), new Vector2Int(224, 65) };

    // [side, animation - 1]: overlay corner, first and last frame (dseg 824/830, 91E/92A).
    private static readonly Vector2Int[,] msOverlayPosition =
    {
        { new Vector2Int(40, 155), new Vector2Int(48, 145), new Vector2Int(36, 145) },
        { new Vector2Int(204, 155), new Vector2Int(204, 145), new Vector2Int(200, 145) }
    };

    private static readonly int[] msFirstFrame = { 2, 6, 10 };

    private static readonly int[] msLastFrame = { 5, 9, 13 };

    private static UWHudDragons msInstance;

    private readonly Func<int, Sprite> mOSpriteFor;

    private readonly Image[] mONeck = new Image[2];

    private readonly Image[] mOHead = new Image[2];

    private readonly Image[] mOTail = new Image[2];

    private readonly Image[] mOOverlay = new Image[2];

    private readonly int[] miCurrent = new int[2];

    private readonly int[] miPending = new int[2];

    private readonly int[] miPhase = new int[2];

    private readonly int[] miCounter = new int[2];

    private readonly int[] miLoop = new int[2];

    private readonly bool[] mbRunning = new bool[2];

    private readonly bool[] mbTailWag = new bool[2];

    /// <summary>Shared by both dragons, as in UW.EXE (dseg 91C).</summary>
    private int miTailCounter;

    private long miLastStep = -1;

    private readonly System.Random mORandom = new System.Random();

    public UWHudDragons(RectTransform pOParent, Func<int, Sprite> pOSpriteFor)
    {
        mOSpriteFor = pOSpriteFor;
        msInstance = this;

        for (int liSide = 0; liSide < 2; liSide++)
        {
            int liOffset = liSide * RightImageOffset;

            // Draw order as the UW.EXE sprite layers: tail, neck, head, animation overlay.
            mOTail[liSide] = fCreate("DragonTail" + liSide, pOParent, msTailPosition[liSide], TailRestImage + liOffset);
            mONeck[liSide] = fCreate("DragonNeck" + liSide, pOParent, msNeckPosition[liSide], NeckImage + liOffset);
            mOHead[liSide] = fCreate("DragonHead" + liSide, pOParent, msHeadPosition[liSide], HeadImage + liOffset);
            mOOverlay[liSide] = fCreate("DragonOverlay" + liSide, pOParent, msHeadPosition[liSide], -1);
        }
    }

    /// <summary>
    /// A request from the game - UW.EXE UpdateUIAnim_seg036_3087_3DF case 4. Picks the dragon:
    /// a free one if only one is free, otherwise at random or the one without a waiting request.
    /// </summary>
    public static void Request(int piAnimation)
    {
        if (msInstance != null)
            msInstance.fRequest(piAnimation);
    }

    private void fRequest(int piValue)
    {
        if (piValue == miPending[0] || piValue == miPending[1] || piValue == miCurrent[0] || piValue == miCurrent[1])
            return;

        int liSide;

        if (miCurrent[0] == 0 && miCurrent[1] == 0)
            liSide = mORandom.Next(2);
        else if (miCurrent[0] != 0 && miCurrent[1] == 0)
            liSide = 1;
        else if (miCurrent[0] == 0)
            liSide = 0;
        else if (miPending[0] == 0 && miPending[1] == 0)
            liSide = mORandom.Next(2);
        else if (miPending[0] != 0 && miPending[1] == 0)
            liSide = 1;
        else
            liSide = 0;

        miPending[liSide] = piValue;
        mbRunning[liSide] = true;
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
            fSetEnabled(mOHead[liSide], pbVisible && miCurrent[liSide] != CoverAnimation);
            fSetEnabled(mOOverlay[liSide], pbVisible && mOOverlay[liSide].sprite != null && miPhase[liSide] != 0);
        }
    }

    /// <summary>Called every frame while the main game screen is active.</summary>
    public void Tick()
    {
        long liStep = (long)(Time.unscaledTime * StepsPerSecond);

        if (liStep == miLastStep)
            return;

        miLastStep = liStep;

        // The flask bubble roll comes first in UW.EXE; it does not change the dragons.
        mORandom.Next(32768);

        int liRoll = mORandom.Next(32768);

        if (liRoll < TailRollThreshold && !mbTailWag[liRoll & 1])
            mbTailWag[liRoll & 1] = true;

        for (int liSide = 0; liSide < 2; liSide++)
        {
            if (mbRunning[liSide])
                fStep(liSide);
        }
    }

    private void fStep(int piSide)
    {
        if (mbTailWag[piSide])
        {
            fShow(mOTail[piSide], msTailSequence[miTailCounter] + (piSide * RightImageOffset));

            if (++miTailCounter >= msTailSequence.Length)
            {
                miTailCounter = 0;
                mbTailWag[piSide] = false;
            }
        }

        if (miPhase[piSide] == 0 && miPending[piSide] != 0)
        {
            miCurrent[piSide] = miPending[piSide];
            miPhase[piSide] = 1;
        }

        switch (miCurrent[piSide])
        {
            case 0:
                if (!mbTailWag[piSide])
                    mbRunning[piSide] = false;
                break;

            case ScrollAnimation:
                fStepScroll(piSide);
                break;

            case NodAnimation:
                fStepNod(piSide);
                break;

            case CoverAnimation:
                fStepCover(piSide);
                break;
        }
    }

    private int fFirst(int piSide) => msFirstFrame[miCurrent[piSide] - 1] + (piSide * RightImageOffset);

    private int fLast(int piSide) => msLastFrame[miCurrent[piSide] - 1] + (piSide * RightImageOffset);

    private void fBegin(int piSide, int piLoops)
    {
        miPending[piSide] = 0;
        miCounter[piSide] = fFirst(piSide);
        miLoop[piSide] = piLoops;

        RectTransform lORect = (RectTransform)mOOverlay[piSide].transform;
        Vector2Int lOCorner = msOverlayPosition[piSide, miCurrent[piSide] - 1];
        lORect.anchoredPosition = new Vector2(lOCorner.x, -lOCorner.y);
    }

    private void fEnd(int piSide)
    {
        fSetEnabled(mOOverlay[piSide], false);
        miCurrent[piSide] = 0;
        miPhase[piSide] = 0;
    }

    /// <summary>Scroll: P1 start, P3 forward loop, P4 last pass, P5 hide.</summary>
    private void fStepScroll(int piSide)
    {
        switch (miPhase[piSide])
        {
            case 1:
                fBegin(piSide, 1);
                miPhase[piSide] = 3;
                goto case 3;

            case 3:
                fShow(mOOverlay[piSide], miCounter[piSide]++);

                if (miCounter[piSide] > fLast(piSide))
                {
                    miCounter[piSide] = fFirst(piSide);
                    miLoop[piSide]--;
                }

                if (miPending[piSide] != 0 || miLoop[piSide] == 0)
                    miPhase[piSide] = 4;
                break;

            case 4:
                fShow(mOOverlay[piSide], miCounter[piSide]++);

                if (miCounter[piSide] > fLast(piSide))
                    miPhase[piSide] = 5;
                break;

            case 5:
                fEnd(piSide);
                break;
        }
    }

    /// <summary>Nod: P2 lead-in, P3 loop over the last two frames, P4 backward, P5 hide.</summary>
    private void fStepNod(int piSide)
    {
        switch (miPhase[piSide])
        {
            case 1:
                fBegin(piSide, 3);
                miPhase[piSide] = 2;
                goto case 2;

            case 2:
                fShow(mOOverlay[piSide], miCounter[piSide]++);

                if (miCounter[piSide] > fFirst(piSide) + 1)
                    miPhase[piSide] = 3;
                break;

            case 3:
                fShow(mOOverlay[piSide], miCounter[piSide]++);

                if (miCounter[piSide] > fLast(piSide))
                {
                    miCounter[piSide] = fFirst(piSide) + 2;
                    miLoop[piSide]--;
                }

                if (miPending[piSide] != 0 || miLoop[piSide] == 0)
                    miPhase[piSide] = 4;
                break;

            case 4:
                fShow(mOOverlay[piSide], miCounter[piSide]--);

                if (miCounter[piSide] < fFirst(piSide))
                    miPhase[piSide] = 5;
                break;

            case 5:
                fEnd(piSide);
                break;
        }
    }

    /// <summary>Cover: P1 hide the head, P2 forward, P3 hold, P4 backward, P5 head back.</summary>
    private void fStepCover(int piSide)
    {
        switch (miPhase[piSide])
        {
            case 1:
                fSetEnabled(mOHead[piSide], false);
                fBegin(piSide, 6);
                miPhase[piSide] = 2;
                goto case 2;

            case 2:
                fShow(mOOverlay[piSide], miCounter[piSide]++);

                if (miCounter[piSide] > fLast(piSide))
                {
                    miCounter[piSide]--;
                    miPhase[piSide] = 3;
                }
                break;

            case 3:
                if (miPending[piSide] != 0 || --miLoop[piSide] == 0)
                    miPhase[piSide] = 4;
                break;

            case 4:
                fShow(mOOverlay[piSide], --miCounter[piSide]);

                if (miCounter[piSide] == fFirst(piSide))
                    miPhase[piSide] = 5;
                break;

            case 5:
                fShow(mOHead[piSide], HeadImage + (piSide * RightImageOffset));
                fEnd(piSide);
                break;
        }
    }

    private Image fCreate(string psName, RectTransform pOParent, Vector2Int pOCorner, int piImage)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Image));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);
        lORect.anchoredPosition = new Vector2(pOCorner.x, -pOCorner.y);

        Image lOImage = lOObject.GetComponent<Image>();
        lOImage.raycastTarget = false;

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
