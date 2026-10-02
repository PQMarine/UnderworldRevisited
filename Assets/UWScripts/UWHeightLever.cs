using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// A lever with EIGHT POSITIONS (a_lever, ID 353). Each use advances it by one position,
/// wrapping around, and the image follows (TMOBJ_4..TMOBJ_11 - the lowest position
/// shows TMOBJ_4).
///
/// THE POSITION IS STORED IN THE OBJECT'S FLAGS FIELD, three bits. Read from the user's
/// save game (2026-09-07): the three levers on level 5, tile 6/29 all carry zero in the
/// original data, but 7, 2 and 6 in his solved save - exactly the
/// solution of the counting puzzle there. Previously this component counted its uses itself;
/// that was lost on a level change, because the world is then rebuilt from the tile
/// data. Now the position lives where the original keeps it too.
///
/// TWO THINGS COUNT IN PARALLEL, and that is not an oversight of the original: the lever keeps
/// its position in the flags field, and the trap behind it keeps its own value - for the
/// puzzle levers a game variable (see UWGameVariables), for the platform lever on level 1
/// the height of the target tile. Both counters stay in sync because both hang off the same
/// use.
///
/// ADVANCING HAPPENS ON USE, not in a specific trap type. Until 2026-09-07
/// it only happened in the a_do trap "raise tile" branch, which is why the puzzle levers on
/// level 5 did not move at all (per user). See UWTriggerSystem.fTryFireTriggerChain.
///
/// FOR THE PLATFORM LEVER (level 1, a_do trap with action 3) the following still holds, confirmed
/// by user and NOT to be accidentally "fixed" during the next rework: when the
/// level is built, the target tile stays at its static height from the level data,
/// which does NOT match the lever position. Only an actual use sets it to
/// position-0 height + position * step (see UWObjectMechanics.DoTrapRaiseTileBaseHeight).
///
/// Sits next to the MeshFilter (see UWObjectSpawner.fSpawnDecal, ID 353 in the
/// PillarSomeDecals branch).
/// </summary>
public class UWHeightLever : MonoBehaviour
{
    private MeshFilter mFilter;
    private Mesh[] mPositionMeshes;
    private UWObject mOData;

    /// <summary>Three bits, so eight positions.</summary>
    private const int PositionMask = 0x7;

    /// <summary>The current position, 0 to 7 - it is stored in the object's flags field.</summary>
    public int Position
    {
        get { return mOData == null ? 0 : mOData.Flags & PositionMask; }
    }

    public void Initialise(MeshFilter pOFilter, Mesh[] pOPositionMeshes, UWObject pOData)
    {
        mFilter = pOFilter;
        mPositionMeshes = pOPositionMeshes;
        mOData = pOData;
    }

    /// <summary>Advances one position and shows the matching image. Returns the new
    /// position.</summary>
    public int Advance()
    {
        if (mOData == null)
            return 0;

        mOData.Flags = (ushort)((mOData.Flags & ~PositionMask) | ((mOData.Flags + 1) & PositionMask));

        if (mFilter != null && mPositionMeshes != null && Position < mPositionMeshes.Length
            && mPositionMeshes[Position] != null)
            mFilter.sharedMesh = mPositionMeshes[Position];

        return Position;
    }

    /// <summary>The height the target tile of a platform lever belongs at - from the
    /// CURRENT position, which has already been advanced at this point (see the
    /// class comment). piStepSize comes from the trap chain and is only known at
    /// use time.</summary>
    public int GetTileHeight(int piStepSize)
    {
        return UWObjectMechanics.DoTrapRaiseTileBaseHeight + (Position * piStepSize);
    }
}
