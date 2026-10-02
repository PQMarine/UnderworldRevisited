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
///              (C D E F G A B C D E). One modifier key raises by an octave, another one
///              lowers - which one is stored as bit 0x200 and 0x100 in the key code; here Shift and Ctrl.
///   Duration   the note sounds until the next one comes or 0x40 timer ticks have passed;
///              here UWSettings.InstrumentNoteSeconds instead of the ticks.
///   Memory     every note goes into a ring of 16 places, starting from the first note.
///   End        ESC: "You put the instrument down."
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

    private const int CupOfWonderObjectId = 174;

    /// <summary>String block 1 (our export; UW.EXE is one lower).</summary>
    private const int StartMessage = 251;

    private const int EndMessage = 252;

    private const int CupMessage = 137;

    private static readonly int[] msPrograms = { 0x39, 0x48 };

    private static readonly int[] msNotes = { 60, 62, 64, 65, 67, 69, 71, 72, 74, 76 };

    private static readonly int[] msCupMelody = { 64, 67, 65, 62, 64, 71, 72, 71, 67 };

    private const int CupLevelIndex = 2;

    private const int CupTileX = 24;

    private const int CupTileY = 45;

    private const int CupRange = 2;

    private const int RingSize = 16;

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

            if (lOKeyboard.shiftKey.isPressed)
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

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        mfNoteOffTime = Time.time + (lOSettings != null ? lOSettings.InstrumentNoteSeconds : 0.35f);
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
        IsPlaying = false;

        if (mOInteraction != null)
            mOInteraction.AddGeneralMessage(EndMessage);

        fTryCupOfWonder();
    }

    private void OnDisable()
    {
        if (IsPlaying)
        {
            fStopNote();
            IsPlaying = false;
        }
    }

    /// <summary>The Cup of Wonder - see the class comment.</summary>
    private void fTryCupOfWonder()
    {
        if (miInstrument != FluteObjectId - MandolinObjectId || UWEndgame.CupOfWonderFound
            || mOLevelLoader == null || mOLevelLoader.CurrentLevelIndex != CupLevelIndex
            || Camera.main == null)
            return;

        UWTilePos lOTile = mOLevelLoader.WorldPositionToTile(Camera.main.transform.position);

        if (Mathf.Abs(lOTile.X - CupTileX) > CupRange || Mathf.Abs(lOTile.Y - CupTileY) > CupRange)
            return;

        for (int liAt = 0; liAt < msCupMelody.Length; liAt++)
        {
            if (miRing[liAt] != msCupMelody[liAt])
                return;
        }

        UWObject lOCup = fCreateCup();

        if (lOCup == null || mOInventory == null)
            return;

        // Into the hand (UW.EXE SpawnObjectInHand); if something is already held there, into the backpack.
        if (mOInventory.CursorItem == null)
            mOInventory.BeginDragFromExternal(lOCup);
        else if (!mOInventory.TryAddToBackpack(lOCup))
            return;

        UWEndgame.CupOfWonderFound = true;

        if (mOInteraction != null)
            mOInteraction.AddGeneralMessage(CupMessage);
    }

    private UWObject fCreateCup()
    {
        UWDataImport.DataImport lOData = mOLevelLoader.UWDataImporter;

        if (lOData == null)
            return null;

        UWObject lOCup = new UWObject((ushort)CupOfWonderObjectId);

        // Like a freshly created object (UW.EXE PrepareNewObjectProps): condition 0x28, and
        // quantity 1 if its quantity class calls for it.
        lOCup.Quality = 0x28;

        UWCommonObjectProperties.Entry lOEntry;

        if (lOData.CommonObjectProperties != null
            && lOData.CommonObjectProperties.TryGet(CupOfWonderObjectId, out lOEntry) && lOEntry.StartsWithQuantity)
        {
            lOCup.HasQuantity = true;
            lOCup.Quantity = 1;
        }

        try
        {
            lOCup.Texture = lOData.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, CupOfWonderObjectId);
        }
        catch
        {
            return null;
        }

        lOCup.WasSpawned = true;

        return lOCup;
    }
}
