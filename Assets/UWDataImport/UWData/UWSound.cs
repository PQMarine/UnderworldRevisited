using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Access to the "sound" folder, which lies NEXT TO "data" - just like "cuts" and "crit".
	/// It was missing entirely from the local copy of the game data and was fetched from the
	/// CD image afterwards.
	///
	/// Contents: 42 SPEECH RECORDINGS (*.VOC), 24 music pieces (*.XMI, twelve titles, each once
	/// for General MIDI and once for AdLib), the timbre libraries UW.AD and UW.MT,
	/// the output drivers (*.ADV) and SOUNDS.DAT.
	///
	/// IMPORTANT: the VOC files are NOT sound effects but spoken dialogue lines from
	/// the intro (recognised by the user, 2026-08-29, and confirmed from the data - see
	/// SpeechLineIndices). Ultima Underworld 1 has no sampled effects at all;
	/// creaking doors and sword blows are synthesised on the AdLib, SOUNDS.DAT names the
	/// TVFX program and note for each (see Effect). Anyone looking for a door sound here is looking in the wrong place.
	///
	/// Loaded on demand and cached.
	/// </summary>
	public class UWSound
	{
		/// <summary>The twelve music pieces with their names according to uw-formats.txt 2.3.1. The
		/// file name is "UW" plus number for General MIDI and "AW" plus number for
		/// AdLib.</summary>
		public static readonly Dictionary<string, string> MusicTrackNames = new Dictionary<string, string>
		{
			{ "01", "Introduction" },
			{ "02", "Dark Abyss" },
			{ "03", "Descent" },
			{ "04", "Wanderer" },
			{ "05", "Battlefield" },
			{ "06", "Combat" },
			{ "07", "Injured" },
			{ "10", "Armed" },
			{ "11", "Victory" },
			{ "12", "Death" },
			{ "13", "Fleeing" },
			{ "15", "Maps & Legends" }
		};

		private readonly string msSoundPath;

		private readonly Dictionary<string, UWVoc> mOVocCache = new Dictionary<string, UWVoc>();

		private readonly Dictionary<string, UWXmi> mOXmiCache = new Dictionary<string, UWXmi>();

		/// <summary>SOUNDS.DAT raw. For uw1 there is NO format description - the
		/// documentation only describes the uw2 version. From the file size, however, one can
		/// derive its layout: 121 bytes, first byte 24, and
		/// 1 + 24 * 5 = 121 works out exactly. So 24 entries of five bytes; their
		/// meaning is decoded in Effect.</summary>
		public byte[] RawSoundsDat { get; private set; }

		public int SoundsDatEntryCount { get; private set; }

		public const int SoundsDatEntrySize = 5;

		/// <summary>
		/// A sound from SOUNDS.DAT - decoded since 2026-09-11 (reference:
		/// SoundsDatLoader): number of the TVFX program in UW.AD bank 1, MIDI note (for the
		/// MT-32 path, the AdLib path ignores it), velocity, then a word of duration.
		/// Duration 0xFFFF or bit 15 set means: plays until the program itself ends.
		/// </summary>
		public struct Effect
		{
			public byte Patch;

			public byte Note;

			public byte Velocity;

			public ushort Duration;

			/// <summary>Lifetime in sixtieths for UWTvfxVoice, -1 for unlimited: the
			/// original divides the word by 16 and counts it down at 16 Hz.</summary>
			public int LifetimeTicks =>
				Duration == 0xFFFF || (Duration & 0x8000) != 0 ? -1 : Duration * 15 / 64;
		}

		private readonly List<Effect> mOEffects = new List<Effect>();

		/// <summary>The game's 24 sounds, by number.</summary>
		public IReadOnlyList<Effect> Effects => mOEffects;

		private UWAdlibBank mOAdlibBank;

		/// <summary>The AdLib timbres (UW.AD), loaded on demand.</summary>
		public UWAdlibBank AdlibBank
		{
			get
			{
				if (mOAdlibBank == null && IsAvailable)
					mOAdlibBank = new UWAdlibBank(Path.Combine(msSoundPath, "UW.AD"));

				return mOAdlibBank;
			}
		}

		public bool IsAvailable { get; private set; }

		public UWSound(string psDataPath)
		{
			string lsRoot = Path.GetDirectoryName(psDataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			msSoundPath = Path.Combine(lsRoot, "SOUND");
			RawSoundsDat = new byte[0];

			if (!Directory.Exists(msSoundPath))
				return;

			IsAvailable = true;

			string lsSoundsDat = Path.Combine(msSoundPath, "SOUNDS.DAT");

			if (File.Exists(lsSoundsDat))
			{
				RawSoundsDat = File.ReadAllBytes(lsSoundsDat);

				// First byte is the count - only accept it if the file size
				// matches, otherwise it would be a guess.
				if (RawSoundsDat.Length > 0
					&& 1 + (RawSoundsDat[0] * SoundsDatEntrySize) == RawSoundsDat.Length)
				{
					SoundsDatEntryCount = RawSoundsDat[0];

					for (int liAt = 0; liAt < SoundsDatEntryCount; liAt++)
					{
						int liOffset = 1 + (liAt * SoundsDatEntrySize);

						mOEffects.Add(new Effect
						{
							Patch = RawSoundsDat[liOffset],
							Note = RawSoundsDat[liOffset + 1],
							Velocity = RawSoundsDat[liOffset + 2],
							Duration = (ushort)(RawSoundsDat[liOffset + 3] | (RawSoundsDat[liOffset + 4] << 8))
						});
					}
				}
			}
		}

		/// <summary>
		/// For each sound number the string index of the spoken sentence, derived from the
		/// cutscene control files: their text-play command names string number and sound number
		/// together (see UWCutsceneScript). All 42 recordings come from CS000, the intro,
		/// and the mapping works out completely - 42 lines for 42 files.
		///
		/// The text is thus in string block 0x0C00. This is needed for subtitles:
		/// speech without the matching text would be worthless in the remake.
		/// </summary>
		public IReadOnlyDictionary<int, int> SpeechLineIndices
		{
			get
			{
				fEnsureSpeechMapping();

				return mOSpeechLines;
			}
		}

		/// <summary>String block containing the spoken sentences - the intro.</summary>
		public const int SpeechStringBlock = 0x0C00;

		private Dictionary<int, int> mOSpeechLines;

		private void fEnsureSpeechMapping()
		{
			if (mOSpeechLines != null)
				return;

			mOSpeechLines = new Dictionary<int, int>();

			string lsRoot = Path.GetDirectoryName(msSoundPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			string lsScript = Path.Combine(Path.Combine(lsRoot, "CUTS"), "CS000.N00");

			UWCutsceneScript lOScript = new UWCutsceneScript(lsScript);

			if (!lOScript.IsLoaded)
				return;

			foreach (KeyValuePair<int, int> lOPair in lOScript.GetSpokenLines())
			{
				// Key is the string index, value the sound number.
				if (!mOSpeechLines.ContainsKey(lOPair.Value))
					mOSpeechLines[lOPair.Value] = lOPair.Key;
			}
		}

		/// <summary>A speech recording, e.g. "00" for 00.VOC.</summary>
		public UWVoc GetSpeech(string psName)
		{
			if (!IsAvailable)
				return null;

			if (mOVocCache.TryGetValue(psName, out UWVoc lOCached))
				return lOCached;

			UWVoc lOVoc = new UWVoc(Path.Combine(msSoundPath, psName + ".VOC"));
			mOVocCache[psName] = lOVoc;

			return lOVoc;
		}

		/// <summary>A music piece. pbAdlib selects the AdLib version (AW..) instead of the
		/// General MIDI version (UW..).</summary>
		public UWXmi GetMusic(string psNumber, bool pbAdlib = false)
		{
			if (!IsAvailable)
				return null;

			string lsName = (pbAdlib ? "AW" : "UW") + psNumber;

			if (mOXmiCache.TryGetValue(lsName, out UWXmi lOCached))
				return lOCached;

			UWXmi lOXmi = new UWXmi(Path.Combine(msSoundPath, lsName + ".XMI"));
			mOXmiCache[lsName] = lOXmi;

			return lOXmi;
		}

		/// <summary>All available speech recordings, without extension.</summary>
		public List<string> ListSpeechClips()
		{
			List<string> lOResult = new List<string>();

			if (!IsAvailable)
				return lOResult;

			foreach (string lsFile in Directory.GetFiles(msSoundPath, "*.VOC"))
				lOResult.Add(Path.GetFileNameWithoutExtension(lsFile));

			lOResult.Sort();

			return lOResult;
		}
	}
}
