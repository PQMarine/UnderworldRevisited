using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UWDataImport;
using System.Collections.Generic;

/// <summary>
/// The HUD section "the rune shelf, the spell entry points, the active spell icons and the flask clicks".
/// Its own class since 2026-09-18 (stage two of the HUD rebuild); the owner hands in the game
/// data, the frame, the character, the interaction and the inventory through mOUi. UWGameUI
/// keeps the public entry points as forwards.
/// </summary>
public sealed class UWHudRunes
{
    private readonly UWGameUI mOUi;

    internal UWHudRunes(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    // ------------------------------------------------- Selected runes

    /// <summary>
    /// The hollow between compass and life flask, in which the selected runes lie.
    ///
    /// Measured on the main screen (see the removed tool UWMainCropDump): the recessed area reaches
    /// from X 174 to 219 and from Y 136 to 153. Three rune tiles of 14 pixels each fit
    /// exactly with a spacing of 15 - the last one ends on the right edge.
    /// </summary>
    private const float SelectedRuneLeft = 176f;

    private const float SelectedRuneTop = 138f;

    private const float SelectedRunePitch = 15f;

    /// <summary>No more than three: the fourth pushes out the left one (per user
    /// on the original, 2026-09-03). Matches the three slots in the save game (0x47-0x49).
    /// </summary>
    public const int SelectedRuneCount = 3;

    /// <summary>The green-marked strip at the bottom of the shelf clears the selection. The WHOLE
    /// strip is clickable, not only the two symbols on it (tested per user in the original).
    /// Position estimated on the panel picture - below the last compartment row, which
    /// ends at Y 92.</summary>
    private const float RuneClearTop = 93f;

    private const float RuneClearBottom = 110f;

    private readonly System.Collections.Generic.List<int> mOSelectedRunes =
        new System.Collections.Generic.List<int>();

    private RawImage[] mSelectedRuneImages;

    /// <summary>The selected runes, from left to right. Read by UWSavegameWriter for the
    /// save game.</summary>
    public System.Collections.Generic.IReadOnlyList<int> SelectedRunes
    {
        get { return mOSelectedRunes; }
    }

    /// <summary>
    /// Empties rune shelf and casting hollow and redraws both - for Armageddon
    /// (see UWMiscSpell). The bag itself lives in the player data
    /// (UWPlayerData.ClearRunes), the selection by contrast here in the display.
    /// </summary>
    public void ClearRuneShelf()
    {
        mOSelectedRunes.Clear();

        fRefreshSelectedRunes();
        mOUi.Panel.fRefreshRunes();
    }

    /// <summary>Puts the given runes into the casting hollow, replacing what lay there - for the
    /// help window's spell list (a click equips a spell, per user 2026-09-27). The caller makes
    /// sure the runes are in the bag.</summary>
    internal void PutOnShelf(System.Collections.Generic.IReadOnlyList<int> pORunes)
    {
        mOSelectedRunes.Clear();

        for (int liAt = 0; pORunes != null && liAt < pORunes.Count && mOSelectedRunes.Count < SelectedRuneCount; liAt++)
            mOSelectedRunes.Add(pORunes[liAt]);

        fRefreshSelectedRunes();
    }

    /// <summary>For the modern rune panel (UWModernRunePanel): a rune into the hollow, appended on
    /// the right - from the fourth on the left one drops out, as on the shelf.</summary>
    internal void AddToShelf(int piRune)
    {
        mOSelectedRunes.Add(piRune);

        while (mOSelectedRunes.Count > SelectedRuneCount)
            mOSelectedRunes.RemoveAt(0);

        fRefreshSelectedRunes();
    }

    /// <summary>For the modern rune panel: the right button on a rune looks at it.</summary>
    internal void LookAtRune(int piRune)
    {
        fLookAtRune(piRune);
    }

    /// <summary>For the modern rune panel: a click on its hollow casts what lies there.</summary>
    internal void CastShelf()
    {
        fCastSelectedRunes();
    }

    /// <summary>
    /// For the modern action bar (UWModernActionBar): casts a rune sequence by the same rules as
    /// the hollow, without touching what lies there. The caller makes sure the runes are in the
    /// bag.
    /// </summary>
    internal void CastSequence(int piSequence)
    {
        if (mOUi.mOInteraction == null)
            mOUi.mOInteraction = mOUi.GetComponent<Interaction>();

        if (mOUi.mOInteraction == null || mOUi.mOUWData == null || mOUi.mCharacter == null)
            return;

        UWSpellCasting.CastRunes((piSequence >> 10) & 0x1F, (piSequence >> 5) & 0x1F, piSequence & 0x1F, fMakeSpellHost());
        fAfterModernCast();
    }

    /// <summary>
    /// THE MODERN SCHEME'S AIM (per user, 2026-10-04): the right button switches the pointer
    /// there, so the LEFT button gives the target (Interaction.fUpdateSpellTargeting). By the
    /// POINTER'S STATE: locked, a projectile flies at once towards the crosshair - one aims with
    /// the view already (Interaction.FirePendingSpell); free, it waits for the click and flies
    /// there, the original's target pointer showing - which also keeps the original's way of
    /// preparing a spell and loosing it later (the mana is paid on release). A spell that waits
    /// for a target (Open, Remove Trap, Name Enchantment) always waits: on the world, or on a bag
    /// slot (UWModernBags). Escape lets either go.
    /// </summary>
    private void fAfterModernCast()
    {
        if (mOUi.mOInteraction != null)
            mOUi.mOInteraction.AfterModernCast();
    }

    /// <summary>Clicks on the rune shelf: left selects a rune, right looks at it, and
    /// the strip at the bottom clears the selection.</summary>
    internal void fUpdateRuneClicks()
    {
        if (mOUi.Panel.mePanelSide != UWHudPanel.PanelSideEnum.Runes || mOUi.Panel.miPanelStep >= 0 || mOUi.Panel.mRuneRoot == null)
            return;

        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        if (lOMouse == null)
            return;

        bool lbLeft = UWMouseButtons.LeftPressed;
        bool lbRight = UWMouseButtons.RightPressed;

        if (!lbLeft && !lbRight)
            return;

        Vector2 lOPosition = lOMouse.position.ReadValue();

        // The clear strip first: it lies below the compartments and does not overlap
        // with them.
        if (lbLeft && fIsInRuneClearArea(lOPosition))
        {
            mOSelectedRunes.Clear();
            fRefreshSelectedRunes();

            return;
        }

        int liRune = fGetRuneUnderMouse(lOPosition);

        if (liRune < 0)
            return;

        if (lbRight)
        {
            fLookAtRune(liRune);

            return;
        }

        // Appended on the right; from the fourth on the left one drops out.
        mOSelectedRunes.Add(liRune);

        while (mOSelectedRunes.Count > SelectedRuneCount)
            mOSelectedRunes.RemoveAt(0);

        fRefreshSelectedRunes();
    }

    /// <summary>Which rune lies under the pointer? Only compartments that really hold
    /// one - RectangleContainsScreenPoint also hits disabled pictures.</summary>
    private int fGetRuneUnderMouse(Vector2 pOScreenPos)
    {
        if (mOUi.Panel.mRuneSlotImages == null)
            return -1;

        for (int liRune = 0; liRune < mOUi.Panel.mRuneSlotImages.Length; liRune++)
        {
            if (mOUi.Panel.mRuneSlotImages[liRune] == null || !mOUi.Panel.mRuneSlotImages[liRune].enabled)
                continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(
                mOUi.Panel.mRuneSlotImages[liRune].rectTransform, pOScreenPos))
                return liRune;
        }

        return -1;
    }

    /// <summary>Is the pointer on the clear strip? Computed in panel coordinates,
    /// because the strip has no hit area of its own.</summary>
    private bool fIsInRuneClearArea(Vector2 pOScreenPos)
    {
        if (mOUi.Panel.mRuneRoot == null)
            return false;

        Vector2 lOLocal;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mOUi.Panel.mRuneRoot, pOScreenPos, null, out lOLocal))
            return false;

        // mRuneRoot has its pivot at top left, Y runs negative downwards.
        float lfY = -lOLocal.y;

        return lOLocal.x >= 0f && lOLocal.x <= UWHudPanel.PanelWidth
            && lfY >= RuneClearTop && lfY <= RuneClearBottom;
    }

    /// <summary>Right click on a rune: the same look message as for an item
    /// in the backpack. For that an object of the matching id is built - no real objects
    /// lie on the shelf, only the ownership state from the bit field.</summary>
    private void fLookAtRune(int piRune)
    {
        if (mOUi.mOInteraction == null)
            mOUi.mOInteraction = mOUi.GetComponent<Interaction>();

        if (mOUi.mOInteraction == null)
            return;

        UWObject lORune = new UWObject((ushort)(UWPlayerData.FirstRuneObjectId + piRune));

        // Full quality, so that no condition word slips into the message.
        lORune.Quality = 63;

        string lsMessage = mOUi.mOInteraction.DescribeInventoryItem(lORune);

        if (!string.IsNullOrEmpty(lsMessage))
            mOUi.mOInteraction.AddMessage(lsMessage);
    }

    /// <summary>Draws the selected runes into the hollow. The pictures hang on the game frame,
    /// NOT on the rotation node - the hollow belongs to the main screen and does not rotate along.</summary>
    internal void fRefreshSelectedRunes()
    {
        if (mOUi.mGameFrame == null || mOUi.mOUWData == null)
            return;

        if (mSelectedRuneImages == null)
            mSelectedRuneImages = new RawImage[SelectedRuneCount];

        for (int liSlot = 0; liSlot < mSelectedRuneImages.Length; liSlot++)
        {
            if (mSelectedRuneImages[liSlot] == null)
            {
                GameObject lOObject = new GameObject("SelectedRune" + liSlot,
                    typeof(RectTransform), typeof(RawImage));
                lOObject.transform.SetParent(mOUi.mGameFrame, false);

                RectTransform lORect = (RectTransform)lOObject.transform;
                lORect.anchorMin = new Vector2(0f, 1f);
                lORect.anchorMax = new Vector2(0f, 1f);
                lORect.pivot = new Vector2(0f, 1f);
                lORect.anchoredPosition = new Vector2(
                    SelectedRuneLeft + (liSlot * SelectedRunePitch), -SelectedRuneTop);

                mSelectedRuneImages[liSlot] = lOObject.GetComponent<RawImage>();
                mSelectedRuneImages[liSlot].raycastTarget = false;

                // Below the dragons: the right dragon's head lies over the rune hollow (per user,
                // 2026-09-14). The runes are created later than the dragons, so they are moved in
                // front of them in the drawing order.
                if (mOUi.mODragons != null)
                    lOObject.transform.SetSiblingIndex(mOUi.mODragons.FirstSiblingIndex);
            }

            RawImage lOImage = mSelectedRuneImages[liSlot];

            if (liSlot >= mOSelectedRunes.Count)
            {
                lOImage.enabled = false;
                continue;
            }

            UWTexture lOSource = mOUi.mOUWData.Textures.GetTextureByType(
                UWTexture.TextureTypes.OBJECTS,
                UWPlayerData.FirstRuneObjectId + mOSelectedRunes[liSlot]);

            if (lOSource == null)
            {
                lOImage.enabled = false;
                continue;
            }

            UWIconPalette.Apply(lOImage);

            lOImage.texture = UWIconTextureBuilder.Build(lOSource, mOUi.TextureFilterMode);
            ((RectTransform)lOImage.transform).sizeDelta =
                new Vector2(lOSource.Width, lOSource.Height);
            lOImage.enabled = true;
        }
    }

    /// <summary>
    /// A left click into the hollow casts the selected runes - see fCastSelectedRunes. The
    /// recessed area itself is the hit area, not the runes in it: anywhere inside triggers,
    /// also on an empty slot and with an empty hollow (tested per user in the original,
    /// 2026-09-03); a right click does nothing. THE AREA IS THE ORIGINAL'S since 2026-09-26
    /// (UWClickRules.RuneShelf, x 176 to 222, rows 139 to 155); ours was drawn two or three
    /// pixels up and left of it.
    /// </summary>
    internal void fUpdateSpellCast()
    {
        // NOT WHILE THE VIEW ROAMS (per user, 2026-09-27: the prepared spell cannot be cast in
        // the original during Roaming Sight). Only the click; whether F8 is refused as well was
        // not tested.
        if (UnityEngine.InputSystem.Mouse.current == null || !UWMouseButtons.LeftPressed
            || UWRoamingSight.IsAnyRunning)
            return;

        Vector2 lOPosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        if (!mOUi.IsPointerInOriginalArea(lOPosition, UWClickRules.RuneShelf, out int _, out int _))
            return;

        fCastSelectedRunes();
    }

    /// <summary>
    /// The engine-free spell rules (UWSpellCasting and the class rules next to it, P3 of the
    /// engine separation, 2026-09-18) see the game through mOUi host - built per cast, because
    /// the level loader and the interaction can change between casts.
    /// </summary>
    private UWSpellHost fMakeSpellHost()
    {
        if (mOUi.mOInteraction == null)
            mOUi.mOInteraction = mOUi.GetComponent<Interaction>();

        mOUi.Map.fEnsureLevelLoader();

        return new UWSpellHost(mOUi.mCharacter, mOUi.mOInteraction, mOUi.Map.mOLevelLoader, mOUi.mOUWData, mOUi);
    }

    /// <summary>F8, the original's cast key: the same handler as a click into the hollow
    /// (PlayerAttemptsSpellCast in its key table).</summary>
    internal void CastByKey()
    {
        fCastSelectedRunes();
    }

    /// <summary>F9, the original's track key: the Detect Monsters search with the player's Track
    /// skill (UWMiscSpellRules.Track).</summary>
    internal void TrackByKey()
    {
        if (mOUi.mCharacter == null || mOUi.mOUWData == null)
            return;

        UWMiscSpellRules.Track(fMakeSpellHost(), mOUi.mCharacter.GetSkill(UWPlayerData.Skill.Track));
    }

    /// <summary>Casts the selected runes - the rules (lookup, circle, mana, skill check,
    /// no-magic check, backfire, effect) are in UWSpellCasting.CastRunes.</summary>
    private void fCastSelectedRunes()
    {
        if (mOUi.mOInteraction == null)
            mOUi.mOInteraction = mOUi.GetComponent<Interaction>();

        if (mOUi.mOInteraction == null || mOUi.mOUWData == null || mOUi.mCharacter == null)
            return;

        UWSpellCasting.CastRunes(fGetSelectedRune(0), fGetSelectedRune(1), fGetSelectedRune(2), fMakeSpellHost());
        fAfterModernCast();
    }

    /// <summary>
    /// Casts the spell of an item - a wand, for example. Without the rules of rune casting:
    /// no circle, no mana, no skill check (UWSpellCasting.CastFromObject).
    /// </summary>
    /// <param name="pbFireImmediately">For a projectile spell send it off immediately, instead of
    /// waiting for another click - the click that applied the wand already gives the
    /// direction.</param>
    /// <returns>True if mOUi spell exists and was cast.</returns>
    public bool CastSpellFromObject(int piSpellIndex, bool pbFireImmediately)
    {
        return UWSpellCasting.CastFromObject(piSpellIndex, pbFireImmediately, fMakeSpellHost());
    }

    /// <summary>Casts a spell via its classes instead of via its place in the spell list -
    /// for the a_spelltrap (UWSpellCasting.CastByClass).</summary>
    public bool CastSpellByClass(int piMajorClass, int piMinorClass)
    {
        return UWSpellCasting.CastByClass(piMajorClass, piMinorClass, fMakeSpellHost());
    }

    /// <summary>For the spell host: the three icons show the active effects anew.</summary>
    public void RefreshSpellIcons()
    {
        fRefreshSpellIcons();
    }

    /// <summary>
    /// Takes over the three spell slots from the save game.
    ///
    /// They are at 0x47 in PLAYER.DAT, one byte per slot, with 0x24 for empty. Whoever leaves
    /// the game with selected runes finds them in the hollow again on loading -
    /// until 2026-09-05 every save game in our version started with empty slots.
    ///
    /// The order matters: the list is filled from left to right, and a
    /// fourth rune pushes out the first.
    /// </summary>
    internal void fLoadSelectedRunes()
    {
        mOSelectedRunes.Clear();

        UWPlayerData lOPlayer = mOUi.mOUWData != null ? mOUi.mOUWData.InitialPlayer : null;

        if (lOPlayer == null)
            return;

        for (int liSlot = 0; liSlot < SelectedRuneCount; liSlot++)
        {
            int liRune = lOPlayer.GetSelectedRune(liSlot);

            if (liRune < 0)
                continue;

            mOSelectedRunes.Add(liRune);
        }
    }

    /// <summary>The rune at the given slot, or the empty value.</summary>
    private int fGetSelectedRune(int piSlot)
    {
        return piSlot >= 0 && piSlot < mOSelectedRunes.Count
            ? mOSelectedRunes[piSlot]
            : UWRunicMagic.EmptyRune;
    }

    /// <summary>
    /// The icons of the active spells, left of the compass.
    ///
    /// POSITION computed back from the reference scene: it draws the screen at factor 4
    /// with origin zero - checked by the stats page coming out at 236/7 with it,
    /// our own measurement. The three slots lie there at 340, 276 and 212, so at 85,
    /// 69 and 53, all at height 138.
    ///
    /// The FIRST spell lies on the right, further ones attach to the left.
    /// </summary>
    private const float SpellIconRight = 85f;

    private const float SpellIconTop = 138f;

    private const float SpellIconPitch = 16f;

    private RawImage[] mSpellIconImages;

    /// <summary>Redraws the icons when something has changed about the active spells.
    /// Needed because they also expire by themselves and not only on casting.</summary>
    internal void fRefreshSpellIconsIfChanged()
    {
        if (mOUi.mCharacter == null || miSpellIconVersion == mOUi.mCharacter.ActiveSpellVersion)
            return;

        miSpellIconVersion = mOUi.mCharacter.ActiveSpellVersion;

        fRefreshSpellIcons();
    }

    private int miSpellIconVersion = -1;

    private void fRefreshSpellIcons()
    {
        if (mOUi.mGameFrame == null || mOUi.mOUWData == null || mOUi.mCharacter == null)
            return;

        if (mSpellIconImages == null)
            mSpellIconImages = new RawImage[UWCharacter.MaxActiveSpells];

        for (int liSlot = 0; liSlot < mSpellIconImages.Length; liSlot++)
        {
            if (mSpellIconImages[liSlot] == null)
            {
                GameObject lOObject = new GameObject("SpellIcon" + liSlot,
                    typeof(RectTransform), typeof(RawImage));
                lOObject.transform.SetParent(mOUi.mGameFrame, false);

                RectTransform lORect = (RectTransform)lOObject.transform;
                lORect.anchorMin = new Vector2(0f, 1f);
                lORect.anchorMax = new Vector2(0f, 1f);
                lORect.pivot = new Vector2(0f, 1f);
                lORect.anchoredPosition = new Vector2(
                    SpellIconRight - (liSlot * SpellIconPitch), -SpellIconTop);

                mSpellIconImages[liSlot] = lOObject.GetComponent<RawImage>();
                mSpellIconImages[liSlot].raycastTarget = false;
            }

            RawImage lOImage = mSpellIconImages[liSlot];

            if (liSlot >= mOUi.mCharacter.ActiveSpells.Count)
            {
                lOImage.enabled = false;
                continue;
            }

            UWActiveSpellEffect lOSpell = mOUi.mCharacter.ActiveSpells[liSlot];

            int liIcon = UWRunicMagic.GetIconIndex(lOSpell.MajorClass, lOSpell.MinorClass);

            UWTexture lOSource = liIcon >= UWRunicMagic.NoIcon
                ? null
                : mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.SPELLS, liIcon);

            if (lOSource == null)
            {

                lOImage.enabled = false;
                continue;
            }

            // The torch icon of In Lor flickers like the torch itself - that is now done by
            // the shader via the rotation table, previously UWCycledIcon pre-baked the steps.
            UWIconPalette.Apply(lOImage);

            lOImage.texture = UWIconTextureBuilder.Build(lOSource, mOUi.TextureFilterMode);
            ((RectTransform)lOImage.transform).sizeDelta =
                new Vector2(lOSource.Width, lOSource.Height);
            lOImage.enabled = true;
        }
    }

    /// <summary>The two flasks report their level. Both buttons do the same (per
    /// user on the original, 2026-09-04).</summary>
    private const int VitalityMessage = 90;

    private const int ManaMessage = 91;

    /// <summary>Between number and maximum there is simply mOUi word. No string entry
    /// exists for it - the reference hard-codes it the same way.</summary>
    private const string OutOfText = " out of ";

    internal void fUpdateFlaskClicks()
    {
        if (mOUi.mCharacter == null || UnityEngine.InputSystem.Mouse.current == null)
            return;

        if (!UWMouseButtons.LeftPressed
            && !UWMouseButtons.RightPressed)
            return;

        // THE ORIGINAL'S AREA since 2026-09-26 (UWClickRules.Flasks and FlaskAt): one area over
        // both flasks and the chain; the chain's part of it turns the panel like the chain's own
        // area (UWHudPanel), and the vitality comes after the poison line.
        Vector2 lOPosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        if (!mOUi.IsPointerInOriginalArea(lOPosition, UWClickRules.Flasks, out int liX, out int liY))
            return;

        switch (UWClickRules.FlaskAt(liX, liY))
        {
            case UWClickRules.FlaskPart.Vitality:
                fDescribePoison(UWClickRules.PoisonDegree(mOUi.mCharacter.Poison));
                fDescribeFlask(VitalityMessage, mOUi.mCharacter.CurrentHP, mOUi.mCharacter.MaxHP);
                break;

            case UWClickRules.FlaskPart.Mana:
                fDescribeFlask(ManaMessage, mOUi.mCharacter.CurrentMana, mOUi.mCharacter.MaxMana);
                break;

            case UWClickRules.FlaskPart.Chain:
                mOUi.Panel.TogglePanelSide();
                break;
        }
    }

    /// <summary>"You are ", the degree, " poisoned." - block 1 in our numbering 92, 85 + degree
    /// and 93, before the vitality line while poisoned (PrintFlaskHealthManaMessage).</summary>
    private const int YouAreMessage = 92;

    private const int FirstPoisonDegreeMessage = 85;

    private const int PoisonedMessage = 93;

    private void fDescribePoison(int piDegree)
    {
        if (piDegree < 0 || mOUi.mOUWData == null)
            return;

        if (mOUi.mOInteraction == null)
            mOUi.mOInteraction = mOUi.GetComponent<Interaction>();

        if (mOUi.mOInteraction == null)
            return;

        try
        {
            mOUi.mOInteraction.AddMessage(
                mOUi.mOUWData.GetGeneralMessage(YouAreMessage).TrimEnd('\r', '\n')
                + mOUi.mOUWData.GetGeneralMessage(FirstPoisonDegreeMessage + piDegree).TrimEnd('\r', '\n')
                + mOUi.mOUWData.GetGeneralMessage(PoisonedMessage).TrimEnd('\r', '\n'));
        }
        catch
        {
            // A missing string entry is not an error - then no message comes.
        }
    }

    /// <summary>Assembles the message from the string start and the two numbers. The
    /// entry already ends with a space.</summary>
    private void fDescribeFlask(int piMessage, float pfCurrent, float pfMaximum)
    {
        if (mOUi.mOInteraction == null)
            mOUi.mOInteraction = mOUi.GetComponent<Interaction>();

        if (mOUi.mOInteraction == null || mOUi.mOUWData == null)
            return;

        try
        {
            string lsStart = mOUi.mOUWData.GetGeneralMessage(piMessage);

            if (string.IsNullOrEmpty(lsStart))
                return;

            mOUi.mOInteraction.AddMessage(string.Format("{0}{1}{2}{3}",
                lsStart.TrimEnd('\r', '\n'),
                Mathf.RoundToInt(pfCurrent), OutOfText, Mathf.RoundToInt(pfMaximum)));
        }
        catch
        {
            // A missing string entry is not an error - then no message comes.
        }
    }

    /// <summary>Click on a spell icon: left ends the spell, right names it including
    /// its state.</summary>
    internal void fUpdateSpellIconClicks()
    {
        if (mSpellIconImages == null || mOUi.mCharacter == null
            || UnityEngine.InputSystem.Mouse.current == null)
            return;

        bool lbLeft = UWMouseButtons.LeftPressed;
        bool lbRight = UWMouseButtons.RightPressed;

        if (!lbLeft && !lbRight)
            return;

        // THE ORIGINAL'S AREA since 2026-09-26 (UWClickRules.ActiveSpells and
        // ActiveSpellSlotAt): 16 columns per slot from the right, the first slot at x 84 to 99 -
        // the pictures sat one column right of that.
        Vector2 lOPosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        if (!mOUi.IsPointerInOriginalArea(lOPosition, UWClickRules.ActiveSpells, out int liX, out int _))
            return;

        int liSlot = UWClickRules.ActiveSpellSlotAt(liX);

        if (liSlot < 0 || liSlot >= mOUi.mCharacter.ActiveSpells.Count)
            return;

        if (lbLeft)
            mOUi.mCharacter.CancelActiveSpell(liSlot);
        else
            fDescribeActiveSpell(liSlot);

        fRefreshSpellIcons();
    }

    /// <summary>Names the active spell and how stable it still is. The name comes by
    /// the ICON number from block 6, the state from block 1 from 138 on.</summary>
    private void fDescribeActiveSpell(int piSlot)
    {
        if (mOUi.mOInteraction == null)
            mOUi.mOInteraction = mOUi.GetComponent<Interaction>();

        string lsText = ActiveSpellText(piSlot);

        if (mOUi.mOInteraction != null && !string.IsNullOrEmpty(lsText))
            mOUi.mOInteraction.AddMessage(lsText);
    }

    /// <summary>For the modern HUD's active spells (UWModernHud): the left button names it in the
    /// messages, the right button ends it - the classic page's two the other way round.</summary>
    internal void DescribeActiveSpell(int piSlot)
    {
        fDescribeActiveSpell(piSlot);
    }

    internal void CancelActiveSpell(int piSlot)
    {
        if (mOUi.mCharacter == null || piSlot < 0 || piSlot >= mOUi.mCharacter.ActiveSpells.Count)
            return;

        mOUi.mCharacter.CancelActiveSpell(piSlot);
        fRefreshSpellIcons();
    }

    /// <summary>The name of an active spell and how stable it still is ("Light is stable"), empty
    /// for none - the message of a click, and the modern HUD's hover text.</summary>
    internal string ActiveSpellText(int piSlot)
    {
        if (mOUi.mCharacter == null || piSlot < 0 || piSlot >= mOUi.mCharacter.ActiveSpells.Count)
            return string.Empty;

        UWActiveSpellEffect lOSpell = mOUi.mCharacter.ActiveSpells[piSlot];

        int liIcon = UWRunicMagic.GetIconIndex(lOSpell.MajorClass, lOSpell.MinorClass);

        string lsName = UWRunicMagic.GetEffectName(liIcon, mOUi.mOUWData.Strings);

        if (string.IsNullOrEmpty(lsName))
            return string.Empty;

        // 138 " is nearly done", 139 " is unstable", 140 " is stable".
        int liStateMessage = 138;

        if (lOSpell.Stability >= 2)
            liStateMessage = lOSpell.Stability <= 0xA ? 139 : 140;

        string lsState = string.Empty;

        try
        {
            lsState = mOUi.mOUWData.GetGeneralMessage(liStateMessage).TrimEnd('\r', '\n');
        }
        catch
        {
            // Without state text the name stands alone.
        }

        return lsName + lsState;
    }

    /// <summary>Pointer while a projectile spell waits for its target - number 9, as in
    /// the reference.</summary>
    internal const int SpellTargetCursor = 9;

    /// <summary>The bluish cross with which you click the target of a class 7 spell
    /// (described per user, 2026-09-07). The reference uses the same
    /// pointer 10 for it (uimanager.mousecursor.SetCursorToCursor(10)).</summary>
    internal const int TargetSpellCursor = 10;
}
