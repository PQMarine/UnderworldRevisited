using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 6.2 (2026-10-24): the renderer's lookup tables are built engine-free
    /// (UWLookupTables) and Unity only uploads them (UWTextureFactory). Pinned: the bytes of every
    /// table against what the Unity builders (UWShadePalette, UWTextureArrayBuilder,
    /// UWPaletteEffects) put into their textures before the move - read back from the textures in
    /// a scratch run of the editor, CRC-32 of the raw texture data - with the colour help off and
    /// in two of its modes. The inverse cubes were never readable after their upload; theirs are
    /// pinned against the old Unity builder's bytes computed in the same scratch run.
    ///
    /// ONE KNOWN RUNTIME DIFFERENCE: with the colour help ON, a few cells of the palette 0 tables
    /// (the colour table, the XFER colours, the inverse cube) come out one step apart here and in
    /// Unity - UWColourFilter's float arithmetic rounds a value on the .5 edge differently under
    /// .NET and under Unity's Mono (2 to 15 bytes of 16384, never more than 1; the same filter code
    /// ran in Unity before the move, and the Unity textures were byte-identical before and after it).
    /// Those three pins are this runtime's values; everything with the colour help off, and every
    /// other table, equals the Unity bytes.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckLookupTables(DataImport pOData)
        {
            Check.Group("Lookup tables (UWLookupTables - cleanup step 6.2)");

            // The colour help as the scratch run set it: mode, strength, brightness, contrast.
            fCheckShownTables(pOData, "off", fFilter(UWColourFilter.ModeEnum.Off, 1f, 1f, 1f),
                0x80A74A7Bu, 0x011D6B9Fu, 0x89C459D5u, 0x5EB3EDFBu, 0x30CC0BFBu, 0x576346F9u, 0xD9ECBE4Bu);
            fCheckShownTables(pOData, "green", fFilter(UWColourFilter.ModeEnum.Green, 1f, 1.1f, 0.9f),
                0x85209BBCu, 0x4C4F567Cu, 0x05ED18DAu, 0x13B510BEu, 0xF539B747u, 0x377F9D28u, 0x1C1F4A2Cu);
            fCheckShownTables(pOData, "red", fFilter(UWColourFilter.ModeEnum.Red, 0.6f, 1.1f, 0.9f),
                0xE7C5671Eu, 0x588CF7F5u, 0xDA73BCAFu, 0x1CCEB443u, 0x657BA5EAu, 0x59AB7F4Eu, 0x7D9FED25u);

            fExpectTable("shade table", UWLookupTables.ShadeTable(pOData, false), 16 * 8, 0x041D66B6u);
            fExpectTable("shade table, diagonal stretch", UWLookupTables.ShadeTable(pOData, true), 16 * 8, 0x5985B459u);
            fExpectTable("rotation table", UWLookupTables.RotationTable(), 256 * 60 * 4, 0xBB726572u);
            fExpectTable("XFER colours raw", UWLookupTables.XferColours(pOData, null), 256 * 6 * 4, 0x30CC0BFBu);
            fExpectTable("light table effect raw", UWLookupTables.LightTableEffect(pOData, null), 256 * 48 * 4, 0x576346F9u);
            fExpectTable("inverse lookup raw", UWLookupTables.InverseLookup(pOData, null), 64 * 64 * 64, 0xD9ECBE4Bu);
            fExpectTable("raw palettes", UWLookupTables.RawPalettes(pOData), 256 * 8 * 4, 0xB83F5EA4u);
            fExpectTable("grime tint", UWLookupTables.GrimeTint(pOData.Palettes.GetPalette(0)), 256 * 4, 0xB53D1D93u);
            fExpectTable("material lookup", UWLookupTables.MaterialLookup(), 256 * 4, 0xDB3AA241u);
        }

        private static void fCheckShownTables(DataImport pOData, string psFilter, UWColourFilter pOFilter, uint puColour,
            uint puPalette3, uint puMono, uint puMonoPalette5, uint puXfer, uint puLightEffect, uint puInverse)
        {
            string lsSuffix = ", colour help " + psFilter;

            fExpectTable("colour table" + lsSuffix, UWLookupTables.ColourTable(pOData, 0, false, pOFilter, out bool lbMono),
                256 * 16 * 4, puColour);
            fExpectBool("colour table uses LIGHT.DAT" + lsSuffix, lbMono, false);
            fExpectTable("colour table, palette 3" + lsSuffix, UWLookupTables.ColourTable(pOData, 3, false, pOFilter, out _),
                256 * 16 * 4, puPalette3);
            fExpectTable("Night Vision table" + lsSuffix, UWLookupTables.ColourTable(pOData, 0, true, pOFilter, out lbMono),
                256 * 16 * 4, puMono);
            fExpectBool("Night Vision table uses MONO.DAT" + lsSuffix, lbMono, true);
            fExpectTable("Night Vision table, palette 5" + lsSuffix, UWLookupTables.ColourTable(pOData, 5, true, pOFilter, out _),
                256 * 16 * 4, puMonoPalette5);
            fExpectTable("XFER colours" + lsSuffix, UWLookupTables.XferColours(pOData, pOFilter), 256 * 6 * 4, puXfer);
            fExpectTable("light table effect" + lsSuffix, UWLookupTables.LightTableEffect(pOData, pOFilter), 256 * 48 * 4,
                puLightEffect);
            fExpectTable("inverse lookup" + lsSuffix, UWLookupTables.InverseLookup(pOData, pOFilter), 64 * 64 * 64, puInverse);
        }

        private static UWColourFilter fFilter(UWColourFilter.ModeEnum peMode, float pfStrength, float pfBrightness, float pfContrast)
        {
            UWColourFilter lOFilter = new UWColourFilter();

            lOFilter.Set(peMode, pfStrength, pfBrightness, pfContrast);

            return lOFilter;
        }

        private static void fExpectTable(string psName, byte[] pyBytes, int piLength, uint puCrc)
        {
            if (pyBytes == null)
            {
                Check.Fail("lookup table: " + psName, "not built");

                return;
            }

            uint luCrc = fCrc32(pyBytes);

            if (pyBytes.Length == piLength && luCrc == puCrc)
                Check.Pass("lookup table: " + psName, string.Format("{0} bytes, CRC {1:X8}", piLength, puCrc));
            else
                Check.Fail("lookup table: " + psName, string.Format("{0} bytes, CRC {1:X8}; expected {2} bytes, CRC {3:X8}",
                    pyBytes.Length, luCrc, piLength, puCrc));
        }
    }
}
