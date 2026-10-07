using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// Places the objects of a level into the scene: free-standing items as
    /// billboards, wall decorations, switches, doors and bridges as meshes.
    ///
    /// An object's graphic can come from different files. OBJECTS.GR,
    /// DOORS.GR, TMOBJ.GR and TMFLAT.GR live in the object atlas; wall and floor textures
    /// on the other hand live in the Texture2DArray of the dungeon geometry. That is why
    /// resolution always goes through UWObject.Texture and not the object id - that one
    /// hits the wrong entry for doors, switches and pillars.
    /// </summary>
    public sealed class UWObjectSpawner
    {
        private const float TileSpacing = UWLevelMeshBuilder.TileSpacing;
        private const float TileHalfSize = UWLevelMeshBuilder.TileHalfSize;

        /// <summary>
        /// Object coordinates within a tile run from 0 to 7, and one step is
        /// an EIGHTH OF A TILE - for us that is eight world units.
        ///
        /// The reference computes an object position as (xpos &lt;&lt; 5) + (tileX &lt;&lt; 8): a
        /// tile is 256 units there, a step 32, so exactly one eighth. The
        /// collision code uses the same unit (obj.xpos + (tileX &lt;&lt; 3)).
        ///
        /// THE HIGHEST VALUE THEREFORE DOES NOT REACH THE TILE EDGE: 7 times 8 is 56 out of
        /// 64, leaving eight units of clearance. That is exactly the point - an item at the
        /// edge of its tile would otherwise have its image stuck in the neighbouring wall.
        ///
        /// Until 2026-09-06 this was 9, so that values 0 to 7 spanned the whole tile
        /// width. The user found the bug on an axe on level 3, tile 48/55:
        /// it has xpos 7 and ypos 7, and the tile is solid both to the east and to the
        /// north. With 9 it ended up 0.9 units before the edge and was stuck in the
        /// wall; in the original it is not.
        ///
        /// Public for UWLevelLoader.SpawnDroppedObject (original: dropping/throwing), which
        /// has to compute XPos/YPos back from a continuous world position.
        /// </summary>
        public const float SubTileScale = UWDataImport.UWData.UWWorldScale.SubTileStep;

        /// <summary>
        /// WHERE IN ITS EIGHTH an object stands: 0xF of the 32 original units of a step, 3.75 of
        /// our 8. The original computes a position as (tile &lt;&lt; 8) + (xpos &lt;&lt; 5) + 0xF
        /// (UnderworldGodot, taken from UW.EXE: object creation, projectiles, spells), the same
        /// for x and y. We left it out until 2026-09-17: in a comparison in front of Drog (SAVE4
        /// level 1) the wall plate had to move 4 to 5 units along the wall and Drog stood too far
        /// away (per user with screenshots). An object at xpos 7 now lies at 59.75 of 64, still
        /// clear of the neighbouring wall (see the axe on level 3 above).
        /// </summary>
        public const float SubTileOffset = UWDataImport.UWData.UWWorldScale.SubTileOffset;

        /// <summary>
        /// For everything that hangs ON A WALL: writing plaques, wall decoration, switches,
        /// pillars, force fields - i.e. everything that goes through fSpawnDecal.
        ///
        /// They need the other scale because their highest value has to reach the TILE EDGE:
        /// a writing plaque belongs on the wall, not eight units in front of it
        /// in the air. Nine times seven is 63, so practically the edge, and the scant
        /// remaining unit keeps it out of the wall surface.
        ///
        /// Free-lying items need exactly the opposite (see SubTileScale) -
        /// they must NOT reach the edge, otherwise they get stuck in the neighbouring wall.
        /// Same number range, two purposes.
        ///
        /// Confirmed by the user on a writing plaque on level 3, tile 23/8: it has
        /// ypos 7 with a solid neighbour tile to the north. With the scale for free
        /// items it floated in front of the wall (2026-09-06).
        /// </summary>
        private const float DecalSubTileScale = 9f;

        /// <summary>Object heights are given in half units. Public for
        /// UWLevelLoader.SpawnDroppedObject (original: dropping/throwing), which has to compute
        /// ZPos back from the floor height of the target tile.</summary>
        public const float HeightScale = UWDataImport.UWData.UWWorldScale.ZPosStep;

        /// <summary>
        /// Model shade values (per-vertex D4 bytes, flat BC levels) to LIGHT.DAT levels: ONE TO ONE.
        /// Read out of UW.EXE 2026-09-23: the D4 handler (segment 004, right after the loop at
        /// seg004_2BB4) adds each vertex's byte to colour register 10 and clamps at
        /// GouraudMaxLevel; the BC handler (before seg004_7DBC) adds a flat face's level to the
        /// same register and clamps at 15. Register 10 is the object's own shade level from
        /// distance and light (dseg_5c99_3172, filled by seg032_2DCA_597) - which our palette
        /// shader adds on its own (UWShadeLevel), so only the model's value comes in here.
        ///
        /// It was 2 until then, an estimate from a screenshot that put the chest outline, flat
        /// level 5, at about LIGHT.DAT level 8 - that is 5 plus a distance level of 3, not a
        /// factor. With 2 the shrine's foot and loop top (values 5 to 7) went nearly black (per
        /// user with screenshots, 2026-09-22).
        /// </summary>
        private const int ModelShadeToLightLevel = 1;

        /// <summary>The D4 handler clamps a Gouraud vertex at level 14 (0x0E), the flat faces go
        /// up to 15 - see ModelShadeToLightLevel.</summary>
        private const int GouraudMaxLevel = 14;

        private const int SpriteLayer = 8;
        private const int BridgeObjectId = UWObjectMechanics.BridgeObjectId;

        // Item id -> model mapping, confirmed 2026-09-15 by the object-to-model table in
        // UW.EXE (DS:0682, file 0x5E342 in the GOG build, 32 words indexed by id - 0x150).
        private const int ShrineObjectId = UWShrineRules.ShrineObjectId;
        private const int SmallBoulderObjectId = 0x0156;
        private const int MediumBoulderObjectId = 0x0155;
        private const int LargeBoulderObjectIdA = 0x0153;
        private const int LargeBoulderObjectIdB = 0x0154;
        private const int BenchObjectId = 0x0150;
        private const int PillarObjectId = 0x0160;

        /// <summary>
        /// A vertical four-cornered side reaching the ceiling, textured from the atlas: cut into
        /// pieces of one repeat of its picture (see the call in fSpawn3DModel). The repeat is as
        /// tall as the picture's texels make it at the width the side gives them (square texels).
        /// The corners keep the model's order and U - top at U 0, top at U 1, bottom at U 1,
        /// bottom at U 0 for the pillar - so the winding stays. False when the face is not of that
        /// shape; the caller then builds it as before.
        /// </summary>
        private static bool fAddRepeatedSide(UW3DModel pOModel, UW3DModelFace pOFace, float pfModelScale,
            float pfLocalCeiling, float pfBaseY, UWTextureRef pOResolved, List<Vector3> pOVerts, List<Vector3> pOUvs,
            List<Color32> pOColors, List<Vector2> pOPaletteData, List<int> pOTriangles)
        {
            Vector3[] lOCorner = new Vector3[4];
            float[] lfU = new float[4];

            for (int i = 0; i < 4; i++)
            {
                int liVertex = pOFace.VertexIndices[i];
                UWVector3 lV = pOModel.Vertices[liVertex];

                lOCorner[i] = new Vector3(lV.X * pfModelScale,
                    pOModel.CeilingVertexIndices.Contains(liVertex) ? pfLocalCeiling : lV.Z * pfModelScale,
                    lV.Y * pfModelScale);
                lfU[i] = pOFace.Uvs[i].X;
            }

            // The side's two top corners and two bottom ones, each pair at the same height.
            if (!Mathf.Approximately(lOCorner[0].y, lOCorner[1].y) || !Mathf.Approximately(lOCorner[2].y, lOCorner[3].y)
                || lOCorner[0].y <= lOCorner[3].y || pOResolved.Size.x <= 0 || pOResolved.Size.y <= 0)
                return false;

            float lfWidth = Vector2.Distance(new Vector2(lOCorner[0].x, lOCorner[0].z), new Vector2(lOCorner[1].x, lOCorner[1].z));
            float lfTexel = lfWidth / (Mathf.Abs(lfU[1] - lfU[0]) * pOResolved.Size.x);
            float lfRepeat = lfTexel * pOResolved.Size.y;

            if (lfRepeat <= 0.01f)
                return false;

            float lfBottom = lOCorner[3].y;
            float lfTop = lOCorner[0].y;
            Vector2 lOOrigin = pOResolved.UvRect.position;
            Vector2 lOSize = pOResolved.UvRect.size;

            // Pieces between the multiples of the repeat in world height.
            float lfFrom = lfBottom;

            while (lfFrom < lfTop - 0.001f)
            {
                float lfWorldFrom = pfBaseY + lfFrom;
                float lfStart = Mathf.Floor((lfWorldFrom + 0.001f) / lfRepeat) * lfRepeat;
                float lfTo = Mathf.Min(lfTop, lfStart + lfRepeat - pfBaseY);
                float lfVFrom = (lfWorldFrom - lfStart) / lfRepeat;
                float lfVTo = (pfBaseY + lfTo - lfStart) / lfRepeat;
                int liBase = pOVerts.Count;

                // Top U0, top U1, bottom U1, bottom U0 - the model's order.
                pOVerts.Add(new Vector3(lOCorner[0].x, lfTo, lOCorner[0].z));
                pOVerts.Add(new Vector3(lOCorner[1].x, lfTo, lOCorner[1].z));
                pOVerts.Add(new Vector3(lOCorner[2].x, lfFrom, lOCorner[2].z));
                pOVerts.Add(new Vector3(lOCorner[3].x, lfFrom, lOCorner[3].z));

                pOUvs.Add(lOOrigin + Vector2.Scale(new Vector2(lfU[0], lfVTo), lOSize));
                pOUvs.Add(lOOrigin + Vector2.Scale(new Vector2(lfU[1], lfVTo), lOSize));
                pOUvs.Add(lOOrigin + Vector2.Scale(new Vector2(lfU[2], lfVFrom), lOSize));
                pOUvs.Add(lOOrigin + Vector2.Scale(new Vector2(lfU[3], lfVFrom), lOSize));

                for (int i = 0; i < 4; i++)
                {
                    pOColors.Add(new Color32(255, 255, 255, 255));
                    pOPaletteData.Add(Vector2.zero);
                }

                pOTriangles.Add(liBase);
                pOTriangles.Add(liBase + 1);
                pOTriangles.Add(liBase + 2);
                pOTriangles.Add(liBase);
                pOTriangles.Add(liBase + 2);
                pOTriangles.Add(liBase + 3);

                lfFrom = lfTo;
            }

            return true;
        }
        private const int GravestoneObjectId = UWEndgameRules.GravestoneObjectId;
        private const int MoongateObjectId = UWObjectMechanics.MoongateObjectId;
        private const int TableObjectId = 0x0158;
        private const int ChestObjectId = UWObjectMechanics.ChestObjectId;
        private const int NightstandObjectId = UWObjectMechanics.NightstandObjectId;
        private const int BarrelObjectId = UWObjectMechanics.BarrelObjectId;
        private const int ChairObjectId = 0x015C;

        /// <summary>
        /// Crossbow bolt and arrow. They are not in the object-to-model table of UW.EXE
        /// (which lists model 0x08 for ids 0x151/0x152) - that they still use model 0x08 is
        /// shown by a screenshot from the original: the user
        /// picked up a fired bolt, dropped it again and took a screenshot
        /// (2026-09-07). It lies there on the floor as a slim shaft with a tip, not
        /// as a flat image.
        /// </summary>
        private const int CrossbowBoltObjectId = UWObjectMechanics.CrossbowBoltObjectId;

        private const int ArrowObjectId = UWObjectMechanics.ArrowObjectId;
        private const int RunestoneFirstId = 232;
        private const int RunestoneLastId = 255;
        private const int RunestoneTextureIndex = 224;
        private const int DecalObjectId = 366;

        /// <summary>force field - a wall panel like 366/367 with the wall texture from the
        /// owner field (see UWLevel); on level 9 the black "nothing".</summary>
        private const int ForceFieldObjectId = UWObjectMechanics.ForceFieldObjectId;

        /// <summary>Range per brightness level. A tile is 64 units wide.</summary>
        private const float LightRangePerBrightness = 90f;
        /// <summary>
        /// Brightness at a distance of one tile. URP uses physical
        /// falloff, so the intensity scales with the square of the tile width.
        /// </summary>
        private const float LightBrightnessAtOneTile = 1.2f;

        private const float DoorHeight = 52f;
        private const float DoorHalfWidth = 16f;

        /// <summary>Unity's built-in layer "Ignore Raycast": collides with everything, but
        /// is skipped by Physics.Raycast without a mask. For colliders that should only
        /// stop the player (see fSpawnPortcullis).</summary>
        private const int IgnoreRaycastLayer = 2;

        /// <summary>Distance of the hinge axis from the door centre - half a tile width plus play.</summary>
        private const float DoorHingeOffset = 16.5f;

        /// <summary>Half thickness of the thickest door part - the bars of a
        /// portcullis (BarDepth 1.5). See fGetDoorTranslation.</summary>
        private const float DoorAssemblyHalfDepth = 1.5f;

        /// <summary>Resolved texture of an object, together with the matching material.</summary>
        private struct UWTextureRef
        {
            /// <summary>Lives in the geometry's Texture2DArray instead of the object atlas.</summary>
            public bool IsArray;
            public int Slice;
            public Rect UvRect;
            public Vector2Int Size;
            public Material Material;
        }

        private readonly DataImport mOData;
        private readonly UWObjectAtlas mOAtlas;
        private readonly Material mOBillboardMaterial;

        /// <summary>For sprites with translucent pixels (fog, ghosts - see
        /// UWTransparencyTables). Null if none was set; then the ordinary billboard
        /// material is used as before.</summary>
        private readonly Material mOTranslucentBillboardMaterial;
        private readonly Material mODecalMaterial;
        private readonly Material mODungeonMaterial;
        private readonly int miFloorSliceOffset;

        /// <summary>
        /// 3D models interpreted from the original uw.exe (shrine/ankh, more in
        /// future) - null if no .exe is configured. Then the affected object
        /// falls back to its 2D sprite.
        /// </summary>
        private readonly UW3DModelImport mO3DModels;

        /// <summary>For the creatures: they need the tile data to walk (see
        /// UWCritter). Null as long as nobody passes a loader - then they stand still.</summary>
        private readonly UWLevelLoader mOLevelLoader;

        private readonly Dictionary<long, Mesh> mOQuadCache = new Dictionary<long, Mesh>();

        /// <summary>Material copies with _DepthBias set, for the animated objects -
        /// see AnimatedBillboardDepthBias. One per source material, because an animated
        /// sprite can be opaque (fountain water) or translucent (fog).</summary>
        private readonly Dictionary<Material, Material> mOAnimatedBillboardMaterials = new Dictionary<Material, Material>();

        /// <summary>A fountain consists of the basin (302) and the animated water (457),
        /// both at exactly the same spot in the tile. Coincident sprites fight over
        /// depth, and for us the basin won - in the original the water is in front
        /// (per user, 2026-08-30). This bias pulls animated sprites an eighth of a
        /// tile towards the camera; that settles the depth unambiguously and, with a
        /// tile width of 64, is not visible as a positional shift.
        ///
        /// Deliberately kept small: the bias also makes the sprite slightly larger,
        /// because it is closer to the camera. At 2 units and the usual viewing distance
        /// of one to two tiles that is about three percent.</summary>
        private const float AnimatedBillboardDepthBias = 2f;

        /// <summary>Palette index (palette 0) of the notch/reveal colour on hand-built
        /// geometry using the dungeon material (door frame narrow sides, door lintel underside) -
        /// determined by screenshot comparison with the original (target colour #171717, index 0xF7 =
        /// RGB(24,24,24) matches practically exactly).</summary>
        private const byte RevealPaletteIndex = 0xF7;

        /// <summary>First slice of the 256 palette colour tiles in the Texture2DArray of the
        /// dungeon geometry, see UWTextureArrayBuilder.GetPaletteSliceOffset. -1 if
        /// no data is present.</summary>
        private readonly int miPaletteSliceOffset;

        /// <summary>Slice of the notch/reveal colour (RevealPaletteIndex) in the Texture2DArray. -1
        /// if no data is present (then the affected faces show the wall texture itself
        /// as a substitute).</summary>
        private readonly int miRevealSlice;

        /// <summary>
        /// Material for the solid-colour faces of baked-in 3D models - chests, barrels and
        /// similar.
        ///
        /// SEPARATE MATERIAL ON PURPOSE, although in the colour path it shows the same as the
        /// object material. Only these faces carry their palette index on the vertex instead
        /// of in a texture; in the palette renderer they therefore need their own shader
        /// (UW/ModelPalette), and a shader of its own needs a material of its own.
        ///
        /// The detour pays off because a shader that reads a vertex channel otherwise places an
        /// invisible requirement on every foreign mesh that uses it - that failed
        /// three times (see UW/ModelPalette).
        /// </summary>
        private readonly Material mOModelSolidMaterial;

        /// <summary>Material of the portcullis - like mOModelSolidMaterial, except that the face
        /// FACING the viewer is omitted.</summary>
        private readonly Material mOPortcullisMaterial;

        public int SpawnedCount { get; private set; }

        /// <summary>Objects whose texture could not be resolved.</summary>
        public int UnresolvedCount { get; private set; }

        public UWObjectSpawner(DataImport pOData, UWObjectAtlas pOAtlas, Material pOBillboardMaterial, Material pODecalMaterial, Material pODungeonMaterial, int piFloorSliceOffset, UW3DModelImport pO3DModels = null, Material pOTranslucentBillboardMaterial = null, UWLevelLoader pOLevelLoader = null, Material pOModelSolidMaterial = null, Material pOPortcullisMaterial = null)
        {
            mOPortcullisMaterial = pOPortcullisMaterial;
            mOModelSolidMaterial = pOModelSolidMaterial;
            mOTranslucentBillboardMaterial = pOTranslucentBillboardMaterial;
            mOLevelLoader = pOLevelLoader;
            mOData = pOData;
            mOAtlas = pOAtlas;
            mOBillboardMaterial = pOBillboardMaterial;
            mODecalMaterial = pODecalMaterial;
            mODungeonMaterial = pODungeonMaterial;
            miFloorSliceOffset = piFloorSliceOffset;
            mO3DModels = pO3DModels;
            miPaletteSliceOffset = pOData != null ? UWTextureArrayBuilder.GetPaletteSliceOffset(pOData) : -1;
            miRevealSlice = miPaletteSliceOffset >= 0 ? miPaletteSliceOffset + RevealPaletteIndex : -1;
        }

        /// <summary>Does this spawner work with exactly this atlas and these materials? Otherwise
        /// UWLevelLoader has to build a new one.</summary>
        public bool Uses(UWObjectAtlas pOAtlas, Material pOBillboardMaterial, Material pODecalMaterial,
            Material pODungeonMaterial, Material pOTranslucentBillboardMaterial, Material pOModelSolidMaterial,
            Material pOPortcullisMaterial)
        {
            return mOAtlas == pOAtlas && mOBillboardMaterial == pOBillboardMaterial
                && mODecalMaterial == pODecalMaterial && mODungeonMaterial == pODungeonMaterial
                && mOTranslucentBillboardMaterial == pOTranslucentBillboardMaterial
                && mOModelSolidMaterial == pOModelSolidMaterial && mOPortcullisMaterial == pOPortcullisMaterial;
        }

        /// <summary>Releases the cached quads - once the level that used them
        /// has been torn down.</summary>
        public void DestroyCachedMeshes()
        {
            foreach (Mesh lOMesh in mOQuadCache.Values)
            {
                if (lOMesh != null)
                    Object.Destroy(lOMesh);
            }

            mOQuadCache.Clear();
        }

        public void Spawn(UWLevel pOLevel, Transform pOParent)
        {
            SpawnedCount = 0;
            UnresolvedCount = 0;
            mOPanelsOnWall.Clear();

            for (int z = 0; z < UWLevelMeshBuilder.TilesPerAxis; z++)
            {
                for (int x = 0; x < UWLevelMeshBuilder.TilesPerAxis; x++)
                {
                    UWTile lOTile = pOLevel.TileData[(z * UWLevelMeshBuilder.TilesPerAxis) + x];

                    if (lOTile.TileType == UWTile.TileTypeEnum.solid)
                        continue;

                    for (int i = 0; i < lOTile.ObjectsInTile.Count; i++)
                        fSpawnObject(x, z, lOTile, lOTile.ObjectsInTile[i], pOParent);
                }
            }
        }

        /// <summary>For UWLevelLoader.SpawnDroppedObject (original: dropping/throwing a single item
        /// after the level has already been built) - the same dispatch logic as the
        /// full level build (Spawn), just for one object instead of the whole tile list.</summary>
        public void SpawnSingleObject(int xPos, int zPos, UWTile pOTile, UWObject pOObject, Transform pOParent)
        {
            fSpawnObject(xPos, zPos, pOTile, pOObject, pOParent);
        }

        private void fSpawnObject(int xPos, int zPos, UWTile pOTile, UWObject pOObject, Transform pOParent)
        {
            if (pOObject == null || pOObject.Texture == null)
                return;

            UWObject.ObjectCategoryEnum leCategory = pOObject.GetCategory();

            // Traps/triggers are pure logic markers (see UWTriggerSystem), not
            // visible items - without this early exit they would fall through to
            // fSpawnBillboard and be visible as floating, pickable sprites
            // (like every object <=460 they have an OBJECTS.GR texture assigned, see
            // UWLevel.load_object_list).
            if (leCategory == UWObject.ObjectCategoryEnum.Traps || leCategory == UWObject.ObjectCategoryEnum.Triggers)
                return;

            // Where the object stands in its tile's chain, and whether a model of the tile is
            // sorted with it - see UWOwnTile.RegisterModelTile.
            miSpawnChainIndex = pOTile != null && pOTile.ObjectsInTile != null ? pOTile.ObjectsInTile.IndexOf(pOObject) : -1;
            mbSpawnTileHasModel = fHasModel(pOTile);

            if (leCategory == UWObject.ObjectCategoryEnum.Doors)
                fSpawnDoor(xPos, zPos, pOTile, pOObject, pOParent);
            else if (mO3DModels != null && fGet3DModelIndex(pOObject.ID) >= 0)
                fSpawn3DModel(xPos, zPos, pOObject, pOParent, fGet3DModelIndex(pOObject.ID));
            else if (leCategory == UWObject.ObjectCategoryEnum.PillarSomeDecalsForceFieldSpecialTmapObj
                || leCategory == UWObject.ObjectCategoryEnum.Switches)
                fSpawnDecal(xPos, zPos, pOObject, pOParent);
            else
                fSpawnBillboard(xPos, zPos, pOObject, pOParent);

            miSpawnChainIndex = -1;
            mbSpawnTileHasModel = false;
        }

        /// <summary>The place in the tile's chain of the object being spawned, -1 outside
        /// fSpawnObject (a sprite without one counts as the last of its tile).</summary>
        private int miSpawnChainIndex = -1;

        private bool mbSpawnTileHasModel;

        private bool fHasModel(UWTile pOTile)
        {
            if (mO3DModels == null || pOTile == null || pOTile.ObjectsInTile == null)
                return false;

            for (int liAt = 0; liAt < pOTile.ObjectsInTile.Count; liAt++)
            {
                UWObject lOObject = pOTile.ObjectsInTile[liAt];

                if (lOObject != null && lOObject.ID != BridgeObjectId && fGet3DModelIndex(lOObject.ID) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>The COMOBJ radius of a big object (byte 1 bit 3), 0 for every other.</summary>
        private int fGetBigRadius(int piObjectId)
        {
            if (mOData != null && mOData.CommonObjectProperties != null
                && mOData.CommonObjectProperties.TryGet(piObjectId, out UWCommonObjectProperties.Entry lOCommon)
                && lOCommon.IsAnimated)
                return lOCommon.Radius;

            return 0;
        }

        /// <summary>
        /// Attaches hit points to a creature. Both come from the creature table
        /// in OBJECTS.DAT: vitality and armour. Objects outside the creature range
        /// (0x40 to 0x7f) get nothing - a pile of bones should not have hit
        /// points.
        /// </summary>
        private void fAttachCritterHealth(GameObject pOGameObject, UWObject pOObject)
        {
            if (mOData == null || mOData.ObjectClassProperties == null)
                return;

            if (!mOData.ObjectClassProperties.TryGetCritter(pOObject.ID,
                out UWObjectClassProperties.Critter lOCritter))
            {
                return;
            }

            if (lOCritter.Vitality <= 0)
            {
                // No vitality in the table: the void creatures on level 9 (0x4F,
                // 0x7D, 0x7E - skull, lightning, eye) with goal 0xB. No hit points, no
                // combat - but animated images and drifting around (see UWVoidCreature).
                if (pOObject is UWNpc lOVoidNpc && lOVoidNpc.NPCGoal == UWVoidCreature.VoidGoal)
                {
                    pOGameObject.AddComponent<UWCritterAnimator>().Initialise(mOData, pOObject.ID,
                        mOBillboardMaterial, mOTranslucentBillboardMaterial);
                    pOGameObject.AddComponent<UWVoidCreature>();
                }

                return;
            }

            // The UWDamageable's single armour value is the body's (row byte 0). A blow or a
            // missile asks UWCritter.GetArmourAgainst for the part it hits instead
            // (AttackerAppliesFinalDamage: row[part % 4], 0xFF falls back to byte 0, deviation
            // 49); this one remains for everything that has no heights, such as a blast.
            int liArmour = lOCritter.ArmourOfPart(UWArmourProtection.PartBody);

            // Which damage types the creature is immune to is not in the
            // creature table but in comobj.dat for each object (see
            // UWDamageTypes). Undead is likewise only noted there.
            int liResistances = 0;

            if (mOData != null && mOData.CommonObjectProperties != null
                && mOData.CommonObjectProperties.TryGet(pOObject.ID, out UWCommonObjectProperties.Entry lOCommon))
                liResistances = lOCommon.Resistances;

            // Hit points are stored PER CREATURE in the object data (npc_hp) - the table
            // only supplies the average. Counted (2026-09-10): of 595 creatures in the
            // data folder only 37 match the table, 281 have fewer, 272 more.
            // The reference subtracts damage from npc_hp and lets the creature die at 0
            // (damage.cs). Only then does an injury also survive saving and loading. Where
            // the data holds 0, the table value is used.
            UWNpc lONpc = pOObject as UWNpc;
            int liHitPoints = lONpc != null && lONpc.HitPoints > 0 ? lONpc.HitPoints : lOCritter.Vitality;

            pOGameObject.AddComponent<UWDamageable>().Initialise(Mathf.Max(lOCritter.Vitality, liHitPoints), liArmour, 0,
                lOCritter.DefensePower, liResistances, liHitPoints);

            // Behaviour: perceive, pursue, strike (see UWCritter). The attitude
            // is stored on the object itself, speed and damage in the creature table.
            pOGameObject.AddComponent<UWCritter>().Initialise(pOObject as UWNpc, lOCritter, true, mOLevelLoader);

            // Animated images instead of a static sprite (see UWCritterAnimator). If the
            // creature finds no animation, it keeps the previous sprite.
            pOGameObject.AddComponent<UWCritterAnimator>().Initialise(mOData, pOObject.ID,
                mOBillboardMaterial, mOTranslucentBillboardMaterial);

            // What is left behind on death - corpse, bones, pool of blood (see
            // UWCritterRemains).
            pOGameObject.AddComponent<UWCritterRemains>().Initialise(lOCritter, mOLevelLoader);

            fApplyCritterFootprint(pOGameObject, pOObject.ID);
        }

        /// <summary>
        /// Attaches hit points to a door. Unlike creatures there is no dedicated field
        /// for it - the object's QUALITY is used, which in the original also carries
        /// the displayed condition (broken to massive, string block 5). The
        /// quality class from COMOBJ.DAT selects the right group of six words.
        /// </summary>
        /// <returns>False if the thing cannot be damaged at all. It still gets a
        /// UWDamageable - its condition is part of the look message - but no behaviour
        /// that only matters when something breaks.</returns>
        private bool fAttachHealth(GameObject pOGameObject, UWObject pOObject)
        {
            int liQualityType = 0;
            int liQualityClass = 0;

            if (mOData != null && mOData.CommonObjectProperties != null
                && mOData.CommonObjectProperties.TryGet(pOObject.ID, out UWCommonObjectProperties.Entry lOCommon))
            {
                liQualityType = lOCommon.QualityType;
                liQualityClass = lOCommon.QualityClass;
            }

            // Quality 0 would be a door that starts out already broken - at least 1.
            int liHealth = pOObject.Quality < 1 ? 1 : pOObject.Quality;

            UWDamageable lODamageable = pOGameObject.AddComponent<UWDamageable>();

            lODamageable.Initialise(liHealth, 0, liQualityType, 0, 0, -1, liQualityClass);

            // WORD 0 BIT 13 shrugs every blow off as well (DamageObjectAndDoors returns at once) -
            // 48 of the doors in LEV.ARK, wooden ones included (see UWHealth.IsProtected).
            lODamageable.IsProtected = pOObject.DoorDirection;

            // Class 3 shrugs every blow off (reference: damage.DamageOtherObjectTypes returns
            // straight away for it) - the metal door, the portcullis, pillar and bridge.
            return liQualityClass != UWCombat.AlwaysBestQualityClass && !pOObject.DoorDirection;
        }

        /// <summary>Hit points for a door, plus what a broken door does.</summary>
        private void fAttachDoorHealth(GameObject pOGameObject, UWObject pOObject)
        {
            if (fAttachHealth(pOGameObject, pOObject))
                pOGameObject.AddComponent<UWDoorDamage>();
        }

        /// <summary>
        /// Shifts a model that fills a whole tile so that its extent lies
        /// exactly on the tile. Anything narrower stays where it is.
        ///
        /// The threshold is nine tenths of a tile: bridge and door frame measure
        /// exactly one, table (0.6) and shrine (0.4) considerably less.
        /// </summary>
        private static Vector3 fAlignFullTileModel(Vector3 pOPosition, int piTileX, int piTileZ, Mesh pOMesh, float pfHeadingDegrees)
        {
            if (pOMesh == null)
                return pOPosition;

            Bounds lOBounds = pOMesh.bounds;

            float lfThreshold = UWLevelMeshBuilder.TileSpacing * 0.9f;

            if (lOBounds.size.x < lfThreshold || lOBounds.size.z < lfThreshold)
                return pOPosition;

            // After its position is set, the object is also rotated about the vertical axis -
            // so the extent rotates with it. Without the rotation here the alignment
            // was only right for unrotated bridges; the rotated group around tile 45/37
            // even got worse because of it (per user, 2026-08-30).
            Vector3 lOCentre = Quaternion.Euler(0f, pfHeadingDegrees, 0f) * lOBounds.center;

            // The centre of the extent should lie on the tile centre. The height stays as
            // it is - it comes from the object's ZPos and is correct.
            return new Vector3(
                (piTileX * UWLevelMeshBuilder.TileSpacing) - lOCentre.x,
                pOPosition.y,
                (piTileZ * UWLevelMeshBuilder.TileSpacing) - lOCentre.z);
        }
        /// <summary>Like fGetObjectPosition, but with the scale for wall-mounted things - see
        /// DecalSubTileScale.</summary>
        private static Vector3 fGetDecalPosition(int xPos, int zPos, UWObject pOObject)
        {
            Vector3 lOAt = UWViewpoint.SubTileToWorld(xPos, zPos, pOObject, DecalSubTileScale);

            // ALONG THE WALL the object stands where every object stands, with the offset
            // within its eighth (see SubTileOffset) - the wall plate in front of Drog had to
            // move 4 to 5 units along the wall (per user, 2026-09-17). ACROSS the wall the
            // decal scale stays, so a plaque at 7 keeps touching the wall. Facing 0 and 4
            // turn the quad to face along Z (wall runs along X), 2 and 6 along X; diagonal
            // facings keep both axes as they were.
            if (pOObject.Heading == 0 || pOObject.Heading == 4)
                lOAt.x = UWViewpoint.TileToWorldAxis(xPos) + (pOObject.XPos * SubTileScale) + SubTileOffset;
            else if (pOObject.Heading == 2 || pOObject.Heading == 6)
                lOAt.z = UWViewpoint.TileToWorldAxis(zPos) + (pOObject.YPos * SubTileScale) + SubTileOffset;

            return lOAt + fGetDrawOffset();
        }

        /// <summary>The same calculation as fGetObjectPosition, for callers outside the spawner - the detached
        /// camera uses it to place itself exactly where a visible object of the same trap
        /// would stand, instead of copying the formula a fourth time.</summary>
        public static Vector3 GetObjectWorldPosition(int piTileX, int piTileZ, UWObject pOObject)
        {
            return fGetObjectPosition(piTileX, piTileZ, pOObject);
        }

        /// <summary>Position of an object within its tile, as in the old build.</summary>
        private static Vector3 fGetObjectPosition(int xPos, int zPos, UWObject pOObject)
        {
            return UWViewpoint.SubTileToWorld(xPos, zPos, pOObject) + fGetDrawOffset();
        }

        /// <summary>The tiny offset against z-fighting. It belongs to CREATING a
        /// visible body, not to the conversion - that is why it is here and not in
        /// UWViewpoint.SubTileToWorld.</summary>
        private static Vector3 fGetDrawOffset()
        {
            return new Vector3(DrawOffset, 0f, DrawOffset);
        }

        private const float DrawOffset = 0.1f;

        /// <summary>Hands a model the floor of the tile it stands in, for the own-tile rule
        /// (UWOwnTile): its origin may lie below that floor, the shrine's does. Only the model face
        /// material reads it; the other submeshes ignore the block.</summary>
        private static void fSetModelDepthRange(Renderer pORenderer, float pfFloorY)
        {
            if (pORenderer == null)
                return;

            MaterialPropertyBlock lOBlock = new MaterialPropertyBlock();

            pORenderer.GetPropertyBlock(lOBlock);
            lOBlock.SetFloat(UWOwnTile.ModelFloorId, pfFloorY);
            pORenderer.SetPropertyBlock(lOBlock);
        }

        /// <summary>
        /// The original's draw order for an object sprite (UWPainterOrder.hlsl, seg033_2EEF_43B):
        /// a BIG object (COMOBJ byte 1 bit 3, radius in bits 0-2) that pokes into the nearer row or
        /// the next inner column is drawn with that tile, and the animations 0x1C0-0x1FF sort one
        /// step nearer. The hit effects are both (radius 3): without this a blood splat stayed in
        /// the creature's own tile while the creature was handed to the nearer row, and the splat
        /// showed behind it (per user, 2026-09-28). In a tile with a 3D model the sprite also gets
        /// its place in the tile's chain, for the ties with the models (UWOwnTile.RegisterModelTile).
        /// Items without any of it keep the material's values.
        /// </summary>
        private void fSetPainterPlace(Renderer pORenderer, int piObjectId)
        {
            if (pORenderer == null)
                return;

            int liRadius = fGetBigRadius(piObjectId);
            float lfKeyBonus = (piObjectId & 0x1C0) == 0x1C0 ? -1f : 0f;
            bool lbChain = mbSpawnTileHasModel && miSpawnChainIndex >= 0;

            if (liRadius == 0 && lfKeyBonus == 0f && !lbChain)
                return;

            MaterialPropertyBlock lOBlock = new MaterialPropertyBlock();

            pORenderer.GetPropertyBlock(lOBlock);
            lOBlock.SetFloat(UWOwnTile.BigRadiusProperty, liRadius);
            lOBlock.SetFloat(UWOwnTile.KeyBonusProperty, lfKeyBonus);

            if (lbChain)
                lOBlock.SetFloat(UWOwnTile.ChainIndexProperty, Mathf.Min(miSpawnChainIndex, UWOwnTile.MaxChainIndex));

            pORenderer.SetPropertyBlock(lOBlock);
        }

        /// <summary>How far an image on a diagonal wall stands out from the wall plane.</summary>
        private const float DiagonalDecalLift = 0.25f;

        /// <summary>
        /// A WALL PANEL IS SEEN FROM THE FRONT ONLY (per user, 2026-09-30, on the waterfall of
        /// level 3, 56/53: in the original one side can be seen through, the back not). The
        /// original draws 366/367 through the model renderer with model 0x16 (seg032_2DCA_3DA,
        /// minor class 2: a quad a tile wide and a tile high, 1/16 in front, ONE face) and its wall
        /// texture from the owner. The waterfall has two panels facing the passage and one, only
        /// the upper, facing back from the cave - so from the cave the lower part is open. Our
        /// decal material is double-sided; a panel gets a copy that culls its back.
        /// </summary>
        private Material fGetSingleSided(Material pOMaterial)
        {
            if (pOMaterial == null || !pOMaterial.HasFloat(CullProperty))
                return pOMaterial;

            if (!mOSingleSided.TryGetValue(pOMaterial, out Material lOCopy))
            {
                lOCopy = new Material(pOMaterial);
                lOCopy.name = pOMaterial.name + " (front only)";
                lOCopy.SetFloat(CullProperty, (float)UnityEngine.Rendering.CullMode.Back);
                mOSingleSided[pOMaterial] = lOCopy;
            }

            return lOCopy;
        }

        private const string CullProperty = "_Cull";

        private readonly Dictionary<Material, Material> mOSingleSided = new Dictionary<Material, Material>();

        /// <summary>Depth of the trigger box a panel of height 0 gets for looking and using -
        /// see fSpawnDecal.</summary>
        private const float DecalPickDepth = 1f;

        /// <summary>Centre of the quad of models 0x10-0x12 along its X: (-1/16 + 3/16) / 2 of a
        /// tile, see fSpawnDecal.</summary>
        private const float WallModelImageShift = TileSpacing / 16f;

        private const int LeverObjectId = 0x0161;
        private const int WallSwitchObjectId = 0x0162;

        private static bool fIsOffCentreWallModel(int piObjectId)
        {
            return piObjectId == LeverObjectId || piObjectId == WallSwitchObjectId
                || piObjectId == UWObjectMechanics.WritingObjectId
                || piObjectId == DecalObjectId || piObjectId == DecalObjectId + 1;
        }

        /// <summary>How far a wall panel stands off its wall, once or twice by the parity of its
        /// tile, so that two panels of neighbouring tiles overlapping along a wall do not share a
        /// plane - see fSpawnDecal.</summary>
        private const float WallPanelStagger = 0.15f;

        /// <summary>How far model 0x16's quad lies behind the panel's eighth: 1/16 of a tile (UW.EXE
        /// model nodes, y 0.0625) - see fSpawnDecal.</summary>
        private const float WallModelBackOffset = TileSpacing / 16f;

        /// <summary>Whether a wall panel's plane lies on the edge of its tile on its back side, where
        /// a wall may stand behind it (within a unit); pOEdge is the position moved onto that edge.
        /// Diagonal facings count as on the edge and keep their position.</summary>
        private static bool fGetTileEdgeBehind(Vector3 pOPosition, int piTileX, int piTileZ, Vector3 pOFront, out Vector3 pOEdge)
        {
            pOEdge = pOPosition;

            if (Mathf.Abs(pOFront.x) > 0.5f && Mathf.Abs(pOFront.z) > 0.5f)
                return true;

            if (Mathf.Abs(pOFront.z) > 0.5f)
            {
                float lfEdge = UWViewpoint.TileToWorldAxis(piTileZ) + (pOFront.z > 0f ? 0f : TileSpacing);

                pOEdge.z = lfEdge;

                return Mathf.Abs(pOPosition.z - lfEdge) < 1f;
            }

            float lfEdgeX = UWViewpoint.TileToWorldAxis(piTileX) + (pOFront.x > 0f ? 0f : TileSpacing);

            pOEdge.x = lfEdgeX;

            return Mathf.Abs(pOPosition.x - lfEdgeX) < 1f;
        }

        /// <summary>How many wall panels this spawner has put on a wall of a tile so far - see
        /// fSpawnDecal.</summary>
        private readonly Dictionary<long, int> mOPanelsOnWall = new Dictionary<long, int>();

        /// <summary>Whether the tile at these coordinates of the level being built is a diagonal one.</summary>
        private bool fIsDiagonalTile(int piTileX, int piTileZ)
        {
            UWLevel lOLevel = mOLevelLoader != null ? mOLevelLoader.CurrentLevel : null;

            if (lOLevel == null || piTileX < 0 || piTileZ < 0
                || piTileX >= UWLevelMeshBuilder.TilesPerAxis || piTileZ >= UWLevelMeshBuilder.TilesPerAxis)
                return false;

            UWTile lOTile = lOLevel.TileData[(piTileZ * UWLevelMeshBuilder.TilesPerAxis) + piTileX];

            return lOTile != null && (lOTile.TileType == UWTile.TileTypeEnum.diagonal_ne
                || lOTile.TileType == UWTile.TileTypeEnum.diagonal_nw
                || lOTile.TileType == UWTile.TileTypeEnum.diagonal_se
                || lOTile.TileType == UWTile.TileTypeEnum.diagonal_sw);
        }

        /// <summary>
        /// Resolves an object's graphic. Wall and floor textures live in the
        /// geometry's Texture2DArray, everything else in the object atlas.
        /// </summary>
        private bool fResolve(UWTexture pOTexture, bool pbBillboard, out UWTextureRef pOResult)
        {
            pOResult = default(UWTextureRef);

            if (pOTexture == null)
                return false;

            if (pOTexture.TextureType == UWTexture.TextureTypes.WALL || pOTexture.TextureType == UWTexture.TextureTypes.FLOOR)
            {
                // Billboards cannot carry array textures - the billboard shader
                // reads two-dimensional UVs. Does not occur in the data either.
                if (pbBillboard || mODungeonMaterial == null)
                    return false;

                pOResult.IsArray = true;
                pOResult.Slice = pOTexture.TextureType == UWTexture.TextureTypes.WALL
                    ? pOTexture.Index
                    : miFloorSliceOffset + pOTexture.Index;
                pOResult.Size = new Vector2Int(Mathf.Max(1, pOTexture.Width), Mathf.Max(1, pOTexture.Height));
                pOResult.Material = mODungeonMaterial;

                return true;
            }

            UWObjectAtlas.Entry lOAtlasEntry;

            if (mOAtlas == null || !mOAtlas.TryGetEntry(pOTexture, out lOAtlasEntry))
                return false;

            pOResult.IsArray = false;
            pOResult.UvRect = lOAtlasEntry.UvRect;
            pOResult.Size = lOAtlasEntry.Size;

            // Sprites with translucent pixels need alpha blending instead of the hard
            // cutoff - see UWTransparencyTables and the material in
            // UWLevelLoader.fCreateObjectMaterials.
            pOResult.Material = pbBillboard
                ? (lOAtlasEntry.IsTranslucent && mOTranslucentBillboardMaterial != null
                    ? mOTranslucentBillboardMaterial
                    : mOBillboardMaterial)
                : mODecalMaterial;

            return pOResult.Size.x > 0 && pOResult.Size.y > 0;
        }

        /// <summary>
        /// Applies UVs to a mesh. The dungeon shader expects three-dimensional
        /// coordinates with the slice in z, the atlas shader two-dimensional ones.
        /// </summary>
        private static void fApplyUVs(Mesh pOMesh, Vector2[] pONormalised, UWTextureRef pOTexture)
        {
            if (pOTexture.IsArray)
            {
                List<Vector3> lOUVs = new List<Vector3>(pONormalised.Length);

                for (int i = 0; i < pONormalised.Length; i++)
                    lOUVs.Add(new Vector3(pONormalised[i].x, pONormalised[i].y, pOTexture.Slice));

                pOMesh.SetUVs(0, lOUVs);
                return;
            }

            Vector2[] lOAtlasUVs = new Vector2[pONormalised.Length];

            for (int i = 0; i < pONormalised.Length; i++)
            {
                lOAtlasUVs[i] = new Vector2(
                    pOTexture.UvRect.x + pONormalised[i].x * pOTexture.UvRect.width,
                    pOTexture.UvRect.y + pONormalised[i].y * pOTexture.UvRect.height);
            }

            pOMesh.uv = lOAtlasUVs;
        }

        /// <summary>
        /// Quad at original size with its pivot at bottom centre, one pixel equals one
        /// world unit. Built only once per texture source and index.
        /// </summary>
        private Mesh fGetQuad(UWTexture pOTexture, UWTextureRef pOResolved)
        {
            long liKey = ((long)pOTexture.TextureType << 32) | (uint)pOTexture.Index;

            Mesh lOCached;

            if (mOQuadCache.TryGetValue(liKey, out lOCached))
                return lOCached;

            float lfHalfWidth = pOResolved.Size.x * 0.5f;
            float lfHeight = pOResolved.Size.y;

            Mesh lOMesh = new Mesh();
            lOMesh.name = string.Format("UWQuad {0}_{1}", pOTexture.TextureType, pOTexture.Index);

            lOMesh.vertices = new Vector3[]
            {
                new Vector3(-lfHalfWidth, 0f, 0f),
                new Vector3(lfHalfWidth, 0f, 0f),
                new Vector3(lfHalfWidth, lfHeight, 0f),
                new Vector3(-lfHalfWidth, lfHeight, 0f)
            };

            fApplyUVs(lOMesh, new Vector2[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f)
            }, pOResolved);

            lOMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            lOMesh.normals = new Vector3[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            lOMesh.RecalculateBounds();

            mOQuadCache[liKey] = lOMesh;

            return lOMesh;
        }

        /// <summary>Whether the tile at these coordinates of the level being built is a slope.</summary>
        private bool fIsSlopeTile(int piTileX, int piTileZ)
        {
            UWLevel lOLevel = mOLevelLoader != null ? mOLevelLoader.CurrentLevel : null;

            if (lOLevel == null || piTileX < 0 || piTileZ < 0
                || piTileX >= UWLevelMeshBuilder.TilesPerAxis || piTileZ >= UWLevelMeshBuilder.TilesPerAxis)
                return false;

            UWTile lOTile = lOLevel.TileData[(piTileZ * UWLevelMeshBuilder.TilesPerAxis) + piTileX];

            return lOTile != null && (lOTile.TileType == UWTile.TileTypeEnum.slope_n
                || lOTile.TileType == UWTile.TileTypeEnum.slope_e
                || lOTile.TileType == UWTile.TileTypeEnum.slope_s
                || lOTile.TileType == UWTile.TileTypeEnum.slope_w);
        }

        private void fSpawnBillboard(int xPos, int zPos, UWObject pOObject, Transform pOParent)
        {
            UWTexture lOTexture = pOObject.Texture;

            // Runestones all share one graphic.
            if (pOObject.ID >= RunestoneFirstId && pOObject.ID <= RunestoneLastId)
                lOTexture = fGetTexture(UWTexture.TextureTypes.OBJECTS, RunestoneTextureIndex) ?? lOTexture;

            // Animated objects (fountain, fire, silver tree) do not show their single
            // OBJECTS.GR graphic but an image sequence from ANIMO.GR - the first frame
            // carries mesh, collider and material, the rest runs through
            // UWAnimatedBillboard.
            UWTexture[] lOAnimation = fGetAnimationTextures(pOObject.ID);

            if (lOAnimation != null)
                lOTexture = lOAnimation[0];

            UWTextureRef lOResolved;

            if (!fResolve(lOTexture, true, out lOResolved))
            {
                UnresolvedCount++;
                return;
            }

            GameObject lOObject = new GameObject("Obj " + pOObject.ID);
            lOObject.layer = SpriteLayer;
            lOObject.transform.SetParent(pOParent, false);
            lOObject.transform.position = fGetObjectPosition(xPos, zPos, pOObject);

            // ON A SLOPE AN OBJECT MUST NOT SINK INTO THE FLOOR. The stored height is counted in
            // whole zpos steps and the slope falls continuously, so an item can lie a few units
            // below our sloped floor: the skull on level 2, 59/14 (slope rising west, zpos 67 at
            // xpos 2 where the floor is about 69.5) looked sunk in (per user, 2026-09-17). Only on
            // slopes, where the rounding happens; on flat floors the stored height stays exact.
            if (mOLevelLoader != null && fIsSlopeTile(xPos, zPos))
            {
                Vector3 lOAt = lOObject.transform.position;

                lOAt.y = Mathf.Max(lOAt.y, mOLevelLoader.GetFloorHeightAt(lOAt));
                lOObject.transform.position = lOAt;
            }

            // In the data the fountain water sits one height step above its basin
            // (ZPos 49 versus 48, i.e. two world units). In the original that is a hint
            // about draw order - for us the depth bias in the shader handles that,
            // and applied as height the water visibly floats too high (per user,
            // 2026-08-30). The compensation lives in UWSettings because its exact value
            // is judged by eye.
            if (lOAnimation != null)
                lOObject.transform.position += new Vector3(0f, fGetAnimatedHeightOffset(), 0f);

            MeshFilter lOFilter = lOObject.AddComponent<MeshFilter>();
            lOFilter.sharedMesh = fGetQuad(lOTexture, lOResolved);
            lOObject.AddComponent<MeshRenderer>().sharedMaterial = lOAnimation != null
                ? fGetAnimatedBillboardMaterial(lOResolved.Material)
                : lOResolved.Material;

            fSetPainterPlace(lOObject.GetComponent<MeshRenderer>(), pOObject.ID);

            if (lOAnimation != null && lOAnimation.Length > 1)
                fAttachAnimation(lOObject, lOFilter, lOAnimation);

            BoxCollider lOCollider = lOObject.AddComponent<BoxCollider>();

            // Square in plan, not paper-thin: the sprite always turns towards the camera in the
            // shader, but the collider does not - with a depth of 1 it stood
            // rigidly in the world XY plane and was hard to hit from the side.
            // That is exactly where the clicks that sometimes missed came from (per user, 2026-08-30).
            lOCollider.size = new Vector3(lOResolved.Size.x, lOResolved.Size.y, lOResolved.Size.x);
            lOCollider.center = new Vector3(0f, lOResolved.Size.y * 0.5f, 0f);

            // COMOBJ.DAT gives every object a height, and 0 there means "no collision".
            // Exactly the things you walk over in the original are at 0: bones,
            // sacks, poles, campfires - and the animated water of a fountain (457),
            // while the basin (302) with height 20 still blocks. As a trigger the
            // body remains for rays (look, use, pick up still hit it),
            // only the player walks through.
            lOCollider.isTrigger = fHasNoCollision(pOObject.ID);

            // Walked over, a glowing rock joins the one the player carries (UWGlowingRockRules).
            if (UWGlowingRockRules.IsGatheredByTouch(pOObject.ID))
                lOObject.AddComponent<UWTouchGather>();

            UWEntityInfo lOInfo = lOObject.AddComponent<UWEntityInfo>();

            // No longer is everything pickable across the board: fixed or painted-on scenery
            // (grass, stalactites, blood stains, campfires) can only be looked at in the
            // original, not taken - see UWFixedScenery.
            // Creatures are never pickable. That is not only self-evident,
            // it also had an unexpected consequence: in the original control scheme the
            // look output suppresses itself as soon as the target is pickable - then
            // UWItemDrag is the sole source (see lbHandledByItemDrag in Interaction).
            // A goblin therefore counted as pickable, and "look" stayed silent for every
            // creature (reported by user, 2026-08-30).
            lOInfo.CanBePickedUp = pOObject.GetCategory() != UWObject.ObjectCategoryEnum.Monsters
                && !UWFixedScenery.IsFixed(pOObject.ID);

            fAttachCritterHealth(lOObject, pOObject);
            lOInfo.ObjectData = pOObject;
            // +1 CONFIRMED correct (per user's knowledge of the original: object ID 35 at tile
            // (24,10) really is "leather leggings", exactly the text at GetDescription(35+1) -
            // so Strings.Blocks[4] is indexed shifted by one relative to the object ID.
            // See UWArmorItemMap for the resulting correction of the table keys.
            lOInfo.Description = fGetDescription(pOObject.ID + 1);

            fAddLightIfSource(lOObject, pOObject, lOResolved.Size.y);

            SpawnedCount++;
        }

        private static float fGetAnimatedFramesPerSecond()
        {
            return UnderworldRevisited.UWSettings.Instance != null
                ? UnderworldRevisited.UWSettings.Instance.AnimatedObjectFramesPerSecond
                : 4f;
        }

        /// <summary>The height correction every animated object gets on placement, for effects put
        /// down outside the spawner (the smoke of UWTileBlast).</summary>
        public static float AnimatedHeightOffset => fGetAnimatedHeightOffset();

        private static float fGetAnimatedHeightOffset()
        {
            return UnderworldRevisited.UWSettings.Instance != null
                ? UnderworldRevisited.UWSettings.Instance.AnimatedObjectHeightOffset
                : -4f;
        }

        /// <summary>
        /// Sets the footprint of the collider to the radius from COMOBJ.DAT.
        ///
        /// Otherwise the body comes from the image size, and for a creature that is the
        /// SPRITE width - 68 units for the goblin, i.e. more than a tile. It therefore stuck out
        /// 34 units forward, and because a creature comes up to within 56 of the player,
        /// the player regularly ended up INSIDE the body. From inside, a body can be left in
        /// any direction - hence walking through (per user, 2026-08-30).
        ///
        /// COMOBJ.DAT lists radius 2 for every creature. The docs do not name the unit;
        /// 6 world units per radius point are assumed, which gives a creature exactly the radius
        /// of the player (whose CharacterController has 12) - a humanoid figure being
        /// as wide as the player is the most obvious assumption.
        ///
        /// Previously this was 8 units per point, and you could no longer get past:
        /// Drog stands in front of a one-tile passage, his body measured 32 and left only
        /// 12 and 20 of the tile's 64 units free - the player needs 24 (per user,
        /// 2026-08-30). With 6, 16 and 24 remain, so you can get past on one side.
        ///
        /// The HEIGHT stays the image height: clicking depends on it, and for that the
        /// sprite height is the right one.
        /// </summary>
        private void fApplyCritterFootprint(GameObject pOGameObject, int piObjectId)
        {
            UWCommonObjectProperties.Entry lOEntry;

            if (mOData == null || mOData.CommonObjectProperties == null
                || !mOData.CommonObjectProperties.TryGet(piObjectId, out lOEntry) || lOEntry.Radius <= 0)
                return;

            BoxCollider lOCollider = pOGameObject.GetComponent<BoxCollider>();

            if (lOCollider == null)
                return;

            float lfSize = lOEntry.Radius * 2f * fGetCritterRadiusScale();

            // A LOW CREATURE MUST NOT BECOME A STEP. The collider is as tall as the picture,
            // one world unit per pixel, and the player's CharacterController climbs anything
            // up to 16 units - exactly one floor level. A flesh slug measures 15 pixels, a
            // rotworm 17, so you walk onto them instead of into them (per user, 2026-09-16:
            // "make the lurker.s hitbox tall enough that you cannot step on it, and raise it a
            // little for rats, rotworm and slug"). A WATER CREATURE needs considerably more:
            // a lurker is fought from the SHORE, which lies one floor level above the water
            // (per user, 2026-09-16), so the player.s feet are already 16 units up and climb
            // another 16 from there - only a body taller than 32 above the water floor is in
            // the way at all, and a whole tile leaves a margin.
            float lfMinimum = fIsSwimmingCritter(piObjectId)
                ? fGetSwimmerMinimumHeight()
                : fGetCritterMinimumHeight();

            float lfHeight = Mathf.Max(lOCollider.size.y, lfMinimum);

            lOCollider.size = new Vector3(lfSize, lfHeight, lfSize);
            lOCollider.center = new Vector3(0f, lfHeight * 0.5f, 0f);
        }

        /// <summary>Category 3 in the critter table is the swimmer - in uw1 only the lurker.
        /// </summary>
        private bool fIsSwimmingCritter(int piObjectId)
        {
            UWObjectClassProperties.Critter lOCritter;

            return mOData != null && mOData.ObjectClassProperties != null
                && mOData.ObjectClassProperties.TryGetCritter(piObjectId, out lOCritter)
                && lOCritter.Category == SwimmingCritterCategory;
        }

        private const int SwimmingCritterCategory = 3;

        private static float fGetCritterMinimumHeight()
        {
            return UnderworldRevisited.UWSettings.Instance != null
                ? UnderworldRevisited.UWSettings.Instance.CritterMinimumColliderHeight
                : 24f;
        }

        private static float fGetSwimmerMinimumHeight()
        {
            return UnderworldRevisited.UWSettings.Instance != null
                ? UnderworldRevisited.UWSettings.Instance.SwimmerMinimumColliderHeight
                : 64f;
        }

        /// <summary>World units per radius point from COMOBJ.DAT - see
        /// fApplyCritterFootprint. The unit is not in any file, hence adjustable in
        /// UWSettings.</summary>
        private static float fGetCritterRadiusScale()
        {
            return UnderworldRevisited.UWSettings.Instance != null
                ? UnderworldRevisited.UWSettings.Instance.CritterRadiusScale
                : 6f;
        }

        /// <summary>
        /// Without any collision according to COMOBJ.DAT: height 0, or not SOLID (byte 6 bit 0,
        /// which the original's collision asks of the struck object before it stops the mover).
        /// The second applies only to the glowing rock - one walks through it and gathers it
        /// (per user, 2026-09-30; UWGlowingRockRules). If the entry is missing, the previous
        /// behaviour stays - better one object too many in the way than one you walk through
        /// unintentionally.
        /// </summary>
        private bool fHasNoCollision(int piObjectId)
        {
            UWCommonObjectProperties.Entry lOEntry;

            if (mOData == null || mOData.CommonObjectProperties == null
                || !mOData.CommonObjectProperties.TryGet(piObjectId, out lOEntry))
                return false;

            return lOEntry.Height == 0 || !lOEntry.IsSolid;
        }

        /// <summary>
        /// The billboard material with depth bias set - a single copy per source material
        /// (opaque or translucent) shared by all animated objects, so they can still be drawn together.
        /// </summary>
        private Material fGetAnimatedBillboardMaterial(Material pOSource)
        {
            if (pOSource == null)
                return null;

            Material lOVariant;

            if (!mOAnimatedBillboardMaterials.TryGetValue(pOSource, out lOVariant))
            {
                lOVariant = new Material(pOSource);
                lOVariant.name = pOSource.name + " (animated)";
                lOVariant.SetFloat("_DepthBias", AnimatedBillboardDepthBias);

                mOAnimatedBillboardMaterials[pOSource] = lOVariant;
            }

            return lOVariant;
        }

        /// <summary>
        /// The image sequence of an animated object (id 0x01C0 to 0x01CF) from ANIMO.GR,
        /// or null for everything else. Start frame and count are in the animation table
        /// of OBJECTS.DAT - see UWObjectClassProperties.AnimationObject, which also notes
        /// where the field meanings were derived from.
        /// </summary>
        private UWTexture[] fGetAnimationTextures(int piObjectId)
        {
            if (mOData == null || mOData.ObjectClassProperties == null)
                return null;

            UWObjectClassProperties.AnimationObject lOAnimation;

            if (!mOData.ObjectClassProperties.TryGetAnimationObject(piObjectId, out lOAnimation)
                || lOAnimation.FrameCount == 0)
                return null;

            List<UWTexture> lOFrames = new List<UWTexture>(lOAnimation.FrameCount);

            for (int liFrame = 0; liFrame < lOAnimation.FrameCount; liFrame++)
            {
                UWTexture lOTexture = fGetTexture(UWTexture.TextureTypes.ANIMO, lOAnimation.StartFrame + liFrame);

                if (lOTexture != null)
                    lOFrames.Add(lOTexture);
            }

            return lOFrames.Count > 0 ? lOFrames.ToArray() : null;
        }

        /// <summary>
        /// Attaches its image sequence to a billboard. The frames all live in the
        /// same atlas, so the material stays the same over the whole sequence - only
        /// the mesh is swapped, since it carries the atlas UVs.
        /// </summary>
        private void fAttachAnimation(GameObject pOGameObject, MeshFilter pOFilter, UWTexture[] pOTextures)
        {
            List<Mesh> lOFrames = new List<Mesh>(pOTextures.Length);

            for (int liFrame = 0; liFrame < pOTextures.Length; liFrame++)
            {
                UWTextureRef lOResolved;

                if (fResolve(pOTextures[liFrame], true, out lOResolved))
                    lOFrames.Add(fGetQuad(pOTextures[liFrame], lOResolved));
            }

            if (lOFrames.Count < 2)
                return;

            // All fountains of a level run in sync - in the original the
            // animations hang off a shared frame counter, not a separate clock per
            // object.
            UWAnimatedBillboard lOAnimated = pOGameObject.AddComponent<UWAnimatedBillboard>();
            lOAnimated.FramesPerSecond = fGetAnimatedFramesPerSecond();
            lOAnimated.Initialise(pOFilter, lOFrames.ToArray(), 0f);
        }

        /// <summary>
        /// The force field (365) as per UW.EXE.
        ///
        /// IMAGE: model 22 ("tmap type 2"), drawn via RenderingObjects_seg032_2DCA_5,
        /// render type 3 (COMOBJ byte 9 &amp; 3), texture owner &amp; 0x3F from the level's wall list. The
        /// model is a single vertical quad, 1.0 x 1.0 (one tile), from x -0.4375
        /// to 0.5625 and offset forward by 0.0625; it is rotated via the object's heading
        /// in 45-degree steps (seg032_2DCA_597), not towards the viewer. With the usual
        /// sub-position 3 it therefore spans the full tile width on the tile centre -
        /// ASSUMPTION: sub-position + 0.5 as the centre, not verified.
        ///
        /// COLLISION: independently of that, a box (CreateColllisionRecord_seg026_A60): radius
        /// COMOBJ byte 1 &amp; 7 = 3 around the sub-position (so 0 to 6 of 8, at the tile corner),
        /// height from z to z + COMOBJ height 128. Not rotated. It blocks the player and
        /// creatures. Hence you cannot get through in the original, although the panel is thin
        /// (per user, 2026-09-14).
        /// </summary>
        private void fMakeForceField(GameObject pOObject, int piTileX, int piTileZ, UWObject pOData, Mesh pOPanel)
        {
            float lfTile = UWLevelMeshBuilder.TileSpacing;
            float lfFine = lfTile / 8f;
            float lfCornerX = (piTileX * lfTile) - UWLevelMeshBuilder.TileHalfSize;
            float lfCornerZ = (piTileZ * lfTile) - UWLevelMeshBuilder.TileHalfSize;
            float lfBottom = pOData.ZPos * HeightScale;

            // Origin of the model: sub-position plus half a step.
            Vector3 lOOrigin = new Vector3(lfCornerX + ((pOData.XPos + 0.5f) * lfFine), lfBottom,
                lfCornerZ + ((pOData.YPos + 0.5f) * lfFine));

            // The quad from fGetQuad is centred on its pivot; the model spans from
            // -0.4375 to 0.5625, so its centre lies 0.0625 tile to the side, and the face
            // 0.0625 tile forward.
            Quaternion lORotation = Quaternion.Euler(0f, pOData.Heading * 45f, 0f);
            Vector3 lOLocalOffset = new Vector3(ForceFieldModelOffset * lfTile, 0f, ForceFieldModelOffset * lfTile);

            pOObject.transform.SetPositionAndRotation(lOOrigin + (lORotation * lOLocalOffset) + fGetDrawOffset(), lORotation);
            pOObject.GetComponent<MeshFilter>().sharedMesh = pOPanel;

            // The flat body of the panel is dropped - the collision is the box.
            MeshCollider lOOldCollider = pOObject.GetComponent<MeshCollider>();

            if (lOOldCollider != null)
                Object.DestroyImmediate(lOOldCollider);

            int liRadius = ForceFieldFallbackRadius;
            int liHeight = ForceFieldFallbackHeight;

            if (mOData != null && mOData.CommonObjectProperties != null
                && mOData.CommonObjectProperties.TryGet(pOData.ID, out UWCommonObjectProperties.Entry lOCommon))
            {
                liRadius = lOCommon.Radius;
                liHeight = lOCommon.Height;
            }

            // A separate child without rotation, because the box does not follow the heading. Look
            // hits it and finds the force field via GetComponentInParent.
            GameObject lOBox = new GameObject("ForceField-Collision");
            lOBox.layer = pOObject.layer;
            lOBox.transform.SetParent(pOObject.transform, false);

            float lfMinX = lfCornerX + (Mathf.Max(0, pOData.XPos - liRadius) * lfFine);
            float lfMaxX = lfCornerX + ((Mathf.Min(7, pOData.XPos + liRadius) + 1) * lfFine);
            float lfMinZ = lfCornerZ + (Mathf.Max(0, pOData.YPos - liRadius) * lfFine);
            float lfMaxZ = lfCornerZ + ((Mathf.Min(7, pOData.YPos + liRadius) + 1) * lfFine);
            float lfTop = lfBottom + (liHeight * HeightScale);

            lOBox.transform.SetPositionAndRotation(
                new Vector3((lfMinX + lfMaxX) * 0.5f, lfBottom, (lfMinZ + lfMaxZ) * 0.5f), Quaternion.identity);

            BoxCollider lOCollider = lOBox.AddComponent<BoxCollider>();
            lOCollider.center = new Vector3(0f, (lfTop - lfBottom) * 0.5f, 0f);
            lOCollider.size = new Vector3(lfMaxX - lfMinX, lfTop - lfBottom, lfMaxZ - lfMinZ);
        }

        /// <summary>Offset of the centre of model 22 from its origin, sideways and
        /// forward, in tiles (UW.EXE model nodes: x -0.4375 to 0.5625, y 0.0625).</summary>
        private const float ForceFieldModelOffset = 0.0625f;

        /// <summary>COMOBJ values of 365 in case the table is missing: radius 3, height 0x80.</summary>
        private const int ForceFieldFallbackRadius = 3;

        private const int ForceFieldFallbackHeight = 0x80;

        private void fSpawnDecal(int xPos, int zPos, UWObject pOObject, Transform pOParent)
        {
            UWTextureRef lOResolved;

            if (!fResolve(pOObject.Texture, false, out lOResolved))
            {
                UnresolvedCount++;
                return;
            }

            GameObject lOObject = new GameObject("Tmap " + pOObject.ID);
            lOObject.transform.SetParent(pOParent, false);

            Vector3 lOPosition = fGetDecalPosition(xPos, zPos, pOObject);

            // A WALL PANEL (366/367) STANDS WHERE ITS MODEL PUTS IT (per user, 2026-09-30: on level 3,
            // 56/53, looking east, the original leaves a gap on the right of the waterfall to look
            // through). Model 0x16 spans -7/16 to +9/16 of a tile along its X around the object's
            // position, so the panel does not cover its tile exactly unless its position is 4 (or 3,
            // by facing) - at 56/53 (position 5, facing 2) it leaves 8 units open at the south end.
            // It takes the shift of the other wall models below (fIsOffCentreWallModel). For a few
            // hours on 2026-09-30 it was centred in its tile instead, after two panels on level 2,
            // 60/41 and 60/42, overlapped and flickered; the overlap is the original's too - its
            // painter draws one over the other - so ours now staggers them (WallPanelStagger).
            if (pOObject.ID == DecalObjectId || pOObject.ID == DecalObjectId + 1)
            {
                Vector3 lOFront = Quaternion.Euler(0f, pOObject.Heading * 45f, 0f) * Vector3.back;

                // ACROSS ITS WALL TOO THE PANEL STANDS WHERE MODEL 0x16 PUTS IT (per user, 2026-10-03,
                // level 7, looking west from 18/35: one could see through between a panel and the
                // pillar beside it). The model's quad lies 1/16 of a tile BEHIND the object's
                // eighth, towards its back: on 17/34/17/35 one panel (17/34, position 3/7, facing 0)
                // lies on the tile edge and the other (17/35, 3/1, facing 4) 8 units north of it,
                // and the pillar at the east end (17/35, 7/0, a sixteenth either way) fills exactly
                // those 8 units. The decal scale the plaques use (fGetDecalPosition, 9 units a
                // step) left this panel 1.25 units off the pillar's face.
                Vector3 lOStraight = new Vector3(Mathf.Round(lOFront.x), 0f, Mathf.Round(lOFront.z));

                if (Mathf.Abs(lOStraight.x) + Mathf.Abs(lOStraight.z) == 1f)
                {
                    bool lbAlongZ = lOStraight.z != 0f;
                    int liTile = lbAlongZ ? zPos : xPos;
                    int liEighth = lbAlongZ ? pOObject.YPos : pOObject.XPos;
                    float lfBackStep = -(lbAlongZ ? lOStraight.z : lOStraight.x);
                    float lfAcross = UWViewpoint.TileToWorldAxis(liTile) + (liEighth * SubTileScale) + SubTileOffset
                        + (lfBackStep * WallModelBackOffset) + DrawOffset;

                    if (lbAlongZ)
                        lOPosition.z = lfAcross;
                    else
                        lOPosition.x = lfAcross;
                }

                // A SECOND PANEL ON THE SAME WALL OF THE SAME TILE steps further out again (per user,
                // 2026-09-30: the waterfall's two panels on 56/53, at z 88 and 96, flickered where
                // they overlap) - counted per tile and facing.
                long liKey = ((long)xPos << 20) | ((long)zPos << 8) | (long)pOObject.Heading;

                mOPanelsOnWall.TryGetValue(liKey, out int liEarlier);
                mOPanelsOnWall[liKey] = liEarlier + 1;

                // Where the panel lies on its tile's edge it is set the step in front of that edge, so
                // it stays in front of the wall behind; anywhere else the step goes back, so the
                // panel's ends stay inside what closes them (the pillar on 17/35) instead of leaving
                // a slit.
                float lfStagger = (((xPos + zPos) & 1) + 1 + (2 * liEarlier)) * WallPanelStagger;

                if (fGetTileEdgeBehind(lOPosition, xPos, zPos, lOFront, out Vector3 lOEdge))
                    lOPosition = lOEdge + (lOFront * lfStagger);
                else
                    lOPosition -= lOFront * lfStagger;
            }

            if (pOObject.Heading % 2 == 1 && fIsDiagonalTile(xPos, zPos))
            {
                // ON A DIAGONAL WALL the image lies ON the wall. The diagonal runs through the tile
                // centre, but the stored position (on the decal scale) is an eighth grid point off
                // it: the plaque "Entrance to Mines" on level 2, 33/16 (xpos 3, ypos 3, facing 1)
                // floated about seven units in front of the wall (per user, 2026-09-17). It is moved
                // along the image normal onto the plane through the tile centre, and a quarter unit
                // out to the front against z-fighting.
                Vector3 lONormal = Quaternion.Euler(0f, pOObject.Heading * 45f, 0f) * Vector3.back;
                Vector3 lOCentre = new Vector3(xPos * UWLevelMeshBuilder.TileSpacing, lOPosition.y, zPos * UWLevelMeshBuilder.TileSpacing);

                lOPosition -= lONormal * Vector3.Dot(lOPosition - lOCentre, lONormal);
                lOPosition += lONormal * DiagonalDecalLift;
            }

            // THE ORIGINAL DRAWS THESE AS A MODEL whose image is off its origin: lever 0x161,
            // switch 0x162 and writing 0x166 use models 0x10, 0x11 and 0x12 (object-to-model table
            // at DS:0682), all the same quad from -1/16 to +3/16 of a tile along the model's X. So
            // the image's centre lies 4 units (4 of its pixels) along the quad's right vector. Per
            // user, 2026-09-29: the plaques on level 6, 8/51 (facing 2) and 9/60 (facing 4), had
            // to move 4 original pixels, both to the right as one faces them.
            //
            // THE SWITCHES 0x170-0x17F ARE THE SAME QUAD: seg032_2DCA_3DA draws minor class 3 of
            // render type 3 through the model renderer with model 0x14 (read 2026-09-30), and
            // 0x14 holds exactly this quad, with a TMFLAT image (16 by 16). Until then a switch
            // was shifted by half its width, 8, which put the plate 4 units too far to the right:
            // on level 2, 12/29 (0x175, facing 0, position 6/7) it stuck out past the east end of
            // the wall (per user); the plate in front of Drog, moved "4 to 5 units" by the user on
            // 2026-09-17, sits where the model puts it. Diagonal facings get the shift as well.
            if (fIsOffCentreWallModel(pOObject.ID)
                || pOObject.GetCategory() == UWObject.ObjectCategoryEnum.Switches)
                lOPosition += (Quaternion.Euler(0f, pOObject.Heading * 45f, 0f) * Vector3.right) * WallModelImageShift;

            lOObject.transform.position = lOPosition;
            lOObject.transform.Rotate(Vector3.up, pOObject.Heading * 45f);

            Mesh lOMesh = fGetQuad(pOObject.Texture, lOResolved);

            lOObject.AddComponent<MeshFilter>().sharedMesh = lOMesh;
            lOObject.AddComponent<MeshRenderer>().sharedMaterial =
                pOObject.ID == DecalObjectId || pOObject.ID == DecalObjectId + 1
                    ? fGetSingleSided(lOResolved.Material)
                    : lOResolved.Material;

            // A PANEL OF HEIGHT 0 DOES NOT BLOCK (per user, 2026-09-30: the waterfall on level 3,
            // 56/53 - two wall panels 0x16E with a water texture, standing in the open passage to
            // the cave on 57/53 - has to be swum through, and ours stopped the player). COMOBJ.DAT
            // gives 0x16E, the writings and the switches height 0, and the original's motion
            // collides with nothing of height 0 (see fHasNoCollision); 0x16F (height 32) and the
            // force field keep a solid body. The trigger box still answers looking and using.
            if (fHasNoCollision(pOObject.ID))
            {
                BoxCollider lOBox = lOObject.AddComponent<BoxCollider>();

                lOBox.center = new Vector3(0f, lOResolved.Size.y * 0.5f, 0f);
                lOBox.size = new Vector3(lOResolved.Size.x, lOResolved.Size.y, DecalPickDepth);
                lOBox.isTrigger = true;
            }
            else
                lOObject.AddComponent<MeshCollider>().sharedMesh = lOMesh;

            // The force field (365): panel and collision box as per UW.EXE, see fMakeForceField.
            if (pOObject.ID == ForceFieldObjectId)
                fMakeForceField(lOObject, xPos, zPos, pOObject, lOMesh);

            UWEntityInfo lOInfo = lOObject.AddComponent<UWEntityInfo>();
            lOInfo.ObjectData = pOObject;

            // Wall decoration 366/367 describes itself via its WALL TEXTURE (string block 10),
            // not via the object name - just like the door frame in fSpawnFrame. For the
            // double gate on level 1 (tiles 31/1 and 32/1) that gives "the locked doors of
            // the Abyss"; the object name would be "force field" instead (reported as
            // wrong by user, 2026-08-28).
            //
            // All other decorations take the object name - with the project's usual +1,
            // which was MISSING here until now: that shifted every description by one (the
            // pillar was called "a lotus turbo esprit", the lever "a_pillar", the bridge
            // nothing at all). See fSpawnBillboard, which does the +1 right from the start.
            if (pOObject.ID == UWObjectMechanics.WritingObjectId)
            {
                // Wall inscription: in the original it shows its wording directly, with a matching
                // lead-in before it - no "You see some writing." (CONFIRMED by user,
                // 2026-08-28). That is why the finished display text is built here already.
                lOInfo.Description = fBuildWritingText(pOObject);
                lOInfo.ShowDescriptionVerbatim = true;
            }
            else if ((pOObject.ID == ForceFieldObjectId || pOObject.ID == 366 || pOObject.ID == 367) && pOObject.Texture != null)
            {
                lOInfo.Description = fGetTextureDescription(pOObject.Texture.Index + 1);

                // When looked at, a window additionally shows an image in the view window. The
                // distinction goes via the texture, not the object id: 366 is
                // the same id as drain, stairs, banner or the Abyss double gate.
                //
                // The source is TERRAIN.DAT, which gives every wall texture a terrain type - there
                // exactly 127 and 142 are listed as Window, i.e. the same two we had
                // found earlier via the texture names. The constant list in
                // UWObjectMechanics stays as a fallback in case the file is missing.
                lOInfo.IsWindow = mOData != null && mOData.Terrain != null && mOData.Terrain.IsLoaded
                    ? mOData.Terrain.IsWindowWall(pOObject.Texture.Index)
                    : UWObjectMechanics.IsWindowTexture(pOObject.Texture.Index);

                lOInfo.IsChainedPrincess = mOData != null && mOData.Terrain != null && mOData.Terrain.IsLoaded
                    && mOData.Terrain.GetWallTerrain(pOObject.Texture.Index) == UWTerrain.TerrainType.ChainedPrincess;
            }
            else
                lOInfo.Description = fGetDescription(pOObject.ID + 1);

            // Switches (368-383): the second image of the same switch sits in TMFLAT.GR offset
            // by 8, so the partner is index XOR 8. XOR instead of "+ 8", because both halves occur as
            // the starting image (ID 375 -> 7, ID 377 -> 9) and "+ 8" would run out of the valid
            // range for the upper half.
            //
            // Exactly this overflow had earlier led to the wrong assumption that switches
            // with index 8-15 had no second image at all and were invisible
            // script anchor points - they were skipped entirely. Disproved by user
            // (2026-08-28): the button on tile 16/35 (ID 377) is visible, clickable and
            // shows both textures correctly.
            //
            // Lower half (0-7) = unpressed, upper (8-15) = pressed. Both images
            // are passed sorted accordingly, and the switch starts in the state its own image
            // shows; every use flips it (see UWSwitchVisual).
            if (pOObject.GetCategory() == UWObject.ObjectCategoryEnum.Switches)
            {
                int liIndex = pOObject.ID & 0xF;
                UWTexture lOPartnerTexture = fGetTexture(UWTexture.TextureTypes.TMFLAT, liIndex ^ 8);

                // BOTH IMAGES FROM THE ID, not the own one from pOObject.Texture: a used switch
                // gets a new ID when the level is left (UWWorldCapture.CaptureSwitch), but its
                // Texture stays the one it was loaded with. On the way back both quads were then
                // the same image, and the lever on level 1, 9/36 no longer changed its image
                // (per user, 2026-09-18).
                UWTexture lOOwnTexture = fGetTexture(UWTexture.TextureTypes.TMFLAT, liIndex);

                if (lOOwnTexture != null && fResolve(lOOwnTexture, false, out UWTextureRef lOOwnResolved))
                {
                    lOMesh = fGetQuad(lOOwnTexture, lOOwnResolved);
                    pOObject.Texture = lOOwnTexture;
                    lOObject.GetComponent<MeshFilter>().sharedMesh = lOMesh;
                }

                if (lOPartnerTexture != null && fResolve(lOPartnerTexture, false, out UWTextureRef lOPartnerResolved))
                {
                    Mesh lOPartnerMesh = fGetQuad(lOPartnerTexture, lOPartnerResolved);
                    bool lbOwnImageIsPressed = liIndex >= 8;

                    lOObject.AddComponent<UWSwitchVisual>().Initialise(
                        lOObject.GetComponent<MeshFilter>(),
                        lbOwnImageIsPressed ? lOPartnerMesh : lOMesh,
                        lbOwnImageIsPressed ? lOMesh : lOPartnerMesh,
                        lbOwnImageIsPressed);
                }
            }

            // Height lever (a_lever, ID 353): 8 positions (TMOBJ_4..TMOBJ_11, see
            // UWLevel.load_object_list) - prepare all eight quads so that UWHeightLever
            // only has to swap the mesh when used (see UWSwitchVisual pattern).
            if (pOObject.ID == 353)
            {
                Mesh[] lyPositionMeshes = new Mesh[8];

                for (int i = 0; i < lyPositionMeshes.Length; i++)
                {
                    UWTexture lOPositionTexture = fGetTexture(UWTexture.TextureTypes.TMOBJ, i + 4);

                    if (lOPositionTexture != null && fResolve(lOPositionTexture, false, out UWTextureRef lOPositionResolved))
                        lyPositionMeshes[i] = fGetQuad(lOPositionTexture, lOPositionResolved);
                }

                // The lever keeps its position in the object's flags field, so it is
                // handed the object itself - see UWHeightLever.
                lOObject.AddComponent<UWHeightLever>().Initialise(lOObject.GetComponent<MeshFilter>(), lyPositionMeshes, pOObject);
            }

            SpawnedCount++;
        }

        /// <summary>
        /// The colour of a solid-colour model face. For the MOONGATE it is not in the
        /// model table but in the object's link field: link minus 512 is the
        /// palette index (reference moongate.ModelColour). On level 9 that gives 180 red, 196
        /// blue and 208 green, the gate in the volcano (link 0x2C0) 192 light blue. With the fixed
        /// table {0, 2, 4} every gate was pink (per user, 2026-09-14).
        /// </summary>
        private static int fGetModelFaceColor(int piModelIndex, UWObject pOObject, int piTableColor)
        {
            if (piModelIndex == UW3DModelImport.ModelIndex.Moongate && pOObject != null
                && pOObject.HasQuantity && pOObject.Quantity >= MoongateColorLinkBase)
                return (pOObject.Quantity - MoongateColorLinkBase) & 0xFF;

            return piTableColor;
        }

        /// <summary>From this value on, a moongate's link field is a colour (0x200).</summary>
        private const int MoongateColorLinkBase = 0x200;

        /// <summary>
        /// Maps an object id to the matching original 3D model index, or -1 if
        /// the object has no such model.
        /// </summary>
        private static int fGet3DModelIndex(int piObjectId)
        {
            switch (piObjectId)
            {
                case ShrineObjectId: return UW3DModelImport.ModelIndex.Shrine;
                case SmallBoulderObjectId: return UW3DModelImport.ModelIndex.SmallBoulder;
                case MediumBoulderObjectId: return UW3DModelImport.ModelIndex.MediumBoulder;
                case LargeBoulderObjectIdA:
                case LargeBoulderObjectIdB: return UW3DModelImport.ModelIndex.LargeBoulder;
                case BridgeObjectId: return UW3DModelImport.ModelIndex.Bridge;
                case BenchObjectId: return UW3DModelImport.ModelIndex.Bench;
                case PillarObjectId: return UW3DModelImport.ModelIndex.Pillar;
                case GravestoneObjectId: return UW3DModelImport.ModelIndex.Gravestone;
                case MoongateObjectId: return UW3DModelImport.ModelIndex.Moongate;
                case TableObjectId: return UW3DModelImport.ModelIndex.Table;
                case ChestObjectId: return UW3DModelImport.ModelIndex.Chest;
                case NightstandObjectId: return UW3DModelImport.ModelIndex.Nightstand;
                case BarrelObjectId: return UW3DModelImport.ModelIndex.Barrel;
                case ChairObjectId: return UW3DModelImport.ModelIndex.Chair;
                case CrossbowBoltObjectId:
                case ArrowObjectId: return UW3DModelImport.ModelIndex.Arrow;
                default: return -1;
            }
        }

        /// <summary>
        /// Places a model at a free world position instead of in a tile - for
        /// flying projectiles (see UWLevelLoader.SpawnFlyingModel).
        ///
        /// Goes through the same build as a model in a tile; only position and rotation
        /// come from outside, and alignment to the tile centre is skipped.
        /// </summary>
        public GameObject SpawnModelAt(UWObject pOObject, int piModelIndex, Vector3 pOPosition,
            Quaternion pORotation, Transform pOParent)
        {
            return fSpawn3DModel(0, 0, pOObject, pOParent, piModelIndex, pOPosition, pORotation);
        }

        /// <summary>
        /// Shrine/ankh and boulders are some of the few objects that exist in the original as a
        /// real 3D model (baked into uw.exe, see
        /// UW3DModelImport) instead of a flat sprite. As with door frame and bridge,
        /// only the faces textured via original UVs carry the resolved
        /// 2D object graphic; faces without UV (node 007E) are solid-coloured in the original -
        /// here they point to the neutral white atlas tile, carry their palette colour as
        /// vertex colour and go into a separate submesh with mOModelSolidMaterial.
        /// Falls back to the sprite (and returns null) if the model or the object's
        /// texture cannot be loaded.
        /// </summary>
        private GameObject fSpawn3DModel(int xPos, int zPos, UWObject pOObject, Transform pOParent, int piModelIndex,
            Vector3? pOPositionOverride = null, Quaternion? pORotationOverride = null)
        {
            UW3DModel lOModel = mO3DModels.GetModel(piModelIndex);

            if (lOModel == null || lOModel.Vertices.Count == 0 || lOModel.Faces.Count == 0)
            {
                fSpawnBillboard(xPos, zPos, pOObject, pOParent);
                return null;
            }

            UWTextureRef lOResolved;

            if (!fResolve(pOObject.Texture, false, out lOResolved))
            {
                fSpawnBillboard(xPos, zPos, pOObject, pOParent);
                return null;
            }

            // Some objects (e.g. the pillar) show a real wall texture in the original instead
            // of an object icon, and it can even differ from instance to instance
            // (see UWLevel.load_object_list, object id 352: the texture there comes via the
            // owner field from the wall texture list, not from the model itself). lOResolved.
            // IsArray indicates that - textured faces (00A8) then use the raw
            // model UV directly with the wall texture slice, no atlas rect.
            bool lbObjectIsArrayTexture = lOResolved.IsArray && mODungeonMaterial != null;

            // Fallback UV in case the palette tiles are missing from the atlas (e.g. an old
            // atlas built before this fix) - old guess "most frequent pixel of the
            // sprite graphic". The actual colour value now comes from the real
            // palette though (see below, mOAtlas.TryGetPaletteUv), no longer from this guess:
            // for solid-colour faces the original engine reads a fixed palette colour from
            // the model bytecode (nodes 0014/00BC/00BE/00D4/0016), not some pixel
            // of the object's 2D sprite graphic - the two have nothing to do with each other
            // content-wise, the old trick was just the only available approximation at the time.
            Vector2 lOFallbackSolid = new Vector2(0.5f, 0.5f);
            UWObjectAtlas.Entry lOEntry;

            if (!lbObjectIsArrayTexture && mOAtlas != null && mOAtlas.TryGetEntry(pOObject.Texture, out lOEntry) && lOEntry.Size.y > 0)
            {
                lOFallbackSolid = new Vector2(
                    (lOEntry.SolidPixel.x + 0.5f) / lOEntry.Size.x,
                    (lOEntry.SolidPixel.y + 0.5f) / lOEntry.Size.y);
            }

            Vector2 lOUvOrigin = lOResolved.UvRect.position;
            Vector2 lOUvSize = lOResolved.UvRect.size;

            // Model space is normalised to 1.0 = one tile width (CONFIRMED on the
            // bridge, which measures exactly 1x1). Y/Z swapped: the model bytecode stores X, Y, Z with Z
            // as the height (UW.EXE model interpreter), Unity is Y-up. An intermediate attempt
            // without the swap left the object lying flat on the floor instead of upright.
            const float lfModelScale = TileSpacing;

            // Computed up front, because node 008C ("reach up to the ceiling", see below) needs
            // to know the object's Y position to derive the local target height from it.
            Vector3 lOPosition = fGetObjectPosition(xPos, zPos, pOObject);

            float lfLocalCeiling = UWLevelMeshBuilder.CeilingHeight - lOPosition.y;

            // Each face gets its own vertex copies instead of shared indices, since UVs
            // differ per face (textured vs. solid-coloured) - Unity only knows
            // one UV per vertex index, not per face corner. One shared vertex pool
            // for two submeshes: some models (e.g. the gravestone) mix wall-texture
            // faces with solid-coloured faces that run on different materials/shaders
            // (see TextureNumber resolution below) - a triangle only references
            // vertices, no reason to split the vertex pool.
            List<Vector3> lOVerts = new List<Vector3>();
            List<Vector3> lOUvs = new List<Vector3>(); // xy = UV, z = array slice (only relevant for submesh 0)
            // Vertex colour: always white for textured/array faces (no effect). For
            // solid-colour faces (see below) it carries the actual palette colour -
            // the UV there ALWAYS points to the same neutral white atlas tile, never directly to a
            // palette tile. Reason: two corners of a triangle with different
            // Gouraud brightness would otherwise have two different palette tiles as UV - the
            // GPU interpolates UVs linearly, and the path between two arbitrary atlas cells
            // runs right through completely unrelated neighbouring graphics (visible
            // garbage). Colour values, on the other hand, can be interpolated safely.
            List<Color32> lOColors = new List<Color32>();

            // Second UV set for the palette renderer: x the palette index, y the
            // additional darkening of the vertex PLUS ONE. The one is also the
            // marker: zero means "not a solid-colour face", there the shader takes the
            // index from the texture as usual.
            //
            // Two numbers instead of three so the channel stays narrow - it has to be present on
            // EVERY mesh the palette shader draws, including the
            // wall geometry (see UWChunkGeometry).
            //
            // A separate channel, because the colour path already uses UV set 0 and the vertex colour
            // differently: there the UV points to a white tile and the vertex colour
            // carries the final colour. Both paths have to share the same mesh.
            List<Vector2> lOPaletteData = new List<Vector2>();
            List<int> lOArrayTriangles = new List<int>();
            List<int> lOAtlasTriangles = new List<int>();

            // Separate submesh for the solid-colour faces, see mOModelSolidMaterial.
            List<int> lOSolidTriangles = new List<int>();

            foreach (UW3DModelFace lOFace in lOModel.Faces)
            {
                int liBase = lOVerts.Count;

                // Textured faces (00A8/00B4/00CE/00A0) always show the graphic of the
                // object itself (pOObject.Texture) - for most models an atlas icon,
                // for some (pillar, see UWLevel.load_object_list ID 352) a real
                // wall texture that differs per instance (lOResolved.IsArray). The
                // model UV (Face.Uvs) only defines HOW the texture is laid over the geometry,
                // not WHICH one - that was the mistake in the previous attempt using the
                // model's internal "TextureNumber".
                bool lbUseArraySlice = lOFace.Uvs != null && lbObjectIsArrayTexture;

                // A TEXTURED SIDE UP TO THE CEILING (the pillar) REPEATS ITS PICTURE instead of
                // stretching it over the whole height (per user, 2026-10-07, screenshots of the
                // original beside ours from the same spot: our texels many times too tall). The
                // pillar's picture is TMOBJ.GR 0-3 (UWLevel, object 352), 8 by 32 texels; the
                // model lays its width across a side (U 0..1 over an eighth of a tile, one texel a
                // unit), and in the original the texels are square - so the picture repeats every
                // 32 units. In the atlas a texture cannot wrap, so the side is cut into pieces of
                // one repeat each, anchored at the world height as the walls are. A DELIBERATE
                // DEVIATION in the way (Todo.md section 8): the original draws one quad whose
                // texture coordinates run in texel units over the whole height and wrap in its
                // rasterizer; the picture is the same.
                if (lOFace.Uvs != null && !lbUseArraySlice && lOFace.VertexIndices.Count == 4
                    && lOFace.VertexIndices.Exists(piAt => lOModel.CeilingVertexIndices.Contains(piAt))
                    && fAddRepeatedSide(lOModel, lOFace, lfModelScale, lfLocalCeiling, lOPosition.y, lOResolved,
                        lOVerts, lOUvs, lOColors, lOPaletteData, lOAtlasTriangles))
                    continue;

                for (int i = 0; i < lOFace.VertexIndices.Count; i++)
                {
                    int liVertexIndex = lOFace.VertexIndices[i];
                    UWVector3 lV = lOModel.Vertices[liVertexIndex];

                    // Pillar/door-frame-like models mark individual vertices as "stretch up
                    // to the ceiling" (node 008C) - the actual ceiling height is only known
                    // here at runtime (current tile), not by the engine-independent
                    // parser. Without this such vertices would stay at their base height, the model
                    // would be degenerate/invisible (X/Z extent 0).
                    float lfY = lOModel.CeilingVertexIndices.Contains(liVertexIndex)
                        ? lfLocalCeiling
                        : lV.Z * lfModelScale;

                    lOVerts.Add(new Vector3(lV.X * lfModelScale, lfY, lV.Y * lfModelScale));

                    if (lbUseArraySlice)
                    {
                        lOUvs.Add(new Vector3(lOFace.Uvs[i].X, lOFace.Uvs[i].Y, lOResolved.Slice));
                        lOColors.Add(new Color32(255, 255, 255, 255));
                        lOPaletteData.Add(Vector2.zero);
                    }
                    else if (lOFace.Uvs != null)
                    {
                        // V mirrored (1-y): the model UV counts V from top to bottom as in the
                        // original, but the baked atlas tile (see UWObjectAtlasBuilder)
                        // follows Unity texture convention (V from bottom to top) - without
                        // the flip the gravestone graphic was upside down. UW3DModelImport.
                        // fPeekTexCo returns the raw 16-bit value/65535, so it is already
                        // guaranteed to be clamped to 0..1 - no additional wrapping needed
                        // (a previous attempt with modulo folding BEFORE the flip produced, at
                        // UV=1.0, a jump back to 1 instead of 0 and thus visible
                        // image noise on bridge/gravestone - a simple plain flip is continuous).
                        lOUvs.Add(lOUvOrigin + Vector2.Scale(new Vector2(lOFace.Uvs[i].X, 1f - lOFace.Uvs[i].Y), lOUvSize));
                        lOColors.Add(new Color32(255, 255, 255, 255));
                        lOPaletteData.Add(Vector2.zero);
                    }
                    else
                    {
                        // Solid-colour faces (no UV in the original) carry a real palette colour
                        // in the original (see UW3DModelImport colour records/ColorIndex).
                        // GOURAUD FACES (D6) shade their BASE colour: in UW.EXE the Gouraud base
                        // comes from the D4 register and the rasterizer dithers that colour between
                        // adjacent shade levels per vertex. Until 2026-09-15 they showed the
                        // AltColorIndex instead, which only looked right on the chest because the
                        // old parser had its registers swapped; the chair then got a grey seat and
                        // back instead of brown (per user, screenshot of the original, 2026-09-15).
                        // The small per-vertex raw value from D4 (0-5 observed) is taken with a
                        // factor of 1 as a LIGHT.DAT level (ModelShadeToLightLevel; it was an
                        // estimated 2 until 2026-09-23). For solid-colour faces the
                        // UV always points to the neutral white tile (WhitePaletteIndex, see
                        // below) - the actual colour is carried by the vertex colour, so that two
                        // corners of different brightness do not need a UV interpolated across
                        // the atlas. The barrel used to be excluded from the Gouraud darkening (it
                        // looked better plain while Gouraud faces showed the wrong colour); removed
                        // per user, 2026-09-15.
                        int liFaceColor = fGetModelFaceColor(piModelIndex, pOObject, lOFace.ColorIndex);
                        int liPaletteIndex = liFaceColor;

                        // Kept separate for the palette renderer: it does not need the
                        // already darkened colour but the RAW index and the
                        // darkening as a separate number - darkening happens only in the shader,
                        // together with the distance-based darkening.
                        int liRawIndex = liFaceColor;
                        int liExtraDarkness = 0;

                        if (lOFace.IsGouraud
                            && mOData != null && mOData.LightLevels != null
                            && lOModel.VertexDarkValues.TryGetValue(liVertexIndex, out byte lyDark))
                        {
                            int liLevel = Mathf.Clamp(lyDark * ModelShadeToLightLevel, 0,
                                Mathf.Min(GouraudMaxLevel, UWLightLevels.LevelCount - 1));
                            liPaletteIndex = mOData.LightLevels.Remap(liLevel, liFaceColor);
                            liExtraDarkness = liLevel;
                        }
                        else if (!lOFace.IsGouraud && lOFace.ShadeLevel > 0
                            && mOData != null && mOData.LightLevels != null)
                        {
                            // FLAT FACES are darkened by their shade level too: UW.EXE draws the
                            // BC colour through the same shade table (per user, 2026-09-15; e.g.
                            // the chest outline at level 5, the inner sides of the chair legs at 2).
                            int liLevel = Mathf.Clamp(lOFace.ShadeLevel * ModelShadeToLightLevel, 0, UWLightLevels.LevelCount - 1);
                            liPaletteIndex = mOData.LightLevels.Remap(liLevel, liFaceColor);
                            liExtraDarkness = liLevel;
                        }

                        lOPaletteData.Add(new Vector2(liRawIndex, liExtraDarkness + 1));

                        Vector2 lOWhiteUv;

                        if (mOAtlas != null && mOAtlas.TryGetPaletteUv(UWObjectAtlas.WhitePaletteIndex, out lOWhiteUv))
                            lOUvs.Add(lOWhiteUv);
                        else
                            lOUvs.Add(lOUvOrigin + Vector2.Scale(lOFallbackSolid, lOUvSize));

                        UWColor32 lOResolvedColor = mOData != null
                            ? mOData.Palettes.GetPalette(0).GetUWColor(liPaletteIndex)
                            : new UWColor32(255, 255, 255, 255);

                        lOColors.Add(new Color32(lOResolvedColor.R, lOResolvedColor.G, lOResolvedColor.B, 255));
                    }
                }

                // Three targets: wall texture, atlas graphic, solid-colour face. The third
                // is recognised by the face having no UV at all in the model.
                List<int> lOTargetTriangles = lbUseArraySlice
                    ? lOArrayTriangles
                    : (lOFace.Uvs != null ? lOAtlasTriangles : lOSolidTriangles);
                int liCount = lOFace.VertexIndices.Count;

                if (liCount <= 4)
                {
                    // For triangles/quads the simple fan is enough (0,1,2 then 0,2,3),
                    // as usual. An earlier version swapped
                    // i/i+1 here because the object initially seemed invisible - but the actual
                    // reason was a parser bug (missing faces due to the sort node
                    // bug), not the winding. The object material (UW/Decal) renders double-sided
                    // anyway (Cull Off), so the swap only turned the normal computed from the same
                    // winding inwards - light practically never arrived
                    // (dot(normalWS, lightDir) near 0). Reverted to the standard order.
                    for (int i = 1; i + 1 < liCount; i++)
                    {
                        lOTargetTriangles.Add(liBase);
                        lOTargetTriangles.Add(liBase + i);
                        lOTargetTriangles.Add(liBase + i + 1);
                    }
                }
                else
                {
                    // From five points on a fan is no longer enough: the ankh outline for
                    // example is re-entrant at the crossbar/stem transition (not convex),
                    // a fan from point 0 wrongly fills in such notches, so a
                    // polygon tessellator is used for these. fTriangulatePolygon() returns the same
                    // winding order as the fan above.
                    List<int> lOEarTriangles = fTriangulatePolygon(lOVerts, liBase, liCount);

                    for (int i = 0; i + 2 < lOEarTriangles.Count; i += 3)
                    {
                        lOTargetTriangles.Add(lOEarTriangles[i]);
                        lOTargetTriangles.Add(lOEarTriangles[i + 1]);
                        lOTargetTriangles.Add(lOEarTriangles[i + 2]);
                    }
                }
            }

            Mesh lOMesh = new Mesh();
            lOMesh.vertices = lOVerts.ToArray();
            // No fApplyUVs here: the UVs above are already final atlas/array coordinates
            // (mixed from textured and palette faces), not a single
            // normalised set that would still have to be scaled by a single UvRect.
            // A single Vector3 channel for both submeshes: the decal shader
            // expects float2 and simply reads x/y only, the unused z (array slice) does
            // no harm.
            lOMesh.SetUVs(0, lOUvs);
            lOMesh.SetUVs(1, lOPaletteData);
            lOMesh.SetColors(lOColors);
            lOMesh.subMeshCount = 3;
            lOMesh.SetTriangles(lOArrayTriangles, 0);
            lOMesh.SetTriangles(lOAtlasTriangles, 1);
            lOMesh.SetTriangles(lOSolidTriangles, 2);
            lOMesh.RecalculateNormals();
            lOMesh.RecalculateBounds();

            // Models that fill a whole tile are structures and belong on the
            // tile grid, not on the object's sub-tile position.
            //
            // Confirmed on the bridge on level 1: its model spans -0.4 to +0.6 tiles in both
            // horizontal axes, so it is exactly one tile wide, but built offset by
            // a tenth of a tile. Together with the sub-tile position
            // (XY 3/3) the deck ended up one and a half units beside the tile - visible
            // as a gap at the transition from the rock ledge, and the creatures' ground ray
            // fell through exactly there, which is why the goblin could not get onto the bridge (reported
            // by user, 2026-08-30). Furniture such as chair or barrel stays untouched,
            // those rightly stand anywhere in the tile.
            // A freely placed model (projectile) brings its own position and is
            // not moved to the tile centre.
            if (!pOPositionOverride.HasValue)
                lOPosition = fAlignFullTileModel(lOPosition, xPos, zPos, lOMesh, pOObject.Heading * 45f);

            GameObject lOObj = new GameObject("Model " + pOObject.ID);
            lOObj.transform.SetParent(pOParent, false);
            lOObj.transform.position = pOPositionOverride ?? lOPosition;

            if (pORotationOverride.HasValue)
                lOObj.transform.rotation = pORotationOverride.Value;
            else
                lOObj.transform.Rotate(Vector3.up, pOObject.Heading * 45f);

            lOObj.AddComponent<MeshFilter>().sharedMesh = lOMesh;
            // Submesh 0 (dungeon material) stays empty (0 triangles) if no wall/
            // floor-referencing face occurs - that is the vast majority of models
            // (shrine, boulder, bench, ...), but it costs nothing extra.
            lOObj.AddComponent<MeshRenderer>().sharedMaterials = new Material[]
            {
                mODungeonMaterial != null ? mODungeonMaterial : lOResolved.Material,
                lOResolved.Material,
                mOModelSolidMaterial != null ? mOModelSolidMaterial : lOResolved.Material
            };

            // ITS OWN TILE DOES NOT HIDE IT - floor, ceiling, walls; see UWOwnTile.
            // The model stays at its true height (the shrine on level 3, 28/44 at zpos 102, two
            // units in its floor and a little into the ceiling, as the original has it); the
            // shader only lets it win against those two planes. A projectile brings its own
            // position and is left alone.
            if (!pOPositionOverride.HasValue && mOLevelLoader != null)
                fSetModelDepthRange(lOObj.GetComponent<MeshRenderer>(), mOLevelLoader.GetFloorHeightAt(lOPosition));

            // BoxCollider instead of MeshCollider: a concave MeshCollider (ankh outline with
            // notches) easily makes the CharacterController snag or slip through thin spots.
            // A simple box around the extent is sufficient and robust for collision with the
            // player.
            BoxCollider lOCollider = lOObj.AddComponent<BoxCollider>();
            lOCollider.center = lOMesh.bounds.center;
            lOCollider.size = lOMesh.bounds.size;

            // AT LEAST AS TALL AS COMOBJ.DAT SAYS: the original collides with an object's
            // height from that table, not with its picture; the boulder's model is lower than
            // its height of 15. With the table height one gets onto a boulder on flat ground
            // from standing and from a run, as in the original (per user, 2026-09-24).
            if (!fHasNoCollision(pOObject.ID) && mOData != null && mOData.CommonObjectProperties != null
                && mOData.CommonObjectProperties.TryGet(pOObject.ID, out UWCommonObjectProperties.Entry lOHeightEntry))
            {
                float lfTableHeight = lOHeightEntry.Height * HeightScale;

                // AND AT LEAST AS WIDE AS ITS RADIUS: the original's object is a circle of that
                // many eighths. A boulder (3, 48 units across) closes a corridor of 64 - in the
                // original one cannot pass it without jumping on, in ours one walked by beside
                // the model (per user, 2026-09-24).
                float lfTableWidth = lOHeightEntry.Radius * 2f * SubTileScale;

                if (lfTableWidth > lOCollider.size.x || lfTableWidth > lOCollider.size.z)
                    lOCollider.size = new Vector3(Mathf.Max(lOCollider.size.x, lfTableWidth), lOCollider.size.y,
                        Mathf.Max(lOCollider.size.z, lfTableWidth));

                if (lfTableHeight > lOCollider.size.y)
                {
                    float lfBottom = lOMesh.bounds.min.y;

                    lOCollider.size = new Vector3(lOCollider.size.x, lfTableHeight, lOCollider.size.z);
                    lOCollider.center = new Vector3(lOCollider.center.x, lfBottom + (lfTableHeight * 0.5f), lOCollider.center.z);
                }
            }

            // A MODEL YOU WALK OVER behaves physically like a sprite.
            // COMOBJ.DAT height 0 means "no collision" (see fHasNoCollision), and among
            // the model objects that applies to ammunition: an arrow lies on the floor, you
            // walk over it, and it must block neither the player nor a creature's
            // line of sight - hence trigger and sprite layer, just like the sprite.
            //
            // AND IT NEEDS A LARGER GRAB AREA. The arrow model measures only 25 by 6
            // by 6 world units; lying flat on the floor, seen from the eye forty
            // units above, that is a very narrow target - the user could barely pick up the arrows on
            // tile 2/9/44 (2026-09-07). As a sprite the same object had
            // an area the size of its sprite graphic. That is exactly what it gets back,
            // and since the body here only exists for clicking, it costs nothing.
            if (fHasNoCollision(pOObject.ID))
            {
                lOCollider.isTrigger = true;
                lOObj.layer = SpriteLayer;

                Vector3 lOPickSize = Vector3.Max(lOCollider.size,
                    new Vector3(lOResolved.Size.x, lOResolved.Size.y, lOResolved.Size.x));

                lOCollider.center = new Vector3(lOCollider.center.x, lOPickSize.y * 0.5f, lOCollider.center.z);
                lOCollider.size = lOPickSize;
            }

            // What lies under a bridge is covered by its deck - see UWOwnTile.RegisterBridge.
            if (pOObject.ID == BridgeObjectId && !pOPositionOverride.HasValue)
            {
                UWOwnTile.RegisterBridge(xPos, zPos, lOPosition.y);
                lOObj.AddComponent<UWBridgePlane>().Set(xPos, zPos);
            }
            else if (!pOPositionOverride.HasValue)
                UWOwnTile.RegisterModelTile(xPos, zPos, pOObject.XPos, pOObject.YPos, fGetBigRadius(pOObject.ID),
                    miSpawnChainIndex >= 0 ? miSpawnChainIndex : UWOwnTile.MaxChainIndex);

            UWEntityInfo lOInfo = lOObj.AddComponent<UWEntityInfo>();
            lOInfo.ObjectData = pOObject;
            lOInfo.IsModel = true;

            // A BRIDGE WITH A FLOOR TEXTURE IS NAMED AFTER THAT FLOOR (per user on the original,
            // 2026-09-29: the plate over the Wine of Compassion is "a marble floor"; ours said "a
            // bridge"; the reference's bridge.cs names it by its texture too). The floor names sit in
            // block 10 counted down from 511, as for the level's own floors (see Interaction).
            if (pOObject.ID == BridgeObjectId && pOObject.Flags > 1 && pOObject.Texture != null
                && pOObject.Texture.TextureType == UWTexture.TextureTypes.FLOOR)
                lOInfo.Description = fGetTextureDescription(511 - pOObject.Texture.Index);
            else
                lOInfo.Description = fGetDescription(pOObject.ID + 1);

            // PICKABLE, BUT ONLY WHAT LIES ON THE FLOOR. The model path never set this
            // flag, and that was right as long as there was only scenery here - nobody takes
            // shrine, bridge, pillar, barrel or table along. Since ammunition gets a model,
            // that no longer holds: the user could look at the arrows on tile 2/9/44
            // but not drag them (2026-09-07).
            //
            // The same gate as for the grab area above: only objects without collision
            // (COMOBJ.DAT height 0). Among the model objects those are exactly bolt and
            // arrow - scenery always has a height and therefore stays put.
            // Within this gate the rule of the sprite path then applies.
            lOInfo.CanBePickedUp = fHasNoCollision(pOObject.ID)
                && pOObject.GetCategory() != UWObject.ObjectCategoryEnum.Monsters
                && !UWFixedScenery.IsFixed(pOObject.ID);

            // A BARREL OR CHEST CAN BE SMASHED IN, like a door: hit points from its quality,
            // and when it breaks its contents end up on the tile (see UWContainerDamage). Until
            // now they had no UWDamageable at all, so a strike went through without a hit, a
            // sound or the flash (per user with a screenshot of the original, 2026-09-16).
            if (pOObject.ID == BarrelObjectId || pOObject.ID == ChestObjectId || pOObject.ID == NightstandObjectId)
            {
                if (fAttachHealth(lOObj, pOObject))
                    lOObj.AddComponent<UWContainerDamage>();
            }

            SpawnedCount++;

            return lOObj;
        }

        /// <summary>
        /// Ear-clipping triangulation for a possibly non-convex polygon in
        /// 3D space (e.g. the ankh outline: at the armpits between crossbar and
        /// stem the edge turns inwards). A simple fan would fill in such
        /// notches. First projects onto its own face plane, then triangulates
        /// in 2D.
        /// </summary>
        /// <returns>Flat list of absolute vertex indices, three per triangle.</returns>
        private static List<int> fTriangulatePolygon(List<Vector3> pVerts, int piBase, int piCount)
        {
            List<int> lOResult = new List<int>();

            // Face normal via Newell's method - robust even for slightly non-planar points.
            Vector3 lONormal = Vector3.zero;

            for (int i = 0; i < piCount; i++)
            {
                Vector3 lOCurrent = pVerts[piBase + i];
                Vector3 lONext = pVerts[piBase + ((i + 1) % piCount)];
                lONormal.x += (lOCurrent.y - lONext.y) * (lOCurrent.z + lONext.z);
                lONormal.y += (lOCurrent.z - lONext.z) * (lOCurrent.x + lONext.x);
                lONormal.z += (lOCurrent.x - lONext.x) * (lOCurrent.y + lONext.y);
            }

            if (lONormal.sqrMagnitude < 1e-8f)
                lONormal = Vector3.up; // degenerate/collinear - emergency exit

            lONormal.Normalize();

            Vector3 lOTangent = Mathf.Abs(Vector3.Dot(lONormal, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 lOU = Vector3.Normalize(Vector3.Cross(lONormal, lOTangent));
            Vector3 lOV = Vector3.Cross(lONormal, lOU);

            Vector3 lOOrigin = pVerts[piBase];
            Vector2[] lO2D = new Vector2[piCount];

            for (int i = 0; i < piCount; i++)
            {
                Vector3 lORel = pVerts[piBase + i] - lOOrigin;
                lO2D[i] = new Vector2(Vector3.Dot(lORel, lOU), Vector3.Dot(lORel, lOV));
            }

            // Ear clipping expects a fixed winding direction - determined via the shoelace formula
            // instead of assumed, reversed if needed.
            float lfArea = 0f;

            for (int i = 0; i < piCount; i++)
            {
                Vector2 lOA = lO2D[i];
                Vector2 lOB = lO2D[(i + 1) % piCount];
                lfArea += (lOA.x * lOB.y) - (lOB.x * lOA.y);
            }

            List<int> lOIndices = new List<int>(piCount);

            for (int i = 0; i < piCount; i++)
                lOIndices.Add(i);

            if (lfArea < 0f)
                lOIndices.Reverse();

            int liGuard = (piCount * piCount) + 8; // safety net against endless loops on degenerate data

            while (lOIndices.Count > 3 && liGuard-- > 0)
            {
                bool lbClipped = false;

                for (int i = 0; i < lOIndices.Count; i++)
                {
                    int liPrev = lOIndices[(i - 1 + lOIndices.Count) % lOIndices.Count];
                    int liCurr = lOIndices[i];
                    int liNext = lOIndices[(i + 1) % lOIndices.Count];

                    Vector2 lOA = lO2D[liPrev];
                    Vector2 lOB = lO2D[liCurr];
                    Vector2 lOC = lO2D[liNext];

                    // Convexity: with the now CCW order the cross product of the
                    // edges must be positive, otherwise the corner is re-entrant - not an ear.
                    float lfCross = ((lOB.x - lOA.x) * (lOC.y - lOA.y)) - ((lOB.y - lOA.y) * (lOC.x - lOA.x));

                    if (lfCross <= 0f)
                        continue;

                    bool lbAnyInside = false;

                    for (int j = 0; j < lOIndices.Count; j++)
                    {
                        int liTest = lOIndices[j];

                        if (liTest == liPrev || liTest == liCurr || liTest == liNext)
                            continue;

                        if (fPointInTriangle(lO2D[liTest], lOA, lOB, lOC))
                        {
                            lbAnyInside = true;
                            break;
                        }
                    }

                    if (lbAnyInside)
                        continue;

                    // Same order as the fan above (prev, curr, next).
                    lOResult.Add(piBase + liPrev);
                    lOResult.Add(piBase + liCurr);
                    lOResult.Add(piBase + liNext);

                    lOIndices.RemoveAt(i);
                    lbClipped = true;
                    break;
                }

                if (!lbClipped)
                    break; // degenerate polygon - better fewer triangles than an endless loop
            }

            if (lOIndices.Count == 3)
            {
                lOResult.Add(piBase + lOIndices[0]);
                lOResult.Add(piBase + lOIndices[1]);
                lOResult.Add(piBase + lOIndices[2]);
            }

            return lOResult;
        }

        private static bool fPointInTriangle(Vector2 pP, Vector2 pA, Vector2 pB, Vector2 pC)
        {
            float lfD1 = fSign(pP, pA, pB);
            float lfD2 = fSign(pP, pB, pC);
            float lfD3 = fSign(pP, pC, pA);

            bool lbHasNeg = lfD1 < 0f || lfD2 < 0f || lfD3 < 0f;
            bool lbHasPos = lfD1 > 0f || lfD2 > 0f || lfD3 > 0f;

            return !(lbHasNeg && lbHasPos);
        }

        private static float fSign(Vector2 pP1, Vector2 pP2, Vector2 pP3)
        {
            return ((pP1.x - pP3.x) * (pP2.y - pP3.y)) - ((pP2.x - pP3.x) * (pP1.y - pP3.y));
        }

        private UWTexture fGetTexture(UWTexture.TextureTypes peSource, int piIndex)
        {
            try
            {
                return mOData.Textures.GetTextureByType(peSource, piIndex);
            }
            catch
            {
                return null;
            }
        }

        private string fGetDescription(int piStringIndex)
        {
            try
            {
                return mOData.Strings.Blocks[4].Strings[piStringIndex];
            }
            catch
            {
                return string.Empty;
            }
        }


        /// <summary>
        /// A CLOSED DOOR OR PORTCULLIS BLOCKS ITS WHOLE TILE for the player, not just the leaf
        /// or the plane of the bars. Measured in the original (per user, 2026-09-19, level 1):
        /// walking at the closed portcullis on 56/49 from the east, and at the closed door on
        /// 55/46 likewise, the player stops with FINE POSITION 0 on the neighbouring tile - exactly
        /// on the tile edge - although both sit at sub-tile 3, in the middle of their tile. The
        /// reference agrees: a class 0x14 object with index below 8 is a collision by itself
        /// (FindClosedDoorCollision). For the pole puzzle this is the whole point: from the edge
        /// the button at 55/49 lies 13 by 3 eighths away, 178 squared, beyond the hand; with an
        /// eight unit box at the bars the player got two eighths closer and pressed it without
        /// the pole.
        ///
        /// The box stands on the TILE CENTRE with the door's facing, not on the door, which sits
        /// at its sub-tile spot and (as a leaf) swings. The mover switches it OFF while the door
        /// is open - unlike the frame, which is only ignored for the player, because rays and
        /// missiles must pass through an open doorway - and asks it whether the player stands
        /// in the tile when the door tries to close (the bounce, see DoorMover.BounceAngle).
        /// The player capsule still stops its centre one radius before the edge, about one and
        /// a half eighths further out than the original's point.
        /// </summary>
        /// <summary>A door's COMOBJ radius in eighths - every door and the portcullis have 3.</summary>
        private const int DoorCollisionRadius = 3;

        private static void fAddClosedDoorBlocker(int xPos, int zPos, UWObject pOObject, float pfBottom, float pfHeight,
            float pfHalfWidth, Transform pOParent, IUsableDoor pIDoor)
        {
            GameObject lOBlock = new GameObject(string.Format("Door blocker ({0},{1})", xPos, zPos));
            lOBlock.transform.SetParent(pOParent, false);
            lOBlock.transform.position = new Vector3(xPos * TileSpacing, 0f, zPos * TileSpacing);
            lOBlock.transform.Rotate(Vector3.up, pOObject.Heading * 45f);
            lOBlock.layer = IgnoreRaycastLayer;

            // AS WIDE AND AS TALL AS THE ORIGINAL'S DOOR COLLISION for a missile: the door object is
            // a circle of radius 3 (24 units) and height 128, so a missile of radius 8 strikes it 32
            // units from the centre - the box's face - anywhere in front of leaf, frame and lintel.
            // With the leaf's width and height only, a shot at the upper corner of the frame
            // brushed the corridor wall first and ended there (per user, 2026-09-24). For the
            // player nothing changes: frame and lintel close that part anyway.
            float lfWidth = Mathf.Max(pfHalfWidth * 2f, DoorCollisionRadius * 2f * SubTileScale);
            float lfTop = UWLevelMeshBuilder.CeilingHeight;

            BoxCollider lOBox = lOBlock.AddComponent<BoxCollider>();
            lOBox.center = new Vector3(0f, (pfBottom + lfTop) * 0.5f, 0f);
            lOBox.size = new Vector3(lfWidth, lfTop - pfBottom, TileSpacing);

            pIDoor.SetTileBlocker(lOBox);

            // A BLOW OR A MISSILE ON THE BLOCKER HITS THE DOOR, and the flash shows at the door
            // (per user, 2026-09-24) - the blocker stands where the original's door object has
            // its collision (UWEntityInfo.PartOf, as for the frame).
            if (pIDoor is Component lODoorComponent)
                lOBlock.AddComponent<UWEntityInfo>().PartOfOwner = lODoorComponent;
        }

        /// <summary>
        /// The world position of a door. Until now that was always the tile centre - doors were
        /// the only object type for which the object's sub-tile position was ignored.
        ///
        /// That is noticeable because some doors do not sit centred at all: on level 1
        /// 19 of 33 are at 3/3, the rest at 0/4, 3/0, 3/1, 3/2, 3/5, 4/6, 4/7 and
        /// similar - one in three of the secret doors. The user reported it on the
        /// secret doors (2026-08-30): in the original some of them sit at the side of the tile
        /// instead of in the middle.
        ///
        /// The shift is only ACROSS the door face, not sideways - a door sits further
        /// forward or further back in its tile, but always centred in the opening. Which
        /// axis that is follows from the facing direction.
        ///
        /// The reference point is 3.5, i.e. the middle of the eight sub-tiles - measured
        /// in the original (user, 2026-08-30): the secret door on tile 35/58 has
        /// sub-tile 0 and stands exactly four wall texture pixels away from the tile edge.
        /// A wall texture is 64 pixels over 64 world units, so one pixel is one
        /// unit, and (0 - 3.5) times eight gives minus 28 - four units before the edge at
        /// 32. This also makes clear that the sub-tile number means the centre of an eighth-cell
        /// and not its edge.
        /// </summary>
        private static Vector3 fGetDoorTranslation(int xPos, int zPos, UWObject pOObject)
        {
            Vector3 lOTranslation = new Vector3(xPos * TileSpacing, 0f, zPos * TileSpacing);

            UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

            if (lOSettings != null && !lOSettings.UseDoorSubTilePosition)
                return lOTranslation;

            const float lfSubTileSize = TileSpacing / 8f;

            // Without settings the MEASURED reference point applies, the same default
            // UWSettings carries (3.5 = centre of the eight sub-tiles). The 4 that used to
            // stand here put every edge door onto the tile edge, where the clamp below had
            // to catch it.
            float lfReference = lOSettings != null
                ? lOSettings.DoorSubTileReference
                : UnderworldRevisited.UWSettings.DefaultDoorSubTileReference;

            // Facing 2 and 6 means: the door face lies across the X axis, so the
            // X sub-tile position decides. Otherwise the Z axis.
            bool lbAlongX = pOObject.Heading == 2 || pOObject.Heading == 6;

            float lfOffset = ((lbAlongX ? pOObject.XPos : pOObject.YPos) - lfReference) * lfSubTileSize;

            // Safeguard in case the reference point is set to 4: then an edge door would lie
            // exactly on the tile edge and stick out beyond it by half its thickness.
            // With the measured value 3.5 the clamp never kicks in.
            float lfLimit = (TileSpacing * 0.5f) - DoorAssemblyHalfDepth;

            lfOffset = Mathf.Clamp(lfOffset, -lfLimit, lfLimit);

            if (lbAlongX)
                lOTranslation.x += lfOffset;
            else
                lOTranslation.z += lfOffset;

            return lOTranslation;
        }
        /// <summary>The game data knows every door twice: 320-327 closed,
        /// 328-335 the same ones open (see UWLevel and string block 4, "an_open door").
        /// </summary>
        private static bool fIsOpenDoorId(int piId)
        {
            return piId >= 328 && piId <= 335;
        }

        private void fSpawnDoor(int xPos, int zPos, UWTile pOTile, UWObject pOObject, Transform pOParent)
        {
            // Secret door (327, 0x0147): according to game knowledge (not docs) it shows the tile's
            // wall texture instead of the door graphic - disguised as part of the wall. Needs
            // its own geometry/UV handling (Texture2DArray slice instead of object atlas rect),
            // see fSpawnSecretDoor.
            // 335 is the same secret door open (see UWLevel: two groups of eight).
            if (pOObject.ID == 327 || pOObject.ID == 335)
            {
                fSpawnSecretDoor(xPos, zPos, pOTile, pOObject, pOParent);
                return;
            }

            // RE-RESEARCHED (2026-09-05): the reference has no model for it either and
            // builds the bars by hand. There is no grille graphic - all thirteen images
            // in DOORS.GR are solid doors, and the reference's formula (58 + id and 7)
            // reads past the end of the level table for the portcullis, which only has six
            // door entries (58 to 63). The user also found no differently coloured
            // portcullis in the original, so it is not the tile's wall texture either
            // as with the secret door. That leaves fixed geometry with a fixed colour - as here.
            //
            // Portcullis (326, 0x0146): not a sprite/quad like a normal door, but a
            // grille of bars which according to game knowledge (not docs) is pulled vertically up into the
            // ceiling instead of swinging open. The real grille geometry could not be found in
            // any of the accessible data sources (model table, DOORS.GR, TMOBJ.GR,
            // TMFLAT.GR - all searched, see session research), hence built procedurally.
            // 334 is the same portcullis open.
            if (pOObject.ID == 326 || pOObject.ID == 334)
            {
                fSpawnPortcullis(xPos, zPos, pOTile, pOObject, pOParent);
                return;
            }

            UWTextureRef lOResolved;

            if (!fResolve(pOObject.Texture, false, out lOResolved) || lOResolved.IsArray)
            {
                UnresolvedCount++;
                return;
            }

            float lfBottom = pOTile.FloorHeight;

            // The door texture is 32x64, the door leaf only occupies its lower part.
            // Height, width and UV range come from the measured outline of the graphic.
            float lfHeight = DoorHeight;
            float lfHalfWidth = DoorHalfWidth;
            float lfUMin = 0f;
            float lfUMax = 1f;
            float lfVMin = 0f;
            float lfVMax = 1f;

            UWObjectAtlas.Entry lOEntry;

            if (mOAtlas != null && mOAtlas.TryGetEntry(pOObject.Texture, out lOEntry) && lOEntry.OpaqueSize.y > 0)
            {
                lfHeight = lOEntry.OpaqueSize.y;
                lfHalfWidth = lOEntry.OpaqueSize.x * 0.5f;

                lfUMin = (float)lOEntry.OpaqueMin.x / lOEntry.Size.x;
                lfUMax = (float)(lOEntry.OpaqueMin.x + lOEntry.OpaqueSize.x) / lOEntry.Size.x;
                lfVMin = (float)lOEntry.OpaqueMin.y / lOEntry.Size.y;
                lfVMax = (float)(lOEntry.OpaqueMin.y + lOEntry.OpaqueSize.y) / lOEntry.Size.y;
            }

            // From here on we work with final atlas coordinates instead of normalised values, because
            // the narrow sides (see below) point to a different atlas tile than the
            // door graphic itself - a single UvRect scaling at the end (as otherwise via
            // fApplyUVs) would no longer be sufficient for that.
            Vector2 lOUvOrigin = lOResolved.UvRect.position;
            Vector2 lOUvSize = lOResolved.UvRect.size;

            // Narrow sides: in the original a real palette colour - the same as the
            // door frame (model index 01, see UW3DModelImport.GetPrimaryPaletteIndex), not a
            // pixel from the door graphic itself. Fallback to the old "most frequent
            // sprite pixel" trick in case the palette tiles are ever missing from the atlas
            // or no model import (UW.EXE) is available.
            byte liDoorFrameColor = mO3DModels != null
                ? mO3DModels.GetPrimaryPaletteIndex(UW3DModelImport.ModelIndex.DoorFrame)
                : (byte)0;
            Vector2 lOSolid;

            if (mOAtlas == null || liDoorFrameColor == 0 || !mOAtlas.TryGetPaletteUv(liDoorFrameColor, out lOSolid))
            {
                Vector2 lOFallbackNormalised = new Vector2(0.5f, 0.5f);

                if (mOAtlas != null && mOAtlas.TryGetEntry(pOObject.Texture, out lOEntry) && lOEntry.Size.y > 0)
                {
                    lOFallbackNormalised = new Vector2(
                        (lOEntry.SolidPixel.x + 0.5f) / lOEntry.Size.x,
                        (lOEntry.SolidPixel.y + 0.5f) / lOEntry.Size.y);
                }

                lOSolid = lOUvOrigin + Vector2.Scale(lOFallbackNormalised, lOUvSize);
            }

            float lfTop = lfBottom + lfHeight;
            const float lfHalfDepth = 1f;

            GameObject lODoor = new GameObject(string.Format("Door ({0},{1})", xPos, zPos));
            lODoor.transform.SetParent(pOParent, false);

            Vector3 lOTranslation = fGetDoorTranslation(xPos, zPos, pOObject);
            lODoor.transform.position = lOTranslation;
            lODoor.transform.Rotate(Vector3.up, pOObject.Heading * 45f);

            System.Collections.Generic.List<Vector3> lOVertices = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<Vector2> lOUVs = new System.Collections.Generic.List<Vector2>();
            System.Collections.Generic.List<int> liTriangles = new System.Collections.Generic.List<int>();

            Vector2 lOUvA = lOUvOrigin + Vector2.Scale(new Vector2(lfUMax, lfVMin), lOUvSize);
            Vector2 lOUvB = lOUvOrigin + Vector2.Scale(new Vector2(lfUMin, lfVMin), lOUvSize);
            Vector2 lOUvC = lOUvOrigin + Vector2.Scale(new Vector2(lfUMin, lfVMax), lOUvSize);
            Vector2 lOUvD = lOUvOrigin + Vector2.Scale(new Vector2(lfUMax, lfVMax), lOUvSize);

            // Both broad sides carry the same UVs. The door is a flat graphic: from
            // one side it reads correctly, from the other it is necessarily
            // mirrored - like a real door whose lock moves to the other side
            // when you walk around it.
            fAddQuad(lOVertices, lOUVs, liTriangles,
                new Vector3(lfHalfWidth, lfBottom, -lfHalfDepth), new Vector3(-lfHalfWidth, lfBottom, -lfHalfDepth),
                new Vector3(-lfHalfWidth, lfTop, -lfHalfDepth), new Vector3(lfHalfWidth, lfTop, -lfHalfDepth),
                lOUvA, lOUvB, lOUvC, lOUvD);

            fAddQuad(lOVertices, lOUVs, liTriangles,
                new Vector3(-lfHalfWidth, lfBottom, lfHalfDepth), new Vector3(lfHalfWidth, lfBottom, lfHalfDepth),
                new Vector3(lfHalfWidth, lfTop, lfHalfDepth), new Vector3(-lfHalfWidth, lfTop, lfHalfDepth),
                lOUvB, lOUvA, lOUvD, lOUvC);

            // Narrow sides solid-coloured: in the original they do not show the door graphic.
            fAddSolidQuad(lOVertices, lOUVs, liTriangles, lOSolid,
                new Vector3(-lfHalfWidth, lfBottom, -lfHalfDepth), new Vector3(-lfHalfWidth, lfBottom, lfHalfDepth),
                new Vector3(-lfHalfWidth, lfTop, lfHalfDepth), new Vector3(-lfHalfWidth, lfTop, -lfHalfDepth));

            fAddSolidQuad(lOVertices, lOUVs, liTriangles, lOSolid,
                new Vector3(lfHalfWidth, lfBottom, lfHalfDepth), new Vector3(lfHalfWidth, lfBottom, -lfHalfDepth),
                new Vector3(lfHalfWidth, lfTop, -lfHalfDepth), new Vector3(lfHalfWidth, lfTop, lfHalfDepth));

            fAddSolidQuad(lOVertices, lOUVs, liTriangles, lOSolid,
                new Vector3(-lfHalfWidth, lfTop, lfHalfDepth), new Vector3(lfHalfWidth, lfTop, lfHalfDepth),
                new Vector3(lfHalfWidth, lfTop, -lfHalfDepth), new Vector3(-lfHalfWidth, lfTop, -lfHalfDepth));

            fAddSolidQuad(lOVertices, lOUVs, liTriangles, lOSolid,
                new Vector3(-lfHalfWidth, lfBottom, -lfHalfDepth), new Vector3(lfHalfWidth, lfBottom, -lfHalfDepth),
                new Vector3(lfHalfWidth, lfBottom, lfHalfDepth), new Vector3(-lfHalfWidth, lfBottom, lfHalfDepth));

            Mesh lOMesh = new Mesh();
            lOMesh.name = "Door";
            lOMesh.SetVertices(lOVertices);
            lOMesh.SetTriangles(liTriangles, 0);

            // No fApplyUVs: lOUVs already contains final atlas coordinates (see above).
            lOMesh.uv = lOUVs.ToArray();

            lOMesh.RecalculateBounds();
            lOMesh.RecalculateNormals();

            lODoor.AddComponent<MeshFilter>().sharedMesh = lOMesh;
            lODoor.AddComponent<MeshRenderer>().sharedMaterial = lOResolved.Material;
            lODoor.AddComponent<MeshCollider>().sharedMesh = lOMesh;

            DoorMover lOMover = lODoor.AddComponent<DoorMover>();
            lOMover.RotationPoint = (Quaternion.Euler(0f, pOObject.Heading * 45f, 0f) * new Vector3(-DoorHingeOffset, 0f, 0f)) + lOTranslation;
            lOMover.DoorDirection = pOObject.DoorDirection;
            lOMover.StartsOpen = fIsOpenDoorId(pOObject.ID);

            lODoor.AddComponent<UWDoorLock>();

            fAttachDoorHealth(lODoor, pOObject);

            fAddClosedDoorBlocker(xPos, zPos, pOObject, lfBottom, lfHeight, lfHalfWidth, pOParent, lOMover);

            UWEntityInfo lOInfo = lODoor.AddComponent<UWEntityInfo>();
            lOInfo.ObjectData = pOObject;
            lOInfo.IsDoor = true;
            lOInfo.Description = fGetDoorDescription(pOObject);

            fSpawnDoorFrames(xPos, zPos, pOTile, pOObject, lfBottom, lfHeight, pOParent, lOMover);

            SpawnedCount++;
        }

        /// <summary>
        /// Portcullis (326, 0x0146): procedural bar grille (vertical + horizontal
        /// bars), striped in the palette slots 106 to 110 of the blue-grey row as measured on
        /// an original screenshot (see the method body). Moves vertically into the ceiling via PortcullisMover
        /// instead of swinging open like a door.
        /// </summary>
        private void fSpawnPortcullis(int xPos, int zPos, UWTile pOTile, UWObject pOObject, Transform pOParent)
        {
            if (mODecalMaterial == null)
            {
                UnresolvedCount++;
                return;
            }

            float lfBottom = pOTile.FloorHeight;
            float lfHeight = DoorHeight;
            float lfHalfWidth = DoorHalfWidth;
            float lfTop = lfBottom + lfHeight;

            // Do not raise it by its full height - in the original a narrow strip of the grille
            // stays visible at the bottom of the frame, see screenshot comparison
            // (considerably less than the 20% tried earlier).
            //
            // Previously it was additionally capped at (ceiling height - door height) so the grille
            // does not poke through the ceiling. That was too strict and practically ate up the lift on high
            // tiles (reported by user, 2026-08-28: "only goes up about
            // 16"): CeilingHeight is a flat 256, with a tile floor height of
            // 192 and DoorHeight 52 only 256-192-52 = 12 units of lift remained instead of the
            // intended 46.8. The cap is also unnecessary - above the door opening there is
            // solid masonry including the lintel anyway (see fSpawnDoorFrames), behind which
            // the raised part of the grille disappears. That is exactly how the
            // original hides it too.
            float lfLiftHeight = lfHeight * 0.9f;

            GameObject lOPortcullis = new GameObject(string.Format("Portcullis ({0},{1})", xPos, zPos));
            lOPortcullis.transform.SetParent(pOParent, false);

            Vector3 lOTranslation = fGetDoorTranslation(xPos, zPos, pOObject);
            lOPortcullis.transform.position = lOTranslation;
            lOPortcullis.transform.Rotate(Vector3.up, pOObject.Heading * 45f);

            Vector2 lOWhiteUv = Vector2.zero;

            if (mOAtlas != null)
                mOAtlas.TryGetPaletteUv(UWObjectAtlas.WhitePaletteIndex, out lOWhiteUv);

            // MEASURED ON THE ORIGINAL (user's screenshot, lantern, directly in front of the
            // grille, 2026-09-05 - file "Screenshot 2026-09-05 215237.png"). Evaluated
            // were the colour sequences of individual image rows and columns; the pixels are
            // scaled up 3.33x horizontally and 4x vertically (320x200 to 4:3).
            //
            // ALL colours are slots of ONE blue-grey row (96 to 111), without any
            // scatter: 106 (#4D4D65), 107 (#414159), 108 (#393949), 109 (#31313D),
            // 110 (#242431). With lantern and zero distance brightness level 0 applies, which in
            // LIGHT.DAT is the identity - so the observed colours ARE the palette colours.
            //
            // EACH COLOUR IS A FACE OF ITS OWN, not a gradient: the colour sequences are
            // hard blocks with exact palette values, and the user had previously seen a
            // rendering glitch in the original in which the triangles per colour
            // get twisted. Hence fixed stripes here.
            //
            // Vertical bar, 6 pixels wide, from left to right:
            //   110 110 108 106 106 109
            // Horizontal bar, 7 image rows high, from top to bottom, symmetrical:
            //   110 109 107 106 107 109 110
            // Identical in all three visible horizontal bars.
            //
            // NOT REPRODUCED: along a vertical bar the light stripe gets darker
            // towards the bottom (106 at the top, 107 from about the middle), while the horizontal
            // bars are equally bright at all heights. Where that comes from is open - a
            // distance shading that only affects the vertical ones makes no sense, and
            // a per-vertex darkening would contradict the fixed stripes. For us
            // the distance shading comes uniformly from the shader.
            //
            // About the row: LIGHT.DAT maps 106 over levels 0..7 to 106 106 107 107
            // 108 109 109 110 - so the stripe colours are exactly the ones a bar of
            // base colour 106 takes on with increasing darkening. The original still colours the
            // faces individually, which is why palette slots are listed here and not
            // darkening values.
            UWPalette lOBarPalette2 = mOData != null ? mOData.Palettes.GetPalette(0) : null;

            Color32 lOTone106 = fGetPaletteColour(lOBarPalette2, 106, new Color32(76, 76, 100, 255));
            Color32 lOTone107 = fGetPaletteColour(lOBarPalette2, 107, new Color32(64, 64, 88, 255));
            Color32 lOTone108 = fGetPaletteColour(lOBarPalette2, 108, new Color32(56, 56, 72, 255));
            Color32 lOTone109 = fGetPaletteColour(lOBarPalette2, 109, new Color32(48, 48, 60, 255));
            Color32 lOTone110 = fGetPaletteColour(lOBarPalette2, 110, new Color32(36, 36, 48, 255));

            // Stripes of equal width; two identical ones side by side make one of double
            // width. Horizontal from bottom to top.
            //
            // Vertical MIRRORED relative to the image: you see the FAR face of the
            // bar (see below), and from that side its pattern appears
            // mirrored. The stripes are arranged here so that the image is right
            // again (per user, 2026-09-05).
            Color32[] lOVerticalStripes = { lOTone109, lOTone106, lOTone106, lOTone108, lOTone110, lOTone110 };
            Color32[] lOHorizontalStripes = { lOTone110, lOTone109, lOTone107, lOTone106, lOTone107, lOTone109, lOTone110 };

            List<Vector3> lOVerts = new List<Vector3>();
            List<Vector2> lOUvs = new List<Vector2>();
            List<Color32> lOColors = new List<Color32>();
            List<int> liTriangles = new List<int>();

            // COUNTED ON THE ORIGINAL (per user, 2026-09-05): three vertical bars, four
            // horizontal. The thicknesses were adjusted by eye - first 4 and 5,
            // then found too thick and reduced to 3 and 3.5. Before that it was five and
            // three, both 1.5 thick - nobody had cared about that back then. The by-eye
            // thicknesses are superseded by the measurement below.
            //
            // DIMENSIONS FROM THE SAME SCREENSHOT, converted via the gate width: the
            // opening is 256 pixels wide in the image and 32 units in the world, so one
            // pixel is 0.125 units wide; vertically one image row corresponds to
            // 0.5 units (4 pixels per original row, and those are 1.2 times as high as
            // wide).
            //
            //   vertical bars:      three, each 6 original columns = 2.5 units wide, with
            //                       centres at 19.5 %, 50 % and 80.5 % of the width -
            //                       i.e. 6.25 units from the edge to the centre.
            //   horizontal bars:    four, each 7 original rows = 3.5 units high, at a
            //                       spacing of 22 rows = 11 units; the topmost sits
            //                       10.5 units below the top edge, the lowest therefore
            //                       8.5 above the floor.
            //
            // VERTICALS IN FRONT: in the middle of a horizontal bar the stripes
            // of the vertical ones run through undisturbed, so the verticals lie in front. Solved
            // here via depth.
            //
            // CAUTION, BACK TO FRONT: the two faces of a bar are wound so
            // that with back-face culling you always see the FAR one - just
            // like in the original (per user in image comparison, 2026-09-05). So the visible
            // face is the more distant one. For the verticals to lie in front, their
            // faces therefore have to sit closer to the centre than those of the horizontals, not
            // further out: the smaller depth wins the depth test from behind.
            const int VerticalBarCount = 3;
            const int HorizontalBarCount = 4;
            const float VerticalBarWidth = 2.5f;
            const float HorizontalBarHeight = 3.5f;
            const float VerticalBarDepth = 1f;
            const float HorizontalBarDepth = 2f;
            const float VerticalBarMargin = 6.25f;
            const float HorizontalBarSpacing = 11f;
            const float LowestHorizontalBar = 8.5f;

            for (int i = 0; i < VerticalBarCount; i++)
            {
                float lfT = VerticalBarCount == 1 ? 0.5f : (float)i / (VerticalBarCount - 1);
                float lfX = Mathf.Lerp(-lfHalfWidth + VerticalBarMargin, lfHalfWidth - VerticalBarMargin, lfT);

                fAddBar(lOVerts, lOUvs, lOColors, liTriangles, lOWhiteUv, lOVerticalStripes,
                    new Vector3(lfX - VerticalBarWidth * 0.5f, lfBottom, -VerticalBarDepth * 0.5f),
                    new Vector3(lfX + VerticalBarWidth * 0.5f, lfTop, VerticalBarDepth * 0.5f));
            }

            // After raising (lfLiftHeight) the lowest bar must disappear completely into the
            // frame - in the original no crossbar is visible any more when open
            // (screenshot comparison). At 8.5 plus 0.9 times 52 its lower edge lies
            // at 53.55, i.e. above the top edge of 52.
            for (int i = 0; i < HorizontalBarCount; i++)
            {
                float lfY = lfBottom + LowestHorizontalBar + (HorizontalBarSpacing * i);

                fAddBar(lOVerts, lOUvs, lOColors, liTriangles, lOWhiteUv, lOHorizontalStripes,
                    new Vector3(-lfHalfWidth, lfY - HorizontalBarHeight * 0.5f, -HorizontalBarDepth * 0.5f),
                    new Vector3(lfHalfWidth, lfY + HorizontalBarHeight * 0.5f, HorizontalBarDepth * 0.5f));
            }

            Mesh lOMesh = new Mesh();
            lOMesh.name = "Portcullis";
            lOMesh.SetVertices(lOVerts);
            lOMesh.SetTriangles(liTriangles, 0);
            lOMesh.SetUVs(0, lOUvs);
            lOMesh.SetColors(lOColors);

            // Second UV set for the palette renderer, derived from the vertex colours.
            //
            // This works in ONE pass, because every face of the grille already carries a
            // uniform colour (see fAddStrip: one separate quad per stripe, not a
            // gradient). The user observed exactly that in the original - there too
            // every colour is a face of its own, visible in a rendering glitch in which
            // the triangles get twisted (2026-09-05).
            //
            // The bar colours already are palette colours (slots 106 to 110, see above), so
            // the nearest palette slot gives back exactly those indices. Darkening zero - in the original these
            // faces are flat, not shaded.
            List<Vector2> lOPaletteData = new List<Vector2>(lOColors.Count);

            UWPalette lOBarPalette = mOData != null ? mOData.Palettes.GetPalette(0) : null;

            foreach (Color32 lOBarColour in lOColors)
            {
                float lfIndex = lOBarPalette != null
                    ? lOBarPalette.GetNearestIndex(lOBarColour.r, lOBarColour.g, lOBarColour.b)
                    : 0f;

                lOPaletteData.Add(new Vector2(lfIndex, 1f));
            }

            lOMesh.SetUVs(1, lOPaletteData);
            lOMesh.RecalculateBounds();
            lOMesh.RecalculateNormals();

            lOPortcullis.AddComponent<MeshFilter>().sharedMesh = lOMesh;
            // Separate material: like the one for solid-colour model faces, but without the face
            // facing the viewer (see UWLevelLoader.PortcullisMaterial).
            lOPortcullis.AddComponent<MeshRenderer>().sharedMaterial =
                mOPortcullisMaterial != null ? mOPortcullisMaterial : mODecalMaterial;

            // TWO COLLIDERS WITH DIFFERENT JOBS.
            //
            // The ray for look, click and strike only hits the BARS - through
            // the gaps you reach what lies behind, as in the original. The
            // visual mesh is not directly suitable for that: its faces are deliberately wound
            // back to front (see above), and rays only hit front faces. Hence a
            // separate collision mesh with every triangle in BOTH windings.
            //
            // The PLAYER, on the other hand, is stopped by a generous box that fills the whole
            // opening - just as the original presumably puts a rough frame around
            // closed doors and grilles (per user, 2026-09-05). Previously
            // the visual mesh served as collider; with Unity's one-sided movement collision
            // you slipped in through the front face and got stuck on the back face
            // (per user: "I get stuck on the grille").
            //
            // The box lies on Unity's built-in layer "Ignore Raycast" (number 2): the
            // physics matrix lets it collide with everything, Physics.Raycast skips it
            // by itself without a mask. Both colliders hang on the same object or
            // below it, so the lift mechanism moves them together and disables them together
            // when open (see PortcullisMover).
            Mesh lOCollisionMesh = new Mesh();
            lOCollisionMesh.name = "Portcullis (Collision)";
            lOCollisionMesh.SetVertices(lOVerts);

            List<int> lOBothSides = new List<int>(liTriangles);

            for (int i = 0; i + 2 < liTriangles.Count; i += 3)
            {
                lOBothSides.Add(liTriangles[i]);
                lOBothSides.Add(liTriangles[i + 2]);
                lOBothSides.Add(liTriangles[i + 1]);
            }

            lOCollisionMesh.SetTriangles(lOBothSides, 0);
            lOCollisionMesh.RecalculateBounds();

            lOPortcullis.AddComponent<MeshCollider>().sharedMesh = lOCollisionMesh;

            PortcullisMover lOMover = lOPortcullis.AddComponent<PortcullisMover>();
            lOMover.StartsOpen = fIsOpenDoorId(pOObject.ID);
            lOMover.LiftHeight = lfLiftHeight;

            fAddClosedDoorBlocker(xPos, zPos, pOObject, lfBottom, lfHeight, lfHalfWidth, pOParent, lOMover);

            lOPortcullis.AddComponent<UWDoorLock>();

            // DELIBERATELY no UWDamageable: that a wooden door can be smashed has been
            // verified in the original (see UWDoorDamage). For the iron portcullis there is
            // no such observation, and inventing destructibility would be guesswork.

            UWEntityInfo lOInfo = lOPortcullis.AddComponent<UWEntityInfo>();
            lOInfo.ObjectData = pOObject;
            lOInfo.IsDoor = true;
            lOInfo.Description = fGetDoorDescription(pOObject);

            fSpawnDoorFrames(xPos, zPos, pOTile, pOObject, lfBottom, lfHeight, pOParent, lOMover);

            SpawnedCount++;
        }

        /// <summary>
        /// One portcullis bar: a front and a back face, each split into stripes across the
        /// thin axis of the bar, one stripe per entry of pOStripes - the thin axis is detected
        /// automatically (X for standing, Y for lying bars). UV always points to the neutral
        /// white atlas tile, the colour is carried by the vertex colour.
        ///
        /// The stripe colours are real palette colours and are NO LONGER lightened and
        /// darkened from a base colour - in the original they are neighbouring
        /// slots of the same row (see fSpawnPortcullis).
        /// </summary>
        private static void fAddBar(List<Vector3> pOVerts, List<Vector2> pOUvs, List<Color32> pOColors, List<int> piTriangles,
            Vector2 pOUv, Color32[] pOStripes, Vector3 pOMin, Vector3 pOMax)
        {

            bool lbThinX = (pOMax.x - pOMin.x) <= (pOMax.y - pOMin.y);

            if (lbThinX)
            {
                fAddGradientQuadX(pOVerts, pOUvs, pOColors, piTriangles, pOUv, pOStripes,
                    pOMin.x, pOMax.x, pOMin.y, pOMax.y, pOMin.z, false);
                fAddGradientQuadX(pOVerts, pOUvs, pOColors, piTriangles, pOUv, pOStripes,
                    pOMin.x, pOMax.x, pOMin.y, pOMax.y, pOMax.z, true);
            }
            else
            {
                fAddGradientQuadY(pOVerts, pOUvs, pOColors, piTriangles, pOUv, pOStripes,
                    pOMin.x, pOMax.x, pOMin.y, pOMax.y, pOMin.z, false);
                fAddGradientQuadY(pOVerts, pOUvs, pOColors, piTriangles, pOUv, pOStripes,
                    pOMin.x, pOMax.x, pOMin.y, pOMax.y, pOMax.z, true);
            }

            // NO SIDE FACES. Previously every bar was a box with four additional
            // faces on the narrow sides. The original has none: from the frame the user
            // sees the FAR face of the grille there (2026-09-05), which only
            // works if nothing lies in between. That fits the rendering glitch in the original
            // in which the triangles get twisted - those are flat polygons, not solids.
            //
            // The bar depth is still preserved, because front and back faces lie at
            // different Z. The portcullis material culls back faces (see
            // UWLevelLoader.PortcullisMaterial), and because of the reversed winding the face
            // that remains per view direction is the far one - exactly the observed image.
        }

        /// <summary>Front/back face of a bar, split into one stripe per entry of pOStripes
        /// along X - for standing bars whose thin axis is X.</summary>
        private static void fAddGradientQuadX(List<Vector3> pOVerts, List<Vector2> pOUvs, List<Color32> pOColors, List<int> piTriangles,
            Vector2 pOUv, Color32[] pOStripes, float pfXMin, float pfXMax, float pfYMin, float pfYMax, float pfZ, bool pbBack)
        {
            int liCount = pOStripes.Length;

            Vector2[] lyBottom = new Vector2[liCount + 1];
            Vector2[] lyTop = new Vector2[liCount + 1];

            for (int liAt = 0; liAt <= liCount; liAt++)
            {
                float lfX = Mathf.Lerp(pfXMin, pfXMax, (float)liAt / liCount);

                lyBottom[liAt] = new Vector2(lfX, pfYMin);
                lyTop[liAt] = new Vector2(lfX, pfYMax);
            }

            fAddStrip(pOVerts, pOUvs, pOColors, piTriangles, pOUv, pOStripes, lyBottom, lyTop, pfZ, pbBack);
        }

        /// <summary>Like fAddGradientQuadX, but split into stripes along Y - for lying
        /// bars whose thin axis is Y.</summary>
        private static void fAddGradientQuadY(List<Vector3> pOVerts, List<Vector2> pOUvs, List<Color32> pOColors, List<int> piTriangles,
            Vector2 pOUv, Color32[] pOStripes, float pfXMin, float pfXMax, float pfYMin, float pfYMax, float pfZ, bool pbBack)
        {
            int liCount = pOStripes.Length;

            // Edge order (right first, left second) swapped compared to fAddGradientQuadX,
            // so that with this stripe direction rotated by 90 degrees the triangles keep
            // the same winding (and thus normal direction) - otherwise the
            // normals of the lying bars point backwards instead of forwards.
            Vector2[] lyRight = new Vector2[liCount + 1];
            Vector2[] lyLeft = new Vector2[liCount + 1];

            for (int liAt = 0; liAt <= liCount; liAt++)
            {
                float lfY = Mathf.Lerp(pfYMin, pfYMax, (float)liAt / liCount);

                lyRight[liAt] = new Vector2(pfXMax, lfY);
                lyLeft[liAt] = new Vector2(pfXMin, lfY);
            }

            fAddStrip(pOVerts, pOUvs, pOColors, piTriangles, pOUv, pOStripes, lyRight, lyLeft, pfZ, pbBack);
        }

        /// <summary>Builds one partial quad per stripe colour from two edge lines of
        /// (stripe count + 1) points each, from start to end.</summary>
        private static void fAddStrip(List<Vector3> pOVerts, List<Vector2> pOUvs, List<Color32> pOColors, List<int> piTriangles,
            Vector2 pOUv, Color32[] pOStripes, Vector2[] pOEdge1, Vector2[] pOEdge2,
            float pfZ, bool pbBack)
        {
            Vector2[] lyEdge1 = pOEdge1;
            Vector2[] lyEdge2 = pOEdge2;
            Color32[] lyColors = pOStripes;

            for (int i = 0; i < lyColors.Length; i++)
            {
                Vector3 lOA = new Vector3(lyEdge1[i].x, lyEdge1[i].y, pfZ);
                Vector3 lOB = new Vector3(lyEdge1[i + 1].x, lyEdge1[i + 1].y, pfZ);
                Vector3 lOC = new Vector3(lyEdge2[i + 1].x, lyEdge2[i + 1].y, pfZ);
                Vector3 lOD = new Vector3(lyEdge2[i].x, lyEdge2[i].y, pfZ);

                int liBase = pOVerts.Count;

                if (!pbBack)
                {
                    pOVerts.Add(lOA); pOVerts.Add(lOB); pOVerts.Add(lOC); pOVerts.Add(lOD);
                }
                else
                {
                    pOVerts.Add(lOD); pOVerts.Add(lOC); pOVerts.Add(lOB); pOVerts.Add(lOA);
                }

                for (int v = 0; v < 4; v++)
                {
                    pOUvs.Add(pOUv);
                    pOColors.Add(lyColors[i]);
                }

                piTriangles.Add(liBase);
                piTriangles.Add(liBase + 1);
                piTriangles.Add(liBase + 2);
                piTriangles.Add(liBase);
                piTriangles.Add(liBase + 2);
                piTriangles.Add(liBase + 3);
            }
        }

        /// <summary>A palette colour, or the fallback value if there is no palette.</summary>
        private static Color32 fGetPaletteColour(UWPalette pOPalette, int piIndex, Color32 pOFallback)
        {
            if (pOPalette == null)
                return pOFallback;

            UWColor32 lOColour = pOPalette.GetUWColor(piIndex);

            return new Color32(lOColour.R, lOColour.G, lOColour.B, 255);
        }


        /// <summary>
        /// Secret door (327, 0x0147): instead of the door graphic from the object atlas it shows the
        /// wall texture of its own tile (Texture2DArray slice, as with the door frame in
        /// fSpawnDoorFrames) - so it blends visually seamlessly into the wall. Otherwise
        /// the same geometry/mechanics as a normal door (DoorMover, frame).
        /// </summary>
        private void fSpawnSecretDoor(int xPos, int zPos, UWTile pOTile, UWObject pOObject, Transform pOParent)
        {
            if (mODungeonMaterial == null)
            {
                UnresolvedCount++;
                return;
            }

            float lfBottom = pOTile.FloorHeight;
            float lfHeight = DoorHeight;
            float lfHalfWidth = DoorHalfWidth;
            float lfTop = lfBottom + lfHeight;
            const float lfHalfDepth = 1f;

            GameObject lODoor = new GameObject(string.Format("SecretDoor ({0},{1})", xPos, zPos));
            lODoor.transform.SetParent(pOParent, false);

            Vector3 lOTranslation = fGetDoorTranslation(xPos, zPos, pOObject);
            lODoor.transform.position = lOTranslation;
            lODoor.transform.Rotate(Vector3.up, pOObject.Heading * 45f);

            List<Vector3> lOVertices = new List<Vector3>();
            List<Vector2> lOUVs = new List<Vector2>();
            List<int> liTriangles = new List<int>();

            Vector3 lOA = new Vector3(lfHalfWidth, lfBottom, -lfHalfDepth);
            Vector3 lOB = new Vector3(-lfHalfWidth, lfBottom, -lfHalfDepth);
            Vector3 lOC = new Vector3(-lfHalfWidth, lfTop, -lfHalfDepth);
            Vector3 lOD = new Vector3(lfHalfWidth, lfTop, -lfHalfDepth);
            Vector3 lOE = new Vector3(-lfHalfWidth, lfBottom, lfHalfDepth);
            Vector3 lOF = new Vector3(lfHalfWidth, lfBottom, lfHalfDepth);
            Vector3 lOG = new Vector3(lfHalfWidth, lfTop, lfHalfDepth);
            Vector3 lOH = new Vector3(-lfHalfWidth, lfTop, lfHalfDepth);

            // Both broad sides carry the same tile wall texture as the surroundings, but
            // according to the user's comparison with the original deliberately shifted slightly against it - in the
            // original a small recognisable offset stays visible in the joints (the
            // door is disguised but does not merge completely invisibly with the wall). Without
            // the offset it was completely invisible in testing.
            Vector2 lOSecretDoorUvOffset = new Vector2(3f / 64f, 2f / 64f);

            fAddQuad(lOVertices, lOUVs, liTriangles, lOA, lOB, lOC, lOD,
                fGetWallUv(lOA) + lOSecretDoorUvOffset, fGetWallUv(lOB) + lOSecretDoorUvOffset,
                fGetWallUv(lOC) + lOSecretDoorUvOffset, fGetWallUv(lOD) + lOSecretDoorUvOffset);

            fAddQuad(lOVertices, lOUVs, liTriangles, lOE, lOF, lOG, lOH,
                fGetWallUv(lOE) + lOSecretDoorUvOffset, fGetWallUv(lOF) + lOSecretDoorUvOffset,
                fGetWallUv(lOG) + lOSecretDoorUvOffset, fGetWallUv(lOH) + lOSecretDoorUvOffset);

            fAddSolidQuad(lOVertices, lOUVs, liTriangles, fGetWallUv(lOB),
                new Vector3(-lfHalfWidth, lfBottom, -lfHalfDepth), new Vector3(-lfHalfWidth, lfBottom, lfHalfDepth),
                new Vector3(-lfHalfWidth, lfTop, lfHalfDepth), new Vector3(-lfHalfWidth, lfTop, -lfHalfDepth));

            fAddSolidQuad(lOVertices, lOUVs, liTriangles, fGetWallUv(lOA),
                new Vector3(lfHalfWidth, lfBottom, lfHalfDepth), new Vector3(lfHalfWidth, lfBottom, -lfHalfDepth),
                new Vector3(lfHalfWidth, lfTop, -lfHalfDepth), new Vector3(lfHalfWidth, lfTop, lfHalfDepth));

            fAddSolidQuad(lOVertices, lOUVs, liTriangles, fGetWallUv(lOC),
                new Vector3(-lfHalfWidth, lfTop, lfHalfDepth), new Vector3(lfHalfWidth, lfTop, lfHalfDepth),
                new Vector3(lfHalfWidth, lfTop, -lfHalfDepth), new Vector3(-lfHalfWidth, lfTop, -lfHalfDepth));

            fAddSolidQuad(lOVertices, lOUVs, liTriangles, fGetWallUv(lOB),
                new Vector3(-lfHalfWidth, lfBottom, -lfHalfDepth), new Vector3(lfHalfWidth, lfBottom, -lfHalfDepth),
                new Vector3(lfHalfWidth, lfBottom, lfHalfDepth), new Vector3(-lfHalfWidth, lfBottom, lfHalfDepth));

            List<Vector3> lOTexCoords = new List<Vector3>(lOUVs.Count);

            for (int i = 0; i < lOUVs.Count; i++)
                lOTexCoords.Add(new Vector3(lOUVs[i].x, lOUVs[i].y, pOTile.TextureWall));

            Mesh lOMesh = new Mesh();
            lOMesh.name = "SecretDoor";
            lOMesh.SetVertices(lOVertices);
            lOMesh.SetTriangles(liTriangles, 0);
            lOMesh.SetUVs(0, lOTexCoords);
            lOMesh.RecalculateBounds();
            lOMesh.RecalculateNormals();

            lODoor.AddComponent<MeshFilter>().sharedMesh = lOMesh;
            lODoor.AddComponent<MeshRenderer>().sharedMaterial = mODungeonMaterial;
            lODoor.AddComponent<MeshCollider>().sharedMesh = lOMesh;

            DoorMover lOMover = lODoor.AddComponent<DoorMover>();
            lOMover.RotationPoint = (Quaternion.Euler(0f, pOObject.Heading * 45f, 0f) * new Vector3(-DoorHingeOffset, 0f, 0f)) + lOTranslation;
            lOMover.DoorDirection = pOObject.DoorDirection;
            lOMover.StartsOpen = fIsOpenDoorId(pOObject.ID);

            lODoor.AddComponent<UWDoorLock>();

            fAttachDoorHealth(lODoor, pOObject);

            fAddClosedDoorBlocker(xPos, zPos, pOObject, lfBottom, lfHeight, lfHalfWidth, pOParent, lOMover);

            UWEntityInfo lOInfo = lODoor.AddComponent<UWEntityInfo>();
            lOInfo.ObjectData = pOObject;
            lOInfo.IsDoor = true;
            lOInfo.Description = fGetDoorDescription(pOObject);

            fSpawnDoorFrames(xPos, zPos, pOTile, pOObject, lfBottom, lfHeight, pOParent, lOMover);

            SpawnedCount++;
        }

        private static void fAddQuad(System.Collections.Generic.List<Vector3> pOVertices, System.Collections.Generic.List<Vector2> pOUVs, System.Collections.Generic.List<int> piTriangles,
            Vector3 pOA, Vector3 pOB, Vector3 pOC, Vector3 pOD,
            Vector2 pOUvA, Vector2 pOUvB, Vector2 pOUvC, Vector2 pOUvD)
        {
            int liBase = pOVertices.Count;

            pOVertices.Add(pOA);
            pOVertices.Add(pOB);
            pOVertices.Add(pOC);
            pOVertices.Add(pOD);

            pOUVs.Add(pOUvA);
            pOUVs.Add(pOUvB);
            pOUVs.Add(pOUvC);
            pOUVs.Add(pOUvD);

            piTriangles.Add(liBase);
            piTriangles.Add(liBase + 1);
            piTriangles.Add(liBase + 2);
            piTriangles.Add(liBase);
            piTriangles.Add(liBase + 2);
            piTriangles.Add(liBase + 3);
        }

        /// <summary>Face whose four corners point to the same texture point - i.e. solid-coloured.</summary>
        private static void fAddSolidQuad(System.Collections.Generic.List<Vector3> pOVertices, System.Collections.Generic.List<Vector2> pOUVs, System.Collections.Generic.List<int> piTriangles,
            Vector2 pOUv, Vector3 pOA, Vector3 pOB, Vector3 pOC, Vector3 pOD)
        {
            fAddQuad(pOVertices, pOUVs, piTriangles, pOA, pOB, pOC, pOD, pOUv, pOUv, pOUv, pOUv);
        }


        /// <summary>
        /// The raw description of a door, with the "_" between article and name.
        ///
        /// Until 2026-09-04 the condition word was additionally inserted here: the "_" was replaced by the
        /// door's quality. That was redundant ever since Interaction does the same when
        /// LOOKING - and that is where it belongs, because the condition changes with
        /// every hit, whereas here a value from spawn time gets frozen.
        ///
        /// Worse still: because the "_" was used up in the process, the second insertion
        /// found no separator any more and prepended its word. The user read in the game
        /// "You see massive an sturdy open door."
        /// </summary>
        private string fGetDoorDescription(UWObject pOObject)
        {
            return fGetDescription(pOObject.ID + 1);
        }

        /// <summary>
        /// Frame left, right and above the door. They use the tile's wall texture
        /// and thus the geometry's Texture2DArray, not the object atlas.
        ///
        /// In the original the narrow sides do not show the wall texture but are
        /// solid-coloured - so all their corners are placed on the same texture point.
        ///
        /// The three parts are coupled to the door (pIDoor) and only block while it is closed -
        /// see the end of this method.
        /// </summary>
        private void fSpawnDoorFrames(int xPos, int zPos, UWTile pOTile, UWObject pOObject, float pfBottom, float pfDoorHeight, Transform pOParent, IUsableDoor pIDoor)
        {
            float lfTextureShift = UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.DoorFrameTextureShift : 0.5f;

            // The door plane for the painter's partition of the tile's objects (UWOwnTile.ClearDoors).
            bool lbAcrossX = pOObject.Heading == 2 || pOObject.Heading == 6;
            Vector3 lODoorAt = fGetDoorTranslation(xPos, zPos, pOObject);

            UWOwnTile.RegisterDoor(xPos, zPos, lbAcrossX, lbAcrossX ? lODoorAt.x : lODoorAt.z);

            if (mODungeonMaterial == null)
                return;

            float lfTop = pfBottom + pfDoorHeight;
            float lfCeiling = UWLevelMeshBuilder.CeilingHeight;
            const float lfDepth = 1f;

            // Frame narrow sides and lintel underside: in the original solid dark grey
            // (#171717, determined by screenshot comparison - see
            // RevealPaletteIndex), not the wall texture and not the
            // 0xEB colour of the door leaf.
            Collider lORight = fSpawnFrame(xPos, zPos, pOTile, pOObject, "DoorFrame right", pOParent, lfTextureShift, new FrameQuad[]
            {
                FrameQuad.Textured(new Vector3(-16f, pfBottom, -lfDepth), new Vector3(-32f, pfBottom, -lfDepth), new Vector3(-32f, lfTop, -lfDepth), new Vector3(-16f, lfTop, -lfDepth)),
                FrameQuad.Textured(new Vector3(-32f, pfBottom, lfDepth), new Vector3(-16f, pfBottom, lfDepth), new Vector3(-16f, lfTop, lfDepth), new Vector3(-32f, lfTop, lfDepth)),
                FrameQuad.Solid(new Vector3(-16f, pfBottom, lfDepth), new Vector3(-16f, pfBottom, -lfDepth), new Vector3(-16f, lfTop, -lfDepth), new Vector3(-16f, lfTop, lfDepth))
            });

            // Left frame: x from 16 to 32
            Collider lOLeft = fSpawnFrame(xPos, zPos, pOTile, pOObject, "DoorFrame left", pOParent, lfTextureShift, new FrameQuad[]
            {
                FrameQuad.Textured(new Vector3(32f, pfBottom, -lfDepth), new Vector3(16f, pfBottom, -lfDepth), new Vector3(16f, lfTop, -lfDepth), new Vector3(32f, lfTop, -lfDepth)),
                FrameQuad.Textured(new Vector3(16f, pfBottom, lfDepth), new Vector3(32f, pfBottom, lfDepth), new Vector3(32f, lfTop, lfDepth), new Vector3(16f, lfTop, lfDepth)),
                FrameQuad.Solid(new Vector3(16f, pfBottom, -lfDepth), new Vector3(16f, pfBottom, lfDepth), new Vector3(16f, lfTop, lfDepth), new Vector3(16f, lfTop, -lfDepth))
            });

            // Lintel above the door
            Collider lOLintel = fSpawnFrame(xPos, zPos, pOTile, pOObject, "DoorLintel", pOParent, lfTextureShift, new FrameQuad[]
            {
                FrameQuad.Textured(new Vector3(32f, lfTop, -lfDepth), new Vector3(-32f, lfTop, -lfDepth), new Vector3(-32f, lfCeiling, -lfDepth), new Vector3(32f, lfCeiling, -lfDepth)),
                FrameQuad.Textured(new Vector3(-32f, lfTop, lfDepth), new Vector3(32f, lfTop, lfDepth), new Vector3(32f, lfCeiling, lfDepth), new Vector3(-32f, lfCeiling, lfDepth)),
                FrameQuad.Solid(new Vector3(-32f, lfTop, -lfDepth), new Vector3(32f, lfTop, -lfDepth), new Vector3(32f, lfTop, lfDepth), new Vector3(-32f, lfTop, lfDepth))
            });

            // In the original frames have no collision of their own. They belong to the door object, and
            // its collision block sits 24 higher when the door is open - just above the head of the
            // character (see UWWorldSync.OpenDoorRaise). So you walk through under an open door including its frame,
            // but cannot get past a closed one (per user, 2026-09-11).
            if (pIDoor != null)
            {
                pIDoor.AddCoupledCollider(lORight);
                pIDoor.AddCoupledCollider(lOLeft);
                pIDoor.AddCoupledCollider(lOLintel);

                // A missile on the frame hits the DOOR (UWEntityInfo.PartOf).
                UWEntityInfo lODoorInfo = pIDoor is Component lODoorComponent
                    ? lODoorComponent.GetComponent<UWEntityInfo>()
                    : null;

                foreach (Collider lOPart in new[] { lORight, lOLeft, lOLintel })
                {
                    UWEntityInfo lOPartInfo = lOPart != null ? lOPart.GetComponent<UWEntityInfo>() : null;

                    if (lOPartInfo != null)
                        lOPartInfo.PartOf = lODoorInfo;
                }
            }
        }

        /// <summary>One face of a frame part, either textured or the solid
        /// notch/reveal colour (see RevealPaletteIndex).</summary>
        private struct FrameQuad
        {
            public Vector3 A;
            public Vector3 B;
            public Vector3 C;
            public Vector3 D;
            public bool IsSolid;

            public static FrameQuad Textured(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                FrameQuad lOResult = new FrameQuad();
                lOResult.A = a; lOResult.B = b; lOResult.C = c; lOResult.D = d;
                lOResult.IsSolid = false;
                return lOResult;
            }

            public static FrameQuad Solid(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                FrameQuad lOResult = new FrameQuad();
                lOResult.A = a; lOResult.B = b; lOResult.C = c; lOResult.D = d;
                lOResult.IsSolid = true;
                return lOResult;
            }
        }

        private Collider fSpawnFrame(int xPos, int zPos, UWTile pOTile, UWObject pOObject, string psName, Transform pOParent, float pfTextureShift, FrameQuad[] pOQuads)
        {
            GameObject lOFrame = new GameObject(psName);
            lOFrame.transform.SetParent(pOParent, false);
            lOFrame.transform.position = fGetDoorTranslation(xPos, zPos, pOObject);
            lOFrame.transform.Rotate(Vector3.up, pOObject.Heading * 45f);

            List<Vector3> lOVertices = new List<Vector3>();
            List<Vector2> lOUVs = new List<Vector2>();
            List<bool> lOIsSolid = new List<bool>();
            List<int> liTriangles = new List<int>();

            for (int i = 0; i < pOQuads.Length; i++)
            {
                FrameQuad lOQuad = pOQuads[i];

                fAddQuad(lOVertices, lOUVs, liTriangles, lOQuad.A, lOQuad.B, lOQuad.C, lOQuad.D,
                    fGetWallUv(lOQuad.A, pfTextureShift), fGetWallUv(lOQuad.B, pfTextureShift), fGetWallUv(lOQuad.C, pfTextureShift), fGetWallUv(lOQuad.D, pfTextureShift));

                while (lOIsSolid.Count < lOVertices.Count)
                    lOIsSolid.Add(lOQuad.IsSolid);
            }

            // Solid-colour corners point to the fixed notch/reveal slice instead of the
            // tile's wall texture. Without loaded .exe palette data (miRevealSlice == -1)
            // it falls back to the wall texture instead of showing no face at all.
            int liRevealSlice = miRevealSlice >= 0 ? miRevealSlice : pOTile.TextureWall;

            List<Vector3> lOTexCoords = new List<Vector3>(lOUVs.Count);

            for (int i = 0; i < lOUVs.Count; i++)
            {
                float lfSlice = lOIsSolid[i] ? liRevealSlice : pOTile.TextureWall;
                Vector2 lOUv = lOIsSolid[i] ? new Vector2(0.5f, 0.5f) : lOUVs[i];
                lOTexCoords.Add(new Vector3(lOUv.x, lOUv.y, lfSlice));
            }

            Mesh lOMesh = new Mesh();
            lOMesh.name = psName;
            lOMesh.SetVertices(lOVertices);
            lOMesh.SetTriangles(liTriangles, 0);
            lOMesh.SetUVs(0, lOTexCoords);
            lOMesh.RecalculateBounds();
            lOMesh.RecalculateNormals();

            lOFrame.AddComponent<MeshFilter>().sharedMesh = lOMesh;
            lOFrame.AddComponent<MeshRenderer>().sharedMaterial = mODungeonMaterial;
            MeshCollider lOCollider = lOFrame.AddComponent<MeshCollider>();
            lOCollider.sharedMesh = lOMesh;

            UWEntityInfo lOInfo = lOFrame.AddComponent<UWEntityInfo>();
            lOInfo.TileData = pOTile;
            lOInfo.Description = fGetTextureDescription(pOTile.TextureWall + 1);

            return lOCollider;
        }

        /// <summary>Wall textures tile over 64 units, as with the dungeon geometry. pfShift moves
        /// the texture sideways by that many tiles (see UWSettings.DoorFrameTextureShift).</summary>
        private static Vector2 fGetWallUv(Vector3 pOVertex, float pfShift = 0f)
        {
            return new Vector2((pOVertex.x / 64f) + pfShift, pOVertex.y / 64f);
        }


        private string fGetTextureDescription(int piIndex)
        {
            try
            {
                return mOData.GetTextureDescription(piIndex);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>Finished display text of a wall inscription: lead-in ("The writing
        /// reads: ") plus the actual text, both from string block 8 - see
        /// UWObjectMechanics.GetWritingPrefixIndex/GetWritingTextIndex. Trailing
        /// line breaks are dropped, inner ones stay (some texts are multi-line).</summary>
        private string fBuildWritingText(UWObject pOObject)
        {
            try
            {
                string lsPrefix = mOData.GetReadableText(UWObjectMechanics.GetWritingPrefixIndex(pOObject)) ?? string.Empty;
                string lsText = mOData.GetReadableText(UWObjectMechanics.GetWritingTextIndex(pOObject)) ?? string.Empty;

                return (lsPrefix.TrimEnd('\r', '\n') + lsText).TrimEnd('\r', '\n');
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Burning light sources get a point light. There are none in the nine levels of UW1
        /// - all light comes from the player's torch - but the
        /// case belongs here as soon as something burning is dropped.
        /// </summary>
        private void fAddLightIfSource(GameObject pOHost, UWObject pOObject, float pfHeight)
        {
            if (mOData == null || mOData.ObjectProperties == null)
                return;

            int liBrightness = mOData.ObjectProperties.GetLightBrightness(pOObject.ID);

            if (liBrightness <= 0)
                return;

            GameObject lOLightObject = new GameObject("Light");
            lOLightObject.transform.SetParent(pOHost.transform, false);
            lOLightObject.transform.localPosition = new Vector3(0f, pfHeight * 0.5f, 0f);

            Light lOLight = lOLightObject.AddComponent<Light>();
            lOLight.type = LightType.Point;
            lOLight.color = new Color(1f, 0.74f, 0.45f);
            lOLight.range = liBrightness * LightRangePerBrightness;
            lOLight.intensity = LightBrightnessAtOneTile * TileSpacing * TileSpacing;
            lOLight.shadows = LightShadows.None;
        }
    }
}
