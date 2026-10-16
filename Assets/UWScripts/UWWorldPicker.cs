using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// ONE MEASUREMENT OF THE WORLD ALONG A RAY, shared by looking, using, taking and the target
/// spells (the cleanup plan's 7.3 UWWorldPicker, pulled forward from phase 5 on 2026-10-12; it
/// fixes B18, the three crosshair definitions). Until then Interaction ran three raycasts with
/// three filters of their own - fRaycastPastEffects for the look and use, TryGetPickupTarget
/// for taking, both again for a target spell - and the pointer's press measured the same ray
/// twice.
///
/// THE ORIGINAL picks by what is drawn under the pointer. So every hit along the ray is first
/// sifted the same way: the player's own body is no target, a model counts only where the ray
/// crosses one of its triangles (its collider is a box widened to its COMOBJ radius), a sprite
/// only where it meets an opaque pixel of the picture as the billboard shader draws it, and a
/// pure logic volume (a move trigger, a level transition: a trigger body without an entity) is
/// looked and reached through. What is left is classified once (HitKind) and read by policy:
///
///   Frontmost  looking, using, the pointer's press: the first thing, but a spell effect or
///              another animation (0x1C0-0x1CF, not the fountain's water and the silver tree,
///              which are themselves what is meant) is looked past when an object or creature
///              lies behind it - the sparks above the Slasher of Veils are almost transparent -
///              and counts itself when only wall or floor follows.
///   Pickable   taking: the first PICKABLE thing; a blood pool or a bone pile does not stop
///              the search (a creature's loot often lies under them), nor does an effect; a
///              wall, a door, a creature, a bridge, a solid thing (barrel, chest, boulder) and
///              the two animated solids do - one does not take through them.
///   SpellTarget a target spell: the pickable thing if there is one, else the frontmost.
///
/// Logic volumes used to stop the taking (the search took the trigger box for a wall); since
/// the measurement is one, they are reached through as they are looked through.
/// </summary>
public static class UWWorldPicker
{
    /// <summary>How far a look reaches, in world units; using and taking are limited afterwards
    /// on the tile grid (UWReachRules).</summary>
    public const float Range = 4000f;

    public enum HitKind
    {
        /// <summary>Wall, floor or ceiling: a chunk without an entity.</summary>
        Geometry,

        Door,

        Creature,

        /// <summary>A bridge is a floor: nothing is taken through its plate.</summary>
        Bridge,

        Pickable,

        /// <summary>A spell effect or another animation that is looked and reached past.</summary>
        PassThroughEffect,

        /// <summary>The fountain's water and the silver tree: animated, but themselves what is
        /// meant - nothing behind them is taken.</summary>
        AnimatedSolid,

        /// <summary>A solid thing that cannot be taken: barrel, chest, table, boulder.</summary>
        Solid,

        /// <summary>A thing one walks over and cannot take: a blood pool, a bone pile.</summary>
        WalkOver
    }

    public struct Hit
    {
        public RaycastHit Raw;

        /// <summary>The entity, null for geometry.</summary>
        public UWEntityInfo Entity;

        public HitKind Kind;
    }

    /// <summary>What one ray found, read by policy.</summary>
    public sealed class Measurement
    {
        public Ray Ray;

        /// <summary>The sifted hits, nearest first.</summary>
        public readonly List<Hit> Hits = new List<Hit>();

        /// <summary>The frontmost rule found something - FrontmostHit is its raycast hit, for
        /// the sight band (Interaction.fIsWithinSight) and the chunk's description.</summary>
        public bool HasFrontmost;

        public RaycastHit FrontmostHit;

        /// <summary>The frontmost thing with an entity; null for wall, floor or nothing.</summary>
        public UWEntityInfo Frontmost;

        /// <summary>The first pickable thing along the ray, or null.</summary>
        public UWEntityInfo Pickable;

        /// <summary>A target spell's target: the pickable thing, else the frontmost.</summary>
        public UWEntityInfo SpellTarget => Pickable != null ? Pickable : Frontmost;
    }

    /// <summary>The crosshair of the modern scheme and of the action bar: the middle of the
    /// game camera's view, which in Classic Wide is not the middle of the screen (B18).</summary>
    public static Ray CrosshairRay()
    {
        return CrosshairRay(Camera.main);
    }

    public static Ray CrosshairRay(Camera pOCamera)
    {
        return pOCamera != null
            ? pOCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : new Ray(Vector3.zero, Vector3.forward);
    }

    /// <summary>Measures the ray over the look's range.</summary>
    public static Measurement Measure(Ray pORay)
    {
        return Measure(pORay, Range);
    }

    public static Measurement Measure(Ray pORay, float pfRange)
    {
        Measurement lOResult = new Measurement { Ray = pORay };
        RaycastHit[] lOHits = Physics.RaycastAll(pORay, pfRange);

        System.Array.Sort(lOHits, (lOA, lOB) => lOA.distance.CompareTo(lOB.distance));

        foreach (RaycastHit lOHit in lOHits)
        {
            if (lOHit.collider == null)
                continue;

            UWEntityInfo lOEntity = lOHit.collider.GetComponentInParent<UWEntityInfo>();

            // PURE LOGIC VOLUMES (move triggers, level transitions) are invisible and carry no
            // object - you look through them. On the tile of the Slasher of Veils on level 8
            // there is a move trigger; its box intercepted the look, from outside the Slasher
            // could not be looked at at all (per user, 2026-09-14).
            if (lOHit.collider.isTrigger && lOEntity == null)
                continue;

            if (fIsPlayerBody(lOHit) || fMissesModelShape(pORay, lOHit) || fMissesSpritePixels(pORay, lOHit))
                continue;

            lOResult.Hits.Add(new Hit { Raw = lOHit, Entity = lOEntity, Kind = fClassify(lOHit, lOEntity) });
        }

        fReadFrontmost(lOResult);
        fReadPickable(lOResult);

        return lOResult;
    }

    private static HitKind fClassify(RaycastHit pOHit, UWEntityInfo pOEntity)
    {
        // Wall, floor or ceiling - and a door's frame, which has an entity without an object.
        if (pOEntity == null || pOEntity.ObjectData == null)
            return HitKind.Geometry;

        int liId = pOEntity.ObjectData.ID;

        if (pOEntity.IsDoor)
            return HitKind.Door;

        if (UWObjectIds.IsCreature(liId))
            return HitKind.Creature;

        // A BRIDGE is a floor: the Wine of Compassion under the plate on level 6, 27/50, could
        // be taken through it (per user, 2026-09-29); in the original the plate is lifted first.
        if (liId == UWObjectIds.Bridge)
            return HitKind.Bridge;

        if (pOEntity.CanBePickedUp)
            return HitKind.Pickable;

        if (IsPassThroughEffect(liId))
            return HitKind.PassThroughEffect;

        // The silver tree and the fountain's water: one walks through them, but nothing behind
        // them can be taken (per user on the original, 2026-10-11).
        if (UWObjectIds.IsAnimated(liId))
            return HitKind.AnimatedSolid;

        // A SOLID THING that cannot be taken stops the taking too - a barrel, chest, table,
        // boulder: in the original the pointer picks what is drawn there, and an item behind a
        // barrel cannot be taken through it (per user, 2026-09-30). Only what one walks over (a
        // trigger body: blood, bones) lets the search on.
        return pOHit.collider.isTrigger ? HitKind.WalkOver : HitKind.Solid;
    }

    /// <summary>
    /// Looking and using take the first thing, except that an effect is looked past when an
    /// object or creature lies behind it: the sparks above the Slasher of Veils on level 8
    /// (0x1C7) are almost fully transparent, you hit the Slasher behind them; our collider box
    /// covers the whole image and intercepted looking and talking (per user, 2026-09-14). If
    /// only wall or floor lies behind the effect, the effect still counts.
    /// </summary>
    private static void fReadFrontmost(Measurement pOResult)
    {
        bool lbHaveEffect = false;
        RaycastHit lOEffect = default(RaycastHit);
        UWEntityInfo lOEffectEntity = null;

        foreach (Hit lOHit in pOResult.Hits)
        {
            if (lOHit.Kind == HitKind.PassThroughEffect)
            {
                if (!lbHaveEffect)
                {
                    lbHaveEffect = true;
                    lOEffect = lOHit.Raw;
                    lOEffectEntity = lOHit.Entity;
                }

                continue;
            }

            // Behind an effect only an object or creature counts, not a wall.
            if (lbHaveEffect && lOHit.Entity == null)
                break;

            pOResult.HasFrontmost = true;
            pOResult.FrontmostHit = lOHit.Raw;
            pOResult.Frontmost = lOHit.Entity;

            return;
        }

        if (lbHaveEffect)
        {
            pOResult.HasFrontmost = true;
            pOResult.FrontmostHit = lOEffect;
            pOResult.Frontmost = lOEffectEntity;
        }
    }

    /// <summary>
    /// Taking finds the first PICKABLE thing. NON-PICKABLE THINGS DO NOT STOP IT: a blood pool
    /// or a bone pile often lies exactly where a creature's loot lies, and used to hide it (per
    /// user, 2026-09-13: "non-pickable items must not prevent other items at the same position
    /// from being picked up"). Walls, doors, creatures, bridges, solid things and the animated
    /// solids still stop it - you do not reach through them.
    /// </summary>
    private static void fReadPickable(Measurement pOResult)
    {
        foreach (Hit lOHit in pOResult.Hits)
        {
            switch (lOHit.Kind)
            {
                case HitKind.Pickable:
                    pOResult.Pickable = lOHit.Entity;
                    return;

                case HitKind.PassThroughEffect:
                case HitKind.WalkOver:
                    continue;

                default:
                    return;
            }
        }
    }

    /// <summary>Last animation object.</summary>
    private const int AnimationLastId = 0x1CF;

    /// <summary>A spell effect or another animation (0x1C0-0x1CF) that the pointer looks and
    /// reaches past - not the fountain's water and not the silver tree, which are themselves
    /// what is meant (until 2026-10-11 the literal 459 stood for the tree, the string index -
    /// id plus one - so the test skipped the wrong id).</summary>
    public static bool IsPassThroughEffect(int piObjectId)
    {
        return piObjectId >= UWObjectClassProperties.AnimationFirstId && piObjectId <= AnimationLastId
            && piObjectId != UWObjectMechanics.FountainWaterObjectId && piObjectId != UWSilverTreeRules.TreeObjectId;
    }

    /// <summary>
    /// THE PLAYER'S OWN BODY IS NO TARGET (per user, 2026-10-01: looking down, items could hardly
    /// or not at all be taken). The pointer's ray starts at the eye, and looking down steeply it
    /// meets the body's capsule a few units in front of the camera first; the pickup search took
    /// it for a wall and stopped (traced in the player log). The original has no body in its way.
    /// </summary>
    private static bool fIsPlayerBody(RaycastHit pOHit)
    {
        return pOHit.collider != null && (pOHit.collider is CharacterController
            || pOHit.collider.GetComponentInParent<UWPlayerMovement>() != null);
    }

    /// <summary>
    /// A 3D MODEL IS AIMED AT BY ITS SHAPE, NOT ITS BOX (per user, 2026-09-30 with a screenshot:
    /// beside a barrel the bread could not be taken, "the barrel is always used"). A model's
    /// collider is a box round its mesh, widened to its COMOBJ.DAT radius for the motion (see
    /// UWObjectSpawner.fSpawn3DModel) - far larger than the barrel as drawn, and the original
    /// picks by what is drawn under the pointer. So a hit on a model's solid box counts only if
    /// the ray really crosses one of the model's triangles; otherwise the ray goes on.
    /// </summary>
    private static bool fMissesModelShape(Ray pORay, RaycastHit pOHit)
    {
        if (pOHit.collider == null || pOHit.collider.isTrigger || !(pOHit.collider is BoxCollider))
            return false;

        UWEntityInfo lOInfo = pOHit.collider.GetComponent<UWEntityInfo>();
        MeshFilter lOFilter = pOHit.collider.GetComponent<MeshFilter>();

        if (lOInfo == null || !lOInfo.IsModel || lOFilter == null || lOFilter.sharedMesh == null)
            return false;

        Mesh lOMesh = lOFilter.sharedMesh;

        if (!lOMesh.isReadable)
            return false;

        Transform lOTransform = lOFilter.transform;
        Vector3 lOOrigin = lOTransform.InverseTransformPoint(pORay.origin);
        Vector3 lODirection = lOTransform.InverseTransformDirection(pORay.direction);
        Vector3[] laVertices = lOMesh.vertices;

        for (int liSub = 0; liSub < lOMesh.subMeshCount; liSub++)
        {
            int[] laTriangles = lOMesh.GetTriangles(liSub);

            for (int liAt = 0; liAt + 2 < laTriangles.Length; liAt += 3)
            {
                if (fRayCrossesTriangle(lOOrigin, lODirection, laVertices[laTriangles[liAt]],
                    laVertices[laTriangles[liAt + 1]], laVertices[laTriangles[liAt + 2]]))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A SPRITE IS AIMED AT BY ITS PIXELS, NOT ITS BOX (per user, 2026-10-01: in front of the small
    /// boulder on level 4, 35/5, the rock hammer was always used on the blood stain beside it; the
    /// original takes the boulder). The original picks what is drawn under the pointer; our sprite
    /// carries a box the size of its whole picture, square in plan, and a low stain's box reaches
    /// over the thing next to it. So a hit on a sprite counts only where the ray meets an opaque
    /// pixel of the picture as the billboard shader draws it - parallel to the screen through the
    /// pivot - with the material's own alpha threshold.
    /// </summary>
    private static bool fMissesSpritePixels(Ray pORay, RaycastHit pOHit)
    {
        Collider lOCollider = pOHit.collider;

        if (lOCollider == null)
            return false;

        UWEntityInfo lOInfo = lOCollider.GetComponent<UWEntityInfo>();
        MeshFilter lOFilter = lOCollider.GetComponent<MeshFilter>();
        MeshRenderer lORenderer = lOCollider.GetComponent<MeshRenderer>();

        if (lOInfo == null || lOInfo.IsModel || lOFilter == null || lOFilter.sharedMesh == null
            || lORenderer == null || lORenderer.sharedMaterial == null)
            return false;

        Material lOMaterial = lORenderer.sharedMaterial;
        string lsShader = lOMaterial.shader != null ? lOMaterial.shader.name : string.Empty;

        if (lsShader != "UW/Billboard" && lsShader != "UW/BillboardPalette")
            return false;

        Texture2D lOTexture = lOMaterial.HasProperty("_MainTex") ? lOMaterial.mainTexture as Texture2D : null;

        if (lOTexture == null && lOMaterial.HasProperty("_IndexTex"))
            lOTexture = lOMaterial.GetTexture("_IndexTex") as Texture2D;

        Mesh lOMesh = lOFilter.sharedMesh;
        Camera lOCamera = Camera.main;

        if (lOTexture == null || !lOTexture.isReadable || !lOMesh.isReadable || lOMesh.vertexCount < 4 || lOCamera == null)
            return false;

        // The shader's frame: forward is the view matrix's third row, flattened.
        Vector3 lOToCamera = lOCamera.worldToCameraMatrix.GetRow(2);
        lOToCamera.y = 0f;

        Vector3 lOForward = (lOToCamera + new Vector3(0f, 0f, 1e-4f)).normalized;
        Vector3 lORight = Vector3.Cross(lOForward, Vector3.up).normalized;

        // The shader's _DepthBias moves only the depth since 2026-10-12, not the picture, so
        // the quad stands at the pivot.
        Vector3 lOPlane = lOCollider.transform.position;

        float lfFacing = Vector3.Dot(pORay.direction, lOForward);

        if (Mathf.Abs(lfFacing) < 1e-5f)
            return false;

        Vector3 lOPoint = pORay.origin + (pORay.direction * (Vector3.Dot(lOPlane - pORay.origin, lOForward) / lfFacing));
        float lfX = Vector3.Dot(lOPoint - lOPlane, lORight);
        float lfY = lOPoint.y - lOPlane.y;

        // The quad of UWObjectSpawner.fGetQuad: 0 bottom left, 1 bottom right, 3 top left.
        Vector3[] laVertices = lOMesh.vertices;
        Vector2[] laUvs = lOMesh.uv;

        if (laUvs == null || laUvs.Length < 4)
            return false;

        float lfWidth = laVertices[1].x - laVertices[0].x;
        float lfHeight = laVertices[3].y - laVertices[0].y;

        if (Mathf.Abs(lfWidth) < 1e-5f || Mathf.Abs(lfHeight) < 1e-5f)
            return false;

        float lfS = (lfX - laVertices[0].x) / lfWidth;
        float lfT = (lfY - laVertices[0].y) / lfHeight;

        if (lfS < 0f || lfS > 1f || lfT < 0f || lfT > 1f)
            return true;

        float lfU = Mathf.Lerp(laUvs[0].x, laUvs[1].x, lfS);
        float lfV = Mathf.Lerp(laUvs[0].y, laUvs[3].y, lfT);
        int liPixelX = Mathf.Clamp(Mathf.FloorToInt(lfU * lOTexture.width), 0, lOTexture.width - 1);
        int liPixelY = Mathf.Clamp(Mathf.FloorToInt(lfV * lOTexture.height), 0, lOTexture.height - 1);
        float lfCutoff = lOMaterial.HasProperty("_Cutoff") ? lOMaterial.GetFloat("_Cutoff") : 0.5f;

        return lOTexture.GetPixel(liPixelX, liPixelY).a < lfCutoff;
    }

    /// <summary>Moeller-Trumbore, both faces - a model's back face counts as well, the pointer
    /// can meet the inside of a barrel's rim.</summary>
    private static bool fRayCrossesTriangle(Vector3 pOOrigin, Vector3 pODirection, Vector3 pOA, Vector3 pOB, Vector3 pOC)
    {
        Vector3 lOEdge1 = pOB - pOA;
        Vector3 lOEdge2 = pOC - pOA;
        Vector3 lOP = Vector3.Cross(pODirection, lOEdge2);
        float lfDet = Vector3.Dot(lOEdge1, lOP);

        if (Mathf.Abs(lfDet) < 1e-6f)
            return false;

        float lfInv = 1f / lfDet;
        Vector3 lOT = pOOrigin - pOA;
        float lfU = Vector3.Dot(lOT, lOP) * lfInv;

        if (lfU < 0f || lfU > 1f)
            return false;

        Vector3 lOQ = Vector3.Cross(lOT, lOEdge1);
        float lfV = Vector3.Dot(pODirection, lOQ) * lfInv;

        if (lfV < 0f || lfU + lfV > 1f)
            return false;

        return Vector3.Dot(lOEdge2, lOQ) * lfInv > 0f;
    }
}
