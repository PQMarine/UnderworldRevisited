using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The weapon hand and the power gem as textures (cleanup step 4.6, 2026-10-18; part of
/// UWCharacter until then). The swing itself is engine-free (UWWeaponSwing); this class drives it
/// with the frame time and Interaction's input, converts the pictures it chooses, and answers its
/// events: the music on drawing and sheathing, the noise of the blow. The classic HUD and the
/// modern scheme read the textures through UWCharacter.Weapon.
/// </summary>
public sealed class UWWeaponView
{
    private readonly UWWeaponSwing mOSwing = new UWWeaponSwing();

    /// <summary>The swing itself (engine-free).</summary>
    public UWWeaponSwing Swing => mOSwing;

    private FilterMode meFilterMode = FilterMode.Point;

    public UWWeaponView()
    {
        mOSwing.AttackConnected += fOnConnected;
        mOSwing.CombatModeEntered += UWMusic.OnWeaponDrawn;
        mOSwing.CombatModeLeft += UWMusic.OnCombatModeLeft;
        mOSwing.Struck += fOnStruck;
    }

    /// <summary>
    /// The weapon and gem from the data. peStartWeapon is the Inspector's start weapon on
    /// UWCharacter (the scene has the axe).
    /// </summary>
    public void Init(DataImport pOData, WeaponTypes peStartWeapon, FilterMode peFilterMode)
    {
        meFilterMode = peFilterMode;

        mOSwing.Init(pOData.WeaponAnimations, pOData.PowerGem, peStartWeapon);
        fSync();
    }

    /// <summary>Reports the moment the blow connects - Interaction's hit check.</summary>
    public event System.Action AttackConnected;

    /// <summary>The blow is struck - UWCharacter makes its noise.</summary>
    public event System.Action Struck;

    private void fOnConnected()
    {
        if (AttackConnected != null)
            AttackConnected();
    }

    private void fOnStruck()
    {
        if (Struck != null)
            Struck();
    }

    /// <summary>One frame of the swing, with UWSettings.AttackConnectFramesFromEnd as it is now.
    /// </summary>
    public void Tick(float pfDeltaSeconds, Interaction pOInteraction)
    {
        mOSwing.ConnectFramesFromEnd = UnderworldRevisited.UWSettings.Instance != null
            ? UnderworldRevisited.UWSettings.Instance.AttackConnectFramesFromEnd
            : UWWeaponSwing.DefaultFramesFromEnd;

        mOHand.Interaction = pOInteraction;
        mOSwing.Tick(pfDeltaSeconds, mOHand);
        fSync();
    }

    /// <summary>Interaction as the swing's hand - read at the moment the swing asks.</summary>
    private sealed class Hand : IUWWeaponHand
    {
        public Interaction Interaction;

        public bool IsCombatModeActive => Interaction.IsCombatModeActive;

        public bool IsAttacking => Interaction.IsAttacking;

        public float AttackCharge => Interaction.AttackCharge;

        public bool IsRangedTargeting => Interaction.IsRangedTargeting;
    }

    private readonly Hand mOHand = new Hand();

    /// <summary>See UWWeaponSwing.SetWeaponType.</summary>
    public void SetWeaponType(WeaponTypes peWeapon, bool pbNothingVisible = false)
    {
        mOSwing.SetWeaponType(peWeapon, pbNothingVisible);
        fSync();
    }

    public void ReleaseAttack()
    {
        mOSwing.ReleaseAttack();
    }

    /// <summary>See UWWeaponSwing.HasRangedWeapon.</summary>
    public bool HasRangedWeapon
    {
        get { return mOSwing.HasRangedWeapon; }
        set { mOSwing.HasRangedWeapon = value; }
    }

    /// <summary>The swing of the next blow - set by Interaction every frame.</summary>
    public AttackTypes AttackType
    {
        get { return mOSwing.AttackType; }
        set { mOSwing.AttackType = value; }
    }

    public bool DrawWWeapon => mOSwing.DrawWWeapon;

    public bool CanAttack => mOSwing.CanAttack;

    public bool IsWindupComplete => mOSwing.IsWindupComplete;

    public bool IsChargingAttack => mOSwing.IsChargingAttack;

    /// <summary>How far the weapon is drawn, 0 to 1.</summary>
    public float CurrentReadyWeaponTime => mOSwing.CurrentReadyWeaponTime;

    public WeaponCoordinate GetWeaponOffset()
    {
        return mOSwing.GetWeaponOffset();
    }

    public Texture2D CurrentWeaponTexture { get; private set; }

    public Texture2D PowerGemTeture { get; private set; }

    private int miShownFrameVersion = -1;

    /// <summary>Converts what the swing chose since the last call: the weapon picture whenever it
    /// was chosen anew (UWCharacter.fUpdateWeapon converted on every call), the gem when its
    /// picture changed.</summary>
    private void fSync()
    {
        if (mOSwing.FrameVersion != miShownFrameVersion)
        {
            miShownFrameVersion = mOSwing.FrameVersion;
            fGetCurrentWeaponTexture();
        }

        fUpdatePowerGemTeture(mOSwing.GemPicture);
    }

    /// <summary>
    /// The weapon image as a texture - each frame converted once and kept.
    ///
    /// LEAK FIXED (per user, 2026-09-13: after long play the screen went dark and the
    /// inventory disappeared, in the log "Resource ID out of range in GetResource ... max is
    /// 1048575"): here every frame change of the weapon created a new texture that was never
    /// released - eight per second in combat. Unity eventually runs out of IDs for
    /// graphics resources, and then no texture can be created any more.
    /// </summary>
    private void fGetCurrentWeaponTexture()
    {
        CurrentWeaponTexture = fGetCachedTexture(mOSwing.Frame);
    }

    /// <summary>Convert a UI texture once and keep it, as with the power orb.
    /// </summary>
    private Texture2D fGetCachedTexture(UWTexture pOTexture)
    {
        if (pOTexture == null)
            return null;

        Texture2D lOCached;

        // A change of the colour help makes every cached picture stale.
        if (miUiColourVersion != UWColourVision.Version)
        {
            foreach (Texture2D lOOld in mOUiTextureCache.Values)
                Object.Destroy(lOOld);

            mOUiTextureCache.Clear();

            miUiColourVersion = UWColourVision.Version;
        }

        if (!mOUiTextureCache.TryGetValue(pOTexture, out lOCached) || lOCached == null)
        {
            lOCached = new Texture2D(pOTexture.Width, pOTexture.Height, TextureFormat.ARGB32, false);
            lOCached.name = "UWWeaponView weapon";
            lOCached.filterMode = meFilterMode;
            lOCached.SetPixels32(UWGameUI.fGetTextureInvert(pOTexture));
            lOCached.Apply();

            mOUiTextureCache[pOTexture] = lOCached;
        }

        return lOCached;
    }

    private readonly Dictionary<UWTexture, Texture2D> mOUiTextureCache = new Dictionary<UWTexture, Texture2D>();

    private int miUiColourVersion = -1;

    /// <summary>
    /// Sets the image of the power orb.
    ///
    /// Previously EVERY call created a new texture here, converted the pixels
    /// and called Apply() - and the call is in Update, so it ran every frame. As long as
    /// the orb showed the same image most of the time, this went unnoticed; since it fills over
    /// the whole charge time, it became visible (per user, 2026-09-01: "choppy").
    ///
    /// Now each of the fourteen orb images is converted once and kept, and a
    /// change only happens when the image really changes.
    /// </summary>
    private void fUpdatePowerGemTeture(UWTexture pTexture)
    {
        if (pTexture == null || pTexture == mOCurrentPowerGemSource)
            return;

        mOCurrentPowerGemSource = pTexture;

        Texture2D lOCached;

        if (!mOPowerGemTextures.TryGetValue(pTexture, out lOCached))
        {
            lOCached = new Texture2D(pTexture.Width, pTexture.Height, TextureFormat.ARGB32, false);
            lOCached.name = "UWWeaponView gem";
            lOCached.SetPixels32(UWGameUI.fGetTextureInvert(pTexture));
            lOCached.filterMode = meFilterMode;
            lOCached.Apply();

            mOPowerGemTextures[pTexture] = lOCached;
        }

        PowerGemTeture = lOCached;
    }

    private readonly Dictionary<UWTexture, Texture2D> mOPowerGemTextures = new Dictionary<UWTexture, Texture2D>();

    private UWTexture mOCurrentPowerGemSource;
}
