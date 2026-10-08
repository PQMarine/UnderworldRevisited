using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// SPLITTING A STACK in the modern bags (stage 2b, per user 2026-10-04): Shift and the left button
/// on a stack - in a bag window, the backpack or a paperdoll slot - open a small leather box above
/// it, as WoW does, instead of the original's "Move how many?" in the message scroll:
///
///   - it starts at 1; digits type a number (the first one replaces the 1, more than the stack
///     becomes the stack), Backspace takes one back; - and + (the buttons, the arrow keys up and
///     down, the mouse wheel over the box) count one down and up;
///   - Enter or OK takes that many onto the pointer (UWInventoryModel.TakeFromStack: the rest
///     stays in its slot); all of them is the same as a plain click. Escape, the right button or
///     a click beside the box cancel;
///   - while it is open the keys belong to it (UWGameUI.IsTextEntryActive), as while typing a
///     name - the action bar's 1 to 0 among them;
///   - what was taken goes back onto its stack when the bags put the pointer's thing back
///     (fReturnCursorItem), else wherever there is room.
/// </summary>
public partial class UWModernBags
{
    /// <summary>The box in original pixels: the leather frame, - and + as round buttons, the
    /// number between them, OK at the right.</summary>
    private const int SplitWidth = 70;

    private const int SplitHeight = 19;

    private const int SplitButton = 9;

    private const int SplitMinusX = 5;

    private const int SplitBoxX = 16;

    private const int SplitBoxWidth = 26;

    private const int SplitPlusX = 44;

    private const int SplitOkX = 55;

    private const int SplitOkWidth = 12;

    private const int SplitRow = 5;

    /// <summary>A count above this is no count (UWItemDrag.fGetPendingStackCount, the original's rule).</summary>
    private const int MaxStackCount = 511;

    private static readonly Key[] msDigitKeys =
    {
        Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
        Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };

    private static readonly Key[] msNumpadKeys =
    {
        Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4,
        Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9
    };

    private static readonly Color msSplitBoxFill = new Color(0.10f, 0.06f, 0.03f, 0.85f);

    private static readonly Color msSplitOkHover = new Color(1f, 0.92f, 0.69f, 1f);

    /// <summary>The stack being split; null while no box is open.</summary>
    private UWObject mOSplitItem;

    /// <summary>Where it lies: a window's slot, or (window null) a paperdoll slot.</summary>
    private Window mOSplitWindow;

    private int miSplitSlot = -1;

    private UWArmorItemMap.BodySlot meSplitEquip;

    /// <summary>The screen spot the box sits above (a paperdoll slot: where the click was).</summary>
    private Rect mOSplitAnchor;

    private string msSplitInput = string.Empty;

    private bool mbSplitFirstKey;

    /// <summary>The frame the box closed: its keys and its click are still its own then.</summary>
    private int miSplitClosedFrame = -1;

    /// <summary>The stack a part was taken from, for putting it back.</summary>
    private UWObject mOSplitOrigin;

    private Rect mOSplitRect;

    private Rect mOSplitMinusRect;

    private Rect mOSplitPlusRect;

    private Rect mOSplitOkRect;

    private RectTransform mOSplitRoot;

    private RawImage mOSplitBack;

    private Texture2D mOSplitBackTexture;

    private int miSplitArtVersion = -1;

    private Image mOSplitBox;

    private Text mOSplitNumber;

    private RawImage mOSplitMinus;

    private RawImage mOSplitPlus;

    private Text mOSplitMinusText;

    private Text mOSplitPlusText;

    private Text mOSplitOk;

    /// <summary>Whether the split box has the keys (UWGameUI.IsTextEntryActive) - also in the frame
    /// it closed, so its Escape or Enter does nothing more.</summary>
    public static bool IsSplitting => Instance != null
        && (Instance.mOSplitItem != null || Instance.miSplitClosedFrame == Time.frameCount);

    private static int fStackCount(UWObject pOItem)
    {
        return pOItem != null && pOItem.HasQuantity && pOItem.Quantity > 1 && pOItem.Quantity <= MaxStackCount
            ? pOItem.Quantity : 1;
    }

    /// <summary>Shift and the left button on a thing (or the context menu's Split, pbForce): a stack
    /// opens the box (true), anything else is taken as by a click.</summary>
    private bool fTryBeginSplit(UWObject pOItem, Window pOWindow, int piSlot, UWArmorItemMap.BodySlot peEquip, Rect pOAnchor,
        bool pbForce = false)
    {
        if ((!pbForce && !UWControls.IsShiftHeld) || fStackCount(pOItem) < 2)
            return false;

        mOSplitItem = pOItem;
        mOSplitWindow = pOWindow;
        miSplitSlot = piSlot;
        meSplitEquip = peEquip;
        mOSplitAnchor = pOAnchor;
        msSplitInput = "1";
        mbSplitFirstKey = true;
        mbPressDrag = false;

        // The keys are the box's from now on, not only from UWGameUI's next look.
        UWControls.SetTextEntryActive(true);

        return true;
    }

    /// <summary>The thing lying where the box was opened now.</summary>
    private UWObject fSplitSource()
    {
        UWInventory lOInventory = fInventory();

        if (lOInventory == null)
            return null;

        if (mOSplitWindow == null)
            return lOInventory.GetEquipped(meSplitEquip);

        return mOSplitWindow.Root.gameObject.activeSelf ? fGetItem(mOSplitWindow, miSplitSlot) : null;
    }

    private void fEndSplit()
    {
        if (mOSplitItem == null)
            return;

        mOSplitItem = null;
        mOSplitWindow = null;
        miSplitClosedFrame = Time.frameCount;
    }

    private int fSplitValue()
    {
        return int.TryParse(msSplitInput, out int liValue) ? liValue : 0;
    }

    private void fStepSplit(int piStep)
    {
        msSplitInput = Mathf.Clamp(fSplitValue() + piStep, 1, fStackCount(mOSplitItem)).ToString();
        mbSplitFirstKey = true;
    }

    /// <summary>The gamepad's held direction on the box, when it steps next, and since when.</summary>
    private int miSplitPadDirection;

    private float mfSplitPadNext;

    private float mfSplitPadSince;

    /// <summary>
    /// THE GAMEPAD ON THE SPLIT BOX (per user, 2026-10-08: the box lies across, minus left and plus
    /// right - so left and right, not up and down): the d-pad's left and right or the walk stick
    /// pushed sideways step the count, held faster and after two seconds by ten; A takes that many,
    /// B lets the box go. The buttons by their place on the pad; the press is used up, so the pad's
    /// pointer does not click with it (UWGamepad.ConsumePress). True when the box closed.
    /// </summary>
    private bool fUpdateSplitPad()
    {
        Gamepad lOPad = Gamepad.current;

        if (lOPad == null)
            return false;

        if (lOPad.buttonSouth.wasPressedThisFrame)
        {
            UWGamepad.ConsumePress();

            if (fSplitValue() > 0)
                fConfirmSplit(fSplitValue());
            else
                fEndSplit();

            return true;
        }

        if (lOPad.buttonEast.wasPressedThisFrame)
        {
            UWGamepad.ConsumePress();
            fEndSplit();
            return true;
        }

        float lfStick = (UWUserSettings.GamepadSwapSticks ? lOPad.rightStick : lOPad.leftStick).ReadValue().x;
        int liDirection = lOPad.dpad.right.isPressed ? 1 : lOPad.dpad.left.isPressed ? -1
            : Mathf.Abs(lfStick) >= 0.5f ? (lfStick > 0f ? 1 : -1) : 0;

        if (liDirection == 0)
        {
            miSplitPadDirection = 0;
            return false;
        }

        float lfNow = Time.unscaledTime;

        if (liDirection != miSplitPadDirection)
        {
            miSplitPadDirection = liDirection;
            mfSplitPadSince = lfNow;
            mfSplitPadNext = lfNow + 0.35f;
        }
        else if (lfNow < mfSplitPadNext)
        {
            return false;
        }
        else
        {
            mfSplitPadNext = lfNow + 0.07f;
        }

        fStepSplit(liDirection * (lfNow - mfSplitPadSince > 2f ? 10 : 1));

        return false;
    }

    /// <summary>Keys and clicks while the box is open; called by Update instead of the bags' own.</summary>
    private void fUpdateSplit(UWControls pOControls)
    {
        // The stack moved or changed meanwhile: nothing to split any more.
        if (fSplitSource() != mOSplitItem || fStackCount(mOSplitItem) < 2)
        {
            fEndSplit();
            return;
        }

        if (fUpdateSplitPad())
            return;

        int liMax = fStackCount(mOSplitItem);
        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard != null)
        {
            if (lOKeyboard.enterKey.wasPressedThisFrame || lOKeyboard.numpadEnterKey.wasPressedThisFrame)
            {
                if (fSplitValue() > 0)
                    fConfirmSplit(fSplitValue());
                else
                    fEndSplit();

                return;
            }

            if (lOKeyboard.escapeKey.wasPressedThisFrame)
            {
                fEndSplit();
                return;
            }

            if (lOKeyboard.upArrowKey.wasPressedThisFrame || lOKeyboard.numpadPlusKey.wasPressedThisFrame)
                fStepSplit(1);
            else if (lOKeyboard.downArrowKey.wasPressedThisFrame || lOKeyboard.numpadMinusKey.wasPressedThisFrame)
                fStepSplit(-1);

            if (lOKeyboard.backspaceKey.wasPressedThisFrame && msSplitInput.Length > 0)
            {
                msSplitInput = msSplitInput.Substring(0, msSplitInput.Length - 1);
                mbSplitFirstKey = false;
            }

            for (int liDigit = 0; liDigit <= 9; liDigit++)
            {
                if (!lOKeyboard[msDigitKeys[liDigit]].wasPressedThisFrame && !lOKeyboard[msNumpadKeys[liDigit]].wasPressedThisFrame)
                    continue;

                if (mbSplitFirstKey)
                    msSplitInput = string.Empty;

                mbSplitFirstKey = false;
                msSplitInput += liDigit;

                // No more than the stack holds.
                if (fSplitValue() > liMax)
                    msSplitInput = liMax.ToString();
            }
        }

        Mouse lOMouse = Mouse.current;

        if (lOMouse == null)
            return;

        Vector2 lOPointer = lOMouse.position.ReadValue();
        float lfWheel = lOMouse.scroll.ReadValue().y;

        if (Mathf.Abs(lfWheel) > 0.01f && mOSplitRect.Contains(lOPointer))
            fStepSplit(lfWheel > 0f ? 1 : -1);

        if (pOControls.Player.Interact.WasPressedThisFrame())
        {
            fEndSplit();
            return;
        }

        if (!pOControls.Player.CursorDrag.WasPressedThisFrame())
            return;

        if (mOSplitMinusRect.Contains(lOPointer))
            fStepSplit(-1);
        else if (mOSplitPlusRect.Contains(lOPointer))
            fStepSplit(1);
        else if (mOSplitOkRect.Contains(lOPointer))
            fConfirmSplit(Mathf.Max(1, fSplitValue()));
        else if (!mOSplitRect.Contains(lOPointer))
            fEndSplit();
    }

    /// <summary>Takes piCount onto the pointer; the whole stack as a plain click takes it.</summary>
    private void fConfirmSplit(int piCount)
    {
        UWObject lOItem = mOSplitItem;
        Window lOWindow = mOSplitWindow;
        int liSlot = miSplitSlot;
        UWArmorItemMap.BodySlot leEquip = meSplitEquip;
        UWInventory lOInventory = fInventory();

        fEndSplit();

        if (lOInventory == null || lOInventory.CursorItem != null)
            return;

        int liTotal = fStackCount(lOItem);
        int liCount = Mathf.Clamp(piCount, 1, liTotal);

        if (liCount >= liTotal)
        {
            if (lOWindow != null)
                fTake(lOWindow, liSlot, lOInventory);
            else
                fTakeEquipped(leEquip, lOInventory);
        }
        else if (lOInventory.TakeFromStack(lOItem, liCount))
        {
            fForgetOrigin();
            mOSplitOrigin = lOItem;
        }

        // It stays on the pointer until the next click puts it down.
        mbPressDrag = false;
    }

    /// <summary>The bags put the pointer's thing back: a part goes onto the stack it came from
    /// while that is still carried.</summary>
    private bool fReturnToSplitOrigin(UWInventory pOInventory)
    {
        return mOSplitOrigin != null && fIsCarried(mOSplitOrigin) && pOInventory.TryStackInto(mOSplitOrigin);
    }

    // ------------------------------------------------- Drawing

    private void fLayoutSplit()
    {
        if (mOSplitRoot == null)
            fBuildSplit();

        bool lbOpen = mOSplitItem != null;

        mOSplitRoot.gameObject.SetActive(lbOpen);

        if (!lbOpen)
        {
            mOSplitRect = Rect.zero;
            mOSplitMinusRect = Rect.zero;
            mOSplitPlusRect = Rect.zero;
            mOSplitOkRect = Rect.zero;
            return;
        }

        if (mOSplitBackTexture == null || miSplitArtVersion != UWColourVision.Version)
        {
            if (mOSplitBackTexture != null)
                Destroy(mOSplitBackTexture);

            mOSplitBackTexture = UWModernHudArt.BuildLeather(fData().Textures, SplitWidth, SplitHeight, mOUi.TextureFilterMode);
            miSplitArtVersion = UWColourVision.Version;
            mOSplitBack.texture = mOSplitBackTexture;
        }

        // Over everything of the bags, the tooltip hidden.
        mOSplitRoot.SetAsLastSibling();
        mOTooltip.enabled = false;
        mOTooltipBack.enabled = false;

        float liScale = miScale;
        float lfWidth = SplitWidth * liScale;
        float lfHeight = SplitHeight * liScale;
        Rect lOAnchor = mOSplitWindow != null && miSplitSlot >= 0 && miSplitSlot < mOSplitWindow.Slots.Count
            ? mOSplitWindow.Slots[miSplitSlot].ScreenRect : mOSplitAnchor;

        // Above the stack, below it where the screen ends.
        float lfX = Mathf.Clamp(Mathf.Round(lOAnchor.center.x - (lfWidth * 0.5f)), 0f, Screen.width - lfWidth);
        float lfY = Mathf.Round(lOAnchor.yMax + (2 * liScale));

        if (lfY + lfHeight > Screen.height)
            lfY = Mathf.Round(lOAnchor.yMin - (2 * liScale) - lfHeight);

        mOSplitRect = new Rect(lfX, lfY, lfWidth, lfHeight);
        fSetRect(mOSplitBack.rectTransform, lfX, lfY, lfWidth, lfHeight);

        float lfRowY = lfY + (SplitRow * liScale);
        float lfButton = SplitButton * liScale;
        Vector2 lOPointer = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);

        mOSplitMinusRect = new Rect(lfX + (SplitMinusX * liScale), lfRowY, lfButton, lfButton);
        mOSplitPlusRect = new Rect(lfX + (SplitPlusX * liScale), lfRowY, lfButton, lfButton);
        mOSplitOkRect = new Rect(lfX + (SplitOkX * liScale), lfRowY, SplitOkWidth * liScale, lfButton);

        fLayoutSplitButton(mOSplitMinus, mOSplitMinusText, mOSplitMinusRect, lOPointer);
        fLayoutSplitButton(mOSplitPlus, mOSplitPlusText, mOSplitPlusRect, lOPointer);

        fSetRect(mOSplitBox.rectTransform, lfX + (SplitBoxX * liScale), lfRowY, SplitBoxWidth * liScale, lfButton);

        mOSplitNumber.text = msSplitInput + "<color=#b8a68a> / " + fStackCount(mOSplitItem) + "</color>";
        mOSplitNumber.fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * liScale));
        fSetRect(mOSplitNumber.rectTransform, lfX + (SplitBoxX * liScale), lfRowY + (0.5f * liScale), SplitBoxWidth * liScale, lfButton);

        mOSplitOk.color = mOSplitOkRect.Contains(lOPointer) ? msSplitOkHover : msGold;
        mOSplitOk.fontSize = Mathf.Max(9, Mathf.RoundToInt(4.4f * liScale));
        fSetRect(mOSplitOk.rectTransform, mOSplitOkRect.x, mOSplitOkRect.y + (0.5f * liScale), mOSplitOkRect.width, mOSplitOkRect.height);
    }

    private void fLayoutSplitButton(RawImage pODisc, Text pOText, Rect pORect, Vector2 pOPointer)
    {
        pODisc.texture = pORect.Contains(pOPointer) ? mODiscHoverTexture : mODiscTexture;
        pODisc.enabled = true;
        fSetRect(pODisc.rectTransform, pORect.x, pORect.y, pORect.width, pORect.height);

        pOText.fontSize = Mathf.Max(10, Mathf.RoundToInt(5.5f * miScale));
        fSetRect(pOText.rectTransform, pORect.x, pORect.y + (0.3f * miScale), pORect.width, pORect.height);
    }

    private void fBuildSplit()
    {
        GameObject lOObject = new GameObject("Split stack", typeof(RectTransform));
        lOObject.transform.SetParent(mORoot, false);

        mOSplitRoot = (RectTransform)lOObject.transform;
        mOSplitRoot.anchorMin = Vector2.zero;
        mOSplitRoot.anchorMax = Vector2.zero;
        mOSplitRoot.pivot = Vector2.zero;

        mOSplitBack = fCreateRawImage(mOSplitRoot, "Leather");
        mOSplitBack.enabled = true;
        UWPixelArtUI.Apply(mOSplitBack);

        mOSplitBox = fCreateImage(mOSplitRoot, "Number box", msSplitBoxFill);

        mOSplitNumber = fCreateText(mOSplitRoot, "Number", TextAnchor.MiddleCenter);
        mOSplitNumber.color = new Color(0.94f, 0.87f, 0.71f);
        mOSplitNumber.supportRichText = true;

        mOSplitMinus = fCreateRawImage(mOSplitRoot, "Minus");
        mOSplitMinusText = fCreateText(mOSplitRoot, "Minus text", TextAnchor.MiddleCenter);
        mOSplitMinusText.text = "−";
        mOSplitMinusText.color = msSplitOkHover;

        mOSplitPlus = fCreateRawImage(mOSplitRoot, "Plus");
        mOSplitPlusText = fCreateText(mOSplitRoot, "Plus text", TextAnchor.MiddleCenter);
        mOSplitPlusText.text = "+";
        mOSplitPlusText.color = msSplitOkHover;

        mOSplitOk = fCreateText(mOSplitRoot, "OK", TextAnchor.MiddleCenter);
        mOSplitOk.text = "OK";
    }

    private void fDestroySplitTexture()
    {
        if (mOSplitBackTexture != null)
            Destroy(mOSplitBackTexture);

        mOSplitBackTexture = null;
    }
}
