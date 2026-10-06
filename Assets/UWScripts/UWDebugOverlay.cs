using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// Overlay with location and look target. Meant for screenshots: it names the
/// tile the player stands on and what lies under the crosshair - for
/// geometry the tile with its texture number, for objects the id and texture source.
///
/// This makes it possible to find a conspicuous spot again later.
/// </summary>
public class UWDebugOverlay : MonoBehaviour
{
    /// <summary>Hidden until F1 is pressed - after loading a game the overlay must not cover
    /// the picture (per user, 2026-09-14). The component is added at runtime, so this default
    /// is what every start gets.</summary>
    [SerializeField]
    private bool mbVisible;

    [SerializeField]
    [Tooltip("Range of the ray for the look target.")]
    private float mfLookRange = 4000f;

    private UWLevelLoader mOLoader;
    private GUIStyle mOStyle;
    private string msLookInfo = string.Empty;
    private int miLastFrame = -1;
    private UWControls mControls;
    private UWInventory mOInventory;

    public void Init(UWLevelLoader pOLoader)
    {
        mOLoader = pOLoader;
    }

    private void Awake()
    {
        mControls = new UWControls();
        mControls.Enable();
    }

    private void OnDestroy()
    {
        mControls.Dispose();
    }

    private void Update()
    {
        if (mControls.Debug.ToggleOverlay.WasPressedThisFrame())
            mbVisible = !mbVisible;
    }

    private void OnGUI()
    {
        if (!mbVisible || mOLoader == null || mOLoader.UWDataImporter == null)
            return;

        // Draw in front of all other overlays.
        GUI.depth = -1000;

        if (mOStyle == null)
        {
            mOStyle = new GUIStyle(GUI.skin.label);
            mOStyle.fontSize = 14;
            mOStyle.normal.textColor = Color.white;
            mOStyle.richText = false;
        }

        // Recompute only once per frame, OnGUI runs several times.
        if (miLastFrame != Time.frameCount)
        {
            miLastFrame = Time.frameCount;
            msLookInfo = fDescribeLookTarget();
        }

        string lsText = fBuildText();

        Vector2 lOSize = mOStyle.CalcSize(new GUIContent(lsText));
        Rect lORect = new Rect(8f, 8f, lOSize.x + 16f, lOSize.y + 12f);

        Color lOPrevious = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(lORect, Texture2D.whiteTexture);
        GUI.color = lOPrevious;

        GUI.Label(new Rect(lORect.x + 8f, lORect.y + 6f, lORect.width, lORect.height), lsText, mOStyle);
    }

    private string fBuildText()
    {
        System.Text.StringBuilder lOText = new System.Text.StringBuilder();

        // CREATURES ONLY: while a creature's behaviour is being hunted down, everything else
        // is in the way - position, backpack, game variables, trap log (per user,
        // 2026-09-16: "and clearer, maybe just the creature data on F1 for a while"). The
        // switch sits next to the trace it belongs to, UWSettings.CritterTrace.
        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        if (lOSettings != null && lOSettings.CritterTrace)
        {
            lOText.AppendFormat("Level {0}", mOLoader.CurrentLevelIndex + 1);
            fAppendNearestCritter(lOText);
            lOText.AppendFormat("\n[{0}] hides", mControls.Debug.ToggleOverlay.GetBindingDisplayString());

            return lOText.ToString();
        }

        Transform lOPlayer = fGetPlayerTransform();

        lOText.AppendFormat("Level {0}", mOLoader.CurrentLevelIndex + 1);

        // Backpack fill level, originally added for testing the inventory setup (step 1/2 of the
        // inventory plan).
        if (mOInventory == null)
            mOInventory = UWScene.Inventory;

        if (mOInventory != null)
        {
            int liCount = 0;

            foreach (UWDataImport.UWData.UWObject lOItem in mOInventory.Backpack)
                if (lOItem != null)
                    liCount++;

            lOText.AppendFormat("   Backpack {0}/{1}", liCount, UWInventory.BackpackSize);
        }

        if (lOPlayer != null)
        {
            Vector3 lOPosition = lOPlayer.position;

            int liTileX = Mathf.FloorToInt((lOPosition.x + UWLevelMeshBuilder.TileHalfSize) / UWLevelMeshBuilder.TileSpacing);
            int liTileZ = Mathf.FloorToInt((lOPosition.z + UWLevelMeshBuilder.TileHalfSize) / UWLevelMeshBuilder.TileSpacing);

            lOText.AppendFormat("   Tile ({0},{1})", liTileX, liTileZ);
            lOText.AppendFormat("\nPosition {0:F0} / {1:F0} / {2:F0}", lOPosition.x, lOPosition.y, lOPosition.z);

            UWTile lOTile = fGetTile(liTileX, liTileZ);

            if (lOTile != null)
                lOText.AppendFormat("   {0}, floor {1}", lOTile.TileType, lOTile.FloorHeight);

            fAppendCharacter(lOText);
            fAppendTileTraps(lOText, lOTile);

            Camera lOCamera = fGetActiveCamera();

            if (lOCamera != null)
            {
                float lfYaw = lOCamera.transform.eulerAngles.y;
                lOText.AppendFormat("\nHeading {0:F0} degrees ({1})", lfYaw, fGetCompass(lfYaw));

                fAppendOriginalUnits(lOText, lOPosition, lfYaw);
            }
        }

        lOText.Append("\n");
        lOText.Append(msLookInfo);

        fAppendNearestCritter(lOText);
        fAppendGameVariables(lOText);
        fAppendTrapLog(lOText);

        lOText.AppendFormat("\n[{0}] hides", mControls.Debug.ToggleOverlay.GetBindingDisplayString());

        return lOText.ToString();
    }

    /// <summary>
    /// What the nearest creature is currently up to.
    ///
    /// WHY IT IS HERE: from the outside you can read almost nothing off a creature.
    /// Whether a fire elemental throws no fireball because it never enters the spell branch,
    /// because its roll fails, or because the projectile does not appear, you cannot tell
    /// by looking at it - and when in doubt you guess (2026-09-10, exactly this case).
    ///
    /// Shown are the numbers from the creature table that determine its behaviour,
    /// and what it is currently doing with them.
    /// </summary>
    private void fAppendNearestCritter(System.Text.StringBuilder pOText)
    {
        // THE LOOKED-AT CREATURE FIRST. The nearest enemy is not necessarily the one
        // that matters right now - with the fire elemental the line showed a spider instead,
        // which happened to be closer (per user, 2026-09-10).
        UWCritter[] lOCritters = UWScene.FindCritters();

        if (lOCritters == null || lOCritters.Length == 0)
        {
            pOText.Append("\nEnemy: none on this level");

            return;
        }

        UWCritter lOShown = fGetLookedAtCritter();
        bool lbLookedAt = lOShown != null;
        float lfDistanceToShown = float.MaxValue;

        foreach (UWCritter lOCritter in lOCritters)
        {
            if (lOCritter == null)
                continue;

            // MEASURED FROM THE EYE, not from this component's own object - the overlay hangs
            // on a UI object that does not move with the player, which is why a lurker
            // standing right in front of the player was reported as 31 tiles away (per user
            // with a screenshot, 2026-09-16). Horizontally, like the creatures themselves
            // measure (UWCritter.fUpdatePlayerVector), so a shore above the water does not
            // inflate the number.
            Camera lOEye = fGetActiveCamera();
            Vector3 lOFrom = lOEye != null ? lOEye.transform.position : transform.position;
            Vector3 lOApart = lOCritter.transform.position - lOFrom;
            lOApart.y = 0f;

            float lfDistance = lOApart.magnitude;

            if (lbLookedAt)
            {
                if (lOCritter != lOShown)
                    continue;

                lfDistanceToShown = lfDistance;

                break;
            }

            if (lfDistance >= lfDistanceToShown)
                continue;

            lfDistanceToShown = lfDistance;
            lOShown = lOCritter;
        }

        if (lOShown == null)
            return;

        pOText.AppendFormat("\nEnemy {0} ({1} of {2})  {3:F0} away ({4:F1} tiles)  {5}  attack {6}",
            lOShown.name, lbLookedAt ? "looked at" : "nearest", lOCritters.Length,
            lfDistanceToShown, lfDistanceToShown / UWLevelMeshBuilder.TileSpacing,
            lOShown.State, lOShown.AttackKind);

        pOText.Append(lOShown.DescribeCombatStats());
        pOText.Append(lOShown.GetTrace());
    }

    /// <summary>The creature under the crosshair, if there is one.</summary>
    private UWCritter fGetLookedAtCritter()
    {
        Camera lOCamera = fGetActiveCamera();

        if (lOCamera == null)
            return null;

        Ray lORay = lOCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        RaycastHit lOHit;

        if (!Physics.Raycast(lORay, out lOHit, mfLookRange))
            return null;

        return lOHit.collider.GetComponentInParent<UWCritter>();
    }

    /// <summary>Hit points, mana and poison - so a hit can be attributed.
    /// Without this you see the flask twitch while testing, but no number.</summary>
    private void fAppendCharacter(System.Text.StringBuilder pOText)
    {
        if (mOCharacter == null)
            mOCharacter = UWScene.Character;

        if (mOCharacter == null)
            return;

        pOText.AppendFormat("\nHealth {0}/{1}   Mana {2:F0}/{3:F0}",
            mOCharacter.CurrentHP, mOCharacter.MaxHP,
            mOCharacter.CurrentMana, mOCharacter.MaxMana);

        if (mOCharacter.Poison > 0)
            pOText.AppendFormat("   POISONED {0}", mOCharacter.Poison);

        // WHERE THE LAST HIT CAME FROM. The damage type reveals the source: physical is
        // melee, fire a fireball or lava, missile an arrow, "-" something
        // untyped like starvation or a trap.
        if (mOCharacter.LastDamageTime >= 0f)
        {
            string lsType = mOCharacter.LastDamageType == UWDamageTypes.None
                ? "untyped"
                : fDescribeDamageTypes(mOCharacter.LastDamageType);

            pOText.AppendFormat("\nLast hit {0} ({1}) {2:F1}s ago",
                mOCharacter.LastDamage, lsType, Time.time - mOCharacter.LastDamageTime);
        }

        fAppendArmour(pOText);
    }

    /// <summary>
    /// Armour and enchantments of the equipment worn.
    ///
    /// WHY IT IS HERE: the original shows NO NUMBER for any of this (per user,
    /// 2026-09-09). Looking at an item tells you THAT it is enchanted, and with enough
    /// lore also what the spell is called - but neither the armour value nor the strength of
    /// the enchantment (see UWLoreCheck). When rebuilding, those are the decisive numbers,
    /// and without a display you cannot tell whether a hit failed because of the armour
    /// or for another reason.
    ///
    /// THE TWO COLUMNS ARE DIFFERENT THINGS (see UWArmourProtection): armour
    /// subtracts DAMAGE, protection lowers the CHANCE TO HIT - and it only counts on head and
    /// body.
    /// </summary>
    private void fAppendArmour(System.Text.StringBuilder pOText)
    {
        mOCharacter.RefreshStatus();

        pOText.AppendFormat("\nArmour  body {0}/{1}  hands {2}/{3}  legs {4}/{5}  head {6}/{7}   (armour/protection)",
            mOCharacter.GetArmourAt(UWArmourProtection.PartBody),
            mOCharacter.GetProtectionAt(UWArmourProtection.PartBody),
            mOCharacter.GetArmourAt(UWArmourProtection.PartHands),
            mOCharacter.GetProtectionAt(UWArmourProtection.PartHands),
            mOCharacter.GetArmourAt(UWArmourProtection.PartLegs),
            mOCharacter.GetProtectionAt(UWArmourProtection.PartLegs),
            mOCharacter.GetArmourAt(UWArmourProtection.PartHead),
            mOCharacter.GetProtectionAt(UWArmourProtection.PartHead));

        // Both values, because they differ: the skill is what the status page shows, the check
        // value is what a creature rolls against (skill + half the weapon skill, see
        // UWPlayerCritterRow).
        pOText.AppendFormat("   Defence {0} (skill {1} + half of {2})", mOCharacter.DefenceCheck,
            mOCharacter.Defence, mOCharacter.DefenceWeaponSkill);

        // The resistance of spell class 2 is already included in the four armour values above.
        // It is still shown separately here, because otherwise you cannot see whether a high
        // number comes from the armour or from the spell.
        if (mOCharacter.DamageResistance > 0)
            pOText.AppendFormat("   Resistance +{0}", mOCharacter.DamageResistance);

        // What the character is currently immune to. Since 2026-09-10 this also takes effect:
        // the damage is deflected entirely, see UWCharacter.ApplyDamage.
        if (mOCharacter.DamageTypeProof != 0)
            pOText.AppendFormat("   Immune to {0}",
                fDescribeDamageTypes(mOCharacter.DamageTypeProof));

        if (mOCharacter.StealthBonus != 0)
            pOText.AppendFormat("   Stealth 0x{0:X}", mOCharacter.StealthBonus);

        if (mOCharacter.HasDragonSkinBoots)
            pOText.Append("   Dragon skin boots (lava-proof)");

        if (mOInventory == null)
            return;

        foreach (UWArmorItemMap.BodySlot leSlot in msArmourSlots)
        {
            UWDataImport.UWData.UWObject lOItem = mOInventory.GetEquipped(leSlot);

            if (lOItem == null)
                continue;

            int liMajor;
            int liMinor;

            if (!UWArmourProtection.TryGetWearableEnchantment(lOItem, out liMajor, out liMinor))
                continue;

            pOText.AppendFormat("\n  {0}: object {1}, spell {2}-{3:X}", leSlot, lOItem.ID, liMajor, liMinor);

            int liProtection = UWArmourProtection.GetProtectionEnchantment(lOItem);

            if (liProtection > 0)
                pOText.AppendFormat("  Protection +{0}", liProtection);
        }
    }

    /// <summary>
    /// A resistance byte in words - the magic level as a number, the rest as names.
    ///
    /// The bit layout is in UWDamageTypes; the names are those of the spells that
    /// set them (Flameproof, Poison Resistance, Missile Protection).
    /// </summary>
    private static string fDescribeDamageTypes(int piBits)
    {
        System.Text.StringBuilder lOText = new System.Text.StringBuilder();

        int liMagicLevel = piBits & 0x3;

        if (liMagicLevel > 0)
            lOText.AppendFormat("Magic {0}/3", liMagicLevel);

        fAppendDamageTypeName(lOText, piBits, UWDamageTypes.Physical, "Physical");
        fAppendDamageTypeName(lOText, piBits, UWDamageTypes.PlainFire, "Fire");
        fAppendDamageTypeName(lOText, piBits, UWDamageTypes.Poison, "Poison");
        fAppendDamageTypeName(lOText, piBits, 0x20, "Cold");
        fAppendDamageTypeName(lOText, piBits, UWDamageTypes.Missile, "Missile");

        return lOText.ToString();
    }

    private static void fAppendDamageTypeName(System.Text.StringBuilder pOText, int piBits,
        int piMask, string psName)
    {
        if ((piBits & piMask) == 0)
            return;

        if (pOText.Length > 0)
            pOText.Append(", ");

        pOText.Append(psName);
    }

    /// <summary>The slots from which an enchantment takes effect: the five armour pieces and
    /// the two rings. The hands are left out on purpose - nothing takes effect from there as long
    /// as you only hold it (see UWArmourProtection).</summary>
    private static readonly UWArmorItemMap.BodySlot[] msArmourSlots =
    {
        UWArmorItemMap.BodySlot.Helmet,
        UWArmorItemMap.BodySlot.Chest,
        UWArmorItemMap.BodySlot.Gloves,
        UWArmorItemMap.BodySlot.Legs,
        UWArmorItemMap.BodySlot.Boots,
        UWArmorItemMap.BodySlot.LeftRing,
        UWArmorItemMap.BodySlot.RightRing
    };

    /// <summary>
    /// Which traps and triggers lie on the player's own tile.
    ///
    /// Major class 6 is objects 384 to 447 - traps and triggers. They have no
    /// representation in the world, so you cannot tell that you are standing on them.
    /// </summary>
    private void fAppendTileTraps(System.Text.StringBuilder pOText, UWTile pOTile)
    {
        if (pOTile == null || pOTile.ObjectsInTile == null || mOLoader == null)
            return;

        foreach (UWDataImport.UWData.UWObject lOObject in pOTile.ObjectsInTile)
        {
            if (lOObject == null || lOObject.ID < 384 || lOObject.ID > 447)
                continue;

            pOText.AppendFormat("\n  on this tile: {0} (q {1}, own {2})",
                UWTrapLog.GetName(mOLoader != null ? mOLoader.UWDataImporter : null, lOObject.ID), lOObject.Quality, lOObject.Owner);

            UWDataImport.UWData.UWObject lOLinked = UWDataImport.UWData.UWObjectMechanics.GetLinkedObject(
                lOObject, mOLoader.CurrentLevel.Masterlist);

            if (lOLinked != null && lOLinked.ID >= 384 && lOLinked.ID <= 447)
                pOText.AppendFormat(" -> {0}", UWTrapLog.GetName(mOLoader != null ? mOLoader.UWDataImporter : null, lOLinked.ID));
        }
    }

    /// <summary>
    /// The game variables that are not zero - the state of the puzzles.
    ///
    /// Without them the variable traps reveal nothing: they have no representation in the
    /// world, and yet whether a counter is already at seven decides whether the next
    /// check passes. Only the set ones, so the line stays short.
    /// </summary>
    private void fAppendGameVariables(System.Text.StringBuilder pOText)
    {
        bool lbAny = false;

        foreach (System.Collections.Generic.KeyValuePair<int, int> lOPair in UWGameVariables.All)
        {
            if (lOPair.Value == 0)
                continue;

            if (!lbAny)
            {
                pOText.Append("\nVariables:");
                lbAny = true;
            }

            pOText.AppendFormat(" {0}={1}", lOPair.Key, lOPair.Value);
        }
    }

    /// <summary>
    /// The same spot once more in the ORIGINAL'S UNITS - so that what is in the save game
    /// and what the reference outputs can be compared without calculating.
    ///
    /// Our world scale is a quarter of the original, consistently: tile 64 versus
    /// 256, sub-tile step 8 versus 32, zpos step 2 versus 8, ceiling height 256 versus 1024.
    /// The axes swap as well - the original counts Y towards north, we count Z.
    ///
    /// THIS IS EXACTLY HOW IT IS STORED IN THE SAVE GAME: the fields at 0x54 and 0x56 hold the fine
    /// position X and Y, whose upper byte is the tile, the field at 0x58 holds zpos times eight, and
    /// the one at 0x5A the heading as a full circle in 16 bits. All four can be read here.
    /// </summary>
    private static void fAppendOriginalUnits(System.Text.StringBuilder pOText, Vector3 pOPosition,
        float pfYaw)
    {
        // These are already EXACTLY the values of the four position fields in the save game: the
        // fine position holds the tile in its upper byte, the height field eighths of a
        // zpos step, the heading field the full circle in 16 bits (see
        // UWPlayerData.WritePosition). That is why the field number is shown right next to it.
        int liX = UWViewpoint.WorldToOriginalX(pOPosition.x);
        int liY = UWViewpoint.WorldToOriginalY(pOPosition.z);
        int liZ = UWViewpoint.WorldToOriginalZ(pOPosition.y);

        int liHeading = (ushort)UWViewpoint.DegreesToAngle(pfYaw);

        pOText.AppendFormat("\nOriginal  x {0} (0x54={0:X4})  y {1} (0x56={1:X4})",
            liX, liY);

        pOText.AppendFormat("\n          zpos {0} (0x58={1:X4})  heading {2} (0x5A={2:X4})",
            liZ >> 3, liZ, liHeading);

        // THE MOTION CORE'S OWN VIEW (stage 3, 2026-10-06): the params block, the contact state
        // and what the last step found under and around the player - the floor samples and the
        // collision records (item ids with bottom and top). For a player standing where he
        // should not (per user: on the edge of a water slope a level too high).
        UWPlayerMovement lOMovement = UWScene.PlayerMovement;

        if (lOMovement != null && lOMovement.UsesMotionCore)
        {
            UWDataImport.UWData.UWPlayerMotion lOMotion = lOMovement.Motion;
            UWDataImport.UWData.UWMotionCore lOCore = lOMotion.Core;

            pOText.AppendFormat("\nCore      x {0} y {1} z {2} (fine)  speed {3} vz {4} gravity {5}  contact 0x{6:X}  state {7}{8}",
                lOMotion.Params.X, lOMotion.Params.Y, lOMotion.Params.Z, lOMotion.Params.Speed, lOMotion.Params.Vz,
                lOMotion.Params.Gravity, lOMotion.Params.Contact, lOMotion.CurrentState,
                lOMotion.Precise ? (lOMotion.FineTicks ? "  precise, fine ticks" : "  precise") : string.Empty);
            pOText.AppendFormat("\n          flags 0x{0:X} all 0x{1:X}  centre floor {2}  max floor {3}  records {4} (overlap {5} from {6})",
                lOCore.Flags, lOCore.AllFlags, lOCore.CentreFloor, lOCore.MaxFloor, lOCore.Count & 0xFF, lOCore.Overlap & 0xFF, (sbyte)lOCore.First);

            for (int liAt = 0; liAt < (lOCore.Count & 0xFF) && liAt < lOCore.Records.Length; liAt++)
            {
                UWDataImport.UWData.UWMotionCore.Record lORecord = lOCore.Records[liAt];

                pOText.AppendFormat("\n          record {0}: item 0x{1:X3} index {2} at {3}/{4} z {5}..{6} flags 0x{7:X}",
                    liAt, lORecord.Body.ItemId & 0x1FF, lORecord.Body.Index, lORecord.Dx, lORecord.Dy, lORecord.Bottom, lORecord.Top, lORecord.Flags);
            }
        }
    }

    /// <summary>The most recently triggered traps, with their age in seconds.</summary>
    private void fAppendTrapLog(System.Text.StringBuilder pOText)
    {
        if (UWTrapLog.Entries.Count == 0)
            return;

        pOText.Append("\nTraps:");

        for (int liAt = UWTrapLog.Entries.Count - 1; liAt >= 0; liAt--)
        {
            UWTrapLog.Entry lOEntry = UWTrapLog.Entries[liAt];

            pOText.AppendFormat("\n  {0:F0}s ago  {1}", Time.time - lOEntry.Time, lOEntry.Text);
        }
    }

    private UWCharacter mOCharacter;

    /// <summary>Compass direction in the level's coordinate system: north is +Z.</summary>
    private static string fGetCompass(float pfYaw)
    {
        string[] lsNames = new string[] { "North", "Northeast", "East", "Southeast", "South", "Southwest", "West", "Northwest" };

        int liIndex = Mathf.RoundToInt(pfYaw / 45f) & 7;

        return lsNames[liIndex];
    }

    private Transform fGetPlayerTransform()
    {
        Camera lOCamera = fGetActiveCamera();

        if (lOCamera == null)
            return null;

        // The camera is attached to the player or to the noclip camera; its position is enough
        // for the tile.
        return lOCamera.transform;
    }

    /// <summary>
    /// Camera.main only finds an active camera tagged "MainCamera" - the
    /// noclip camera, however, carries the tag "SpectatorCam" and becomes the only active
    /// camera while noclip is on, with the player camera deactivated. Without
    /// this fallback the overlay shows "no camera" there instead of location and
    /// look target.
    /// </summary>
    private static Camera fGetActiveCamera()
    {
        return UWScene.ActiveCamera;
    }

    private UWTile fGetTile(int piTileX, int piTileZ)
    {
        if (piTileX < 0 || piTileX >= UWLevelMeshBuilder.TilesPerAxis || piTileZ < 0 || piTileZ >= UWLevelMeshBuilder.TilesPerAxis)
            return null;

        UWLevel lOLevel = mOLoader.CurrentLevel;

        if (lOLevel == null)
            return null;

        return lOLevel.TileData[(piTileZ * UWLevelMeshBuilder.TilesPerAxis) + piTileX];
    }

    /// <summary>
    /// Determines what lies in the view direction. For geometry the table in
    /// UWLevelChunk is used to work back which tile was hit; the surface normal
    /// distinguishes floor, ceiling and wall. For objects the id and texture source are
    /// given - exactly the details needed for debugging.
    /// </summary>
    private string fDescribeLookTarget()
    {
        Camera lOCamera = fGetActiveCamera();

        if (lOCamera == null)
            return "Look target: no camera";

        Ray lORay = lOCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit lOHit;

        if (!Physics.Raycast(lORay, out lOHit, mfLookRange))
            return "Look target: nothing";

        UWEntityInfo lOEntity = lOHit.collider.GetComponentInParent<UWEntityInfo>();

        if (lOEntity != null && lOEntity.ObjectData != null)
        {
            UWObject lOObject = lOEntity.ObjectData;

            string lsTexture = "?";

            if (lOObject.Texture != null)
                lsTexture = string.Format("{0} {1}", lOObject.Texture.TextureType, lOObject.Texture.Index);

            return string.Format("Look target: object 0x{0:X4} ({1}), texture {2}\n   tile ({3},{4}), height {5}, heading {6}, {7}",
                lOObject.ID, lOObject.GetCategory(), lsTexture,
                lOObject.TileX, lOObject.TileY, lOObject.ZPos, lOObject.Heading,
                string.IsNullOrEmpty(lOEntity.Description) ? "-" : lOEntity.Description);
        }

        UWLevelChunk lOChunk = lOHit.collider.GetComponent<UWLevelChunk>();

        if (lOChunk != null)
        {
            int liTileIndex = lOChunk.GetTileIndex(lOHit.triangleIndex);

            if (liTileIndex >= 0)
            {
                int liTileX = liTileIndex % UWLevelMeshBuilder.TilesPerAxis;
                int liTileZ = liTileIndex / UWLevelMeshBuilder.TilesPerAxis;

                UWTile lOTile = fGetTile(liTileX, liTileZ);

                float lfUp = Vector3.Dot(lOHit.normal, Vector3.up);

                string lsSurface;
                int liTexture;

                if (lfUp > 0.5f)
                {
                    lsSurface = "floor";
                    liTexture = lOTile != null ? lOTile.TextureFloor : -1;
                }
                else if (lfUp < -0.5f)
                {
                    lsSurface = "ceiling";
                    liTexture = lOTile != null ? lOTile.TextureCeiling : -1;
                }
                else
                {
                    lsSurface = "wall";
                    liTexture = lOTile != null ? lOTile.TextureWall : -1;
                }

                return string.Format("Look target: {0} on tile ({1},{2}), texture {3}, {4}",
                    lsSurface, liTileX, liTileZ, liTexture,
                    lOTile != null ? lOTile.TileType.ToString() : "?");
            }
        }

        if (lOEntity != null)
            return string.Format("Look target: {0} ({1})", lOHit.collider.name, string.IsNullOrEmpty(lOEntity.Description) ? "-" : lOEntity.Description);

        return "Look target: " + lOHit.collider.name;
    }
}
