namespace UWDataImport.UWData
{
	public class UWAuxPalette : IPalette
	{
		private readonly byte[] myAuxPaletteData;

		private readonly UWPalette mOPalette;

		public UWAuxPalette(byte[] pyAuxPaletteData, UWPalette pOPalette)
		{
			myAuxPaletteData = pyAuxPaletteData;
			mOPalette = pOPalette;
		}

		public byte[] GetBGRA(int piIndex)
		{
			return mOPalette.GetBGRA(myAuxPaletteData[piIndex]);
		}

		public byte[] GetARGB(int piIndex)
		{
			return mOPalette.GetARGB(myAuxPaletteData[piIndex]);
		}

		public byte[] GetRGB(int piIndex)
		{
			return mOPalette.GetRGB(myAuxPaletteData[piIndex]);
		}

		public UWColor32 GetUWColor(int piIndex)
		{
			return mOPalette.GetUWColor(myAuxPaletteData[piIndex]);
		}

		/// <summary>The index in the main palette that this 4-bit index points to.
		/// Needed where not the colour matters but the index itself has a
		/// meaning - for example the transparency triggers from XFER.DAT (see
		/// UWTransparencyTables).</summary>
		public byte GetMainPaletteIndex(int piIndex)
		{
			return myAuxPaletteData[piIndex];
		}
	}
}
