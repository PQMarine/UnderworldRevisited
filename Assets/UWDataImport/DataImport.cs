using System.Collections.Generic;
using UWDataImport.UWData;

namespace UWDataImport
{
	/// <summary>
	/// The data folder and the game loaded from it - since 2026-10-14 a FACADE (cleanup steps 2.7
	/// and 2.9): the part that does not depend on a save game lives in UWGameData (GameData), the
	/// game itself in a UWGameSession (Session) that UWSaveCodec loads and saves. Every member here
	/// only forwards, so the many callers that take a DataImport keep working while they move over
	/// to the data and the session.
	/// </summary>
	public class DataImport
	{
		/// <summary>The installation's data, read once (see UWGameData).</summary>
		public UWGameData GameData { get; private set; }

		/// <summary>The game loaded last (LoadGame): world, character, conversation memory. A new
		/// one with every LoadGame, so nothing of the previous game survives in it.</summary>
		public UWGameSession Session { get; private set; }

		/// <summary>Raw 256-colour palettes (PALS.DAT/ALLPALS.DAT), see UWGameData.</summary>
		public UWPalettes Palettes => GameData.Palettes;

		/// <summary>Brightness level lookup tables (LIGHT.DAT) - see UWLightLevels.</summary>
		public UWLightLevels LightLevels => GameData.LightLevels;

		public List<UWLevel> Levels => Session.World.Levels;

		public UWTextures Textures => GameData.Textures;

		public UWStrings Strings => GameData.Strings;

		public UWWeaponAnimations WeaponAnimations => GameData.WeaponAnimations;

		public UWWearables Wearables => GameData.Wearables;

		public UWCursors Cursors => GameData.Cursors;

		public UWPowerGem PowerGem => GameData.PowerGem;

		public UWFlasks Flasks => GameData.Flasks;

		/// <summary>Properties of whole object ranges from OBJECTS.DAT, e.g. light sources.</summary>
		public UWObjectProperties ObjectProperties => GameData.ObjectProperties;

		/// <summary>Properties every object has (COMOBJ.DAT), see UWCommonObjectProperties.</summary>
		public UWCommonObjectProperties CommonObjectProperties => GameData.CommonObjectProperties;

		/// <summary>Class-specific tables from OBJECTS.DAT, see UWObjectClassProperties.</summary>
		public UWObjectClassProperties ObjectClassProperties => GameData.ObjectClassProperties;

		/// <summary>Creature animations from the "crit" folder - see UWCritterAnimations.</summary>
		public UWCritterAnimations CritterAnimations => GameData.CritterAnimations;

		/// <summary>Conversation programs from CNV.ARK - see UWConversations.</summary>
		public UWConversations Conversations => GameData.Conversations;

		/// <summary>The memory of the conversations (BGLOBALS.DAT), see UWConversationGlobals -
		/// the session's ConversationMemory, which goes into the save game when saving.</summary>
		public UWConversationGlobals ConversationGlobals => Session.ConversationMemory;

		/// <summary>Terrain properties of the textures (TERRAIN.DAT), see UWTerrain.</summary>
		public UWTerrain Terrain => GameData.Terrain;

		/// <summary>The six original fonts (FONT*.SYS) - see UWFonts.</summary>
		public UWFonts Fonts => GameData.Fonts;

		/// <summary>Combination rules for items (CMB.DAT) - see UWObjectCombining.</summary>
		public UWObjectCombining ObjectCombining => GameData.ObjectCombining;

		/// <summary>Starting character from PLAYER.DAT - see UWPlayerData; the session's Player.</summary>
		public UWPlayerData InitialPlayer => Session.Player;

		/// <summary>Puts a newly created character in place of the loaded one - see
		/// UWCharacterCreationScreen and UWLevelLoader.</summary>
		public void ReplaceInitialPlayer(UWPlayerData pOPlayer)
		{
			Session.ReplacePlayer(pOPlayer);
		}

		/// <summary>From now on this folder counts as the loaded save game - see
		/// UWSaveCodec.AdoptSavegame.</summary>
		public void AdoptSavegame(string psSavegamePath)
		{
			UWSaveCodec.AdoptSavegame(Session, psSavegamePath);
		}

		/// <summary>Colour lookup tables for transparency (XFER.DAT), see UWTransparencyTables.</summary>
		public UWTransparencyTables TransparencyTables => GameData.TransparencyTables;

		/// <summary>Lighting and view distance (SHADES.DAT) - see UWShades.</summary>
		public UWShades Shades => GameData.Shades;

		/// <summary>The game keeps map notes for this many levels (UWLevArkLayout, where the
		/// file's block numbers live since 2026-10-12; UWHudMap reads the count here).</summary>
		public const int MapNoteLevelCount = UWLevArkLayout.MapNoteLevelCount;

		/// <summary>Writes map notes, automaps and the levels back into LEV.ARK - see
		/// UWSaveCodec.SaveWorld.</summary>
		public bool SaveMapData(string psTargetFolder = null, byte[] pyPlayerSlot = null, int piPlayerLevel = -1)
		{
			return UWSaveCodec.SaveWorld(Session, psTargetFolder, pyPlayerSlot, piPlayerLevel);
		}

		public List<UWLevel.MapNote> GetMapNotes(int piLevelIndex)
		{
			return Session.World.GetMapNotes(piLevelIndex);
		}

		/// <summary>The small remaining files (MONO.DAT, WEAPONS.CM, SKILLS.DAT, LIGHTS.DAT,
		/// CHRGEN.DAT) as raw bytes, see UWGameData.</summary>
		public UWMiscDataFiles MiscDataFiles => GameData.MiscDataFiles;

		/// <summary>Sound effects and music from the "sound" folder - see UWSound.</summary>
		public UWSound Sound => GameData.Sound;

		/// <summary>Folder with the game data (DATA).</summary>
		public string DataPath => GameData.DataPath;

		public string GetTextureDescription(int piTextureIndex) => GameData.GetTextureDescription(piTextureIndex);

		public string GetObjectDescription(int pObjectId) => GameData.GetObjectDescription(pObjectId);

		public string GetObjectLookAndQualityDescription(int pObjectId) => GameData.GetObjectLookAndQualityDescription(pObjectId);

		public string GetWallTextBookDescription(int pObjectId) => GameData.GetWallTextBookDescription(pObjectId);

		/// <summary>Readable inscriptions (block 8), see UWGameData.GetReadableText.</summary>
		public string GetReadableText(int piIndex) => GameData.GetReadableText(piIndex);

		/// <summary>The text of a book or scroll (block 3), see UWGameData.GetBookText.</summary>
		public string GetBookText(int piIndex) => GameData.GetBookText(piIndex);

		/// <summary>General game messages (block 1), see UWGameData.GetGeneralMessage.</summary>
		public string GetGeneralMessage(int piIndex) => GameData.GetGeneralMessage(piIndex);

		/// <summary>Level-related event messages (block 9), see UWGameData.GetLevelMessage.</summary>
		public string GetLevelMessage(int piIndex) => GameData.GetLevelMessage(piIndex);

		/// <summary>Cutscene animation from the "cuts" folder beside "data", cached; null when the
		/// folder is missing. See UWGameData.GetCutscene.</summary>
		public UWCutscene GetCutscene(string psFileName) => GameData.GetCutscene(psFileName);

		/// <summary>Folder of the loaded save game, or empty on a normal start.</summary>
		public string SavegamePath => Session.World.SavegamePath;

		/// <summary>Whether the world data comes from a save game.</summary>
		public bool IsSavegame => Session.World.IsSavegame;

		/// <summary>
		/// Loads the static game data and then the game state - either from the save game
		/// folder or, without one, the untouched world of the data folder. From a save game
		/// only the changeable part comes (LEV.ARK, PLAYER.DAT, BGLOBALS.DAT, see LoadGame);
		/// everything else (textures, strings, tables) stays with the data folder. Diagnostic
		/// tools use this; the game itself keeps one instance per data folder and calls LoadGame
		/// for every start (see UWLevelLoader).
		/// </summary>
		public DataImport(string psPath, string psSavegamePath = null)
		{
			GameData = new UWGameData(psPath);
			LoadGame(psSavegamePath);
		}

		/// <summary>
		/// (Re)loads everything that belongs to one game: world (LEV.ARK with automaps and map
		/// notes), character (PLAYER.DAT) and conversation memory (BGLOBALS.DAT), into a new
		/// session (UWSaveCodec.Load). Null or an invalid folder means the data folder, i.e. a fresh
		/// world. Everything else - textures, strings, tables, fonts, sound - stays as loaded, which
		/// is what makes starting a game from the main menu cheap.
		/// </summary>
		public void LoadGame(string psSavegamePath)
		{
			Session = UWSaveCodec.Load(GameData, psSavegamePath);
		}
	}
}
