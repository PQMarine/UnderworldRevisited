using System.Collections.Generic;
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
///
/// ON THE KIT (cleanup 8c, 2026-10-25): this component fills a plain snapshot of the scene once a
/// frame (UWDebugSnapshot - the camera, the character, the tile, the creature, the game variables,
/// the trap log), the engine-free report turns it into sections of rows (UWDebugReport) and the debug
/// panel draws them on the kit (UWDebugPanel), with the debug tools as its second tab (UWDebugTools).
/// Until then one IMGUI text block formatted straight from the scene in OnGUI. Shift+F1 toggles the
/// panel as it toggled the overlay; the game's buttons stay off the panel while the pointer is on it.
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
    private UWControls mControls;
    private UWInventory mOInventory;
    private UWCharacter mOCharacter;
    private UWDebugPanel mOPanel;

    /// <summary>A button went down on the panel: the game stays shut out of the buttons until every
    /// one is up again, so the release does not use, drop or walk anything (as the help's reading
    /// mode does, UWHelpWindow).</summary>
    private bool mbSwallowUntilReleased;

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

        if (mOPanel != null)
            mOPanel.Dispose();

        UWMouseButtons.IsBlockedByDebug = false;
    }

    private void OnDisable()
    {
        mbSwallowUntilReleased = false;
        UWMouseButtons.IsBlockedByDebug = false;

        // The panel's canvas goes dark with the component.
        if (mOPanel != null)
            mOPanel.Draw(false, null, null);
    }

    private void Update()
    {
        // Shift+F1 (UWHotkeys since cleanup step 7.5).
        if (mOKeys == null)
            mOKeys = new OverlayKeys(this);

        UWHotkeys.Route(UWHotkeySite.DebugOverlay, mOKeys);

        fUpdateButtonBlock();
    }

    private OverlayKeys mOKeys;

    /// <summary>The overlay's hotkey (UWHotkeys), on this component's own controls.</summary>
    private sealed class OverlayKeys : UWHotkeyHost
    {
        private readonly UWDebugOverlay mOOverlay;

        public OverlayKeys(UWDebugOverlay pOOverlay)
        {
            mOOverlay = pOOverlay;
        }

        protected override UWControls Controls => mOOverlay.mControls;

        public override bool Gate(UWHotkeyGate peGate, UWHotkeyRow pORow)
        {
            return false;
        }

        public override bool Run(UWHotkeyHandler peHandler)
        {
            if (peHandler != UWHotkeyHandler.ToggleOverlay)
                return false;

            mOOverlay.mbVisible = !mOOverlay.mbVisible;
            return true;
        }
    }

    /// <summary>The game's buttons stay off the panel: while the pointer is on it (as last drawn), and
    /// from a press on it until every button is up (UWMouseButtons.IsBlockedByDebug).</summary>
    private void fUpdateButtonBlock()
    {
        bool lbOver = mOPanel != null && mOPanel.IsShown && UWModernUi.WindowTakesPointer(out Vector2 lOAt) && mOPanel.ScreenRect.Contains(lOAt);
        bool lbPressed = UWPointer.HasMouse && (UWPointer.LeftPressed || UWPointer.RightPressed
            || UWPointer.MiddlePressed);
        bool lbHeld = UWPointer.HasMouse && (UWPointer.LeftHeld || UWPointer.RightHeld || UWPointer.MiddleHeld);

        if (lbOver && lbPressed)
            mbSwallowUntilReleased = true;
        else if (!lbHeld)
            mbSwallowUntilReleased = false;

        UWMouseButtons.IsBlockedByDebug = lbOver || mbSwallowUntilReleased;
    }

    private void LateUpdate()
    {
        bool lbReady = mOLoader != null && mOLoader.UWDataImporter != null;
        UWDebugTools lOTools = UWDebugTools.IsOpen ? UWDebugTools.Instance : null;
        bool lbShown = lbReady && (mbVisible || lOTools != null);

        if (mOPanel == null)
        {
            if (!lbShown)
                return;

            mOPanel = new UWDebugPanel(transform);
        }

        UWDebugReport lOReport = lbShown && lOTools == null ? UWDebugReport.Build(fSnapshot()) : null;

        switch (mOPanel.Draw(lbShown, lOReport, lOTools))
        {
            case UWDebugPanel.ActionEnum.ShowReport:
                mbVisible = true;

                if (UWDebugTools.Instance != null)
                    UWDebugTools.Instance.Close();

                break;

            case UWDebugPanel.ActionEnum.ShowTools:
                if (UWDebugTools.Instance != null)
                    UWDebugTools.Instance.Open();

                break;
        }
    }

    // ------------------------------------------------- The snapshot

    /// <summary>What the report shows this frame, read from the scene as the IMGUI overlay read it
    /// (fBuildText and the methods below it until cleanup 8c).</summary>
    private UWDebugSnapshot fSnapshot()
    {
        UWDebugSnapshot lOSnapshot = new UWDebugSnapshot
        {
            Level = mOLoader.CurrentLevelIndex + 1,
            HideKey = mControls.Debug.ToggleOverlay.GetBindingDisplayString(),
            Now = Time.time
        };

        // The player's rules.json, when this game has one (UWLevelLoader applies it at the start).
        UWRulesReport lORules = UWGameSession.Current.RulesReport;

        if (lORules != null && lORules.Found)
        {
            lOSnapshot.Rules = lORules.Summary;
            lOSnapshot.RulesProblem = lORules.HasProblems;
        }

        // CREATURES ONLY: while a creature's behaviour is being hunted down, everything else
        // is in the way (per user, 2026-09-16). The switch sits next to the trace it belongs to,
        // UWSettings.CritterTrace.
        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        if (lOSettings != null && lOSettings.CritterTrace)
        {
            lOSnapshot.CritterTrace = true;
            fFillCritter(lOSnapshot);

            return lOSnapshot;
        }

        // Backpack fill level, originally added for testing the inventory setup (step 1/2 of the
        // inventory plan).
        if (mOInventory == null)
            mOInventory = UWScene.Inventory;

        if (mOInventory != null)
        {
            int liCount = 0;

            foreach (UWObject lOItem in mOInventory.Backpack)
                if (lOItem != null)
                    liCount++;

            lOSnapshot.HasBackpack = true;
            lOSnapshot.BackpackCount = liCount;
            lOSnapshot.BackpackSize = UWInventory.BackpackSize;
        }

        // The camera is attached to the player or to the noclip camera; its position is enough
        // for the tile.
        Camera lOCamera = fGetActiveCamera();

        if (lOCamera != null)
        {
            Vector3 lOPosition = lOCamera.transform.position;

            lOSnapshot.HasPlace = true;
            lOSnapshot.X = lOPosition.x;
            lOSnapshot.Y = lOPosition.y;
            lOSnapshot.Z = lOPosition.z;
            lOSnapshot.TileX = UWUnits.WorldAxisToTile(lOPosition.x);
            lOSnapshot.TileZ = UWUnits.WorldAxisToTile(lOPosition.z);

            UWTile lOTile = fGetTile(lOSnapshot.TileX, lOSnapshot.TileZ);

            if (lOTile != null)
            {
                lOSnapshot.HasTile = true;
                lOSnapshot.TileType = lOTile.TileType.ToString();
                lOSnapshot.FloorHeight = lOTile.FloorHeight;
            }

            fFillCharacter(lOSnapshot);
            fFillTileTraps(lOSnapshot, lOTile);

            lOSnapshot.Yaw = lOCamera.transform.eulerAngles.y;
            fFillOriginalUnits(lOSnapshot, lOPosition, lOSnapshot.Yaw);
        }

        fFillLookTarget(lOSnapshot.Look, lOCamera);
        fFillCritter(lOSnapshot);

        // The game variables (the report shows the set ones) and the most recently triggered traps.
        foreach (KeyValuePair<int, int> lOPair in UWGameVariables.All)
            lOSnapshot.Variables.Add(lOPair);

        foreach (UWTrapLog.Entry lOEntry in UWTrapLog.Entries)
            lOSnapshot.Traps.Add(lOEntry);

        return lOSnapshot;
    }

    /// <summary>The creatures of fFillCritter, kept between frames.</summary>
    private readonly List<UWCritter> mOCritters = new List<UWCritter>();

    /// <summary>
    /// What the nearest creature is currently up to.
    ///
    /// WHY IT IS HERE: from the outside you can read almost nothing off a creature.
    /// Whether a fire elemental throws no fireball because it never enters the spell branch,
    /// because its roll fails, or because the projectile does not appear, you cannot tell
    /// by looking at it - and when in doubt you guess (2026-09-10, exactly this case).
    ///
    /// Shown are the numbers from the creature table that determine its behaviour,
    /// and what it is currently doing with them (UWCritter.FillDebug).
    /// </summary>
    private void fFillCritter(UWDebugSnapshot pOSnapshot)
    {
        // THE LOOKED-AT CREATURE FIRST. The nearest enemy is not necessarily the one
        // that matters right now - with the fire elemental the line showed a spider instead,
        // which happened to be closer (per user, 2026-09-10).
        // The creatures from the world's search (cleanup step 3.2, 2026-10-17; before, a scene search).
        List<UWCritter> lOCritters = mOCritters;
        UWWorld lOWorld = UWScene.World;

        if (lOWorld == null || lOWorld.FindCritters(UWTileArea.WholeLevel, UWCreatureFilter.Any, lOCritters) == 0)
        {
            pOSnapshot.CreatureCount = 0;

            return;
        }

        pOSnapshot.CreatureCount = lOCritters.Count;

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

        UWDebugCreature lOView = new UWDebugCreature
        {
            Name = lOShown.name,
            LookedAt = lbLookedAt,
            Distance = lfDistanceToShown,
            State = lOShown.State.ToString(),
            AttackKind = lOShown.AttackKind.ToString()
        };

        lOShown.FillDebug(lOView);
        pOSnapshot.Creature = lOView;
    }

    /// <summary>The creature under the crosshair, if there is one.</summary>
    private UWCritter fGetLookedAtCritter()
    {
        Camera lOCamera = fGetActiveCamera();

        if (lOCamera == null)
            return null;

        Ray lORay = UWWorldPicker.CrosshairRay(lOCamera);

        RaycastHit lOHit;

        if (!Physics.Raycast(lORay, out lOHit, mfLookRange))
            return null;

        return lOHit.collider.GetComponentInParent<UWCritter>();
    }

    /// <summary>Hit points, mana and poison - so a hit can be attributed.
    /// Without this you see the flask twitch while testing, but no number.</summary>
    private void fFillCharacter(UWDebugSnapshot pOSnapshot)
    {
        if (mOCharacter == null)
            mOCharacter = UWScene.Character;

        if (mOCharacter == null)
            return;

        UWDebugPlayer lOPlayer = new UWDebugPlayer
        {
            HitPoints = mOCharacter.Vitals.CurrentHP,
            MaxHitPoints = mOCharacter.Vitals.MaxHP,
            Mana = mOCharacter.Vitals.CurrentMana,
            MaxMana = mOCharacter.Vitals.MaxMana,
            Poison = mOCharacter.Vitals.Poison
        };

        pOSnapshot.Player = lOPlayer;

        // WHERE THE LAST HIT CAME FROM, and the combat shake of that hit, if it shook at all.
        if (mOCharacter.LastDamageTime >= 0f)
        {
            lOPlayer.HasLastHit = true;
            lOPlayer.LastDamage = mOCharacter.Vitals.LastDamage;
            lOPlayer.LastDamageType = mOCharacter.Vitals.LastDamageType;
            lOPlayer.LastHitAge = Time.time - mOCharacter.LastDamageTime;
            lOPlayer.Shook = mOCharacter.LastShakeTime >= 0f && Mathf.Abs(mOCharacter.LastShakeTime - mOCharacter.LastDamageTime) < 0.1f;
            lOPlayer.ShakeLevel = mOCharacter.LastShakeLevel;
        }

        fFillArmour(lOPlayer);
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
    /// </summary>
    private void fFillArmour(UWDebugPlayer pOPlayer)
    {
        mOCharacter.RefreshStatus();

        for (int liPart = 0; liPart < msArmourParts.Length; liPart++)
        {
            pOPlayer.Armour[liPart] = mOCharacter.GetArmourAt(msArmourParts[liPart]);
            pOPlayer.Protection[liPart] = mOCharacter.GetProtectionAt(msArmourParts[liPart]);
        }

        pOPlayer.DefenceCheck = mOCharacter.DefenceCheck;
        pOPlayer.Defence = mOCharacter.Vitals.Defence;
        pOPlayer.DefenceSkill = mOCharacter.DefenceWeaponSkill.ToString();
        pOPlayer.Resistance = mOCharacter.DamageResistance;
        pOPlayer.Proof = mOCharacter.DamageTypeProof;
        pOPlayer.Stealth = mOCharacter.StealthBonus;
        pOPlayer.DragonSkinBoots = mOCharacter.HasDragonSkinBoots;

        if (mOInventory == null)
            return;

        foreach (UWArmorItemMap.BodySlot leSlot in msArmourSlots)
        {
            UWObject lOItem = mOInventory.GetEquipped(leSlot);

            if (lOItem == null)
                continue;

            int liMajor;
            int liMinor;

            if (!UWArmourProtection.TryGetWearableEnchantment(lOItem, out liMajor, out liMinor))
                continue;

            pOPlayer.Enchantments.Add(new UWDebugEnchantment
            {
                Slot = leSlot.ToString(),
                ObjectId = lOItem.ID,
                Major = liMajor,
                Minor = liMinor,
                Protection = UWArmourProtection.GetProtectionEnchantment(lOItem)
            });
        }
    }

    /// <summary>The armour parts in the snapshot's order: body, hands, legs, head.</summary>
    private static readonly int[] msArmourParts =
    {
        UWArmourProtection.PartBody,
        UWArmourProtection.PartHands,
        UWArmourProtection.PartLegs,
        UWArmourProtection.PartHead
    };

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
    private void fFillTileTraps(UWDebugSnapshot pOSnapshot, UWTile pOTile)
    {
        if (pOTile == null || pOTile.ObjectsInTile == null || mOLoader == null)
            return;

        foreach (UWObject lOObject in pOTile.ObjectsInTile)
        {
            if (lOObject == null || lOObject.ID < 384 || lOObject.ID > 447)
                continue;

            UWObject lOLinked = UWObjectMechanics.GetLinkedObject(lOObject, mOLoader.CurrentLevel.Masterlist);

            pOSnapshot.TileTraps.Add(new UWDebugTileTrap
            {
                Name = UWTrapLog.GetName(mOLoader.UWDataImporter, lOObject.ID),
                Quality = lOObject.Quality,
                Owner = lOObject.Owner,
                LinkedName = lOLinked != null && lOLinked.ID >= 384 && lOLinked.ID <= 447
                    ? UWTrapLog.GetName(mOLoader.UWDataImporter, lOLinked.ID)
                    : null
            });
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
    /// the one at 0x5A the heading as a full circle in 16 bits (see UWPlayerData.WritePosition).
    /// </summary>
    private static void fFillOriginalUnits(UWDebugSnapshot pOSnapshot, Vector3 pOPosition, float pfYaw)
    {
        pOSnapshot.OriginalX = UWViewpoint.WorldToOriginalX(pOPosition.x);
        pOSnapshot.OriginalY = UWViewpoint.WorldToOriginalY(pOPosition.z);
        pOSnapshot.OriginalZ = UWViewpoint.WorldToOriginalZ(pOPosition.y);
        pOSnapshot.OriginalHeading = (ushort)UWViewpoint.DegreesToAngle(pfYaw);

        // THE MOTION CORE'S OWN VIEW (stage 3, 2026-10-06): the params block, the contact state
        // and what the last step found under and around the player (UWDebugMotion.From).
        UWPlayerMovement lOMovement = UWScene.PlayerMovement;

        if (lOMovement != null && lOMovement.UsesMotionCore)
            pOSnapshot.Motion = UWDebugMotion.From(lOMovement.Motion);
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
        UWLevel lOLevel = mOLoader.CurrentLevel;

        return lOLevel != null ? lOLevel.GetTile(piTileX, piTileZ) : null;
    }

    /// <summary>
    /// Determines what lies in the view direction. For geometry the table in
    /// UWLevelChunk is used to work back which tile was hit; the surface normal
    /// distinguishes floor, ceiling and wall. For objects the id and texture source are
    /// given - exactly the details needed for debugging.
    /// </summary>
    private void fFillLookTarget(UWDebugLook pOLook, Camera pOCamera)
    {
        if (pOCamera == null)
        {
            pOLook.Kind = UWDebugLookKind.NoCamera;

            return;
        }

        Ray lORay = UWWorldPicker.CrosshairRay(pOCamera);
        RaycastHit lOHit;

        if (!Physics.Raycast(lORay, out lOHit, mfLookRange))
        {
            pOLook.Kind = UWDebugLookKind.Nothing;

            return;
        }

        UWEntityInfo lOEntity = lOHit.collider.GetComponentInParent<UWEntityInfo>();

        if (lOEntity != null && lOEntity.ObjectData != null)
        {
            UWObject lOObject = lOEntity.ObjectData;

            pOLook.Kind = UWDebugLookKind.Object;
            pOLook.ObjectId = lOObject.ID;
            pOLook.Category = lOObject.GetCategory().ToString();
            pOLook.Texture = lOObject.Texture != null
                ? string.Format("{0} {1}", lOObject.Texture.TextureType, lOObject.Texture.Index)
                : "?";
            pOLook.TileX = lOObject.TileX;
            pOLook.TileY = lOObject.TileY;
            pOLook.Height = lOObject.ZPos;
            pOLook.Heading = lOObject.Heading;
            pOLook.Description = string.IsNullOrEmpty(lOEntity.Description) ? "-" : lOEntity.Description;

            return;
        }

        UWLevelChunk lOChunk = lOHit.collider.GetComponent<UWLevelChunk>();

        if (lOChunk != null)
        {
            int liTileIndex = lOChunk.GetTileIndex(lOHit.triangleIndex);

            if (liTileIndex >= 0)
            {
                int liTileX = liTileIndex % UWWorldScale.TilesPerAxis;
                int liTileZ = liTileIndex / UWWorldScale.TilesPerAxis;

                UWTile lOTile = fGetTile(liTileX, liTileZ);

                float lfUp = Vector3.Dot(lOHit.normal, Vector3.up);

                pOLook.Kind = UWDebugLookKind.Surface;
                pOLook.TileX = liTileX;
                pOLook.TileY = liTileZ;
                pOLook.TileType = lOTile != null ? lOTile.TileType.ToString() : "?";

                if (lfUp > 0.5f)
                {
                    pOLook.Surface = "floor";
                    pOLook.TextureIndex = lOTile != null ? lOTile.TextureFloor : -1;
                }
                else if (lfUp < -0.5f)
                {
                    pOLook.Surface = "ceiling";
                    pOLook.TextureIndex = lOTile != null ? lOTile.TextureCeiling : -1;
                }
                else
                {
                    pOLook.Surface = "wall";
                    pOLook.TextureIndex = lOTile != null ? lOTile.TextureWall : -1;
                }

                return;
            }
        }

        pOLook.Kind = UWDebugLookKind.Collider;
        pOLook.ColliderName = lOHit.collider.name;
        pOLook.HasEntity = lOEntity != null;

        if (lOEntity != null)
            pOLook.Description = string.IsNullOrEmpty(lOEntity.Description) ? "-" : lOEntity.Description;
    }
}
