using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The character stats from "player.dat" (uw-formats.txt 9.2.1). This is
	/// the structure the original uses: three base attributes, twenty skills (attack,
	/// defense and the eighteen named ones in Skill), vitality,
	/// mana, hunger, fatigue, level, experience, carrying capacity and position.
	///
	/// The file in the DATA folder is the starting character ("gronkey") and is UNENCRYPTED.
	/// The player.dat of a save game, on the other hand, has a leading key byte and
	/// 210 encrypted bytes - that is what Decrypt (and Encrypt for writing) is for.
	/// </summary>
	public class UWPlayerData
	{
		/// <summary>The eighteen named skills in file order starting at offset 0x0023. Attack
		/// and defense, the other two of the twenty, sit directly before them (see Attack,
		/// Defense).</summary>
		public enum Skill
		{
			Unarmed,
			Sword,
			Axe,
			Mace,
			Missile,
			Mana,
			Lore,
			Casting,
			Traps,
			Search,
			Track,
			Sneak,
			Repair,
			Charm,
			Picklock,
			Acrobat,
			Appraise,
			Swimming
		}

		/// <summary>The twenty-four runes in the order they sit on the shelf -
		/// it matches objects 232 to 255 (string block 4, entries 233 to 256).</summary>
		public enum Rune
		{
			An,
			Bet,
			Corp,
			Des,
			Ex,
			Flam,
			Grav,
			Hur,
			In,
			Jux,
			Kal,
			Lor,
			Mani,
			Nox,
			Ort,
			Por,
			Quas,
			Rel,
			Sanct,
			Tym,
			Uus,
			Vas,
			Wis,
			Ylem
		}

		private const int NameOffset = 0x00;

		private const int NameLength = 14;

		private const int StrengthOffset = 0x1E;

		public const int SkillsOffset = 0x23;

		/// <summary>Vitality, mana and level in the decrypted block. Until 2026-09-04
		/// they only existed as raw numbers in the readout.</summary>
		public const int CurrentVitalityOffset = 0x35;

		public const int MaxVitalityOffset = 0x36;

		public const int CurrentManaOffset = 0x37;

		public const int MaxManaOffset = 0x38;

		public const int LevelOffset = 0x3D;

		public const int SkillCount = 18;

		/// <summary>String block 2: the skill names begin here, one per skill number - for the
		/// stats page, the shrine and character creation alike (declared three times until
		/// 2026-09-18).</summary>
		public const int SkillNameStringIndex = 32;

		/// <summary>Skills top out at 30.</summary>
		public const int MaxSkillValue = 30;

		public const int RuneCount = 24;

		/// <summary>First rune object (An). The shelf images are simply the
		/// inventory icons of these objects from OBJECTS.GR, 14x14 visible in a 16x16 image.
		/// SPELLS.GR was the wrong lead - it holds the spell icons.</summary>
		public const int FirstRuneObjectId = 232;

		/// <summary>The rune bit field: three bytes from 0x44, read from the top down - An
		/// is bit 7 of the first byte. Cross-checked against a save game of the user: it shows
		/// FF FF F9, and in the original exactly Vas and Wis are missing there (2026-09-03).</summary>
		public const int RuneFlagsOffset = 0x44;

		/// <summary>The three slots of the selected runes. 24 means empty - decimal, right after Ylem (23), that is how the original writes it. Until 2026-09-10 this said 0x24: it happened to read correctly because 24 lies outside the runes, but it wrote 36.</summary>
		private const int SelectedRunesOffset = 0x47;

		public const int SelectedRuneNone = 24;

		/// <summary>Three slots on the rune shelf.</summary>
		private const int miSelectedRuneSlots = 3;

		// 210, NOT 220: the original only encrypts up to 0xD1. At 0xD2 the number of
		// inventory records is stored in plain text - verified on all four of the user's
		// save games (2026-09-10). The 220 went unnoticed before because reading and
		// writing apply the same XOR and cancel out; with the count field inside it the
		// original would have read a scrambled number.
		private const int EncryptedLength = 210;

		public string Name { get; private set; }

		public int Strength { get; private set; }

		public int Dexterity { get; private set; }

		public int Intelligence { get; private set; }

		public int Attack { get; private set; }

		public int Defense { get; private set; }

		private readonly int[] miSkills = new int[SkillCount];

		private readonly bool[] mbRunes = new bool[RuneCount];

		private readonly int[] miSelectedRunes = new int[3];

		public int CurrentVitality { get; private set; }

		public int MaxVitality { get; private set; }

		public int CurrentMana { get; private set; }

		public int MaxMana { get; private set; }

		public int Hunger { get; private set; }

		public int Fatigue { get; private set; }

		/// <summary>Byte 0x3B: five-minute blocks since the last meal, up to 255 - eating heals an
		/// eighth of it, at most eight (see UWPlayerVitals.ChangeHunger).</summary>
		public int MealHealCounter { get; private set; }

		/// <summary>Byte 0x3C: raised with fatigue every five minutes and read nowhere in UW.EXE
		/// that we know of; kept counting so the save matches the original's.</summary>
		public int Counter3C { get; private set; }

		/// <summary>The game clock value - see UWGameClock.</summary>
		public int ClockValue { get; private set; }

		public int Level { get; private set; }

		/// <summary>Carried weight in tenths of a stone. Read from the same field (0x4A) as
		/// CarriedWeight.</summary>
		public int CurrentWeight { get; private set; }

		/// <summary>Carrying capacity in tenths of a stone. The docs give the formula
		/// 300 + 13 * strength, but it does NOT match the stored value. What matches is
		/// STRENGTH TIMES TWENTY: Yulanthe with strength 15 carries 300, strength 21 carries 420, a
		/// character freshly created in the original with strength 25 carries 500 (2026-09-11).</summary>
		public int MaxWeight { get; private set; }

		/// <summary>Carried load in tenths of a stone, as stored in the save game.</summary>
		public int CarriedWeight { get; private set; }

		/// <summary>Remaining capacity in tenths of a stone - this is the number the
		/// original shows below the paperdoll, though divided by ten there.</summary>
		public int RemainingCapacity => MaxWeight - CarriedWeight;

		/// <summary>Experience in tenths of a point.</summary>
		public long Experience { get; private set; }

		public int AvailableSkillPoints { get; private set; }

		/// <summary>All skill points ever earned. This decides whether new experience
		/// yields another one - one per full 3000.</summary>
		public int TotalSkillPoints { get; private set; }

		/// <summary>Character class 0 to 7. The name is in string block 2 from 0x17.</summary>
		public int CharacterClass { get; private set; }

		/// <summary>Whether the character is female - decides which half of BODIES.GR is used
		/// and the armour images.</summary>
		public bool IsFemale { get; private set; }

		/// <summary>Body shape 0 to 4. Together with the gender it gives the image in
		/// BODIES.GR: body plus five if female (the reference: uimanager_paperdoll.SetBody).
		/// </summary>
		public int Body { get; private set; }

		/// <summary>Left-handed character: bit 0 of byte 0x64 is clear. UW.EXE takes the weapon
		/// from inventory slot 8 minus that bit (CalculateAttackResults_seg022_230E_6B9,
		/// seg022_230E_F16), so a left-hander fights with the left hand.</summary>
		public bool IsLeftHanded { get; private set; }

		/// <summary>The easy difficulty: byte 0xB4 nonzero (0 Standard, 1 Easy, chosen at the
		/// character creation). UW.EXE halves melee and missile damage to the player on it
		/// (AttackerAppliesFinalDamage_seg022_8A5, label A28). Read since 2026-09-20 (deviation
		/// 47); the save writer starts from the file, so the byte survives a save.</summary>
		public bool IsEasyDifficulty { get; private set; }

		/// <summary>Fine position within the level, NOT a tile index - the values are too large
		/// for that (save game Yulanthe: 823 and 32781).</summary>
		public int PositionX { get; private set; }

		public int PositionY { get; private set; }

		/// <summary>Tile the character stands on - the upper byte of the fine position.</summary>
		public int TileX => PositionX >> 8;

		public int TileY => PositionY >> 8;

		public int ZPosition { get; private set; }

		public int Heading { get; private set; }

		/// <summary>Level, one-based as in the original.</summary>
		public int DungeonLevel { get; private set; }

		/// <summary>
		/// Which level the moonstone lies on - the target of Gate Travel. Zero means
		/// "none", in which case the spell only reports that the stone is not available.
		///
		/// CHECKED AGAINST THE SAVE GAMES (2026-09-07): in all four of the user's save games
		/// the lower nibble holds a 2, and exactly on level 2 lies the only
		/// moonstone in the world (object 0x126 on tile 9/35, counted across all nine
		/// levels of LEV.ARK). The reference also sets 2 for a fresh character.
		///
		/// The UPPER nibble of the same byte holds the level of the silver tree where one
		/// is resurrected - the byte is called "Silver Tree and Moonstone" there
		/// (UWReverseEngineering, 2026-09-08). It is read separately as SilverTreeLevel,
		/// hence the mask here.
		/// </summary>
		public int MoonstoneLevel { get; private set; }

		/// <summary>Upper nibble of 0x5E: the dungeon level (1-based) of the planted silver tree,
		/// 0 for none. Set by UW.EXE PlantSilverSeed_ovr143_12FB, cleared when the tree is taken
		/// back (seg040 SilverTree) or by Armageddon; death teleports there
		/// (DeathAndResurrection_ovr143_1610).</summary>
		public int SilverTreeLevel { get; private set; }

		/// <summary>0x62 bit 2: the talismans can be destroyed in the volcano. Set when
		/// Garamon is buried (UW.EXE UseBones_seg040_719, there "or [bx+62h], 4"; the reference
		/// counts the key byte too and says 0x63).</summary>
		public bool TalismansDestroyable { get; private set; }

		/// <summary>How many incense visions have already been seen (0 to 3) - byte 0x61, bits 0
		/// and 1 (the reference: playerdatquest.IncenseCounter at its 0x62). The first three uses
		/// show the three visions in order, after that it is random (see UWItemDrag).</summary>
		public int IncenseCounter { get; set; }

		/// <summary>0x62 bit 3: Garamon is buried - no more dreams of him.</summary>
		public bool GaramonBuried { get; private set; }

		/// <summary>0x60 bit 7: the Cup of Wonder has already appeared (UW.EXE
		/// SpawnCupOfWonder_seg014_14C3, "or [bx+60h], 80h").</summary>
		public bool CupOfWonderFound { get; private set; }

		/// <summary>0x60 bit 6: the Key of Truth was chanted out of the shrine once
		/// (ChantMantraAtShrine_ovr143_48D label 621). It is not given a second time.</summary>
		public bool KeyOfTruthGiven { get; private set; }

		/// <summary>0x60 bit 5: Tybal's orb is destroyed (the orb rock sets it) - see
		/// UWTybalOrbRules.</summary>
		public bool OrbDestroyed { get; private set; }

		/// <summary>0xB0: the maximum mana kept aside while the orb drains it in Tybal's lair -
		/// see UWTybalOrbRules.</summary>
		public int OrbManaBackup { get; private set; }

		/// <summary>
		/// How drunk the character is, 0 to 63. Six bits, and they do NOT sit on a
		/// byte boundary: they are bits 4 to 9 of the word at 0x61 (the reference:
		/// playerdatstatus.intoxication with its mask 0xFC0F at 0x62). In the same word
		/// the mushroom effect sits below, and above it whether Garamon is buried.
		/// </summary>
		public int Intoxication { get; private set; }

		/// <summary>How long the mushroom effect still lasts, 0 to 3. It sits in bits 2
		/// and 3 of the same byte, below the intoxication.</summary>
		public int Hallucination { get; private set; }

		/// <summary>Sound effects on, from bits 0-1 of the settings byte - see
		/// SettingsOffset. In the original this is a per-SAVE setting, not a global one.</summary>
		public bool SoundEnabled { get; private set; }

		/// <summary>Music on, from bits 2-3 of the same byte.</summary>
		public bool MusicEnabled { get; private set; }

		/// <summary>The swim counter at 0xB9 (UW.EXE SetPlayerDataOxB9_seg008_B, SwimSkillCheck_seg028_4AE):
		/// 0x60 on entering water, raised by failed swimming checks, subtracted from the eye
		/// height, above 0x78 the character drowns. Zero out of the water.</summary>
		public int SwimCounter { get; private set; }

		public const int SwimCounterOffset = 0xB9;

		/// <summary>An active spell from the save game - the same three values that
		/// UWActiveSpellEffect keeps at runtime.</summary>
		public struct ActiveSpell
		{
			public int MajorClass;

			public int MinorClass;

			public int Stability;
		}

		/// <summary>
		/// The active spells from the save game, at most three. Contains ONLY the
		/// valid ones - see fParseActiveSpells for why that is not the same as "the three
		/// slots".
		/// </summary>
		public IReadOnlyList<ActiveSpell> ActiveSpells => mOActiveSpells;

		private readonly List<ActiveSpell> mOActiveSpells = new List<ActiveSpell>();

		/// <summary>The first of the three spell slots. One word per slot: lower byte the
		/// id, upper byte the stability.</summary>
		private const int ActiveSpellOffset = 0x3E;

		public const int MaxActiveSpells = 3;

		/// <summary>Word holding the COUNT of active spells - in bits 6 to 9.
		/// </summary>
		private const int ActiveSpellCountOffset = 0x5F;

		/// <summary>
		/// Reads the active spells.
		///
		/// LAYOUT per slot (the reference: playerdatstatus.GetEffectClass/GetEffectStability, its
		/// offsets 0x3F/0x41/0x43 - i.e. ours minus one):
		///
		///   lower byte    the id, with SWAPPED NIBBLES: the major class
		///                 is at the bottom, the minor class at the top. The reference builds it
		///                 on casting as (minor &lt;&lt; 4) + major.
		///   upper byte    the stability, which counts down.
		///
		/// THE COUNT IS ESSENTIAL. A finished spell is not zeroed out, its
		/// slot stays written - only the count goes down. That is exactly what is in
		/// the user's save games (2026-09-08): SAVE1 has count 1 and in slot
		/// zero a light spell (class 0, level 3, stability 28); SAVE2 and SAVE3 have
		/// count 0 but still have leftovers in slot zero (class 3/4 and 0/5, both
		/// with stability 1). Reading the slots without the count brings these leftovers
		/// into the game as active spells.
		/// </summary>
		private void fParseActiveSpells(byte[] pyData)
		{
			mOActiveSpells.Clear();

			int liCount = (fRead16(pyData, ActiveSpellCountOffset) >> 6) & 0xF;

			if (liCount > MaxActiveSpells)
				liCount = MaxActiveSpells;

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				int liWord = fRead16(pyData, ActiveSpellOffset + (liAt * 2));
				int liId = liWord & 0xFF;

				mOActiveSpells.Add(new ActiveSpell
				{
					MajorClass = liId & 0xF,
					MinorClass = (liId >> 4) & 0xF,
					Stability = (liWord >> 8) & 0xFF
				});
			}
		}

		public bool IsLoaded { get; private set; }

		/// <summary>
		/// The quest flags and the game variables from the save game.
		///
		/// READ FROM THE USER'S SAVE GAME (2026-09-07). In SAVE3 five puzzles are
		/// solved, and the values are exactly where the variable traps expect them:
		///
		///   Var 31/32/33 = 7/2/6   the counting puzzle on level 5 (packed: 470)
		///   Var 16 = 51            one of the two targets on level 3
		///   Var 27 = 38            the target on level 4
		///   Var 50/51 = 1          the markers on level 7
		///
		/// The quest flags come before: the first 32 individually as BITS from 0x65, then
		/// six whole bytes from 0x69. The reference reads the same positions one higher
		/// (playerdatquest) because it counts the file's key byte too.
		///
		/// HOW MANY game variables there are is stated neither by the docs nor by the reference - both
		/// simply access them. 64 are read; that covers everything that occurs in uw1
		/// (the highest number used is 51) and stays within the decrypted
		/// range.
		/// </summary>
		private void fReadStoredState(byte[] pyData)
		{
			miQuestFlags = new int[QuestFlagCount];

			for (int liAt = 0; liAt < QuestFlagCount; liAt++)
			{
				miQuestFlags[liAt] = liAt < QuestFlagBitCount
					? (pyData[QuestFlagOffset + (liAt / 8)] >> (liAt % 8)) & 0x1
					: pyData[QuestFlagOffset + (QuestFlagBitCount / 8) + (liAt - QuestFlagBitCount)];
			}

			miGameVariables = new int[GameVariableCount];

			for (int liAt = 0; liAt < GameVariableCount; liAt++)
				miGameVariables[liAt] = pyData[GameVariableOffset + liAt];
		}

		private const int QuestFlagOffset = 0x65;

		/// <summary>This many quest flags are stored as single bits, the rest as whole
		/// bytes.</summary>
		private const int QuestFlagBitCount = 32;

		public const int QuestFlagCount = 38;

		private const int GameVariableOffset = 0x70;

		public const int GameVariableCount = 64;

		private int[] miQuestFlags;

		private int[] miGameVariables;

		/// <summary>Number of quest flags read - see fReadStoredState.</summary>
		public int QuestFlags => miQuestFlags == null ? 0 : miQuestFlags.Length;

		/// <summary>Number of game variables read - see fReadStoredState.</summary>
		public int GameVariables => miGameVariables == null ? 0 : miGameVariables.Length;

		public int GetQuestFlag(int piFlag)
		{
			return miQuestFlags != null && piFlag >= 0 && piFlag < miQuestFlags.Length
				? miQuestFlags[piFlag] : 0;
		}

		public int GetGameVariable(int piVariable)
		{
			return miGameVariables != null && piVariable >= 0 && piVariable < miGameVariables.Length
				? miGameVariables[piVariable] : 0;
		}

		public int GetSkill(Skill peSkill)
		{
			int liIndex = (int)peSkill;

			return liIndex >= 0 && liIndex < SkillCount ? miSkills[liIndex] : 0;
		}

		/// <summary>
		/// Empties the rune bag and the three spell slots - for Armageddon, the only thing
		/// in the game that does this (see UWMiscSpell).
		/// </summary>
		public void ClearRunes()
		{
			for (int liAt = 0; liAt < RuneCount; liAt++)
				mbRunes[liAt] = false;

			for (int liAt = 0; liAt < miSelectedRunes.Length; liAt++)
				miSelectedRunes[liAt] = SelectedRuneNone;
		}

		/// <summary>
		/// A RUNE STONE PUT INTO THE RUNE BAG (read 2026-09-26, ovr119_0, to which the
		/// inventory hands a drop on the bag): the rune is the stone's object id minus
		/// FirstRuneObjectId, its bit is set here and the stone is freed by the caller - no
		/// message. Anything else is refused ("You can only put runes in the rune bag."). The
		/// original lets one id more through, 0x18 above the first stone (object 256), whose
		/// bit would land in the byte after the rune flags - ours takes the 24 stones only.
		/// Until 2026-09-26 the bag took the stone as ordinary contents and no rune was ever
		/// learned (per user, with a fresh character).
		/// </summary>
		public bool TryLearnRuneStone(int piObjectId)
		{
			int liRune = piObjectId - FirstRuneObjectId;

			if (liRune < 0 || liRune >= RuneCount)
				return false;

			mbRunes[liRune] = true;

			return true;
		}

		/// <summary>Is this rune in the bag?</summary>
		public bool HasRune(Rune peRune)
		{
			int liIndex = (int)peRune;

			return liIndex >= 0 && liIndex < RuneCount && mbRunes[liIndex];
		}

		/// <summary>Which rune sits in the given one of the three spell slots, or -1.
		/// </summary>
		public int GetSelectedRune(int piSlot)
		{
			if (piSlot < 0 || piSlot >= miSelectedRunes.Length)
				return -1;

			int liRune = miSelectedRunes[piSlot];

			return liRune >= 0 && liRune < RuneCount ? liRune : -1;
		}

		/// <summary>
		/// Loads a PLAYER.DAT and decrypts it if necessary.
		///
		/// The template shipped in the data folder is unencrypted and has no
		/// key byte; a save game has both. This is detected via the name field: if it holds
		/// readable text, it is the template.
		/// </summary>
		public UWPlayerData(string psDataPath)
		{
			string lsFile = Path.Combine(psDataPath, "PLAYER.DAT");

			if (!File.Exists(lsFile))
				return;

			byte[] lyRaw = File.ReadAllBytes(lsFile);

			fParse(IsPlainTemplate(lyRaw) ? lyRaw : Decrypt(lyRaw));
		}

		private UWPlayerData()
		{
		}

		/// <summary>The decrypted block that was read from. Needed for a newly
		/// created character that is not in any save game yet - on the first save
		/// it becomes the PLAYER.DAT (see UWSavegameWriter).</summary>
		public byte[] PlainBytes { get; private set; }

		/// <summary>From character creation, not yet in any save game.</summary>
		public bool IsNewCharacter { get; private set; }

		/// <summary>A character from an unencrypted block - see BuildNewCharacter.
		/// </summary>
		public static UWPlayerData FromPlain(byte[] pyPlain)
		{
			UWPlayerData lOPlayer = new UWPlayerData();

			if (pyPlain != null)
				lOPlayer.fParse(pyPlain);

			lOPlayer.IsNewCharacter = true;

			return lOPlayer;
		}

		/// <summary>What character creation delivers (see UWCharacterGeneration).</summary>
		public class NewCharacter
		{
			public string Name;

			public int CharacterClass;

			public int Body;

			public bool IsFemale;

			public bool IsLeftHanded;

			/// <summary>0 Standard, 1 Easy.</summary>
			public int Difficulty;

			public int Strength;

			public int Dexterity;

			public int Intelligence;

			public int Attack;

			public int Defense;

			/// <summary>The eighteen named skills.</summary>
			public int[] Skills;

			public int MaxVitality;

			public int MaxMana;

			/// <summary>Where the character starts - the middle of the starting room on level 1.</summary>
			public int StartTileX = 32;

			public int StartTileY = 2;

			/// <summary>Height in eighths of a zpos step: the tile floor there is 12, so
			/// zpos 96 - that is what the original's fresh save contains.</summary>
			public int StartZ = 96 * 8;
		}

		/// <summary>Byte holding the difficulty (the reference: playerdat.difficuly at 0xB5, our
		/// count one lower).</summary>
		private const int DifficultyOffset = 0xB4;

		/// <summary>
		/// The settings bytes 0xB5, 0xB6 and 0xBB - in a save freshly created in the original
		/// they are 35, 08 and FF (per user, SAVE1, 2026-09-11). Our first attempt wrote 30,
		/// 00, 00, and the original did not load the save.
		///
		/// 0xB5 WAS DECODED ON 2026-09-21, prompted by the user: switch the music off, save,
		/// load again and it is still off, while another save has it on as it was saved. It
		/// holds three settings of two bits each, and the fresh 0x35 is 01 01 11 - sound on,
		/// music on, detail level three:
		///
		///   bits 0-1  sound effects, written from seg014_1DC5_754 and read back into it
		///   bits 2-3  music, written from seg014_1DC5_740 (MusicEnabled_dseg_5c99_135)
		///   bits 4-5  the detail level (the reference: DetailLevel)
		///
		/// The writing side is ovr133 labels 8E to B9 (380520-380559), the reading side
		/// labels 1A4 to 1C2 (380792-380822), which hands both values straight to the two
		/// toggles. Zero is off, anything else is on; the original only ever writes 0 or 1.
		/// </summary>
		private const int SettingsOffset = 0xB5;

		/// <summary>Bits 0-1 of SettingsOffset: the sound effects.</summary>
		private const int SoundEnabledShift = 0;

		/// <summary>Bits 2-3: the music.</summary>
		private const int MusicEnabledShift = 2;

		private const int SoundSettingMask = 0x3;

		private const int NewSettings = 0x35;

		private const int NewSettings2 = 0x08;

		private const int NewSettings3Offset = 0xBB;

		private const int NewSettings3 = 0xFF;

		/// <summary>
		/// Carrying capacity in tenths of a stone: strength times twenty. Checked against all of the
		/// user's saves - strength 15 carries 300, 21 carries 420, the fresh save with strength 25
		/// carries 500 (2026-09-11).
		/// </summary>
		private const int WeightPerStrength = 20;

		/// <summary>How the original equips a fresh character, read from the user's fresh save;
		/// clock and talismans follow the reference (InitEmptyPlayer).</summary>
		private const int NewHunger = 0xC0;

		/// <summary>The original writes 0x40, not the reference's 0x30.</summary>
		private const int NewFatigue = 0x40;

		/// <summary>The meal heal counter of a fresh character (InitPlayer_ovr098_0 writes 0x40 to
		/// 0x3B, "food health regen bonus" in the disassembly, and 0 to 0x3C).</summary>
		private const int NewMealHealCounter = 0x40;

		/// <summary>
		/// THE PLAYER OBJECT, 27 bytes from 0xD4 - the same record as a mobile object of the
		/// level, and in LEV.ARK it appears a second time as object 1 of the level the
		/// character is on (the reference: PlayerObjectStoragePTR, there 0xD5).
		///
		/// Cross-checked against four saves of the original: both copies match except for
		/// byte 0 (7F here, 3F in the level) and the link in bytes 6/7 (1 here with a
		/// non-empty inventory, 0 in the level). Established:
		///
		///   0/1    7F 20 - object 127, bit 13 set
		///   2/3    zpos, heading (8 steps), fine offset Y and X (each 0 to 7)
		///   6/7    link: 1 if the inventory has records
		///   8      hit points
		///   15     2C for a fresh character
		///   16/17  tile: X in bits 10-15, Y in bits 4-9
		///   1A     FD
		///
		/// We had zero there, and in the level the data garbage from DATA\LEV.ARK (readable
		/// text fragments) - the original stayed black on loading (per user, 2026-09-11).
		/// </summary>
		public const int PlayerObjectOffset = 0xD4;

		public const int PlayerObjectSize = 27;

		private const int NewClockValue = 0x10B3000;

		private const int NewMoonstoneLevel = 2;

		/// <summary>Quest flag 36 counts the talismans still to be destroyed - eight.</summary>
		public const int TalismanQuestFlag = 36;

		/// <summary>Quest flag 37 holds the dream bits - see UWSleepRules.</summary>
		public const int DreamQuestFlag = 37;

		private const int TalismanCount = 8;

		/// <summary>Game variable 26 holds the attempts at the bullfrog puzzle.</summary>
		private const int BullfrogVariable = 26;

		private const int BullfrogRetries = 53;

		/// <summary>
		/// Builds the unencrypted PLAYER.DAT of a newly created character.
		///
		/// NOT from the template in the data folder: from 0x34 it holds numbers that match
		/// nothing (vitality 35 of 0, mana 188, level 0, experience in the millions), and
		/// after the inventory counter there is nonsense. The original apparently builds the file
		/// itself, and so does the reference (playerdat.InitEmptyPlayer) - so here too:
		/// all zero, then name, attributes, skills, state and the fixed starting values,
		/// plus an empty inventory (counter one, no records).
		///
		/// The level is 1, the location tile 32/2 - that is where the original places the character too
		/// (the reference: uimanager_mainmenu.JourneyOnwards). The height is
		/// NewCharacter.StartZ, in eighths of a zpos step as in the original's fresh save.
		/// </summary>
		public static byte[] BuildNewCharacter(NewCharacter pOCharacter)
		{
			byte[] lyPlain = new byte[InventoryRecordOffset];

			string lsName = pOCharacter.Name ?? string.Empty;

			for (int liAt = 0; liAt < NameLength && liAt < lsName.Length; liAt++)
			{
				char lcChar = lsName[liAt];

				lyPlain[NameOffset + liAt] = (byte)(lcChar >= 0x20 && lcChar <= 0x7E ? lcChar : '_');
			}

			int[] liFlags = new int[QuestFlagCount];
			liFlags[TalismanQuestFlag] = TalismanCount;

			int[] liVariables = new int[GameVariableCount];
			liVariables[BullfrogVariable] = BullfrogRetries;

			SavedState lOState = new SavedState
			{
				Hunger = NewHunger,
				Fatigue = NewFatigue,
				ClockValue = NewClockValue,
				MoonstoneLevel = NewMoonstoneLevel,
				CurrentVitality = pOCharacter.MaxVitality,
				MaxVitality = pOCharacter.MaxVitality,
				CurrentMana = pOCharacter.MaxMana,
				MaxMana = pOCharacter.MaxMana,
				ActiveSpells = new List<ActiveSpell>(),
				QuestFlags = liFlags,
				GameVariables = liVariables,
				SelectedRunes = new int[0],

				HasCharacter = true,
				Strength = pOCharacter.Strength,
				Dexterity = pOCharacter.Dexterity,
				Intelligence = pOCharacter.Intelligence,
				Attack = pOCharacter.Attack,
				Defense = pOCharacter.Defense,
				Skills = pOCharacter.Skills ?? new int[SkillCount],
				Level = 1,
				Experience = 0,
				AvailableSkillPoints = 1,
				TotalSkillPoints = 0,
				Runes = new bool[RuneCount]
			};

			fWriteCharacter(lyPlain, lOState);
			fWriteState(lyPlain, lOState);

			lyPlain[MealHealCounterOffset] = NewMealHealCounter;
			lyPlain[Counter3COffset] = 0;

			fWrite16(lyPlain, 0x4A, 0);
			fWrite16(lyPlain, 0x4C, pOCharacter.Strength * WeightPerStrength);

			// Class in the upper three bits, body shape in 2 to 4, gender in bit 1,
			// bit 0 set means right-handed (the reference: playerdat.isLefty).
			lyPlain[0x64] = (byte)(((pOCharacter.CharacterClass & 0x7) << 5)
				| ((pOCharacter.Body & 0x7) << 2)
				| (pOCharacter.IsFemale ? 0x2 : 0)
				| (pOCharacter.IsLeftHanded ? 0 : 0x1));

			lyPlain[DifficultyOffset] = (byte)(pOCharacter.Difficulty & 0xFF);
			lyPlain[SettingsOffset] = NewSettings;
			lyPlain[SettingsOffset + 1] = NewSettings2;
			lyPlain[NewSettings3Offset] = NewSettings3;

			int liPositionX = ((pOCharacter.StartTileX & 0xFF) << 8) | 0x80;
			int liPositionY = ((pOCharacter.StartTileY & 0xFF) << 8) | 0x80;

			fWrite16(lyPlain, 0x54, liPositionX);
			fWrite16(lyPlain, 0x56, liPositionY);
			fWrite16(lyPlain, 0x58, pOCharacter.StartZ & 0xFFFF);
			fWrite16(lyPlain, 0x5A, 0);
			lyPlain[0x5C] = 1;

			// An empty inventory: the number of records plus one.
			fWrite16(lyPlain, InventoryCountOffset, 1);

			fWritePlayerObject(lyPlain, liPositionX, liPositionY, pOCharacter.StartZ, 0, pOCharacter.MaxVitality);

			return lyPlain;
		}

		/// <summary>
		/// Writes position, hit points, tile and the inventory link into the player object
		/// (see PlayerObjectOffset). What we do not know (motion state in 9 to 14 and
		/// 18) stays as it was. An empty record first gets the original's base form.
		/// </summary>
		private static void fWritePlayerObject(byte[] pyPlain, int piPositionX, int piPositionY,
			int piZEighths, int piHeading, int piHitPoints)
		{
			if (pyPlain.Length < PlayerObjectOffset + PlayerObjectSize)
				return;

			int liAt = PlayerObjectOffset;

			if (pyPlain[liAt] == 0 && pyPlain[liAt + 1] == 0)
			{
				pyPlain[liAt] = 0x7F;
				pyPlain[liAt + 1] = 0x20;
				pyPlain[liAt + 0x15] = 0x2C;
				pyPlain[liAt + 0x1A] = 0xFD;
			}

			int liZ = System.Math.Max(0, System.Math.Min(127, piZEighths >> 3));
			int liHeading = (piHeading >> 13) & 0x7;
			int liFineX = (piPositionX & 0xFF) >> 5;
			int liFineY = (piPositionY & 0xFF) >> 5;

			fWrite16(pyPlain, liAt + 2, liZ | (liHeading << 7) | (liFineY << 10) | (liFineX << 13));

			pyPlain[liAt + 8] = (byte)System.Math.Max(0, System.Math.Min(255, piHitPoints));

			int liHome = fRead16(pyPlain, liAt + 0x16);

			fWrite16(pyPlain, liAt + 0x16, (liHome & 0xF)
				| (((piPositionY >> 8) & 0x3F) << 4)
				| (((piPositionX >> 8) & 0x3F) << 10));

			int liRecords = pyPlain.Length > InventoryRecordOffset
				? (pyPlain.Length - InventoryRecordOffset) / InventoryRecordSize : 0;

			fWrite16(pyPlain, liAt + 6, (fRead16(pyPlain, liAt + 6) & 0x3F) | ((liRecords > 0 ? 1 : 0) << 6));
		}

		/// <summary>
		/// The player object from a save game's PLAYER.DAT, as it belongs in the level as
		/// object 1: object number 3F instead of 7F, no link. Null if there is none.
		/// </summary>
		public static byte[] GetLevelPlayerSlot(string psSaveFolder)
		{
			string lsFile = Path.Combine(psSaveFolder, "PLAYER.DAT");

			if (!File.Exists(lsFile))
				return null;

			byte[] lyRaw = File.ReadAllBytes(lsFile);
			byte[] lyPlain = IsPlainTemplate(lyRaw) ? lyRaw : Decrypt(lyRaw);

			if (lyPlain == null || lyPlain.Length < PlayerObjectOffset + PlayerObjectSize || lyPlain[PlayerObjectOffset] == 0)
				return null;

			byte[] lySlot = new byte[PlayerObjectSize];
			System.Array.Copy(lyPlain, PlayerObjectOffset, lySlot, 0, PlayerObjectSize);

			lySlot[0] &= 0x3F;
			lySlot[6] &= 0x3F;
			lySlot[7] = 0;

			return lySlot;
		}

		/// <summary>
		/// Writes LOCATION AND LEVEL back into a save game's PLAYER.DAT. With pOState the
		/// state, the character and (if Equipment and Backpack are set) the inventory are
		/// written too; without it everything else - name, attributes, inventory - stays
		/// byte for byte as it was.
		///
		/// Meant for comparing with the original: fly to a spot in noclip mode,
		/// write it in here, load the save game in the original and look there.
		/// Some corners are otherwise hard to reach (per user, 2026-09-06 - the arrow trap
		/// on level 5).
		///
		/// The file is decrypted, changed and encrypted again with THE SAME
		/// key byte. Before the first change a backup is created
		/// as PLAYER.DAT.bak.
		///
		/// THE HEIGHT IS STORED IN EIGHTHS OF A ZPOS STEP. The field at 0x58 is a whole word
		/// and holds zpos * 8, not zpos. Measured on three save games, each against
		/// the floor height of the tile from the LEV.ARK of the same save:
		///
		///   SAVE1  level 1  tile 55/13  field 640  -> 80   tile floor 10 -> zpos 80
		///   SAVE2  level 7  tile 38/31  field 768  -> 96   tile floor 12 -> zpos 96
		///
		/// Previously zpos was written here without the shift, and truncated to one byte.
		/// In SAVE3 the error could be read afterwards: it held 72, i.e. zpos 9,
		/// where the tile requires zpos 72 - 126 world units too deep. That is exactly how the
		/// user described it: the character is stuck below the floor on loading (2026-09-07).
		/// The reference says the same (playerdat.Z with "zpos => Z >> 3").
		///
		/// Hence the FIELD UNIT itself is passed, i.e. eighths of a zpos step. In
		/// both untouched save games the value is divisible by eight, but there
		/// the character was also standing on level ground. On a slope the floor lies
		/// in between, and the three extra bits are exactly for that - the
		/// reference's motion code also keeps the player's height in this
		/// finer unit (motion.playerMotionParams.z_4).
		///
		/// THE HEADING IS A FULL CIRCLE IN 16 BITS, at 0x5A. The suspicion that it was
		/// unresolved was unfounded: the reference keeps it as heading_full, and the
		/// object heading is the same value, only coarser (motion_init: heading << 0xD, i.e.
		/// eight directions of 8192 each). Zero is north, counted clockwise -
		/// the same counting as for every object in the world. Cross-check in SAVE3: it
		/// holds 33143, that is 182 degrees, and the upper byte divided by 32 gives
		/// object heading 4 - south.
		/// </summary>
		/// <returns>An error message, or null on success.</returns>
		/// <param name="pOWorldObjects">Object list of the level the character is on - for
		/// items that were picked up from the world and whose attachments are
		/// listed there. See fResolveChildren.</param>
		/// <param name="pOLoadedRecords">The inventory records of the save game that was loaded -
		/// items still carried from it keep their links into that list. See fResolveChildren.</param>
		public static string WritePosition(string psSaveFolder, int piLevel, int piPositionX,
			int piPositionY, int piZPosition, int piHeading, SavedState pOState = null,
			List<UWObject> pOWorldObjects = null, List<UWObject> pOLoadedRecords = null)
		{
			string lsFile = Path.Combine(psSaveFolder, "PLAYER.DAT");

			if (!File.Exists(lsFile))
				return "No PLAYER.DAT in " + psSaveFolder;

			byte[] lyRaw = File.ReadAllBytes(lsFile);

			if (IsPlainTemplate(lyRaw))
				return "This is the unencrypted template, not a save game.";

			byte lyKey = lyRaw[0];
			byte[] lyPlain = Decrypt(lyRaw);

			if (lyPlain == null || lyPlain.Length <= 0x5C)
				return "PLAYER.DAT is too short.";

			string lsBackup = lsFile + ".bak";

			if (!File.Exists(lsBackup))
				File.Copy(lsFile, lsBackup);

			fWrite16(lyPlain, 0x54, piPositionX);
			fWrite16(lyPlain, 0x56, piPositionY);
			fWrite16(lyPlain, 0x58, piZPosition & 0xFFFF);
			fWrite16(lyPlain, 0x5A, piHeading & 0xFFFF);

			lyPlain[0x5C] = (byte)piLevel;

			fWriteState(lyPlain, pOState);
			fWriteCharacter(lyPlain, pOState);

			// The inventory can make the file longer or shorter - so afterwards
			// a new block is due.
			if (pOState != null && pOState.Equipment != null && pOState.Backpack != null)
			{
				string lsError = fWriteInventory(ref lyPlain, pOState, pOWorldObjects, pOLoadedRecords);

				if (lsError != null)
					return lsError;
			}

			// After the inventory - the link in the player object depends on the number of records.
			fWritePlayerObject(lyPlain, piPositionX, piPositionY, piZPosition, piHeading,
				pOState != null ? pOState.CurrentVitality : lyPlain[PlayerObjectOffset + 8]);

			File.WriteAllBytes(lsFile, Encrypt(lyPlain, lyKey));

			return null;
		}

		/// <summary>
		/// Attributes, skills, experience and rune bag.
		///
		/// All of this was only read until 2026-09-10. Skills raised at the shrine,
		/// a new level or collected runes would have been lost on saving.
		/// </summary>
		private static void fWriteCharacter(byte[] pyPlain, SavedState pOState)
		{
			if (pOState == null || !pOState.HasCharacter || pyPlain.Length <= 0x53)
				return;

			pyPlain[StrengthOffset] = (byte)(pOState.Strength & 0xFF);
			pyPlain[StrengthOffset + 1] = (byte)(pOState.Dexterity & 0xFF);
			pyPlain[StrengthOffset + 2] = (byte)(pOState.Intelligence & 0xFF);
			pyPlain[StrengthOffset + 3] = (byte)(pOState.Attack & 0xFF);
			pyPlain[StrengthOffset + 4] = (byte)(pOState.Defense & 0xFF);

			for (int liAt = 0; pOState.Skills != null && liAt < pOState.Skills.Count && liAt < SkillCount; liAt++)
				pyPlain[SkillsOffset + liAt] = (byte)(pOState.Skills[liAt] & 0xFF);

			pyPlain[LevelOffset] = (byte)(pOState.Level & 0xFF);
			fWrite32(pyPlain, 0x4E, pOState.Experience);
			pyPlain[0x52] = (byte)(pOState.AvailableSkillPoints & 0xFF);
			pyPlain[0x53] = (byte)(pOState.TotalSkillPoints & 0xFF);

			if (pOState.Runes == null)
				return;

			// Three bytes, read from the top down - An is bit 7 of the first.
			for (int liByte = 0; liByte < 3; liByte++)
				pyPlain[RuneFlagsOffset + liByte] = 0;

			for (int liRune = 0; liRune < RuneCount && liRune < pOState.Runes.Count; liRune++)
			{
				if (pOState.Runes[liRune])
					pyPlain[RuneFlagsOffset + (liRune / 8)] |= (byte)(1 << (7 - (liRune % 8)));
			}
		}

		/// <summary>
		/// Rewrites the inventory: slot table and records.
		///
		/// THE LAYOUT IS THE SAME AS WHEN READING (fParseInventory): from 0xF7 nineteen links
		/// with the record index in bits 6 to 15, from 0x137 the records of eight bytes each. Both lie
		/// after the 210 encrypted bytes and are in plain text.
		///
		/// THE RECORDS ARE RENUMBERED - first the thing in the slot, then its contents,
		/// depth first. The numbers from the loaded save are no longer usable: during play
		/// things are added and disappear, and an item picked up from the world
		/// has a number from the LEVEL list, not from this one.
		///
		/// The objects THEMSELVES ARE NOT CHANGED in the process. The game continues after
		/// saving, and their link fields then still belong to the list they
		/// came from. The new numbers exist only in the file.
		///
		/// WHAT HANGS OFF A THING follows the special link in the quantity field, provided it carries no
		/// quantity: for a container its contents, for a wand its
		/// spell object. It is resolved in order via already opened contents,
		/// the list of the loaded save game and finally the level's object list.
		/// </summary>
		private static string fWriteInventory(ref byte[] pyPlain, SavedState pOState,
			List<UWObject> pOWorldObjects, List<UWObject> pOLoadedRecords)
		{
			if (pyPlain.Length < InventoryRecordOffset)
				return "PLAYER.DAT is too short for an inventory.";

			List<UWObject> lOOrder = new List<UWObject>();
			Dictionary<UWObject, int> lOIndex = new Dictionary<UWObject, int>();
			Dictionary<UWObject, List<UWObject>> lOChildren = new Dictionary<UWObject, List<UWObject>>();

			UWObject[] lOTop = new UWObject[EquipmentSlotCount + BackpackSlotCount];

			for (int liSlot = 0; liSlot < EquipmentSlotCount; liSlot++)
				lOTop[liSlot] = liSlot < pOState.Equipment.Length ? pOState.Equipment[liSlot] : null;

			for (int liSlot = 0; liSlot < BackpackSlotCount; liSlot++)
				lOTop[EquipmentSlotCount + liSlot] = liSlot < pOState.Backpack.Length ? pOState.Backpack[liSlot] : null;

			// THE ORIGINAL'S ORDER: first the eight backpack slots, then the eleven
			// equipment slots, each depth first with its contents. The reference calls
			// this "DOS-canonical order" (PlayerDatWriter.SerializeUw1Canonical).
			List<UWObject> lOTopEmitted = new List<UWObject>();

			for (int liSlot = EquipmentSlotCount; liSlot < lOTop.Length; liSlot++)
				fCollectTop(lOTop[liSlot], lOTopEmitted, lOOrder, lOIndex, lOChildren, pOWorldObjects, pOLoadedRecords);

			for (int liSlot = 0; liSlot < EquipmentSlotCount; liSlot++)
				fCollectTop(lOTop[liSlot], lOTopEmitted, lOOrder, lOIndex, lOChildren, pOWorldObjects, pOLoadedRecords);

			// Ten bits for the index - the format holds no more.
			if (lOOrder.Count > 1023)
				return "Too many items for the format (" + lOOrder.Count + ").";

			byte[] lyNew = new byte[InventoryRecordOffset + (lOOrder.Count * InventoryRecordSize)];
			System.Array.Copy(pyPlain, lyNew, InventoryRecordOffset);

			for (int liSlot = 0; liSlot < lOTop.Length; liSlot++)
			{
				int liRecord = lOTop[liSlot] != null && lOIndex.ContainsKey(lOTop[liSlot])
					? lOIndex[lOTop[liSlot]] : 0;

				fWrite16(lyNew, SlotTableOffset + (liSlot * 2), liRecord << 6);
			}

			// Who follows whom: siblings in a chain point to each other via link.
			Dictionary<UWObject, int> lONext = new Dictionary<UWObject, int>();

			foreach (KeyValuePair<UWObject, List<UWObject>> lOPair in lOChildren)
			{
				for (int liAt = 0; liAt < lOPair.Value.Count; liAt++)
					lONext[lOPair.Value[liAt]] = liAt + 1 < lOPair.Value.Count ? lOIndex[lOPair.Value[liAt + 1]] : 0;
			}

			// THE TOP LEVEL IS ITSELF A CHAIN. That was the bug in the first
			// attempt (per user, 2026-09-10): we set zero everywhere there, and the
			// original walked the chain from record one and stopped right away. All that remained
			// was what sat in record one - the crown in the helmet slot. The slot table alone
			// is not enough for the original; our own loader only reads that, which is why everything
			// looked right for us. Checked on Henrietta's SAVE3 written by the original:
			// there every top-level item points to the next one.
			for (int liAt = 0; liAt < lOTopEmitted.Count; liAt++)
				lONext[lOTopEmitted[liAt]] = liAt + 1 < lOTopEmitted.Count ? lOIndex[lOTopEmitted[liAt + 1]] : 0;

			HashSet<UWObject> lOTopSet = new HashSet<UWObject>(lOTopEmitted);

			// THE NUMBER OF RECORDS PLUS ONE is at 0xD2, sixteen bits, in plain text.
			// The original reads from it how many records to load. Checked on all four of the
			// user's save games (15, 29, 63 and 67 records - each stores
			// one more).
			fWrite16(lyNew, InventoryCountOffset, lOOrder.Count + 1);

			for (int liAt = 0; liAt < lOOrder.Count; liAt++)
			{
				UWObject lOObject = lOOrder[liAt];
				int liOffset = InventoryRecordOffset + (liAt * InventoryRecordSize);

				int liWrittenId = lOObject.ID & 0x1FF;

				// AN OPEN SACK AT THE TOP OF THE INVENTORY IS WRITTEN CLOSED. A container of
				// classes 128 to 139 knows its open state via bit 0 of the
				// object number; according to the reference the original considers an open sack at
				// top level invalid and shows the inventory empty. Only at the top - further
				// inside, bit 0 is part of the object number itself (143 is the rune bag).
				if (lOTopSet.Contains(lOObject) && (liWrittenId >> 6) == 2
					&& ((liWrittenId >> 4) & 0x3) == 0 && (liWrittenId & 0xF) <= 0xB
					&& (liWrittenId & 0x1) != 0)
					liWrittenId &= ~0x1;

				int liWord0 = liWrittenId
					| ((lOObject.Flags & 0x7) << 9)
					| ((lOObject.IsEnchanted ? 1 : 0) << 12)
					| ((lOObject.DoorDirection ? 1 : 0) << 13)
					| ((lOObject.IsHidden ? 1 : 0) << 14)
					| ((lOObject.HasQuantity ? 1 : 0) << 15);

				int liWord1 = (lOObject.ZPos & 0x7F)
					| ((lOObject.Heading & 0x7) << 7)
					| ((lOObject.YPos & 0x7) << 10)
					| ((lOObject.XPos & 0x7) << 13);

				int liNext = lONext.ContainsKey(lOObject) ? lONext[lOObject] : 0;

				int liWord2 = (lOObject.Quality & 0x3F) | (liNext << 6);

				// Quantity field: a real quantity stays, a special link now points to the
				// new number of the first child - or to nothing if none was found.
				int liSpecial = lOObject.Quantity;

				if (!lOObject.HasQuantity)
				{
					List<UWObject> lOKids;

					liSpecial = lOChildren.TryGetValue(lOObject, out lOKids) && lOKids.Count > 0
						? lOIndex[lOKids[0]] : 0;
				}

				int liWord3 = (lOObject.Owner & 0x3F) | ((liSpecial & 0x3FF) << 6);

				fWrite16(lyNew, liOffset, liWord0);
				fWrite16(lyNew, liOffset + 2, liWord1);
				fWrite16(lyNew, liOffset + 4, liWord2);
				fWrite16(lyNew, liOffset + 6, liWord3);
			}

			pyPlain = lyNew;

			return null;
		}

		/// <summary>A top-level slot: records it for the top-level chain
		/// and adds it together with its contents.</summary>
		private static void fCollectTop(UWObject pOObject, List<UWObject> pOTop, List<UWObject> pOOrder,
			Dictionary<UWObject, int> pOIndex, Dictionary<UWObject, List<UWObject>> pOChildren,
			List<UWObject> pOWorldObjects, List<UWObject> pOLoadedRecords)
		{
			if (pOObject == null || pOIndex.ContainsKey(pOObject))
				return;

			pOTop.Add(pOObject);
			fCollectInventory(pOObject, pOOrder, pOIndex, pOChildren, pOWorldObjects, pOLoadedRecords);
		}

		/// <summary>Adds a thing and everything hanging off it to the order.
		/// Numbers start at one; the format has no record zero.</summary>
		private static void fCollectInventory(UWObject pOObject, List<UWObject> pOOrder,
			Dictionary<UWObject, int> pOIndex, Dictionary<UWObject, List<UWObject>> pOChildren,
			List<UWObject> pOWorldObjects, List<UWObject> pOLoadedRecords)
		{
			if (pOObject == null || pOIndex.ContainsKey(pOObject))
				return;

			pOOrder.Add(pOObject);
			pOIndex[pOObject] = pOOrder.Count;

			List<UWObject> lOKids = fResolveChildren(pOObject, pOWorldObjects, pOLoadedRecords);

			if (lOKids.Count == 0)
				return;

			pOChildren[pOObject] = lOKids;

			foreach (UWObject lOKid in lOKids)
				fCollectInventory(lOKid, pOOrder, pOIndex, pOChildren, pOWorldObjects, pOLoadedRecords);
		}

		/// <summary>The chain behind a thing's special link, as a list.</summary>
		private static List<UWObject> fResolveChildren(UWObject pOObject, List<UWObject> pOWorldObjects, List<UWObject> pOLoadedRecords)
		{
			List<UWObject> lOResult = new List<UWObject>();

			if (pOObject.HasQuantity)
				return lOResult;

			if (pOObject.Contents != null)
			{
				foreach (UWObject lOItem in pOObject.Contents)
				{
					if (lOItem != null)
						lOResult.Add(lOItem);
				}

				return lOResult;
			}

			if (pOObject.Quantity == 0)
				return lOResult;

			// If it comes from the loaded inventory, that list applies; otherwise the level's.
			List<UWObject> lOList = pOLoadedRecords != null && pOLoadedRecords.Contains(pOObject)
				? pOLoadedRecords
				: pOWorldObjects;

			if (lOList == null || pOObject.Quantity >= lOList.Count)
				return lOResult;

			UWObject lOCurrent = lOList[pOObject.Quantity];

			for (int liGuard = 0; lOCurrent != null && liGuard < 1024; liGuard++)
			{
				lOResult.Add(lOCurrent);
				lOCurrent = lOCurrent.Link == 0 || lOCurrent.Link >= lOList.Count ? null : lOList[lOCurrent.Link];
			}

			return lOResult;
		}

		/// <summary>
		/// What else belongs in the save game besides location and heading. All of it was
		/// only READ until 2026-09-08: saving with us wrote a save in which the
		/// character stood somewhere else but still had the hunger, fatigue, time of day
		/// and active spells from loading.
		/// </summary>
		public class SavedState
		{
			public int Hunger;

			public int Fatigue;

			public int ClockValue;

			/// <summary>The level the moonstone lies on (see MoonstoneLevel), 0 for
			/// nowhere. Below zero leaves the byte as it was.</summary>
			public int MoonstoneLevel = -1;

			/// <summary>Upper nibble of 0x5E, see SilverTreeLevel. Below zero leaves it as it
			/// was.</summary>
			public int SilverTreeLevel = -1;

			/// <summary>How many incense visions have been seen (0x61, bits 0 and 1).</summary>
			public int? IncenseCounter;

			/// <summary>The two endgame bits in 0x62 (see TalismansDestroyable,
			/// GaramonBuried); null leaves them as they were.</summary>
			public bool? TalismansDestroyable;

			public bool? GaramonBuried;

			/// <summary>0x60 bit 7, see CupOfWonderFound; null leaves it as it was.</summary>
			public bool? CupOfWonderFound;

			/// <summary>0x60 bit 6, see KeyOfTruthGiven; null leaves it as it was.</summary>
			public bool? KeyOfTruthGiven;

			/// <summary>0x60 bit 5, see OrbDestroyed; null leaves it as it was.</summary>
			public bool? OrbDestroyed;

			/// <summary>0xB0, see OrbManaBackup; null leaves it as it was.</summary>
			public int? OrbManaBackup;

			public int CurrentVitality;

			public int MaxVitality;

			public int CurrentMana;

			public int MaxMana;

			/// <summary>How much poison is still active, four bits.</summary>
			public int Poison;

			/// <summary>How drunk the character is, six bits.</summary>
			public int Intoxication;

			/// <summary>Byte 0x3B (see MealHealCounter); below zero leaves it as it was.</summary>
			public int MealHealCounter = -1;

			/// <summary>Byte 0x3C (see Counter3C); below zero leaves it as it was.</summary>
			public int Counter3C = -1;

			/// <summary>PLAYER.DAT 0x4A, the carried load in tenths of a stone as the original's
			/// running total (see UWInventoryModel.WeightOffsetTenthStones); below zero leaves it as
			/// it was.</summary>
			public int CarriedWeight = -1;

			/// <summary>How long the mushroom effect still lasts, two bits.</summary>
			public int Hallucination;

			/// <summary>The byte at SwimCounterOffset; below zero leaves it as it was.</summary>
			public int SwimCounter = -1;

			/// <summary>Sound effects and music, each 1 on, 0 off, below zero leaves the two
			/// bit pairs of SettingsOffset as they were. The original saves them per save game
			/// and switches to them on loading - see SettingsOffset.</summary>
			public int SoundEnabled = -1;

			public int MusicEnabled = -1;

			/// <summary>At most three; anything beyond that is not written.</summary>
			public IReadOnlyList<ActiveSpell> ActiveSpells;

			/// <summary>All quest flags, numbered from zero. Shorter than
			/// QuestFlagCount is allowed; the rest then stays as it is.</summary>
			public IReadOnlyList<int> QuestFlags;

			/// <summary>All game variables, numbered from zero.</summary>
			public IReadOnlyList<int> GameVariables;

			/// <summary>The runes on the shelf, at most three. Shorter means the
			/// remaining slots are cleared.</summary>
			public IReadOnlyList<int> SelectedRunes;

			// ---- since 2026-09-10, for saving from the options panel

			/// <summary>False means: this group is not written and the old contents
			/// stay. That way SavedState stays usable for a caller that only wants to write
			/// the location.</summary>
			public bool HasCharacter;

			public int Strength;

			public int Dexterity;

			public int Intelligence;

			public int Attack;

			public int Defense;

			/// <summary>The eighteen named skills, in save game
			/// order.</summary>
			public IReadOnlyList<int> Skills;

			public int Level;

			public int Experience;

			public int AvailableSkillPoints;

			public int TotalSkillPoints;

			/// <summary>Which of the 24 runes are in the bag.</summary>
			public IReadOnlyList<bool> Runes;

			/// <summary>Null means: the inventory stays as it is in the file.</summary>
			public UWObject[] Equipment;

			public UWObject[] Backpack;
		}

		/// <summary>
		/// Writes hunger, fatigue, vitality, mana, clock, moonstone and silver tree level,
		/// quest flags, game variables, selected runes, poison, intoxication, hallucination,
		/// the endgame bits and the active spells.
		///
		/// THE SPELL SLOTS ARE OVERWRITTEN COMPLETELY, including the empty ones. The original
		/// does leave a finished spell in place and only counts the count down (see
		/// fParseActiveSpells), but a leftover nobody reads any more helps nobody - and
		/// a cleanly zeroed slot is harmless with respect to the original, because the count
		/// decides anyway.
		///
		/// THE COUNT is in bits 6 to 9 of a word that also carries other flags
		/// (dream stage, plot markers). It is therefore masked in, not written
		/// over - the same mask as in the reference (0xFC3F).
		/// </summary>
		private static void fWriteState(byte[] pyPlain, SavedState pOState)
		{
			// The last field is the clock at 0xCE to 0xD1.
			if (pOState == null || pyPlain.Length <= 0xD1)
				return;

			pyPlain[0x39] = (byte)(pOState.Hunger & 0xFF);
			pyPlain[0x3A] = (byte)(pOState.Fatigue & 0xFF);

			if (pOState.MealHealCounter >= 0)
				pyPlain[MealHealCounterOffset] = (byte)(pOState.MealHealCounter & 0xFF);

			if (pOState.Counter3C >= 0)
				pyPlain[Counter3COffset] = (byte)(pOState.Counter3C & 0xFF);

			if (pOState.CarriedWeight >= 0)
				fWrite16(pyPlain, CarriedWeightOffset, pOState.CarriedWeight);

			pyPlain[CurrentVitalityOffset] = (byte)(pOState.CurrentVitality & 0xFF);
			pyPlain[MaxVitalityOffset] = (byte)(pOState.MaxVitality & 0xFF);
			pyPlain[CurrentManaOffset] = (byte)(pOState.CurrentMana & 0xFF);
			pyPlain[MaxManaOffset] = (byte)(pOState.MaxMana & 0xFF);

			fWrite32(pyPlain, 0xCE, pOState.ClockValue);

			if (pOState.SwimCounter >= 0)
				pyPlain[SwimCounterOffset] = (byte)(pOState.SwimCounter & 0xFF);

			if (pOState.SoundEnabled >= 0)
				pyPlain[SettingsOffset] = (byte)((pyPlain[SettingsOffset] & ~(SoundSettingMask << SoundEnabledShift))
					| ((pOState.SoundEnabled != 0 ? 1 : 0) << SoundEnabledShift));

			if (pOState.MusicEnabled >= 0)
				pyPlain[SettingsOffset] = (byte)((pyPlain[SettingsOffset] & ~(SoundSettingMask << MusicEnabledShift))
					| ((pOState.MusicEnabled != 0 ? 1 : 0) << MusicEnabledShift));

			// Lower nibble of 0x5E; the upper one is the silver tree and stays.
			if (pOState.MoonstoneLevel >= 0)
				pyPlain[0x5E] = (byte)((pyPlain[0x5E] & 0xF0) | (pOState.MoonstoneLevel & 0xF));

			fWriteQuestFlags(pyPlain, pOState.QuestFlags);
			fWriteGameVariables(pyPlain, pOState.GameVariables);

			// The three runes on the shelf. An empty slot holds 24, not zero - zero
			// would be the rune An.
			for (int liAt = 0; liAt < miSelectedRuneSlots; liAt++)
			{
				pyPlain[SelectedRunesOffset + liAt] =
					pOState.SelectedRunes != null && liAt < pOState.SelectedRunes.Count
						? (byte)(pOState.SelectedRunes[liAt] & 0xFF)
						: (byte)SelectedRuneNone;
			}

			// THE POISON SITS IN THE SAME BYTE as the spell count: bits 0-1 dream stage,
			// bits 2-5 poison, from bit 6 the count (the reference: playerdatstatus.play_poison with
			// its mask 0xC3 on byte 0x60, i.e. our 0x5F). So first mask in the poison
			// and then the count - each touches only its own bits.
			pyPlain[PoisonOffset] = (byte)((pyPlain[PoisonOffset] & 0xC3)
				| ((pOState.Poison & 0xF) << 2));

			// The intoxication spans a byte boundary - bits 4 to 9 of a word in which
			// the mushroom effect sits below and a plot marker above. So it is also
			// masked in, not written over.
			fWrite16(pyPlain, IntoxicationOffset,
				(fRead16(pyPlain, IntoxicationOffset) & 0xFC0F) | ((pOState.Intoxication & 0x3F) << 4));

			// The endgame bits are in the byte above, bits 2 and 3 - the intoxication
			// only occupies bits 0 and 1 there.
			if (pOState.IncenseCounter.HasValue)
				pyPlain[0x61] = (byte)((pyPlain[0x61] & 0xFC) | (pOState.IncenseCounter.Value & 3));

			if (pOState.TalismansDestroyable.HasValue)
				pyPlain[0x62] = (byte)((pyPlain[0x62] & 0xFB) | (pOState.TalismansDestroyable.Value ? 4 : 0));

			if (pOState.GaramonBuried.HasValue)
				pyPlain[0x62] = (byte)((pyPlain[0x62] & 0xF7) | (pOState.GaramonBuried.Value ? 8 : 0));

			if (pOState.SilverTreeLevel >= 0)
				pyPlain[0x5E] = (byte)((pyPlain[0x5E] & 0x0F) | ((pOState.SilverTreeLevel & 0xF) << 4));

			if (pOState.CupOfWonderFound.HasValue)
				pyPlain[0x60] = (byte)((pyPlain[0x60] & 0x7F) | (pOState.CupOfWonderFound.Value ? 0x80 : 0));

			if (pOState.KeyOfTruthGiven.HasValue)
				pyPlain[0x60] = (byte)((pyPlain[0x60] & 0xBF) | (pOState.KeyOfTruthGiven.Value ? 0x40 : 0));

			if (pOState.OrbDestroyed.HasValue)
				pyPlain[0x60] = (byte)((pyPlain[0x60] & 0xDF) | (pOState.OrbDestroyed.Value ? 0x20 : 0));

			if (pOState.OrbManaBackup.HasValue)
				pyPlain[0xB0] = (byte)(pOState.OrbManaBackup.Value & 0xFF);

			// The mushroom effect sits directly below, bits 2 and 3 of the same byte.
			pyPlain[IntoxicationOffset] = (byte)((pyPlain[IntoxicationOffset] & 0xF3)
				| ((pOState.Hallucination & 0x3) << 2));

			int liCount = 0;

			for (int liAt = 0; liAt < MaxActiveSpells; liAt++)
			{
				bool lbHasSpell = pOState.ActiveSpells != null && liAt < pOState.ActiveSpells.Count;

				if (!lbHasSpell)
				{
					fWrite16(pyPlain, ActiveSpellOffset + (liAt * 2), 0);

					continue;
				}

				ActiveSpell lOSpell = pOState.ActiveSpells[liAt];

				// Swapped nibbles - see fParseActiveSpells.
				int liId = ((lOSpell.MinorClass & 0xF) << 4) | (lOSpell.MajorClass & 0xF);

				fWrite16(pyPlain, ActiveSpellOffset + (liAt * 2),
					liId | ((lOSpell.Stability & 0xFF) << 8));

				liCount++;
			}

			int liFlags = fRead16(pyPlain, ActiveSpellCountOffset);

			fWrite16(pyPlain, ActiveSpellCountOffset, (liFlags & 0xFC3F) | ((liCount & 0xF) << 6));
		}

		/// <summary>Byte holding the poison - see fWriteState. The same as the lower
		/// byte of ActiveSpellCountOffset.</summary>
		private const int PoisonOffset = 0x5F;

		/// <summary>Word holding the intoxication - in bits 4 to 9.</summary>
		private const int IntoxicationOffset = 0x61;

		private const int MealHealCounterOffset = 0x3B;

		/// <summary>The carried load, a running total in the original.</summary>
		private const int CarriedWeightOffset = 0x4A;

		private const int Counter3COffset = 0x3C;

		/// <summary>
		/// Writes the quest flags - the first 32 as single bits, the rest as whole
		/// bytes after them. Counterpart to reading in fReadStoredState.
		/// </summary>
		private static void fWriteQuestFlags(byte[] pyPlain, IReadOnlyList<int> pOFlags)
		{
			if (pOFlags == null)
				return;

			for (int liAt = 0; liAt < pOFlags.Count && liAt < QuestFlagCount; liAt++)
			{
				if (liAt < QuestFlagBitCount)
				{
					int liByte = QuestFlagOffset + (liAt / 8);
					int liBit = 1 << (liAt % 8);

					if (pOFlags[liAt] != 0)
						pyPlain[liByte] |= (byte)liBit;
					else
						pyPlain[liByte] &= (byte)~liBit;

					continue;
				}

				pyPlain[QuestFlagOffset + (QuestFlagBitCount / 8) + (liAt - QuestFlagBitCount)]
					= (byte)(pOFlags[liAt] & 0xFF);
			}
		}

		/// <summary>Writes the game variables, one byte each. They are six bits wide
		/// (see UWGameVariables), but the whole byte is written - that is also how it is
		/// in the save game.</summary>
		private static void fWriteGameVariables(byte[] pyPlain, IReadOnlyList<int> pOVariables)
		{
			if (pOVariables == null)
				return;

			for (int liAt = 0; liAt < pOVariables.Count && liAt < GameVariableCount; liAt++)
				pyPlain[GameVariableOffset + liAt] = (byte)(pOVariables[liAt] & 0xFF);
		}

		private static void fWrite16(byte[] pyData, int piOffset, int piValue)
		{
			pyData[piOffset] = (byte)(piValue & 0xFF);
			pyData[piOffset + 1] = (byte)((piValue >> 8) & 0xFF);
		}

		private static void fWrite32(byte[] pyData, int piOffset, int piValue)
		{
			fWrite16(pyData, piOffset, piValue & 0xFFFF);
			fWrite16(pyData, piOffset + 2, (piValue >> 16) & 0xFFFF);
		}

		/// <summary>If the file carries its name in plain text, it is the unencrypted
		/// template.</summary>
		public static bool IsPlainTemplate(byte[] pyRaw)
		{
			if (pyRaw == null || pyRaw.Length < 2)
				return true;

			return pyRaw[0] >= 'A' && pyRaw[0] <= 'Z'
				&& (pyRaw[1] == 0 || (pyRaw[1] >= 'A' && pyRaw[1] <= 'Z'));
		}

		/// <summary>For a player.dat from a save game: decrypts the first 210
		/// bytes. The first byte of the file is the seed and is not part of the data.
		/// The counter is reset at 80 and 160 - that is what the format
		/// description says and is not a guess.</summary>
		public static byte[] Decrypt(byte[] pyRaw)
		{
			if (pyRaw == null || pyRaw.Length < 2)
				return pyRaw;

			byte lyXor = pyRaw[0];
			byte[] lyResult = new byte[pyRaw.Length - 1];
			System.Array.Copy(pyRaw, 1, lyResult, 0, lyResult.Length);

			byte lyIncrement = 3;

			for (int liIndex = 0; liIndex < EncryptedLength && liIndex < lyResult.Length; liIndex++)
			{
				if (liIndex == 80 || liIndex == 160)
					lyIncrement = 3;

				lyResult[liIndex] ^= (byte)(lyXor + lyIncrement);
				lyIncrement += 3;
			}

			return lyResult;
		}


		/// <summary>
		/// Counterpart to Decrypt: turns a decrypted block back into a
		/// save game file, with the key byte in front.
		///
		/// The encryption is an XOR and thus its own inverse - it is
		/// the same loop as for reading.
		/// </summary>
		public static byte[] Encrypt(byte[] pyPlain, byte pyKey)
		{
			if (pyPlain == null)
				return null;

			byte[] lyResult = new byte[pyPlain.Length + 1];

			lyResult[0] = pyKey;
			System.Array.Copy(pyPlain, 0, lyResult, 1, pyPlain.Length);

			byte lyIncrement = 3;

			for (int liIndex = 0; liIndex < EncryptedLength && liIndex < pyPlain.Length; liIndex++)
			{
				if (liIndex == 80 || liIndex == 160)
					lyIncrement = 3;

				lyResult[liIndex + 1] ^= (byte)(pyKey + lyIncrement);
				lyIncrement += 3;
			}

			return lyResult;
		}

		/// <summary>The inventory records, counted from 1 - entry 0 is always null, the format
		/// has no record 0. Also serves as the object list for the contents of
		/// containers.</summary>
		public List<UWObject> InventoryRecords { get; private set; } = new List<UWObject>();

		/// <summary>The eleven equipment slots in the original's order: helmet, chest,
		/// gloves, leggings, boots, right shoulder, left shoulder, right hand,
		/// left hand, right ring, left ring. Empty slots are null.</summary>
		public UWObject[] Equipment { get; private set; } = new UWObject[EquipmentSlotCount];

		/// <summary>The eight backpack slots. Empty ones are null.</summary>
		public UWObject[] Backpack { get; private set; } = new UWObject[BackpackSlotCount];

		public const int EquipmentSlotCount = 11;

		public const int BackpackSlotCount = 8;

		private const int SlotTableOffset = 0xF7;

		private const int InventoryRecordOffset = 0x137;

		private const int InventoryRecordSize = 8;

		/// <summary>Number of inventory records plus one, sixteen bits in plain text - see
		/// fWriteInventory. The reference says 0xD3, but counts the key byte too.</summary>
		private const int InventoryCountOffset = 0xD2;

		/// <summary>
		/// Reads the inventory.
		///
		/// From 0x137 follow records of eight bytes each, with the same layout as world objects. From 0xF7
		/// there are 19 links to them: eleven equipment slots and eight backpack slots. The
		/// record index is in BITS 6 TO 15 there, not a plain number - the same
		/// format as an object's chain field.
		///
		/// uw-formats.txt does not describe the records ("how items are stored" is a
		/// TODO there). The details come from UnderworldGodot (MIT) and were recalculated
		/// against real save games.
		/// </summary>
		private void fParseInventory(byte[] pyData)
		{
			InventoryRecords = new List<UWObject> { null };

			for (int liAt = InventoryRecordOffset; liAt + InventoryRecordSize <= pyData.Length; liAt += InventoryRecordSize)
				InventoryRecords.Add(fParseRecord(pyData, liAt));

			Equipment = new UWObject[EquipmentSlotCount];
			Backpack = new UWObject[BackpackSlotCount];

			for (int liSlot = 0; liSlot < EquipmentSlotCount + BackpackSlotCount; liSlot++)
			{
				int liAt = SlotTableOffset + (liSlot * 2);

				if (liAt + 1 >= pyData.Length)
					break;

				int liIndex = fRead16(pyData, liAt) >> 6;

				if (liIndex < 1 || liIndex >= InventoryRecords.Count)
					continue;

				if (liSlot < EquipmentSlotCount)
					Equipment[liSlot] = InventoryRecords[liIndex];
				else
					Backpack[liSlot - EquipmentSlotCount] = InventoryRecords[liIndex];
			}

			// Resolve container contents right here while the correct object list is at
			// hand. Later UWInventory would resolve them against the LEVEL's object list and
			// thereby put completely unrelated things into the sack.
			for (int liRecord = 1; liRecord < InventoryRecords.Count; liRecord++)
			{
				if (InventoryRecords[liRecord] != null)
					InventoryRecords[liRecord].EnsureContentsLoaded(InventoryRecords);
			}
		}

		/// <summary>
		/// The carried objects in the order of their chain: from the player object's own link
		/// (word 3 of the copy at PlayerObjectOffset) record by record along the link field. It is
		/// the order in which RestoreGame puts them into the level (ovr118_573, see
		/// UWCarriedSlots) - not the slot order: an item added during play joins the chain
		/// where the original put it.
		/// </summary>
		public List<UWObject> GetCarriedChain()
		{
			List<UWObject> lOChain = new List<UWObject>();

			if (PlainBytes == null || PlainBytes.Length < PlayerObjectOffset + 8 || InventoryRecords == null)
				return lOChain;

			int liCount = PlainBytes.Length > InventoryCountOffset + 1
				? fRead16(PlainBytes, InventoryCountOffset) - 1
				: InventoryRecords.Count - 1;

			int liNext = fRead16(PlainBytes, PlayerObjectOffset + 6) >> 6;
			HashSet<int> lOSeen = new HashSet<int>();

			while (liNext >= 1 && liNext <= liCount && liNext < InventoryRecords.Count && lOSeen.Add(liNext))
			{
				UWObject lOObject = InventoryRecords[liNext];

				if (lOObject == null)
					break;

				lOChain.Add(lOObject);
				liNext = lOObject.Link;
			}

			return lOChain;
		}

		private static UWObject fParseRecord(byte[] pyData, int piAt)
		{
			ushort lyWord0 = (ushort)fRead16(pyData, piAt);
			ushort lyWord1 = (ushort)fRead16(pyData, piAt + 2);
			ushort lyWord2 = (ushort)fRead16(pyData, piAt + 4);
			ushort lyWord3 = (ushort)fRead16(pyData, piAt + 6);

			UWObject lOObject = new UWObject((ushort)(lyWord0 & 0x1FF));

			lOObject.Flags = (ushort)((lyWord0 & 0x1F00) >> 9);
			lOObject.IsEnchanted = ((lyWord0 & 0x1000) >> 12) != 0;
			lOObject.DoorDirection = ((lyWord0 & 0x2000) >> 13) != 0;
			lOObject.IsHidden = ((lyWord0 & 0x4000) >> 14) != 0;
			lOObject.HasQuantity = ((lyWord0 & 0x8000) >> 15) != 0;
			lOObject.ZPos = (ushort)(lyWord1 & 0x7F);
			lOObject.Heading = (ushort)((lyWord1 & 0x380) >> 7);
			lOObject.YPos = (ushort)((lyWord1 & 0x1C00) >> 10);
			lOObject.XPos = (ushort)((lyWord1 & 0xE000) >> 13);
			lOObject.Quality = (ushort)(lyWord2 & 0x3F);
			lOObject.Link = (ushort)((lyWord2 & 0xFFC0) >> 6);
			lOObject.Owner = (ushort)(lyWord3 & 0x3F);
			lOObject.Quantity = (ushort)((lyWord3 & 0xFFC0) >> 6);

			return lOObject;
		}

		/// <summary>Attaches their images to the inventory items. Separate from reading because
		/// UWPlayerData does not know the textures - DataImport supplies them afterwards.</summary>
		public void AssignTextures(UWTextures pOTextures)
		{
			if (pOTextures == null)
				return;

			for (int liRecord = 1; liRecord < InventoryRecords.Count; liRecord++)
			{
				UWObject lOObject = InventoryRecords[liRecord];

				if (lOObject == null || lOObject.ID > 460)
					continue;

				try
				{
					lOObject.Texture = pOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, lOObject.ID);
				}
				catch
				{
				}
			}
		}
		private void fParse(byte[] pyData)
		{
			if (pyData.Length < 0x60)
				return;

			PlainBytes = (byte[])pyData.Clone();

			StringBuilder lOName = new StringBuilder();

			for (int liIndex = 0; liIndex < NameLength; liIndex++)
			{
				byte lyChar = pyData[NameOffset + liIndex];

				if (lyChar == 0)
					break;

				lOName.Append((char)lyChar);
			}

			Name = lOName.ToString();

			Strength = pyData[StrengthOffset];
			Dexterity = pyData[StrengthOffset + 1];
			Intelligence = pyData[StrengthOffset + 2];
			Attack = pyData[StrengthOffset + 3];
			Defense = pyData[StrengthOffset + 4];

			for (int liIndex = 0; liIndex < SkillCount; liIndex++)
				miSkills[liIndex] = pyData[SkillsOffset + liIndex];

			// The bits run from the top down: An is in bit 7 of the first byte, Ylem in
			// bit 0 of the third.
			for (int liIndex = 0; liIndex < RuneCount; liIndex++)
			{
				int liByte = RuneFlagsOffset + (liIndex / 8);

				mbRunes[liIndex] = liByte < pyData.Length
					&& ((pyData[liByte] >> (7 - (liIndex % 8))) & 1) != 0;
			}

			for (int liSlot = 0; liSlot < miSelectedRunes.Length; liSlot++)
			{
				int liAt = SelectedRunesOffset + liSlot;

				miSelectedRunes[liSlot] = liAt < pyData.Length && pyData[liAt] != SelectedRuneNone
					? pyData[liAt]
					: -1;
			}

			CurrentVitality = pyData[CurrentVitalityOffset];
			MaxVitality = pyData[MaxVitalityOffset];
			CurrentMana = pyData[CurrentManaOffset];
			MaxMana = pyData[MaxManaOffset];
			Hunger = pyData[0x39];
			Fatigue = pyData[0x3A];
			MealHealCounter = pyData[MealHealCounterOffset];
			Counter3C = pyData[Counter3COffset];

			// The game clock, a whole four-byte word. Its unit is given in the
			// reference's conversions: 0x3C00 per game minute, 0xE1000 per hour
			// (playerdatclock.Minute and TwelveHourClock), so one day is 0x1518000.
			ClockValue = (int)fRead32(pyData, 0xCE);
			Level = pyData[LevelOffset];

			CurrentWeight = fRead16(pyData, 0x4A);
			// The original does not compute the carried load on demand but keeps it
			// as a running total. What the game shows, however, is the REMAINING capacity,
			// i.e. maximum load minus carried, divided by ten (per the reference
			// uimanager_views.RefreshWeightDisplay, cross-checked against a save game of the
			// user: 420 minus 345 gives 75, 7 is shown).
			CarriedWeight = fRead16(pyData, CarriedWeightOffset);
			MaxWeight = fRead16(pyData, 0x4C);
			Experience = fRead32(pyData, 0x4E);
			AvailableSkillPoints = pyData[0x52];
			TotalSkillPoints = pyData[0x53];

			// The class is in the upper three bits of a byte otherwise used for other things.
			//
			// Cross-checked against three of the user's save games (2026-09-03): 0x73 gives Tinker,
			// 0x2F Mage, 0x0D Fighter - all three match the original. Byte 0x63
			// was suspected for a while; it happens to give the same result for two of the three
			// characters and only stands out with the third. So there is NO offset shift in
			// the docs here, unlike with the location values.
			CharacterClass = (pyData[0x64] >> 5) & 0x7;

			// THE SAME BYTE carries two more things, and the paperdoll needs both:
			// bit 1 is the gender, bits 2 to 4 the body shape. From the reference
			// (playerdat.Body and playerdat.gender, there at 0x65 - our count is
			// one lower because the file's key byte is not counted).
			IsFemale = ((pyData[0x64] >> 1) & 0x1) != 0;
			Body = (pyData[0x64] >> 2) & 0x7;
			IsLeftHanded = (pyData[0x64] & 0x1) == 0;
			IsEasyDifficulty = pyData.Length > DifficultyOffset && pyData[DifficultyOffset] != 0;

			fReadStoredState(pyData);

			// Until 2026-09-01 the location values were off by one byte and the level by two.
			// Noticed on real save games: the tiles came out as 596 and 768,
			// the level as 11 and 62. With these offsets the same files give tile
			// 30/60 on level 1 and tile 6/27 on level 5 - all within the valid range.
			//
			// The UPPER byte of PositionX is the tile, the lower one the fine position within it.
			PositionX = fRead16(pyData, 0x54);
			PositionY = fRead16(pyData, 0x56);
			// The field holds zpos * 8 - see WritePosition.
			ZPosition = fRead16(pyData, 0x58) >> 3;
			Heading = fRead16(pyData, 0x5A);
			DungeonLevel = pyData[0x5C];
			MoonstoneLevel = pyData[0x5E] & 0xF;
			SilverTreeLevel = (pyData[0x5E] >> 4) & 0xF;
			TalismansDestroyable = (pyData[0x62] & 4) != 0;
			IncenseCounter = pyData[0x61] & 0x3;
			GaramonBuried = (pyData[0x62] & 8) != 0;
			CupOfWonderFound = (pyData[0x60] & 0x80) != 0;
			KeyOfTruthGiven = (pyData[0x60] & 0x40) != 0;
			OrbDestroyed = (pyData[0x60] & 0x20) != 0;
			OrbManaBackup = pyData[0xB0];
			Intoxication = (fRead16(pyData, IntoxicationOffset) >> 4) & 0x3F;
			Hallucination = (pyData[IntoxicationOffset] >> 2) & 0x3;
			SwimCounter = pyData[SwimCounterOffset];
			SoundEnabled = ((pyData[SettingsOffset] >> SoundEnabledShift) & SoundSettingMask) != 0;
			MusicEnabled = ((pyData[SettingsOffset] >> MusicEnabledShift) & SoundSettingMask) != 0;

			fParseActiveSpells(pyData);

			fParseInventory(pyData);

			IsLoaded = true;
		}

		private static int fRead16(byte[] pyData, int piOffset)
		{
			return piOffset + 1 < pyData.Length ? pyData[piOffset] | (pyData[piOffset + 1] << 8) : 0;
		}

		private static long fRead32(byte[] pyData, int piOffset)
		{
			if (piOffset + 3 >= pyData.Length)
				return 0;

			return pyData[piOffset] | ((long)pyData[piOffset + 1] << 8)
				| ((long)pyData[piOffset + 2] << 16) | ((long)pyData[piOffset + 3] << 24);
		}
	}
}
