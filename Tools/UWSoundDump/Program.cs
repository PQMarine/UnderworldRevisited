using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Writes the game's sound effects out as WAV files.
    ///
    /// WHY: the effects are AdLib patches, not samples, so one cannot simply listen to a file
    /// in the game data. Whenever a sound has to be IDENTIFIED - which number is the splash,
    /// which the swimming - this renders the candidates and one can hear them.
    ///
    /// It runs the same chain as the game: UWSound gives patch, note, velocity and duration
    /// per effect number, UWTvfxVoice turns that into OPL registers at sixty ticks a second,
    /// and UWOpl2 makes the samples at 49716 Hz - see UWAudioEngine, which does exactly this
    /// in Unity's audio thread.
    ///
    ///   uwsounddump &lt;data path&gt; &lt;output folder&gt; [number ...]
    ///
    /// MUSIC (2026-10-08): the MT-32 versions of the pieces (UW*.XMI) on General MIDI, through
    /// UWXmiSequencer and UWGmMusicDriver as in UWAudioEngine, stereo at 44.1 kHz - to hear what
    /// the instrument table does without starting the game:
    ///
    ///   uwsounddump &lt;data path&gt; &lt;output folder&gt; music &lt;soundfont.sf2&gt; [piece number ...]
    ///
    /// The same on the MT-32 emulation (UWMt32MusicDriver; mt32emu.dll beside the tool), with the
    /// ROMs from a folder:
    ///
    ///   uwsounddump &lt;data path&gt; &lt;output folder&gt; mt32 &lt;ROM folder&gt; [piece number ...]
    /// </summary>
    internal static class Program
    {
        /// <summary>The tick rate of the voice service, as in UWAudioEngine.</summary>
        private const double TicksPerSecond = 60.0;

        /// <summary>How long a rendered file may get at most.</summary>
        private const double MaxSeconds = 6.0;

        /// <summary>A little silence after the voice has finished, so nothing is cut off.</summary>
        private const double TailSeconds = 0.25;

        private static int Main(string[] psArgs)
        {
            if (psArgs.Length < 2)
            {
                Console.WriteLine("uwsounddump <data path> <output folder> [number ...]");

                return 1;
            }

            UWSound lOSound = new UWSound(psArgs[0]);

            if (!lOSound.IsAvailable || lOSound.AdlibBank == null)
            {
                Console.WriteLine("no sound data under " + psArgs[0]);

                return 1;
            }

            Directory.CreateDirectory(psArgs[1]);

            if (psArgs.Length >= 4 && (psArgs[2] == "music" || psArgs[2] == "mt32"))
            {
                bool lbMt32 = psArgs[2] == "mt32";
                List<string> lONumbers = new List<string>();

                for (int liArg = 4; liArg < psArgs.Length; liArg++)
                    lONumbers.Add(psArgs[liArg]);

                if (lONumbers.Count == 0)
                    lONumbers.AddRange(UWSound.MusicTrackNames.Keys);

                foreach (string lsNumber in lONumbers)
                {
                    IUWRenderingMidiDriver lIDriver;

                    if (lbMt32)
                    {
                        lIDriver = UWMt32MusicDriver.TryCreate(psArgs[3], MusicRate, out string lsError);

                        if (lIDriver == null)
                        {
                            Console.WriteLine("MT-32: " + lsError);

                            return 1;
                        }
                    }
                    else
                    {
                        lIDriver = new UWGmMusicDriver(psArgs[3], MusicRate);
                    }

                    fWriteMusic(lOSound, psArgs[1], lIDriver, lbMt32 ? "mt32" : "gm", lsNumber);

                    if (lIDriver is IDisposable lIDisposable)
                        lIDisposable.Dispose();
                }

                return 0;
            }

            for (int liArg = 2; liArg < psArgs.Length || (psArgs.Length == 2 && liArg == 2); liArg++)
            {
                if (psArgs.Length == 2)
                {
                    for (int liEffect = 0; liEffect < lOSound.Effects.Count; liEffect++)
                        fWrite(lOSound, psArgs[1], liEffect);

                    break;
                }

                fWrite(lOSound, psArgs[1], int.Parse(psArgs[liArg]));
            }

            return 0;
        }

        /// <summary>One effect into one file.</summary>
        private static void fWrite(UWSound pOSound, string psFolder, int piEffect)
        {
            if (piEffect < 0 || piEffect >= pOSound.Effects.Count)
            {
                Console.WriteLine("effect " + piEffect + ": there is no such number");

                return;
            }

            UWSound.Effect lOEffect = pOSound.Effects[piEffect];
            UWAdlibBank.TvfxPatch lOPatch = pOSound.AdlibBank.GetEffect(lOEffect.Patch);

            if (lOPatch == null)
            {
                Console.WriteLine("effect " + piEffect + ": patch " + lOEffect.Patch + " is missing");

                return;
            }

            UWOpl2 lOChip = new UWOpl2();
            UWTvfxVoice lOVoice = new UWTvfxVoice(0);

            lOVoice.StartKeyOn(lOPatch, lOEffect.LifetimeTicks, UWTvfxVoice.ComputeVolumeScale(lOEffect.Velocity, 0));

            int liMaxSamples = (int)(MaxSeconds * UWOpl2.SampleRate);
            int liTail = (int)(TailSeconds * UWOpl2.SampleRate);
            int liSilence = 0;
            double lfAccumulator = 0.0;

            using (MemoryStream lOSamples = new MemoryStream())
            {
                for (int liSample = 0; liSample < liMaxSamples; liSample++)
                {
                    lfAccumulator += TicksPerSecond / UWOpl2.SampleRate;

                    if (lfAccumulator >= 1.0)
                    {
                        lfAccumulator -= 1.0;

                        if (lOVoice.Phase != UWTvfxVoice.PhaseEnum.Idle)
                        {
                            lOVoice.ServiceTick();
                            lOVoice.EmitRegisters(lOChip);
                        }
                    }

                    int liValue = lOChip.GenerateSample();

                    lOSamples.WriteByte((byte)(liValue & 0xFF));
                    lOSamples.WriteByte((byte)((liValue >> 8) & 0xFF));

                    // Stop once the voice is done and the chip has gone quiet.
                    if (lOVoice.Phase == UWTvfxVoice.PhaseEnum.Idle)
                    {
                        liSilence++;

                        if (liSilence > liTail)
                            break;
                    }
                }

                string lsPath = Path.Combine(psFolder, "effect" + piEffect.ToString("00") + ".wav");

                fWriteWav(lsPath, lOSamples.ToArray());

                Console.WriteLine("effect " + piEffect + ": patch " + lOEffect.Patch + ", note " + lOEffect.Note
                    + ", velocity " + lOEffect.Velocity + ", duration " + lOEffect.Duration
                    + " -> " + Path.GetFileName(lsPath) + " (" + (lOSamples.Length / 2 / (double)UWOpl2.SampleRate).ToString("0.00") + " s)");
            }
        }

        /// <summary>The rate of the music files.</summary>
        private const int MusicRate = 44100;

        /// <summary>The longest piece runs under four minutes; a looping one stops here.</summary>
        private const double MaxMusicSeconds = 300.0;

        /// <summary>One piece's MT-32 version on General MIDI or the MT-32 into one file, with two
        /// seconds for the last notes to fade.</summary>
        private static void fWriteMusic(UWSound pOSound, string psFolder, IUWRenderingMidiDriver pIDriver, string psSuffix, string psNumber)
        {
            UWXmi lOXmi = pOSound.GetMusic(psNumber, false);

            if (lOXmi == null || !lOXmi.IsLoaded || lOXmi.Sequences.Count == 0)
            {
                Console.WriteLine("UW" + psNumber + ".XMI: not there");

                return;
            }

            IUWRenderingMidiDriver lODriver = pIDriver;
            UWXmiSequencer lOSequencer = new UWXmiSequencer(lOXmi.Sequences[0], MusicRate);
            const int Block = 64;
            float[] lfLeft = new float[Block];
            float[] lfRight = new float[Block];
            int liMaxBlocks = (int)(MaxMusicSeconds * MusicRate / Block);
            int liTailBlocks = 2 * MusicRate / Block;
            int liAfterEnd = 0;

            using (MemoryStream lOSamples = new MemoryStream())
            {
                for (int liBlock = 0; liBlock < liMaxBlocks && liAfterEnd < liTailBlocks; liBlock++)
                {
                    lOSequencer.Advance(Block, lODriver);
                    lODriver.Render(lfLeft, lfRight, Block);

                    for (int liAt = 0; liAt < Block; liAt++)
                    {
                        fWriteSample(lOSamples, lfLeft[liAt]);
                        fWriteSample(lOSamples, lfRight[liAt]);
                    }

                    if (lOSequencer.IsFinished)
                        liAfterEnd++;
                }

                string lsName = UWSound.MusicTrackNames.TryGetValue(psNumber, out string lsTitle) ? lsTitle : psNumber;
                string lsPath = Path.Combine(psFolder, "music" + psNumber + "-" + psSuffix + ".wav");

                fWriteWav(lsPath, lOSamples.ToArray(), MusicRate, 2);
                Console.WriteLine("UW" + psNumber + ".XMI (" + lsName + ") -> " + Path.GetFileName(lsPath)
                    + " (" + (lOSamples.Length / 4 / (double)MusicRate).ToString("0.0") + " s)");
            }
        }

        private static void fWriteSample(MemoryStream pOOut, float pfValue)
        {
            int liValue = (int)Math.Round(Math.Max(-1f, Math.Min(1f, pfValue)) * 32767f);

            pOOut.WriteByte((byte)(liValue & 0xFF));
            pOOut.WriteByte((byte)((liValue >> 8) & 0xFF));
        }

        /// <summary>Mono, sixteen bit, at the chip's own rate - no resampling, so nothing is
        /// coloured by it.</summary>
        private static void fWriteWav(string psPath, byte[] pySamples)
        {
            fWriteWav(psPath, pySamples, UWOpl2.SampleRate, 1);
        }

        private static void fWriteWav(string psPath, byte[] pySamples, int piRate, int piChannels)
        {
            using (FileStream lOFile = File.Create(psPath))
            using (BinaryWriter lOOut = new BinaryWriter(lOFile))
            {
                lOOut.Write(new char[] { 'R', 'I', 'F', 'F' });
                lOOut.Write(36 + pySamples.Length);
                lOOut.Write(new char[] { 'W', 'A', 'V', 'E' });
                lOOut.Write(new char[] { 'f', 'm', 't', ' ' });
                lOOut.Write(16);
                lOOut.Write((short)1);
                lOOut.Write((short)piChannels);
                lOOut.Write(piRate);
                lOOut.Write(piRate * 2 * piChannels);
                lOOut.Write((short)(2 * piChannels));
                lOOut.Write((short)16);
                lOOut.Write(new char[] { 'd', 'a', 't', 'a' });
                lOOut.Write(pySamples.Length);
                lOOut.Write(pySamples);
            }
        }
    }
}
