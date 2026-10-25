/// <summary>
/// THE FIXED UPDATE ORDER, in one place (cleanup step 4.7, 2026-10-18). Unity runs the Updates of
/// scripts with the same execution order in an order it does not promise, and until this step
/// every player script sat at 0: the movement read the motion abilities the character wrote in
/// its Update (a frame late or not, depending on the run), the swim view read the motion's height
/// before or after the frame moved it, the head bob the speed of this frame or the last. Each
/// class names its slot with [DefaultExecutionOrder(UWUpdateOrder.X)]; LateUpdate follows the
/// same order.
///
/// The player's frame, in this order:
///   Character -50   the status pass (armour, light, the motion abilities pushed into the
///                   movement), the game clock's tick with the vitals and the swim check, the
///                   noise, the weapon. Before the movement, so the frame moves with this frame's
///                   abilities.
///   Look      -10   the mouse look turns the body (modern scheme) and tilts the camera: the
///                   frame's turn goes into the frame's walk.
///   Movement    0   input, the motion core's steps (UWMotionStepper), the transform, the move
///                   triggers, the footsteps; in LateUpdate the measured speed.
///   Terrain   +10   the view of where the frame ended: the swim view and its sway over the
///                   moved feet (the camera cannot trail the body by a frame), the head bob,
///                   the water sound, the lava, the automap; in LateUpdate the roll on the camera
///                   the look has tilted. The head bob reads the speed the movement measured in
///                   the last LateUpdate - smoothed over a tenth of a second, so that frame is of
///                   no account.
///   Shake     +15   the screen shake (UWScreenShake), the LAST to turn the camera in LateUpdate:
///                   after the terrain has set its roll absolutely. At 0 it ran before the
///                   terrain, which overwrote every shake's roll in the same frame - no hit
///                   shook the view after step 4.7 (per user, 2026-10-19, a trap's arrow).
///   Death     +20   a death the frame's damage caused is handled in the same frame, after the
///                   victory check (UWCritterDriver).
///
/// The creature driver (UWCritterDriver: the creatures, the world scheduler, then the victory
/// check) and Interaction stay at 0, unordered against the movement as before; the easy
/// movement's repeat tolerates that (UWPlayerInput.HoldEasyMovement).
///
/// The pointer first of all, Pointer -300 (UWPointerDriver, cleanup step 7.7B, 2026-10-25): the
/// frame's position, capture and clamp are there before anyone reads them; since cleanup step 7.8
/// also the pad pointer's decision (the PadPointer context) and the places that wait for a pad press
/// as the frame begins (UWPadOwners).
///
/// The interface's slots, older than this class: FitCanvas -200 (before the canvas scaler reads
/// its settings), ModernMinimap +100 (after the panel it may lie in), GamepadPointer +1000 (late:
/// what waits for a pad press acts on it first; the pointer asks UWPadOwners whether it may click).
/// </summary>
public static class UWUpdateOrder
{
    public const int Pointer = -300;

    public const int FitCanvas = -200;

    public const int Character = -50;

    public const int Look = -10;

    public const int Movement = 0;

    public const int Terrain = 10;

    public const int Shake = 15;

    public const int Death = 20;

    public const int ModernMinimap = 100;

    public const int GamepadPointer = 1000;
}
