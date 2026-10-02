using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The small leftover files from the DATA folder that nobody had touched so far.
	/// Three of them are documented, two are not - and those two are deliberately provided
	/// here only as raw bytes, instead of forcing an invented structure onto them.
	/// </summary>
	public class UWMiscDataFiles
	{
		/// <summary>
		/// MONO.DAT: the same shape as LIGHT.DAT - 16 blocks of 256 bytes that map one
		/// palette index onto another, here onto greyscale. The original uses this for
		/// the black-and-white mode.
		/// </summary>
		public class MonochromeMapping
		{
			public const int LevelCount = 16;

			private const int BlockSize = 256;

			private readonly byte[][] myLevels = new byte[LevelCount][];

			public bool IsLoaded { get; private set; }

			public MonochromeMapping(string psDataPath)
			{
				string lsFile = Path.Combine(psDataPath, "MONO.DAT");

				if (!File.Exists(lsFile))
					return;

				byte[] lyData = File.ReadAllBytes(lsFile);

				for (int liLevel = 0; liLevel < LevelCount; liLevel++)
				{
					myLevels[liLevel] = new byte[BlockSize];

					if ((liLevel * BlockSize) + BlockSize <= lyData.Length)
						System.Array.Copy(lyData, liLevel * BlockSize, myLevels[liLevel], 0, BlockSize);
				}

				IsLoaded = true;
			}

			public byte Remap(int piLevel, int piPaletteIndex)
			{
				if (!IsLoaded)
					return (byte)piPaletteIndex;

				int liLevel = piLevel < 0 ? 0 : (piLevel >= LevelCount ? LevelCount - 1 : piLevel);
				int liIndex = piPaletteIndex < 0 ? 0 : (piPaletteIndex >= BlockSize ? BlockSize - 1 : piPaletteIndex);

				return myLevels[liLevel][liIndex];
			}
		}

		/// <summary>
		/// WEAPONS.CM: two 16-colour auxiliary palettes for the attack animations
		/// (uw-formats.txt 3.8). They tint the weapon images to the skin colour of the
		/// chosen character - so far the project shows them untinted.
		/// </summary>
		public class WeaponPalettes
		{
			public const int PaletteCount = 2;

			public const int PaletteSize = 16;

			private readonly byte[][] myPalettes = new byte[PaletteCount][];

			public bool IsLoaded { get; private set; }

			public WeaponPalettes(string psDataPath)
			{
				string lsFile = Path.Combine(psDataPath, "WEAPONS.CM");

				if (!File.Exists(lsFile))
					return;

				byte[] lyData = File.ReadAllBytes(lsFile);

				if (lyData.Length < PaletteCount * PaletteSize)
					return;

				for (int liIndex = 0; liIndex < PaletteCount; liIndex++)
				{
					myPalettes[liIndex] = new byte[PaletteSize];
					System.Array.Copy(lyData, liIndex * PaletteSize, myPalettes[liIndex], 0, PaletteSize);
				}

				IsLoaded = true;
			}

			/// <summary>The 16 main palette indices of one of the two auxiliary palettes.</summary>
			public byte[] Get(int piPalette)
			{
				return IsLoaded && piPalette >= 0 && piPalette < PaletteCount ? myPalettes[piPalette] : null;
			}
		}

		/// <summary>
		/// SKILLS.DAT: which skills are offered per class during character creation
		/// (uw-formats.txt 9.3). The first 0x20 bytes hold the base attributes per class
		/// (uw-formats.txt calls them unknown, see myAttributes), then follow five
		/// groups per class for eight classes. Each group starts with its length, followed
		/// by that many skill numbers. Length 1 means: no choice, the skill is
		/// fixed.
		/// </summary>
		public class CharacterSkills
		{
			public const int ClassCount = 8;

			public const int GroupsPerClass = 5;

			private const int HeaderSize = 0x20;

			private readonly List<byte[]>[] mOGroups = new List<byte[]>[ClassCount];

			/// <summary>
			/// The first 0x20 bytes are NOT unknown: four bytes per class - strength,
			/// dexterity, intelligence as base values, followed by the total of points that
			/// character creation distributes randomly over the three (reference:
			/// chargen.InitClassAttributes, 2026-09-11). Fighter 20/16/12 plus 12, shepherd
			/// 12/12/12 plus 20.
			/// </summary>
			private readonly byte[] myAttributes = new byte[HeaderSize];

			public bool IsLoaded { get; private set; }

			public CharacterSkills(string psDataPath)
			{
				string lsFile = Path.Combine(psDataPath, "SKILLS.DAT");

				if (!File.Exists(lsFile))
					return;

				byte[] lyData = File.ReadAllBytes(lsFile);

				if (lyData.Length <= HeaderSize)
					return;

				System.Array.Copy(lyData, 0, myAttributes, 0, HeaderSize);

				int liCursor = HeaderSize;

				for (int liClass = 0; liClass < ClassCount; liClass++)
				{
					mOGroups[liClass] = new List<byte[]>();

					for (int liGroup = 0; liGroup < GroupsPerClass; liGroup++)
					{
						if (liCursor >= lyData.Length)
							break;

						int liLength = lyData[liCursor++];
						byte[] lySkills = new byte[liLength];

						for (int liIndex = 0; liIndex < liLength && liCursor < lyData.Length; liIndex++)
							lySkills[liIndex] = lyData[liCursor++];

						mOGroups[liClass].Add(lySkills);
					}
				}

				IsLoaded = true;
			}

			/// <summary>The choice groups of a class. A group with exactly one entry
			/// is a fixed assignment without a choice.</summary>
			public IReadOnlyList<byte[]> GetGroups(int piClass)
			{
				return IsLoaded && piClass >= 0 && piClass < ClassCount ? mOGroups[piClass] : null;
			}

			/// <summary>Base values of a class: strength, dexterity, intelligence and the bonus
			/// points to distribute. Without the file false (all values 0).</summary>
			public bool TryGetBaseAttributes(int piClass, out int piStrength, out int piDexterity,
				out int piIntelligence, out int piBonus)
			{
				piStrength = 0;
				piDexterity = 0;
				piIntelligence = 0;
				piBonus = 0;

				if (!IsLoaded || piClass < 0 || piClass >= ClassCount)
					return false;

				piStrength = myAttributes[piClass * 4];
				piDexterity = myAttributes[(piClass * 4) + 1];
				piIntelligence = myAttributes[(piClass * 4) + 2];
				piBonus = myAttributes[(piClass * 4) + 3];

				return true;
			}
		}

		public MonochromeMapping Monochrome { get; private set; }

		public WeaponPalettes WeaponAttackPalettes { get; private set; }

		public CharacterSkills SkillChoices { get; private set; }

		/// <summary>
		/// LIGHTS.DAT, eight bytes, and CHRGEN.DAT, 238 bytes. For neither is there ANY
		/// format description in uw-formats.txt. CHRGEN.DAT has nevertheless been decoded
		/// since 2026-09-11 - see UWCharacterGeneration, which gets the raw bytes from here.
		/// </summary>
		public byte[] RawLights { get; private set; }

		public byte[] RawCharacterGeneration { get; private set; }

		public UWMiscDataFiles(string psDataPath)
		{
			Monochrome = new MonochromeMapping(psDataPath);
			WeaponAttackPalettes = new WeaponPalettes(psDataPath);
			SkillChoices = new CharacterSkills(psDataPath);

			RawLights = fReadRaw(Path.Combine(psDataPath, "LIGHTS.DAT"));
			RawCharacterGeneration = fReadRaw(Path.Combine(psDataPath, "CHRGEN.DAT"));
		}

		private static byte[] fReadRaw(string psFile)
		{
			return File.Exists(psFile) ? File.ReadAllBytes(psFile) : new byte[0];
		}
	}
}
