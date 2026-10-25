using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport.UWData;
using Window = UWModernBagPages.Window;

/// <summary>
/// THE BAGS' CONTEXT MENU (per user, 2026-10-04: the right button used at once and Shift with it
/// looked - not liked): the right button on a thing in a bag window or on the paperdoll opens a
/// small leather menu at the pointer.
///
///   - First what the thing is for: a container OPEN or CLOSE (the rune bag opens the rune panel),
///     armour, rings and weapons in a bag EQUIP, on the paperdoll USE (what is not worn as such)
///     and TAKE OFF, anything else USE - at once, or the use mode for what goes onto something
///     else (UWCarriedItemUse);
///   - then LOOK, PICK UP (onto the pointer, as a click), SPLIT for a stack (the split box) and
///     CANCEL.
///
/// The left button chooses; the right button, Escape or a click beside it close it. While it is
/// open the keys are its own and its clicks do nothing else (IsMenuOpen, as the split box).
///
/// A class of its own since cleanup 5.17 (2026-10-23; the partial UWModernBags.ContextMenu.cs
/// until then), as it was.
/// </summary>
internal sealed class UWModernBagMenu
{
    public UWModernBagMenu(UWModernBags pOBags)
    {
        mOBags = pOBags;
    }

    private readonly UWModernBags mOBags;

    private enum MenuActionEnum
    {
        Use,
        Open,
        Close,
        OpenRunes,
        Equip,
        TakeOff,
        Look,
        PickUp,
        Split,
        Cancel,
        Talk,
        WorldUse,
        WorldLook,
        WorldPickUp
    }

    /// <summary>The ids of the entries' texts in the text table (UWText, cleanup step 8.5, 2026-10-24).</summary>
    private static readonly Dictionary<MenuActionEnum, string> msMenuLabels = new Dictionary<MenuActionEnum, string>
    {
        { MenuActionEnum.Use, "hud.menu.use" },
        { MenuActionEnum.Open, "hud.menu.open" },
        { MenuActionEnum.Close, "hud.menu.close" },
        { MenuActionEnum.OpenRunes, "hud.menu.open_runes" },
        { MenuActionEnum.Equip, "hud.menu.equip" },
        { MenuActionEnum.TakeOff, "hud.menu.take_off" },
        { MenuActionEnum.Look, "hud.menu.look" },
        { MenuActionEnum.PickUp, "hud.menu.pick_up" },
        { MenuActionEnum.Split, "hud.menu.split" },
        { MenuActionEnum.Cancel, "hud.menu.cancel" },
        { MenuActionEnum.Talk, "hud.menu.talk" },
        { MenuActionEnum.WorldUse, "hud.menu.world_use" },
        { MenuActionEnum.WorldLook, "hud.menu.world_look" },
        { MenuActionEnum.WorldPickUp, "hud.menu.world_pick_up" }
    };

    /// <summary>The menu in original pixels.</summary>
    private const int MenuWidth = 52;

    private const int MenuRow = 8;

    private const int MenuPad = 4;

    /// <summary>The thing the menu is for; null while none is open.</summary>
    private UWObject mOMenuItem;

    /// <summary>Where it lies: a window's slot, or (window null) a paperdoll slot.</summary>
    private Window mOMenuWindow;

    private int miMenuSlot = -1;

    private UWArmorItemMap.BodySlot meMenuEquip;

    private Vector2 mOMenuAt;

    private int miMenuOpenedFrame = -1;

    /// <summary>The menu is for a thing in the world (OpenWorldMenu), not in a slot.</summary>
    private bool mbMenuWorld;

    private UWEntityInfo mOMenuEntity;

    private UWEntityInfo mOMenuPickable;

    private int miMenuClosedFrame = -1;

    private readonly List<MenuActionEnum> mOMenuActions = new List<MenuActionEnum>();

    private readonly List<Rect> mOMenuRects = new List<Rect>();

    private Rect mOMenuRect;

    private RectTransform mOMenuRoot;

    private RawImage mOMenuBack;

    private readonly UWVersionedTexture mOMenuBackArt = new UWVersionedTexture();

    private Image mOMenuHighlight;

    private readonly List<Text> mOMenuTexts = new List<Text>();

    /// <summary>A menu is open (its thing taken; it runs instead of the bags' own clicks).</summary>
    internal bool IsOpen => mOMenuItem != null;

    /// <summary>The menu has the keys and the mouse (UWTextEntryState.IsTextEntryActive, UWModernPointer) -
    /// also in the frame it closed, so its click and its right button do nothing more
    /// (UWModernBags.IsMenuOpen).</summary>
    internal bool HasInput => mOMenuItem != null || miMenuClosedFrame == Time.frameCount;

    /// <summary>Where the open menu lies on the screen, empty while none is open.</summary>
    internal Rect ScreenRect => mOMenuRect;

    internal void Open(UWObject pOItem, Window pOWindow, int piSlot, UWArmorItemMap.BodySlot peEquip, Vector2 pOAt)
    {
        UWInventory lOInventory = mOBags.Inventory;

        mOMenuActions.Clear();

        bool lbWorn = UWModernBagDrag.TryGetEquipSlot(pOItem, lOInventory, out UWArmorItemMap.BodySlot _);

        if (pOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
        {
            if (pOItem.ID == UWObjectMechanics.RuneBagId)
                mOMenuActions.Add(MenuActionEnum.OpenRunes);
            else
                mOMenuActions.Add(mOBags.Pages.IsBagOpen(pOItem, lOInventory) ? MenuActionEnum.Close : MenuActionEnum.Open);
        }
        else if (pOWindow == null)
        {
            if (!lbWorn)
                mOMenuActions.Add(MenuActionEnum.Use);
        }
        else
        {
            mOMenuActions.Add(lbWorn ? MenuActionEnum.Equip : MenuActionEnum.Use);
        }

        if (pOWindow == null)
            mOMenuActions.Add(MenuActionEnum.TakeOff);

        mOMenuActions.Add(MenuActionEnum.Look);
        mOMenuActions.Add(MenuActionEnum.PickUp);

        if (UWStackRules.CountAtLeastOne(pOItem) >= 2)
            mOMenuActions.Add(MenuActionEnum.Split);

        mOMenuActions.Add(MenuActionEnum.Cancel);

        // In a conversation nothing is used, as in the original (the rune panel is hidden there too).
        if (UWModernBagTrade.IsTalking)
        {
            mOMenuActions.Remove(MenuActionEnum.Use);
            mOMenuActions.Remove(MenuActionEnum.OpenRunes);
        }

        mOMenuItem = pOItem;
        mOMenuWindow = pOWindow;
        miMenuSlot = piSlot;
        meMenuEquip = peEquip;
        mOMenuAt = pOAt;
        miMenuOpenedFrame = Time.frameCount;
        mOBags.Clicks.EndPress();
        mOMenuStepper.Opened();

        // The keys are the menu's from now on, not only from UWGameUI's next look.
        UWControls.SetTextEntryActive(true);
    }

    internal void Close()
    {
        if (mOMenuItem == null)
            return;

        mOMenuItem = null;
        mOMenuWindow = null;
        mbMenuWorld = false;
        mOMenuEntity = null;
        mOMenuPickable = null;
        miMenuClosedFrame = Time.frameCount;
    }

    /// <summary>
    /// THE MENU OF A THING IN THE WORLD (per user, 2026-10-04): a left click on it with the free
    /// pointer and the weapon put away (Interaction.TryOpenWorldMenu) - a creature TALK, anything else USE (the direct use,
    /// as E held), PICK UP for what can be taken (onto the pointer), LOOK, CANCEL.
    /// </summary>
    internal void OpenWorld(UWEntityInfo pOTarget, UWEntityInfo pOPickableBehind, bool pbCreature, bool pbPickable, Vector2 pOAt)
    {
        if (pOTarget == null || pOTarget.ObjectData == null)
            return;

        mOMenuActions.Clear();
        mOMenuActions.Add(pbCreature ? MenuActionEnum.Talk : MenuActionEnum.WorldUse);

        if (pbPickable)
            mOMenuActions.Add(MenuActionEnum.WorldPickUp);

        mOMenuActions.Add(MenuActionEnum.WorldLook);
        mOMenuActions.Add(MenuActionEnum.Cancel);

        mOMenuItem = pOTarget.ObjectData;
        mOMenuWindow = null;
        miMenuSlot = -1;
        mbMenuWorld = true;
        mOMenuEntity = pOTarget;
        mOMenuPickable = pOPickableBehind;
        mOMenuAt = pOAt;
        miMenuOpenedFrame = Time.frameCount;
        mOBags.Clicks.EndPress();
        mOMenuStepper.Opened();

        UWControls.SetTextEntryActive(true);
    }

    /// <summary>The thing lying where the menu was opened now.</summary>
    private UWObject fMenuSource()
    {
        UWInventory lOInventory = mOBags.Inventory;

        if (lOInventory == null)
            return null;

        // A thing in the world: as long as it is there.
        if (mbMenuWorld)
            return mOMenuEntity != null ? mOMenuEntity.ObjectData : null;

        if (mOMenuWindow == null)
            return lOInventory.GetEquipped(meMenuEquip);

        UWModernBagPages lOPages = mOBags.Pages;

        // The original page's slots: window 0 stands in for them, never shown (per user,
        // 2026-10-10: the menu there closed again at once).
        if (UWModernBagPages.IsPageShown && mOMenuWindow == lOPages.GetWindow(0))
            return lOPages.GetItem(lOPages.PageWindow(), miMenuSlot);

        return mOMenuWindow.Root.gameObject.activeSelf ? lOPages.GetItem(mOMenuWindow, miMenuSlot) : null;
    }

    /// <summary>Keys and clicks while the menu is open; called by UWModernBags.Update instead of the bags' own.</summary>
    internal void Update(UWControls pOControls)
    {
        if (fMenuSource() != mOMenuItem)
        {
            Close();
            return;
        }

        if (Keyboard.current != null && UWBack.Takes(UWBackOwner.BagMenu, UWBackKey.Escape))
        {
            Close();
            return;
        }

        if (!UWPointer.HasMouse || Time.frameCount == miMenuOpenedFrame)
            return;

        // THE GAMEPAD (per user, 2026-10-08: the d-pad moves through buttons and options): B closes
        // the menu, up and down put the pointer onto the next entry - A then chooses it with the
        // pad's click, the highlight follows as with the mouse.
        if (UWBack.Takes(UWBackOwner.BagMenu, UWBackKey.PadBack))
        {
            Close();
            return;
        }

        mOMenuStepper.Update(mOMenuRects);

        if (pOControls.Player.Interact.WasPressedThisFrame())
        {
            Close();
            return;
        }

        if (!pOControls.Player.CursorDrag.WasPressedThisFrame())
            return;

        Vector2 lOPointer = UWPointer.Position;

        for (int liAt = 0; liAt < mOMenuRects.Count && liAt < mOMenuActions.Count; liAt++)
        {
            if (mOMenuRects[liAt].Contains(lOPointer))
            {
                fRunMenu(mOMenuActions[liAt]);
                return;
            }
        }

        Close();
    }

    /// <summary>The gamepad steps through the entries (UWPadEntryStepper, per user 2026-10-08).</summary>
    private readonly UWPadEntryStepper mOMenuStepper = new UWPadEntryStepper();

    private void fRunMenu(MenuActionEnum peAction)
    {
        UWObject lOItem = mOMenuItem;
        Window lOWindow = mOMenuWindow;
        int liSlot = miMenuSlot;
        UWArmorItemMap.BodySlot leEquip = meMenuEquip;
        Vector2 lOAt = mOMenuAt;
        UWEntityInfo lOEntity = mOMenuEntity;
        UWEntityInfo lOPickable = mOMenuPickable;
        UWInventory lOInventory = mOBags.Inventory;
        UWItemDrag lODrag = UWScene.ItemDrag;
        Interaction lOInteraction = mOBags.Interaction;

        Close();

        if (lOInventory == null || lOInteraction == null)
            return;

        switch (peAction)
        {
            case MenuActionEnum.Talk:
            case MenuActionEnum.WorldUse:
                if (lOEntity != null)
                    lOInteraction.ModernUseThing(lOEntity, lOPickable);

                break;

            case MenuActionEnum.WorldLook:
                if (lOEntity != null)
                    lOInteraction.LookAtTarget(lOEntity, null);

                break;

            case MenuActionEnum.WorldPickUp:
                if (lOEntity != null && UWWorldPickup.Take(lOInteraction, lOEntity, UWWorldPickup.WholeStack,
                    UWWorldPickup.Destination.Pointer))
                {
                    if (!mOBags.IsOpen)
                        mOBags.Open();

                    mOBags.Clicks.EndPress();
                }

                break;

            case MenuActionEnum.Use:
                UWCarriedItemUse.Use(lODrag, lOItem, UWCarriedItemUse.Context.ModernMenu);
                break;

            case MenuActionEnum.Open:
            case MenuActionEnum.Close:
                mOBags.OpenBags.Toggle(lOItem);
                break;

            case MenuActionEnum.OpenRunes:
                if (UWModernRunePanel.Instance != null)
                    UWModernRunePanel.Instance.Open(UWModernRunePanel.TabEnum.Runes);

                break;

            case MenuActionEnum.Equip:
                if (lOWindow != null && UWModernBagDrag.TryGetEquipSlot(lOItem, lOInventory, out UWArmorItemMap.BodySlot leSlot))
                    mOBags.Drag.Equip(lOWindow, liSlot, leSlot, lOInventory);

                break;

            case MenuActionEnum.TakeOff:
                if (lOInventory.TryUnequipAnywhere(leEquip))
                    lOInventory.ApplyCarriedLight();
                else
                    lOInteraction.AddGeneralMessage(UWStringIds.NoPlaceToPutMessage);

                break;

            case MenuActionEnum.Look:
                mOBags.Clicks.Look(lOItem);
                break;

            case MenuActionEnum.PickUp:
                if (lOWindow != null)
                    mOBags.Drag.Take(lOWindow, liSlot, lOInventory);
                else
                    mOBags.Drag.TakeEquipped(leEquip, lOInventory);

                // It stays on the pointer until the next click puts it down.
                mOBags.Clicks.EndPress();
                break;

            case MenuActionEnum.Split:
                mOBags.Split.TryBegin(lOItem, lOWindow, liSlot, leEquip,
                    lOWindow != null && liSlot >= 0 && liSlot < lOWindow.Slots.Count ? mOBags.Pages.SlotRect(lOWindow, liSlot) : new Rect(lOAt, Vector2.zero),
                    true);
                break;
        }
    }

    // ------------------------------------------------- Drawing

    internal void Layout()
    {
        if (mOMenuRoot == null)
            fBuildMenu();

        bool lbOpen = mOMenuItem != null;

        mOMenuRoot.gameObject.SetActive(lbOpen);

        if (!lbOpen)
        {
            mOMenuRect = Rect.zero;
            mOMenuRects.Clear();
            return;
        }

        float liScale = mOBags.View.Scale;
        int liHeight = (2 * MenuPad) + (mOMenuActions.Count * MenuRow);

        UWArtStamp lOStamp = UWVersionedTexture.Colours(liHeight);

        if (!mOMenuBackArt.IsCurrent(lOStamp))
            mOMenuBack.texture = mOMenuBackArt.Set(lOStamp, UWModernHudArt.BuildLeather(mOBags.Data.Textures, MenuWidth, liHeight, mOBags.Ui.TextureFilterMode));

        // Over everything of the bags, the tooltip hidden.
        mOMenuRoot.SetAsLastSibling();
        mOBags.View.HideTooltip();

        // Down and right of the pointer, inside the screen.
        float lfWidth = MenuWidth * liScale;
        float lfHeight = liHeight * liScale;
        float lfX = Mathf.Round(Mathf.Clamp(mOMenuAt.x + (2 * liScale), 0f, Screen.width - lfWidth));
        float lfY = Mathf.Round(Mathf.Clamp(mOMenuAt.y - lfHeight - (2 * liScale), 0f, Screen.height - lfHeight));

        mOMenuRect = new Rect(lfX, lfY, lfWidth, lfHeight);
        UWUiKit.SetRect(mOMenuBack.rectTransform, lfX, lfY, lfWidth, lfHeight);

        while (mOMenuTexts.Count < mOMenuActions.Count)
        {
            Text lOText = UWUiKit.CreateText(mOMenuRoot, "Entry", TextAnchor.MiddleLeft, UWModernPalette.HudText, mOBags.Ui);
            mOMenuTexts.Add(lOText);
        }

        Vector2 lOPointer = UWPointer.HasMouse ? UWPointer.Position : new Vector2(-1f, -1f);
        float lfRowTop = lfY + lfHeight - (MenuPad * liScale);

        mOMenuRects.Clear();
        mOMenuHighlight.enabled = false;

        for (int liAt = 0; liAt < mOMenuTexts.Count; liAt++)
        {
            Text lOText = mOMenuTexts[liAt];

            if (liAt >= mOMenuActions.Count)
            {
                lOText.enabled = false;
                continue;
            }

            Rect lORow = new Rect(lfX + (UWModernHudArt.LeatherLeft * liScale), lfRowTop - ((liAt + 1) * MenuRow * liScale),
                (MenuWidth - UWModernHudArt.LeatherLeft - UWModernHudArt.LeatherRight) * liScale, MenuRow * liScale);
            bool lbHover = lORow.Contains(lOPointer);

            mOMenuRects.Add(lORow);
            lOText.enabled = true;
            lOText.text = UWText.T(msMenuLabels[mOMenuActions[liAt]]);
            lOText.color = mOMenuActions[liAt] == MenuActionEnum.Cancel ? UWModernPalette.Dim : lbHover ? UWModernPalette.Gold : UWModernPalette.Text;
            lOText.fontSize = Mathf.Max(9, Mathf.RoundToInt(4.2f * liScale));
            UWUiKit.SetRect(lOText.rectTransform, lORow.x + (2 * liScale), lORow.y + (0.5f * liScale), lORow.width - (2 * liScale), lORow.height);

            if (lbHover)
            {
                mOMenuHighlight.enabled = true;
                UWUiKit.SetRect(mOMenuHighlight.rectTransform, lORow.x, lORow.y, lORow.width, lORow.height);
            }
        }
    }

    private void fBuildMenu()
    {
        mOMenuRoot = UWUiKit.CreateGroup(mOBags.View.Root, "Context menu");

        mOMenuBack = UWUiKit.CreateRawImage(mOMenuRoot, "Leather", true);
        UWPixelArtUI.Apply(mOMenuBack);

        mOMenuHighlight = UWUiKit.CreateImage(mOMenuRoot, "Hover", UWModernPalette.WithAlpha(UWModernPalette.Gold, 0.18f));
    }

    internal void Release()
    {
        mOMenuBackArt.Release();
    }
}
