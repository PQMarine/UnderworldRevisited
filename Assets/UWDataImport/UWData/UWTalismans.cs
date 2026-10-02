using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The eight talismans of the virtues. In the game they carry their own names, which are NOT in
	/// the object name block but in string block 1 at numbers 262 to 270:
	///
	///   the Book of Honesty, the Taper of Sacrifice (twice, for extinguished and burning),
	///   the Wine of Compassion, the Standard of Honor, the Shield of Valor,
	///   the Cup of Wonder, the Sword of Justice, the Ring of Humility
	///
	/// They are recognised by their object id. This is not a shot in the dark: the nine
	/// associated objects are the only ones of their kind that carry the value 60 in COMOBJ.DAT
	/// byte 9 - the same field that decides whether an item is lost in water and lava
	/// (see UWCommonObjectProperties.SinksInLiquid). A talisman that could be lost in the
	/// lake would also be bad for the game.
	///
	/// The mapping of item to virtue follows from the names themselves - a sword can
	/// only be the talisman of Justice, a shield only that of Valor. The trigger was
	/// the user's observation that our "shiny sword" is called "Sword of Justice" in the original
	/// (2026-09-01).
	/// </summary>
	public static class UWTalismans
	{
		/// <summary>String block of the general messages, in which the names are stored.</summary>
		public const int NameStringBlock = 1;

		private static readonly Dictionary<int, int> myNames = new Dictionary<int, int>
		{
			{ 310, 262 }, // a_book        -> the Book of Honesty
			{ 147, 263 }, // a_taper       -> the Taper of Sacrifice
			{ 151, 264 }, // a_lit taper   -> the Taper of Sacrifice
			{ 191, 265 }, // bottle of wine-> the Wine of Compassion
			{ 287, 266 }, // a_standard    -> the Standard of Honor
			{ 55, 267 },  // a_shiny shield-> the Shield of Valor
			{ 174, 268 }, // a_shiny cup   -> the Cup of Wonder
			{ 10, 269 },  // a_shiny sword -> the Sword of Justice
			{ 54, 270 }   // an_iron ring  -> the Ring of Humility
		};

		public static bool IsTalisman(int piObjectId)
		{
			return myNames.ContainsKey(piObjectId);
		}

		/// <summary>
		/// The full name including article, e.g. "the Sword of Justice" - without the full stop that
		/// the string carries at its end. Returns null if it is not a talisman.
		/// </summary>
		public static string GetName(int piObjectId, UWStrings pOStrings)
		{
			int liString;

			if (pOStrings == null || !myNames.TryGetValue(piObjectId, out liString))
				return null;

			try
			{
				string lsName = pOStrings.Blocks[NameStringBlock].Strings[liString];

				if (string.IsNullOrWhiteSpace(lsName))
					return null;

				return lsName.Trim().TrimEnd('.');
			}
			catch
			{
				return null;
			}
		}
	}
}
