namespace UWDataImport.UWData
{
	/// <summary>Where a blow comes from - one per kind of damage site that goes through
	/// IUWWorld.Damage (cleanup step 3.3, 2026-10-17). It names the site for the log and the checks
	/// and says, for the curse and the backfire, that the amount is a number of dice.</summary>
	public enum UWBlowSource
	{
		/// <summary>The player's melee blow (Interaction.fApplyMeleeHit).</summary>
		PlayerBlow,

		/// <summary>A creature's blow on another creature, a door or an object in the way.</summary>
		CreatureBlow,

		/// <summary>A missile's hit (UWMissiles): an arrow, a bolt, a stone, a spell missile.</summary>
		Missile,

		/// <summary>Everything on a tile takes a blow of its own (UWTileBlast): a fireball's or a
		/// lightning bolt's explosion, Flame Wind, Sheet Lightning.</summary>
		Blast,

		/// <summary>A class 7 spell on a clicked creature (UWTargetSpell): Smite Undead, Poison.</summary>
		TargetSpell,

		/// <summary>A damage trap (UWTrapRules) on the player, a creature or a thing in
		/// flight.</summary>
		Trap,

		/// <summary>The player's Rune of Warding on a creature stepping on it.</summary>
		Ward,

		/// <summary>The lava under a creature.</summary>
		Lava,

		/// <summary>Drowning or lava while the player sleeps (UWSleepRules).</summary>
		Sleep,

		/// <summary>A class 9 curse on the player (the exploding book); the amount is the dice.</summary>
		Curse,

		/// <summary>A backfired spell on the player; the amount is the dice.</summary>
		Backfire
	}

	/// <summary>
	/// HOW A STRUCK CREATURE LEARNS WHO HIT IT - the three ways the sites told it until cleanup step
	/// 3.3 (2026-10-17), kept apart because they differ when the blow does not land (an
	/// indestructible creature raises no damage event, so an announcement stays pending).
	///
	/// DamageNPC (54563-54802) stores the attacker in byte 0x12 - one for the player, and FOR A
	/// PROJECTILE THE PROJECTILE'S OWN BYTE 0x12, which is its launcher. Only then can the damage
	/// reaction do its work: it ignores a hit that came from neither the player nor an ally of
	/// either side, and it makes the attacker its goal target (see UWCritterBrain.fDamageReaction).
	/// The kin alarm, the experience and the combat music follow from the same attacker.
	/// </summary>
	public enum UWBlowAttribution
	{
		/// <summary>Nobody tells the creature: its damage event charges whoever is pending - the
		/// player, unless an earlier announcement is still waiting. The player's blow and the
		/// target spells.</summary>
		Untold,

		/// <summary>The attacker is announced before the blow and holds until the next damage
		/// event takes it (UWCritter.AnnounceAttacker): a missile, a blast. Until 2026-09-22 a
		/// creature shot by another creature's arrow blamed the player.</summary>
		Announced,

		/// <summary>The attacker holds for this blow only and falls back to the player right
		/// after, whether the blow raised a damage event or not: a creature's blow, and damage
		/// nobody dealt (attacker UWBlow.Nobody).</summary>
		ThisBlowOnly
	}

	/// <summary>
	/// ONE BLOW, as every damage site hands it to IUWWorld.Damage (cleanup step 3.3, 2026-10-17;
	/// section 7.2 of Docs/CLEANUP-PLAN.md). Until then each site called UWDamageable.ApplyDamage or
	/// UWCharacter.ApplyDamage itself and told the creature its attacker in one of three ways - the
	/// missile and the blast through AnnounceAttacker, the creature's blow by setting the pending
	/// attacker around the call, the trap, the ward and the lava through ApplyDamageWithoutAttacker
	/// (section 4.3: "Damage to objects and creatures").
	///
	/// THE AMOUNT IS THE SITE'S OWN: every site still computes its damage as before - the dice of a
	/// blast, the armour and the easy halving of a missile on the player, the charge and the flank
	/// of a blow - and passes the result. The blow carries only what the landing needs: the amount
	/// (the dice for a curse or a backfire), the damage type, the attacker and how the creature
	/// learns of it. The formulas are unified later (UWAttackResolution, UWDamagePipeline).
	/// </summary>
	public readonly struct UWBlow
	{
		/// <summary>The attacker of damage nobody dealt: damage_item with no attacker (from 0,
		/// UnderworldExhumed AI.C damage_critter: who = 0).</summary>
		public const int Nobody = 0;

		/// <summary>No attacker given to a blast: the struck creatures are not told (see Blast).</summary>
		public const int NoBlastAttacker = -1;

		/// <summary>No creature target, for Blast on the player or an object.</summary>
		public const int NoCreature = -1;

		/// <summary>Hit points, or for a curse or a backfire the number of dice (RollsDice).</summary>
		public readonly int Amount;

		/// <summary>A UWDamageTypes value.</summary>
		public readonly int DamageType;

		/// <summary>Whom a struck creature holds responsible: a creature's index,
		/// UWCritterBrain.PlayerIndex, or Nobody. Meaningful under Announced and ThisBlowOnly.</summary>
		public readonly int Attacker;

		public readonly UWBlowAttribution Attribution;

		public readonly UWBlowSource Source;

		private UWBlow(int piAmount, int piDamageType, int piAttacker, UWBlowAttribution peAttribution, UWBlowSource peSource)
		{
			Amount = piAmount;
			DamageType = piDamageType;
			Attacker = piAttacker;
			Attribution = peAttribution;
			Source = peSource;
		}

		/// <summary>The amount is a number of dice: the curse's and the backfire's own roll with
		/// its floor of three hit points (UWPlayerVitals.ApplyCurse, ApplyBackfire).</summary>
		public bool RollsDice => Source == UWBlowSource.Curse || Source == UWBlowSource.Backfire;

		/// <summary>The player's melee blow. The player's own strike is bash damage, just like a
		/// creature's (reference: combat.AttackerAppliesFinalDamage with four). The creature is not
		/// told: its damage event charges the player by default.</summary>
		public static UWBlow PlayerBlow(int piDamage)
		{
			return new UWBlow(piDamage, UWDamageTypes.Physical, UWCritterBrain.PlayerIndex, UWBlowAttribution.Untold,
				UWBlowSource.PlayerBlow);
		}

		/// <summary>A creature's blow: DamageObject with type 4, charged to the attacking
		/// creature's index for this blow only - on a creature, a door it bashes or an object in
		/// the way (an object keeps no attacker).</summary>
		public static UWBlow CreatureBlow(int piDamage, int piAttacker)
		{
			return new UWBlow(piDamage, UWDamageTypes.Physical, piAttacker, UWBlowAttribution.ThisBlowOnly,
				UWBlowSource.CreatureBlow);
		}

		/// <summary>A missile's hit, its launcher announced (the player for his own and, as
		/// UWMissiles has always done, for a missile without a launching creature).</summary>
		public static UWBlow Missile(int piDamage, int piDamageType, int piAttacker)
		{
			return new UWBlow(piDamage, piDamageType, piAttacker, UWBlowAttribution.Announced, UWBlowSource.Missile);
		}

		/// <summary>
		/// One thing's blow of a blast. The attacker - a creature's index, the player's, or
		/// NoBlastAttacker - is announced to a struck creature unless there is none or it is the
		/// creature itself (a mage caught in its own fireball does not turn on itself); then the
		/// creature is not told at all (Untold). piCreatureIndex is the struck creature's index, or
		/// NoCreature for the player and an object.
		/// </summary>
		public static UWBlow Blast(int piDamage, int piDamageType, int piAttacker, int piCreatureIndex)
		{
			bool lbTells = piAttacker >= 0 && piAttacker != piCreatureIndex;

			return new UWBlow(piDamage, piDamageType, piAttacker,
				lbTells ? UWBlowAttribution.Announced : UWBlowAttribution.Untold, UWBlowSource.Blast);
		}

		/// <summary>A class 7 spell's damage on its target (Smite Undead, Poison); the creature is
		/// not told (L-review: "no announce, verify against the original").</summary>
		public static UWBlow TargetSpell(int piDamage, int piDamageType)
		{
			return new UWBlow(piDamage, piDamageType, UWCritterBrain.PlayerIndex, UWBlowAttribution.Untold,
				UWBlowSource.TargetSpell);
		}

		/// <summary>
		/// DAMAGE THAT NOBODY DEALT: damage_item with no attacker (from 0, UnderworldExhumed
		/// AI.C damage_critter: who = 0) - the lava under the creature, a damage trap (whack_thing,
		/// WORLDEV.C) and the player's Rune of Warding (TRAP_WARD, TRIGGER.C, which hands the damage
		/// to whack_thing). The tally grows and the creature may die, but no attacker is recorded,
		/// no kin alarm raised, no experience paid and no combat music started. Until 2026-10-15 the
		/// trap and the ward fell back to the player as the attacker, so a monster stepping on the
		/// rune turned on him with its kin and a kill paid experience (audit round 2, A7 / KR D3).
		///
		/// A damage trap's blow: type 4 on a creature or a thing in flight (DamageObject,
		/// as UWTrapRules has it), untyped on the player - the site passes the type.
		/// </summary>
		public static UWBlow Trap(int piDamage, int piDamageType)
		{
			return new UWBlow(piDamage, piDamageType, Nobody, UWBlowAttribution.ThisBlowOnly, UWBlowSource.Trap);
		}

		/// <summary>The Rune of Warding on a creature: type 4, nobody's (see Trap).</summary>
		public static UWBlow Ward(int piDamage)
		{
			return new UWBlow(piDamage, UWDamageTypes.Physical, Nobody, UWBlowAttribution.ThisBlowOnly, UWBlowSource.Ward);
		}

		/// <summary>The lava under a creature: plain fire, nobody's (see Trap).</summary>
		public static UWBlow Lava(int piDamage)
		{
			return new UWBlow(piDamage, UWDamageTypes.PlainFire, Nobody, UWBlowAttribution.ThisBlowOnly, UWBlowSource.Lava);
		}

		/// <summary>Drowning or lava while the player sleeps: untyped, as the sleep host always
		/// passed it (B5 of Docs/CLEANUP-PLAN.md, decision 1 open).</summary>
		public static UWBlow Sleep(int piDamage)
		{
			return new UWBlow(piDamage, UWDamageTypes.None, Nobody, UWBlowAttribution.Untold, UWBlowSource.Sleep);
		}

		/// <summary>A class 9 curse of piDice dice on the player, with its flash.</summary>
		public static UWBlow Curse(int piDice)
		{
			return new UWBlow(piDice, UWDamageTypes.None, Nobody, UWBlowAttribution.Untold, UWBlowSource.Curse);
		}

		/// <summary>A backfired spell of piDice dice on the player, with its flash.</summary>
		public static UWBlow Backfire(int piDice)
		{
			return new UWBlow(piDice, UWDamageTypes.None, Nobody, UWBlowAttribution.Untold, UWBlowSource.Backfire);
		}

		public override string ToString()
		{
			return Source + " " + Amount + " type " + DamageType + " by " + Attacker + " (" + Attribution + ")";
		}
	}

	/// <summary>What a blow strikes.</summary>
	public enum UWDamageTargetKind
	{
		Player,

		/// <summary>A creature, by its index in the level's master list.</summary>
		Creature,

		/// <summary>A door, a chest, a barrel - an object with hit points of its own.</summary>
		Object,

		/// <summary>A thing in flight, by its flight index (UWMotionHost): the damage trap's strike
		/// on a thrown object that set it off.</summary>
		ThingInFlight
	}

	/// <summary>
	/// THE TARGET OF A BLOW (cleanup step 3.3, 2026-10-17). Engine-free it is a kind and an index or
	/// an object's data; a site that already holds the struck body hands it along in Body, so the
	/// world need not look it up again (and finds the very body the site found - a frame post
	/// belongs to its door, a creature's collider to its creature). Body is the engine's own and
	/// opaque here: the Unity world puts the UWDamageable there.
	/// </summary>
	public readonly struct UWDamageTarget
	{
		public static readonly UWDamageTarget ThePlayer = new UWDamageTarget(UWDamageTargetKind.Player, UWCritterBrain.PlayerIndex,
			null, null);

		public readonly UWDamageTargetKind Kind;

		/// <summary>A creature's index in the master list, a thing in flight's flight index; the
		/// player's index for the player; 0 for an object.</summary>
		public readonly int Index;

		/// <summary>The struck object's data, when known.</summary>
		public readonly UWObject Object;

		/// <summary>The engine's own body of the target, when the site holds it; null otherwise.</summary>
		public readonly object Body;

		private UWDamageTarget(UWDamageTargetKind peKind, int piIndex, UWObject pOObject, object pOBody)
		{
			Kind = peKind;
			Index = piIndex;
			Object = pOObject;
			Body = pOBody;
		}

		public static UWDamageTarget Creature(int piIndex, object pOBody = null)
		{
			return new UWDamageTarget(UWDamageTargetKind.Creature, piIndex, null, pOBody);
		}

		public static UWDamageTarget ObjectOf(UWObject pOObject, object pOBody = null)
		{
			return new UWDamageTarget(UWDamageTargetKind.Object, 0, pOObject, pOBody);
		}

		public static UWDamageTarget InFlight(int piFlightIndex)
		{
			return new UWDamageTarget(UWDamageTargetKind.ThingInFlight, piFlightIndex, null, null);
		}

		public override string ToString()
		{
			return Kind == UWDamageTargetKind.Object ? "object" + (Object != null ? " " + Object.ID : "") : Kind + " " + Index;
		}
	}

	/// <summary>
	/// What a world offers the landing of a blow (UWBlowRouting): the bodies' own operations, each
	/// doing exactly what the sites did before - nothing decides here. The Unity world implements it
	/// over UWDamageable, UWCritter, UWCharacter and UWMissiles; the self-check's FakeWorld logs it.
	/// </summary>
	public interface IUWBlowBodies
	{
		/// <summary>The creature is there and not destroyed - every creature site asked this before
		/// its blow (or ApplyDamageWithoutAttacker did).</summary>
		bool CreatureTakesBlows(UWDamageTarget pOTarget);

		/// <summary>The creature's pending attacker, which its next damage event takes.</summary>
		void SetCreatureAttacker(UWDamageTarget pOTarget, int piAttacker);

		/// <summary>The hit points of a creature or an object take the damage (UWDamageable.
		/// ApplyDamage: resistances, quality class, a door's spike, the events).</summary>
		void DamageBody(UWDamageTarget pOTarget, int piDamage, int piDamageType);

		/// <summary>The player's hit points take the damage, less what his equipment and spells
		/// ward off of its type (UWCharacter.ApplyDamage).</summary>
		void DamagePlayer(int piDamage, int piDamageType);

		void CursePlayer(int piDice);

		void BackfirePlayer(int piDice);

		/// <summary>The damage trap's strike on a thing in flight (UWMissiles.StrikeThing).</summary>
		void StrikeThingInFlight(int piFlightIndex, int piDamage, int piDamageType);
	}

	/// <summary>
	/// WHERE A BLOW LANDS, the one routine behind IUWWorld.Damage (cleanup step 3.3, 2026-10-17):
	/// the player, a creature with its attacker told as the blow says, an object, a thing in flight.
	/// Engine-free so the self-check runs it in a fake world.
	/// </summary>
	public static class UWBlowRouting
	{
		public static void Land(IUWBlowBodies pIBodies, UWDamageTarget pOTarget, UWBlow pOBlow)
		{
			if (pIBodies == null)
				return;

			switch (pOTarget.Kind)
			{
				case UWDamageTargetKind.Player:
					if (pOBlow.Source == UWBlowSource.Curse)
						pIBodies.CursePlayer(pOBlow.Amount);
					else if (pOBlow.Source == UWBlowSource.Backfire)
						pIBodies.BackfirePlayer(pOBlow.Amount);
					else
						pIBodies.DamagePlayer(pOBlow.Amount, pOBlow.DamageType);

					return;

				case UWDamageTargetKind.Creature:
					// A curse or a backfire strikes the player only.
					if (pOBlow.RollsDice || !pIBodies.CreatureTakesBlows(pOTarget))
						return;

					if (pOBlow.Attribution == UWBlowAttribution.Untold)
					{
						pIBodies.DamageBody(pOTarget, pOBlow.Amount, pOBlow.DamageType);

						return;
					}

					pIBodies.SetCreatureAttacker(pOTarget, pOBlow.Attacker);
					pIBodies.DamageBody(pOTarget, pOBlow.Amount, pOBlow.DamageType);

					if (pOBlow.Attribution == UWBlowAttribution.ThisBlowOnly)
						pIBodies.SetCreatureAttacker(pOTarget, UWCritterBrain.PlayerIndex);

					return;

				case UWDamageTargetKind.Object:
					if (!pOBlow.RollsDice)
						pIBodies.DamageBody(pOTarget, pOBlow.Amount, pOBlow.DamageType);

					return;

				case UWDamageTargetKind.ThingInFlight:
					if (!pOBlow.RollsDice)
						pIBodies.StrikeThingInFlight(pOTarget.Index, pOBlow.Amount, pOBlow.DamageType);

					return;
			}
		}
	}
}
