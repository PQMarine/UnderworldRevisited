using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// Sits on everything the player can look at or use, and holds the link
/// to the original data.
/// </summary>
public class UWEntityInfo : MonoBehaviour
{
    // Runtime reference to the original data, not meant to be saved in the scene - without
    // NonSerialized the Unity analyzer reported UAC1001 on every start.
    [System.NonSerialized]
    public UWTile TileData;

    [System.NonSerialized]
    public UWObject ObjectData;

    public string Description;

    public bool IsDoor { get; set; }

    /// <summary>A 3D model (UWObjectSpawner.fSpawn3DModel): aimed at by its triangles, not its
    /// collider box - see Interaction.fMissesModelShape.</summary>
    public bool IsModel { get; set; }

    /// <summary>Description is already the finished display text and must NOT be embedded
    /// in "You see ..." - for wall inscriptions that in the original show their wording
    /// directly ("The writing reads: ..."), see UWObjectSpawner.fSpawnDecal.</summary>
    public bool ShowDescriptionVerbatim { get; set; }

    /// <summary>Window onto the volcano's maw: looking at it shows a picture in the view window
    /// in addition to the text (cutscene CS400.N01), see UWGameUI.ShowWindowPicture.</summary>
    public bool IsWindow { get; set; }

    /// <summary>Arial chained to her wall: a wall decoration whose texture has the TERRAIN.DAT
    /// type ChainedPrincess - talking to it answers "There is no reaction from the princess."
    /// (UWClickRules.TalkToThing).</summary>
    public bool IsChainedPrincess { get; set; }

    /// <summary>The object this piece belongs to - a door frame's door. In the original the
    /// frame is no object of its own but part of the door's collision, so a missile that hits
    /// the frame hits the door, and the flash shows at the door as if it had been struck (per
    /// user on the original, 2026-09-24).</summary>
    public UWEntityInfo PartOf
    {
        get
        {
            if (mOPartOf == null && mOPartOfOwner != null)
                mOPartOf = mOPartOfOwner.GetComponent<UWEntityInfo>();

            return mOPartOf;
        }
        set { mOPartOf = value; }
    }

    /// <summary>The owner's component when its UWEntityInfo does not exist yet at build time -
    /// PartOf looks it up on first use (the door blocker is built before the door's info).</summary>
    public Component PartOfOwner
    {
        set { mOPartOfOwner = value; }
    }

    private UWEntityInfo mOPartOf;

    private Component mOPartOfOwner;

    public bool CanBePickedUp { get; set; }
}
