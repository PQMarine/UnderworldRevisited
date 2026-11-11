using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S AREA SPELLS, class 6 (UW2 plan phase 4 item 3, stage 2 part B, 2026-11-11, read in
	/// UnderworldExhumed uw2 combat/SPELLS.C: nail_area, gronk_area, process_area, area_spells[] and the sp_
	/// handlers). nail_area takes area_spells[(minor &amp; ~0xC0) - 1]: the handler, a FIXED count (the first game
	/// rolls 3d4), how far ahead of the caster the centre lies and the radius; the minor's top bits are the walk's
	/// type. Seven entries, the first game's four and three new ones:
	///
	///   minor  spell              count  ahead  radius  per square
	///   1      Reveal               100    1      2     sp_true_sight: an object's trap trigger set off (Search 45)
	///   2      Sheet Lightning        6    4      2     lightning, damage_square kind 2 (6d5 magic)
	///   3      Mass Confusion        12    4      2     a creature to goal 2, attitude 1; sparkles on empty squares
	///   4      Flame Wind            10    4      2     sp_meteor: an explosion and damage_square kind 1 (10d6 fire)
	///                                                    ON THAT SQUARE ONLY - the first game burns a cross; at most
	///                                                    five creatures, never the same square twice running, an
	///                                                    empty square (or a thing) one time in three
	///   5      Repel Undead          50    4      2     the player's only: undead flee, those fleeing take Casting
	///                                                    damage; a budget of Casting * 10 hit points
	///   6      Shockwave             50    0      1     Casting / 2 + 15 magic damage to every creature but the caster
	///   7      Frost                  5    3      1     frost on empty squares, 10 magic-and-cold damage to all there
	///
	/// THE WALK of type 0x80 calls the handler for EVERY OPEN SQUARE as well (with no object), then for every
	/// object on it - the first game's walk does not (UWAreaWalk's pbOpenSquares). The floating skulls of Repel
	/// Undead (item 0x13, destroy_floatskull, a world hack of WORLDEV.C) come with the world's hacks (G6).
	/// Reveal sets off what the trap search finds: in the second game that comes with the traps (G3), until then
	/// the host finds nothing.
	/// </summary>
	public static class UWAreaSpellRulesUw2
	{
		public const int MajorClass = 6;

		/// <summary>One entry of area_spells[] (SPELLS.C:729).</summary>
		public readonly struct Entry
		{
			public readonly int Count;

			public readonly int Ahead;

			public readonly int Radius;

			public Entry(int piCount, int piAhead, int piRadius)
			{
				Count = piCount;
				Ahead = piAhead;
				Radius = piRadius;
			}
		}

		public const int RevealMinor = 1;

		public const int SheetLightningMinor = 2;

		public const int ConfusionMinor = 3;

		public const int FlameWindMinor = 4;

		public const int RepelUndeadMinor = 5;

		public const int ShockwaveMinor = 6;

		public const int FrostMinor = 7;

		private static readonly Entry[] mOEntries =
		{
			new Entry(100, 1, 2),
			new Entry(6, 4, 2),
			new Entry(12, 4, 2),
			new Entry(10, 4, 2),
			new Entry(50, 4, 2),
			new Entry(50, 0, 1),
			new Entry(5, 3, 1),
		};

		/// <summary>The table's entry of an effect minor (1 to 7); false for another.</summary>
		public static bool TryGetEntry(int piMinor, out Entry pOEntry)
		{
			pOEntry = default;

			if (piMinor < RevealMinor || piMinor > FrostMinor)
				return false;

			pOEntry = mOEntries[piMinor - 1];

			return true;
		}

		/// <summary>Whether this effect minor runs (all seven do).</summary>
		public static bool IsBuilt(int piMinor)
		{
			return TryGetEntry(piMinor, out Entry _);
		}

		// ------------------------------------------------- The effects' pictures (items.h, ANIMO.GR)

		private const int AnimationFirstId = UWObjectClassProperties.AnimationFirstId;

		/// <summary>ITEM_EXPLOSION_1C2, an explosion.</summary>
		private const int ExplosionId = AnimationFirstId + 2;

		/// <summary>ITEM_LIGHTNING_1C5, lightning.</summary>
		private const int LightningId = AnimationFirstId + 5;

		/// <summary>Animation class 7, the spell effect put_effect lays on a struck creature.</summary>
		private const int SpellEffectId = AnimationFirstId + 7;

		/// <summary>Animation class 11, the splash of wound_foe and Shockwave's empty squares.</summary>
		private const int SplashId = AnimationFirstId + 11;

		/// <summary>ITEM_FROST 0x1CA, some frost.</summary>
		private const int FrostId = AnimationFirstId + 10;

		/// <summary>How long a picture stays: as the first game's area pictures (UWAreaSpellRules).</summary>
		private const float EffectSeconds = 0.7f;

		/// <summary>CreateObj's fine spot of a new object (MAPADDR.C).</summary>
		private const int NewObjectFine = 3;

		/// <summary>put_effect's height on an empty square: the caster's z plus this (-(z + 20)).</summary>
		private const int EmptySquareLift = 20;

		// ------------------------------------------------- Numbers of the handlers

		private const int OneInThree = 3;

		/// <summary>sp_meteor: at most this many creatures.</summary>
		private const int FlameWindMostCreatures = 5;

		/// <summary>Shockwave: Casting / 2 + 15 of type 3.</summary>
		private const int ShockwaveBase = 15;

		/// <summary>Repel Undead's budget: Casting times this in hit points.</summary>
		private const int RepelBudgetPerCasting = 10;

		/// <summary>Frost: 10 of magic and cold (0x23).</summary>
		private const int FrostDamage = 10;

		private const int FrostDamageType = UWDamageTypes.Magic | 0x20;

		/// <summary>The goal of a fleeing creature (Cause Fear, hit_critter_goal(6, ...)).</summary>
		private const int FleeingGoal = 6;

		/// <summary>The first game's class 7 minor 1, Cause Fear (UWTargetSpell): goal 6 against the player after the
		/// resistance check, the attitude kept - what hit_critter_goal(6, -1, 1) does in the second game.</summary>
		private const int CauseFearTargetMinor = 1;

		/// <summary>
		/// THE CAST (nail_area): the entry of the minor's effect, area_spell_state at 0, the centre the entry's
		/// distance ahead of the caster (gronk_area, move_along), the walk of the minor's top bits over the square of
		/// the entry's radius with the entry's count. False for a minor the table has not.
		/// </summary>
		public static bool Cast(int piFullMinor, IUWSpellHost pIHost)
		{
			int liMinor = UWRunicMagic.GetEffectMinor(piFullMinor);

			if (pIHost == null || (!pIHost.HasPlayer && !pIHost.Cast.ByCreature) || !TryGetEntry(liMinor, out Entry lOEntry))
				return false;

			UWLevel lOLevel = UWSpellCasting.LevelOf(pIHost);

			if (lOLevel == null || lOLevel.TileData == null)
				return false;

			State lOState = new State(liMinor, pIHost, lOLevel);
			UWTilePos lOCentre = UWAreaSpellRules.GetCentreTile(pIHost, lOEntry.Ahead);

			UWAreaWalk.Square(lOLevel, lOCentre, lOEntry.Radius, lOEntry.Count, pIHost.Cast.AttackerIndex,
				UWRunicMagic.GetAreaTargetType(piFullMinor), lOState.Apply, true);

			return true;
		}

		/// <summary>One cast's walk and its area_spell_state.</summary>
		private sealed class State
		{
			private readonly int miMinor;

			private readonly IUWSpellHost mIHost;

			private readonly UWLevel mOLevel;

			/// <summary>area_spell_state: Flame Wind's last square and creature count, Repel Undead's spent budget.</summary>
			private int miAreaState;

			public State(int piMinor, IUWSpellHost pIHost, UWLevel pOLevel)
			{
				miMinor = piMinor;
				mIHost = pIHost;
				mOLevel = pOLevel;
			}

			public bool Apply(UWTile pOTile, UWObject pOTarget)
			{
				switch (miMinor)
				{
					case RevealMinor:
						return fReveal(pOTarget);

					case SheetLightningMinor:
						return fSheetLightning(pOTile);

					case ConfusionMinor:
						return fConfusion(pOTile, pOTarget);

					case FlameWindMinor:
						return fFlameWind(pOTile, pOTarget);

					case RepelUndeadMinor:
						return fRepelUndead(pOTile, pOTarget);

					case ShockwaveMinor:
						return fShockwave(pOTile, pOTarget);

					case FrostMinor:
						return fFrost(pOTile, pOTarget);

					default:
						return false;
				}
			}

			/// <summary>sp_true_sight: an object that is no trap and carries a list has its trap trigger set off with
			/// Search 45 (UseTrigger's how 5) - the host's look trigger.</summary>
			private bool fReveal(UWObject pOTarget)
			{
				// ol.f.link: the object's own list (word 3 without the quantity bit, UWObject.Quantity).
				if (pOTarget == null || pOTarget.HasQuantity || pOTarget.Quantity == 0
					|| UWObjectIds.Major(pOTarget.ID) == UWObjectMechanics.TrapMajorClass)
					return false;

				return mIHost.FireLookTrigger(pOTarget, UWTrapRules.RevealSearchSkill);
			}

			/// <summary>sp_sheet_light: lightning at a random height, damage_square kind 2; always takes.</summary>
			private bool fSheetLightning(UWTile pOTile)
			{
				fStrikePicture(pOTile, LightningId, UWRandom.Next(4));

				UWRulesMagic.SheetLightningRules lOLightning = mIHost.World.Session.Rules.Magic.SheetLightning;

				mIHost.World.StrikeTile(new UWTilePos(pOTile.X, pOTile.Z), lOLightning.Dice, lOLightning.Sides, UWDamageTypes.Magic,
					mIHost.Cast.AttackerIndex);

				return true;
			}

			/// <summary>sp_confusion: sparkles on one empty square in three (taking nothing); on a creature the spell
			/// effect, then hit_critter_goal(2, 1, 1) - goal 2 against the player, attitude 1, unless resisted; it
			/// takes either way.</summary>
			private bool fConfusion(UWTile pOTile, UWObject pOTarget)
			{
				if (pOTarget == null)
				{
					if (UWRandom.Next(OneInThree) == 0)
						fEmptySquarePicture(pOTile, SpellEffectId);

					return false;
				}

				if (!fIsCreature(pOTarget))
					return false;

				UWTargetSpellRules.Result leResult = mIHost.World.ConfuseCreature(pOTarget);

				if (leResult == UWTargetSpellRules.Result.Missed)
					return false;

				mIHost.World.ShowEffectAtCreature(pOTarget, SpellEffectId, EffectSeconds);

				return true;
			}

			/// <summary>sp_meteor: not the square of the last stroke, not after five creatures; a square or a thing only
			/// one time in three; an explosion with its flames and damage_square kind 1 on THIS square. A creature
			/// counts.</summary>
			private bool fFlameWind(UWTile pOTile, UWObject pOTarget)
			{
				int liSquare = (pOTile.X << 6) + pOTile.Z;
				int liLast = miAreaState >> 4;
				int liCount = miAreaState & 0xF;
				bool lbCreature = pOTarget != null && fIsCreature(pOTarget);

				if (liLast == liSquare || liCount >= FlameWindMostCreatures)
					return false;

				if (!lbCreature && UWRandom.Next(OneInThree) != 0)
					return false;

				int liZPos = fStrikePicture(pOTile, ExplosionId, 0);

				foreach (UWExplosionRules.Flame lOFlame in UWExplosionRules.ScatterFlames(ExplosionId, NewObjectFine, NewObjectFine, liZPos))
					mIHost.World.Presentation.ShowEffect(lOFlame.ObjectId,
						new UWSpot(new UWTilePos(pOTile.X, pOTile.Z), lOFlame.FineX, lOFlame.FineY, lOFlame.ZPos),
						UWExplosionRules.LengthToSeconds(lOFlame.LengthTicks), lOFlame.StartFrame);

				UWRulesMagic.FlameWindRules lOFlame2 = mIHost.World.Session.Rules.Magic.FlameWind;

				mIHost.World.StrikeTile(new UWTilePos(pOTile.X, pOTile.Z), lOFlame2.Dice, lOFlame2.Sides, UWDamageTypes.Fire,
					mIHost.Cast.AttackerIndex);

				if (lbCreature)
					liCount++;

				miAreaState = (liSquare << 4) | (liCount & 0xF);

				return true;
			}

			/// <summary>sp_repel_undead: the player's only (another caster's takes without effect); sparkles on one empty
			/// square in three; an undead creature while the budget of Casting * 10 lasts - one already fleeing takes
			/// Casting magic damage, any other is made to flee - and its hit points go onto the budget.</summary>
			private bool fRepelUndead(UWTile pOTile, UWObject pOTarget)
			{
				if (!mIHost.HasPlayer || mIHost.Cast.ByCreature)
					return true;

				if (pOTarget == null)
				{
					if (UWRandom.Next(OneInThree) == 0)
						fEmptySquarePicture(pOTile, SpellEffectId);

					return false;
				}

				if (!fIsCreature(pOTarget) || !fIsUndead(pOTarget) || fCasting() * RepelBudgetPerCasting < miAreaState)
					return false;

				UWCritterRecord lORecord = pOTarget is UWNpc lONpc ? new UWCritterRecord(lONpc) : null;
				bool lbTook;

				if (lORecord != null && lORecord.Goal == FleeingGoal)
					lbTook = fWound(pOTile, pOTarget, fCasting(), UWDamageTypes.Magic);
				else
					lbTook = mIHost.World.CastTargetSpellOnCreature(pOTarget, CauseFearTargetMinor) != UWTargetSpellRules.Result.Missed;

				miAreaState += lORecord != null ? lORecord.HitPoints : 0;
				mIHost.World.ShowEffectAtCreature(pOTarget, SpellEffectId, EffectSeconds);

				return lbTook;
			}

			/// <summary>sp_shockwave: a splash on every empty square (taking nothing), Casting / 2 + 15 magic damage on
			/// every creature but the caster.</summary>
			private bool fShockwave(UWTile pOTile, UWObject pOTarget)
			{
				if (pOTarget == null)
				{
					fEmptySquarePicture(pOTile, SplashId);

					return false;
				}

				if (UWAreaWalk.MobileIndexOf(mOLevel, pOTarget) == mIHost.Cast.AttackerIndex)
					return false;

				return fWound(pOTile, pOTarget, (fCasting() / 2) + ShockwaveBase, UWDamageTypes.Magic);
			}

			/// <summary>sp_frost: frost at a random height on an empty square (taking nothing); 10 magic-and-cold damage
			/// on a creature (wound_foe) or a thing (damage_item) there - neither takes.</summary>
			private bool fFrost(UWTile pOTile, UWObject pOTarget)
			{
				if (pOTarget == null)
				{
					int liRoll = UWRandom.Next(OneInThree);

					fStrikePicture(pOTile, FrostId, liRoll);

					return false;
				}

				if (fIsCreature(pOTarget))
					return fWound(pOTile, pOTarget, FrostDamage, FrostDamageType);

				mIHost.World.Damage(UWDamageTarget.ObjectOf(pOTarget), UWBlow.Blast(FrostDamage, FrostDamageType, mIHost.Cast.AttackerIndex));

				return false;
			}

			/// <summary>wound_foe: the splash on the creature, the damage charged to the caster. False for no creature.</summary>
			private bool fWound(UWTile pOTile, UWObject pOTarget, int piDamage, int piType)
			{
				if (!fIsCreature(pOTarget))
					return false;

				mIHost.World.ShowEffectAtCreature(pOTarget, SplashId, EffectSeconds, UWAreaSpellRules.WoundSplashEighths);
				mIHost.World.Damage(UWDamageTarget.Creature(UWAreaWalk.MobileIndexOf(mOLevel, pOTarget)),
					UWBlow.Blast(piDamage, piType, mIHost.Cast.AttackerIndex));

				return true;
			}

			/// <summary>build_new_obj and add_animobj: the picture at CreateObj's fine spot and a random height between
			/// the floor and the ceiling, from piStartFrame. Returns the height.</summary>
			private int fStrikePicture(UWTile pOTile, int piObjectId, int piStartFrame)
			{
				int liZPos = UWAreaSpellRules.RollStrikeZPos(pOTile.FloorHeight >> 1);

				mIHost.World.Presentation.ShowEffect(piObjectId,
					new UWSpot(new UWTilePos(pOTile.X, pOTile.Z), NewObjectFine, NewObjectFine, liZPos), EffectSeconds, piStartFrame);

				return liZPos;
			}

			/// <summary>put_effect with no object: the picture at CreateObj's fine spot, the caster's height plus 20.</summary>
			private void fEmptySquarePicture(UWTile pOTile, int piObjectId)
			{
				mIHost.World.Presentation.ShowEffect(piObjectId,
					new UWSpot(new UWTilePos(pOTile.X, pOTile.Z), NewObjectFine, NewObjectFine, fCasterZPos() + EmptySquareLift),
					EffectSeconds, 0);
			}

			private int fCasterZPos()
			{
				if (mIHost.Cast.ByCreature)
					return mIHost.Cast.CasterSpot.ZPos;

				return mIHost.World.Player != null ? mIHost.World.Player.FeetSpot.ZPos : 0;
			}

			/// <summary>The player's Casting - the handlers read player->skills even for another caster.</summary>
			private int fCasting()
			{
				if (mIHost.HasPlayer)
					return mIHost.PlayerCastingSkill;

				return mIHost.World.Player != null && mIHost.World.Player.Vitals != null
					? mIHost.World.Player.Vitals.GetSkill(UWPlayerData.Skill.Casting) : 0;
			}

			private static bool fIsCreature(UWObject pOObject)
			{
				return pOObject != null && UWObjectIds.Major(pOObject.ID) == UWObjectIds.CreatureMajorClass;
			}

			/// <summary>check_res(target, 1, 0x80) == 0: the resistance byte's undead bit (UWDamageTypes.IsUndead).</summary>
			private bool fIsUndead(UWObject pOObject)
			{
				UWCommonObjectProperties lOProperties = mIHost.World.Data != null ? mIHost.World.Data.CommonObjectProperties : null;

				return lOProperties != null && lOProperties.TryGet(pOObject.ID, out UWCommonObjectProperties.Entry lOEntry)
					&& UWDamageTypes.IsUndead(lOEntry.Resistances);
			}
		}
	}
}
