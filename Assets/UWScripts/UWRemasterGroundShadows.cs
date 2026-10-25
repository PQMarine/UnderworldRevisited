using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// Soft ground shadows under creatures and items, only in the Remastered render mode
/// (per user, 2026-09-13: "carry on with the ground shadows"). Item 3 of the planned
/// effects in Todo.md.
///
/// WHY: the sprites stand there without ground contact and seem to float slightly. Real
/// shadows of billboards look odd because the image turns with the view - a
/// soft blob underneath, on the other hand, anchors them immediately.
///
/// WHICH OBJECTS: everything standing in the world as a sprite (shader UW/Billboard), except the
/// light sources (UWLightSourceTable) - a campfire casting a dark blob would be
/// wrong - and what flies (UWProjectileFlight). Wall decorations (UW/Decal), doors and 3D models get none.
///
/// HOW BIG: as wide as the collider in plan view. For items that is the
/// image width, for creatures the radius from COMOBJ.DAT (UWObjectSpawner.
/// fApplyCritterFootprint) - the image width of a goblin would be more than a tile.
///
/// Both are the one ground-shadow rule since cleanup step 6.4 (2026-10-24): UWGroundShadowRule,
/// asked through UWGroundShadowCasters, shared with the palette path's ground shadows.
///
/// The world changes during play: creatures appear, items are
/// dropped. So there is a regular check for who does not have a blob yet. The blob is attached
/// as a child of the object, so it moves along and disappears with it.
///
/// TOGETHER WITH THE AO: the sprites write depth, so the screen-space AO already darkens the ground
/// around their feet a little (per user, 2026-09-13). The blob complements that and is
/// therefore set up to be subtle.
///
/// A child of the Remastered path since cleanup step 6.5 (IUWRemasterFeature, 2026-10-24): ticked
/// while it is in force, cleared when it goes; until then a component of its own that asked the
/// render mode every frame.
/// </summary>
public sealed class UWRemasterGroundShadows : IUWRemasterFeature
{
    private const string ShadowShaderName = "UW/GroundShadow";

    private const string ShadowName = "Remastered Ground Shadow";

    /// <summary>How often to look for new objects, in seconds.</summary>
    private const float ScanInterval = 1f;

    /// <summary>Above the ground, so the blob does not disappear into it.</summary>
    private const float Lift = 0.3f;

    private readonly UWRenderPathHost mOHost;

    private UWLevelLoader mOLevelLoader;

    /// <summary>The shader is missing: no ground shadows for this scene (the component switched
    /// itself off until 2026-10-24).</summary>
    private bool mbMissingShader;

    private readonly List<UWGroundShadowMarker> mOShadows = new List<UWGroundShadowMarker>();

    private Material mOMaterial;

    private Mesh mOQuad;

    private float mfNextScan;

    private float mfAppliedSize = -1f;

    public UWRemasterGroundShadows(UWRenderPathHost pOHost)
    {
        mOHost = pOHost;
    }

    public void Tick(UnderworldRevisited.UWSettings pOSettings)
    {
        mOLevelLoader = mOHost.LevelLoader;

        UnderworldRevisited.UWSettings lOSettings = pOSettings;

        if (mbMissingShader || mOLevelLoader == null || lOSettings == null)
            return;

        bool lbWanted = lOSettings.RemasterGroundShadow > 0f;

        if (!lbWanted)
        {
            if (mOShadows.Count > 0)
                fClear();

            return;
        }

        if (!fEnsureResources())
            return;

        mOMaterial.SetFloat(UWShaderIds.Strength, lOSettings.RemasterGroundShadow);

        if (!Mathf.Approximately(mfAppliedSize, lOSettings.RemasterGroundShadowSize))
        {
            mfAppliedSize = lOSettings.RemasterGroundShadowSize;
            fResizeAll();
        }

        if (Time.unscaledTime < mfNextScan)
            return;

        mfNextScan = Time.unscaledTime + ScanInterval;

        fScan();
    }

    private void fScan()
    {
        // Destroyed objects took their blob with them.
        mOShadows.RemoveAll(lOMarker => lOMarker == null);

        UWEntityInfo[] lOEntities = mOLevelLoader.GetWorldEntities();

        for (int liAt = 0; liAt < lOEntities.Length; liAt++)
        {
            UWEntityInfo lOEntity = lOEntities[liAt];

            UWGroundShadowMarker lOMarker = TryAddShadow(lOEntities[liAt], mOMaterial, mOQuad);

            if (lOMarker != null)
                mOShadows.Add(lOMarker);
        }
    }

    /// <summary>
    /// Places a ground shadow under a world object, if it qualifies for one and does not have
    /// one yet. Public since a render test tool took the same path (removed before the release).
    /// </summary>
    public static UWGroundShadowMarker TryAddShadow(UWEntityInfo pOEntity, Material pOMaterial, Mesh pOQuad)
    {
        if (pOEntity == null || pOEntity.ObjectData == null)
            return null;

        if (pOEntity.GetComponentInChildren<UWGroundShadowMarker>(true) != null)
            return null;

        // Not the light sources, nothing in flight - spell missiles, thrown things, arrows (per
        // user, 2026-10-04: the blob flew along under them; UWGroundShadowRule), only sprites.
        float lfWidth;

        if (!UWGroundShadowCasters.TryGetWidth(pOEntity, false, out lfWidth))
            return null;

        return fAddShadow(pOEntity.transform, lfWidth, pOMaterial, pOQuad);
    }

    private static UWGroundShadowMarker fAddShadow(Transform pOParent, float pfWidth, Material pOMaterial, Mesh pOQuad)
    {
        GameObject lOObject = new GameObject(ShadowName);

        lOObject.layer = pOParent.gameObject.layer;
        lOObject.transform.SetParent(pOParent, false);
        lOObject.transform.localRotation = Quaternion.identity;

        lOObject.AddComponent<MeshFilter>().sharedMesh = pOQuad;

        MeshRenderer lORenderer = lOObject.AddComponent<MeshRenderer>();

        lORenderer.sharedMaterial = pOMaterial;
        lORenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lORenderer.receiveShadows = false;

        UWGroundShadowMarker lOMarker = lOObject.AddComponent<UWGroundShadowMarker>();

        lOMarker.Width = pfWidth;

        fPlace(lOMarker);

        return lOMarker;
    }

    /// <summary>Sets size and height - divided by the object's world scale, so that
    /// the blob is correct in world units.</summary>
    private static void fPlace(UWGroundShadowMarker pOMarker)
    {
        Transform lOTransform = pOMarker.transform;
        Vector3 lOParentScale = lOTransform.parent != null ? lOTransform.parent.lossyScale : Vector3.one;
        float lfSize = UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.RemasterGroundShadowSize : 1f;
        float lfDiameter = UWGroundShadowRule.RemasterDiameter(pOMarker.Width, lfSize);

        lOTransform.localScale = new Vector3(
            lfDiameter / Mathf.Max(0.0001f, Mathf.Abs(lOParentScale.x)),
            1f,
            lfDiameter / Mathf.Max(0.0001f, Mathf.Abs(lOParentScale.z)));

        lOTransform.localPosition = new Vector3(0f, Lift / Mathf.Max(0.0001f, Mathf.Abs(lOParentScale.y)), 0f);
    }

    private void fResizeAll()
    {
        mOShadows.RemoveAll(lOMarker => lOMarker == null);

        foreach (UWGroundShadowMarker lOMarker in mOShadows)
            fPlace(lOMarker);
    }

    private void fClear()
    {
        foreach (UWGroundShadowMarker lOMarker in mOShadows)
        {
            if (lOMarker != null)
                Object.Destroy(lOMarker.gameObject);
        }

        mOShadows.Clear();
        mfNextScan = 0f;
    }

    private bool fEnsureResources()
    {
        if (mOMaterial == null)
        {
            Shader lOShader = Shader.Find(ShadowShaderName);

            if (lOShader == null)
            {
                Debug.LogError("Shader " + ShadowShaderName + " not found - no ground shadows.");
                mbMissingShader = true;
                return false;
            }

            mOMaterial = CreateMaterial(lOShader);
        }

        if (mOQuad == null)
            mOQuad = CreateQuad();

        return true;
    }

    /// <summary>Remastered goes: every blob goes.</summary>
    public void Clear()
    {
        if (mOShadows.Count > 0)
            fClear();
    }

    /// <summary>The scene goes: the blobs go with their objects.</summary>
    public void Dispose()
    {
    }

    public static Material CreateMaterial(Shader pOShader)
    {
        return new Material(pOShader) { name = ShadowName };
    }

    /// <summary>A flat quad with an edge length of one unit in the ground plane.</summary>
    public static Mesh CreateQuad()
    {
        Mesh lOQuad = new Mesh { name = ShadowName };

        lOQuad.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f)
        };
        lOQuad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        lOQuad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        lOQuad.RecalculateNormals();
        lOQuad.RecalculateBounds();

        return lOQuad;
    }
}
