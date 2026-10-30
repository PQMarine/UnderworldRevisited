using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EVERY SHADER PROPERTY AND KEYWORD NAME THE GAME USES, in one place (cleanup step 6.1,
/// 2026-10-24): the properties as Shader.PropertyToID ids, the keywords as their names. Until then
/// the names were spread over six classes as strings, and most of the per-frame globals went
/// through a string, so Unity hashed the name on every call; a typo made a value that no shader
/// ever saw. No call site writes a property name itself any more.
///
/// The GLOBALS (Shader.SetGlobal*, written through UWShaderGlobals) are registered with their
/// names, so the dump (UWShaderGlobalsDump, Shift+F12) lists exactly what the game sets; every one
/// of them is read by a shader under Assets/UWShaders (_UWCutoffDistance, set but read by none, went on 2026-10-24). The material and per-renderer properties
/// stay with the code that sets them (UWLevelLoader, UWPaletteRenderPath, UWRemasterRenderPath and
/// the others); the render-path step of the cleanup moves those.
///
/// The keywords stay strings: Material.EnableKeyword takes the name, and all three are
/// multi_compile, per material (see UWDungeon.shader) - do not turn them into shader_feature.
/// </summary>
public static class UWShaderIds
{
    // Declared before every id that registers itself: static fields start in text order.
    private static readonly List<string> msGlobalNames = new List<string>();

    private static readonly Dictionary<int, string> msNames = new Dictionary<int, string>();

    /// <summary>The names of every global the game sets, in the order of this file.</summary>
    public static IReadOnlyList<string> GlobalNames => msGlobalNames;

    /// <summary>The name of a global's id, or null for an id that is not a registered global.</summary>
    public static string GlobalName(int piId)
    {
        return msNames.TryGetValue(piId, out string lsName) ? lsName : null;
    }

    // ------------------------------------------------- Keywords

    /// <summary>The shader keyword that switches the own-tile rule on (UWOwnTile.hlsl, see
    /// UWOwnTile).</summary>
    public const string OwnTileKeyword = "_UW_OWN_TILE";

    /// <summary>Remastered's part of UWDungeon.shader (UWRemasterRenderPath).</summary>
    public const string RemasterKeyword = "_UW_REMASTER";

    /// <summary>The smooth upscaling of the interface icons (UWIconPalette).</summary>
    public const string PixelSmoothKeyword = "UW_PIXEL_SMOOTH";

    // ------------------------------------------------- Globals: the palette lookup
    // UWPaletteLookup.hlsl and the interface shaders, set by UWShadePalette.ApplyGlobals and
    // UWRenderPathHost.

    /// <summary>Name of the colour table in the shader.</summary>
    public static readonly int ColourTable = fGlobal("_UWColourTable");

    /// <summary>The colour table the 3D view shades through (UWPaletteLookup.hlsl) - the same as
    /// _UWColourTable except at Night Vision, where MONO.DAT replaces LIGHT.DAT for the world
    /// only: the interface draws without the light table in the original and stays in colour.</summary>
    public static readonly int WorldColourTable = fGlobal("_UWWorldColourTable");

    /// <summary>Name of the shade table in the shader.</summary>
    public static readonly int ShadeTable = fGlobal("_UWShadeTable");

    /// <summary>Name of the rotation table in the shader.</summary>
    public static readonly int RotationTable = fGlobal("_UWRotationTable");

    public static readonly int LightLevel = fGlobal("_UWLightLevel");

    public static readonly int RotationStep = fGlobal("_UWRotationStep");

    public static readonly int RotationStepCount = fGlobal("_UWRotationStepCount");

    public static readonly int TileSpacing = fGlobal("_UWTileSpacing");

    public static readonly int InterpolateLevels = fGlobal("_UWInterpolateLevels");

    public static readonly int Dither = fGlobal("_UWDither");

    public static readonly int DitherPattern = fGlobal("_UWDitherPattern");

    public static readonly int DitherScale = fGlobal("_UWDitherScale");
    public static readonly int ShadeFormula = fGlobal("_UWShadeFormula");
    public static readonly int ViewScale = fGlobal("_UWViewScale");
    public static readonly int Demaster = fGlobal("_UWDemaster");

    public static readonly int FullBright = fGlobal("_UWFullBright");

    /// <summary>Names of the XFER.DAT lookups in the shader (UWPaletteLookup.hlsl).</summary>
    public static readonly int InverseLookup = fGlobal("_UWInverseLookup");

    public static readonly int XferColours = fGlobal("_UWXferColours");

    public static readonly int XferReady = fGlobal("_UWXferReady");

    // ------------------------------------------------- Globals: hallucination and Remastered's ghosts
    // UWHallucination.hlsl and UWPaletteLookup.hlsl, set by UWRenderPathHost and UWPaletteCommon.

    public static readonly int Scramble = fGlobal("_UWScramble");

    public static readonly int WallSlices = fGlobal("_UWWallSlices");

    public static readonly int FloorSlices = fGlobal("_UWFloorSlices");

    public static readonly int LightEffectTable = fGlobal("_UWLightEffectTable");

    public static readonly int LightEffect = fGlobal("_UWLightEffect");

    public static readonly int HallucinationPalette = fGlobal("_UWHallucinationPalette");

    public static readonly int RemasterXfer = fGlobal("_UWRemasterXfer");

    public static readonly int RemasterPalettes = fGlobal("_UWRemasterPalettes");

    public static readonly int RemasterLightEffect = fGlobal("_UWRemasterLightEffect");

    public static readonly int RemasterInverseLookup = fGlobal("_UWRemasterInverseLookup");

    public static readonly int RemasterXferColours = fGlobal("_UWRemasterXferColours");

    // ------------------------------------------------- Globals: the palette effects
    // UWPaletteEffects.hlsl and UWGrime.hlsl, set by UWPaletteEffects (a child of UWPaletteRenderPath).

    public static readonly int PaletteAO = fGlobal("_UWPaletteAO");

    public static readonly int PaletteLights = fGlobal("_UWPaletteLights");

    public static readonly int PaletteLightMap = fGlobal("_UWPaletteLightMap");

    public static readonly int PaletteGrime = fGlobal("_UWPaletteGrime");

    public static readonly int GrimeTint = fGlobal("_UWGrimeTint");

    public static readonly int GrimeTintTable = fGlobal("_UWGrimeTintTable");

    public static readonly int GrimeToneColour = fGlobal("_UWGrimeToneColour");

    public static readonly int PaletteRelief = fGlobal("_UWPaletteRelief");

    public static readonly int PaletteCavity = fGlobal("_UWPaletteCavity");

    public static readonly int PaletteNormalArray = fGlobal("_UWPaletteNormalArray");

    public static readonly int PaletteDepth = fGlobal("_UWPaletteDepth");

    public static readonly int PaletteParallax = fGlobal("_UWPaletteParallax");

    public static readonly int PaletteSelfShadow = fGlobal("_UWPaletteSelfShadow");

    public static readonly int PaletteGlow = fGlobal("_UWPaletteGlow");

    public static readonly int PaletteShadows = fGlobal("_UWPaletteShadows");

    public static readonly int PaletteShadowCount = fGlobal("_UWPaletteShadowCount");

    public static readonly int PaletteShadowStrength = fGlobal("_UWPaletteShadowStrength");

    // ------------------------------------------------- Globals: painter order and own tile
    // UWPainterOrder.hlsl and UWOwnTile.hlsl, set by UWSweepMaskDriver (a child of UWPaletteRenderPath) and UWOwnTile.

    public static readonly int SweepMask = fGlobal("_UWSweepMask");

    public static readonly int SweepMaskReady = fGlobal("_UWSweepMaskReady");

    public static readonly int OwnTileSize = fGlobal("_UWOwnTileSize");

    public static readonly int OwnTileCeilingY = fGlobal("_UWOwnTileCeilingY");

    public static readonly int FloorHeights = fGlobal("_UWFloorHeights");

    public static readonly int FloorHeightsReady = fGlobal("_UWFloorHeightsReady");

    public static readonly int DoorPlanes = fGlobal("_UWDoorPlanes");

    public static readonly int DoorPlanesReady = fGlobal("_UWDoorPlanesReady");

    public static readonly int ModelOrder = fGlobal("_UWModelOrder");

    // ------------------------------------------------- Material and per-renderer properties

    /// <summary>The toggle property of the billboard shaders that goes with the keyword.</summary>
    public static readonly int OwnTile = Shader.PropertyToID("_OwnTile");

    /// <summary>The material float with a BIG object's radius in eighths of a tile (COMOBJ byte 1
    /// bits 0-2 when bit 3 is set - creatures), 0 for the items: the original hands a big object
    /// that pokes into the nearer row or the next inner column to that tile (see UWOwnTile.hlsl,
    /// UWOwnTileSpriteDepth).</summary>
    public static readonly int BigRadius = Shader.PropertyToID("_BigRadius");

    /// <summary>Added to an object's sort key in the painter's order (UWPainterOrder.hlsl): -1 for
    /// the animations 0x1C0-0x1FF, which the original sorts one step nearer. Set per renderer
    /// (UWObjectSpawner.fSetPainterPlace).</summary>
    public static readonly int KeyBonus = Shader.PropertyToID("_KeyBonus");

    /// <summary>The sprite's place in its tile's chain (UWObjectSpawner.fSetPainterPlace).</summary>
    public static readonly int ChainIndex = Shader.PropertyToID("_ChainIndex");

    /// <summary>A 3D model's tile floor, per renderer: the model's origin may lie below it, the
    /// shrine's does by two units, so the box needs the real floor.</summary>
    public static readonly int ModelFloorY = Shader.PropertyToID("_UWModelFloorY");

    /// <summary>The model faces and the portcullis take their palette index from the vertex
    /// (UWHallucination.hlsl).</summary>
    public static readonly int VertexIndex = Shader.PropertyToID("_UWVertexIndex");

    /// <summary>A light source's bright pixels in Remastered, per renderer (UWLightFlicker).</summary>
    public static readonly int SpriteGlow = Shader.PropertyToID("_SpriteGlow");

    /// <summary>A light source's renderer while the palette glow is on (UWPaletteEffects).</summary>
    public static readonly int GlowSprite = Shader.PropertyToID("_UWGlowSprite");

    public static readonly int MainTex = Shader.PropertyToID("_MainTex");

    public static readonly int IndexTex = Shader.PropertyToID("_IndexTex");

    public static readonly int TexArray = Shader.PropertyToID("_TexArray");

    public static readonly int IndexArray = Shader.PropertyToID("_IndexArray");

    public static readonly int NormalArray = Shader.PropertyToID("_NormalArray");

    public static readonly int MaterialLut = Shader.PropertyToID("_MaterialLut");

    public static readonly int Cull = Shader.PropertyToID("_Cull");

    public static readonly int Cutoff = Shader.PropertyToID("_Cutoff");

    public static readonly int DepthBias = Shader.PropertyToID("_DepthBias");

    public static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");

    public static readonly int DstBlend = Shader.PropertyToID("_DstBlend");

    public static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

    public static readonly int AmbientFloor = Shader.PropertyToID("_AmbientFloor");

    public static readonly int PointLightCap = Shader.PropertyToID("_PointLightCap");

    // Remastered's dungeon material (UWRemasterRenderPath).

    public static readonly int NormalStrength = Shader.PropertyToID("_NormalStrength");

    public static readonly int SpecularStrength = Shader.PropertyToID("_SpecularStrength");

    public static readonly int SpecularPower = Shader.PropertyToID("_SpecularPower");

    public static readonly int EmissiveStrength = Shader.PropertyToID("_EmissiveStrength");

    public static readonly int Roughness = Shader.PropertyToID("_Roughness");

    public static readonly int SpecularBase = Shader.PropertyToID("_SpecularBase");

    public static readonly int WrapLighting = Shader.PropertyToID("_WrapLighting");

    public static readonly int ParallaxDepth = Shader.PropertyToID("_ParallaxDepth");

    public static readonly int ParallaxLevels = Shader.PropertyToID("_ParallaxLevels");

    public static readonly int ParallaxSteps = Shader.PropertyToID("_ParallaxSteps");

    public static readonly int ParallaxSign = Shader.PropertyToID("_ParallaxSign");

    public static readonly int SelfShadowStrength = Shader.PropertyToID("_SelfShadowStrength");

    public static readonly int SelfShadowDepth = Shader.PropertyToID("_SelfShadowDepth");

    public static readonly int SelfShadowReach = Shader.PropertyToID("_SelfShadowReach");

    public static readonly int SelfShadowSoftness = Shader.PropertyToID("_SelfShadowSoftness");

    public static readonly int CavityStrength = Shader.PropertyToID("_CavityStrength");

    // Remastered's particles (UWRemasterParticles) and ground shadows (UWRemasterGroundShadows).

    public static readonly int Intensity = Shader.PropertyToID("_Intensity");

    public static readonly int Additive = Shader.PropertyToID("_Additive");

    public static readonly int Lit = Shader.PropertyToID("_Lit");

    public static readonly int Softness = Shader.PropertyToID("_Softness");

    public static readonly int Strength = Shader.PropertyToID("_Strength");

    private static int fGlobal(string psName)
    {
        int liId = Shader.PropertyToID(psName);

        msGlobalNames.Add(psName);
        msNames[liId] = psName;

        return liId;
    }
}
