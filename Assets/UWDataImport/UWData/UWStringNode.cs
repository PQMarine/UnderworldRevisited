namespace UWDataImport.UWData
{
	public class UWStringNode
	{
		/// <summary>The character of a leaf, 0 to 255. UNSIGNED SINCE 2026-10-26 (translation wave 1,
		/// found 2026-10-09): it was read as an sbyte, so a byte from 0x80 up became a negative number
		/// and the string decoder's (char) cast gave U+FF80 and above - a character no font has, which
		/// UWFontRenderer skipped without a word. A STRINGS.PAK with umlauts (a translated one) lost
		/// them. Now a byte is the Latin-1 character of its value (0xE4 is a-umlaut). The English
		/// STRINGS.PAK holds no byte above 0x7F, so its strings are unchanged.</summary>
		public int CharSymbol;

		public int ParentNode;

		public int LeftChild;

		public int RightChild;

		public UWStringNode(uint plStringData)
		{
			CharSymbol = (byte)(plStringData & 0xFF);
			ParentNode = (byte)((plStringData & 0xFF00) >> 8);
			LeftChild = (byte)((plStringData & 0xFF0000) >> 16);
			RightChild = (byte)((plStringData & 0xFF000000u) >> 24);
		}
	}
}
