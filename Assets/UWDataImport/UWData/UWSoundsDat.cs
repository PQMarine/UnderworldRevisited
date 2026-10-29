using System.Collections.Generic;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// SOUNDS.DAT OF EITHER GAME, AND THE SOUND FILES' NAMES (Docs/UW2-PLAN.md phase 1 step 4,
	/// 2026-10-29; Underworld Exhumed, docs/FORMATS.md "Sound and music", SOUND.C:1294, UW1 SOUND.C:695).
	///
	/// THE FILE: a byte, the number of effects, then per effect the patch, the note, the volume and
	/// the length HIGH BYTE FIRST - 5 bytes in the first game; the second adds a byte "digital" (nonzero
	/// plays a VOC file) and the priority, byte 6 + byte 7 x 200 (8 bytes).
	///
	/// THE FIRST GAME'S EFFECTS come from here too (UWSound, since 2026-10-29): until then UWSound read
	/// the length low byte first, where UW1 reads len_hi before len_lo (UW1 SOUND.C:703-708).
	///
	/// THE NAMES: an effect's VOC in the second game is UWnn.VOC from effect 100 (nn = effect - 100),
	/// SPnn.VOC below it, nn decimal (SOUND.C:383-394); the first game's speech is nn.VOC. A music theme is
	/// the theme number in octal behind "UWR" for the Roland card and "UWA" for the others in the second
	/// game (SOUND.C:876-880), "UW" (MT-32, General MIDI) and "AW" (AdLib) in the first (UWSound.GetMusic).
	/// </summary>
	public sealed class UWSoundsDat
	{
		public const int Uw1EntrySize = 5;

		public const int Uw2EntrySize = 8;

		/// <summary>The second game's priority: byte 6 plus byte 7 times this.</summary>
		public const int PriorityHighFactor = 200;

		/// <summary>From this effect on, the second game plays UWnn.VOC.</summary>
		public const int FirstUwVocEffect = 100;

		public struct Effect
		{
			public int Patch;

			public int Note;

			public int Volume;

			/// <summary>High byte first, as both games read it.</summary>
			public int Length;

			/// <summary>The second game's: nonzero plays the effect's VOC file.</summary>
			public int Digital;

			public int Priority;
		}

		public readonly List<Effect> Effects = new List<Effect>();

		public static int EntrySize(UWGame peGame) => peGame == UWGame.Uw1 ? Uw1EntrySize : Uw2EntrySize;

		/// <summary>The file's bytes; the count byte decides, a short file gives the effects it holds.</summary>
		public UWSoundsDat(byte[] pyFile, UWGame peGame)
		{
			if (pyFile == null || pyFile.Length < 1)
				return;

			int liSize = EntrySize(peGame);

			for (int liAt = 0; liAt < pyFile[0] && 1 + ((liAt + 1) * liSize) <= pyFile.Length; liAt++)
			{
				int liBase = 1 + (liAt * liSize);

				Effects.Add(new Effect
				{
					Patch = pyFile[liBase],
					Note = pyFile[liBase + 1],
					Volume = pyFile[liBase + 2],
					Length = (pyFile[liBase + 3] << 8) | pyFile[liBase + 4],
					Digital = peGame == UWGame.Uw1 ? 0 : pyFile[liBase + 5],
					Priority = peGame == UWGame.Uw1 ? 0 : pyFile[liBase + 6] + (pyFile[liBase + 7] * PriorityHighFactor)
				});
			}
		}

		/// <summary>The second game's VOC of an effect, without the folder: "UW00", "SP05".</summary>
		public static string Uw2VocName(int piEffect)
		{
			return piEffect >= FirstUwVocEffect ? "UW" + (piEffect - FirstUwVocEffect).ToString("00") : "SP" + piEffect.ToString("00");
		}

		/// <summary>A theme's music file, without the folder and ".XMI": the octal number behind each
		/// game's prefix for the device - the first game "UW" (MT-32 and General MIDI) or "AW" (AdLib), the
		/// second "UWR" (Roland) or "UWA".</summary>
		public static string MusicName(UWGame peGame, int piTheme, bool pbRoland)
		{
			string lsNumber = new StringBuilder().Append((char)('0' + (piTheme >> 3))).Append((char)('0' + (piTheme & 7))).ToString();

			if (peGame == UWGame.Uw1)
				return (pbRoland ? "UW" : "AW") + lsNumber;

			return (pbRoland ? "UWR" : "UWA") + lsNumber;
		}
	}
}
