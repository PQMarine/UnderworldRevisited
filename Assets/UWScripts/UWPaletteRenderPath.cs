using System.Collections.Generic;
using UnityEngine;
using UnderworldRevisited;
using UnderworldRevisited.Build;
using UWDataImport.UWData;

/// <summary>
/// THE PALETTE PATH (IUWRenderPath, cleanup step 6.5, 2026-10-24): until this step
/// UWPaletteRenderToggle, a component that switched itself on, plus the palette effects and the
/// sweep mask as two components kept across scenes, each asking the render mode every frame.
/// UWRenderPathHost switches and ticks it now; its children are the palette effects
/// (UWPaletteEffects) and the sweep mask (UWSweepMaskDriver), ticked only while it is in force
/// and cleared when it goes (Deactivate). What both paths read of the palette - the tables, the
/// hallucination, the dither - is UWPaletteCommon's and the host's.
///
/// The switch is the detail level of the Graphics menu and the options panel
/// (UWGraphicsDetail.SetLevel: Low is the palette path, every other level Remastered); the F6 key
/// that toggled it is gone since 2026-09-26 (per user).
///
/// THE PALETTE PATH IS THE DEFAULT - UWSettings.RenderMode is set to Palette,
/// and the host starts it by itself as soon as the level's atlas is there (TryStart).
///
/// Implemented as a material swap after the fact and not as a separate build path: the swap
/// is needed for the switch while the game runs anyway, and both paths share meshes, UVs and
/// slice numbers. A second build path would be double work with no gain.
///
/// WHAT GETS SWITCHED: everything that runs on the dungeon shader. That is the
/// geometry chunks and everything sharing their material - door frame narrow sides,
/// secret doors and the wall-textured faces of real 3D models (see UWObjectSpawner).
///
/// SPRITES AND CREATURES are switched as well: they run on UW/Billboard and
/// get the index version of their atlas (see UWObjectAtlasBuilder and
/// UWCritterAtlasBuilder, both from the same packing run as the colour atlas, so the UVs
/// remain valid unchanged). Inscriptions on walls (UW/Decal) get UW/DecalPalette the same way.
///
/// THE SOLID-COLOUR FACES of the 3D models (UWLevelLoader.ModelSolidMaterial) and the
/// portcullis (UWLevelLoader.PortcullisMaterial) get UW/ModelPalette: in the colour path
/// they carry their colour as vertex colour and multiply it with a white tile - an index
/// cannot be multiplied, so the palette version reads the index from the vertex instead.
///
/// The UI is not switched here: the icons always go through the palette anyway
/// (see UWIconPalette).
///
/// THE PLAYER LIGHT IS OFF in the palette path (see fSetSideEffects). The palette shader
/// calculates only from light level and distance, an additional URP light would take effect twice.
///
/// What TO EXPECT, so that bugs can be told apart from intent:
///
///   - hard brightness steps instead of a smooth falloff, in rings around the player. That is
///     correct; the original switches in sixteen steps via a table.
///   - a fixed view distance beyond which it turns black - three tiles without light, seven
///     with the brightest light source.
///   - no effect at all of wall torches on the walls. In the original they light up nothing.
///   - flickering on distant floors, because index textures cannot have mipmaps.
///   - water and lava keep moving, but now via the rotation table instead of
///     pre-baked images.
/// </summary>
public sealed class UWPaletteRenderPath : IUWRenderPath
{
    /// <summary>Names of the shaders whose material is swapped, and their
    /// palette version.</summary>
    private const string DungeonShaderName = "UW/Dungeon";

    private const string BillboardShaderName = "UW/Billboard";

    private const string PaletteBillboardShaderName = "UW/BillboardPalette";

    private const string PaletteModelShaderName = "UW/ModelPalette";

    private const string DecalShaderName = "UW/Decal";

    private const string PaletteDecalShaderName = "UW/DecalPalette";

    private readonly UWRenderPathHost mOHost;

    /// <summary>The world's palette indices; UWTextureCache owns the array (cleanup step 6.2,
    /// 2026-10-24 - until then this class built its own copy).</summary>
    private Texture2DArray mOIndexArray;

    private Material mOPaletteMaterial;

    private Material mOModelMaterial;

    private Material mOPortcullisMaterial;

    private readonly List<MeshRenderer> mORenderers = new List<MeshRenderer>();

    /// <summary>The original material list per renderer. A LIST, because the
    /// 3D models carry three materials - wall texture, atlas graphic and solid-colour
    /// faces - and swapping only the first would mean forgetting two thirds.</summary>
    private readonly List<Material[]> mOOriginals = new List<Material[]>();

    /// <summary>For each colour texture its index version. Fetched anew on every apply,
    /// because creature atlases only come into being when the first creature of their kind appears.
    /// </summary>
    private readonly Dictionary<Texture, Texture> mOIndexTextures = new Dictionary<Texture, Texture>();

    /// <summary>For each sprite material its palette version, so that not every
    /// apply creates new materials.</summary>
    private readonly Dictionary<Material, Material> mOPaletteMaterials = new Dictionary<Material, Material>();

    /// <summary>The palette renderer's own effects, ticked while this path is in force.</summary>
    private readonly UWPaletteEffects mOEffects = new UWPaletteEffects();

    /// <summary>The original's view sweep for the sprites, ticked while this path is in force.</summary>
    private readonly UWSweepMaskDriver mOSweepMask = new UWSweepMaskDriver();

    private bool mbActive;

    private int miAppliedLevel = -1;

    /// <summary>Number of creature atlases at the last swap - see Tick.</summary>
    private int miAppliedAtlasCount = -1;

    private bool mbStarted;

    public UWPaletteRenderPath(UWRenderPathHost pOHost)
    {
        mOHost = pOHost;
    }

    public UWSettings.RenderModeEnum Mode => UWSettings.RenderModeEnum.Palette;

    /// <summary>Whether the materials are swapped - the palette path is on.</summary>
    public bool IsApplied => mbActive;

    /// <summary>The palette XFER mixing reads the picture behind the translucent pixels, while the
    /// hallucination does not swap the palette (the lookups are built from palette 0).</summary>
    public bool WantsSceneColour => mbActive && mOHost.Common.HallucinationTable == null;

    /// <summary>The DEPTH texture the whole time the palette path is on: the sprites read the
    /// geometry behind them from it (UWPainterOrder.hlsl, UWPainterSpriteVisible).</summary>
    public bool WantsSceneDepth => mbActive;

    public bool WantsPostProcessing => false;

    /// <summary>The palette ambient occlusion reads URP's occlusion texture.</summary>
    public bool WantsOcclusion => UWPaletteEffects.AmbientOcclusionLevels > 0f;

    private UWLevelLoader fLoader => mOHost.LevelLoader;

    /// <summary>Switch on by itself at start (the render mode asks for the palette path). Only
    /// from the host's Update, because the level loader builds its materials and atlases in its
    /// own Start - before that there would be nothing to swap.</summary>
    public void TryStart()
    {
        if (mbStarted || mbActive)
            return;

        if (fLoader == null || fLoader.ObjectAtlas == null)
            return;

        mbStarted = true;

        fStart();
    }

    /// <summary>On the first switch-on the tables are built, afterwards they stay around - a
    /// switch then costs nothing more.</summary>
    public void Activate()
    {
        mbStarted = true;

        if (!mbActive)
            fStart();
    }

    private void fStart()
    {
        if (!mOHost.EnsureTables())
        {
            Debug.LogError("Render path: palette tables could not be built");
            return;
        }

        mbActive = true;
        miAppliedLevel = -1;

        fSetSideEffects(true);

        Debug.Log("Render path: Palette (SHADES.DAT and LIGHT.DAT)");
    }

    /// <summary>
    /// Off: the materials go back, the side effects too, and the effects and the sweep mask clear
    /// the globals only this path reads (UWPaletteEffects.Clear, UWSweepMaskDriver.Clear) - the
    /// user saw the light map, the normal array and the shadow list still set in a Remastered
    /// dump (2026-10-24).
    /// </summary>
    public void Deactivate()
    {
        if (mbActive)
        {
            fRestore();

            mbActive = false;

            fSetSideEffects(false);

            Debug.Log("Render path: URP lighting");
        }

        mbStarted = true;

        mOEffects.Clear();
        mOSweepMask.Clear();
    }

    /// <summary>
    /// What else besides the materials depends on the render path.
    ///
    /// POINT LIGHT: in the palette path there are no local light sources, the brightness
    /// comes only from light level and distance. An additional URP light would take effect
    /// twice. The light is set up anew at once (since 2026-10-24), so it is off from the first
    /// frame of the palette path and not only when the next light change comes.
    ///
    /// TEXTURE ANIMATION: UWAnimatedTextures copies pre-baked water and lava images
    /// into the slices of the COLOUR array every frame. The palette path does not use this array
    /// at all - it rotates the whole palette via a single number in the shader. The copying
    /// would be pure waste.
    /// </summary>
    private void fSetSideEffects(bool pbPalette)
    {
        UWLighting lOLighting = mOHost.Lighting;

        if (lOLighting != null)
        {
            lOLighting.SuppressPlayerLight = pbPalette;
            lOLighting.RefreshPlayerLight();
        }

        UWAnimatedTextures lOAnimator = mOHost.GetComponent<UWAnimatedTextures>();

        if (lOAnimator != null)
            lOAnimator.enabled = !pbPalette;
    }

    /// <summary>
    /// After a level change there are new chunks that carry the old material again.
    /// Only while the palette path is on - otherwise nothing is to be swapped.
    ///
    /// ADDITIONALLY for a new CREATURE KIND: its atlas only comes into being when the first
    /// creature of this kind appears (see UWCritterAnimator). If one joins after the swap
    /// - behind a secret wall just opened, for example -, the
    /// colour atlas -> index atlas mapping does not know its atlas yet, the swap fails
    /// silently, and exactly this creature stays on the old path. It stands out immediately,
    /// because the player light is off in the palette path: the creature then appears dark.
    /// </summary>
    public void Tick()
    {
        int liAtlasCount = UWCritterAnimator.AtlasCount;
        UWLevelLoader lOLoader = fLoader;

        if (mbActive && lOLoader != null
            && (lOLoader.CurrentLevelIndex != miAppliedLevel || liAtlasCount != miAppliedAtlasCount))
        {
            fApply();

            miAppliedLevel = lOLoader.CurrentLevelIndex;
            miAppliedAtlasCount = liAtlasCount;
        }
    }

    /// <summary>The effects and the sweep mask, after the frame moved the camera.</summary>
    public void LateTick()
    {
        mOEffects.Tick();
        mOSweepMask.Tick();
    }

    /// <summary>
    /// Forgets what was swapped last - the swap then runs over everything again on the next
    /// frame.
    ///
    /// Called after every rebuild of a level. Until 2026-09-06 the swap depended
    /// only on the LEVEL NUMBER: if you jumped with F7 to a tile on the SAME level,
    /// the loader rebuilt everything, but the number stayed the same - and the fresh geometry
    /// was left on the URP materials. It looked as if the jump had switched the
    /// render path (per user, jump to 7,40,20).
    /// </summary>
    public void OnLevelBuilt()
    {
        miAppliedLevel = -1;
        miAppliedAtlasCount = -1;
    }

    /// <summary>
    /// The world's palette indices from UWTextureCache, asked for until it hands them out (the
    /// cache builds the array again for another data set and destroys the old one).
    /// </summary>
    public bool EnsureIndexArray(UWDataImport.DataImport pOData)
    {
        if (mOIndexArray == null)
        {
            mOIndexArray = UWTextureCache.GetIndexArray(pOData);

            if (mOPaletteMaterial != null && mOIndexArray != null)
                mOPaletteMaterial.SetTexture(UWShaderIds.IndexArray, mOIndexArray);
        }

        return mOIndexArray != null;
    }

    /// <summary>The palette version of the dungeon material, made once.</summary>
    public bool EnsureMaterial()
    {
        if (mOPaletteMaterial == null)
            mOPaletteMaterial = UWLevelAssembler.CreateMaterial(mOIndexArray, "UW Dungeon (Palette)",
                UWSettings.RenderModeEnum.Palette);

        return mOPaletteMaterial != null;
    }

    /// <summary>Swaps the material on everything that has a palette version (see
    /// GetMaterial), and remembers what was on it before.</summary>
    private void fApply()
    {
        fRestore();
        fCollectIndexTextures();

        foreach (MeshRenderer lORenderer in mOHost.GetComponentsInChildren<MeshRenderer>(true))
            fSwapRenderer(lORenderer);
    }

    /// <summary>
    /// Puts an object created AFTER THE FACT onto the palette path.
    ///
    /// fApply runs once per level over everything present at that time. Whatever appears later -
    /// a dropped item, a hit effect, the remains of a creature,
    /// the secret door behind an opened wall - otherwise kept the URP materials
    /// and was the only thing lit by the player light. It looked as if exactly this
    /// object was still running on the old renderer, and that is exactly what it did (per user,
    /// 2026-09-06, at the secret door on level 3).
    ///
    /// It was only noticed now because before it only affected short-lived things: a
    /// blood splatter is visible for less than a second.
    ///
    /// While the palette path is off a no-op - nothing is to be swapped then.
    /// </summary>
    public void OnObjectSpawned(GameObject pOSpawned)
    {
        if (pOSpawned == null || !mbActive || mOPaletteMaterial == null)
            return;

        // Anew EVERY TIME: a freshly created creature can be the first of its kind, and
        // then its atlas has only just come into being. The mapping is then outdated,
        // even if it is not empty. The cost is two small caches.
        fCollectIndexTextures();

        foreach (MeshRenderer lORenderer in pOSpawned.GetComponentsInChildren<MeshRenderer>(true))
            fSwapRenderer(lORenderer);
    }

    /// <summary>Swaps the materials of ONE renderer and remembers what was on it
    /// before - so that a switch back to Remastered restores the old state.</summary>
    private void fSwapRenderer(MeshRenderer pORenderer)
    {
        if (pORenderer == null)
            return;

        Material[] lOMaterials = pORenderer.sharedMaterials;

        if (lOMaterials == null || lOMaterials.Length == 0)
            return;

        Material[] lOReplacements = new Material[lOMaterials.Length];
        bool lbAny = false;

        for (int liAt = 0; liAt < lOMaterials.Length; liAt++)
        {
            lOReplacements[liAt] = lOMaterials[liAt];

            Material lOSwapped = GetMaterial(lOMaterials[liAt]);

            if (lOSwapped == null)
                continue;

            lOReplacements[liAt] = lOSwapped;
            lbAny = true;
        }

        if (!lbAny)
            return;

        mORenderers.Add(pORenderer);
        mOOriginals.Add(lOMaterials);

        pORenderer.sharedMaterials = lOReplacements;
    }

    /// <summary>
    /// The palette version of a material, or null if there is none.
    ///
    /// The solid-colour model faces and the portcullis are checked FIRST: in the colour path
    /// they run on the same shader as the wall inscriptions (UW/Decal), so they differ only by
    /// the material itself (see UWLevelLoader.ModelSolidMaterial and PortcullisMaterial).
    /// </summary>
    public Material GetMaterial(Material pOMaterial)
    {
        if (pOMaterial == null || pOMaterial.shader == null)
            return null;

        UWLevelLoader lOLoader = fLoader;

        if (lOLoader != null && pOMaterial == lOLoader.ModelSolidMaterial)
            return fGetPaletteModel(ref mOModelMaterial, "UW Model Faces (Palette)",
                UnityEngine.Rendering.CullMode.Off, true);

        if (lOLoader != null && pOMaterial == lOLoader.PortcullisMaterial)
            return fGetPaletteModel(ref mOPortcullisMaterial, "UW Portcullis (Palette)",
                UnityEngine.Rendering.CullMode.Back, false);

        if (pOMaterial.shader.name == DungeonShaderName)
            return mOPaletteMaterial;

        if (pOMaterial.shader.name == BillboardShaderName)
            return fGetPaletteBillboard(pOMaterial, PaletteBillboardShaderName);

        if (pOMaterial.shader.name == DecalShaderName)
            return fGetPaletteBillboard(pOMaterial, PaletteDecalShaderName);

        return null;
    }

    /// <summary>The material of the solid-colour model faces or of the portcullis, cached in
    /// pOCache. It needs no texture - the index is stored on the vertex.
    ///
    /// pbModelDepth: the model faces are not hidden by the floor and the ceiling of their own
    /// tile (UWOwnTile). The palette path builds its OWN material here, so the keyword
    /// of the colour path's material (UWLevelLoader) does not reach it - the first version had
    /// it in the colour path only (per user, 2026-09-22: "in the palette renderer the shrine is
    /// not drawn over floor and ceiling"). Not for the portcullis.</summary>
    private Material fGetPaletteModel(ref Material pOCache, string psName,
        UnityEngine.Rendering.CullMode peCull, bool pbModelDepth)
    {
        if (pOCache != null)
            return pOCache;

        Shader lOShader = Shader.Find(PaletteModelShaderName);

        if (lOShader == null)
        {
            Debug.LogError("Render path: shader " + PaletteModelShaderName + " not found");
            return null;
        }

        pOCache = new Material(lOShader);
        pOCache.name = psName;
        pOCache.SetFloat(UWShaderIds.Cull, (float)peCull);

        if (pbModelDepth)
            UWOwnTile.Enable(pOCache);

        return pOCache;
    }

    /// <summary>Fetches the colour texture to index texture mapping for objects and
    /// creatures. Must be done anew on every apply: creature atlases only come into being
    /// when the first creature of their kind appears.</summary>
    private void fCollectIndexTextures()
    {
        mOIndexTextures.Clear();

        UWLevelLoader lOLoader = fLoader;

        if (lOLoader != null && lOLoader.ObjectAtlas != null
            && lOLoader.ObjectAtlas.Atlas != null
            && lOLoader.ObjectAtlas.IndexAtlas != null)
            mOIndexTextures[lOLoader.ObjectAtlas.Atlas] = lOLoader.ObjectAtlas.IndexAtlas;

        UWCritterAnimator.CollectIndexTextures(mOIndexTextures);
    }

    /// <summary>
    /// The palette version of a sprite or decal material. Alpha cutoff, depth bias and the
    /// blend state are carried over - only the texture is swapped for the index version
    /// and the shader changed.
    ///
    /// Returns null if there is no index version of this texture. Then the
    /// material stays as it is - better a sprite on the old path than a wrongly
    /// coloured one.
    /// </summary>
    private Material fGetPaletteBillboard(Material pOSource, string psShaderName)
    {
        Material lOResult;

        if (mOPaletteMaterials.TryGetValue(pOSource, out lOResult))
            return lOResult;

        Texture lOIndexTexture;

        if (pOSource.mainTexture == null
            || !mOIndexTextures.TryGetValue(pOSource.mainTexture, out lOIndexTexture))
            return null;

        Shader lOShader = Shader.Find(psShaderName);

        if (lOShader == null)
        {
            Debug.LogError("Render path: shader " + psShaderName + " not found");
            return null;
        }

        lOResult = new Material(lOShader);
        lOResult.name = pOSource.name + " (Palette)";

        lOResult.SetTexture(UWShaderIds.IndexTex, lOIndexTexture);

        // Only carry over what both sides know. The decal shader has neither
        // depth bias nor an adjustable blend state.
        fCopyFloat(pOSource, lOResult, UWShaderIds.Cutoff);
        fCopyFloat(pOSource, lOResult, UWShaderIds.DepthBias);
        fCopyFloat(pOSource, lOResult, UWShaderIds.SrcBlend);
        fCopyFloat(pOSource, lOResult, UWShaderIds.DstBlend);
        fCopyFloat(pOSource, lOResult, UWShaderIds.ZWrite);
        fCopyFloat(pOSource, lOResult, UWShaderIds.Cull);
        fCopyFloat(pOSource, lOResult, UWShaderIds.BigRadius);
        // Creatures and items: their own tile does not cover them (UWOwnTile).
        UWOwnTile.CopyTo(pOSource, lOResult);

        lOResult.renderQueue = pOSource.renderQueue;

        mOPaletteMaterials[pOSource] = lOResult;

        return lOResult;
    }

    /// <summary>Copies a float value if both materials know it.</summary>
    private static void fCopyFloat(Material pOFrom, Material pOTo, int piId)
    {
        if (pOFrom.HasFloat(piId) && pOTo.HasFloat(piId))
            pOTo.SetFloat(piId, pOFrom.GetFloat(piId));
    }

    private void fRestore()
    {
        for (int liAt = 0; liAt < mORenderers.Count; liAt++)
        {
            if (mORenderers[liAt] != null)
                mORenderers[liAt].sharedMaterials = mOOriginals[liAt];
        }

        mORenderers.Clear();
        mOOriginals.Clear();

        miAppliedLevel = -1;
    }

    /// <summary>The scene goes: the effects' and the sweep mask's textures with it. The materials
    /// are left as the toggle left them before this step.</summary>
    public void Dispose()
    {
        mOEffects.Dispose();
        mOSweepMask.Dispose();
    }
}
