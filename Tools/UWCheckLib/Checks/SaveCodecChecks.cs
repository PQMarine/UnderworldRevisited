using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 2.9 (2026-10-14): UWSaveCodec, the game on disk. Every save game loads into a
    /// session and saves out of it unchanged - LEV.ARK (with the active-list repair the round trip
    /// expects), PLAYER.DAT, BGLOBALS.DAT and DESC byte for byte - through the whole slot write the
    /// game uses, into a scratch folder (the save games themselves are only read). Two sessions
    /// loaded from the same folder share no object. A new character saves, loads back and saves
    /// again identically, and the slot it was saved into becomes its save game.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckSaveCodec(CheckContext pOContext)
        {
            Check.Group("Save codec (UWSaveCodec: load -> save byte identity, a new character's save and reload)");

            UWGameData lOData = pOContext.Data.GameData;
            string lsScratchRoot = Path.Combine(Path.GetTempPath(), "uwselfcheck-savecodec");

            try
            {
                if (Directory.Exists(lsScratchRoot))
                    Directory.Delete(lsScratchRoot, true);

                Directory.CreateDirectory(lsScratchRoot);

                fCheckCodecWorldOnly(lOData, lsScratchRoot);

                int liSaves = 0;

                if (!string.IsNullOrEmpty(pOContext.SavesPath) && Directory.Exists(pOContext.SavesPath))
                {
                    List<string> lsFolders = new List<string>(Directory.GetDirectories(pOContext.SavesPath));

                    lsFolders.Sort(StringComparer.Ordinal);

                    foreach (string lsSave in lsFolders)
                    {
                        if (!File.Exists(Path.Combine(lsSave, "LEV.ARK")) || !File.Exists(Path.Combine(lsSave, "PLAYER.DAT")))
                            continue;

                        liSaves++;
                        fCheckCodecSlot(lOData, lsSave, Path.Combine(lsScratchRoot, Path.GetFileName(lsSave)));
                    }
                }

                if (liSaves == 0)
                    Check.Pass("save codec: saves", "none given (--saves <folder> checks every SAVE* in it)");

                fCheckCodecNewCharacter(lOData, lsScratchRoot);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(lsScratchRoot))
                        Directory.Delete(lsScratchRoot, true);
                }
                catch
                {
                }
            }
        }

        /// <summary>The data folder's world: loaded fresh and written over a copy of its LEV.ARK.</summary>
        private static void fCheckCodecWorldOnly(UWGameData pOData, string psScratchRoot)
        {
            UWGameSession lOSession = UWSaveCodec.Load(pOData, null);
            string lsScratch = Path.Combine(psScratchRoot, "DATA");

            Check.Bool("save codec: DATA loads a fresh world", !lOSession.World.IsSavegame && lOSession.World.Levels != null
                && lOSession.World.Levels.Count == UWLevArkLayout.LevelBlockCount, true);
            Check.Bool("save codec: a fresh world is not saved without a folder", UWSaveCodec.SaveWorld(lOSession), false);

            Directory.CreateDirectory(lsScratch);
            File.Copy(Path.Combine(pOData.DataPath, "LEV.ARK"), Path.Combine(lsScratch, "LEV.ARK"));

            Check.Bool("save codec: DATA LEV.ARK through SaveWorld", UWSaveCodec.SaveWorld(lOSession, lsScratch), true);
            fExpectSameArk("save codec: DATA LEV.ARK", Path.Combine(lsScratch, "LEV.ARK"), Path.Combine(pOData.DataPath, "LEV.ARK"));
        }

        /// <summary>One save game through Load and the whole slot write into a scratch folder.</summary>
        private static void fCheckCodecSlot(UWGameData pOData, string psSave, string psScratch)
        {
            string lsName = Path.GetFileName(psSave);
            UWGameSession lOSession = UWSaveCodec.Load(pOData, psSave);
            UWGameSession lOTwin = UWSaveCodec.Load(pOData, psSave);
            List<string> lsShared = new List<string>();

            fCollectShared(lOSession, lOTwin, "session", lsShared, 0);
            Check.Bool("save codec: " + lsName + " loaded twice shares no object (" + string.Join(", ", lsShared) + ")",
                lsShared.Count == 0, true);

            UWPlayerData lOPlayer = lOSession.Player;

            Check.Bool("save codec: " + lsName + " loads as a save game", lOSession.World.IsSavegame && lOPlayer != null && lOPlayer.IsLoaded, true);

            if (lOPlayer == null || !lOPlayer.IsLoaded)
                return;

            string lsDescFile = Path.Combine(psSave, "DESC");
            byte[] lyDesc = File.Exists(lsDescFile) ? File.ReadAllBytes(lsDescFile) : new byte[0];
            // Read as the game reads it (UWSavegameSlots.GetName): a byte per character, so a name
            // saved with umlauts (their Latin-1 bytes, translation wave 2) is written back as it was;
            // ASCII would have made them question marks.
            System.Text.StringBuilder lODescription = new System.Text.StringBuilder();

            foreach (byte lyByte in lyDesc)
                lODescription.Append((char)lyByte);

            string lsDescription = lODescription.ToString();

            // The position as the file holds it: the snapshot's z is the raw word at 0x58 (the
            // host writes the motion core's z there), while UWPlayerData.ZPosition is that word
            // shifted down by three. No state: everything else stays as loaded.
            string lsError = UWSaveCodec.Save(lOSession, psScratch, new UWSavegameStore.Snapshot
            {
                LevelIndex = lOPlayer.DungeonLevel - 1,
                PositionX = lOPlayer.PositionX,
                PositionY = lOPlayer.PositionY,
                PositionZ = UWBinary.Read16OrZero(lOPlayer.PlainBytes, 0x58),
                Heading = lOPlayer.Heading,
                State = null
            }, lsDescription);

            Check.Bool("save codec: " + lsName + " saves into a scratch slot" + (lsError != null ? " (" + lsError + ")" : ""), lsError == null, true);

            if (lsError != null)
                return;

            Check.Bool("save codec: " + lsName + " stays the loaded save game", lOSession.World.SavegamePath == psSave, true);

            // Object 1 of the player's level is the character a second time, from the PLAYER.DAT
            // just written (as the original keeps them); saves of older builds of ours held a
            // stale copy there, so the expectation takes the player's record for it.
            fExpectSameArk("save codec: " + lsName + " LEV.ARK", Path.Combine(psScratch, "LEV.ARK"), Path.Combine(psSave, "LEV.ARK"),
                lOPlayer.DungeonLevel - 1, UWPlayerData.GetLevelPlayerSlot(psSave));
            fExpectSameFile("save codec: " + lsName + " PLAYER.DAT",
                UWPlayerData.Decrypt(File.ReadAllBytes(Path.Combine(psScratch, "PLAYER.DAT"))),
                UWPlayerData.Decrypt(File.ReadAllBytes(Path.Combine(psSave, "PLAYER.DAT"))));

            if (File.Exists(Path.Combine(psSave, "BGLOBALS.DAT")))
                fExpectSameFile("save codec: " + lsName + " BGLOBALS.DAT",
                    File.ReadAllBytes(Path.Combine(psScratch, "BGLOBALS.DAT")), File.ReadAllBytes(Path.Combine(psSave, "BGLOBALS.DAT")));

            if (lyDesc.Length <= UWSavegameStore.MaxDescriptionLength)
                fExpectSameFile("save codec: " + lsName + " DESC", File.ReadAllBytes(Path.Combine(psScratch, "DESC")), lyDesc);
        }

        /// <summary>A new character: saved (the slot becomes its save game), loaded back, saved again.</summary>
        private static void fCheckCodecNewCharacter(UWGameData pOData, string psScratchRoot)
        {
            UWGameSession lOSession = UWSaveCodec.Load(pOData, null);
            UWPlayerData lONew = UWPlayerData.FromPlain(UWPlayerData.BuildNewCharacter(new UWPlayerData.NewCharacter { Name = "Codec" }));
            string lsFirst = Path.Combine(psScratchRoot, "NEW1");
            string lsSecond = Path.Combine(psScratchRoot, "NEW2");

            lOSession.ReplacePlayer(lONew);

            UWSavegameStore.Snapshot lOSnapshot = new UWSavegameStore.Snapshot
            {
                LevelIndex = lONew.DungeonLevel - 1,
                PositionX = lONew.PositionX,
                PositionY = lONew.PositionY,
                PositionZ = UWBinary.Read16OrZero(lONew.PlainBytes, 0x58),
                Heading = lONew.Heading
            };

            string lsError = UWSaveCodec.Save(lOSession, lsFirst, lOSnapshot, "codec");

            Check.Bool("save codec: a new character saves" + (lsError != null ? " (" + lsError + ")" : ""), lsError == null, true);

            if (lsError != null)
                return;

            Check.Bool("save codec: the slot becomes the new character's save game", lOSession.World.SavegamePath == lsFirst, true);

            UWGameSession lOLoaded = UWSaveCodec.Load(pOData, lsFirst);

            Check.Str("save codec: the new character loads back", lOLoaded.Player != null ? lOLoaded.Player.Name : "-", "Codec");

            lsError = UWSaveCodec.Save(lOLoaded, lsSecond, lOSnapshot, "codec");
            Check.Bool("save codec: ... and saves again" + (lsError != null ? " (" + lsError + ")" : ""), lsError == null, true);

            if (lsError != null)
                return;

            foreach (string lsFile in new[] { "LEV.ARK", "BGLOBALS.DAT", "DESC" })
                fExpectSameFile("save codec: a new character's " + lsFile + " after save, load, save",
                    File.ReadAllBytes(Path.Combine(lsSecond, lsFile)), File.ReadAllBytes(Path.Combine(lsFirst, lsFile)));

            fExpectSameFile("save codec: a new character's PLAYER.DAT after save, load, save",
                UWPlayerData.Decrypt(File.ReadAllBytes(Path.Combine(lsSecond, "PLAYER.DAT"))),
                UWPlayerData.Decrypt(File.ReadAllBytes(Path.Combine(lsFirst, "PLAYER.DAT"))));
        }

        /// <summary>A written LEV.ARK against the one it came from, with the active lists repaired
        /// as the round trip expects (see fRepairActiveList) and, when given, object 1 of the
        /// player's level replaced by the player's record.</summary>
        private static void fExpectSameArk(string psName, string psWritten, string psSource,
            int piPlayerLevel = -1, byte[] pyPlayerSlot = null)
        {
            byte[] lyExpected = File.ReadAllBytes(psSource);
            int liCount = lyExpected[0] | (lyExpected[1] << 8);

            for (int liLevel = 0; liLevel < 9 && liLevel < liCount; liLevel++)
            {
                uint luOffset = BitConverter.ToUInt32(lyExpected, 2 + (liLevel * 4));

                if (luOffset != 0 && luOffset + UWLevArkLayout.BlockSize <= lyExpected.Length)
                {
                    fRepairActiveList(lyExpected, (int)luOffset);

                    if (liLevel == piPlayerLevel && pyPlayerSlot != null && pyPlayerSlot.Length == UWLevArkLayout.MobileRecordSize)
                        Array.Copy(pyPlayerSlot, 0, lyExpected, (int)luOffset + UWLevArkLayout.PlayerSlotOffset, pyPlayerSlot.Length);
                }
            }

            fExpectSameFile(psName, File.ReadAllBytes(psWritten), lyExpected);
        }

        private static void fExpectSameFile(string psName, byte[] pyWritten, byte[] pyExpected)
        {
            int liFirst = -1;

            if (pyWritten != null && pyExpected != null && fSameBytes(pyWritten, pyExpected, out liFirst))
                Check.Pass(psName, pyExpected.Length + " bytes identical");
            else
                Check.Fail(psName, pyWritten == null || pyExpected == null ? "missing"
                    : fDescribeDifference(pyWritten, pyExpected, liFirst));
        }
    }
}
