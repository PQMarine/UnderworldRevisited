namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S CLASS 11 (xt_spells, Underworld Exhumed uw2 combat/SPELLS2.C:695; UW2 plan phase 4, list
	/// item 3, stage 2 part A). The player's only, but Tremor, which a creature casts as well. Its minors are not
	/// the first game's (UWMiscSpellRules): 1 is Portal where the first game detects monsters, 2 Restoration,
	/// 3 Locate, 13 Dispel Hunger; 4 and 5 do nothing.
	///
	///   0    Speed            active spell, effect minor 2
	///   1    Portal           two squares ahead, if there is room
	///   2    Restoration      poison, mushrooms, drink, paralysis, hunger, fatigue gone, full health
	///   3    Locate           the automap on, not in the Ethereal Void
	///   6    Cure Poison
	///   7    Roaming Sight    active spell, the view leaves the body (not built yet: stage 2 part D)
	///   8    Telekinesis      active spell, effect minor 3
	///   9    Tremor           falling boulders; in the Void the things of the floor
	///   10   Gate Travel      to the level of the first moonstone (from things only)
	///   11   Freeze Time      active spell, effect minor 0
	///   12   Armageddon       inventory, the level's objects, the bag and shelf gone; every later level empty
	///   13   Dispel Hunger
	/// </summary>
	public static class UWExtraSpellRulesUw2
	{
		public const int MajorClass = UWMiscSpellRules.MajorClass;

		public const int SpeedSpell = 0;

		public const int PortalSpell = 1;

		public const int RestorationSpell = 2;

		public const int LocateSpell = 3;

		public const int CurePoisonSpell = 6;

		public const int RoamingSightSpell = 7;

		public const int TelekinesisSpell = 8;

		public const int TremorSpell = 9;

		public const int GateTravelSpell = 10;

		public const int FreezeTimeSpell = 11;

		public const int ArmageddonSpell = 12;

		public const int DispelHungerSpell = 13;

		/// <summary>The Ethereal Void's world (levels 65 to 72), where Locate and Roaming Sight do nothing.</summary>
		public const int VoidWorld = 8;

		public const int LevelsPerWorld = 8;

		// game_sprint numbers of block 1, one more in ours.
		public const int NoEffectString = 0x142 + 1;

		public const int PositionRevealedString = 0x12A + 1;

		public const int NoSuitableSpaceString = 0x132 + 1;

		public const int FeelMuchBetterString = 0x133 + 1;

		public const int WellFedString = 0x134 + 1;

		public const int NoMoonstoneString = 0x121 + 1;

		/// <summary>Portal's distance, in squares (the loop for (dist = 2; dist &lt;= 2; ...)).</summary>
		public const int PortalDistance = 2;

		/// <summary>Portal's height test: the target tile's height less the player's fine z (zpos &lt;&lt; 3) at most
		/// this. The original compares the tile's 4-bit height with the fine z, so above the lowest zpos it always
		/// holds; kept as the code has it.</summary>
		public const int PortalMaxRise = 2;

		/// <summary>Dispel Hunger's hunger (SPELLS2.C:791).</summary>
		public const int WellFedHunger = 0xC0;

		/// <summary>Whether the minor runs on the second game yet (Roaming Sight comes with part D).</summary>
		public static bool IsBuilt(int piMinor)
		{
			return piMinor != RoamingSightSpell;
		}

		/// <summary>
		/// xt_spells: piStability is the lasting spells' duration (RollStability of the minor's top bits). False
		/// when the minor does nothing; a lasting spell with all slots taken is false as well, as the first game's
		/// class 11 answers (set_curmagic's result is ignored there, so the cast still counts as taken in
		/// ApplyEffect - see Cast's caller).
		/// </summary>
		public static bool Cast(int piMinor, int piStability, IUWSpellHost pIHost)
		{
			if (pIHost == null)
				return false;

			IUWSpellHostUw2 lIPlayer = pIHost.HasPlayer ? pIHost as IUWSpellHostUw2 : null;

			// "the player's only, except tremor" (SPELLS2.C:703: caster == ThePlayer || sub == 9).
			if (lIPlayer == null && piMinor != TremorSpell)
				return false;

			switch (piMinor)
			{
				case SpeedSpell:
					return pIHost.TryAddActiveSpell(MajorClass, UWMiscSpellRules.SpeedEffectMinor, piStability);

				case PortalSpell:
					return fPortal(pIHost, lIPlayer);

				case RestorationSpell:
					return fRestore(pIHost, lIPlayer);

				case LocateSpell:
					return fLocate(pIHost, lIPlayer);

				case CurePoisonSpell:
					pIHost.CurePoison();

					return true;

				case TelekinesisSpell:
					return pIHost.TryAddActiveSpell(MajorClass, UWMiscSpellRules.TelekinesisEffectMinor, piStability);

				case TremorSpell:
					return fTremor(pIHost);

				case GateTravelSpell:
					return fGateTravel(pIHost, lIPlayer);

				case FreezeTimeSpell:
					return pIHost.TryAddActiveSpell(MajorClass, UWMiscSpellRules.FreezeTimeEffectMinor, piStability);

				case ArmageddonSpell:
					// The engine's part through the shared host (UWSpellHostUw2.Armageddon: the inventory, the
					// level's objects, the shelf); the bag and the record's bit here.
					pIHost.Armageddon();
					UWPlayerRecordUw2.ClearRuneBag(lIPlayer.Record);
					UWPlayerRecordUw2.SetArmageddon(lIPlayer.Record);

					return true;

				case DispelHungerSpell:
					lIPlayer.Vitals.FeedTo(WellFedHunger);
					pIHost.World.Presentation.AddGeneralMessage(WellFedString);

					return true;

				default:
					return false;
			}
		}

		/// <summary>The world of a dungeon level, 0-based (Britannia 0, the Void 8).</summary>
		public static int WorldOf(int piDungeonLevel)
		{
			return (piDungeonLevel - 1) / LevelsPerWorld;
		}

		private static int fWorld(IUWSpellHost pIHost)
		{
			UWLevel lOLevel = UWSpellCasting.LevelOf(pIHost);

			return lOLevel != null ? WorldOf(lOLevel.LevelNumber) : -1;
		}

		/// <summary>
		/// Portal (SPELLS2.C:711): the square two along the player's heading (move_along), if its floor height
		/// less the player's fine z is at most 2 and his body fits there (can_place at the square's centre and
		/// his height); else "There is no suitable space."
		/// </summary>
		private static bool fPortal(IUWSpellHost pIHost, IUWSpellHostUw2 pIPlayer)
		{
			UWLevel lOLevel = UWSpellCasting.LevelOf(pIHost);

			if (lOLevel == null)
				return false;

			UWTilePos lOTarget = UWAreaSpellRules.GetCentreTile(pIHost, PortalDistance);
			UWTile lOTile = lOLevel.GetTile(lOTarget.X, lOTarget.Y);
			int liZ = pIHost.World.Player.FeetSpot.ZPos << 3;

			if (lOTile != null && PortalAllows(lOTile.FloorHeight >> 4, liZ) && pIPlayer.PlayerFitsAt(lOTarget))
			{
				pIPlayer.MovePlayer(0, lOTarget);

				return true;
			}

			pIHost.World.Presentation.AddGeneralMessage(NoSuitableSpaceString);

			return true;
		}

		/// <summary>Portal's height test with the tile's 4-bit height and the player's fine z.</summary>
		public static bool PortalAllows(int piTileHeight, int piFineZ)
		{
			return piTileHeight - piFineZ <= PortalMaxRise;
		}

		/// <summary>Restoration (SPELLS2.C:729): the vitals' part, the paralysis in the record, "You feel much
		/// better."</summary>
		private static bool fRestore(IUWSpellHost pIHost, IUWSpellHostUw2 pIPlayer)
		{
			UWPlayerRecordUw2.SetParalysis(pIPlayer.Record, 0);
			pIPlayer.Vitals.Restore();
			pIHost.World.Presentation.AddGeneralMessage(FeelMuchBetterString);

			return true;
		}

		/// <summary>Locate (SPELLS2.C:743): the automap on, unless it is on or the player is in the Void.</summary>
		private static bool fLocate(IUWSpellHost pIHost, IUWSpellHostUw2 pIPlayer)
		{
			if (UWAutomapStateUw2.IsOn(pIPlayer.Record) || fWorld(pIHost) == VoidWorld)
			{
				pIHost.World.Presentation.AddGeneralMessage(NoEffectString);

				return true;
			}

			pIHost.World.Presentation.AddGeneralMessage(PositionRevealedString);
			UWAutomapStateUw2.TurnOn(pIPlayer.Record);

			return true;
		}

		// ------------------------------------------------- Tremor

		/// <summary>tremor_area's three boulders, from 0x154 (as the first game's).</summary>
		private const int BoulderKinds = 3;

		/// <summary>In the Void, by the floor's texture slot 0 to 4: a skull, a mushroom, a fish, a resilient sphere,
		/// a coin (SPELLS2.C:667); a higher slot drops nothing and still counts.</summary>
		private static readonly int[] miVoidThings = { 0xC2, 0xB9, 0xB6, 0x129, 0xA0 };

		private const int TremorDistance = 5;

		private const int TremorRadius = 3;

		private const int TremorShakeDuration = 0x28;

		/// <summary>
		/// Tremor (SPELLS2.C:771): gronk_area(caster, rollem(8, 3), tremor_area, AREA_RANDOM, 5, 3), the screen
		/// shaking and effect 0x12 at the caster - the first game's walk (UWMiscSpellRules.fTremor) with the
		/// Void's own things. THE VOID'S THINGS FALL as the boulders do here; the original creates them at zpos
		/// 0x6E and gives a speed only to a mobile one (IsMobElem), so a static skull or coin may hang where it
		/// was put - not seen, kept falling until it is.
		/// </summary>
		private static bool fTremor(IUWSpellHost pIHost)
		{
			UWLevel lOLevel = UWSpellCasting.LevelOf(pIHost);

			if (lOLevel == null || lOLevel.TileData == null)
				return false;

			bool lbVoid = WorldOf(lOLevel.LevelNumber) == VoidWorld;
			UWRulesMagic.TremorRules lODice = pIHost.World.Session.Rules.Magic.Tremor;

			UWAreaWalk.Square(lOLevel, UWAreaSpellRules.GetCentreTile(pIHost, TremorDistance), TremorRadius,
				UWRandom.RollDice(lODice.Dice, lODice.Sides), pIHost.Cast.AttackerIndex, UWAreaWalk.RandomSquares,
				(pOTile, pOTarget) =>
				{
					int liThing = lbVoid ? VoidThing(pOTile) : UWObjectIds.LargeBoulderB + UWRandom.Next(BoulderKinds);

					if (liThing >= 0)
						pIHost.World.DropBoulder(liThing, pOTile.X, pOTile.Z);

					return true;
				});

			pIHost.World.Presentation.ShakeScreen(TremorShakeDuration);
			pIHost.World.Presentation.PlaySoundAtPlayer(UWMiscSpellRules.TremorSound);

			return true;
		}

		/// <summary>The Void's thing for a square, by its floor texture slot (the tile's raw bits 10-13); -1 none.</summary>
		public static int VoidThing(UWTile pOTile)
		{
			int liSlot = (int)((pOTile.RawTileData >> 10) & 0xF);

			return liSlot < miVoidThings.Length ? miVoidThings[liSlot] : -1;
		}

		// ------------------------------------------------- Gate Travel

		/// <summary>
		/// Gate Travel from a thing (SPELLS2.C:775): to the level of the first moonstone, where do_mstone finds the
		/// stone (moveto). As in the first game ours looks for the stone first and stays put when there is none
		/// (Docs/DEVIATIONS.md, "Gate Travel looks for the moonstone before it moves the player").
		/// </summary>
		private static bool fGateTravel(IUWSpellHost pIHost, IUWSpellHostUw2 pIPlayer)
		{
			int liLevel = UWPlayerRecordUw2.Moonstone(pIPlayer.Record, 0);
			UWObject lOStone = null;

			if (liLevel > 0 && pIHost.Data != null && pIHost.Data.Levels != null && liLevel <= pIHost.Data.Levels.Count
				&& pIHost.Data.Levels[liLevel - 1] != null)
				lOStone = UWMoonstoneRules.FindHolderInLevel(pIHost.Data.Levels[liLevel - 1]);

			if (lOStone == null)
			{
				pIHost.World.Presentation.AddGeneralMessage(NoMoonstoneString);

				return true;
			}

			pIPlayer.MovePlayer(liLevel, new UWTilePos(lOStone.TileX, lOStone.TileY));

			return true;
		}
	}
}
