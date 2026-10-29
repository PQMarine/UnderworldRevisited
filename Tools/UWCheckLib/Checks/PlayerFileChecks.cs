using System.IO;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// A SAVE GAME'S PLAYER.DAT OF EITHER GAME (Docs/UW2-PLAN.md phase 1 step 3, 2026-10-29): UWPlayerFile
    /// reads and writes it back byte for byte. The first game's saves (--saves) are also deciphered as
    /// UWPlayerData does; the second game's (--uw2-saves, an UNDEROM2 folder of saves the DOS game
    /// wrote) are held to what the game writes: the key the name's first letter xor 0xAA, the length
    /// 1 + 0x37D + 2 + 0x5B + 8 x the count word (UE FORMATS.md "PLAYER.DAT"), the inventory's objects
    /// within the saved ones, the position on the map. Nothing of the saves is pinned or copied: they
    /// are the player's. The key's rule is held for the DOS game's saves; the first game's saves this
    /// port wrote before 2026-10-29 carry its old fixed key 0x2A until they are saved again (since then
    /// the port keys by the name as DOS does, UWPlayerData.KeyFor), reported, not held.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckPlayerFiles(CheckContext pOContext)
        {
            Check.Group("A save game's PLAYER.DAT of either game (UW2 plan, phase 1 step 3)");

            // Each cipher is its own inverse, a block shorter than two bytes left as it is (the second game's).
            byte[] lyData = new byte[UWPlayerFile.Uw2RecordSize + 1];

            new System.Random(3).NextBytes(lyData);

            string lsBad = string.Empty;

            foreach (UWGame leGame in new[] { UWGame.Uw1, UWGame.Uw2 })
            {
                byte[] lyOnce = UWPlayerFile.Cipher(lyData, 0x5A, leGame);

                if (fSameBytes(lyOnce, lyData) || !fSameBytes(UWPlayerFile.Cipher(lyOnce, 0x5A, leGame), lyData))
                    lsBad += leGame + " ";
            }

            Check.Str("player file: each game's cipher changes the bytes and is its own inverse", lsBad, string.Empty);
            // Eleven whole blocks and one byte: that byte stays as it is.
            byte[] lyOneOver = new byte[(11 * UWPlayerFile.CipherBlock) + 1];

            System.Array.Copy(lyData, lyOneOver, lyOneOver.Length);
            Check.Bool("player file: the second game's cipher leaves a last block of one byte as it is",
                UWPlayerFile.Cipher(lyOneOver, 0x5A, UWGame.Uw2)[lyOneOver.Length - 1] == lyOneOver[lyOneOver.Length - 1], true);

            if (pOContext.SavesPath != null && Directory.Exists(pOContext.SavesPath))
            {
                foreach (string lsSave in Directory.GetDirectories(pOContext.SavesPath, "SAVE?"))
                    fCheckPlayerFile(Path.Combine(lsSave, "PLAYER.DAT"), UWGame.Uw1, "UW1 " + Path.GetFileName(lsSave));
            }

            if (pOContext.Uw2SavesPath == null)
            {
                Check.Pass("player file: UW2", "no --uw2-saves folder given: the second game's saves are skipped");

                return;
            }

            foreach (string lsSave in Directory.GetDirectories(pOContext.Uw2SavesPath, "SAVE?"))
                fCheckPlayerFile(Path.Combine(lsSave, "PLAYER.DAT"), UWGame.Uw2, "UW2 " + Path.GetFileName(lsSave));
        }

        private static void fCheckPlayerFile(string psPath, UWGame peGame, string psLabel)
        {
            if (!File.Exists(psPath))
                return;

            byte[] lyFile = File.ReadAllBytes(psPath);
            UWPlayerFile lOFile = UWPlayerFile.Read(lyFile, peGame);

            // DATA's PLAYER.DAT is the plain template, not a save (UWPlayerData.IsPlainTemplate).
            if (lOFile == null || UWPlayerData.IsPlainTemplate(lyFile))
                return;

            int liExpected = 1 + UWPlayerFile.RecordSize(peGame) + 2 + UWPlayerFile.WorkspaceHeaderSize + (UWPlayerFile.SavedObjectSize * lOFile.CountWord);
            bool lbInventory = lOFile.InventoryHead <= lOFile.SavedObjectCount;

            for (int liSlot = 0; liSlot < UWPlayerFile.UsedSlotCount; liSlot++)
                lbInventory &= lOFile.Slot(liSlot) <= lOFile.SavedObjectCount;

            Check.Str("player file: " + psLabel + " length by the count word, read and written back",
                (lyFile.Length == liExpected) + " " + fSameBytes(lOFile.ToBytes(), lyFile), "True True");

            if (peGame == UWGame.Uw2)
                Check.Bool("player file: " + psLabel + " key the name's first letter xor 0xAA, as DOS writes it", lOFile.KeyFitsName, true);
            else
                Check.Pass("player file: " + psLabel + " key", lOFile.KeyFitsName ? "the DOS game's (from the name)" : "this port's 0x" + lOFile.Key.ToString("X2"));
            Check.Bool("player file: " + psLabel + " inventory head and slots name saved objects", lbInventory, true);

            // The shared head to 0x5D: the position (0x54, 0x56, the tile in the high byte) and the level (0x5C).
            int liTileX = UWBinary.ReadU16(lOFile.Record, 0x54) >> 8;
            int liTileY = UWBinary.ReadU16(lOFile.Record, 0x56) >> 8;
            int liLevel = UWBinary.ReadU16(lOFile.Record, 0x5C);
            int liLevels = peGame == UWGame.Uw1 ? UWLevArkFile.Uw1.LevelCount : UWLevArkFile.Uw2.LevelCount;

            Check.Bool("player file: " + psLabel + " position on the map and level within the game's",
                liTileX < 64 && liTileY < 64 && liLevel >= 1 && liLevel <= liLevels, true);

            if (peGame == UWGame.Uw1)
                Check.Bool("player file: " + psLabel + " deciphers as UWPlayerData does",
                    fSameBytes(lOFile.Record, fFirst(UWPlayerData.Decrypt(lyFile), UWPlayerFile.Uw1RecordSize)), true);
        }

        private static byte[] fFirst(byte[] pyData, int piCount)
        {
            byte[] lyOut = new byte[System.Math.Min(piCount, pyData.Length)];

            System.Array.Copy(pyData, lyOut, lyOut.Length);

            return lyOut;
        }
    }
}
