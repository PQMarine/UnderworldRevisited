using UnityEngine;

/// <summary>
/// A PART OF THE MODERN HUD (cleanup 5.16, 2026-10-22): something UWModernHud builds onto its
/// canvas once and lays out every frame in a fixed order - the weapon, the shelf with the flasks,
/// the active spells, the heading strip, the compass, the rune hollow, the original's stats and
/// character pages, the messages, the stone shelf, the backs, the view's pictures, the crosshair.
/// The compass and the original's pages (UWModernCompass, UWModernStatsPage,
/// UWModernInventoryPage) had these three members before; the pieces split out of UWModernHud
/// share them through UWModernPart.
/// </summary>
public interface IUWModernPart
{
    /// <summary>Where the part lies on the screen this frame (pixels, bottom-left origin) where it
    /// counts as interface (UWModernHud.IsOverParts), empty while it shows nothing or takes no
    /// pointer.</summary>
    Rect ScreenRect { get; }

    /// <summary>Makes the part's images under pORoot, once - the HUD calls the builds in the order
    /// the images are to lie in.</summary>
    void Build(Transform pORoot);

    /// <summary>Once a frame while the modern HUD shows; pfPixelScale is UWModernUi.PixelScale as
    /// the HUD applied it this frame.</summary>
    void Update(float pfPixelScale);
}
