using UnityEngine;
using UnderworldRevisited;

/// <summary>
/// ONE RENDER PATH (cleanup step 6.5, 2026-10-24): the palette path (UWPaletteRenderPath, the
/// original's look through SHADES.DAT and LIGHT.DAT) or Remastered (UWRemasterRenderPath, URP's
/// lights). UWRenderPathHost holds both, switches between them and ticks the one in force; until
/// this step each piece of either path was a component of its own that asked the render mode
/// every frame and built or tore down on its own (nine such places, see UWRenderPathHost).
///
/// THE CONTRACT: Activate puts the path on (it may finish later, when what it needs is there -
/// the palette path waits for the level's atlas); Deactivate is its exact inverse and leaves the
/// other path a clean field - every material, light and object it changed goes back, and the
/// shader globals only it reads are cleared through UWShaderGlobals, so a globals dump
/// (UWShaderGlobalsDump) in the other mode shows none of them. What both paths read (the palette
/// tables, the hallucination, the grime) is UWPaletteCommon's and the host's and stays on.
///
/// The camera's flags and URP's occlusion feature are the host's alone: a path only says what it
/// wants of them.
/// </summary>
public interface IUWRenderPath
{
    UWSettings.RenderModeEnum Mode { get; }

    /// <summary>Whether the path is on and has done what it does to the world (the palette path:
    /// its materials swapped).</summary>
    bool IsApplied { get; }

    void Activate();

    void Deactivate();

    /// <summary>Every frame in Update while the path is in force.</summary>
    void Tick();

    /// <summary>Every frame in LateUpdate while the path is in force, after the frame moved the
    /// camera.</summary>
    void LateTick();

    /// <summary>After the level was built or rebuilt (a level change, a jump, a terrain trap).</summary>
    void OnLevelBuilt();

    /// <summary>A node built after the level - an object picture, a remains, the rebuilt geometry
    /// (UWWorldView, the one exit for those). Every runtime view needs it, or it stays on the
    /// other path (Docs/CLEANUP-PLAN.md section 11, "Palette path").</summary>
    void OnObjectSpawned(GameObject pOSpawned);

    /// <summary>The material this path draws a source material with, or null for the source
    /// itself.</summary>
    Material GetMaterial(Material pOSource);

    /// <summary>The copy of the opaque picture (translucent pixels read it).</summary>
    bool WantsSceneColour { get; }

    /// <summary>The depth texture (the palette sprites read the geometry behind them).</summary>
    bool WantsSceneDepth { get; }

    /// <summary>The camera's post-processing (Remastered's Volume).</summary>
    bool WantsPostProcessing { get; }

    /// <summary>URP's screen-space ambient occlusion feature.</summary>
    bool WantsOcclusion { get; }

    /// <summary>The host goes (the scene is unloaded): what the path made is destroyed.</summary>
    void Dispose();
}
