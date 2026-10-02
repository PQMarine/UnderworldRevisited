using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>All six original fonts, loaded on demand. See UWFont.</summary>
	public class UWFonts
	{
		public enum FontType
		{
			/// <summary>font4x5p.sys - small font.</summary>
			Small,

			/// <summary>font5x6i.sys - italic, character stats.</summary>
			Italic,

			/// <summary>font5x6p.sys - normal font for messages and scrolls.</summary>
			Normal,

			/// <summary>fontbig.sys - large font of the cutscenes.</summary>
			Big,

			/// <summary>fontbutn.sys - buttons.</summary>
			Button,

			/// <summary>fontchar.sys - character creation.</summary>
			CharacterGeneration
		}

		private static readonly Dictionary<FontType, string> mOFileNames = new Dictionary<FontType, string>
		{
			{ FontType.Small, "FONT4X5P.SYS" },
			{ FontType.Italic, "FONT5X6I.SYS" },
			{ FontType.Normal, "FONT5X6P.SYS" },
			{ FontType.Big, "FONTBIG.SYS" },
			{ FontType.Button, "FONTBUTN.SYS" },
			{ FontType.CharacterGeneration, "FONTCHAR.SYS" }
		};

		private readonly string msDataPath;

		private readonly Dictionary<FontType, UWFont> mOFonts = new Dictionary<FontType, UWFont>();

		public UWFonts(string psDataPath)
		{
			msDataPath = psDataPath;
		}

		public UWFont Get(FontType peType)
		{
			if (mOFonts.TryGetValue(peType, out UWFont lOCached))
				return lOCached;

			UWFont lOFont = new UWFont(Path.Combine(msDataPath, mOFileNames[peType]));
			mOFonts[peType] = lOFont;

			return lOFont;
		}
	}
}
