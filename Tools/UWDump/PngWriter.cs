using System;
using System.IO;
using System.IO.Compression;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The smallest PNG writer that serves the map pictures: 8-bit RGBA, no filter, one IDAT
    /// chunk compressed with ZLibStream. No engine and no imaging package - the tools stay on
    /// the plain .NET SDK.
    /// </summary>
    internal static class PngWriter
    {
        private static readonly uint[] miCrcTable = fBuildCrcTable();

        /// <summary>Writes piWidth x piHeight pixels, four bytes (R, G, B, A) each. pbBottomUp:
        /// the first row of pyRgba is the picture's BOTTOM row, as in a Unity texture.</summary>
        public static void Write(string psPath, int piWidth, int piHeight, byte[] pyRgba, bool pbBottomUp)
        {
            if (pyRgba == null || pyRgba.Length != piWidth * piHeight * 4)
                throw new ArgumentException("Expected " + (piWidth * piHeight * 4) + " bytes of RGBA.");

            int liStride = piWidth * 4;
            byte[] lyCompressed;

            using (MemoryStream lOBuffer = new MemoryStream())
            {
                using (ZLibStream lOZlib = new ZLibStream(lOBuffer, CompressionLevel.Optimal, true))
                {
                    for (int liRow = 0; liRow < piHeight; liRow++)
                    {
                        int liSource = pbBottomUp ? piHeight - 1 - liRow : liRow;

                        // Filter type 0 (none) before every row.
                        lOZlib.WriteByte(0);
                        lOZlib.Write(pyRgba, liSource * liStride, liStride);
                    }
                }

                lyCompressed = lOBuffer.ToArray();
            }

            byte[] lyHeader = new byte[13];

            fPutBigEndian(lyHeader, 0, (uint)piWidth);
            fPutBigEndian(lyHeader, 4, (uint)piHeight);
            lyHeader[8] = 8;   // bits per channel
            lyHeader[9] = 6;   // colour type: RGBA
            lyHeader[10] = 0;  // deflate
            lyHeader[11] = 0;  // adaptive filtering (every row uses type 0)
            lyHeader[12] = 0;  // not interlaced

            using (FileStream lOFile = new FileStream(psPath, FileMode.Create, FileAccess.Write))
            {
                lOFile.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);
                fWriteChunk(lOFile, "IHDR", lyHeader);
                fWriteChunk(lOFile, "IDAT", lyCompressed);
                fWriteChunk(lOFile, "IEND", new byte[0]);
            }
        }

        private static void fWriteChunk(Stream pOStream, string psType, byte[] pyData)
        {
            byte[] lyLength = new byte[4];
            byte[] lyType = System.Text.Encoding.ASCII.GetBytes(psType);
            byte[] lyCrc = new byte[4];

            fPutBigEndian(lyLength, 0, (uint)pyData.Length);

            uint liCrc = 0xFFFFFFFFu;

            liCrc = fUpdateCrc(liCrc, lyType);
            liCrc = fUpdateCrc(liCrc, pyData);

            fPutBigEndian(lyCrc, 0, liCrc ^ 0xFFFFFFFFu);

            pOStream.Write(lyLength, 0, 4);
            pOStream.Write(lyType, 0, 4);
            pOStream.Write(pyData, 0, pyData.Length);
            pOStream.Write(lyCrc, 0, 4);
        }

        private static void fPutBigEndian(byte[] pyTarget, int piAt, uint piValue)
        {
            pyTarget[piAt] = (byte)(piValue >> 24);
            pyTarget[piAt + 1] = (byte)(piValue >> 16);
            pyTarget[piAt + 2] = (byte)(piValue >> 8);
            pyTarget[piAt + 3] = (byte)piValue;
        }

        private static uint fUpdateCrc(uint piCrc, byte[] pyData)
        {
            foreach (byte lyByte in pyData)
                piCrc = miCrcTable[(piCrc ^ lyByte) & 0xFF] ^ (piCrc >> 8);

            return piCrc;
        }

        /// <summary>The CRC-32 of the PNG specification (polynomial 0xEDB88320).</summary>
        private static uint[] fBuildCrcTable()
        {
            uint[] liTable = new uint[256];

            for (uint liAt = 0; liAt < 256; liAt++)
            {
                uint liValue = liAt;

                for (int liBit = 0; liBit < 8; liBit++)
                    liValue = (liValue & 1) != 0 ? 0xEDB88320u ^ (liValue >> 1) : liValue >> 1;

                liTable[liAt] = liValue;
            }

            return liTable;
        }
    }
}
