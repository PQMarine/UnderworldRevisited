using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// THE GAMEPAD'S LETTER GRID (per user, 2026-10-07: no on-screen keyboard, but a letter grid as the
/// streaming services have it): A to Z, the digits and a few signs in rows of ten, below them
/// case, space, delete, cancel and done. It comes up by itself while the game asks for text -
/// the character's name, a save game's name, a mantra, a conversation's typed answer, the
/// "how many" prompt, a map note (UWGameUI.IsTypingText) - and the gamepad was used last; a key
/// on the real keyboard puts it away until that input ends.
///
/// It TYPES REAL KEYS (UWOsKeys), so every kind of text input takes them as from the keyboard.
/// The d-pad or the left stick moves, A types the key, B deletes, X is a space, Y switches the
/// case, Start is done (Enter). A word begins with a capital, the rest is small, unless Y chose.
/// The buttons are read by their place on the pad, not through the bindings.
/// </summary>
public class UWLetterGrid : MonoBehaviour
{
    /// <summary>The grid is up and has the gamepad (UWGamepadPointer stands aside, UWGameUI's B).</summary>
    public static bool IsShown { get; private set; }

    private static readonly string[] msRows = { "ABCDEFGHIJ", "KLMNOPQRST", "UVWXYZ'-.,", "1234567890" };

    private static readonly string[] msBottom = { "Aa", "Space", "Delete", "Cancel", "Done" };

    private const int Columns = 10;

    /// <summary>The bottom row's keys are two columns wide.</summary>
    private const int BottomRow = 4;

    // The layout, on 720 lines (OnGUI).
    private const float KeyWidth = 50f;

    private const float KeyHeight = 42f;

    private const float Gap = 4f;

    private const float Pad = 12f;

    private const float HintHeight = 32f;

    private const float BottomMargin = 16f;

    private const float GridHeight = ((BottomRow + 1) * (KeyHeight + Gap)) - Gap + (2f * Pad) + HintHeight;

    /// <summary>Screen pixels at the bottom the grid takes while it is up, with a little room above
    /// it - the modern conversation lifts its answers above them (per user, 2026-10-07: the grid
    /// lay over the typed answer's field).</summary>
    public static float ReservedPixels => IsShown && !fIsAtTop() ? (GridHeight + BottomMargin + 8f) * (Screen.height / 720f) : 0f;

    /// <summary>The classic conversation's answer stands on the original's scroll at the bottom,
    /// which cannot move: there the grid goes to the top.</summary>
    private static bool fIsAtTop()
    {
        UWControlScheme lOScheme = UWScene.ControlScheme;

        return UWConversationScreen.IsTypingAnswer
            && (lOScheme == null || lOScheme.Current == UWControlScheme.SchemeEnum.Original);
    }

    private const float RepeatDelaySeconds = 0.35f;

    private const float RepeatSeconds = 0.09f;

    private int miRow;

    private int miColumn;

    private bool mbUpper = true;

    /// <summary>Y chose the case: no automatic capital until the next word.</summary>
    private bool mbCaseByHand;

    private bool mbTyping;

    private bool mbDismissed;

    private Vector2Int mOHeld;

    private float mfNextRepeat;

    private float mfOwnKeysUntil;

    private Texture2D mOWhite;

    private GUIStyle mOKeyStyle;

    private GUIStyle mOHintStyle;

    private static readonly Color msPanel = new Color(0.11f, 0.08f, 0.05f, 0.94f);

    private static readonly Color msKey = new Color(0.25f, 0.19f, 0.12f, 1f);

    private static readonly Color msFocus = new Color(0.925f, 0.77f, 0.44f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void fCreate()
    {
        GameObject lOObject = new GameObject("UWLetterGrid");
        DontDestroyOnLoad(lOObject);
        lOObject.AddComponent<UWLetterGrid>();
    }

    private static bool fIsTyping()
    {
        if (UWCharacterCreationScreen.IsTypingName)
            return true;

        UWGameUI lOUi = UWScene.GameUi;

        return lOUi != null && lOUi.IsTypingText;
    }

    private void Update()
    {
        // A capital's Shift, held over from the last frame on Linux (UWOsKeys).
        UWOsKeys.ReleaseHeld();

        if (!fIsTyping())
        {
            mbTyping = false;
            mbDismissed = false;
            IsShown = false;
            return;
        }

        // A new input: from the top left, a capital first.
        if (!mbTyping)
        {
            mbTyping = true;
            miRow = 0;
            miColumn = 0;
            mbUpper = true;
            mbCaseByHand = false;
            mOHeld = Vector2Int.zero;
        }

        Keyboard lOKeyboard = Keyboard.current;

        // A real key puts it away - not a system shortcut (a screenshot, UWGamepad.IsSystemShortcut).
        if (lOKeyboard != null && lOKeyboard.anyKey.wasPressedThisFrame && Time.unscaledTime > mfOwnKeysUntil
            && !UWGamepad.IsSystemShortcut)
            mbDismissed = true;

        // ANY PAD BUTTON BRINGS IT BACK (per user, 2026-10-08: once a key had put it away, the pad
        // could not) - that press types nothing.
        if (mbDismissed && UWGamepad.AnyPressed(true))
        {
            mbDismissed = false;
            IsShown = UWGamepad.IsActive;
            return;
        }

        IsShown = !mbDismissed && UWGamepad.IsActive;

        Gamepad lOPad = Gamepad.current;

        if (!IsShown || lOPad == null)
            return;

        fNavigate(lOPad);

        if (lOPad.buttonSouth.wasPressedThisFrame)
            fActivate();
        else if (lOPad.buttonEast.wasPressedThisFrame)
            fSendKey(UWOsKeys.KeyEnum.Backspace);
        else if (lOPad.buttonWest.wasPressedThisFrame)
            fType(' ');
        else if (lOPad.buttonNorth.wasPressedThisFrame)
            fToggleCase();
        else if (lOPad.startButton.wasPressedThisFrame)
            fSendKey(UWOsKeys.KeyEnum.Enter);
    }

    /// <summary>The d-pad or the left stick, a step on the push and repeating while held.</summary>
    private void fNavigate(Gamepad pOPad)
    {
        Vector2 lOStick = pOPad.leftStick.ReadValue();
        Vector2 lODpad = pOPad.dpad.ReadValue();
        Vector2 lOPush = lODpad.sqrMagnitude > 0.25f ? lODpad : lOStick;
        Vector2Int lODirection = Vector2Int.zero;

        if (Mathf.Abs(lOPush.x) >= 0.5f && Mathf.Abs(lOPush.x) >= Mathf.Abs(lOPush.y))
            lODirection.x = lOPush.x > 0f ? 1 : -1;
        else if (Mathf.Abs(lOPush.y) >= 0.5f)
            lODirection.y = lOPush.y > 0f ? -1 : 1;

        if (lODirection == Vector2Int.zero)
        {
            mOHeld = Vector2Int.zero;
            return;
        }

        if (lODirection == mOHeld && Time.unscaledTime < mfNextRepeat)
            return;

        mfNextRepeat = Time.unscaledTime + (lODirection == mOHeld ? RepeatSeconds : RepeatDelaySeconds);
        mOHeld = lODirection;

        // The bottom row's keys span two columns: sideways there by two.
        int liStep = miRow == BottomRow ? 2 : 1;

        miColumn = (miColumn + (lODirection.x * liStep) + Columns) % Columns;
        miRow = (miRow + lODirection.y + BottomRow + 1) % (BottomRow + 1);

        if (miRow == BottomRow)
            miColumn -= miColumn % 2;
    }

    private void fActivate()
    {
        if (miRow < BottomRow)
        {
            fType(msRows[miRow][miColumn]);
            return;
        }

        switch (miColumn / 2)
        {
            case 0:
                fToggleCase();
                break;
            case 1:
                fType(' ');
                break;
            case 2:
                fSendKey(UWOsKeys.KeyEnum.Backspace);
                break;
            case 3:
                fSendKey(UWOsKeys.KeyEnum.Escape);
                break;
            default:
                fSendKey(UWOsKeys.KeyEnum.Enter);
                break;
        }
    }

    private void fType(char pcChar)
    {
        if (char.IsLetter(pcChar))
            pcChar = mbUpper ? char.ToUpperInvariant(pcChar) : char.ToLowerInvariant(pcChar);

        mfOwnKeysUntil = Time.unscaledTime + 0.3f;
        UWOsKeys.Type(pcChar);

        // A word begins with a capital, the rest small - unless Y chose.
        if (pcChar == ' ')
        {
            mbCaseByHand = false;
            mbUpper = true;
        }
        else if (!mbCaseByHand && char.IsLetter(pcChar))
        {
            mbUpper = false;
        }
    }

    private void fSendKey(UWOsKeys.KeyEnum peKey)
    {
        mfOwnKeysUntil = Time.unscaledTime + 0.3f;
        UWOsKeys.Press(peKey);
    }

    private void fToggleCase()
    {
        mbUpper = !mbUpper;
        mbCaseByHand = true;
    }

    // ------------------------------------------------- Drawing

    private void OnGUI()
    {
        if (!IsShown || Event.current.type != EventType.Repaint)
            return;

        fEnsureStyles();

        // Laid out on 720 lines, scaled to the screen, at the bottom in the middle (at the top for
        // the classic conversation, fIsAtTop).
        float lfScale = Screen.height / 720f;
        float lfWidth = (Columns * KeyWidth) + ((Columns - 1) * Gap) + (2f * Pad);
        float lfHeight = GridHeight;
        float lfLeft = ((Screen.width / lfScale) - lfWidth) * 0.5f;
        float lfTop = fIsAtTop() ? BottomMargin : fBottom(lfScale) - lfHeight;

        Matrix4x4 lOBefore = GUI.matrix;
        int liDepth = GUI.depth;

        GUI.matrix = Matrix4x4.Scale(new Vector3(lfScale, lfScale, 1f));
        GUI.depth = -1000;

        fFill(new Rect(lfLeft, lfTop, lfWidth, lfHeight), msPanel);

        for (int liRow = 0; liRow <= BottomRow; liRow++)
        {
            float lfY = lfTop + Pad + (liRow * (KeyHeight + Gap));
            int liKeys = liRow < BottomRow ? Columns : msBottom.Length;

            for (int liKey = 0; liKey < liKeys; liKey++)
            {
                int liSpan = liRow < BottomRow ? 1 : 2;
                float lfX = lfLeft + Pad + (liKey * liSpan * (KeyWidth + Gap));
                float lfW = (liSpan * KeyWidth) + ((liSpan - 1) * Gap);
                bool lbFocus = liRow == miRow && (liRow < BottomRow ? liKey == miColumn : liKey == miColumn / 2);
                string lsLabel = liRow < BottomRow ? fShown(msRows[liRow][liKey]) : msBottom[liKey];

                fFill(new Rect(lfX, lfY, lfW, KeyHeight), lbFocus ? msFocus : msKey);
                mOKeyStyle.normal.textColor = lbFocus ? Color.black : Color.white;
                GUI.Label(new Rect(lfX, lfY, lfW, KeyHeight), lsLabel, mOKeyStyle);
            }
        }

        fDrawHints(lfLeft, lfTop + lfHeight - Pad - 26f, lfWidth);

        GUI.matrix = lOBefore;
        GUI.depth = liDepth;
    }

    /// <summary>
    /// Where the grid's bottom edge lies, on 720 lines from the top: the screen's bottom margin -
    /// IN THE CLASSIC SCHEME above the message scroll, where the mantra and the input line roll
    /// and the grid lay over the active line (per user, 2026-10-07). Not for the character's
    /// name, whose screen has no scroll.
    /// </summary>
    private static float fBottom(float pfScale)
    {
        UWControlScheme lOScheme = UWScene.ControlScheme;
        UWGameUI lOUi = UWScene.GameUi;

        if (lOUi == null || UWCharacterCreationScreen.IsTypingName
            || lOScheme == null || lOScheme.Current != UWControlScheme.SchemeEnum.Original)
            return 720f - BottomMargin;

        float lfScrollTop = (Screen.height - lOUi.MessageLogTopScreenY) / pfScale;

        return Mathf.Min(720f - BottomMargin, lfScrollTop - 6f);
    }

    private static readonly string[] msHintPaths =
    {
        "<Gamepad>/buttonSouth", "<Gamepad>/buttonEast", "<Gamepad>/buttonWest", "<Gamepad>/buttonNorth", "<Gamepad>/start"
    };

    private static readonly string[] msHintTexts = { "type", "delete", "space", "Aa", "done" };

    /// <summary>
    /// The hint line below the keys: each button as its GLYPH in the chosen style (per user,
    /// 2026-10-08; UWGlyphs) - its name where a glyph is missing - and what it does, centred.
    /// </summary>
    private void fDrawHints(float pfLeft, float pfTop, float pfWidth)
    {
        const float GlyphSize = 24f;
        const float GlyphGap = 4f;
        const float ItemGap = 18f;

        Texture2D[] lyGlyphs = new Texture2D[msHintPaths.Length];
        string[] lsLabels = new string[msHintPaths.Length];
        float lfTotal = 0f;

        for (int liItem = 0; liItem < msHintPaths.Length; liItem++)
        {
            lyGlyphs[liItem] = UWGlyphs.ForPath(msHintPaths[liItem]);
            lsLabels[liItem] = lyGlyphs[liItem] != null ? msHintTexts[liItem]
                : UWGamepad.ButtonName(msHintPaths[liItem]) + " " + msHintTexts[liItem];

            lfTotal += (lyGlyphs[liItem] != null ? GlyphSize + GlyphGap : 0f)
                + mOHintStyle.CalcSize(new GUIContent(lsLabels[liItem])).x + (liItem > 0 ? ItemGap : 0f);
        }

        float lfX = pfLeft + ((pfWidth - lfTotal) * 0.5f);

        for (int liItem = 0; liItem < msHintPaths.Length; liItem++)
        {
            if (liItem > 0)
                lfX += ItemGap;

            if (lyGlyphs[liItem] != null)
            {
                GUI.DrawTexture(new Rect(lfX, pfTop, GlyphSize, GlyphSize), lyGlyphs[liItem]);
                lfX += GlyphSize + GlyphGap;
            }

            float lfTextWidth = mOHintStyle.CalcSize(new GUIContent(lsLabels[liItem])).x;

            GUI.Label(new Rect(lfX, pfTop, lfTextWidth, GlyphSize), lsLabels[liItem], mOHintStyle);
            lfX += lfTextWidth;
        }
    }

    private string fShown(char pcChar)
    {
        return (char.IsLetter(pcChar) && !mbUpper ? char.ToLowerInvariant(pcChar) : pcChar).ToString();
    }

    private void fFill(Rect pORect, Color pOColour)
    {
        Color lOBefore = GUI.color;

        GUI.color = pOColour;
        GUI.DrawTexture(pORect, mOWhite);
        GUI.color = lOBefore;
    }

    private void fEnsureStyles()
    {
        if (mOWhite == null)
            mOWhite = Texture2D.whiteTexture;

        if (mOKeyStyle != null)
            return;

        mOKeyStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };

        // No wrapping: the hint labels are as wide as their text, and a rounding broke them in two
        // (per user, 2026-10-08).
        mOHintStyle = new GUIStyle(GUI.skin.label)
        {
            wordWrap = false,
            clipping = TextClipping.Overflow,
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14
        };

        mOHintStyle.normal.textColor = new Color(0.85f, 0.8f, 0.7f);
    }
}
