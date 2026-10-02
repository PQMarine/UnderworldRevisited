using System;
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

        /// <summary>Mono, sixteen bit, at the chip's own rate - no resampling, so nothing is
        /// coloured by it.</summary>
        private static void fWriteWav(string psPath, byte[] pySamples)
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
                lOOut.Write((short)1);
                lOOut.Write(UWOpl2.SampleRate);
                lOOut.Write(UWOpl2.SampleRate * 2);
                lOOut.Write((short)2);
                lOOut.Write((short)16);
                lOOut.Write(new char[] { 'd', 'a', 't', 'a' });
                lOOut.Write(pySamples.Length);
                lOOut.Write(pySamples);
            }
        }
    }
}
