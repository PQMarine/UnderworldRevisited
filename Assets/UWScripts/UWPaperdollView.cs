using UnityEngine;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// The paper doll as textures: the body and the five armour slots, rebuilt whenever the
/// inventory changes (cleanup step 4.6, 2026-10-18; part of UWCharacter until then). Which picture
/// a slot shows is UWPaperdollRules'. The classic HUD, the modern inventory page and the modern
/// panel read it through UWCharacter.Paperdoll.
///
/// THE COLOUR HELP (decision 6 of the cleanup plan, B30; per user 2026-10-19: the paperdoll did
/// not follow the colour filter): the body and the armour are built through it like every other
/// picture and built anew when UWColourVision.Version changes - the texture properties look at
/// it when they are read, so whoever shows them gets the new pictures the same frame.
///
/// ONE TEXTURE PER SLOT (2026-10-19): each slot keeps its picture with a stamp of what it shows
/// (UWVersionedTexture: the colour help, material, condition, type) and builds only when that
/// changes; the picture it replaces is destroyed. Until then every inventory change made five new
/// textures and never destroyed the old ones (found by cleanup step 5.4).
/// </summary>
public sealed class UWPaperdollView
{
    private DataImport mOUWData;

    private UWInventory mOInventory;

    private UWWearables.Sex meSex;

    private FilterMode meFilterMode = FilterMode.Point;

    private int miBodyIndex;

    private readonly UWVersionedTexture mOBody = new UWVersionedTexture();

    /// <summary>The five armour slots in the order of UWArmorItemMap.BodySlot (Helmet to Boots).</summary>
    private readonly UWVersionedTexture[] mOSlots =
    {
        new UWVersionedTexture(), new UWVersionedTexture(), new UWVersionedTexture(), new UWVersionedTexture(), new UWVersionedTexture()
    };

    /// <summary>The colour help the pictures were last built with (UWColourVision.Version).</summary>
    private int miBuiltColours = -1;

    /// <summary>Counts every change of the pictures across all paperdolls, so a reader that
    /// composes them anew (the modern panel's figure) can stamp what it built with.</summary>
    private static int msiLooks;

    /// <summary>Changes whenever the body or an armour picture is built anew - not when only the
    /// colour help changes (that is the stamp's own part).</summary>
    public int LookVersion { get; private set; }

    public Texture2D CharTexture => fTexture(mOBody);

    public Texture2D HelmetTexture => fTexture(mOSlots[(int)UWArmorItemMap.BodySlot.Helmet]);

    public Texture2D GlovesTexture => fTexture(mOSlots[(int)UWArmorItemMap.BodySlot.Gloves]);

    public Texture2D ChestArmorTexture => fTexture(mOSlots[(int)UWArmorItemMap.BodySlot.Chest]);

    public Texture2D LegsArmorTexture => fTexture(mOSlots[(int)UWArmorItemMap.BodySlot.Legs]);

    public Texture2D BootsTexture => fTexture(mOSlots[(int)UWArmorItemMap.BodySlot.Boots]);

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
        // Another game's data or filter mode: everything is built anew.
        Release();

        mOUWData = pOData;
        meFilterMode = peFilterMode;
        meSex = UWPaperdollRules.GetSex(piBodyIndex);
        miBodyIndex = piBodyIndex;

        fRebuildBody();

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

    /// <summary>Destroys the pictures (the character is destroyed, or Init starts anew). Nothing
    /// is built again until the next Init.</summary>
    public void Release()
    {
        mOUWData = null;
        mOBody.Release();

        foreach (UWVersionedTexture lOSlot in mOSlots)
            lOSlot.Release();

        miBuiltColours = -1;
    }

    /// <summary>A picture as it is to be shown now: built anew first when the colour help changed
    /// since the last build (the one place the paperdoll looks at it).</summary>
    private Texture2D fTexture(UWVersionedTexture pOPicture)
    {
        if (mOUWData != null && miBuiltColours != UWColourVision.Version)
        {
            fRebuildBody();
            fRebuildAllArmorSlots();
        }

        return pOPicture.Texture;
    }

    private void fRebuildBody()
    {
        UWArtStamp lOStamp = fStamp(miBodyIndex);

        if (mOBody.IsCurrent(lOStamp))
            return;

        UWTexture lOCharTex = mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.BODIES, miBodyIndex);
        mOBody.Set(lOStamp, UWTextureFactory.FromTexture(lOCharTex, meFilterMode, "UWPaperdollView body"));
        LookVersion = ++msiLooks;
    }

    /// <summary>The stamp of a picture built now, which is also the colour help the paperdoll
    /// was built with. The filter is read first: its first reading loads the settings and counts
    /// the version up (UWColourVision).</summary>
    private UWArtStamp fStamp(int piA, int piB = 0, int piC = 0)
    {
        _ = UWColourVision.Filter;
        miBuiltColours = UWColourVision.Version;

        return UWVersionedTexture.Colours(piA, piB, piC);
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
        int liDrop = lOLook.Drop;

        UWTexture lOSource = lOLook.Type == UWWearables.ArmorTypes.None ? null
            : mOUWData.Wearables.GetArmor(meSex, lOLook.Material, lOLook.Condition, lOLook.Type);

        // Built only when the look or the colour help changed; Set destroys the picture it replaces.
        UWVersionedTexture lOSlot = mOSlots[(int)peSlot];
        UWArtStamp lOStamp = fStamp((int)lOLook.Material, (int)lOLook.Condition, (int)lOLook.Type);

        if (!lOSlot.IsCurrent(lOStamp))
        {
            lOSlot.Set(lOStamp, fBuildArmorTexture(lOSource, liDrop));
            LookVersion = ++msiLooks;
        }

        switch (peSlot)
        {
            case UWArmorItemMap.BodySlot.Helmet: HelmetSource = lOSource; break;
            case UWArmorItemMap.BodySlot.Chest: ChestSource = lOSource; break;
            case UWArmorItemMap.BodySlot.Gloves: GlovesSource = lOSource; break;
            case UWArmorItemMap.BodySlot.Legs: LegsSource = lOSource; LegsDrop = liDrop; break;
            case UWArmorItemMap.BodySlot.Boots: BootsSource = lOSource; break;
        }
    }

    /// <summary>An armour picture with the colour help (UWUploadPixels.FromTexture, the pixels of
    /// UWTextureFactory.FromTexture), or the empty slot's placeholder for none.</summary>
    private Texture2D fBuildArmorTexture(UWTexture pOSource, int piDrop)
    {
        if (pOSource == null)
            return fBuildTransparentPlaceholder();

        byte[] lyPixels = UWUploadPixels.FromTexture(pOSource, UWColourVision.Filter);

        // The men's plate legs sit two rows too high in their picture (UWWearables.GetArmorDrop):
        // the picture's rows moved down here, for the classic scheme and the modern panel alike.
        if (piDrop > 0)
            lyPixels = UWUploadPixels.MoveDown(lyPixels, pOSource.Width, pOSource.Height, piDrop);

        return UWTextureFactory.Upload(lyPixels, pOSource.Width, pOSource.Height, meFilterMode, TextureWrapMode.Repeat,
            "UWPaperdollView armour");
    }

    // GetArmor(..., ArmorTypes.None) previously: returned WeaponAnimations.Black (a
    // real WEAPONS.GR frame), which was only "accidentally" invisible because UWPalette
    // wrongly treated pure black as a transparency marker. Since the palette fix
    // (see UWPalette.cs) real black is opaque again, which made this placeholder
    // show up as a visible black spot on empty armour slots. An empty
    // slot needs no borrowed texture, only real transparency.
    // Through UWTextureFactory since 2026-10-19 (RGBA32, the same one transparent pixel).
    private Texture2D fBuildTransparentPlaceholder()
    {
        return UWTextureFactory.Upload(new byte[UWUploadPixels.BytesPerPixel], 1, 1, meFilterMode, TextureWrapMode.Repeat,
            "UWPaperdollView empty slot");
    }
}
