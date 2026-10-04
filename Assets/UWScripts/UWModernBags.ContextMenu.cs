using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// THE BAGS' CONTEXT MENU (per user, 2026-10-04: the right button used at once and Shift with it
/// looked - not liked): the right button on a thing in a bag window or on the paperdoll opens a
/// small leather menu at the pointer.
///
///   - First what the thing is for: a container OPEN or CLOSE (the rune bag opens the rune panel),
///     armour, rings and weapons in a bag EQUIP, on the paperdoll USE (what is not worn as such)
///     and TAKE OFF, anything else USE - at once, or the use mode for what goes onto something
///     else (UWItemDrag.UseCarriedItem, NeedsUseTarget);
///   - then LOOK, PICK UP (onto the pointer, as a click), SPLIT for a stack (the split box) and
///     CANCEL.
///
/// The left button chooses; the right button, Escape or a click beside it close it. While it is
/// open the keys are its own and its clicks do nothing else (IsMenuOpen, as the split box).
/// </summary>
public partial class UWModernBags
{
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

    private static readonly Dictionary<MenuActionEnum, string> msMenuLabels = new Dictionary<MenuActionEnum, string>
    {
        { MenuActionEnum.Use, "Use" },
        { MenuActionEnum.Open, "Open" },
        { MenuActionEnum.Close, "Close" },
        { MenuActionEnum.OpenRunes, "Open" },
        { MenuActionEnum.Equip, "Equip" },
        { MenuActionEnum.TakeOff, "Take off" },
        { MenuActionEnum.Look, "Look" },
        { MenuActionEnum.PickUp, "Pick up" },
        { MenuActionEnum.Split, "Split" },
        { MenuActionEnum.Cancel, "Cancel" },
        { MenuActionEnum.Talk, "Talk" },
        { MenuActionEnum.WorldUse, "Use" },
        { MenuActionEnum.WorldLook, "Look" },
        { MenuActionEnum.WorldPickUp, "Pick up" }
    };

    /// <summary>The menu in original pixels.</summary>
    private const int MenuWidth = 52;

    private const int MenuRow = 8;

    private const int MenuPad = 4;

    private static readonly Color msMenuText = new Color(0.94f, 0.87f, 0.71f);

    private static readonly Color msMenuDim = new Color(0.78f, 0.71f, 0.57f);

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

    private Texture2D mOMenuBackTexture;

    private Vector2Int mOMenuBackSize;

    private int miMenuArtVersion = -1;

    private Image mOMenuHighlight;

    private readonly List<Text> mOMenuTexts = new List<Text>();

    /// <summary>The menu has the keys and the mouse (UWGameUI.IsTextEntryActive, UWModernPointer) -
    /// also in the frame it closed, so its click and its right button do nothing more.</summary>
    public static bool IsMenuOpen => Instance != null
        && (Instance.mOMenuItem != null || Instance.miMenuClosedFrame == Time.frameCount);

    private void fOpenMenu(UWObject pOItem, Window pOWindow, int piSlot, UWArmorItemMap.BodySlot peEquip, Vector2 pOAt)
    {
        UWInventory lOInventory = fInventory();

        mOMenuActions.Clear();

        bool lbWorn = fTryGetEquipSlot(pOItem, lOInventory, out UWArmorItemMap.BodySlot _);

        if (pOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
        {
            if (pOItem.ID == UWObjectMechanics.RuneBagId)
                mOMenuActions.Add(MenuActionEnum.OpenRunes);
            else
                mOMenuActions.Add(mOOpenBags.Contains(pOItem) ? MenuActionEnum.Close : MenuActionEnum.Open);
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

        if (fStackCount(pOItem) >= 2)
            mOMenuActions.Add(MenuActionEnum.Split);

        mOMenuActions.Add(MenuActionEnum.Cancel);

        // In a conversation nothing is used, as in the original (the rune panel is hidden there too).
        if (fIsTalking())
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
        mbPressDrag = false;

        // The keys are the menu's from now on, not only from UWGameUI's next look.
        UWControls.SetTextEntryActive(true);
    }

    private void fCloseMenu()
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
    public void OpenWorldMenu(UWEntityInfo pOTarget, UWEntityInfo pOPickableBehind, bool pbCreature, bool pbPickable, Vector2 pOAt)
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
        mbPressDrag = false;

        UWControls.SetTextEntryActive(true);
    }

    /// <summary>A thing taken from the world by the free pointer's drag (Interaction): the bags
    /// open, and it is put down where the button is let go (fLeftReleased), as a drag in them.</summary>
    public void BeginWorldDrag(Vector2 pOStart)
    {
        if (!mbOpen)
            fOpen();

        fForgetOrigin();
        mbPressDrag = true;
        mOPressStart = pOStart;
        mOPressWindow = null;
        miPressSlot = -1;
        mbPressEquip = false;
    }

    /// <summary>The thing lying where the menu was opened now.</summary>
    private UWObject fMenuSource()
    {
        UWInventory lOInventory = fInventory();

        if (lOInventory == null)
            return null;

        // A thing in the world: as long as it is there.
        if (mbMenuWorld)
            return mOMenuEntity != null ? mOMenuEntity.ObjectData : null;

        if (mOMenuWindow == null)
            return lOInventory.GetEquipped(meMenuEquip);

        return mOMenuWindow.Root.gameObject.activeSelf ? fGetItem(mOMenuWindow, miMenuSlot) : null;
    }

    /// <summary>Keys and clicks while the menu is open; called by Update instead of the bags' own.</summary>
    private void fUpdateMenu(UWControls pOControls)
    {
        if (fMenuSource() != mOMenuItem)
        {
            fCloseMenu();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            fCloseMenu();
            return;
        }

        if (Mouse.current == null || Time.frameCount == miMenuOpenedFrame)
            return;

        if (pOControls.Player.Interact.WasPressedThisFrame())
        {
            fCloseMenu();
            return;
        }

        if (!pOControls.Player.CursorDrag.WasPressedThisFrame())
            return;

        Vector2 lOPointer = Mouse.current.position.ReadValue();

        for (int liAt = 0; liAt < mOMenuRects.Count && liAt < mOMenuActions.Count; liAt++)
        {
            if (mOMenuRects[liAt].Contains(lOPointer))
            {
                fRunMenu(mOMenuActions[liAt]);
                return;
            }
        }

        fCloseMenu();
    }

    private void fRunMenu(MenuActionEnum peAction)
    {
        UWObject lOItem = mOMenuItem;
        Window lOWindow = mOMenuWindow;
        int liSlot = miMenuSlot;
        UWArmorItemMap.BodySlot leEquip = meMenuEquip;
        Vector2 lOAt = mOMenuAt;
        UWEntityInfo lOEntity = mOMenuEntity;
        UWEntityInfo lOPickable = mOMenuPickable;
        UWInventory lOInventory = fInventory();
        UWItemDrag lODrag = UWScene.ItemDrag;

        fCloseMenu();

        if (lOInventory == null || mOInteraction == null)
            return;

        switch (peAction)
        {
            case MenuActionEnum.Talk:
            case MenuActionEnum.WorldUse:
                if (lOEntity != null)
                    mOInteraction.ModernUseThing(lOEntity, lOPickable);

                break;

            case MenuActionEnum.WorldLook:
                if (lOEntity != null)
                    mOInteraction.LookAtTarget(lOEntity, null);

                break;

            case MenuActionEnum.WorldPickUp:
                if (lOEntity != null && mOInteraction.TakeOntoPointer(lOEntity))
                {
                    if (!mbOpen)
                        fOpen();

                    mbPressDrag = false;
                }

                break;

            case MenuActionEnum.Use:
                if (lODrag != null && lODrag.UseCarriedItem(lOItem))
                    break;

                if (lODrag != null && lODrag.NeedsUseTarget(lOItem))
                {
                    BeginUse(lOItem, false);
                    break;
                }

                mOInteraction.AddMessage("You cannot use that.");
                break;

            case MenuActionEnum.Open:
            case MenuActionEnum.Close:
                fToggleBag(lOItem);
                break;

            case MenuActionEnum.OpenRunes:
                if (UWModernRunePanel.Instance != null)
                    UWModernRunePanel.Instance.Open(UWModernRunePanel.TabEnum.Runes);

                break;

            case MenuActionEnum.Equip:
                if (lOWindow != null && fTryGetEquipSlot(lOItem, lOInventory, out UWArmorItemMap.BodySlot leSlot))
                    fEquip(lOWindow, liSlot, leSlot, lOInventory);

                break;

            case MenuActionEnum.TakeOff:
                if (lOInventory.TryUnequipAnywhere(leEquip))
                    lOInventory.ApplyCarriedLight();
                else
                    mOInteraction.AddMessage("Your pack and bags are full.");

                break;

            case MenuActionEnum.Look:
                fLook(lOItem);
                break;

            case MenuActionEnum.PickUp:
                if (lOWindow != null)
                    fTake(lOWindow, liSlot, lOInventory);
                else
                    fTakeEquipped(leEquip, lOInventory);

                // It stays on the pointer until the next click puts it down.
                mbPressDrag = false;
                break;

            case MenuActionEnum.Split:
                fTryBeginSplit(lOItem, lOWindow, liSlot, leEquip,
                    lOWindow != null && liSlot >= 0 && liSlot < lOWindow.Slots.Count ? lOWindow.Slots[liSlot].ScreenRect : new Rect(lOAt, Vector2.zero),
                    true);
                break;
        }
    }

    // ------------------------------------------------- Drawing

    private void fLayoutMenu()
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

        float liScale = miScale;
        int liHeight = (2 * MenuPad) + (mOMenuActions.Count * MenuRow);

        if (mOMenuBackTexture == null || mOMenuBackSize.y != liHeight || miMenuArtVersion != UWColourVision.Version)
        {
            if (mOMenuBackTexture != null)
                Destroy(mOMenuBackTexture);

            mOMenuBackTexture = UWModernHudArt.BuildLeather(fData().Textures, MenuWidth, liHeight, mOUi.TextureFilterMode);
            mOMenuBackSize = new Vector2Int(MenuWidth, liHeight);
            miMenuArtVersion = UWColourVision.Version;
            mOMenuBack.texture = mOMenuBackTexture;
        }

        // Over everything of the bags, the tooltip hidden.
        mOMenuRoot.SetAsLastSibling();
        mOTooltip.enabled = false;
        mOTooltipBack.enabled = false;

        // Down and right of the pointer, inside the screen.
        float lfWidth = MenuWidth * liScale;
        float lfHeight = liHeight * liScale;
        float lfX = Mathf.Round(Mathf.Clamp(mOMenuAt.x + (2 * liScale), 0f, Screen.width - lfWidth));
        float lfY = Mathf.Round(Mathf.Clamp(mOMenuAt.y - lfHeight - (2 * liScale), 0f, Screen.height - lfHeight));

        mOMenuRect = new Rect(lfX, lfY, lfWidth, lfHeight);
        fSetRect(mOMenuBack.rectTransform, lfX, lfY, lfWidth, lfHeight);

        while (mOMenuTexts.Count < mOMenuActions.Count)
        {
            Text lOText = fCreateText(mOMenuRoot, "Entry", TextAnchor.MiddleLeft);
            mOMenuTexts.Add(lOText);
        }

        Vector2 lOPointer = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);
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
            lOText.text = msMenuLabels[mOMenuActions[liAt]];
            lOText.color = mOMenuActions[liAt] == MenuActionEnum.Cancel ? msMenuDim : lbHover ? msGold : msMenuText;
            lOText.fontSize = Mathf.Max(9, Mathf.RoundToInt(4.2f * liScale));
            fSetRect(lOText.rectTransform, lORow.x + (2 * liScale), lORow.y + (0.5f * liScale), lORow.width - (2 * liScale), lORow.height);

            if (lbHover)
            {
                mOMenuHighlight.enabled = true;
                fSetRect(mOMenuHighlight.rectTransform, lORow.x, lORow.y, lORow.width, lORow.height);
            }
        }
    }

    private void fBuildMenu()
    {
        GameObject lOObject = new GameObject("Context menu", typeof(RectTransform));
        lOObject.transform.SetParent(mORoot, false);

        mOMenuRoot = (RectTransform)lOObject.transform;
        mOMenuRoot.anchorMin = Vector2.zero;
        mOMenuRoot.anchorMax = Vector2.zero;
        mOMenuRoot.pivot = Vector2.zero;

        mOMenuBack = fCreateRawImage(mOMenuRoot, "Leather");
        mOMenuBack.enabled = true;
        UWPixelArtUI.Apply(mOMenuBack);

        mOMenuHighlight = fCreateImage(mOMenuRoot, "Hover", new Color(msGold.r, msGold.g, msGold.b, 0.18f));
    }

    private void fDestroyMenuTexture()
    {
        if (mOMenuBackTexture != null)
            Destroy(mOMenuBackTexture);

        mOMenuBackTexture = null;
    }
}
