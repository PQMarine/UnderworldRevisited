using System.Collections.Generic;
using System.IO;
using UWDataImport.Audio;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE INSTALLATION'S DATA (cleanup step 2.7, 2026-10-14): everything read from the data
	/// folder that no game changes - palettes, textures, strings, the object and creature tables,
	/// conversations, terrain, fonts, sound, shades and the small misc files. One instance per data
	/// folder, shared by every game started from it; the changeable part of a game lives in
	/// UWGameSession.
	///
	/// Until then this was the first half of DataImport (its fLoadStaticData), mixed with the game
	/// state in one class. DataImport keeps forwarding every member, so the callers that take a
	/// DataImport keep working while they move over one by one.
	///
	/// Immutable after construction, except for the cutscene cache, which only remembers what was
	/// read from the CUTS folder.
	/// </summary>
	public sealed class UWGameData
	{
		/// <summary>Folder with the game data (DATA), needed for files outside of it - see
		/// GetCutscene.</summary>
		public string DataPath { get; }

		/// <summary>Raw 256-colour palettes (PALS.DAT/ALLPALS.DAT) - needed where a real palette
		/// colour is required instead of one taken from a graphic, e.g. for the single-colour
		/// faces of the baked-in 3D models (see UW3DModelImport).</summary>
		public UWPalettes Palettes { get; }

		/// <summary>Brightness level lookup tables (LIGHT.DAT) - see UWLightLevels.</summary>
		public UWLightLevels LightLevels { get; }

		public UWTextures Textures { get; }

		public UWStrings Strings { get; }

		public UWWeaponAnimations WeaponAnimations { get; }

		public UWWearables Wearables { get; }

		public UWCursors Cursors { get; }

		public UWPowerGem PowerGem { get; }

		public UWFlasks Flasks { get; }

		/// <summary>Properties of whole object ranges from OBJECTS.DAT, e.g. light sources.</summary>
		public UWObjectProperties ObjectProperties { get; }

		/// <summary>Properties every object has (COMOBJ.DAT) - height, radius, mass, value,
		/// pickupability. See UWCommonObjectProperties.</summary>
		public UWCommonObjectProperties CommonObjectProperties { get; }

		/// <summary>Class-specific tables from OBJECTS.DAT - weapons, armour, creatures. See
		/// UWObjectClassProperties.</summary>
		public UWObjectClassProperties ObjectClassProperties { get; }

		/// <summary>Creature animations from the "crit" folder - see UWCritterAnimations.</summary>
		public UWCritterAnimations CritterAnimations { get; }

		/// <summary>Conversation programs from CNV.ARK - see UWConversations.</summary>
		public UWConversations Conversations { get; }

		/// <summary>Terrain properties of the textures (TERRAIN.DAT) - water, lava, windows,
		/// stairs. See UWTerrain.</summary>
		public UWTerrain Terrain { get; }

		/// <summary>The six original fonts (FONT*.SYS) - see UWFonts.</summary>
		public UWFonts Fonts { get; }

		/// <summary>Combination rules for items (CMB.DAT) - see UWObjectCombining.</summary>
		public UWObjectCombining ObjectCombining { get; }

		/// <summary>Colour lookup tables for transparency (XFER.DAT) - see
		/// UWTransparencyTables.</summary>
		public UWTransparencyTables TransparencyTables { get; }

		/// <summary>Lighting and view distance (SHADES.DAT) - see UWShades.</summary>
		public UWShades Shades { get; }

		/// <summary>The small remaining files: MONO.DAT, WEAPONS.CM, SKILLS.DAT as well as the
		/// undocumented LIGHTS.DAT and CHRGEN.DAT as raw bytes.</summary>
		public UWMiscDataFiles MiscDataFiles { get; }

		/// <summary>Sound effects and music from the "sound" folder - see UWSound.</summary>
		public UWSound Sound { get; }

		private readonly Dictionary<string, UWCutscene> mOCutscenes = new Dictionary<string, UWCutscene>();

		/// <summary>Reads every file of the data folder that does not depend on a save game.</summary>
		public UWGameData(string psPath)
		{
			DataPath = psPath;

			Palettes = new UWPalettes(psPath);
			LightLevels = new UWLightLevels(psPath);
			Textures = new UWTextures(psPath, Palettes);
			PowerGem = new UWPowerGem(Textures);
			Flasks = new UWFlasks(Textures);
			WeaponAnimations = new UWWeaponAnimations(psPath, Textures.GetTexturesByType(UWTexture.TextureTypes.WEAPONS));
			Wearables = new UWWearables(Textures, WeaponAnimations.Black);
			Cursors = new UWCursors(Textures.GetTexturesByType(UWTexture.TextureTypes.CURSORS));
			ObjectProperties = new UWObjectProperties(psPath);
			CommonObjectProperties = new UWCommonObjectProperties(psPath);
			ObjectClassProperties = new UWObjectClassProperties(psPath);
			CritterAnimations = new UWCritterAnimations(psPath);
			Conversations = new UWConversations(psPath);
			Terrain = new UWTerrain(psPath);
			Fonts = new UWFonts(psPath);
			ObjectCombining = new UWObjectCombining(psPath);
			TransparencyTables = new UWTransparencyTables(psPath);
			Shades = new UWShades(psPath);
			MiscDataFiles = new UWMiscDataFiles(psPath);
			Sound = new UWSound(psPath);
			Strings = new UWStrings(File.ReadAllBytes(Path.Combine(psPath, "STRINGS.PAK")));
		}

		public string GetTextureDescription(int piTextureIndex)
		{
			return Strings.Blocks[10].Strings[piTextureIndex];
		}

		public string GetObjectDescription(int pObjectId)
		{
			return Strings.Blocks[4].Strings[pObjectId];
		}

		public string GetObjectLookAndQualityDescription(int pObjectId)
		{
			return Strings.Blocks[5].Strings[pObjectId];
		}

		public string GetWallTextBookDescription(int pObjectId)
		{
			return Strings.Blocks[3].Strings[pObjectId];
		}

		/// <summary>Readable inscriptions (block 8): wall writings, plaques, signs, runes, shrine
		/// texts - and from index 370 on the matching introductions ("The writing reads: ", "The
		/// plaque reads: ", ...). See UWObjectMechanics.GetWritingTextIndex/GetWritingPrefixIndex.
		/// </summary>
		public string GetReadableText(int piIndex)
		{
			return Strings.Blocks[8].Strings[piIndex];
		}

		/// <summary>The text of a book or scroll (block 3) - for the index see
		/// UWObjectMechanics.GetBookStringIndex. Empty means: not a text but a picture.</summary>
		public string GetBookText(int piIndex)
		{
			if (piIndex < 0 || Strings == null || !Strings.Blocks.ContainsKey(3))
				return string.Empty;

			var lOStrings = Strings.Blocks[3].Strings;

			return piIndex < lOStrings.Count ? lOStrings[piIndex] : string.Empty;
		}

		/// <summary>General game messages (block 1) - the feedback messages not tied to a level,
		/// e.g. "The cauldron is empty." or the fountain texts. Index is the ALREADY PARSED index,
		/// i.e. the one from the string export; the project's usual +1 only applies when starting
		/// from an object id.</summary>
		public string GetGeneralMessage(int piIndex)
		{
			return Strings.Blocks[1].Strings[piIndex];
		}

		/// <summary>Level-related event messages (block 9), e.g. the texts of the a_text string
		/// traps - see UWObjectMechanics.GetTextTrapStringIndex for how the index is formed. Block
		/// 9 also contains global texts (intro sequence, special messages like "The doors are
		/// securely locked.") in the lower 64 entries, which do NOT belong to a level.</summary>
		public string GetLevelMessage(int piIndex)
		{
			return Strings.Blocks[9].Strings[piIndex];
		}

		/// <summary>Cutscene animation from the "cuts" folder, which lies NEXT TO "data" (i.e.
		/// C:\UW\CUTS alongside C:\UW\DATA). The result is cached because a file is needed several
		/// times (e.g. CS400.N01 for the windows of every level). If the folder is missing (with an
		/// incomplete copy of the game data it is often only in the CD image), null is returned
		/// instead of an exception.</summary>
		public UWCutscene GetCutscene(string psFileName)
		{
			UWCutscene lOResult;

			if (mOCutscenes.TryGetValue(psFileName, out lOResult))
				return lOResult;

			string lsRoot = Path.GetDirectoryName(DataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			string lsPath = Path.Combine(Path.Combine(lsRoot, "CUTS"), psFileName);
			lOResult = File.Exists(lsPath) ? new UWCutscene(lsPath) : null;
			mOCutscenes[psFileName] = lOResult;

			return lOResult;
		}
	}
}
