namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S CLASS 13 (special_spells, Underworld Exhumed uw2 combat/SPELLS.C:944; UW2 plan phase 4,
	/// list item 3, stage 2 part A). Only Map Area has runes; the rest come from things (stage 3) or creatures.
	///
	///   0        cut a line of power: a Guardian marker within two squares
	///   2        Mind Blast, a creature's (not built: the creatures' own spells)
	///   3, 4     the view swings, and unless the player resists, he hallucinates (level 2)
	///   5        hallucination level 3
	///   7        Map Area: a circle round the player onto the automap
	///   8, 9     the acid and snowball missiles (not built: stage 2 part C)
	///   12-15    mana of strength 3, 7, 11, 15 (class 10)
	/// </summary>
	public static class UWSpecialSpellRulesUw2
	{
		public const int MajorClass = 13;

		public const int LinesOfPowerSpell = 0;

		public const int MindBlastSpell = 2;

		public const int MapAreaSpell = 7;

		public const int AcidSpell = 8;

		public const int SnowballSpell = 9;

		public const int FirstManaSpell = 12;

		public const int LastManaSpell = 15;

		// game_sprint numbers of block 1, one more in ours.
		public const int VisionDistortsString = 0xF3 + 1;

		public const int SuddenAwarenessString = 0x113 + 1;

		/// <summary>The view's swing either way, in the original's pitch steps (SPELLS.C:979).</summary>
		public const int SwingSteps = 3;

		/// <summary>The intelligence check against this keeps the mind clear (SPELLS.C:980).</summary>
		public const int ResistTarget = 20;

		/// <summary>The flash of a mind that kept clear (fill_FB(0x5F)).</summary>
		public const int ClearMindFlashColour = 0x5F;

		public const float ClearMindFlashSeconds = 0.1f;

		/// <summary>The item of a line of power: an invisible Guardian signet ring with id bit 13 (SPELLS2.C:820).</summary>
		public const int GuardianSignetRing = 0x35;

		/// <summary>The squares round the player the search walks (gronk_area(ThePlayer, 1, ..., 0x80, 0, 2)).</summary>
		public const int MarkerRadius = 2;

		/// <summary>The sounds of a cut line (play_effect_here 0x12 and 0x2A, SPELLS2.C:809).</summary>
		public const int CutSoundA = 0x12;

		public const int CutSoundB = 0x2A;

		public static bool IsBuilt(int piMinor)
		{
			return piMinor != MindBlastSpell && piMinor != AcidSpell && piMinor != SnowballSpell;
		}

		/// <summary>special_spells. False for a minor that does nothing.</summary>
		public static bool Cast(int piMinor, IUWSpellHost pIHost)
		{
			if (pIHost == null)
				return false;

			IUWSpellHostUw2 lIPlayer = pIHost.HasPlayer ? pIHost as IUWSpellHostUw2 : null;

			if (piMinor >= FirstManaSpell && piMinor <= LastManaSpell)
			{
				// do_spell(10, (sub - 11) * 4 - 1): 3, 7, 11, 15; restore_mana works for the player only.
				if (pIHost.HasPlayer)
					pIHost.RestoreManaFromSpell(((piMinor - 11) * 4) - 1);

				return true;
			}

			if (lIPlayer == null)
				return false;

			switch (piMinor)
			{
				case LinesOfPowerSpell:
					return fCutLineOfPower(pIHost, lIPlayer);

				case 3:
				case 4:
					lIPlayer.SwingView(UWRandom.Next(2) == 0 ? -SwingSteps : SwingSteps);

					if (lIPlayer.Vitals.Hallucination > 0
						|| UWSkillCheck.IsSuccess(UWSkillCheck.Check(lIPlayer.Vitals.Intelligence, ResistTarget)))
					{
						pIHost.World.Presentation.FlashWindow(ClearMindFlashColour, ClearMindFlashSeconds);

						return true;
					}

					fHallucinate(pIHost, lIPlayer, 2);

					return true;

				case 5:
					fHallucinate(pIHost, lIPlayer, 3);

					return true;

				case MapAreaSpell:
					return fMapArea(pIHost, lIPlayer);

				default:
					return false;
			}
		}

		private static void fHallucinate(IUWSpellHost pIHost, IUWSpellHostUw2 pIPlayer, int piLevel)
		{
			pIHost.World.Presentation.AddGeneralMessage(VisionDistortsString);
			pIPlayer.Vitals.SetHallucination(piLevel);
		}

		// ------------------------------------------------- Map Area

		/// <summary>Map Area's radius (SPELLS.C:994): Casting, plus Casting - 13 more above 15, / 5 + 2.</summary>
		public static int MapAreaRadius(int piCasting)
		{
			int liR = piCasting;

			liR += liR > 15 ? liR - 13 : 0;

			return (liR / 5) + 2;
		}

		/// <summary>Map Area: not in the Void; else the circle round the player's square onto the map, "You gain
		/// a sudden awareness of your surroundings."</summary>
		private static bool fMapArea(IUWSpellHost pIHost, IUWSpellHostUw2 pIPlayer)
		{
			UWLevel lOLevel = UWSpellCasting.LevelOf(pIHost);

			if (lOLevel == null || UWExtraSpellRulesUw2.WorldOf(lOLevel.LevelNumber) == UWExtraSpellRulesUw2.VoidWorld)
			{
				pIHost.World.Presentation.AddGeneralMessage(UWExtraSpellRulesUw2.NoEffectString);

				return true;
			}

			UWTerrainUw2 lOTerrain = pIHost.Data != null && pIHost.Data.Terrain != null ? pIHost.Data.Terrain.Uw2 : null;
			UWTilePos lOAt = UWSpellCasting.CasterTile(pIHost);

			MapCircle(lOLevel, lOTerrain, lOAt.X, lOAt.Y, MapAreaRadius(pIPlayer.Vitals.GetSkill(UWPlayerData.Skill.Casting)));
			pIHost.World.Presentation.AddGeneralMessage(SuddenAwarenessString);

			return true;
		}

		/// <summary>
		/// automap_area with clip_circle (uw2 ui/AUTOMAP.C:130, combat/SPELLS.C:935), step for step: x and y from
		/// the centre less r to the centre plus r; a square outside the circle ((x - cx)^2 + (y - cy)^2 &gt; r^2) is
		/// passed over; then an x below 1 becomes 1, an x of 0x3F or more ends the row, and so does a y below 1 or
		/// of 0x3F or more. The byte written is the tile's type and its floor's terrain, no door, bridge or stair
		/// (the original's object loop assigns nothing).
		/// </summary>
		public static void MapCircle(UWLevel pOLevel, UWTerrainUw2 pOTerrain, int piCentreX, int piCentreY, int piRadius)
		{
			if (pOLevel == null || pOLevel.AutomapTiles == null)
				return;

			for (int liY = piCentreY - piRadius; liY <= piCentreY + piRadius; liY++)
			{
				for (int liX = piCentreX - piRadius; liX <= piCentreX + piRadius; liX++)
				{
					int liDx = liX - piCentreX;
					int liDy = liY - piCentreY;

					if ((liDx * liDx) + (liDy * liDy) > piRadius * piRadius)
						continue;

					if (liX < 1)
						liX = 1;

					if (liX >= 0x3F || liY < 1 || liY >= 0x3F)
						break;

					UWTile lOTile = pOLevel.GetTile(liX, liY);

					if (lOTile == null)
						continue;

					int liTerrain = pOTerrain != null ? pOTerrain.Class(lOTile.TextureFloor) : 0;

					pOLevel.AutomapTiles[new UWTilePos(liX, liY).Index] = UWAutomapCode.Compose((int)lOTile.TileType, 0, liTerrain, UWGame.Uw2);
				}
			}
		}

		// ------------------------------------------------- Lines of power

		/// <summary>
		/// thump_your_magic_twanger_froggie and check_Guardian_magic_marker (SPELLS2.C:804, :820): the first
		/// Guardian marker within two squares is taken away; if this world's line was not cut yet its bit goes into
		/// quest byte 128, all eight set quest 14, the ice caverns (world 3) quest 52, and the line's sounds play.
		/// A marker of a line already cut goes as well, without the sounds: "The spell has no noticeable effect."
		/// THE MARKER'S EFFECT AND THE DIGITAL SOUND are not built (put_effect(obj, 7, ...), do_sfx(4, 0xF)).
		/// </summary>
		private static bool fCutLineOfPower(IUWSpellHost pIHost, IUWSpellHostUw2 pIPlayer)
		{
			UWLevel lOLevel = UWSpellCasting.LevelOf(pIHost);
			bool lbCut = false;

			if (lOLevel != null && lOLevel.TileData != null)
			{
				int liWorld = UWExtraSpellRulesUw2.WorldOf(lOLevel.LevelNumber);
				int liBit = liWorld >= 1 ? 1 << (liWorld - 1) : 0;

				UWAreaWalk.Square(lOLevel, UWSpellCasting.CasterTile(pIHost), MarkerRadius, 1, pIHost.Cast.AttackerIndex,
					UWAreaWalk.Objects, (pOTile, pOTarget) =>
					{
						if (pOTarget == null || pOTarget.ID != GuardianSignetRing || !pOTarget.DoorDirection || !pOTarget.IsHidden)
							return false;

						pOTarget.DoorDirection = false;
						pIHost.World.Remove(pOTarget);

						int liLines = UWPlayerRecordUw2.GetQuest(pIPlayer.Record, UWPlayerRecordUw2.LinesOfPowerQuest);

						if ((liLines & liBit) != 0)
							return true;

						liLines |= liBit;
						UWPlayerRecordUw2.SetQuest(pIPlayer.Record, UWPlayerRecordUw2.LinesOfPowerQuest, liLines);

						if (liLines == 0xFF)
							UWPlayerRecordUw2.SetQuest(pIPlayer.Record, UWPlayerRecordUw2.AllLinesCutQuest, 1);

						if (liWorld == 3)
							UWPlayerRecordUw2.SetQuest(pIPlayer.Record, UWPlayerRecordUw2.IceLineCutQuest, 1);

						lbCut = true;

						return true;
					});
			}

			if (lbCut)
			{
				pIHost.World.Presentation.PlaySoundAtPlayer(CutSoundA);
				pIHost.World.Presentation.PlaySoundAtPlayer(CutSoundB);
			}
			else
				pIHost.World.Presentation.AddGeneralMessage(UWExtraSpellRulesUw2.NoEffectString);

			return true;
		}
	}
}
