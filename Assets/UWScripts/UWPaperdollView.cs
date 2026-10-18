using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The paper doll as textures: the body and the five armour slots, rebuilt whenever the
/// inventory changes (cleanup step 4.6, 2026-10-18; part of UWCharacter until then). Which picture
/// a slot shows is UWPaperdollRules'. The classic HUD, the modern inventory page and the modern
/// panel read it through UWCharacter.Paperdoll.
/// </summary>
public sealed class UWPaperdollView
{
    private DataImport mOUWData;

    private UWInventory mOInventory;

    private UWWearables.Sex meSex;

    private FilterMode meFilterMode = FilterMode.Point;

    public Texture2D CharTexture { get; private set; }

    public Texture2D HelmetTexture { get; private set; }

    public Texture2D GlovesTexture { get; private set; }

    public Texture2D ChestArmorTexture { get; private set; }

    public Texture2D LegsArmorTexture { get; private set; }

    public Texture2D BootsTexture { get; private set; }

    /// <summary>The armour pictures as the data has them (null when nothing is worn there), for the
    /// modern panel's figure on a back of its own (UWHudArt.ComposePaperdoll).</summary>
    public UWTexture HelmetSource { get; private set; }

    public UWTexture ChestSource { get; private set; }

    public UWTexture GlovesSource { get; private set; }

    public UWTexture LegsSource { get; private set; }

    public UWTexture BootsSource { get; private set; }

    /// <summary>How many rows lower the legs' picture is drawn (UWWearables.GetArmorDrop) - already
    /// moved inside LegsArmorTexture, to be added where LegsSource is drawn.</summary>
    public int LegsDrop { get; private set; }

    /// <summary>The body picture and the armour of what is worn now; from then on it follows the
    /// inventory.</summary>
    public void Init(DataImport pOData, int piBodyIndex, FilterMode peFilterMode, UWInventory pOInventory)
    {
        mOUWData = pOData;
        meFilterMode = peFilterMode;
        meSex = UWPaperdollRules.GetSex(piBodyIndex);

        UWTexture lOCharTex = mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.BODIES, piBodyIndex);
        Texture2D lOChar = new Texture2D(lOCharTex.Width, lOCharTex.Height, TextureFormat.ARGB32, false);
        lOChar.name = "UWPaperdollView body";
        lOChar.filterMode = meFilterMode;
        lOChar.SetPixels32(UWGameUI.fGetTextureInvert(lOCharTex));
        lOChar.Apply();
        CharTexture = lOChar;

        Detach();

        mOInventory = pOInventory;

        if (mOInventory != null)
            mOInventory.InventoryChanged += fRebuildAllArmorSlots;

        fRebuildAllArmorSlots();
    }

    /// <summary>Stops following the inventory (the character is destroyed).</summary>
    public void Detach()
    {
        if (mOInventory != null)
            mOInventory.InventoryChanged -= fRebuildAllArmorSlots;
    }

    private void fRebuildAllArmorSlots()
    {
        fRebuildArmorSlot(UWArmorItemMap.BodySlot.Helmet);
        fRebuildArmorSlot(UWArmorItemMap.BodySlot.Chest);
        fRebuildArmorSlot(UWArmorItemMap.BodySlot.Gloves);
        fRebuildArmorSlot(UWArmorItemMap.BodySlot.Legs);
        fRebuildArmorSlot(UWArmorItemMap.BodySlot.Boots);
    }

    /// <summary>
    /// Rebuilds the paper doll texture of a body area from the actually
    /// equipped item (UWInventory) - colour/condition/slot via UWPaperdollRules.GetSlotLook. If
    /// nothing is equipped, the slot shows no armour (ArmorTypes.None); the
    /// Material/Condition/Type fields in the Inspector are not used.
    /// </summary>
    private void fRebuildArmorSlot(UWArmorItemMap.BodySlot peSlot)
    {
        UWObject lOItem = mOInventory != null ? mOInventory.GetEquipped(peSlot) : null;

        UWPaperdollRules.SlotLook lOLook = UWPaperdollRules.GetSlotLook(lOItem, meSex);

        Texture2D lOTexture = fBuildArmorTexture(lOLook.Material, lOLook.Condition, lOLook.Type);
        int liDrop = lOLook.Drop;

        // The men's plate legs sit two rows too high in their picture (UWWearables.GetArmorDrop):
        // the picture's rows moved down here, for the classic scheme and the modern panel alike.
        if (liDrop > 0)
        {
            Color32[] lOPixels = lOTexture.GetPixels32();
            Color32[] lOMoved = new Color32[lOPixels.Length];
            int liWidth = lOTexture.width;

            for (int liRow = 0; liRow < lOTexture.height - liDrop; liRow++)
                System.Array.Copy(lOPixels, (liRow + liDrop) * liWidth, lOMoved, liRow * liWidth, liWidth);

            lOTexture.SetPixels32(lOMoved);
            lOTexture.Apply();
        }

        UWTexture lOSource = lOLook.Type == UWWearables.ArmorTypes.None ? null
            : mOUWData.Wearables.GetArmor(meSex, lOLook.Material, lOLook.Condition, lOLook.Type);

        switch (peSlot)
        {
            case UWArmorItemMap.BodySlot.Helmet: HelmetTexture = lOTexture; HelmetSource = lOSource; break;
            case UWArmorItemMap.BodySlot.Chest: ChestArmorTexture = lOTexture; ChestSource = lOSource; break;
            case UWArmorItemMap.BodySlot.Gloves: GlovesTexture = lOTexture; GlovesSource = lOSource; break;
            case UWArmorItemMap.BodySlot.Legs: LegsArmorTexture = lOTexture; LegsSource = lOSource; LegsDrop = liDrop; break;
            case UWArmorItemMap.BodySlot.Boots: BootsTexture = lOTexture; BootsSource = lOSource; break;
        }
    }

    private Texture2D fBuildArmorTexture(UWWearables.ArmorMaterials peMaterial, UWWearables.ArmorConditions peCondition, UWWearables.ArmorTypes peType)
    {
        if (peType == UWWearables.ArmorTypes.None)
            return fBuildTransparentPlaceholder();

        UWTexture lOSource = mOUWData.Wearables.GetArmor(meSex, peMaterial, peCondition, peType);
        Texture2D lOResult = new Texture2D(lOSource.Width, lOSource.Height, TextureFormat.ARGB32, false);
        lOResult.name = "UWPaperdollView armour";
        lOResult.SetPixels32(UWGameUI.fGetTextureInvert(lOSource));
        lOResult.filterMode = meFilterMode;
        lOResult.Apply();
        return lOResult;
    }

    // GetArmor(..., ArmorTypes.None) previously: returned WeaponAnimations.Black (a
    // real WEAPONS.GR frame), which was only "accidentally" invisible because UWPalette
    // wrongly treated pure black as a transparency marker. Since the palette fix
    // (see UWPalette.cs) real black is opaque again, which made this placeholder
    // show up as a visible black spot on empty armour slots. An empty
    // slot needs no borrowed texture, only real transparency.
    private Texture2D fBuildTransparentPlaceholder()
    {
        Texture2D lOResult = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        lOResult.name = "UWPaperdollView empty slot";
        lOResult.filterMode = meFilterMode;
        lOResult.SetPixel(0, 0, new Color32(0, 0, 0, 0));
        lOResult.Apply();
        return lOResult;
    }
}
