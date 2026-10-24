using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE POINTER ZONES OF THE CLASSIC SCHEME (cleanup 5.18, 2026-10-23: out of UWGameUI, the code as
/// it was - section 11 of the cleanup plan freezes the zone numbers, the speeds and the
/// pointer-distance rules, which mirror UW.EXE): with the left button held in the 3D view the
/// pointer's place in the view decides the movement - strafing and backing in the bottom band,
/// turning in the middle one, walking with turning in the top one, the speed and the turn rate
/// from the distance to the band's border (Update). The pointer is clamped to the original's
/// view window meanwhile (WarpCursorPosition; the window of place_3d_view, 172 x 113 at 52/19,
/// since 2026-10-23 - the hole of the frame picture until then), and the position it was clamped
/// to is the one everyone draws the pointer at (PointerPosition). The motion core takes the
/// pointer's pixel in the original's 172 x 113 area (CursorAreaX/Y, UWPlayerMotion.PointerCommand);
/// the pointer's picture follows CursorMovement (UWPointerCursor, which draws the pointer itself
/// at the clamped position while the mouse holds, IsMouseHeld).
///
/// Works in real screen pixels, since it evaluates the real mouse position; the window's
/// rectangle comes from UWClassicScreen. The zone constants are UWClassicLayout.PointerZones'.
/// </summary>
public sealed class UWPointerZones
{
    public UWPointerZones(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    private readonly UWGameUI mOUi;

    /// <summary>The player's movement, whose input says whether a cursor movement is in progress
    /// (UWGameUI.Start).</summary>
    private UWPlayerMovement mOMovement;

    internal void Attach(UWPlayerMovement pOMovement)
    {
        mOMovement = pOMovement;
    }

    public UWCursors.CursorEnum CursorMovement { get; set; }
    public Vector3 LastCursorPositionInGameArea { get; set; }

    /// <summary>0..1 speed fraction for Forward/Backward/SlightLeft/SlightRight, analogue from the
    /// pointer distance to the respective zone border - see Update().</summary>
    public float CursorWalkFactor { get; set; }

    /// <summary>0..1 turn rate fraction for TurnLeft/TurnRight/SlightLeft/SlightRight, analogue from
    /// the pointer distance to the screen centre - see Update().</summary>
    public float CursorRotateFactor { get; set; }

    /// <summary>The pointer in the view's movement area in the original's pixels, x from the
    /// left, y from the BOTTOM (0 to UWPlayerMotion.PointerAreaWidth/Height - 1), as
    /// seg034_2F89_4E reads it - the motion core's pointer scheme takes these, see
    /// UWPlayerMotion.PointerCommand. Kept while the pointer is outside the area.</summary>
    public int CursorAreaX { get; private set; }

    public int CursorAreaY { get; private set; }

    /// <summary>The last position in the view starts at its middle (UWGameUI.Init).</summary>
    internal void ResetLastPositionToViewCentre()
    {
        LastCursorPositionInGameArea = mOUi.ClassicScreen.MovementAreaRect.center;
    }

    /// <summary>
    /// WHERE THE POINTER IS DRAWN this frame: the mouse position, or during a cursor movement
    /// the position it was clamped to (2026-10-06, per user with a screenshot: a ring on the
    /// pointer hung far to the right of it in the panel while turning right). After
    /// WarpCursorPosition the Input System keeps reporting the pushed-out position, so whoever
    /// reads Mouse.current.position - UWItemDrag for the thing on the pointer - draws beyond
    /// the edge the pointer itself is held at. This is the one position everyone draws with.
    /// </summary>
    public Vector2 PointerPosition { get; private set; }

    /// <summary>A cursor movement is under way and the MOUSE drives it: the pointer is clamped
    /// here each frame, and UWPointerCursor draws it at the clamped position meanwhile (the pad's
    /// pointer is its own and never overshoots, UWGamepadPointer).</summary>
    public bool IsMouseHeld => mOMovement != null && mOMovement.Input.IsCursorMovementInProgress && !UWGamepadPointer.OwnsPosition;

    /// <summary>Once a frame at the end of UWGameUI.Update, after the HUD parts: the clamp, the
    /// drawn position, the zone.</summary>
    internal void Update()
    {
        Vector2 lOMousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        // THE ORIGINAL'S VIEW WINDOW, 172 x 113 at 52/19 (UWClassicScreen.MovementAreaRect; per
        // user from the original, 2026-10-23: the pointer stays in the view while the button is
        // held). Until then the hole of the frame picture, 176 x 121 at 50/17: seven rows lower at
        // the bottom, so the pointer's picture hung into the rune hollow.
        Rect lOArea = mOUi.ClassicScreen.MovementAreaRect;

        // During a held cursor drag (original scheme) the real pointer is
        // actively clamped to the small 3D viewport - via WarpCursorPosition, because
        // CursorLockMode.Confined only knows the border of the whole game window, not
        // this smaller rectangle. Outside a drag the pointer stays free, as in the
        // original from 1992, where it may also wander over paper doll & co.
        if (mOMovement != null && mOMovement.Input.IsCursorMovementInProgress)
        {
            // The gamepad's pointer: its own position, the Input System's is stale after the
            // warps - with it the pointer stuck to the view's top and left edges (per user,
            // 2026-10-07; UWGamepadPointer.OwnsPosition).
            if (UWGamepadPointer.OwnsPosition)
                lOMousePos = UWGamepadPointer.Position;

            float lfClampedX = Mathf.Clamp(lOMousePos.x, lOArea.xMin, lOArea.xMax);
            float lfClampedY = Mathf.Clamp(lOMousePos.y, lOArea.yMin, lOArea.yMax);

            if (!Mathf.Approximately(lfClampedX, lOMousePos.x) || !Mathf.Approximately(lfClampedY, lOMousePos.y))
            {
                lOMousePos = new Vector2(lfClampedX, lfClampedY);
                Mouse.current.WarpCursorPosition(lOMousePos);
            }
        }

        PointerPosition = lOMousePos;

        // The border counts as inside: during a drag the pointer is clamped exactly onto it, and
        // with a strict check the zone was then not re-evaluated any more - at the top edge
        // (full walking speed) the movement stuck in the last zone and did not switch between
        // left/forward/right (per user, 2026-09-15).
        if (lOMousePos.x >= lOArea.xMin && lOMousePos.x <= lOArea.xMax && lOMousePos.y >= lOArea.yMin && lOMousePos.y <= lOArea.yMax)
        {
            LastCursorPositionInGameArea = lOMousePos;

            CursorWalkFactor = 0f;
            CursorRotateFactor = 0f;

            // Normalised to the viewport: relx 0 (left) to 1 (right), rely 0 (top) to 1 (bottom).
            // Y is flipped because Unity's mouse position starts at 0 at the bottom.
            //
            // THE ZONES FOLLOW UW.EXE (verified in the disassembly 2026-09-15): the view
            // registers its cursor regions in ovr134_6DB, the movement is decided in
            // seg034_2F89_4E, for a movement area of W = 172 by H = 113 pixels. With xm/ym the
            // position in that area (ym counted from the bottom):
            //   ym < H/5 (22):          strafe left / backward / strafe right by xm*3/W
            //                           (commands 9/8/0A from the table at DS:0768), constant
            //                           speeds, independent of the position;
            //   22 <= ym <= 45 (6H/15): turn left / centre / turn right, no walking;
            //   ym > 45:                slight left / forward / slight right, walking and turning.
            //   Left/right: xm < 57 (W*5/15), xm > 114.
            //   Turn = (57 - xm) * 384 / W resp. (xm - 114) * 384 / W, i.e. linear from the
            //   border (0) to the edge (1); walk = (ym - 45) * 192 / H, linear from the band
            //   border (0) to the top edge (1, about run-key speed).
            // Our area is that window scaled to the screen and widened with the frame (Classic
            // Wide), therefore the borders are applied as fractions.
            float relx = (lOMousePos.x - lOArea.xMin) / lOArea.width;
            float rely = (lOArea.yMax - lOMousePos.y) / lOArea.height;

            CursorAreaX = Mathf.Clamp(Mathf.FloorToInt(relx * UWPlayerMotion.PointerAreaWidth), 0, UWPlayerMotion.PointerAreaWidth - 1);
            CursorAreaY = Mathf.Clamp(Mathf.FloorToInt((1f - rely) * UWPlayerMotion.PointerAreaHeight), 0, UWPlayerMotion.PointerAreaHeight - 1);

            // The borders as fractions of the area: UWClassicLayout.PointerZones (57 and 115 of
            // 172 columns, 22 and 46 of 113 rows), engine-free since cleanup 5.18.
            const float lfLeftBorder = UWClassicLayout.PointerZones.LeftBorder;
            const float lfRightBorder = UWClassicLayout.PointerZones.RightBorder;
            const float lfStrafeBorder = UWClassicLayout.PointerZones.StrafeBorder;
            const float lfWalkBorder = UWClassicLayout.PointerZones.WalkBorder;

            if (rely > lfStrafeBorder)
            {
                // Bottom fifth: strafe sideways, backwards in the middle, at constant speed.
                if (relx < lfLeftBorder)
                    CursorMovement = UWCursors.CursorEnum.StrafeLeft;
                else if (relx >= lfRightBorder)
                    CursorMovement = UWCursors.CursorEnum.StrafeRight;
                else
                {
                    CursorMovement = UWCursors.CursorEnum.Backward;
                    CursorWalkFactor = 1f;
                }
            }
            else if (rely > lfWalkBorder)
            {
                // Middle band: pure turning, rest point in the middle.
                if (relx < lfLeftBorder)
                {
                    CursorMovement = UWCursors.CursorEnum.TurnLeft;
                    CursorRotateFactor = Mathf.Clamp01((lfLeftBorder - relx) / lfLeftBorder);
                }
                else if (relx >= lfRightBorder)
                {
                    CursorMovement = UWCursors.CursorEnum.TurnRight;
                    CursorRotateFactor = Mathf.Clamp01((relx - lfRightBorder) / (1f - lfRightBorder));
                }
                else
                    CursorMovement = UWCursors.CursorEnum.Center;
            }
            else
            {
                // Upper band: forwards, at the sides additionally with turning; walk and turn
                // factors are independent of each other, as in the original.
                CursorWalkFactor = Mathf.Clamp01((lfWalkBorder - rely) / lfWalkBorder);

                if (relx < lfLeftBorder)
                {
                    CursorMovement = UWCursors.CursorEnum.SlightLeft;
                    CursorRotateFactor = Mathf.Clamp01((lfLeftBorder - relx) / lfLeftBorder);
                }
                else if (relx >= lfRightBorder)
                {
                    CursorMovement = UWCursors.CursorEnum.SlightRight;
                    CursorRotateFactor = Mathf.Clamp01((relx - lfRightBorder) / (1f - lfRightBorder));
                }
                else
                    CursorMovement = UWCursors.CursorEnum.Forward;
            }
        }
        else
        {
            if (mOMovement != null && !mOMovement.Input.IsCursorMovementInProgress)
                CursorMovement = UWCursors.CursorEnum.Center;
        }
    }
}
