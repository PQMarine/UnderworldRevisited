using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UWDataImport;
using UWDataImport.Audio;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 6.6 (2026-10-24): the sound's structure out of UWAudioEngine - the music
    /// devices (IUWMusicDevice), UWMusicDeviceSelector, UWEffectSynth, UWAudioSettings and the
    /// engine-free mix UWAudioMixCore - plus the XMIDI controller filter that moved into
    /// UWXmiSequencer.
    ///
    /// THE MIX PINS: a scripted session (pieces, effects, the water's held voice, an instrument,
    /// gains changed, a switch of the device mid-piece and back, a stop) rendered through
    /// UWAudioMixCore.Mix, hashed over every output sample. The hashes were recorded from a
    /// line-by-line transcription of UWAudioEngine as it was before the step (modernisierung
    /// 91bf6a6a) on the same data, so they pin "no audible change" sample for sample. The script
    /// plays AdLib files only (also as the MT-32 version on General MIDI), so it needs only what
    /// every installation has; General MIDI uses the soundfont in StreamingAssets.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>The AdLib session, 48 kHz stereo, 300 buffers of 1024 frames.</summary>
        private const string AdlibMixHash = "0A4FB8F0EA68DD9C66CD4D4372FCABCACBDF25DEE18F0780AB3F88C56AE32403";

        /// <summary>The same session on General MIDI, the switch going to the AdLib and back.</summary>
        private const string GeneralMidiMixHash = "FE315E2D221CA82FFB43540548293ED0456BBB172D9E22F0BA05CB709D262D9B";

        private static void fCheckAudioMix(DataImport pOData)
        {
            Check.Group("Sound structure (music devices, effect synth, mix - cleanup step 6.6)");

            fCheckXmidiFilter();
            fCheckAudioSettings();
            fCheckRecordings();

            UWSound lOSound = pOData != null ? pOData.Sound : null;

            if (lOSound == null || !lOSound.IsAvailable || lOSound.AdlibBank == null || !lOSound.AdlibBank.IsLoaded)
            {
                Check.Pass("audio: mix", "skipped - no SOUND folder");

                return;
            }

            fCheckDeviceSelector(lOSound);
            fCheckEffectSynth(lOSound);

            Check.Str("audio: the AdLib session sounds as before the step", fMixHash(lOSound, UWMusicDeviceId.AdLib, null), AdlibMixHash);

            string lsRoot = CheckContext.ProjectRoot;
            string lsFont = lsRoot != null ? Path.Combine(lsRoot, "Assets", "StreamingAssets", "UWMusic", "UWGeneralMidi.sf2") : null;

            if (lsFont == null || !File.Exists(lsFont))
                Check.Pass("audio: General MIDI session", "skipped - no soundfont outside the source tree");
            else
                Check.Str("audio: the General MIDI session sounds as before the step", fMixHash(lOSound, UWMusicDeviceId.GeneralMidi, lsFont), GeneralMidiMixHash);
        }

        /// <summary>The Miles driver's controllers 110-120 stop in the sequencer; the loop
        /// controllers 116/117 still loop; 109 and 121 pass.</summary>
        private static void fCheckXmidiFilter()
        {
            Check.Bool("xmidi: 109 is a device's controller", UWXmiSequencer.IsXmidiController(109), false);
            Check.Bool("xmidi: 110 is the driver's", UWXmiSequencer.IsXmidiController(110), true);
            Check.Bool("xmidi: 120 is the driver's (General MIDI would read All Sound Off)", UWXmiSequencer.IsXmidiController(120), true);
            Check.Bool("xmidi: 121 is a device's (reset all controllers)", UWXmiSequencer.IsXmidiController(121), false);

            UWXmi.Sequence lOSequence = new UWXmi.Sequence();

            lOSequence.Events.Add(new UWXmi.Event { Tick = 0, Status = 0xB1, Data1 = 7, Data2 = 100 });
            lOSequence.Events.Add(new UWXmi.Event { Tick = 0, Status = 0xB1, Data1 = 110, Data2 = 1 });
            lOSequence.Events.Add(new UWXmi.Event { Tick = 0, Status = 0xB1, Data1 = 114, Data2 = 0 });
            lOSequence.Events.Add(new UWXmi.Event { Tick = 0, Status = 0xB1, Data1 = 120, Data2 = 0 });
            lOSequence.Events.Add(new UWXmi.Event { Tick = 0, Status = 0xB1, Data1 = 121, Data2 = 0 });
            lOSequence.Events.Add(new UWXmi.Event { Tick = 0, Status = 0xB1, Data1 = 116, Data2 = 2 });
            lOSequence.Events.Add(new UWXmi.Event { Tick = 1, Status = 0x91, Data1 = 60, Data2 = 127 });
            lOSequence.Events.Add(new UWXmi.Event { Tick = 5, Status = 0x81, Data1 = 60, Data2 = 0 });
            lOSequence.Events.Add(new UWXmi.Event { Tick = 6, Status = 0xB1, Data1 = 117, Data2 = 0 });
            lOSequence.Events.Add(new UWXmi.Event { Tick = 8, Status = 0xFF, Data1 = 0x2F });

            // At 120 samples a second one sample is one tick.
            UWXmiSequencer lOSequencer = new UWXmiSequencer(lOSequence, 120);
            RecordingMidiDriver lODriver = new RecordingMidiDriver();

            for (int liStep = 0; liStep < 40 && !lOSequencer.IsFinished; liStep++)
                lOSequencer.Advance(1, lODriver);

            Check.Str("xmidi: the driver hears volume, reset and the looped note twice, nothing of 110/114/120",
                string.Join(", ", lODriver.Events), "B1 7 100, B1 121 0, 91 60 127, 81 60 0, 91 60 127, 81 60 0, notes off");
        }

        private static void fCheckAudioSettings()
        {
            UWAudioSettings lOFirst = UWAudioSettings.Initial.With(0.6f, 0.8f, UWMusicDeviceId.AdLib);

            Check.Int("audio settings: a change counts one revision", lOFirst.Revision, UWAudioSettings.Initial.Revision + 1);
            Check.Bool("audio settings: the same values keep the snapshot", ReferenceEquals(lOFirst.With(0.6f, 0.8f, UWMusicDeviceId.AdLib), lOFirst), true);

            UWAudioSettings lOSecond = lOFirst.With(0.6f, 0.8f, UWMusicDeviceId.Mt32);

            Check.Int("audio settings: the device counts too", lOSecond.Revision, lOFirst.Revision + 1);
            Check.Bool("audio settings: the gains are clamped to 0..1",
                lOSecond.With(-0.5f, 1.5f, UWMusicDeviceId.Mt32).MusicGain == 0f && lOSecond.With(-0.5f, 1.5f, UWMusicDeviceId.Mt32).EffectGain == 1f, true);
        }

        /// <summary>The player's recordings (UWMusicRecordings): named by the game's piece numbers
        /// without a prefix, the extension in any case, Ogg before WAV, other names and formats left
        /// out, each piece counted once.</summary>
        private static void fCheckRecordings()
        {
            string lsFolder = Path.Combine(Path.GetTempPath(), "UWCheckRecordings_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(lsFolder);

                foreach (string lsName in new[] { "01.ogg", "01.wav", "15.WAV", "10.mp3", "08.ogg", "1.ogg", "UW02.ogg", "Intro.ogg" })
                    File.WriteAllBytes(Path.Combine(lsFolder, lsName), new byte[0]);

                Check.Str("recordings: the introduction is 01, Ogg before WAV",
                    Path.GetFileName(UWMusicRecordings.Find(lsFolder, UWMusicSelector.GetFileNumber(UWMusicSelector.IntroTheme))), "01.ogg");
                Check.Str("recordings: Maps and Legends is 15, the extension in any case",
                    Path.GetFileName(UWMusicRecordings.Find(lsFolder, UWMusicSelector.GetFileNumber(UWMusicSelector.MapsAndLegendsTheme))), "15.WAV");
                Check.Bool("recordings: an MP3 is not taken (Armed, 10)",
                    UWMusicRecordings.Find(lsFolder, UWMusicSelector.GetFileNumber(UWMusicSelector.ArmedTheme)) == null, true);
                Check.Bool("recordings: a name with the game file's prefix is not taken (UW02)",
                    UWMusicRecordings.Find(lsFolder, UWMusicSelector.GetFileNumber(UWMusicSelector.FirstLevelTheme)) == null, true);
                Check.Int("recordings: counted once per piece, two octal digits only (01, 15)", UWMusicRecordings.Count(lsFolder), 2);
                Check.Bool("recordings: no folder, none", UWMusicRecordings.Find(Path.Combine(lsFolder, "missing"), "01") == null
                    && UWMusicRecordings.Count(Path.Combine(lsFolder, "missing")) == 0, true);
            }
            finally
            {
                try
                {
                    Directory.Delete(lsFolder, true);
                }
                catch (IOException)
                {
                }
            }
        }

        /// <summary>The fallback to the AdLib: no soundfont, no ROMs. General MIDI is tried once,
        /// the MT-32 on every wish (ROMs put in later are taken).</summary>
        private static void fCheckDeviceSelector(UWSound pOSound)
        {
            string lsNowhere = Path.Combine(Path.GetTempPath(), "UWSelfCheck-no-such-folder-6.6");
            List<string> lOWarnings = new List<string>();
            UWMusicDeviceSelector lOSelector = new UWMusicDeviceSelector(new UWAdlibMusicDevice(pOSound.AdlibBank), 48000,
                Path.Combine(lsNowhere, "none.sf2"), () => lsNowhere, null, lOWarnings.Add);

            Check.Bool("devices: General MIDI without a soundfont falls back to the AdLib",
                lOSelector.Resolve(UWMusicDeviceId.GeneralMidi) == UWMusicDeviceId.AdLib, true);
            Check.Bool("devices: ... and is not tried again",
                lOSelector.Resolve(UWMusicDeviceId.GeneralMidi) == UWMusicDeviceId.AdLib && lOWarnings.Count == 1, true);
            Check.Bool("devices: the MT-32 without ROMs falls back to the AdLib",
                lOSelector.Resolve(UWMusicDeviceId.Mt32) == UWMusicDeviceId.AdLib, true);
            Check.Bool("devices: ... and is asked again on the next wish",
                lOSelector.Resolve(UWMusicDeviceId.Mt32) == UWMusicDeviceId.AdLib && lOWarnings.Count == 3, true);
            Check.Str("devices: the AdLib plays the AW files", lOSelector.Adlib.XmiVersion.ToString(), "AdLib");
            Check.Str("devices: no ROMs in a missing folder", UWMusicDeviceSelector.GetMt32Problem(lsNowhere), "no MT-32 or CM-32L ROMs in that folder");

            lOSelector.Dispose();
        }

        /// <summary>Nine voices, one search: the tenth effect is dropped, as in the original.</summary>
        private static void fCheckEffectSynth(UWSound pOSound)
        {
            UWEffectSynth lOSynth = new UWEffectSynth(pOSound.AdlibBank);
            UWSound.Effect lODoor = pOSound.Effects[UWSoundEffectId.Door];
            string lsVoices = "";

            for (int liAt = 0; liAt < UWOpl2.ChannelCount; liAt++)
                lsVoices += lOSynth.Play(lODoor, 0) + " ";

            Check.Str("effects: nine doors take the nine voices in order", lsVoices.Trim(), "0 1 2 3 4 5 6 7 8");
            Check.Int("effects: a tenth is dropped", lOSynth.Play(lODoor, 0), -1);
            Check.Int("effects: ... and a held one as well", lOSynth.StartHeld(pOSound.Effects[UWSoundEffectId.Water], 0), -1);
            Check.Bool("effects: voice 0 sounds", lOSynth.IsSounding(0), true);
            Check.Bool("effects: there is no voice 9", lOSynth.IsSounding(9), false);

            float[] lfOut = new float[UWOpl2.SampleRate];

            for (int liSecond = 0; liSecond < 8; liSecond++)
                lOSynth.Render(lfOut, lfOut.Length);

            Check.Bool("effects: after eight seconds the doors have ended", lOSynth.IsSounding(0), false);
        }

        /// <summary>
        /// The scripted session through UWAudioMixCore as UWAudioMixer drives it (the calls one
        /// after the other, as the lock serialises them), hashed. Mirrored line for line by the
        /// transcription that recorded the pins - change both or neither.
        /// </summary>
        private static string fMixHash(UWSound pOSound, UWMusicDeviceId peDevice, string psSoundFont)
        {
            UWAudioMixCore lOCore = new UWAudioMixCore(pOSound.AdlibBank, 48000, psSoundFont, () => null, null, null);
            lOCore.SwitchDevice(lOCore.ResolveDevice(peDevice));

            UWXmiVersion leVersion = peDevice != UWMusicDeviceId.AdLib ? UWXmiVersion.Mt32 : UWXmiVersion.AdLib;
            float lfMusic = 0.6f, lfEffects = 0.8f;
            int liHeld = -1;
            string lsPiece = null;
            float[] lfData = new float[1024 * 2];
            byte[] lyBytes = new byte[lfData.Length * 4];

            void Play(string psNumber, UWXmiVersion peAs)
            {
                lsPiece = psNumber;
                lOCore.PlayMusic(pOSound.GetMusic(psNumber, true).Sequences[0], false, peAs);
            }

            void Switch(UWMusicDeviceId peTo, UWXmiVersion peAs)
            {
                bool lbWasPlaying = lOCore.IsMusicPlaying;

                if (lOCore.SwitchDevice(lOCore.ResolveDevice(peTo)) && lbWasPlaying)
                    Play(lsPiece, peAs);
            }

            using (IncrementalHash lOHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                for (int liBuffer = 0; liBuffer < 300; liBuffer++)
                {
                    switch (liBuffer)
                    {
                        case 0: Play("01", leVersion); break;
                        case 10: lOCore.PlayEffect(pOSound.Effects[UWSoundEffectId.Door], -10); break;
                        case 20: liHeld = lOCore.StartHeldEffect(pOSound.Effects[UWSoundEffectId.Water], 0); break;
                        case 30: lOCore.BeginInstrument(0x39); lOCore.PlayInstrumentNote(60); break;
                        case 45: lOCore.StopInstrumentNote(60); lOCore.EndInstrument(); break;
                        case 60: Play("06", leVersion); break;
                        case 100: lOCore.StopHeldEffect(liHeld); break;
                        case 120:
                            lfMusic = 0.3f;
                            lfEffects = 1f;
                            lOCore.PlayEffect(pOSound.Effects[UWSoundEffectId.HitCritter], 0);
                            lOCore.PlayEffect(pOSound.Effects[UWSoundEffectId.HitPlayer], 4);
                            break;
                        case 150: Switch(UWMusicDeviceId.AdLib, UWXmiVersion.AdLib); break;
                        case 200: Switch(peDevice, leVersion); break;
                        case 250: lOCore.StopMusic(); break;
                        case 260: Play("10", leVersion); break;
                    }

                    Array.Clear(lfData, 0, lfData.Length);
                    lOCore.Mix(lfData, 2, lfMusic, lfEffects);
                    Buffer.BlockCopy(lfData, 0, lyBytes, 0, lyBytes.Length);
                    lOHash.AppendData(lyBytes);
                }

                lOCore.Dispose();

                return Convert.ToHexString(lOHash.GetHashAndReset());
            }
        }

        /// <summary>SetInteractionMode_seg024_24DC_13D5: drawing the weapon keeps a combat theme,
        /// leaving combat mode keeps it too; Armed and the level theme only from the calm.</summary>
        private static void fCheckWeaponMusic()
        {
            MusicOutput lOOutput = new MusicOutput();
            UWMusicSelector lOSelector = new UWMusicSelector(lOOutput);

            lOSelector.ChangeTheme(UWMusicSelector.EvenCombatTheme);
            lOSelector.Refresh(false, 0f);
            lOSelector.MarkCombat(0f);
            lOSelector.OnWeaponDrawn();
            lOSelector.Refresh(true, 1f);
            fExpectInt("weapon music: drawn during combat music, it goes on", lOSelector.CurrentTheme, UWMusicSelector.EvenCombatTheme);

            lOSelector.OnCombatModeLeft();
            lOSelector.Refresh(false, 2f);
            fExpectInt("weapon music: put away during combat music, it goes on", lOSelector.CurrentTheme, UWMusicSelector.EvenCombatTheme);

            UWMusicSelector lOCalm = new UWMusicSelector(lOOutput);

            lOCalm.ChangeTheme(UWMusicSelector.FirstLevelTheme);
            lOCalm.Refresh(false, 0f);
            lOCalm.OnWeaponDrawn();
            lOCalm.Refresh(true, 1f);
            fExpectInt("weapon music: drawn in the calm, Armed", lOCalm.CurrentTheme, UWMusicSelector.ArmedTheme);

            lOCalm.OnCombatModeLeft();
            lOCalm.Refresh(false, 2f);
            fExpectBool("weapon music: put away from Armed, a level theme",
                lOCalm.CurrentTheme >= UWMusicSelector.FirstLevelTheme && lOCalm.CurrentTheme <= UWMusicSelector.LastLevelTheme, true);

            // RefreshMusic from label 1746: the fanfare ends with nothing requested - a level theme,
            // with the weapon drawn Armed; a request made during the fanfare plays after it.
            MusicOutput lOFanfareOutput = new MusicOutput();
            UWMusicSelector lOFanfare = new UWMusicSelector(lOFanfareOutput);

            lOFanfare.ChangeTheme(UWMusicSelector.FanfareTheme);
            lOFanfare.Refresh(false, 0f);
            lOFanfareOutput.IsPlaying = false;
            lOFanfare.Refresh(false, 1f);
            fExpectBool("music: after the fanfare a level theme",
                lOFanfare.CurrentTheme >= UWMusicSelector.FirstLevelTheme && lOFanfare.CurrentTheme <= UWMusicSelector.LastLevelTheme, true);

            lOFanfareOutput.IsPlaying = true;
            lOFanfare.ChangeTheme(UWMusicSelector.FanfareTheme);
            lOFanfare.Refresh(false, 2f);
            lOFanfare.OnWeaponDrawn();
            lOFanfare.Refresh(true, 3f);
            fExpectInt("music: the fanfare is not interrupted", lOFanfare.CurrentTheme, UWMusicSelector.FanfareTheme);
            lOFanfareOutput.IsPlaying = false;
            lOFanfare.Refresh(true, 4f);
            fExpectInt("music: after the fanfare with the weapon drawn, Armed", lOFanfare.CurrentTheme, UWMusicSelector.ArmedTheme);
        }
    }
}
