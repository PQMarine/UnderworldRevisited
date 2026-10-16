using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// Playing instruments - mandolin and flute (per user, 2026-09-14: without this the Cup of
/// Wonder, and with it the ending, cannot be reached).
///
/// PER UW.EXE (PlayMusicalInstrument_seg014_1195, SpawnCupOfWonder_seg014_14C3):
///   Start      "You play the instrument.  (Use 0-9 to play, or ESC to return to game)"
///              Timbre per instrument from the table at dseg_164: mandolin 0x39, flute 0x48.
///   Notes      keys 1-9 and 0 from the table at dseg_166: 60 62 64 65 67 69 71 72 74 76
///              (C D E F G A B C D E). Alt (key bit 0x200) raises by an octave, Ctrl (0x100)
///              lowers (play_instrument, UnderworldExhumed sound/SOUND.C, read 2026-10-15;
///              until then Shift raised here).
///   Duration   the note sounds until the next one comes or 0x40 ticks of the 1/256 s game
///              clock have passed - a quarter of a second (NoteSeconds; until 2026-10-15 a
///              tuning value of 0.35 s, "not verified").
///   Memory     every note goes into a ring of 16 places, starting from the first note.
///   End        ESC: "You put the instrument down." - NOT when the cup appears: then its own
///              line stands alone (play_instrument returns before 0xFB; until 2026-10-15
///              ours printed both).
///   Cup        only with the flute, on level 3, at most 2 tiles away from 24/45, as long as
///              the cup has not appeared yet: the first nine notes equal "@CA>@GHGC"
///              (64 67 65 62 64 71 72 71 67, keys 3 5 4 2 3 7 8 7 5). Then the cup lies in
///              the hand, "An object appears in the air and falls into your hand...", and
///              PLAYER.DAT 0x60 bit 7 is set.
///
/// While playing, the keyboard is locked for everything else (UWGameUI.IsTextEntryActive).
/// </summary>
public class UWInstrumentPlayer : MonoBehaviour
{
    public const int MandolinObjectId = 291;

    public const int FluteObjectId = 292;

    private const int CupOfWonderObjectId = UWObjectIds.CupOfWonder;

    /// <summary>String block 1 (our export; UW.EXE is one lower).</summary>
    private const int StartMessage = 251;

    private const int EndMessage = 252;

    private const int CupMessage = 137;

    private static readonly int[] msPrograms = { 0x39, 0x48 };

    private static readonly int[] msNotes = { 60, 62, 64, 65, 67, 69, 71, 72, 74, 76 };

    private static readonly int[] msCupMelody = { 64, 67, 65, 62, 64, 71, 72, 71, 67 };

    /// <summary>Where the cup lies, zero-based level and tile (UWLevelIds, UWQuestPlaces - the
    /// same place UWShrineRules names one-based).</summary>
    private const int CupLevelIndex = UWLevelIds.CupOfWonder;

    private const int CupTileX = UWQuestPlaces.CupTileX;

    private const int CupTileY = UWQuestPlaces.CupTileY;

    private const int CupRange = 2;

    private const int RingSize = 16;

    /// <summary>How long a note sounds without a next one: 0x40 ticks of the 1/256 s game clock
    /// (play_instrument, GAME_TIME).</summary>
    private const float NoteSeconds = 0x40 / 256f;

    /// <summary>Is someone playing right now? Locks the keyboard (see UWGameUI).</summary>
    public static bool IsPlaying { get; private set; }

    private readonly int[] miRing = new int[RingSize];

    private int miRingAt;

    private int miInstrument;

    private int miSoundingNote = -1;

    private float mfNoteOffTime;

    private int miStartFrame;

    private Interaction mOInteraction;

    private UWInventory mOInventory;

    private UWLevelLoader mOLevelLoader;

    public static bool IsInstrument(int piObjectId)
    {
        return piObjectId == MandolinObjectId || piObjectId == FluteObjectId;
    }

    /// <summary>Takes up the instrument. Returns false if it is not one.</summary>
    public static bool Begin(GameObject pOHost, UWObject pOItem)
    {
        if (pOHost == null || pOItem == null || !IsInstrument(pOItem.ID) || IsPlaying)
            return false;

        UWInstrumentPlayer lOPlayer = pOHost.GetComponent<UWInstrumentPlayer>();

        if (lOPlayer == null)
            lOPlayer = pOHost.AddComponent<UWInstrumentPlayer>();

        lOPlayer.fBegin(pOItem.ID - MandolinObjectId);

        return true;
    }

    private void fBegin(int piInstrument)
    {
        if (mOInteraction == null)
            mOInteraction = GetComponent<Interaction>();

        if (mOInventory == null)
            mOInventory = GetComponent<UWInventory>();

        if (mOLevelLoader == null)
            mOLevelLoader = UWScene.LevelLoader;

        miInstrument = Mathf.Clamp(piInstrument, 0, msPrograms.Length - 1);
        miRingAt = 0;
        System.Array.Clear(miRing, 0, miRing.Length);
        miSoundingNote = -1;
        miStartFrame = Time.frameCount;
        IsPlaying = true;

        if (mOInteraction != null)
            mOInteraction.AddGeneralMessage(StartMessage);

        if (UWAudioEngine.Instance != null)
            UWAudioEngine.Instance.BeginInstrument(msPrograms[miInstrument]);
    }

    private void Update()
    {
        if (!IsPlaying)
            return;

        // The click that took up the instrument should not already play a note.
        if (Time.frameCount == miStartFrame)
            return;

        if (miSoundingNote >= 0 && Time.time >= mfNoteOffTime)
            fStopNote();

        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard == null)
            return;

        if (lOKeyboard.escapeKey.wasPressedThisFrame)
        {
            fEnd();
            return;
        }

        for (int liDigit = 0; liDigit <= 9; liDigit++)
        {
            if (!fDigitPressed(lOKeyboard, liDigit))
                continue;

            // Key 1 is the first note, key 0 the tenth.
            int liNote = msNotes[liDigit == 0 ? 9 : liDigit - 1];

            if (lOKeyboard.altKey.isPressed)
                liNote += 12;

            if (lOKeyboard.ctrlKey.isPressed)
                liNote -= 12;

            fPlayNote(liNote);
            break;
        }
    }

    private static bool fDigitPressed(Keyboard pOKeyboard, int piDigit)
    {
        Key leKey = piDigit == 0 ? Key.Digit0 : (Key)((int)Key.Digit1 + piDigit - 1);
        Key leNumpad = (Key)((int)Key.Numpad0 + piDigit);

        return pOKeyboard[leKey].wasPressedThisFrame || pOKeyboard[leNumpad].wasPressedThisFrame;
    }

    private void fPlayNote(int piNote)
    {
        if (miSoundingNote >= 0)
            fStopNote();

        miRing[miRingAt] = piNote;
        miRingAt = (miRingAt + 1) % RingSize;

        if (UWAudioEngine.Instance != null)
            UWAudioEngine.Instance.PlayInstrumentNote(piNote);

        miSoundingNote = piNote;
        mfNoteOffTime = Time.time + NoteSeconds;
    }

    private void fStopNote()
    {
        if (miSoundingNote >= 0 && UWAudioEngine.Instance != null)
            UWAudioEngine.Instance.StopInstrumentNote(miSoundingNote);

        miSoundingNote = -1;
    }

    private void fEnd()
    {
        fStopNote();
        fReleaseChannel();
        IsPlaying = false;

        // The cup's line stands alone: play_instrument returns before 0xFB when it appears.
        if (!fTryCupOfWonder() && mOInteraction != null)
            mOInteraction.AddGeneralMessage(EndMessage);
    }

    private void OnDisable()
    {
        if (IsPlaying)
        {
            fStopNote();
            fReleaseChannel();
            IsPlaying = false;
        }
    }

    /// <summary>The channel goes back to the music, as UW.EXE releases the one it got from the
    /// driver (matters on the MT-32 - UWAudioEngine.EndInstrument).</summary>
    private static void fReleaseChannel()
    {
        if (UWAudioEngine.Instance != null)
            UWAudioEngine.Instance.EndInstrument();
    }

    /// <summary>The Cup of Wonder - see the class comment. True when it appeared (play_cup_tune's
    /// answer, which spares the end line).</summary>
    private bool fTryCupOfWonder()
    {
        if (miInstrument != FluteObjectId - MandolinObjectId || UWEndgame.CupOfWonderFound
            || mOLevelLoader == null || mOLevelLoader.CurrentLevelIndex != CupLevelIndex
            || Camera.main == null)
            return false;

        UWTilePos lOTile = mOLevelLoader.WorldPositionToTile(Camera.main.transform.position);

        if (Mathf.Abs(lOTile.X - CupTileX) > CupRange || Mathf.Abs(lOTile.Y - CupTileY) > CupRange)
            return false;

        for (int liAt = 0; liAt < msCupMelody.Length; liAt++)
        {
            if (miRing[liAt] != msCupMelody[liAt])
                return false;
        }

        UWObject lOCup = fCreateCup();

        if (lOCup == null || mOInventory == null)
            return false;

        // The pointer, else the backpack, else the feet (UWInventoryModel.GiveToPlayer, one
        // behaviour for the four gifts since 2026-10-12; until then a full backpack kept the cup).
        if (!mOInventory.GiveToPlayer(lOCup, false))
            return false;

        UWEndgame.CupOfWonderFound = true;

        if (mOInteraction != null)
            mOInteraction.AddGeneralMessage(CupMessage);

        return true;
    }

    private UWObject fCreateCup()
    {
        UWDataImport.DataImport lOData = mOLevelLoader.UWDataImporter;

        if (lOData == null)
            return null;

        // CreateObj's fresh object with its picture (UWObjectFactory), as place_new makes the
        // cup (play_cup_tune, SOUND.C).
        UWObject lOCup = UWObjectFactory.Create(CupOfWonderObjectId, lOData);

        if (lOCup == null)
            return null;

        lOCup.WasSpawned = true;

        return lOCup;
    }
}
