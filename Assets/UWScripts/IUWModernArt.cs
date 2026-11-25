using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE GAME'S PICTURES FOR THE MODERN INTERFACE (Docs/UW2-PLAN.md, "The modern interface for UW2: one
/// interface, two hosts", stage U1, 2026-11-23): the only one of the session's interfaces that hands out
/// Unity's types. So far the pictures that follow the character's state - the flasks as full as the
/// character is, the power gem, the weapon in the hand. The freed pieces (paperdoll, panels, slots, compass,
/// hollow, shelf) follow in the later stages.
///
/// A host hands out THE SAME TEXTURE OBJECT for the same state: the vitals key their cut-out copies by it
/// (UWModernVitals.fKeyed), as they did by UWCharacter's cached pictures.
/// </summary>
public interface IUWModernArt
{
    /// <summary>The vitality flask at the character's vitality (green while poisoned), null before the art is built.</summary>
    Texture2D HealthFlask { get; }

    Texture2D ManaFlask { get; }

    /// <summary>The power gem as it is now (idle, charging, full), null before the art is built.</summary>
    Texture2D PowerGem { get; }

    /// <summary>The weapon in the hand this frame; false while none is drawn.</summary>
    bool TryGetWeapon(out UWModernWeaponFrame pOFrame);

    /// <summary>
    /// What the flasks and the gem stand on and how they are cut free (stage U4 part A step 3, 2026-11-23; the
    /// vitals called the first game's builders themselves until then). Built anew at every call; the caller owns
    /// the textures.
    /// </summary>
    UWModernVitalsArt BuildVitalsArt(bool pbGemApart);

    // ------------------------------------------------- Stage U3 part C

    /// <summary>The game's pointer picture for the view's middle: the look, use or fight pointer as the game
    /// stands, the lock's while pbLocked; null for none.</summary>
    Texture2D Crosshair(bool pbLocked);

    /// <summary>The original's compass at a step of sixteen, its disc and needle as one picture; pbFreed cut
    /// free of the frame's stone.</summary>
    Texture2D Compass(int piStep, bool pbFreed);

    /// <summary>The picture the game shows in the view now (a shrine's, a cutscene's still), null for none.</summary>
    Sprite ViewPicture { get; }

    // ------------------------------------------------- The paperdoll (stage U3 part D)

    /// <summary>Which body the figure is (sex and skin), for the art cut free of the page.</summary>
    int FigureBody { get; }

    /// <summary>Changes when the body or the armour worn changes; the pages rebuild the figure then.</summary>
    int FigureVersion { get; }

    /// <summary>How far the leg armour's picture hangs below its place, in the original's rows.</summary>
    int FigureLegsDrop { get; }

    /// <summary>The source picture of an armour piece as worn, null for none (and for the body).</summary>
    UWTexture FigureSource(UWModernFigurePiece pePiece);

    /// <summary>The body's or an armour piece's texture as the original's page shows it, null for none.</summary>
    Texture2D FigureTexture(UWModernFigurePiece pePiece);

    /// <summary>A picture of the first game's frame dragons, for the Classic Wide frame built from that frame
    /// (stage U3 part D; the second game's frame has none and gets a frame of its own in stage U7).</summary>
    Sprite DragonSprite(int piIndex);
}

/// <summary>The paperdoll's pieces, in the order the pages lay them.</summary>
public enum UWModernFigurePiece
{
    Body,
    Helmet,
    Gloves,
    Legs,
    Chest,
    Boots
}

/// <summary>
/// The art under the flasks and the gem: the shelf they stand on with their places on it, the gem's stand for
/// the layouts that set it apart, and which pixels of the game's flask and gem pictures to keep.
/// </summary>
public struct UWModernVitalsArt
{
    /// <summary>The shelf's picture, as wide and high as the layout says. A game without a shelf hands a clear one.</summary>
    public Texture2D Shelf;

    public UWModernHudArt.ShelfLayout Layout;

    /// <summary>The gem's own stand and where the gem lies on it, null for a game without one.</summary>
    public Texture2D GemStand;

    public Vector2Int GemOnStand;

    /// <summary>The pixels to keep of a flask on the shelf, of a flask standing free and of the gem, rows from the
    /// top. Null where the game's pictures come cut free already.</summary>
    public bool[] FlaskMask;

    public bool[] FreeFlaskMask;

    public bool[] GemMask;
}

/// <summary>
/// The drawn weapon of one frame, in the original's pixels: what UWModernWeaponView lays out. The offsets count
/// from the view's left edge and up from its bottom; the view's measures are the game's (172 x 112 on the first
/// game, 208 x 128 on the second).
/// </summary>
public struct UWModernWeaponFrame
{
    public Texture2D Texture;

    /// <summary>The game's view in its own pixels, which the weapon's place and size are measured in.</summary>
    public float ViewWidth;

    public float ViewHeight;

    /// <summary>The weapon is cut at the view's edges, as the game's view cuts it (the second game's pictures
    /// reach beyond it).</summary>
    public bool ClipToView;

    /// <summary>The picture's size in the original's pixels, not the texture's (larger with the sprites' xBR).</summary>
    public Vector2 Size;

    /// <summary>The frame's place from Weapons.dat.</summary>
    public int OffsetX;

    public int OffsetY;

    /// <summary>The sway while walking, in the original's columns.</summary>
    public float Sway;

    /// <summary>How far the weapon is raised, 0 down to 1 ready.</summary>
    public float Ready;

    /// <summary>A mod's larger picture drawn instead, null for none, and where the original lies in it.</summary>
    public Texture2D Extended;

    public int ExtendedLeft;

    public int ExtendedTop;

    public Vector2 ExtendedSize;
}
