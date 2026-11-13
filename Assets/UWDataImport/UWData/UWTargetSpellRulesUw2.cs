namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S TARGET SPELLS ON A CREATURE (UW2 plan G1 part D1, 2026-11-12; Underworld Exhumed uw2
	/// combat/SPELLS.C nail_1area, target_spells, area1_spells): class 7 minors 0 to 7. The player picks the target in the
	/// target mode (UWAttackUw2); the handler answers whether the spell took, and only then is the mana paid.
	///
	///   minor  spell          handler
	///   0      Bleeding       sp_bleed: Casting / 2 + 10 of type 4 through wound_foe, only to a creature that bleeds
	///                         ("That creature does not bleed." otherwise)
	///   1      Cause Fear     sp_fear: attitude 1 at once, then goal 6 unless resisted (hit_critter_goal)
	///   2      Smite Undead   sp_ward_undead: an undead creature takes 255 of type 3, a liche (race 0x17) half its hit
	///                         points; no picture. The floating skull comes with the world's hacks (G6).
	///   3      Charm          sp_charm: unless resisted the picture, and a creature whose whoami is '+' in string
	///                         0x15E (0x15F from whoami 0x8C) becomes friendly: goal 8, attitude 3; it leaves the pit fight.
	///   4      Poison         sp_poison: the picture and 5d4 of poison and magic (no rune spell; for items).
	///   5      Paralyze       sp_hold: a creature that is not undead is held, goal 15 for 16 + 0..15 * power ticks
	///                         (power Casting / 3 for the player), unless resisted; the brain counts it down.
	///   6      Smite Foe      sp_smite: Casting * 3 + 110 of type 4 through wound_foe, only to one that bleeds.
	///   7      Study Monster  (part D2)
	///
	/// The resistance (check_res(npc, 1, 3)) is the magic one: UWDamageTypes.Scale of one point of magic.
	/// </summary>
	public static class UWTargetSpellRulesUw2
	{
		public const int MajorClass = UWSpellCasting.TargetMajorClass;

		public const int Bleeding = 0;
		public const int CauseFear = 1;
		public const int SmiteUndead = 2;
		public const int Charm = 3;
		public const int Poison = 4;
		public const int Paralyze = 5;
		public const int SmiteFoe = 6;
		public const int StudyMonster = 7;

		/// <summary>The minors that take a creature (target_spells); from 8 on an object (obj_spells, part D2).</summary>
		public const int LastCreatureSpell = 7;

		/// <summary>"That creature does not bleed." (string 0x12B of block 1).</summary>
		public const int DoesNotBleedString = 0x12B + 1;

		/// <summary>put_effect's class 7, the spell sparkle (455).</summary>
		public const int SpellEffectId = UWObjectClassProperties.AnimationFirstId + 7;

		/// <summary>wound_foe's picture for these spells: put_effect(target, effect 0, ...), class 0.</summary>
		public const int WoundEffectId = UWObjectClassProperties.AnimationFirstId;

		/// <summary>sp_bleed's and sp_smite's damage type.</summary>
		public const int WoundDamageType = UWDamageTypes.Physical;

		public const int SmiteUndeadDamage = 0xFF;

		/// <summary>The liches' race (Creature.race 0x17): Smite Undead takes half their hit points.</summary>
		public const int LicheRace = 0x17;

		/// <summary>sp_charm's strings 0x15E and 0x15F of block 1, a character per whoami, '+' charmable.</summary>
		public const int CharmString = 0x15E + 1;

		public const int CharmSecondWhoami = 0x8C;

		public const int FleeGoal = 6;
		public const int FriendlyGoal = 8;
		public const int FriendlyAttitude = 3;
		public const int UpsetAttitude = 1;
		public const int HeldGoal = 15;

		/// <summary>sp_hold's time: 16 + (rand() &amp; 15) * power.</summary>
		public const int HoldBase = 0x10;

		/// <summary>sp_hold's power for a caster that is not the player.</summary>
		public const int HoldOtherPower = 8;

		/// <summary>D1: the creature spells but Study Monster.</summary>
		public static bool IsBuilt(int piMinor)
		{
			return piMinor >= Bleeding && piMinor <= SmiteFoe;
		}

		/// <summary>target_spells: the handler on the clicked thing; true when it took (the mana is paid then).</summary>
		public static bool Cast(int piMinor, UWObject pOTarget, IUWSpellHost pIHost)
		{
			if (pIHost == null || pIHost.World == null || pOTarget == null)
				return false;

			switch (piMinor)
			{
				case Bleeding:
					return fWound(pOTarget, pIHost, (fCasting(pIHost) / 2) + 10);

				case CauseFear:
					if (!fIsCreature(pOTarget))
						return false;

					pIHost.World.SetCreatureGoal(pOTarget, -1, -1, UpsetAttitude);

					return HitCritterGoal(pOTarget, pIHost, FleeGoal, -1, 1);

				case SmiteUndead:
					return fSmiteUndead(pOTarget, pIHost);

				case Charm:
					return fCharm(pOTarget, pIHost);

				case Poison:
					if (!fIsCreature(pOTarget))
						return false;

					pIHost.World.ShowEffectAtCreature(pOTarget, SpellEffectId, EffectSeconds);
					fDamage(pOTarget, pIHost, UWTargetSpellRules.RollPoisonDamage(), UWTargetSpellRules.PoisonDamageType);

					return true;

				case Paralyze:
					return fHold(pOTarget, pIHost);

				case SmiteFoe:
					return fWound(pOTarget, pIHost, (fCasting(pIHost) * 3) + 110);

				default:
					return false;
			}
		}

		/// <summary>How long the spell picture stays (the area spells' length).</summary>
		private const float EffectSeconds = 0.7f;

		/// <summary>sp_bleed and sp_smite: a creature that bleeds is wounded (wound_foe: the picture at two eighths of its
		/// height, the damage charged to the caster); "That creature does not bleed." otherwise, and it does not take.</summary>
		private static bool fWound(UWObject pOTarget, IUWSpellHost pIHost, int piDamage)
		{
			if (!fIsCreature(pOTarget))
				return false;

			UWObjectClassProperties lOClasses = pIHost.World.Data != null ? pIHost.World.Data.ObjectClassProperties : null;

			if (lOClasses == null || !lOClasses.TryGetCritter(pOTarget.ID, out UWObjectClassProperties.Critter lORow) || !lORow.BleedsOnHit)
			{
				pIHost.World.Presentation.AddGeneralMessage(DoesNotBleedString);

				return false;
			}

			pIHost.World.ShowEffectAtCreature(pOTarget, WoundEffectId, EffectSeconds, UWAreaSpellRules.WoundSplashEighths);
			fDamage(pOTarget, pIHost, piDamage, WoundDamageType);

			return true;
		}

		/// <summary>sp_ward_undead: 255 of type 3 to an undead creature, half its hit points to a liche; no picture.</summary>
		private static bool fSmiteUndead(UWObject pOTarget, IUWSpellHost pIHost)
		{
			if (!fIsCreature(pOTarget) || !fIsUndead(pOTarget, pIHost))
				return false;

			int liDamage = SmiteUndeadDamage;
			UWObjectClassProperties lOClasses = pIHost.World.Data != null ? pIHost.World.Data.ObjectClassProperties : null;

			if (lOClasses != null && lOClasses.TryGetCritter(pOTarget.ID, out UWObjectClassProperties.Critter lORow) && lORow.Race == LicheRace)
				liDamage = pIHost.World.CreatureHitPoints(pOTarget) / 2;

			fDamage(pOTarget, pIHost, liDamage, UWDamageTypes.Magic);

			return true;
		}

		/// <summary>sp_charm: always takes on a creature; unless resisted the picture, and by the whoami's character
		/// friendly (goal 8, attitude 3), out of the pit fight.</summary>
		private static bool fCharm(UWObject pOTarget, IUWSpellHost pIHost)
		{
			if (!fIsCreature(pOTarget))
				return false;

			if (!fPassesResistance(pOTarget, pIHost))
				return true;

			pIHost.World.ShowEffectAtCreature(pOTarget, SpellEffectId, EffectSeconds);

			int liWhoami = pOTarget is UWNpc lONpc ? lONpc.NPCwhoami : 0;
			int liString = CharmString;

			if (liWhoami >= CharmSecondWhoami)
			{
				liWhoami -= CharmSecondWhoami;
				liString++;
			}

			string lsFlags = UWText.Block(pIHost.World.Data, UWText.GeneralBlock, liString);

			if (liWhoami < lsFlags.Length && lsFlags[liWhoami] == '+')
			{
				pIHost.World.SetCreatureGoal(pOTarget, FriendlyGoal, 0, FriendlyAttitude);

				if (pIHost is IUWSpellHostUw2 lIPlayer && UWPlayerRecordUw2.IsInPits(lIPlayer.Record))
				{
					UWLevel lOLevel = UWSpellCasting.LevelOf(pIHost);

					if (lOLevel != null)
						UWPlayerRecordUw2.RemovePitOpponent(lIPlayer.Record, UWAreaWalk.MobileIndexOf(lOLevel, pOTarget));
				}
			}

			return true;
		}

		/// <summary>sp_hold: a creature that is not undead is held for 16 + (rand() &amp; 15) * power ticks unless resisted.</summary>
		private static bool fHold(UWObject pOTarget, IUWSpellHost pIHost)
		{
			int liPower = pIHost.HasPlayer ? fCasting(pIHost) / 3 : HoldOtherPower;
			int liTime = ((UWRandom.Next(0x8000) & 0xF) * liPower) + HoldBase;

			if (!fIsCreature(pOTarget) || fIsUndead(pOTarget, pIHost))
				return false;

			return HitCritterGoal(pOTarget, pIHost, HeldGoal, UpsetAttitude, liTime);
		}

		/// <summary>hit_critter_goal: unless the creature shrugs it off the picture, the goal and target and, if not -1,
		/// the attitude. Takes either way. Public for the tym rune (UWRuneRulesUw2), which holds a creature with it.</summary>
		public static bool HitCritterGoal(UWObject pOTarget, IUWSpellHost pIHost, int piGoal, int piAttitude, int piGTarg)
		{
			if (fPassesResistance(pOTarget, pIHost))
			{
				pIHost.World.ShowEffectAtCreature(pOTarget, SpellEffectId, EffectSeconds);
				pIHost.World.SetCreatureGoal(pOTarget, piGoal, piGTarg, piAttitude);
			}

			return true;
		}

		/// <summary>check_res(npc, 1, 3): one point of magic through the creature's resistance.</summary>
		private static bool fPassesResistance(UWObject pOTarget, IUWSpellHost pIHost)
		{
			UWCommonObjectProperties lOProperties = pIHost.World.Data != null ? pIHost.World.Data.CommonObjectProperties : null;

			if (lOProperties == null || !lOProperties.TryGet(pOTarget.ID, out UWCommonObjectProperties.Entry lOEntry))
				return true;

			return UWDamageTypes.Scale(lOEntry.Resistances, 1, UWDamageTypes.Magic) != 0;
		}

		private static bool fIsUndead(UWObject pOTarget, IUWSpellHost pIHost)
		{
			UWCommonObjectProperties lOProperties = pIHost.World.Data != null ? pIHost.World.Data.CommonObjectProperties : null;

			return lOProperties != null && lOProperties.TryGet(pOTarget.ID, out UWCommonObjectProperties.Entry lOEntry)
				&& UWDamageTypes.IsUndead(lOEntry.Resistances);
		}

		private static bool fIsCreature(UWObject pOObject)
		{
			return pOObject != null && UWObjectIds.Major(pOObject.ID) == UWObjectIds.CreatureMajorClass;
		}

		private static void fDamage(UWObject pOTarget, IUWSpellHost pIHost, int piDamage, int piType)
		{
			UWLevel lOLevel = UWSpellCasting.LevelOf(pIHost);

			if (lOLevel == null)
				return;

			pIHost.World.Damage(UWDamageTarget.Creature(UWAreaWalk.MobileIndexOf(lOLevel, pOTarget)),
				UWBlow.Blast(piDamage, piType, pIHost.Cast.AttackerIndex));
		}

		private static int fCasting(IUWSpellHost pIHost)
		{
			if (pIHost.HasPlayer)
				return pIHost.PlayerCastingSkill;

			return pIHost.World.Player != null && pIHost.World.Player.Vitals != null
				? pIHost.World.Player.Vitals.GetSkill(UWPlayerData.Skill.Casting) : 0;
		}
	}
}
