namespace UWDataImport.UWData
{
	public class UWPalette : IPalette
	{
		public byte[] Red = new byte[256];

		public byte[] Green = new byte[256];

		public byte[] Blue = new byte[256];

		public byte[] Alpha = new byte[256];

		public UWPalette(byte[] pyPaletteData)
		{
			for (int i = 0; i < pyPaletteData.Length; i += 3)
			{
				byte lyR = pyPaletteData[i];
				byte lyG = pyPaletteData[i + 1];
				byte lyB = pyPaletteData[i + 2];

				Red[i / 3] = (byte)(lyR << 2);
				Green[i / 3] = (byte)(lyG << 2);
				Blue[i / 3] = (byte)(lyB << 2);

				// The transparency marker is NOT "red channel == 0" (that also applies to
				// real, deliberately drawn black, e.g. the black outline of
				// item/UI icons - both had red==0 and so far became equally
				// transparent, which swallowed the outline). Confirmed by user pixel
				// inspection: the actual marker colour is an extremely dark blue
				// (HSV hue 240 degrees, saturation 100%) - with red=green=0 EVERY positive
				// blue value automatically has exactly this hue/saturation, regardless of the exact
				// blue value, so "red=0 AND green=0 AND blue>0" suffices as a test without
				// knowing the exact blue value. Real black (red=green=blue=0) stays
				// opaque.
				Alpha[i / 3] = (byte)((lyR == 0 && lyG == 0 && lyB != 0) ? 0u : 255u);
			}
		}

		/// <summary>
		/// The palette slot whose colour comes closest to the given one.
		///
		/// Needed where a computed blend colour has to go back onto a palette slot
		/// - the palette renderer only has slots, no in-between colours. This is
		/// an approximation; the original blends onto what is already in the image (XFER.DAT,
		/// see UWTransparencyTables), and would land on a different slot depending on the background.
		///
		/// The transparency marker is excluded, it is not a colour.
		/// </summary>
		public byte GetNearestIndex(byte pyRed, byte pyGreen, byte pyBlue)
		{
			int liKey = (pyRed << 16) | (pyGreen << 8) | pyBlue;

			if (mONearest == null)
				mONearest = new System.Collections.Generic.Dictionary<int, byte>();

			byte lyCached;

			if (mONearest.TryGetValue(liKey, out lyCached))
				return lyCached;

			int liBest = 0;
			int liBestDistance = int.MaxValue;

			for (int liAt = 0; liAt < 256; liAt++)
			{
				if (Alpha[liAt] == 0)
					continue;

				int liRed = Red[liAt] - pyRed;
				int liGreen = Green[liAt] - pyGreen;
				int liBlue = Blue[liAt] - pyBlue;

				int liDistance = (liRed * liRed) + (liGreen * liGreen) + (liBlue * liBlue);

				if (liDistance >= liBestDistance)
					continue;

				liBestDistance = liDistance;
				liBest = liAt;
			}

			mONearest[liKey] = (byte)liBest;

			return (byte)liBest;
		}

		private System.Collections.Generic.Dictionary<int, byte> mONearest;

		public byte[] GetARGB(int piIndex)
		{
			return new byte[4]
			{
				Alpha[piIndex],
				Red[piIndex],
				Green[piIndex],
				Blue[piIndex]
			};
		}

		public byte[] GetBGRA(int piIndex)
		{
			byte[] array = new byte[4];
			array[3] = Alpha[piIndex];
			array[2] = Red[piIndex];
			array[1] = Green[piIndex];
			array[0] = Blue[piIndex];
			return array;
		}

		public byte[] GetRGB(int piIndex)
		{
			return new byte[3]
			{
				Red[piIndex],
				Green[piIndex],
				Blue[piIndex]
			};
		}

		public UWColor32 GetUWColor(int piIndex)
		{
			return new UWColor32(Red[piIndex], Green[piIndex], Blue[piIndex], Alpha[piIndex]);
		}

		public override string ToString()
		{
			return $"R:{Red[0]} G:{Green[0]} B:{Blue[0]} A:{Alpha[0]}";
		}
	}
}
