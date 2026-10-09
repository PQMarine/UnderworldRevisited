using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cuts a SoundFont 2 file down to a list of presets.
    ///
    /// WHY: the General MIDI music path (UWGmMusicDriver) ships a soundfont so it works out of the
    /// box. FluidR3_GM (Frank Wen, MIT) is 140 MB; the twelve pieces of Ultima Underworld use
    /// only a few dozen of its presets, so the shipped file keeps those and the drum kit and
    /// nothing else.
    ///
    /// HOW: the preset headers of the wanted presets are kept with their zones; every instrument
    /// they reach is kept with its zones, every sample those reach (and the other half of a stereo
    /// pair) is copied with the 46 zero points the format wants after each sample. All indices are
    /// renumbered; nothing inside a zone is changed, so the kept presets sound as in the full file.
    /// Only 16-bit data is copied (an sm24 chunk is dropped).
    ///
    /// A preset can be limited to some keys (bank:preset=key,key,...): then only its zones and
    /// those of its instruments whose key range takes one of the keys stay. That is for the drum
    /// kit, of which the pieces play thirteen drums - the whole kit alone is 11 MB.
    ///
    ///   uwsoundfonttrim &lt;in.sf2&gt; &lt;out.sf2&gt; &lt;bank:preset[=key,...]&gt; ...
    /// </summary>
    internal static class Program
    {
        private const int GeneratorInstrument = 41;

        private const int GeneratorSampleId = 53;

        private const int GeneratorKeyRange = 43;

        private const int SamplePadding = 46;

        private sealed class Chunk
        {
            public string Id;

            public byte[] Data;
        }

        private static int Main(string[] psArgs)
        {
            if (psArgs.Length < 3)
            {
                Console.WriteLine("uwsoundfonttrim <in.sf2> <out.sf2> <bank:preset> ...");

                return 1;
            }

            HashSet<int> lOWanted = new HashSet<int>();
            Dictionary<int, HashSet<int>> lOWantedKeys = new Dictionary<int, HashSet<int>>();

            for (int liArg = 2; liArg < psArgs.Length; liArg++)
            {
                string[] lsKeys = psArgs[liArg].Split('=');
                string[] lsParts = lsKeys[0].Split(':');
                int liId = (int.Parse(lsParts[0]) << 16) | int.Parse(lsParts[1]);

                lOWanted.Add(liId);

                if (lsKeys.Length > 1)
                {
                    HashSet<int> lOKeys = new HashSet<int>();

                    foreach (string lsKey in lsKeys[1].Split(','))
                        lOKeys.Add(int.Parse(lsKey));

                    lOWantedKeys[liId] = lOKeys;
                }
            }

            byte[] lyFile = File.ReadAllBytes(psArgs[0]);

            if (Encoding.ASCII.GetString(lyFile, 0, 4) != "RIFF" || Encoding.ASCII.GetString(lyFile, 8, 4) != "sfbk")
            {
                Console.WriteLine("not a SoundFont 2 file: " + psArgs[0]);

                return 1;
            }

            Dictionary<string, Chunk> lOLists = new Dictionary<string, Chunk>();
            Dictionary<string, byte[]> lOChunks = new Dictionary<string, byte[]>();

            fReadChunks(lyFile, 12, lyFile.Length, lOLists, lOChunks);

            byte[] lySamples = lOChunks["smpl"];
            byte[] lyPhdr = lOChunks["phdr"];
            byte[] lyPbag = lOChunks["pbag"];
            byte[] lyPmod = lOChunks["pmod"];
            byte[] lyPgen = lOChunks["pgen"];
            byte[] lyInst = lOChunks["inst"];
            byte[] lyIbag = lOChunks["ibag"];
            byte[] lyImod = lOChunks["imod"];
            byte[] lyIgen = lOChunks["igen"];
            byte[] lyShdr = lOChunks["shdr"];

            // Presets: 38 bytes each, the last one is the terminal EOP record.
            int liPresetCount = (lyPhdr.Length / 38) - 1;
            List<int> lOKeptPresets = new List<int>();
            Dictionary<int, HashSet<int>> lOPresetKeys = new Dictionary<int, HashSet<int>>();

            for (int liPreset = 0; liPreset < liPresetCount; liPreset++)
            {
                int liProgram = fU16(lyPhdr, (liPreset * 38) + 20);
                int liBank = fU16(lyPhdr, (liPreset * 38) + 22);
                int liId = (liBank << 16) | liProgram;

                if (!lOWanted.Remove(liId))
                    continue;

                lOKeptPresets.Add(liPreset);

                if (lOWantedKeys.TryGetValue(liId, out HashSet<int> lOKeys))
                    lOPresetKeys[liPreset] = lOKeys;
            }

            foreach (int liMissing in lOWanted)
                Console.WriteLine("not in the file: " + (liMissing >> 16) + ":" + (liMissing & 0xFFFF));

            // Instruments reached from the kept presets.
            List<int> lOKeptInstruments = new List<int>();
            Dictionary<int, int> lOInstrumentMap = new Dictionary<int, int>();

            // An instrument reached only from key-limited presets takes their keys; one reached
            // from any other preset stays whole (null).
            Dictionary<int, HashSet<int>> lOInstrumentKeys = new Dictionary<int, HashSet<int>>();

            foreach (int liPreset in lOKeptPresets)
            {
                lOPresetKeys.TryGetValue(liPreset, out HashSet<int> lOKeys);

                for (int liBag = fU16(lyPhdr, (liPreset * 38) + 24); liBag < fU16(lyPhdr, ((liPreset + 1) * 38) + 24); liBag++)
                {
                    if (!fZoneTakes(lyPgen, fU16(lyPbag, liBag * 4), fU16(lyPbag, (liBag + 1) * 4), lOKeys))
                        continue;

                    for (int liGen = fU16(lyPbag, liBag * 4); liGen < fU16(lyPbag, (liBag + 1) * 4); liGen++)
                    {
                        if (fU16(lyPgen, liGen * 4) != GeneratorInstrument)
                            continue;

                        int liInstrument = fU16(lyPgen, (liGen * 4) + 2);

                        if (!lOInstrumentMap.ContainsKey(liInstrument))
                        {
                            lOInstrumentMap[liInstrument] = lOKeptInstruments.Count;
                            lOKeptInstruments.Add(liInstrument);
                            lOInstrumentKeys[liInstrument] = lOKeys;
                        }
                        else if (lOKeys == null)
                        {
                            lOInstrumentKeys[liInstrument] = null;
                        }
                        else if (lOInstrumentKeys[liInstrument] != null)
                        {
                            lOInstrumentKeys[liInstrument] = new HashSet<int>(lOInstrumentKeys[liInstrument]);
                            lOInstrumentKeys[liInstrument].UnionWith(lOKeys);
                        }
                    }
                }
            }

            // Samples reached from the kept instruments, with the other half of a stereo pair.
            List<int> lOKeptSamples = new List<int>();
            Dictionary<int, int> lOSampleMap = new Dictionary<int, int>();

            foreach (int liInstrument in lOKeptInstruments)
            {
                for (int liBag = fU16(lyInst, (liInstrument * 22) + 20); liBag < fU16(lyInst, ((liInstrument + 1) * 22) + 20); liBag++)
                {
                    if (!fZoneTakes(lyIgen, fU16(lyIbag, liBag * 4), fU16(lyIbag, (liBag + 1) * 4), lOInstrumentKeys[liInstrument]))
                        continue;

                    for (int liGen = fU16(lyIbag, liBag * 4); liGen < fU16(lyIbag, (liBag + 1) * 4); liGen++)
                    {
                        if (fU16(lyIgen, liGen * 4) == GeneratorSampleId)
                            fKeepSample(lyShdr, fU16(lyIgen, (liGen * 4) + 2), lOKeptSamples, lOSampleMap);
                    }
                }
            }

            // New sample data and headers.
            MemoryStream lOSmpl = new MemoryStream();
            MemoryStream lOShdr = new MemoryStream();

            foreach (int liSample in lOKeptSamples)
            {
                int liBase = liSample * 46;
                uint luStart = fU32(lyShdr, liBase + 20);
                uint luEnd = fU32(lyShdr, liBase + 24);
                uint luLoopStart = fU32(lyShdr, liBase + 28);
                uint luLoopEnd = fU32(lyShdr, liBase + 32);
                uint luNewStart = (uint)(lOSmpl.Length / 2);

                lOSmpl.Write(lySamples, (int)(luStart * 2), (int)((luEnd - luStart) * 2));
                lOSmpl.Write(new byte[SamplePadding * 2], 0, SamplePadding * 2);

                byte[] lyHeader = new byte[46];
                Array.Copy(lyShdr, liBase, lyHeader, 0, 46);
                fPut32(lyHeader, 20, luNewStart);
                fPut32(lyHeader, 24, luNewStart + (luEnd - luStart));
                fPut32(lyHeader, 28, luNewStart + (luLoopStart - luStart));
                fPut32(lyHeader, 32, luNewStart + (luLoopEnd - luStart));

                int liLink = fU16(lyShdr, liBase + 42);
                int liType = fU16(lyShdr, liBase + 44);

                fPut16(lyHeader, 42, (liType & 0x0E) != 0 && lOSampleMap.TryGetValue(liLink, out int liNewLink) ? liNewLink : 0);
                lOShdr.Write(lyHeader, 0, 46);
            }

            byte[] lyEos = new byte[46];
            Encoding.ASCII.GetBytes("EOS").CopyTo(lyEos, 0);
            lOShdr.Write(lyEos, 0, 46);

            // Instruments with their zones; the sample generator renumbered.
            MemoryStream lOInst = new MemoryStream();
            MemoryStream lOIbag = new MemoryStream();
            MemoryStream lOImod = new MemoryStream();
            MemoryStream lOIgen = new MemoryStream();

            foreach (int liInstrument in lOKeptInstruments)
            {
                byte[] lyHeader = new byte[22];
                Array.Copy(lyInst, liInstrument * 22, lyHeader, 0, 22);
                fPut16(lyHeader, 20, (int)(lOIbag.Length / 4));
                lOInst.Write(lyHeader, 0, 22);

                for (int liBag = fU16(lyInst, (liInstrument * 22) + 20); liBag < fU16(lyInst, ((liInstrument + 1) * 22) + 20); liBag++)
                {
                    if (!fZoneTakes(lyIgen, fU16(lyIbag, liBag * 4), fU16(lyIbag, (liBag + 1) * 4), lOInstrumentKeys[liInstrument]))
                        continue;

                    fWriteBag(lOIbag, lOIgen, lOImod);
                    fCopyGenerators(lyIgen, fU16(lyIbag, liBag * 4), fU16(lyIbag, (liBag + 1) * 4), GeneratorSampleId, lOSampleMap, lOIgen);
                    lOImod.Write(lyImod, fU16(lyIbag, (liBag * 4) + 2) * 10, (fU16(lyIbag, ((liBag + 1) * 4) + 2) - fU16(lyIbag, (liBag * 4) + 2)) * 10);
                }
            }

            fWriteTerminal(lOInst, 22, "EOI", 20, (int)(lOIbag.Length / 4));
            fWriteBag(lOIbag, lOIgen, lOImod);
            lOIgen.Write(new byte[4], 0, 4);
            lOImod.Write(new byte[10], 0, 10);

            // Presets with their zones; the instrument generator renumbered.
            MemoryStream lOPhdr = new MemoryStream();
            MemoryStream lOPbag = new MemoryStream();
            MemoryStream lOPmod = new MemoryStream();
            MemoryStream lOPgen = new MemoryStream();

            foreach (int liPreset in lOKeptPresets)
            {
                byte[] lyHeader = new byte[38];
                Array.Copy(lyPhdr, liPreset * 38, lyHeader, 0, 38);
                fPut16(lyHeader, 24, (int)(lOPbag.Length / 4));
                lOPhdr.Write(lyHeader, 0, 38);

                lOPresetKeys.TryGetValue(liPreset, out HashSet<int> lOKeys);

                for (int liBag = fU16(lyPhdr, (liPreset * 38) + 24); liBag < fU16(lyPhdr, ((liPreset + 1) * 38) + 24); liBag++)
                {
                    if (!fZoneTakes(lyPgen, fU16(lyPbag, liBag * 4), fU16(lyPbag, (liBag + 1) * 4), lOKeys))
                        continue;

                    fWriteBag(lOPbag, lOPgen, lOPmod);
                    fCopyGenerators(lyPgen, fU16(lyPbag, liBag * 4), fU16(lyPbag, (liBag + 1) * 4), GeneratorInstrument, lOInstrumentMap, lOPgen);
                    lOPmod.Write(lyPmod, fU16(lyPbag, (liBag * 4) + 2) * 10, (fU16(lyPbag, ((liBag + 1) * 4) + 2) - fU16(lyPbag, (liBag * 4) + 2)) * 10);
                }
            }

            fWriteTerminal(lOPhdr, 38, "EOP", 24, (int)(lOPbag.Length / 4));
            fWriteBag(lOPbag, lOPgen, lOPmod);
            lOPgen.Write(new byte[4], 0, 4);
            lOPmod.Write(new byte[10], 0, 10);

            // Write: INFO as it was, the new sdta and pdta.
            using (FileStream lOOut = File.Create(psArgs[1]))
            {
                MemoryStream lOBody = new MemoryStream();
                lOBody.Write(Encoding.ASCII.GetBytes("sfbk"), 0, 4);
                fWriteList(lOBody, "INFO", lOLists["INFO"].Data);

                MemoryStream lOSdta = new MemoryStream();
                fWriteChunk(lOSdta, "smpl", lOSmpl.ToArray());
                fWriteList(lOBody, "sdta", lOSdta.ToArray());

                MemoryStream lOPdta = new MemoryStream();
                fWriteChunk(lOPdta, "phdr", lOPhdr.ToArray());
                fWriteChunk(lOPdta, "pbag", lOPbag.ToArray());
                fWriteChunk(lOPdta, "pmod", lOPmod.ToArray());
                fWriteChunk(lOPdta, "pgen", lOPgen.ToArray());
                fWriteChunk(lOPdta, "inst", lOInst.ToArray());
                fWriteChunk(lOPdta, "ibag", lOIbag.ToArray());
                fWriteChunk(lOPdta, "imod", lOImod.ToArray());
                fWriteChunk(lOPdta, "igen", lOIgen.ToArray());
                fWriteChunk(lOPdta, "shdr", lOShdr.ToArray());
                fWriteList(lOBody, "pdta", lOPdta.ToArray());

                fWriteChunk(lOOut, "RIFF", lOBody.ToArray());
            }

            Console.WriteLine(lOKeptPresets.Count + " presets, " + lOKeptInstruments.Count + " instruments, "
                + lOKeptSamples.Count + " samples, " + new FileInfo(psArgs[1]).Length + " bytes");

            return 0;
        }

        private static void fReadChunks(byte[] pyFile, int piStart, int piEnd, Dictionary<string, Chunk> pOLists, Dictionary<string, byte[]> pOChunks)
        {
            int liAt = piStart;

            while (liAt + 8 <= piEnd)
            {
                string lsId = Encoding.ASCII.GetString(pyFile, liAt, 4);
                int liLength = (int)fU32(pyFile, liAt + 4);

                if (lsId == "LIST")
                {
                    string lsType = Encoding.ASCII.GetString(pyFile, liAt + 8, 4);
                    byte[] lyData = new byte[liLength - 4];
                    Array.Copy(pyFile, liAt + 12, lyData, 0, lyData.Length);
                    pOLists[lsType] = new Chunk { Id = lsType, Data = lyData };
                    fReadChunks(pyFile, liAt + 12, liAt + 8 + liLength, pOLists, pOChunks);
                }
                else
                {
                    byte[] lyData = new byte[liLength];
                    Array.Copy(pyFile, liAt + 8, lyData, 0, liLength);
                    pOChunks[lsId] = lyData;
                }

                liAt += 8 + liLength + (liLength & 1);
            }
        }

        /// <summary>Whether a zone stays under a key limit: always without a limit or without a
        /// key range of its own (a global zone has none), else when the range takes a key.</summary>
        private static bool fZoneTakes(byte[] pyGen, int piFrom, int piTo, HashSet<int> pOKeys)
        {
            if (pOKeys == null)
                return true;

            for (int liGen = piFrom; liGen < piTo; liGen++)
            {
                if (fU16(pyGen, liGen * 4) != GeneratorKeyRange)
                    continue;

                int liLow = pyGen[(liGen * 4) + 2];
                int liHigh = pyGen[(liGen * 4) + 3];

                foreach (int liKey in pOKeys)
                {
                    if (liKey >= liLow && liKey <= liHigh)
                        return true;
                }

                return false;
            }

            return true;
        }

        private static void fKeepSample(byte[] pyShdr, int piSample, List<int> pOKept, Dictionary<int, int> pOMap)
        {
            if (pOMap.ContainsKey(piSample))
                return;

            pOMap[piSample] = pOKept.Count;
            pOKept.Add(piSample);

            // A left or right half pulls in its partner (sample types 2 and 4, linked by index).
            int liType = fU16(pyShdr, (piSample * 46) + 44);

            if ((liType & 0x06) != 0)
                fKeepSample(pyShdr, fU16(pyShdr, (piSample * 46) + 42), pOKept, pOMap);
        }

        private static void fCopyGenerators(byte[] pyGen, int piFrom, int piTo, int piIndexGenerator, Dictionary<int, int> pOMap, MemoryStream pOOut)
        {
            for (int liGen = piFrom; liGen < piTo; liGen++)
            {
                byte[] lyGen = new byte[4];
                Array.Copy(pyGen, liGen * 4, lyGen, 0, 4);

                if (fU16(lyGen, 0) == piIndexGenerator)
                    fPut16(lyGen, 2, pOMap[fU16(lyGen, 2)]);

                pOOut.Write(lyGen, 0, 4);
            }
        }

        private static void fWriteBag(MemoryStream pOBag, MemoryStream pOGen, MemoryStream pOMod)
        {
            byte[] lyBag = new byte[4];
            fPut16(lyBag, 0, (int)(pOGen.Length / 4));
            fPut16(lyBag, 2, (int)(pOMod.Length / 10));
            pOBag.Write(lyBag, 0, 4);
        }

        private static void fWriteTerminal(MemoryStream pOOut, int piSize, string psName, int piBagOffset, int piBag)
        {
            byte[] lyRecord = new byte[piSize];
            Encoding.ASCII.GetBytes(psName).CopyTo(lyRecord, 0);
            fPut16(lyRecord, piBagOffset, piBag);
            pOOut.Write(lyRecord, 0, piSize);
        }

        private static void fWriteChunk(Stream pOOut, string psId, byte[] pyData)
        {
            byte[] lyHeader = new byte[8];
            Encoding.ASCII.GetBytes(psId).CopyTo(lyHeader, 0);
            fPut32(lyHeader, 4, (uint)pyData.Length);
            pOOut.Write(lyHeader, 0, 8);
            pOOut.Write(pyData, 0, pyData.Length);

            if ((pyData.Length & 1) != 0)
                pOOut.WriteByte(0);
        }

        private static void fWriteList(Stream pOOut, string psType, byte[] pyData)
        {
            byte[] lyBody = new byte[pyData.Length + 4];
            Encoding.ASCII.GetBytes(psType).CopyTo(lyBody, 0);
            Array.Copy(pyData, 0, lyBody, 4, pyData.Length);
            fWriteChunk(pOOut, "LIST", lyBody);
        }

        private static int fU16(byte[] pyData, int piAt) => pyData[piAt] | (pyData[piAt + 1] << 8);

        private static uint fU32(byte[] pyData, int piAt) => (uint)(pyData[piAt] | (pyData[piAt + 1] << 8) | (pyData[piAt + 2] << 16) | (pyData[piAt + 3] << 24));

        private static void fPut16(byte[] pyData, int piAt, int piValue)
        {
            pyData[piAt] = (byte)piValue;
            pyData[piAt + 1] = (byte)(piValue >> 8);
        }

        private static void fPut32(byte[] pyData, int piAt, uint puValue)
        {
            pyData[piAt] = (byte)puValue;
            pyData[piAt + 1] = (byte)(puValue >> 8);
            pyData[piAt + 2] = (byte)(puValue >> 16);
            pyData[piAt + 3] = (byte)(puValue >> 24);
        }
    }
}
