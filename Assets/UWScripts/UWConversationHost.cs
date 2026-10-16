using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The Unity side of a conversation: what UWConversationSession and UWConversationTrade ask
/// for through IUWConversationHost, read from the components of the scene - UWCharacter for
/// the player, UWLevelLoader for the world, UWInventory for the backpack, Interaction for
/// the look descriptions and UWConversationScreen for the look text. Created per
/// conversation by UWConversationScreen; the components are looked up once when first needed.
/// </summary>
public sealed class UWConversationHost : IUWConversationHost
{
    private UWCharacter mOCharacter;

    private UWLevelLoader mOLoader;

    private UWInventory mOInventory;

    private Interaction mOInteraction;

    private UWConversationScreen mOScreen;

    private bool mbLookedUp;

    private void fLookUp()
    {
        if (mbLookedUp)
            return;

        mbLookedUp = true;

        mOCharacter = UWScene.Character;
        mOInventory = UWScene.Inventory;
        mOInteraction = UWScene.Interaction;
        mOScreen = UWScene.ConversationScreen;

        mOLoader = UWScene.LevelLoader;
    }

    private UWCharacter fCharacter
    {
        get
        {
            fLookUp();
            return mOCharacter;
        }
    }

    private UWLevelLoader fLoader
    {
        get
        {
            fLookUp();
            return mOLoader;
        }
    }

    // ------------------------------------------------- The player

    public bool HasPlayer => fCharacter != null;

    public int PlayerHunger => fCharacter != null ? fCharacter.Hunger : 0;

    public int PlayerHitPoints => fCharacter != null ? Mathf.RoundToInt(fCharacter.CurrentHP) : 0;

    public int PlayerMaxHitPoints => fCharacter != null ? Mathf.RoundToInt(fCharacter.MaxHP) : 0;

    public int PlayerMana => fCharacter != null ? Mathf.RoundToInt(fCharacter.CurrentMana) : 0;

    public int PlayerAttack => fCharacter != null ? fCharacter.Attack : 0;

    public int PlayerStrength => fCharacter != null ? fCharacter.Strength : 0;

    public int PlayerDexterity => fCharacter != null ? fCharacter.Dexterity : 0;

    public int PlayerLevel => fCharacter != null ? fCharacter.Level : 0;

    public int PlayerClock => fCharacter != null ? fCharacter.ClockValue : 0;

    public int PlayerPoison => fCharacter != null ? fCharacter.Poison : 0;

    public bool PlayerWeaponDrawn => fCharacter != null && fCharacter.DrawWWeapon;

    public int GetPlayerSkill(UWPlayerData.Skill peSkill)
    {
        return fCharacter != null ? fCharacter.GetSkill(peSkill) : 0;
    }

    public int GetPlayerSkillByNumber(int piSkillNumber)
    {
        return fCharacter != null ? fCharacter.GetSkillByNumber(piSkillNumber) : 0;
    }

    public void SetPlayerSkillByNumber(int piSkillNumber, int piValue)
    {
        if (fCharacter != null)
            fCharacter.SetSkillByNumber(piSkillNumber, piValue);
    }

    public bool TryIncreasePlayerSkill(int piSkillNumber)
    {
        return fCharacter != null && fCharacter.TryIncreaseSkill(piSkillNumber);
    }

    public void ApplyConversationValues(int piHunger, int piHitPoints, int piMana, int piPoison)
    {
        if (fCharacter != null)
            fCharacter.ApplyConversationValues(piHunger, piHitPoints, piMana, piPoison);
    }

    public void ChangeExperience(int piAmount, int piDungeonLevel)
    {
        if (fCharacter != null)
            fCharacter.ChangeExperience(piAmount, piDungeonLevel);
    }

    // ------------------------------------------------- The world

    public UWLevel CurrentLevel => fLoader != null ? fLoader.CurrentLevel : null;

    public int CurrentLevelNumber => fLoader != null ? fLoader.CurrentLevelIndex + 1 : 1;

    public bool SetDoorState(int piTileX, int piTileY, int piMode)
    {
        return fLoader != null && UWTriggerSystem.TrySetDoorState(piTileX, piTileY, piMode, fLoader);
    }

    public bool SpawnObjectInTile(UWObject pOItem, int piTileX, int piTileY)
    {
        return fLoader != null && fLoader.SpawnObjectInTile(pOItem, piTileX, piTileY);
    }

    /// <summary>
    /// place_object with a negative x (BABLHACK.C): put_at at the player's tile with its fine
    /// position 0/0, drop_around_place's stay_centered set (that point is tried first), range 6
    /// and nocull - the object is never culled. Until 2026-10-12 a random spot of the player's
    /// tile, a roll the original never makes.
    /// </summary>
    public void DropAtPlayer(UWObject pOItem)
    {
        if (fLoader == null || pOItem == null)
            return;

        Camera lOCamera = Camera.main;

        if (lOCamera == null)
            return;

        Vector3 lOFeet = UWPlayerThrow.GetFeet(lOCamera.transform);
        UWTilePos lOTile = fLoader.WorldPositionToTile(lOFeet);
        Vector3 lOCorner = new Vector3(UWUnits.EighthsToWorldAxis(lOTile.X << 3), lOFeet.y,
            UWUnits.EighthsToWorldAxis(lOTile.Y << 3));

        fLoader.SpawnScatteredAround(pOItem, lOCorner, PlaceObjectRange, lOTile, false, true, true);
    }

    /// <summary>place_object's range at the player's feet, in eighths.</summary>
    private const int PlaceObjectRange = 6;

    public void DropAroundPlayer(UWObject pOItem)
    {
        if (fLoader == null || pOItem == null)
            return;

        Camera lOCamera = Camera.main;

        if (lOCamera == null)
            return;

        Vector3 lOFeet = UWPlayerThrow.GetFeet(lOCamera.transform);

        // No second try when it returns false: then the culling test took it, as in the original.
        fLoader.SpawnScatteredAround(pOItem, lOFeet, BarterDropRadius, fLoader.WorldPositionToTile(lOFeet));
    }

    /// <summary>Radius of end_barter's PlaceObjectAtNPC, in eighths of a tile.</summary>
    private const int BarterDropRadius = 5;

    /// <summary>
    /// near_mob_put_at(talking_to, item, 5, cull): around the partner's own spot, radius 5, the
    /// culling test deciding when no spot fits. False when the culling test took it (the 3 of
    /// npc_barter_give). Until 2026-10-12 a failure went on to the player's tile, which the
    /// original never does.
    /// </summary>
    public bool DropAtNpc(UWObject pOItem, UWNpc pONpc)
    {
        if (fLoader == null || pOItem == null || pONpc == null)
            return false;

        UWEntityInfo lOEntity;
        Vector3 lOCentre = fLoader.TryGetEntity(pONpc, out lOEntity) && lOEntity != null
            ? lOEntity.transform.position
            : UnderworldRevisited.Build.UWObjectSpawner.GetObjectWorldPosition(pONpc.TileX, pONpc.TileY, pONpc);

        return fLoader.SpawnScatteredAround(pOItem, lOCentre, BarterDropRadius, fLoader.WorldPositionToTile(lOCentre));
    }

    public bool RemoveObjectFromWorld(UWObject pOObject)
    {
        return fLoader != null && pOObject != null && fLoader.RemoveObjectFromWorld(pOObject);
    }

    // ------------------------------------------------- Inventory and text

    public UWInventoryModel Inventory
    {
        get
        {
            fLookUp();
            return mOInventory != null ? mOInventory.Model : null;
        }
    }

    public string DescribeItem(UWObject pOItem, int piDetail = UWItemDescriptions.DetailFromLoreCheck)
    {
        fLookUp();

        return mOInteraction != null && pOItem != null ? mOInteraction.DescribeInventoryItem(pOItem, piDetail) : null;
    }

    public bool ShowLookText(string psText)
    {
        fLookUp();

        if (mOScreen == null)
            return false;

        mOScreen.ShowLookText(psText);

        return true;
    }
}
