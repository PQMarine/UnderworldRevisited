using UnityEngine;

/// <summary>
/// The shader rule "what stands in a tile is not covered by that tile's own floor, ceiling or
/// walls" (Assets/UWShaders/UWOwnTile.hlsl). Its names live in UWShaderIds since 2026-10-24
/// (OwnTileKeyword, OwnTile, BigRadius, KeyBonus, ChainIndex, ModelFloorY and the globals), with
/// every other shader name of the game; its globals are written through UWShaderGlobals.
///
/// The original paints tile by tile from the back to the front, and what stands in a tile after
/// that tile. Who uses the rule: every creature (UWCritterAnimator; the lurker in its water since
/// 2026-09-17, a goblin's arm at a wall since 2026-09-22), the item sprites, thrown ones included
/// (UWLevelLoader), and the single-coloured faces of the 3D models - the shrine on level 3, 28/44
/// (UWLevelLoader, UWPaletteRenderPath, UWObjectSpawner). The palette copies of a material take
/// the keyword over from their source (UWPaletteRenderPath.fGetPaletteBillboard).
/// </summary>
public static class UWOwnTile
{
    private static Texture2D msFloorHeights;

    /// <summary>
    /// The floor height of every tile of the level, for the sprite rule (per user, 2026-09-28: a
    /// thrown mushroom falling past a ledge, its centre still over the higher tile but already
    /// below its floor, showed over that floor - in the original it is covered). A sprite whose
    /// pivot lies below its own tile's floor keeps its true depth, so the floor covers it; the
    /// original never has an object below its tile's floor, only our physics passes through that
    /// state. Set on every level build and every rebuild after a height change (UWLevelLoader).
    /// Solid tiles count as the ceiling. The second channel holds the tile type (0 solid to 9,
    /// UWTile.TileTypeEnum), for the line of sight through diagonals (UWOwnTileBehindRock).
    /// </summary>
    public static void SetFloorHeights(UWDataImport.UWData.UWLevel pOLevel)
    {
        if (pOLevel == null)
        {
            UWShaderGlobals.SetFloat(UWShaderIds.FloorHeightsReady, 0f);
            return;
        }

        int liSize = UWDataImport.UWData.UWWorldScale.TilesPerAxis;

        if (msFloorHeights == null)
        {
            msFloorHeights = new Texture2D(liSize, liSize, TextureFormat.RGBAFloat, false, true);
            msFloorHeights.name = "UW Floor Heights";
            msFloorHeights.filterMode = FilterMode.Point;
            msFloorHeights.wrapMode = TextureWrapMode.Clamp;
        }

        Color[] lOPixels = new Color[liSize * liSize];

        // The third channel names the liquid, 1 water and 2 lava - the palette renderer's grime
        // leaves those floors alone (UWPaletteEffects.hlsl, UWGrimeLevel).
        UWDataImport.UWData.UWTileQueries lOQueries = UWScene.LevelLoader != null ? UWScene.LevelLoader.TileQueries : null;

        for (int liY = 0; liY < liSize; liY++)
        {
            for (int liX = 0; liX < liSize; liX++)
            {
                UWDataImport.UWData.UWTile lOTile = pOLevel.GetTile(liX, liY);
                float lfFloor = lOTile == null || lOTile.TileType == UWDataImport.UWData.UWTile.TileTypeEnum.solid
                    ? UWDataImport.UWData.UWWorldScale.CeilingHeight : lOTile.FloorHeight;

                float lfType = lOTile == null ? 0f : (float)(int)lOTile.TileType;

                float lfLiquid = lOQueries == null || lOTile == null ? 0f
                    : lOQueries.IsWaterTile(lOTile) ? 1f
                    : lOQueries.IsLavaTile(lOTile) ? 2f
                    : 0f;

                lOPixels[(liY * liSize) + liX] = new Color(lfFloor, lfType, lfLiquid, 0f);
            }
        }

        msFloorHeights.SetPixels(lOPixels);
        msFloorHeights.Apply(false, false);
        UWShaderGlobals.SetTexture(UWShaderIds.FloorHeights, msFloorHeights);
        UWShaderGlobals.SetFloat(UWShaderIds.FloorHeightsReady, 1f);
    }

    private static Texture2D msDoorPlanes;

    private static Color[] msDoorPixels;

    /// <summary>
    /// THE DOOR OF EVERY TILE, for the painter's partition (per user, 2026-09-28: creatures
    /// behind a door showed over the door and its lintel). The original sorts the objects of a
    /// tile with a door into those beyond the door plane, the door, and those on the viewer's
    /// side (seg033_2EEF_43B, seg033_2EEF_281) - and a big creature just behind the door is
    /// handed to the door's tile. Per tile: r = 1 with a door, g = 1 when the door plane lies
    /// across the X axis (heading 2 or 6), b = the plane's world coordinate on that axis.
    /// Cleared before a level's objects are spawned, filled by UWObjectSpawner, applied after.
    /// </summary>
    public static void ClearDoors()
    {
        int liSize = UWDataImport.UWData.UWWorldScale.TilesPerAxis;

        if (msDoorPixels == null)
            msDoorPixels = new Color[liSize * liSize];
        else
            System.Array.Clear(msDoorPixels, 0, msDoorPixels.Length);

        if (msModelPixels == null)
            msModelPixels = new Color[liSize * liSize];
        else
            System.Array.Clear(msModelPixels, 0, msModelPixels.Length);

        msDoorsApplied = false;
        DoorPlanesGeneration++;
        UWShaderGlobals.SetFloat(UWShaderIds.DoorPlanesReady, 0f);
    }

    /// <summary>Counts the level builds, so a bridge destroyed with the old level does not clear
    /// the new level's tile (UWBridgePlane).</summary>
    public static int DoorPlanesGeneration { get; private set; }

    /// <summary>Whether the level build has applied the planes; a bridge registered or removed
    /// later (a trap, a respawn) applies them again itself.</summary>
    private static bool msDoorsApplied;

    /// <summary>A door in tile x/z (world grid); the last one of a tile wins, as in the
    /// original.</summary>
    public static void RegisterDoor(int piTileX, int piTileZ, bool pbAcrossX, float pfPlane)
    {
        int liSize = UWDataImport.UWData.UWWorldScale.TilesPerAxis;

        if (msDoorPixels == null || piTileX < 0 || piTileZ < 0 || piTileX >= liSize || piTileZ >= liSize)
            return;

        int liAt = (piTileZ * liSize) + piTileX;

        msDoorPixels[liAt] = new Color(1f, pbAcrossX ? 1f : 0f, pfPlane, msDoorPixels[liAt].a);
    }

    private const float BridgePlaneMargin = 1f;

    /// <summary>
    /// A BRIDGE'S DECK AS A LEVEL PLANE (per user, 2026-09-29: the Wine of Compassion showed
    /// through the floor plate over it, level 6 27/50). In the original the bridge, later in the
    /// tile's chain, is painted over what lies under it; ours let every sprite win against the
    /// geometry of its own tile. The deck height goes into the same per-tile plane as a door's
    /// (g = 2: the plane lies across Y), so what lies below the deck, seen from above, is covered
    /// by the tile's nearer surfaces - and the other way round. A door of the tile keeps its plane.
    /// </summary>
    public static void RegisterBridge(int piTileX, int piTileZ, float pfDeckY)
    {
        int liSize = UWDataImport.UWData.UWWorldScale.TilesPerAxis;

        if (msDoorPixels == null || piTileX < 0 || piTileZ < 0 || piTileX >= liSize || piTileZ >= liSize)
            return;

        int liAt = (piTileZ * liSize) + piTileX;

        if (msDoorPixels[liAt].r >= 0.5f)
            return;

        // A unit below the deck, so what lies on it (its pivot at the deck's height) stays on the
        // eye's side whatever the rounding.
        msDoorPixels[liAt] = new Color(1f, 2f, pfDeckY - BridgePlaneMargin, msDoorPixels[liAt].a);

        if (msDoorsApplied)
            ApplyDoors();
    }

    /// <summary>The bridge of the tile is gone (the plate over the Wine of Compassion is removed
    /// by a delete trap when lifted): what lay under it is free again. Called by UWBridgePlane
    /// when the bridge's object is destroyed, only for the level it was registered in.</summary>
    public static void UnregisterBridge(int piTileX, int piTileZ, int piGeneration)
    {
        int liSize = UWDataImport.UWData.UWWorldScale.TilesPerAxis;

        if (msDoorPixels == null || piGeneration != DoorPlanesGeneration
            || piTileX < 0 || piTileZ < 0 || piTileX >= liSize || piTileZ >= liSize)
            return;

        int liAt = (piTileZ * liSize) + piTileX;

        if (msDoorPixels[liAt].r < 0.5f || msDoorPixels[liAt].g < 1.5f)
            return;

        // The model flag (alpha) stays - see RegisterModelTile.
        msDoorPixels[liAt] = new Color(0f, 0f, 0f, msDoorPixels[liAt].a);

        if (msDoorsApplied)
            ApplyDoors();
    }

    /// <summary>
    /// A 3D MODEL STANDS IN THE TILE (alpha of the tile's plane pixel; per user, 2026-09-30 with
    /// a screenshot: a flask of lamp oil behind a barrel of its own tile showed through the
    /// barrel). The sprite test lets every sprite win against the surfaces of its own tile - right
    /// for the tile's floor and walls, which the original paints before its objects, wrong for
    /// a model among those objects, which the original sorts with them (seg033_2EEF_3CD). In a
    /// tile with a model a sprite is therefore covered by whatever of the tile is nearer than it
    /// (the door rule in UWPainterSpriteVisible), so the barrel in front hides the flask and one
    /// behind does not.
    ///
    /// BUT ONLY BY A MODEL THE ORIGINAL PAINTS AFTER THE SPRITE (per user, 2026-10-03, level 7,
    /// 17/34: the ruby set into a gravestone was cut in half by the stone's front face; in the
    /// original it shows whole). Both stand on the same eighth, so their keys are equal, and the
    /// stable sort of seg033_2EEF_43B keeps the chain order: the gravestone comes first in the
    /// tile, the ruby is painted over it. So every model of the tile is noted with its eighth in
    /// the world (0 to 7 east and north), its radius when COMOBJ calls it big, and its place in
    /// the tile's chain, and the shader compares them with the sprite's (_ChainIndex, see
    /// UWObjectSpawner.fSetPainterPlace). Four models per tile are kept; a fifth makes the tile
    /// fall back to the depth rule alone.
    /// </summary>
    public static void RegisterModelTile(int piTileX, int piTileZ, int piEighthX, int piEighthZ, int piBigRadius,
        int piChainIndex)
    {
        int liSize = UWDataImport.UWData.UWWorldScale.TilesPerAxis;

        if (msDoorPixels == null || piTileX < 0 || piTileZ < 0 || piTileX >= liSize || piTileZ >= liSize)
            return;

        int liAt = (piTileZ * liSize) + piTileX;

        msDoorPixels[liAt].a = 1f;

        float lfModel = 1f + Mathf.Clamp(piEighthX, 0, 7) + (8 * Mathf.Clamp(piEighthZ, 0, 7))
            + (64 * Mathf.Clamp(piBigRadius, 0, 7)) + (512 * Mathf.Clamp(piChainIndex, 0, MaxChainIndex));
        Color lOModels = msModelPixels[liAt];

        // r below zero: more than four, the tile keeps the depth rule alone.
        if (lOModels.r >= 0f)
        {
            if (lOModels.r == 0f)
                lOModels.r = lfModel;
            else if (lOModels.g == 0f)
                lOModels.g = lfModel;
            else if (lOModels.b == 0f)
                lOModels.b = lfModel;
            else if (lOModels.a == 0f)
                lOModels.a = lfModel;
            else
                lOModels.r = -1f;
        }

        msModelPixels[liAt] = lOModels;

        if (msDoorsApplied)
            ApplyDoors();
    }

    /// <summary>The highest chain place kept; a sprite without one counts as the last.</summary>
    public const int MaxChainIndex = 63;

    private static Texture2D msModelOrder;

    private static Color[] msModelPixels;

    public static void ApplyDoors()
    {
        int liSize = UWDataImport.UWData.UWWorldScale.TilesPerAxis;

        if (msDoorPixels == null)
            return;

        if (msDoorPlanes == null)
        {
            msDoorPlanes = new Texture2D(liSize, liSize, TextureFormat.RGBAFloat, false, true);
            msDoorPlanes.name = "UW Door Planes";
            msDoorPlanes.filterMode = FilterMode.Point;
            msDoorPlanes.wrapMode = TextureWrapMode.Clamp;
        }

        msDoorPlanes.SetPixels(msDoorPixels);
        msDoorPlanes.Apply(false, false);

        if (msModelOrder == null)
        {
            msModelOrder = new Texture2D(liSize, liSize, TextureFormat.RGBAFloat, false, true);
            msModelOrder.name = "UW Model Order";
            msModelOrder.filterMode = FilterMode.Point;
            msModelOrder.wrapMode = TextureWrapMode.Clamp;
        }

        if (msModelPixels != null)
        {
            msModelOrder.SetPixels(msModelPixels);
            msModelOrder.Apply(false, false);
        }

        UWShaderGlobals.SetTexture(UWShaderIds.ModelOrder, msModelOrder);
        msDoorsApplied = true;
        UWShaderGlobals.SetTexture(UWShaderIds.DoorPlanes, msDoorPlanes);
        UWShaderGlobals.SetFloat(UWShaderIds.DoorPlanesReady, 1f);
    }

    /// <summary>Switches the rule on for a material.</summary>
    public static void Enable(Material pOMaterial)
    {
        if (pOMaterial == null)
            return;

        if (pOMaterial.HasProperty(UWShaderIds.OwnTile))
            pOMaterial.SetFloat(UWShaderIds.OwnTile, 1f);

        pOMaterial.EnableKeyword(UWShaderIds.OwnTileKeyword);
    }

    /// <summary>Copies the switch from one material to another (the palette copies).</summary>
    public static void CopyTo(Material pOSource, Material pOTarget)
    {
        if (pOSource != null && pOTarget != null && pOSource.IsKeywordEnabled(UWShaderIds.OwnTileKeyword))
            Enable(pOTarget);
    }

    /// <summary>The size of a tile and the height of the ceiling the shaders need to build the
    /// box. Without them the rule knows only the ground, as it did until 2026-09-22.</summary>
    public static void SetGlobals()
    {
        UWShaderGlobals.SetFloat(UWShaderIds.OwnTileSize, UWDataImport.UWData.UWWorldScale.TileSize);
        UWShaderGlobals.SetFloat(UWShaderIds.OwnTileCeilingY, UWDataImport.UWData.UWWorldScale.CeilingHeight);
    }
}
