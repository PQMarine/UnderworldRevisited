using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Backpack/equipment panel for the modern controls: a separate UGUI panel toggled by a
/// key, with the 8 backpack slots and the 5 equipment slots (see
/// UWInventory) - Modern shows no classic paper doll, hence both in one panel.
/// With Original the backpack instead sits always visible in the paper doll panel of the
/// classic 320x200 frame (UWGameUI); this panel stays unused/invisible with Original.
///
/// Clicking a backpack slot equips the item (UWInventory.TryAutoEquip), clicking
/// an equipment slot puts it back into the backpack (TryUnequip). No drag&drop here
/// (that is the Original controls, see plan) - manual hit testing instead of Unity UI events,
/// same manual polling convention as the rest of the project (see UWGameUI.fCursorMovement).
/// </summary>
public class UWInventoryUI : MonoBehaviour
{
    private const int BackpackSlotCount = UWInventory.BackpackSize;
    // Deliberately only the first 5 of UWArmorItemMap.BodySlot (Helmet..Boots, the
    // body-fitted armour sprite) - the ring/hand/use slots added 2026-08-25
    // so far only show in the classic paper doll (UWGameUI), not in this
    // Modern panel.
    private const int EquipSlotCount = 5;
    private const float SlotSize = 32f;
    private const float SlotSpacing = 4f;
    private const float ClickMoveThreshold = 6f;

    private struct SlotRow
    {
        public Image[] Images;
        public UWObject[] ItemsShown;
    }

    private DataImport mOUWData;
    private UWInventory mOInventory;
    private UWControlScheme mOControlScheme;
    private UWControls mOControls;

    private GameObject mOCanvasRoot;
    private SlotRow mBackpackRow;
    private SlotRow mEquipRow;

    private bool mbIsOpen;
    private bool mbIsPressed;
    private bool mbIsBackpackPressed;
    private int miPressedSlot = -1;
    private Vector2 mOPressStartPos;

    public void Init(DataImport pOUWData)
    {
        mOUWData = pOUWData;
    }

    private void Awake()
    {
        mOInventory = GetComponent<UWInventory>();

        if (mOInventory == null)
            mOInventory = gameObject.AddComponent<UWInventory>();

        mOControlScheme = GetComponentInParent<UWControlScheme>();
    }

    private void Start()
    {
        mBackpackRow.ItemsShown = new UWObject[BackpackSlotCount];
        mEquipRow.ItemsShown = new UWObject[EquipSlotCount];

        if (mOControlScheme != null)
            mOControls = mOControlScheme.Controls;

        fBuildCanvas();
        fSetOpen(false);
    }

    private void Update()
    {
        if (mOControls == null && mOControlScheme != null)
            mOControls = mOControlScheme.Controls;

        if (mOControls == null)
            return;

        // With Original the backpack is always visible in the classic paper doll
        // panel (UWGameUI) - this panel stays off there, even if the key is pressed.
        bool lbIsModern = mOControlScheme == null || mOControlScheme.Current == UWControlScheme.SchemeEnum.Modern;

        if (lbIsModern && mOControls.Player.ToggleInventory.WasPressedThisFrame())
            fSetOpen(!mbIsOpen);

        if (!lbIsModern && mbIsOpen)
            fSetOpen(false);

        if (!mbIsOpen)
            return;

        fRefreshRow(mBackpackRow, fGetBackpackItem);
        fRefreshRow(mEquipRow, fGetEquippedItem);
        fHandleClicks();
    }

    private UWObject fGetBackpackItem(int piIndex)
    {
        return mOInventory.Backpack[piIndex];
    }

    private UWObject fGetEquippedItem(int piIndex)
    {
        return mOInventory.GetEquipped((UWArmorItemMap.BodySlot)piIndex);
    }

    private void fSetOpen(bool pbOpen)
    {
        mbIsOpen = pbOpen;
        mOCanvasRoot.SetActive(pbOpen);
        mbIsPressed = false;
        miPressedSlot = -1;

        if (mOControlScheme != null)
            mOControlScheme.SetUiModal("inventory", pbOpen);
    }

    /// <summary>Left click (the same button as CursorDrag/Original movement, otherwise unused
    /// with Modern): clicking a backpack slot equips, clicking an equipment slot
    /// puts back - only if press and release are on the same slot and the
    /// mouse has barely moved in between (mirror of Interaction.cs' mMousePosStartUse check).</summary>
    private void fHandleClicks()
    {
        if (mOControls.Player.CursorDrag.WasPressedThisFrame())
        {
            mbIsPressed = true;
            mOPressStartPos = Mouse.current.position.ReadValue();

            miPressedSlot = fGetSlotUnderMouse(mBackpackRow);
            mbIsBackpackPressed = miPressedSlot >= 0;

            if (miPressedSlot < 0)
                miPressedSlot = fGetSlotUnderMouse(mEquipRow);
        }
        else if (mOControls.Player.CursorDrag.WasReleasedThisFrame())
        {
            if (mbIsPressed && miPressedSlot >= 0)
            {
                Vector2 lOReleasePos = Mouse.current.position.ReadValue();

                if (Vector2.Distance(mOPressStartPos, lOReleasePos) <= ClickMoveThreshold)
                {
                    int liReleaseSlot = fGetSlotUnderMouse(mbIsBackpackPressed ? mBackpackRow : mEquipRow);

                    if (liReleaseSlot == miPressedSlot)
                        fTryUseSlot(mbIsBackpackPressed, miPressedSlot);
                }
            }

            mbIsPressed = false;
            miPressedSlot = -1;
        }
    }

    private void fTryUseSlot(bool pbIsBackpack, int piSlot)
    {
        if (pbIsBackpack)
        {
            UWObject lOItem = mOInventory.Backpack[piSlot];

            if (lOItem != null)
                mOInventory.TryAutoEquip(lOItem);
        }
        else
        {
            mOInventory.TryUnequip((UWArmorItemMap.BodySlot)piSlot);
        }
    }

    private static int fGetSlotUnderMouse(SlotRow pORow)
    {
        if (pORow.Images == null)
            return -1;

        Vector2 lOMousePos = Mouse.current.position.ReadValue();

        for (int i = 0; i < pORow.Images.Length; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(pORow.Images[i].rectTransform, lOMousePos))
                return i;
        }

        return -1;
    }

    private void fRefreshRow(SlotRow pORow, System.Func<int, UWObject> pOGetItem)
    {
        if (mOInventory == null)
            return;

        for (int i = 0; i < pORow.Images.Length; i++)
        {
            UWObject lOItem = pOGetItem(i);

            if (lOItem == pORow.ItemsShown[i])
                continue;

            pORow.ItemsShown[i] = lOItem;
            fUpdateSlotImage(pORow.Images[i], lOItem);
        }
    }

    private static void fUpdateSlotImage(Image pOImage, UWObject pOItem)
    {
        // Icon is only set for the special cases (pillars, doors, decorations, ...) in
        // UWLevel.cs - for normal pickable items (weapons/armour/
        // treasures/...) Texture carries the matching OBJECTS.GR icon instead (see
        // UWLevel.cs: "if (uWObject.ID <= 460) uWObject.Texture = OBJECTS, ID").
        UWTexture lOSource = pOItem?.Icon ?? pOItem?.Texture;

        if (lOSource == null)
        {
            pOImage.sprite = null;
            pOImage.enabled = false;
            return;
        }

        Texture2D lOTexture = UWIconTextureBuilder.Build(lOSource, FilterMode.Point);

        UWIconPalette.Apply(pOImage);

        pOImage.sprite = Sprite.Create(lOTexture, new Rect(0f, 0f, lOTexture.width, lOTexture.height), new Vector2(0.5f, 0.5f), 1f);
        pOImage.enabled = true;
    }

    private void fBuildCanvas()
    {
        mOCanvasRoot = new GameObject("Inventory UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        mOCanvasRoot.transform.SetParent(transform, false);

        Canvas lOCanvas = mOCanvasRoot.GetComponent<Canvas>();
        lOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler lOScaler = mOCanvasRoot.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        lOScaler.referenceResolution = new Vector2(1280f, 720f);
        lOScaler.matchWidthOrHeight = 0.5f;

        // Two rows (equipment on top, backpack below), provisional semi-transparent
        // background until the INV.GR graphics (see plan) are adopted.
        mEquipRow.Images = fBuildRow(mOCanvasRoot.transform, "EquipRow", EquipSlotCount, 40f);
        mBackpackRow.Images = fBuildRow(mOCanvasRoot.transform, "BackpackRow", BackpackSlotCount, -40f);
    }

    private static Image[] fBuildRow(Transform pOParent, string psName, int piCount, float pfYOffset)
    {
        GameObject lOPanel = new GameObject(psName, typeof(RectTransform), typeof(Image));
        lOPanel.transform.SetParent(pOParent, false);

        RectTransform lOPanelRect = (RectTransform)lOPanel.transform;
        float lfPanelWidth = (piCount * (SlotSize + SlotSpacing)) + SlotSpacing;
        float lfPanelHeight = SlotSize + (SlotSpacing * 2f);
        lOPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        lOPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        lOPanelRect.pivot = new Vector2(0.5f, 0.5f);
        lOPanelRect.sizeDelta = new Vector2(lfPanelWidth, lfPanelHeight);
        lOPanelRect.anchoredPosition = new Vector2(0f, pfYOffset);

        Image lOPanelImage = lOPanel.GetComponent<Image>();
        lOPanelImage.color = new Color(0f, 0f, 0f, 0.75f);

        Image[] lOImages = new Image[piCount];

        for (int i = 0; i < piCount; i++)
        {
            GameObject lOSlot = new GameObject($"Slot{i}", typeof(RectTransform), typeof(Image));
            lOSlot.transform.SetParent(lOPanelRect, false);

            RectTransform lOSlotRect = (RectTransform)lOSlot.transform;
            lOSlotRect.anchorMin = new Vector2(0f, 0.5f);
            lOSlotRect.anchorMax = new Vector2(0f, 0.5f);
            lOSlotRect.pivot = new Vector2(0f, 0.5f);
            lOSlotRect.sizeDelta = new Vector2(SlotSize, SlotSize);
            lOSlotRect.anchoredPosition = new Vector2(SlotSpacing + (i * (SlotSize + SlotSpacing)), 0f);

            Image lOSlotBackground = lOSlot.GetComponent<Image>();
            lOSlotBackground.color = new Color(1f, 1f, 1f, 0.15f);

            GameObject lOIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            lOIcon.transform.SetParent(lOSlotRect, false);

            RectTransform lOIconRect = (RectTransform)lOIcon.transform;
            lOIconRect.anchorMin = Vector2.zero;
            lOIconRect.anchorMax = Vector2.one;
            lOIconRect.offsetMin = Vector2.zero;
            lOIconRect.offsetMax = Vector2.zero;

            Image lOIconImage = lOIcon.GetComponent<Image>();
            lOIconImage.enabled = false;
            lOImages[i] = lOIconImage;
        }

        return lOImages;
    }
}
