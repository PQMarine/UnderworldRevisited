using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport.Presentation;
using UWDataImport.UWData;
using Window = UWModernBagPages.Window;

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
///   - while it is open the keys belong to it (UWTextEntryState.IsTextEntryActive), as while typing a
///     name - the action bar's 1 to 0 among them;
///   - what was taken goes back onto its stack when the bags put the pointer's thing back
///     (UWModernBagDrag.ReturnCursorItem), else wherever there is room.
///
/// A class of its own since cleanup 5.17 (2026-10-23; the partial UWModernBags.StackSplit.cs
/// until then), as it was.
/// </summary>
internal sealed class UWModernBagSplit
{
    public UWModernBagSplit(UWModernBags pOBags)
    {
        mOBags = pOBags;
    }

    private readonly UWModernBags mOBags;

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

    // The count of a stack and its cap are UWStackRules.CountAtLeastOne and
    // UWStackRules.MaxStackCount (the port's deliberate "stacks stop at 511") since 2026-10-11.

    /// <summary>The digits typed, by the key's place (UWTextEntry.Digits, cleanup step 7.1, 2026-10-25;
    /// the box read the digit row and the keypad itself until then, the same keys). No more than the
    /// stack holds (UWCountInput clamps while typing here).</summary>
    private UWTextEntry mOSplitDigits;

    private UWTextEntry fSplitDigits()
    {
        if (mOSplitDigits == null)
            mOSplitDigits = UWTextEntry.Digits(pcDigit => mOSplitInput.TypeDigit(pcDigit - '0'));

        return mOSplitDigits;
    }

    /// <summary>The stack being split; null while no box is open.</summary>
    private UWObject mOSplitItem;

    /// <summary>Where it lies: a window's slot, or (window null) a paperdoll slot.</summary>
    private Window mOSplitWindow;

    private int miSplitSlot = -1;

    private UWArmorItemMap.BodySlot meSplitEquip;

    /// <summary>The screen spot the box sits above (a paperdoll slot: where the click was).</summary>
    private Rect mOSplitAnchor;

    /// <summary>The count typed or stepped (UWCountInput with the box's policy since cleanup step
    /// 5.13, 2026-10-22: clamps at the ends and while typing, a digit after a step replaces, Enter
    /// on 0 lets the box go - as the box did in its own fields until then).</summary>
    private readonly UWCountInput mOSplitInput = new UWCountInput(UWCountInput.ModernSplitBox);

    /// <summary>The frame the box closed: its keys and its click are still its own then.</summary>
    private int miSplitClosedFrame = -1;

    private Rect mOSplitRect;

    private Rect mOSplitMinusRect;

    private Rect mOSplitPlusRect;

    private Rect mOSplitOkRect;

    private RectTransform mOSplitRoot;

    private RawImage mOSplitBack;

    private readonly UWVersionedTexture mOSplitBackArt = new UWVersionedTexture();

    private Image mOSplitBox;

    private Text mOSplitNumber;

    private RawImage mOSplitMinus;

    private RawImage mOSplitPlus;

    private Text mOSplitMinusText;

    private Text mOSplitPlusText;

    private Text mOSplitOk;

    /// <summary>A box is open (it runs instead of the bags' own clicks).</summary>
    internal bool IsOpen => mOSplitItem != null;

    /// <summary>Whether the split box has the keys (UWTextEntryState.IsTextEntryActive) - also in the frame
    /// it closed, so its Escape or Enter does nothing more (UWModernBags.IsSplitting).</summary>
    internal bool HasInput => mOSplitItem != null || miSplitClosedFrame == Time.frameCount;

    /// <summary>Where the open box lies on the screen, empty while none is open.</summary>
    internal Rect ScreenRect => mOSplitRect;

    /// <summary>Shift and the left button on a thing (or the context menu's Split, pbForce): a stack
    /// opens the box (true), anything else is taken as by a click.</summary>
    internal bool TryBegin(UWObject pOItem, Window pOWindow, int piSlot, UWArmorItemMap.BodySlot peEquip, Rect pOAnchor,
        bool pbForce = false)
    {
        if ((!pbForce && !UWControls.IsShiftHeld) || UWStackRules.CountAtLeastOne(pOItem) < 2)
            return false;

        mOSplitItem = pOItem;
        mOSplitWindow = pOWindow;
        miSplitSlot = piSlot;
        meSplitEquip = peEquip;
        mOSplitAnchor = pOAnchor;
        mOSplitInput.Begin(UWStackRules.CountAtLeastOne(pOItem));
        fSplitDigits().Begin();
        mOBags.Clicks.EndPress();

        // The keys are the box's from now on, not only from UWGameUI's next look.
        UWControls.SetTextEntryActive(true);

        return true;
    }

    /// <summary>The thing lying where the box was opened now.</summary>
    private UWObject fSplitSource()
    {
        UWInventory lOInventory = mOBags.Inventory;

        if (lOInventory == null)
            return null;

        if (mOSplitWindow == null)
            return lOInventory.GetEquipped(meSplitEquip);

        UWModernBagPages lOPages = mOBags.Pages;

        // THE ORIGINAL'S PAGE (Classic Wide, Classic Modular): window 0 stands in for its slots
        // while the bag windows stay shut - inactive, so the check below found nothing and the
        // box closed in the frame it opened (per user, 2026-10-22; the page's split never worked).
        if (UWModernBagPages.IsPageShown && mOSplitWindow == lOPages.GetWindow(0))
            return lOPages.GetItem(lOPages.PageWindow(), miSplitSlot);

        return mOSplitWindow.Root.gameObject.activeSelf ? lOPages.GetItem(mOSplitWindow, miSplitSlot) : null;
    }

    internal void End()
    {
        if (mOSplitItem == null)
            return;

        mOSplitItem = null;
        mOSplitWindow = null;
        miSplitClosedFrame = Time.frameCount;

        if (mOSplitInput.IsOpen)
            mOSplitInput.Cancel();

        fSplitDigits().End();
    }

    /// <summary>
    /// THE GAMEPAD ON THE SPLIT BOX (per user, 2026-10-08: the box lies across, minus left and plus
    /// right - so left and right, not up and down): the d-pad's left and right or the walk stick
    /// pushed sideways step the count, held faster and after two seconds by ten (the repeat is
    /// UWCountInput's, the same as the classic prompt's); A takes that many, B lets the box go.
    /// A and B as they are bound (cleanup step 7.6; by their place on the pad before), the d-pad by
    /// its place; the press is the box's, so the pad's pointer does not click with it (UWPadOwners asks
    /// UWModernBags.SplitTakesPad when the frame begins; UWGamepad.ConsumePress until cleanup step
    /// 7.8). True when the box closed.
    /// </summary>
    private bool fUpdateSplitPad()
    {
        Gamepad lOPad = Gamepad.current;

        if (lOPad == null)
            return false;

        // A and B as they are bound (PadClick, PadBack: their first binding, cleanup step 7.6 - by
        // their place until then); the d-pad and the stick by their place.
        if (UWInput.PadPressed(UWAct.PadClick, 0, "<Gamepad>/buttonSouth"))
        {
            fEnterSplit();

            return true;
        }

        if (UWBack.Takes(UWBackOwner.BagSplit, UWBackKey.PadButton))
        {
            End();
            return true;
        }

        float lfStick = (UWUserSettings.GamepadSwapSticks ? lOPad.rightStick : lOPad.leftStick).ReadValue().x;
        int liDirection = lOPad.dpad.right.isPressed ? 1 : lOPad.dpad.left.isPressed ? -1
            : Mathf.Abs(lfStick) >= 0.5f ? (lfStick > 0f ? 1 : -1) : 0;

        mOSplitInput.HoldDirection(liDirection, Time.unscaledTime);

        return false;
    }

    /// <summary>Enter or the pad's A: that many onto the pointer; with 0 or nothing typed the box
    /// goes (UWCountInput.Enter with the box's policy).</summary>
    private void fEnterSplit()
    {
        if (mOSplitInput.Enter())
            fConfirmSplit(mOSplitInput.TakenCount);
        else
            End();
    }

    /// <summary>Keys and clicks while the box is open; called by UWModernBags.Update instead of the bags' own.</summary>
    internal void Update(UWControls pOControls)
    {
        // The stack moved or changed meanwhile: nothing to split any more.
        if (fSplitSource() != mOSplitItem || UWStackRules.CountAtLeastOne(mOSplitItem) < 2)
        {
            End();
            return;
        }

        // The stack as it is now caps the count (it was read anew for every step and digit).
        mOSplitInput.Maximum = UWStackRules.CountAtLeastOne(mOSplitItem);

        if (fUpdateSplitPad())
            return;

        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard != null)
        {
            if (UWTextEntry.EnterPressed)
            {
                fEnterSplit();
                return;
            }

            if (UWBack.Takes(UWBackOwner.BagSplit, UWBackKey.Escape))
            {
                End();
                return;
            }

            if (lOKeyboard.upArrowKey.wasPressedThisFrame || lOKeyboard.numpadPlusKey.wasPressedThisFrame)
                mOSplitInput.Step(1);
            else if (lOKeyboard.downArrowKey.wasPressedThisFrame || lOKeyboard.numpadMinusKey.wasPressedThisFrame)
                mOSplitInput.Step(-1);

            if (UWTextEntry.BackspacePressed)
                mOSplitInput.Backspace();

            fSplitDigits().ReadKeys();
        }

        if (!UWPointer.HasMouse)
            return;

        Vector2 lOPointer = UWPointer.Position;
        float lfWheel = UWPointer.Scroll.y;

        if (Mathf.Abs(lfWheel) > 0.01f && mOSplitRect.Contains(lOPointer))
            mOSplitInput.Step(lfWheel > 0f ? 1 : -1);

        if (pOControls.Player.Interact.WasPressedThisFrame())
        {
            End();
            return;
        }

        if (!pOControls.Player.CursorDrag.WasPressedThisFrame())
            return;

        if (mOSplitMinusRect.Contains(lOPointer))
            mOSplitInput.Step(-1);
        else if (mOSplitPlusRect.Contains(lOPointer))
            mOSplitInput.Step(1);
        else if (mOSplitOkRect.Contains(lOPointer))
            fConfirmSplit(mOSplitInput.Commit(Mathf.Max(1, mOSplitInput.Value)));
        else if (!mOSplitRect.Contains(lOPointer))
            End();
    }

    /// <summary>Takes piCount onto the pointer; the whole stack as a plain click takes it.</summary>
    private void fConfirmSplit(int piCount)
    {
        UWObject lOItem = mOSplitItem;
        Window lOWindow = mOSplitWindow;
        int liSlot = miSplitSlot;
        UWArmorItemMap.BodySlot leEquip = meSplitEquip;
        UWInventory lOInventory = mOBags.Inventory;

        End();

        if (lOInventory == null || lOInventory.CursorItem != null)
            return;

        int liTotal = UWStackRules.CountAtLeastOne(lOItem);
        int liCount = Mathf.Clamp(piCount, 1, liTotal);

        if (liCount >= liTotal)
        {
            if (lOWindow != null)
                mOBags.Drag.Take(lOWindow, liSlot, lOInventory);
            else
                mOBags.Drag.TakeEquipped(leEquip, lOInventory);
        }
        else if (lOInventory.TakeFromStack(lOItem, liCount))
        {
            // The stack a part was taken from, for putting it back.
            mOBags.Drag.TookFromStack(lOItem);
        }

        // It stays on the pointer until the next click puts it down.
        mOBags.Clicks.EndPress();
    }

    // ------------------------------------------------- Drawing

    internal void Layout()
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

        UWArtStamp lOStamp = UWVersionedTexture.Colours();

        if (!mOSplitBackArt.IsCurrent(lOStamp))
            mOSplitBack.texture = mOSplitBackArt.Set(lOStamp, UWModernHudArt.BuildLeather(mOBags.Data.Textures, SplitWidth, SplitHeight, mOBags.Ui.TextureFilterMode));

        // Over everything of the bags, the tooltip hidden.
        mOSplitRoot.SetAsLastSibling();
        mOBags.View.HideTooltip();

        float liScale = mOBags.View.Scale;
        float lfWidth = SplitWidth * liScale;
        float lfHeight = SplitHeight * liScale;
        Rect lOAnchor = mOSplitWindow != null && miSplitSlot >= 0 && miSplitSlot < mOSplitWindow.Slots.Count
            ? mOBags.Pages.SlotRect(mOSplitWindow, miSplitSlot) : mOSplitAnchor;

        // Above the stack, below it where the screen ends.
        float lfX = Mathf.Clamp(Mathf.Round(lOAnchor.center.x - (lfWidth * 0.5f)), 0f, Screen.width - lfWidth);
        float lfY = Mathf.Round(lOAnchor.yMax + (2 * liScale));

        if (lfY + lfHeight > Screen.height)
            lfY = Mathf.Round(lOAnchor.yMin - (2 * liScale) - lfHeight);

        mOSplitRect = new Rect(lfX, lfY, lfWidth, lfHeight);
        UWUiKit.SetRect(mOSplitBack.rectTransform, lfX, lfY, lfWidth, lfHeight);

        float lfRowY = lfY + (SplitRow * liScale);
        float lfButton = SplitButton * liScale;
        Vector2 lOPointer = UWPointer.HasMouse ? UWPointer.Position : new Vector2(-1f, -1f);

        mOSplitMinusRect = new Rect(lfX + (SplitMinusX * liScale), lfRowY, lfButton, lfButton);
        mOSplitPlusRect = new Rect(lfX + (SplitPlusX * liScale), lfRowY, lfButton, lfButton);
        mOSplitOkRect = new Rect(lfX + (SplitOkX * liScale), lfRowY, SplitOkWidth * liScale, lfButton);

        fLayoutSplitButton(mOSplitMinus, mOSplitMinusText, mOSplitMinusRect, lOPointer);
        fLayoutSplitButton(mOSplitPlus, mOSplitPlusText, mOSplitPlusRect, lOPointer);

        UWUiKit.SetRect(mOSplitBox.rectTransform, lfX + (SplitBoxX * liScale), lfRowY, SplitBoxWidth * liScale, lfButton);

        mOSplitNumber.text = mOSplitInput.Text + "<color=#b8a68a> / " + UWStackRules.CountAtLeastOne(mOSplitItem) + "</color>";
        mOSplitNumber.fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * liScale));
        UWUiKit.SetRect(mOSplitNumber.rectTransform, lfX + (SplitBoxX * liScale), lfRowY + (0.5f * liScale), SplitBoxWidth * liScale, lfButton);

        mOSplitOk.color = mOSplitOkRect.Contains(lOPointer) ? UWModernPalette.LightText : UWModernPalette.Gold;
        mOSplitOk.fontSize = Mathf.Max(9, Mathf.RoundToInt(4.4f * liScale));
        UWUiKit.SetRect(mOSplitOk.rectTransform, mOSplitOkRect.x, mOSplitOkRect.y + (0.5f * liScale), mOSplitOkRect.width, mOSplitOkRect.height);
    }

    private void fLayoutSplitButton(RawImage pODisc, Text pOText, Rect pORect, Vector2 pOPointer)
    {
        float lfScale = mOBags.View.Scale;

        pODisc.texture = pORect.Contains(pOPointer) ? mOBags.View.DiscHoverTexture : mOBags.View.DiscTexture;
        pODisc.enabled = true;
        UWUiKit.SetRect(pODisc.rectTransform, pORect.x, pORect.y, pORect.width, pORect.height);

        pOText.fontSize = Mathf.Max(10, Mathf.RoundToInt(5.5f * lfScale));
        UWUiKit.SetRect(pOText.rectTransform, pORect.x, pORect.y + (0.3f * lfScale), pORect.width, pORect.height);
    }

    private void fBuildSplit()
    {
        UWGameUI lOUi = mOBags.Ui;

        mOSplitRoot = UWUiKit.CreateGroup(mOBags.View.Root, "Split stack");

        mOSplitBack = UWUiKit.CreateRawImage(mOSplitRoot, "Leather", true);
        UWPixelArtUI.Apply(mOSplitBack);

        mOSplitBox = UWUiKit.CreateImage(mOSplitRoot, "Number box", UWModernPalette.InputFill);

        mOSplitNumber = UWUiKit.CreateText(mOSplitRoot, "Number", TextAnchor.MiddleCenter, UWModernPalette.HudText, lOUi);
        mOSplitNumber.color = UWModernPalette.Text;
        mOSplitNumber.supportRichText = true;

        mOSplitMinus = UWUiKit.CreateRawImage(mOSplitRoot, "Minus");
        mOSplitMinusText = UWUiKit.CreateText(mOSplitRoot, "Minus text", TextAnchor.MiddleCenter, UWModernPalette.HudText, lOUi);
        mOSplitMinusText.text = "−";
        mOSplitMinusText.color = UWModernPalette.LightText;

        mOSplitPlus = UWUiKit.CreateRawImage(mOSplitRoot, "Plus");
        mOSplitPlusText = UWUiKit.CreateText(mOSplitRoot, "Plus text", TextAnchor.MiddleCenter, UWModernPalette.HudText, lOUi);
        mOSplitPlusText.text = "+";
        mOSplitPlusText.color = UWModernPalette.LightText;

        mOSplitOk = UWUiKit.CreateText(mOSplitRoot, "OK", TextAnchor.MiddleCenter, UWModernPalette.HudText, lOUi);
        mOSplitOk.text = UWText.T("hud.split.ok");
    }

    internal void Release()
    {
        mOSplitBackArt.Release();
    }
}
