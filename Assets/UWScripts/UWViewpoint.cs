using UnityEngine;
using UnderworldRevisited.Build;

/// <summary>
/// Conversion between the original's units and our world measures, and the question of
/// WHICH camera is currently rendering.
///
/// THE ORIGINAL'S UNITS: a tile is 0x100 = 256 units wide, a
/// sub-tile step 0x20 = 32, a zpos step 8, a full circle 65536. For us a
/// tile is 64 world units, a sub-tile step 8 and a zpos step 2 - all four give the same
/// factor: 64/256 = 8/32 = 2/8 = 1/4.
///
/// SINCE 2026-09-17 the measures live in UWDataImport.UWData.UWWorldScale and the per-axis
/// arithmetic in UWUnits (P0 of the engine separation); this class only builds Unity vectors
/// from them and knows the rendering camera.
///
/// THE AXES SWAP: the original counts X to the east and Y to the north, for us
/// north is on +Z and height on +Y.
///
/// THE ORIGIN SHIFTS: the original counts from the tile corner, our tile centre
/// is at tileX * 64. Hence the subtraction of half a tile.
/// </summary>
public static class UWViewpoint
{
    /// <summary>One original unit in world units - see class comment.</summary>
    public const float UnitsPerOriginalUnit = UWDataImport.UWData.UWWorldScale.UnitsPerOriginalUnit;

    /// <summary>A full circle in the original's angle measure. UWLevelLoader uses the same
    /// number for the view direction in the save game.</summary>
    public const float FullCircleUnits = UWDataImport.UWData.UWWorldScale.FullCircleUnits;

    /// <summary>
    /// The world coordinate of the lower tile corner on one axis - the zero point from
    /// which the sub-tile position counts. Our tile centre is at tile times
    /// TileSpacing, so the corner is half a tile before it.
    /// </summary>
    public static float TileToWorldAxis(int piTile)
    {
        return UWDataImport.UWData.UWUnits.TileToWorldAxis(piTile);
    }

    /// <summary>
    /// Where an object with this tile, sub-tile position and height lies in the world.
    ///
    /// THE ONE PLACE where this formula lives. Until 2026-09-08 it was copied word for word
    /// in seven places - twice in the spawner, in the debugging code, twice in the level loader
    /// and twice in the trap system. The risk was not the scale, but
    /// the copies: whoever changes one of them does not change the others along with it.
    ///
    /// WITHOUT the render offset against z-fighting - that belongs to creating a visible
    /// body, not to the conversion, and for a camera or a projectile start it would
    /// only be noise.
    /// </summary>
    public static Vector3 SubTileToWorld(int piTileX, int piTileZ, UWDataImport.UWData.UWObject pOObject)
    {
        return new Vector3(
            UWDataImport.UWData.UWUnits.SubTileToWorldAxis(piTileX, pOObject.XPos),
            UWDataImport.UWData.UWUnits.ZPosToWorld(pOObject.ZPos),
            UWDataImport.UWData.UWUnits.SubTileToWorldAxis(piTileZ, pOObject.YPos));
    }

    /// <summary>Like SubTileToWorld, but with its own scale for the
    /// sub-tile position - wall items need a different one, see UWObjectSpawner.
    /// </summary>
    public static Vector3 SubTileToWorld(int piTileX, int piTileZ,
        UWDataImport.UWData.UWObject pOObject, float pfSubScale)
    {
        return new Vector3(
            TileToWorldAxis(piTileX) + (pOObject.XPos * pfSubScale),
            UWDataImport.UWData.UWUnits.ZPosToWorld(pOObject.ZPos),
            TileToWorldAxis(piTileZ) + (pOObject.YPos * pfSubScale));
    }

    /// <summary>A point in original units as a world position.</summary>
    public static Vector3 OriginalToWorld(int piX, int piY, int piZ)
    {
        return new Vector3(
            UWDataImport.UWData.UWUnits.OriginalToWorldAxis(piX),
            UWDataImport.UWData.UWUnits.OriginalToWorldHeight(piZ),
            UWDataImport.UWData.UWUnits.OriginalToWorldAxis(piY));
    }

    public static int WorldToOriginalX(float pfWorldX)
    {
        return UWDataImport.UWData.UWUnits.WorldAxisToOriginal(pfWorldX);
    }

    public static int WorldToOriginalY(float pfWorldZ)
    {
        return UWDataImport.UWData.UWUnits.WorldAxisToOriginal(pfWorldZ);
    }

    public static int WorldToOriginalZ(float pfWorldY)
    {
        return UWDataImport.UWData.UWUnits.WorldHeightToOriginal(pfWorldY);
    }

    /// <summary>A heading value of the original in degrees. It is read WITHOUT sign: the
    /// reference stores it in a short, where heading 4 (i.e. 4 &lt;&lt; 13) wraps to -32768
    /// - as an angle that is the same 180 degrees.</summary>
    public static float AngleToDegrees(int piAngle)
    {
        return UWDataImport.UWData.UWUnits.AngleToDegrees(piAngle);
    }

    public static short DegreesToAngle(float pfDegrees)
    {
        return UWDataImport.UWData.UWUnits.DegreesToAngle(pfDegrees);
    }

    /// <summary>
    /// A pitch value of the original in degrees, with inverted sign.
    ///
    /// The reference rotates about its right axis by +(pitch / 32767) * PI. In its
    /// right-handed system, where the view looks along -Z, a positive value makes it look
    /// UP - its Roaming Sight therefore sets -1024 to look down. In Unity
    /// looking down is a POSITIVE pitch angle. So flip the sign.
    /// </summary>
    public static float PitchToDegrees(int piPitch)
    {
        return UWDataImport.UWData.UWUnits.PitchToDegrees(piPitch);
    }

    /// <summary>
    /// The camera currently rendering: the detached one while one is running, otherwise the
    /// player character's, otherwise the tools' spectator camera.
    ///
    /// WHAT IS MEANT IS THE VIEWPOINT, not the player. Whoever wants to know where the FIGURE stands
    /// still asks Camera.main - it stays with the body, even while a detached
    /// camera is running. That is exactly why creatures do not follow the flying
    /// camera during Roaming Sight.
    /// </summary>
    public static Camera Current
    {
        get
        {
            UWRemoteCamera lORemote = UWRemoteCamera.Current;

            if (lORemote != null && lORemote.IsActive && lORemote.ViewCamera != null)
                return lORemote.ViewCamera;

            return UWScene.ActiveCamera;
        }
    }
}
