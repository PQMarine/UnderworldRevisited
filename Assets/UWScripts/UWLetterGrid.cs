using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE GAMEPAD'S LETTER GRID (per user, 2026-10-07: no on-screen keyboard, but a letter grid as the
/// streaming services have it): A to Z, the digits and a few signs in rows of ten, below them
/// case, space, delete, cancel and done. Since translation 5.1 (2026-10-26) the language's alphabet
/// stands for A to Z and its other letters (the German umlauts and sharp s) get a row of their own
/// above the digits - the keys and the moves are UWLetterGridLayout's (engine-free, checked); English
/// keeps exactly the old grid. A switch of language reaches the grid at once, open or not.
///
/// It comes up by itself while the game asks for text -
/// the character's name, a save game's name, a mantra, a conversation's typed answer, the
/// "how many" prompt, a map note (UWTextEntryState.IsTypingText) - and the gamepad was used last; a key
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

    /// <summary>The bottom row's keys, ids in the text table (cleanup step 8.5, 2026-10-24).</summary>
    private static readonly string[] msBottomIds = { "grid.key.case", "grid.key.space", "grid.key.delete", "grid.key.cancel", "grid.key.done" };

    private const int Columns = UWLetterGridLayout.Columns;

    // The layout, on 720 lines (LateUpdate; OnGUI until cleanup 8b.4).
    private const float KeyWidth = 50f;

    private const float KeyHeight = 42f;

    private const float Gap = 4f;

    private const float Pad = 12f;

    private const float HintHeight = 32f;

    private const float BottomMargin = 16f;

    /// <summary>The grid's height on 720 lines: its rows (five in English, one more per row of the
    /// language's other letters or of a longer alphabet) and the hint line.</summary>
    private static float GridHeight => (UWLetterGridLayout.Current.Rows * (KeyHeight + Gap)) - Gap + (2f * Pad) + HintHeight;

    /// <summary>Screen pixels at the bottom the grid takes while it is up, with a little room above
    /// it - the modern conversation lifts its answers above them (per user, 2026-10-07: the grid
    /// lay over the typed answer's field).</summary>
    public static float ReservedPixels => IsShown && !fIsAtTop() ? (GridHeight + BottomMargin + 8f) * UWGuiTheme.Scale : 0f;

    /// <summary>The classic conversation's answer stands on the original's scroll at the bottom,
    /// which cannot move: there the grid goes to the top.</summary>
    private static bool fIsAtTop()
    {
        UWControlScheme lOScheme = UWScene.ControlScheme;

        return UWConversationController.IsTypingAnswer
            && (lOScheme == null || UWControlScheme.Ui.Policy.AnswerTypedOnScroll);
    }

    private const float RepeatDelaySeconds = 0.35f;

    private const float RepeatSeconds = 0.09f;

    /// <summary>The direction held and when it steps again (UWRepeat since cleanup step 7.3,
    /// 2026-10-25): a step on the push, again after RepeatDelaySeconds, then RepeatSeconds after
    /// each step; another direction is a new push.</summary>
    private readonly UWRepeat mORepeat = new UWRepeat(RepeatDelaySeconds, RepeatSeconds, UWRepeat.RuleEnum.FromNow);

    private int miRow;

    private int miColumn;

    /// <summary>The column the cursor keeps through a short row (UWLetterGridLayout.Move).</summary>
    private int miWanted;

    /// <summary>The layout the cursor stands in; another one (a switch of language) puts it back inside.</summary>
    private UWLetterGridLayout mOLayout;

    private bool mbUpper = true;

    /// <summary>Y chose the case: no automatic capital until the next word.</summary>
    private bool mbCaseByHand;

    private bool mbTyping;

    private bool mbDismissed;


    private float mfOwnKeysUntil;

    // The panel's, the keys' and the focused key's colours are the theme's since cleanup phase 8d
    // (UWGuiTheme.Palette.LetterGridPanel, LetterGridKey, LetterGridFocus; Plain's are the values kept
    // here until then: a dark brown at 0.94, a lighter brown, UWModernPalette.Gold).

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

        return lOUi != null && lOUi.TextEntry.IsTypingText;
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
            miWanted = 0;
            mOLayout = UWLetterGridLayout.Current;
            mbUpper = true;
            mbCaseByHand = false;
            mORepeat.Reset();
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

        fFollowLanguage();
        fNavigate(lOPad);

        if (lOPad.buttonSouth.wasPressedThisFrame)
            fActivate();
        else if (UWBack.Takes(UWBackOwner.LetterGrid))
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

        // The direction as one number for the repeat, never 0 for a push (0 lets go).
        int liHeld = lODirection == Vector2Int.zero ? 0 : ((lODirection.x + 2) * 4) + lODirection.y + 2;

        if (!mORepeat.Hold(liHeld, Time.unscaledTime))
            return;

        // Sideways within the row's keys (two columns on the bottom row), up and down over the rows.
        mOLayout.Move(ref miRow, ref miColumn, ref miWanted, lODirection.x, lODirection.y);
    }

    /// <summary>The language's layout (UWLetterGridLayout.Current); when it changed while the grid is
    /// up - a switch of language - the cursor stays where it was as far as the new rows allow.</summary>
    private void fFollowLanguage()
    {
        UWLetterGridLayout lOLayout = UWLetterGridLayout.Current;

        if (ReferenceEquals(lOLayout, mOLayout))
            return;

        mOLayout = lOLayout;
        miRow = Mathf.Clamp(miRow, 0, lOLayout.BottomRow);
        miColumn = lOLayout.ClampColumn(miRow, miColumn);
        miWanted = miColumn;
    }

    private void fActivate()
    {
        if (miRow < mOLayout.BottomRow)
        {
            fType(mOLayout.KeyAt(miRow, mOLayout.KeyOfColumn(miRow, miColumn)).Shown(mbUpper));
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

    /// <summary>Types a character - a key's in the grid's case (UWLetterGridLayout.Key.Shown), or the
    /// space - as a real key, the same path for every letter (UWOsKeys).</summary>
    private void fType(char pcChar)
    {
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

    // ------------------------------------------------- Drawing on the kit (cleanup 8b.4)

    /// <summary>
    /// THE GRID ON THE KIT (cleanup 8b.4, 2026-10-24; phase 8b, decision 26): drawn by UWKitRows in the
    /// letter grid's look (the IMGUI skin's font, LegacyRuntime) on a canvas of its own
    /// (UWUiSorting.LetterGrid) in LateUpdate, from the same numbers as the IMGUI form in OnGUI (drawn
    /// in its Repaint only, GUI.depth -1000; gone at the end of phase 8b): on 720 lines, scaled to the
    /// screen, at the bottom in the middle (at the top for the classic conversation, fIsAtTop; above
    /// the classic message scroll otherwise, fBottom), the keys in rows of ten with the bottom row's
    /// keys two wide, the focused key gold with dark text, the hint line of glyphs and texts centred
    /// under them. It takes no input itself - the pad is read in Update - so the drawer's pointer is
    /// dropped.
    /// </summary>
    private UWKitRows mOKit;

    private void LateUpdate()
    {
        bool lbShown = IsShown;

        if (mOKit == null)
        {
            if (!lbShown)
                return;

            mOKit = new UWKitRows(transform, UWUiSorting.LetterGrid, UWRowLookEnum.LetterGrid);
        }

        mOKit.Begin(lbShown, false, false, false);

        if (!lbShown)
            return;

        mOKit.DropPointer();
        fFollowLanguage();

        UWLetterGridLayout lOLayout = mOLayout;
        int liBottomRow = lOLayout.BottomRow;

        float lfScale = UWGuiTheme.Scale;
        float lfWidth = (Columns * KeyWidth) + ((Columns - 1) * Gap) + (2f * Pad);
        float lfHeight = GridHeight;
        float lfLeft = (mOKit.Width - lfWidth) * 0.5f;
        float lfTop = fIsAtTop() ? BottomMargin : fBottom(lfScale) - lfHeight;
        UWKitRows.HelpLook lOKey = new UWKitRows.HelpLook
        {
            Size = UWGuiTheme.LetterGridKeySize, Colour = Color.white, Anchor = TextAnchor.MiddleCenter, Bold = true
        };

        UWGuiTheme.Palette lOPalette = UWGuiTheme.Current;

        mOKit.Ground(new Rect(lfLeft, lfTop, lfWidth, lfHeight), UWWindowGroundEnum.LetterGrid);

        // A short row's keys from the left, the cells after its last key empty (UWLetterGridLayout).
        for (int liRow = 0; liRow <= liBottomRow; liRow++)
        {
            float lfY = lfTop + Pad + (liRow * (KeyHeight + Gap));
            int liKeys = lOLayout.KeysIn(liRow);
            int liSpan = lOLayout.SpanOf(liRow);

            for (int liKey = 0; liKey < liKeys; liKey++)
            {
                float lfX = lfLeft + Pad + (liKey * liSpan * (KeyWidth + Gap));
                float lfW = (liSpan * KeyWidth) + ((liSpan - 1) * Gap);
                bool lbFocus = liRow == miRow && liKey == lOLayout.KeyOfColumn(liRow, miColumn);
                string lsLabel = liRow < liBottomRow ? lOLayout.KeyAt(liRow, liKey).Shown(mbUpper).ToString() : UWText.T(msBottomIds[liKey]);
                Rect lORect = new Rect(lfX, lfY, lfW, KeyHeight);

                lOKey.Colour = lbFocus ? Color.black : Color.white;
                mOKit.Fill(lORect, lbFocus ? lOPalette.LetterGridFocus : lOPalette.LetterGridKey);
                mOKit.HelpText(lORect, lsLabel, lOKey);
            }
        }

        fDrawKitHints(lfLeft, lfTop + lfHeight - Pad - 26f, lfWidth);

        mOKit.End();
    }

    /// <summary>
    /// The hint line below the keys: each button as its GLYPH in the chosen style (per user,
    /// 2026-10-08; UWGlyphs) - its name where a glyph is missing - and what it does, centred. On the
    /// kit each text as wide as the skin's label measured it (no padding left or right), on one line:
    /// no wrapping, the hint labels are as wide as their text, and a rounding broke them in two (per
    /// user, 2026-10-08).
    /// </summary>
    private void fDrawKitHints(float pfLeft, float pfTop, float pfWidth)
    {
        const float GlyphSize = 24f;
        const float GlyphGap = 4f;
        const float ItemGap = 18f;

        UWKitRows.HelpLook lOHint = new UWKitRows.HelpLook
        {
            Size = UWGuiTheme.LetterGridHintSize, Colour = UWGuiTheme.LetterGridHint, Anchor = TextAnchor.MiddleCenter
        };
        Texture2D[] lyGlyphs = new Texture2D[msHintPaths.Length];
        string[] lsLabels = new string[msHintPaths.Length];
        float[] lfWidths = new float[msHintPaths.Length];
        float lfTotal = 0f;

        for (int liItem = 0; liItem < msHintPaths.Length; liItem++)
        {
            lyGlyphs[liItem] = UWGlyphs.ForPath(msHintPaths[liItem]);
            string lsHint = UWText.T(msHintIds[liItem]);

            lsLabels[liItem] = lyGlyphs[liItem] != null ? lsHint
                : UWBindingInfo.ButtonName(msHintPaths[liItem]) + " " + lsHint;
            lfWidths[liItem] = mOKit.HelpTextWidth(lsLabels[liItem], lOHint);

            lfTotal += (lyGlyphs[liItem] != null ? GlyphSize + GlyphGap : 0f) + lfWidths[liItem] + (liItem > 0 ? ItemGap : 0f);
        }

        float lfX = pfLeft + ((pfWidth - lfTotal) * 0.5f);

        for (int liItem = 0; liItem < msHintPaths.Length; liItem++)
        {
            if (liItem > 0)
                lfX += ItemGap;

            if (lyGlyphs[liItem] != null)
            {
                mOKit.Picture(new Rect(lfX, pfTop, GlyphSize, GlyphSize), lyGlyphs[liItem]);
                lfX += GlyphSize + GlyphGap;
            }

            mOKit.HelpText(new Rect(lfX, pfTop, lfWidths[liItem], GlyphSize), lsLabels[liItem], lOHint);
            lfX += lfWidths[liItem];
        }
    }

    private void OnDestroy()
    {
        if (mOKit != null)
            mOKit.Dispose();
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
            || lOScheme == null || !UWControlScheme.Ui.Policy.AnswerTypedOnScroll)
            return UWGuiTheme.ReferenceHeight - BottomMargin;

        float lfScrollTop = (Screen.height - lOUi.ClassicScreen.MessageLogTopScreenY) / pfScale;

        return Mathf.Min(UWGuiTheme.ReferenceHeight - BottomMargin, lfScrollTop - 6f);
    }

    private static readonly string[] msHintPaths =
    {
        "<Gamepad>/buttonSouth", "<Gamepad>/buttonEast", "<Gamepad>/buttonWest", "<Gamepad>/buttonNorth", "<Gamepad>/start"
    };

    /// <summary>What each button does, ids in the text table (cleanup step 8.5, 2026-10-24).</summary>
    private static readonly string[] msHintIds = { "grid.hint.type", "grid.hint.delete", "grid.hint.space", "grid.hint.case", "grid.hint.done" };
}
