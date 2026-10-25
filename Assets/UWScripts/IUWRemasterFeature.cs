using UnderworldRevisited;

/// <summary>
/// One of Remastered's own additions to the world - the placed lights (UWRemasterLights), the
/// ground shadows (UWRemasterGroundShadows) - as a child of UWRemasterRenderPath (cleanup step
/// 6.5, 2026-10-24). Until then each was a component that asked the render mode every frame and
/// built or tore down on its own; now the path ticks them while it is in force and clears them
/// when it goes. Each still follows its own settings (on, off, strength) in Tick.
/// </summary>
public interface IUWRemasterFeature
{
    /// <summary>Every frame while Remastered is in force.</summary>
    void Tick(UWSettings pOSettings);

    /// <summary>Remastered goes: everything the feature put into the world goes with it.</summary>
    void Clear();

    /// <summary>The scene goes.</summary>
    void Dispose();
}
