using System.Collections.Generic;
using UnityEngine;

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
/// light sources from UWRemasterLights - a campfire casting a dark blob would be
/// wrong. Wall decorations (UW/Decal), doors and 3D models get none.
///
/// HOW BIG: as wide as the collider in plan view. For items that is the
/// image width, for creatures the radius from COMOBJ.DAT (UWObjectSpawner.
/// fApplyCritterFootprint) - the image width of a goblin would be more than a tile.
///
/// The world changes during play: creatures appear, items are
/// dropped. So there is a regular check for who does not have a blob yet. The blob is attached
/// as a child of the object, so it moves along and disappears with it.
///
/// TOGETHER WITH THE AO: the sprites write depth, so the screen-space AO already darkens the ground
/// around their feet a little (per user, 2026-09-13). The blob complements that and is
/// therefore set up to be subtle.
/// </summary>
public class UWRemasterGroundShadows : MonoBehaviour
{
    private const string BillboardShaderName = "UW/Billboard";

    private const string ShadowShaderName = "UW/GroundShadow";

    private const string ShadowName = "Remastered Ground Shadow";

    /// <summary>How often to look for new objects, in seconds.</summary>
    private const float ScanInterval = 1f;

    /// <summary>Above the ground, so the blob does not disappear into it.</summary>
    private const float Lift = 0.3f;

    /// <summary>Smallest diameter - a key or a coin would otherwise get a
    /// barely visible dot.</summary>
    private const float MinDiameter = 10f;

    private UWLevelLoader mOLevelLoader;

    private readonly List<UWGroundShadowMarker> mOShadows = new List<UWGroundShadowMarker>();

    private Material mOMaterial;

    private Mesh mOQuad;

    private float mfNextScan;

    private float mfAppliedSize = -1f;

    private void Update()
    {
        if (mOLevelLoader == null)
            mOLevelLoader = GetComponent<UWLevelLoader>();

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        if (mOLevelLoader == null || lOSettings == null)
            return;

        bool lbWanted = lOSettings.IsRemasteredActive
            && lOSettings.RemasterGroundShadow > 0f;

        if (!lbWanted)
        {
            if (mOShadows.Count > 0)
                fClear();

            return;
        }

        if (!fEnsureResources())
            return;

        mOMaterial.SetFloat("_Strength", lOSettings.RemasterGroundShadow);

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

        if (UWRemasterLights.IsLightSource(pOEntity.ObjectData.ID))
            return null;

        MeshRenderer lORenderer = pOEntity.GetComponent<MeshRenderer>();

        if (lORenderer == null || lORenderer.sharedMaterial == null
            || lORenderer.sharedMaterial.shader == null
            || lORenderer.sharedMaterial.shader.name != BillboardShaderName)
            return null;

        BoxCollider lOCollider = pOEntity.GetComponent<BoxCollider>();
        float lfWidth = lOCollider != null ? Mathf.Max(lOCollider.size.x, lOCollider.size.z) : 16f;

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
        float lfDiameter = Mathf.Max(MinDiameter, pOMarker.Width * lfSize);

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
                Destroy(lOMarker.gameObject);
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
                enabled = false;
                return false;
            }

            mOMaterial = CreateMaterial(lOShader);
        }

        if (mOQuad == null)
            mOQuad = CreateQuad();

        return true;
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
