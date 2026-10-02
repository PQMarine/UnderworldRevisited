using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Roaming Sight (spell class 11, minor class 7 when cast, effect slot 1): the view
/// detaches from the body and travels across the level while the figure stays put.
///
/// BELONGS TO THE EFFECT, NOT TO THE CASTING. The view is set up and ended from
/// UWCharacter.fApplyActiveSpells, and ALL endings converge there: expiry,
/// clicking the icon away, sleep and loading a saved game. So there is no
/// state in which the spell is still in the icon bar but the camera is already back -
/// or vice versa. The reference has exactly this faulty state: it detaches the camera
/// before it knows whether an effect slot was free at all.
///
/// THE FIGURE STANDS STILL, gravity included - that is how the original does it, suppressing its entire
/// movement calculation meanwhile. Whoever casts the spell mid-jump hangs in
/// the air and only continues falling at the end.
/// </summary>
public sealed class UWRoamingSight : MonoBehaviour
{
    /// <summary>How high the view rises. The reference sets 0x458 = 1112 original units,
    /// so 278 in our units.
    ///
    /// THAT IS ABOVE THE CEILING (256), and that is correct: the ceiling quad is built with
    /// a downward-pointing normal, and both dungeon shaders discard back faces. From
    /// above, the ceiling is therefore invisible and the view looks down into the rooms. The
    /// only condition: if someone switches the shaders to double-sided drawing, the
    /// view abruptly becomes one single ceiling surface.</summary>
    [SerializeField]
    [Tooltip("Height of the detached view in world units. Original 0x458 divided by four.")]
    private float mfHeight = 278f;

    /// <summary>The view points slightly downwards. The reference sets -1024 in its
    /// angle measure, which is 5.625 degrees - negative there because its coordinate system pitches
    /// the other way round (see UWViewpoint.PitchToDegrees).</summary>
    [SerializeField]
    [Tooltip("Downward pitch angle in degrees. Original -1024, i.e. 5.625 degrees.")]
    private float mfPitchDegrees = 5.625f;

    /// <summary>
    /// How fast the view turns and travels.
    ///
    /// CONVERTED, NOT GUESSED: the reference turns per tick by 0x400 = 1024 heading units
    /// (5.625 degrees) and travels about 128 original units (32 world units, half a
    /// tile). Its tick is 0.097659 seconds, i.e. 10.24 ticks per second - giving 57.6 degrees
    /// and 327.68 world units per second.
    ///
    /// FOR COMPARISON: the figure walks at about 200 and turns at 105 degrees per second. So the
    /// view glides one and a half times as fast and turns half as fast as the figure. That is
    /// not a calculation error, but it is noticeable when trying it out - and the reference's tick is
    /// itself marked there as an estimate from breakpoints. Hence adjustable.
    ///
    /// READ 2026-09-25 (ovr134_AF7): the original steps once PER FRAME, steered only by the
    /// pointer's third of the view window, so its speed is its frame rate - not measured yet
    /// (Todo.md, marker audit row 24). So ours steps by the original's amounts - 0x400 of the
    /// heading (5.625 degrees) and the sine or cosine >> 8 (half a tile, 32 world units) - at
    /// mfOriginalFramesPerSecond, which stays the reference's 10.24 until the measurement.
    /// </summary>
    [SerializeField]
    [Tooltip("How many frames a second the original draws while the view roams - each frame turns 5.625 degrees or travels half a tile. 10.24 is the reference's estimated tick; replace it by the measured value.")]
    private float mfOriginalFramesPerSecond = 10.24f;

    /// <summary>One frame's turn: 0x400 of the 16-bit heading.</summary>
    private const float TurnDegreesPerFrame = 5.625f;

    /// <summary>One frame's travel: the sine or cosine (up to 0x7FFF) >> 8, of 0x100 a tile -
    /// half a tile.</summary>
    private const float MoveUnitsPerFrame = 32f;

    private float mfTurnDegreesPerSecond => TurnDegreesPerFrame * mfOriginalFramesPerSecond;

    /// <summary>
    /// TRAVEL AT WALKING SPEED (per user, 2026-09-27: the turn feels right, "straight ahead I
    /// would take the walking speed"). The original has no speed of its own to copy - it steps
    /// once per drawn frame, unthrottled, far too fast in DOSBox - and half a tile per step at
    /// 10.24 steps a second glided one and a half times as fast as the figure walks. So the
    /// view travels as fast as the figure walks on normal ground in the current scheme; the
    /// turn stays 5.625 degrees per original step.
    /// </summary>
    private float mfMoveUnitsPerSecond
    {
        get
        {
            UWPlayerMovement lOMovement = UWScene.PlayerMovement;

            return lOMovement != null ? lOMovement.NormalForwardSpeed : MoveUnitsPerFrame * mfOriginalFramesPerSecond;
        }
    }

    /// <summary>The reference clamps the view to 0x180 to 0x3D80, that is the centre of
    /// tile 1 to the centre of tile 61. No height change, no collision check.
    /// </summary>
    private const float MinAxis = 64f;

    private const float MaxAxis = 3904f;

    private int miClaim;

    private UWControlScheme mOScheme;

    public bool IsRunning
    {
        get
        {
            return miClaim > 0 && UWRemoteCamera.Current != null
                && UWRemoteCamera.Current.HasClaim(miClaim);
        }
    }

    /// <summary>Whether the player's view is roaming right now - the options button does not
    /// open meanwhile (UWHudOptions), as in the original.</summary>
    public static bool IsAnyRunning
    {
        get
        {
            UWCharacter lOCharacter = UWScene.Character;
            UWRoamingSight lOSight = lOCharacter != null ? lOCharacter.GetComponent<UWRoamingSight>() : null;

            return lOSight != null && lOSight.IsRunning;
        }
    }

    /// <summary>Detaches the view. Can be called repeatedly - UWCharacter.fApplyActiveSpells runs
    /// on every spell change and on every twenty-second tick.</summary>
    public void Begin(Vector3 pOBodyPosition, float pfBodyYaw)
    {
        if (IsRunning)
            return;

        UWRemoteCamera lOCamera = UWRemoteCamera.Ensure();

        miClaim = lOCamera.Begin(UWRemoteCamera.SpellPriority,
            new Vector3(pOBodyPosition.x, mfHeight, pOBodyPosition.z),
            pfBodyYaw, mfPitchDegrees, false, fOnCameraEnded);
    }

    public void Stop()
    {
        if (miClaim <= 0)
            return;

        int liClaim = miClaim;

        // Remember first, then clear: the callback should not try to end the spell a second
        // time.
        miClaim = 0;

        if (UWRemoteCamera.Current != null)
            UWRemoteCamera.Current.End(liClaim);
    }

    /// <summary>The camera was dropped from outside - on a level change, for example. Then
    /// the spell must end as well, otherwise it would stay in the icon bar without a view.</summary>
    private void fOnCameraEnded()
    {
        if (miClaim <= 0)
            return;

        miClaim = 0;

        UWCharacter lOCharacter = GetComponent<UWCharacter>();

        if (lOCharacter != null)
            lOCharacter.EndSpellEffect(UWMiscSpell.MajorClass, UWMiscSpell.RoamingSightEffectMinor);
    }

    private void Update()
    {
        if (miClaim <= 0)
            return;

        UWRemoteCamera lOCamera = UWRemoteCamera.Current;

        if (lOCamera == null || !lOCamera.HasClaim(miClaim))
        {
            miClaim = 0;

            return;
        }

        // If the trap view is on top, nothing moves here - the view belongs to it
        // for that time, and the key press that ends it should not also move the view.
        if (!lOCamera.IsClaimActive(miClaim))
            return;

        if (mOScheme == null)
            mOScheme = UWScene.ControlScheme;

        if (mOScheme == null || mOScheme.Controls == null)
            return;

        // IN THE MODERN SCHEME THERE IS NO WAY OUT VIA THE SPELL ICON: the classic
        // interface is switched off there, so the icon bar cannot be clicked. Without
        // this line one would be trapped in the view until expiry. DELIBERATE ADDITION - the
        // original has no modern scheme.
        if (mOScheme.Current == UWControlScheme.SchemeEnum.Modern
            && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Stop();

            return;
        }

        Vector3 lOPosition;
        float lfYaw;
        float lfPitch;

        if (!lOCamera.GetPose(miClaim, out lOPosition, out lfYaw, out lfPitch))
            return;

        float lfTurn;
        float lfForward;

        // THE KEYS STEER IN BOTH SCHEMES: turning on Turn and the X axis of Move, travel on the
        // Y axis of Move. In the original scheme that is a DELIBERATE DEVIATION (per user,
        // 2026-09-27: the original steers by the pointer alone, which is clumsy - "I would allow
        // the keyboard anyway"); the pointer's thirds with the left button add to it.
        lfTurn = mOScheme.Controls.Player.Turn.ReadValue<float>()
            + mOScheme.Controls.Player.Move.ReadValue<Vector2>().x;
        lfForward = mOScheme.Controls.Player.Move.ReadValue<Vector2>().y;

        if (mOScheme.Current == UWControlScheme.SchemeEnum.Original)
        {
            fReadPointerThirds(out float lfPointerTurn, out float lfPointerForward);

            lfTurn += lfPointerTurn;
            lfForward += lfPointerForward;
        }

        lfTurn = Mathf.Clamp(lfTurn, -1f, 1f);
        lfForward = Mathf.Clamp(lfForward, -1f, 1f);

        lfYaw += lfTurn * mfTurnDegreesPerSecond * Time.deltaTime;

        lOPosition += Quaternion.Euler(0f, lfYaw, 0f) * Vector3.forward
            * lfForward * mfMoveUnitsPerSecond * Time.deltaTime;

        lOPosition.x = Mathf.Clamp(lOPosition.x, MinAxis, MaxAxis);
        lOPosition.z = Mathf.Clamp(lOPosition.z, MinAxis, MaxAxis);
        lOPosition.y = mfHeight;

        lOCamera.SetPose(miClaim, lOPosition, lfYaw, mfPitchDegrees);
    }

    /// <summary>
    /// THE ORIGINAL SCHEME STEERS BY THE POINTER (ovr134_AF7, marker audit row 24): the view
    /// window is cut into thirds each way - the left third turns left, the right third right,
    /// the top third travels forward, the bottom third back, the middle does nothing - every
    /// frame WHILE THE LEFT BUTTON IS HELD (per user in the original, 2026-09-27; the routine
    /// itself asks no button, so its caller must, as the walking in the view does). Ours
    /// steered with the movement keys in both schemes until 2026-09-27. Outside the view window
    /// nothing moves (what the original does with a pointer over the panels was not read).
    /// </summary>
    private void fReadPointerThirds(out float pfTurn, out float pfForward)
    {
        pfTurn = 0f;
        pfForward = 0f;

        UWGameUI lOGameUi = UWScene.GameUi;

        if (lOGameUi == null || Mouse.current == null || !Mouse.current.leftButton.isPressed)
            return;

        Vector2 lOPointer = Mouse.current.position.ReadValue();

        if (!lOGameUi.IsScreenPositionInGameArea(lOPointer))
            return;

        UWPlayerThrow.GetPointerInView(lOGameUi, lOPointer, out int liX, out int liY);

        // x * 3 / width and y * 3 / height as the original divides; its y counts from the top,
        // GetPointerInView's from the bottom.
        int liColumn = Mathf.Clamp((liX * 3) / (ViewWidth + 1), 0, 2);
        int liRow = Mathf.Clamp(((ViewHeight - liY) * 3) / (ViewHeight + 1), 0, 2);

        pfTurn = liColumn - 1;
        pfForward = 1 - liRow;
    }

    /// <summary>The view window in the original's pixels, 0 to 0xAC and 0 to 0x71
    /// (UWPlayerThrow.GetPointerInView).</summary>
    private const int ViewWidth = 0xAC;

    private const int ViewHeight = 0x71;
}
