namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S READABLES AND SMALL USES (2026-11-18, the uses still on the placeholder): UseBook (uw2
	/// obj/USEITEMS.C:451) and BookLook (obj/LOOK.C:232) for books and scrolls, UseWatch (USEITEMS.C:63) and UseCrystal
	/// (USEITEMS.C:89). Engine-free: what a read shows, which quest it sets, which cutscene it plays; the screens say it.
	///
	/// A READABLE is 0x130 to 0x13A (MAJOR_SPEC minor 3, index up to 0xA; the map and the bit of a map are the caller's,
	/// they open the map). Its text number is its link's low nine bits (the quantity field 0x200 plus the number, as in
	/// the first game; UWObjectMechanics.GetBookStringIndex). Index 0xB to 0xF of the class (the dead plants, the bottle,
	/// the stick, the resilient sphere) do nothing on use.
	/// </summary>
	public static class UWReadRulesUw2
	{
		public const int FirstReadable = 0x130;

		/// <summary>The class's last index UseBook reads (OBJ_INCLASS(obj) &gt; 0xA returns).</summary>
		public const int LastReadable = 0x13A;

		public const int BitOfAMap = 0x139;

		public const int Map = 0x13A;

		/// <summary>The text whose reading sets quests[26] bit 2 (quest 106 in get_quest's numbering, four bits to a long).</summary>
		public const int QuestText = 6;

		public const int QuestOfText = 106;

		/// <summary>Text numbers from 0x100 on: UseBook calls the empty make_stew, BookLook prints the text alone.</summary>
		public const int HeaderlessText = 0x100;

		/// <summary>show_cutscene(0x100 + text) for a readable with ID_FLAG10 (none in the shipped data).</summary>
		public const int FirstBookCutscene = 0x100;

		/// <summary>"The watch reads " and the full stop after the time (0x60). UseWatch asks get_string(0x227): block 1
		/// (0x200) string 0x27 - not string 0x227, which does not exist (per user 2026-11-18: the line came without it).</summary>
		public const int WatchString = 0x27;

		public const int FullStopString = 0x60;

		/// <summary>"You \"read\" the crystal's signature: " (0x160).</summary>
		public const int CrystalString = 0x160;

		public const int Pocketwatch = 0xAF;

		public const int StorageCrystal = 0xA1;

		/// <summary>game_clock ticks in a minute and in an hour (256 a second).</summary>
		public const int TicksPerMinute = 0x3C00;

		public const int TicksPerHour = 0xE1000;

		public static bool IsReadable(int piId) => piId >= FirstReadable && piId <= LastReadable;

		/// <summary>What reading shows.</summary>
		public enum ReadEnum
		{
			/// <summary>Nothing (an index above 0xA, a bit of a map looked at, an enchanted scroll looked at, make_stew).</summary>
			Nothing,

			/// <summary>A cutscene (Cutscene).</summary>
			Cutscene,

			/// <summary>"You read the &lt;name&gt;..." and the text (Text; QuestOfText set when SetsQuest).</summary>
			HeaderAndText,

			/// <summary>The text alone (a number from 0x100 on, looked at).</summary>
			TextAlone,

			/// <summary>An enchanted scroll used: its traps, its spell, used up when cast.</summary>
			Spell
		}

		public struct Reading
		{
			public ReadEnum Kind;

			/// <summary>The text's number in block 3 (0-based, the original's).</summary>
			public int Text;

			public int Cutscene;

			public bool SetsQuest;
		}

		/// <summary>The text number: the link's low nine bits (the quantity field holds 0x200 plus it).</summary>
		public static int TextNumber(UWObject pOBook) => pOBook == null ? 0 : pOBook.Quantity & 0x1FF;

		/// <summary>ID_FLAG10, word 0 bit 10 (Flags bit 1).</summary>
		public static bool PlaysCutscene(UWObject pOBook) => pOBook != null && (pOBook.Flags & UWObjectMechanics.BookCutsceneFlag) != 0;

		/// <summary>
		/// UseBook from the pack (how 1; in the world it does nothing, and UseObj runs no traps after it). The map and the
		/// bit of a map are not asked here. Not enchanted: ID_FLAG10 plays 0x100 + the text, a number below 0x100 is read
		/// with its header (text 6 sets quest 106), a higher one calls the empty make_stew. Enchanted: the spell.
		/// </summary>
		public static Reading Use(UWObject pOBook, bool pbCarried)
		{
			if (pOBook == null || !pbCarried || !IsReadable(pOBook.ID))
				return new Reading { Kind = ReadEnum.Nothing };

			if (pOBook.IsEnchanted)
				return new Reading { Kind = ReadEnum.Spell };

			return fRead(pOBook, false);
		}

		/// <summary>BookLook with print 1 or more (LookAt's SpecialLook, which asks it for index below 10 only, so the map
		/// is never asked and BookLook's map line never shows): a bit of a map and an enchanted scroll nothing, ID_FLAG10
		/// the cutscene, a number above 0xFF the text alone, else the header and the text (text 6 sets quest 106 here
		/// too).</summary>
		public static Reading Look(UWObject pOBook)
		{
			if (pOBook == null || pOBook.ID < FirstReadable || pOBook.ID >= Map)
				return new Reading { Kind = ReadEnum.Nothing };

			if (pOBook.ID == BitOfAMap || pOBook.IsEnchanted)
				return new Reading { Kind = ReadEnum.Nothing };

			return fRead(pOBook, true);
		}

		private static Reading fRead(UWObject pOBook, bool pbLook)
		{
			int liText = TextNumber(pOBook);

			if (PlaysCutscene(pOBook))
				return new Reading { Kind = ReadEnum.Cutscene, Text = liText, Cutscene = FirstBookCutscene + liText };

			if (liText >= HeaderlessText)
				return new Reading { Kind = pbLook ? ReadEnum.TextAlone : ReadEnum.Nothing, Text = liText };

			return new Reading { Kind = ReadEnum.HeaderAndText, Text = liText, SetsQuest = liText == QuestText };
		}

		/// <summary>UseWatch: the time on a 12-hour dial after "The watch reads ", e.g. "7:05" or "12:00".</summary>
		public static string WatchTime(int piGameClock)
		{
			uint luClock = (uint)piGameClock;
			int liHours = (int)(luClock / TicksPerHour % 12);
			int liMinutes = (int)(luClock / TicksPerMinute % 60);

			if (liHours == 0)
				liHours = 12;

			return liHours + ":" + (liMinutes / 10) + (liMinutes % 10);
		}

		/// <summary>UseCrystal: four characters, letter digit letter digit, from the quality.</summary>
		public static string CrystalSignature(int piQuality)
		{
			char[] lcLabel = new char[4];
			int liValue = (0x49 - piQuality) * 2;

			for (int liAt = 0; liAt < 4; liAt++)
			{
				lcLabel[liAt] = (liAt & 1) != 0 ? (char)('0' + fMod(liValue, 10)) : (char)('A' + fMod(liValue, 26));
				liValue += piQuality + 0x1E;
			}

			return new string(lcLabel);
		}

		/// <summary>C's remainder (the quality is 0 to 63, so the value stays positive; kept in range regardless).</summary>
		private static int fMod(int piValue, int piBy)
		{
			int liRest = piValue % piBy;

			return liRest < 0 ? liRest + piBy : liRest;
		}
	}
}
