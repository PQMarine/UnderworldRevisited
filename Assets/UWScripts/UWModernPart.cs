using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// THE BASE OF THE PIECES SPLIT OUT OF UWModernHud (cleanup 5.16, 2026-10-22): what they shared in
/// copies while they were one class of 2,440 lines.
///
///   - The scene's UI, character, interaction and control scheme, through the HUD that owns the
///     part (UWModernHud finds them in its Start) - under the names the HUD's fields had, so the
///     moved code reads as it did.
///   - The UI's scale as the HUD applied it this frame (miPixelScale), and an element's own size
///     on top of it (fScaleOf; fFontOf for its text) - UWModernLayout's size per element.
///   - THE SCALE'S CHANGE (OnPixelScale): sizes that follow only from the scale, set again only
///     when it changes (UWModernHud.fApplyPixelScale, as before for the whole HUD).
///   - THE PLACEMENT from the layout settings: UWModernLayout.Place gives the element's rect (its
///     default while it was never moved), Report hands it back for the layout editor and the parts
///     that keep beside it (fPlaceAndReport).
///   - The art is rebuilt on its stamp (UWVersionedTexture, UWArtCache: the colour help, the backs,
///     the size) as each part did; the pointer gate of the HUD's buttons is the root's
///     (UWModernUi.HudTakesPointer), asked by the HUD's routing (UWModernHudInput).
/// </summary>
public abstract class UWModernPart : IUWModernPart
{
    protected UWModernPart(UWModernHud pOHud)
    {
        mOHud = pOHud;
    }

    /// <summary>The HUD that owns the part.</summary>
    protected readonly UWModernHud mOHud;

    /// <summary>The UI's scale as the HUD applied it (UWModernHud.fApplyPixelScale); -1 until then.</summary>
    protected float miPixelScale = -1f;

    public Rect ScreenRect { get; protected set; }

    public abstract void Build(Transform pORoot);

    public void Update(float pfPixelScale)
    {
        miPixelScale = pfPixelScale;
        fUpdate();
    }

    /// <summary>The scale changed: what follows from it alone (font sizes, fixed places).</summary>
    public virtual void OnPixelScale(float pfPixelScale)
    {
        miPixelScale = pfPixelScale;
    }

    /// <summary>The part's frame, at miPixelScale.</summary>
    protected abstract void fUpdate();

    protected UWGameUI mOUi => mOHud.Ui;

    protected UWCharacter mOCharacter => mOHud.Character;

    protected Interaction mOInteraction => mOHud.Interaction;

    protected UWControlScheme mOScheme => mOHud.Scheme;

    /// <summary>The text size that goes with the UI's scale (UWModernUi.FontSize).</summary>
    protected static int FontSize => UWModernUi.FontSize;

    /// <summary>An element's scale: the UI's times the element's own size (UWModernLayout.Scale).</summary>
    protected float fScaleOf(UWModernLayout.ElementEnum peElement)
    {
        return miPixelScale * UWModernLayout.Scale(peElement);
    }

    /// <summary>An element's text size: the UI's at the element's own size, 9 at least.</summary>
    protected static int fFontOf(UWModernLayout.ElementEnum peElement)
    {
        return Mathf.Max(9, Mathf.RoundToInt(FontSize * UWModernLayout.Scale(peElement)));
    }

    /// <summary>The element's place (UWModernLayout.Place, pODefault while it was never moved),
    /// reported back for the layout editor and the parts beside it.</summary>
    protected static Rect fPlaceAndReport(UWModernLayout.ElementEnum peElement, Rect pODefault)
    {
        Rect lOPlaced = UWModernLayout.Place(peElement, pODefault);

        UWModernLayout.Report(peElement, lOPlaced);

        return lOPlaced;
    }

    /// <summary>Puts a picture or text at a rect of its parent (its anchors and pivot as built).</summary>
    protected static void fSetRect(Graphic pOGraphic, Rect pORect)
    {
        RectTransform lORect = (RectTransform)pOGraphic.transform;

        lORect.anchoredPosition = pORect.position;
        lORect.sizeDelta = pORect.size;
    }
}
