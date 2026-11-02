using System.Collections;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// WHO STRIKES: what the player's blow (UWPlayerMelee) and what it looks like (UWHitFeedback) ask of the game
/// around them. Interaction answers for the first game, UWAttackUw2 for the second, where Interaction and
/// UWCharacter stand idle (UW2 plan phase 4, list item 2).
/// </summary>
public interface IUWMeleeHost
{
    UWDataImport.DataImport GameData { get; }

    UWLevelLoader LevelLoader { get; }

    /// <summary>The player's body, null without one.</summary>
    CharacterController MeleeController { get; }

    /// <summary>What the blow's heading is read from, and the spot where a broken piece falls.</summary>
    Transform MeleeFacing { get; }

    /// <summary>The view, for the pitch that raises or lowers the blow.</summary>
    Camera MeleeCamera { get; }

    float AttackRange { get; }

    /// <summary>The modern scheme's ray to the target.</summary>
    Ray InteractionRay();

    /// <summary>The classic scheme's area before the character instead of that ray.</summary>
    bool MeleeByHitArea { get; }

    UWPlayerVitals MeleeVitals { get; }

    int MeleeFeetZPos { get; }

    UWInventoryModel MeleeInventory { get; }

    /// <summary>What the attack score gains besides skill and weapon (Valor).</summary>
    int MeleeAttackBonus { get; }

    /// <summary>Poison Weapon's share of the damage, in fortieths; 0 without it.</summary>
    int PoisonWeaponShare { get; }

    void AddMessage(string psMessage);

    /// <summary>A piece of equipment wore: the status pass runs again.</summary>
    void EquipmentWorn();

    Coroutine StartCoroutine(IEnumerator pOWork);

    /// <summary>Whether the eyes above the view can show a creature's condition.</summary>
    bool ShowsFoeCondition { get; }

    void ShowFoeCondition(int piHitPoints, int piTableHitPoints, string psName);
}
