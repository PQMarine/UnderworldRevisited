using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S AUTOMAP THAT CAN BE OFF, AND ITS MAP SCRAPS (UW2 plan phase 3 step 6), as Underworld
	/// Exhumed has them. player->automap says whether the player knows where they are: while it is off the
	/// tiles drawn are not written down (3d/GRIDDB.C grdb_elem), the map opens on the level of player->map_scrap
	/// and shows no arrow (ui/AUTOMAP.C). A teleport trap can stop it (event/TRIGGER.C), the Ethereal Void
	/// stops it while the player is there (game/GAMEWRAP.C do_level_hacks), and stepping onto a tile the map
	/// knows starts it again (motion/PHYSICS.C player_newsq).
	///
	/// A bit of a map (obj/USEITEMS.C UseBook) carries the sections it shows in its link, 0x200 plus a mask; once
	/// copied the link is 0x200, and using it again opens the map on the level it covers. Its map and notes lie
	/// in the pages from level 0x47 + quality, which have no map block: 74 and 79 in the shipped file.
	///
	/// The state lives in the player's record, so it is saved with it. Without a record (the walk before its first
	/// save) the automap is on.
	///
	/// NOT BUILT: the other two that set the flag come with their rules, the Locate spell (combat/SPELLS2.C) and
	/// waking from the Void (game/SKILLS.C), and so does the crystal ball, which holds it off while it draws.
	/// </summary>
	public static class UWAutomapStateUw2
	{
		/// <summary>The Ethereal Void's world (Underworld Exhumed game/GAMEWRAP.C do_level_hacks).</summary>
		public const int VoidWorld = 8;

		/// <summary>map_section: a point this close to the map's middle (squared) is section 0.</summary>
		public const int MiddleSectionSquared = 0x51;

		/// <summary>The tile a section's distance counts from.</summary>
		public const int MapMiddle = 0x20;

		/// <summary>The sum of a scrap's new tiles is divided by this for the experience.</summary>
		public const int ExperienceDivisor = 20;

		/// <summary>The note's place on the screen less this, by three, is the tile (update_map_scraps).</summary>
		public const int NoteTileLeft = 10;

		public const int NoteTileBottom = 7;

		public const int PixelsATile = 3;

		/// <summary>A byte's low nibble: 1 to 9 a tile known, 0xA and up one seen but not known.</summary>
		private const int TypeMask = 0xF;

		/// <summary>The tile types are stored in rows of 64, x and y running from 1 to 0x3F.</summary>
		private const int LastTile = 0x3F;

		private const int RowShift = 6;

		public static bool IsOn(UWPlayerFile pOFile)
		{
			return pOFile == null || (pOFile.Record[UWPlayerRecordUw2.AutomapByteOffset] & UWPlayerRecordUw2.AutomapBit) != 0;
		}

		private static void fSetOn(UWPlayerFile pOFile, bool pbOn)
		{
			byte[] lyRecord = pOFile.Record;

			lyRecord[UWPlayerRecordUw2.AutomapByteOffset] = (byte)(pbOn
				? lyRecord[UWPlayerRecordUw2.AutomapByteOffset] | UWPlayerRecordUw2.AutomapBit
				: lyRecord[UWPlayerRecordUw2.AutomapByteOffset] & ~UWPlayerRecordUw2.AutomapBit);
		}

		/// <summary>AutoMap: the level the map opens on, and the scrap's switch taken back. Without the automap
		/// the level of map_scrap; a high bit there (a bit of a map used) overrides both.</summary>
		public static int OpeningLevel(UWPlayerFile pOFile, int piPlayerLevel)
		{
			if (pOFile == null)
				return piPlayerLevel;

			int liScrap = pOFile.Record[UWPlayerRecordUw2.MapScrapOffset];
			int liLevel = IsOn(pOFile) ? piPlayerLevel : liScrap;

			if ((liScrap & 0x80) != 0)
			{
				liLevel = ~liScrap & 0xFF;
				pOFile.Record[UWPlayerRecordUw2.MapScrapOffset] = (byte)liLevel;
			}

			return liLevel;
		}

		/// <summary>ExitAutoMap: the level shown is remembered.</summary>
		public static void LeaveMap(UWPlayerFile pOFile, int piLevel)
		{
			if (pOFile != null)
				pOFile.Record[UWPlayerRecordUw2.MapScrapOffset] = (byte)piLevel;
		}

		/// <summary>UseBook: the next map opens on this level.</summary>
		public static void OpenOnScrap(UWPlayerFile pOFile, int piLevel)
		{
			if (pOFile != null)
				pOFile.Record[UWPlayerRecordUw2.MapScrapOffset] = (byte)~piLevel;
		}

		/// <summary>UseTrap's TRAP_TELEPORT with the setup's bit 2: the map keeps the level left.</summary>
		public static void StopByTrap(UWPlayerFile pOFile, int piPlayerLevel)
		{
			if (pOFile == null)
				return;

			if (IsOn(pOFile))
				pOFile.Record[UWPlayerRecordUw2.MapScrapOffset] = (byte)piPlayerLevel;

			fSetOn(pOFile, false);
		}

		/// <summary>do_level_hacks: the Void takes the automap away on arrival and gives it back on leaving.</summary>
		public static void ChangeLevel(UWPlayerFile pOFile, int piFromLevel, int piToLevel)
		{
			if (pOFile == null)
				return;

			if ((piFromLevel - 1) / UWAutomapScreenUw2.LevelsAWorld == VoidWorld)
				fSetOn(pOFile, UWBinary.ReadU16(pOFile.Record, UWPlayerRecordUw2.SavedAutomapOffset) != 0);

			if ((piToLevel - 1) / UWAutomapScreenUw2.LevelsAWorld == VoidWorld)
			{
				UWBinary.Write16(pOFile.Record, UWPlayerRecordUw2.SavedAutomapOffset, IsOn(pOFile) ? 1 : 0);
				fSetOn(pOFile, false);
			}
		}

		/// <summary>player_newsq: a tile the map knows puts the automap on again.</summary>
		public static void EnterTile(UWPlayerFile pOFile, byte pyAutomapByte)
		{
			int liType = pyAutomapByte & TypeMask;

			if (pOFile != null && !IsOn(pOFile) && liType != 0 && liType < UWLevel.FirstUndiscoveredType)
				fSetOn(pOFile, true);
		}

		/// <summary>map_section: which of the map's sections a point relative to its middle lies in, 0 the middle,
		/// 1 to 7 round it. The sections are not all the same size, as in the original.</summary>
		public static int Section(int piX, int piY)
		{
			int liSum = 0;

			if ((piX * piX) + (piY * piY) <= MiddleSectionSquared)
				return 0;

			if (piY < 0)
			{
				piY = -piY;
				piX = -piX;
				liSum = 0x1C;
			}

			if (piX < 0)
			{
				int liTurned = piY;

				piY = -piX;
				piX = liTurned;
				liSum += 0xE;
			}

			if (piY < piX)
				liSum += piY * 7 / piX;
			else
				liSum += 0xE - (piX * 7 / piY);

			liSum = (liSum / 8) + 1;

			return liSum > 7 ? 7 : liSum;
		}

		/// <summary>update_map_scraps' first loop: the scrap's map with every tile outside the sections made
		/// unseen. It ORs the unseen code into the byte, so a known tile does not always come out as its own
		/// code, as in the original.</summary>
		public static byte[] ScrapShown(byte[] pyScrapMap, int piSections)
		{
			byte[] lyShown = (byte[])pyScrapMap.Clone();

			for (int liY = LastTile; liY >= 1; liY--)
			{
				for (int liX = 1; liX <= LastTile; liX++)
				{
					if (((piSections >> Section(liX - MapMiddle, liY - MapMiddle)) & 1) != 0)
						continue;

					int liAt = (liY << RowShift) + liX;
					int liCode = lyShown[liAt] & TypeMask;

					if (liCode >= 6 && liCode <= 9)
						liCode = 0xB;
					else if (liCode <= 5)
						liCode += 0xA;

					lyShown[liAt] |= (byte)liCode;
				}
			}

			return lyShown;
		}

		/// <summary>update_map_scraps: what the scrap shows, into the level's map and notes. A tile takes the
		/// scrap's byte where the level's map has nothing, or where it has only seen one the scrap knows; a note
		/// comes along when its tile lies in the sections and the page has room. The answer is the experience,
		/// a level's worth per tile, a twentieth in all (player_get_exp's rules come with the rules of phase 4,
		/// so nothing adds it yet).</summary>
		public static int CopyScrap(byte[] pyScrapMap, IList<UWLevel.MapNote> pOScrapNotes, int piSections, int piLevel,
			byte[] pyLevelMap, IList<UWLevel.MapNote> pOLevelNotes)
		{
			byte[] lyShown = ScrapShown(pyScrapMap, piSections);
			int liExperience = 0;

			for (int liY = LastTile; liY >= 1; liY--)
			{
				for (int liX = 1; liX < LastTile; liX++)
				{
					int liAt = (liY << RowShift) + liX;
					int liScrap = lyShown[liAt] & TypeMask;
					int liOwn = pyLevelMap[liAt] & TypeMask;

					if (liOwn != 0 && (liScrap >= UWLevel.FirstUndiscoveredType || liOwn < UWLevel.FirstUndiscoveredType))
						continue;

					liExperience += (piLevel / UWAutomapScreenUw2.LevelsAWorld) + 1;
					pyLevelMap[liAt] = lyShown[liAt];
				}
			}

			foreach (UWLevel.MapNote lONote in pOScrapNotes)
			{
				int liTileX = (lONote.X - NoteTileLeft) / PixelsATile;
				int liTileY = (lONote.Y - NoteTileBottom) / PixelsATile;
				int liSection = Section(liTileX - MapMiddle, liTileY - MapMiddle);

				if (((piSections >> liSection) & 1) != 0 && UWMapNoteRules.CanAddNote(pOLevelNotes.Count))
					pOLevelNotes.Add(lONote);
			}

			return liExperience / ExperienceDivisor;
		}
	}
}
