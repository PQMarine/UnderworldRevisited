using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The class-specific tables from "objects.dat" (uw-formats.txt 6.3). Until now only
	/// the light source table was read from this file (see UWObjectProperties); this
	/// adds the rest, above all the critter table, which contains practically everything
	/// for combat and enemy behaviour.
	///
	/// File layout:
	///   0x0002  Melee weapons    16 entries of 8 bytes    (object id 0x00-0x0f)
	///   0x0082  Ranged weapons   16 entries of 3 bytes    (object id 0x10-0x1f)
	///   0x00b2  Armour           32 entries of 4 bytes    (object id 0x20-0x3f)
	///   0x0132  Critters         64 entries of 48 bytes   (object id 0x40-0x7f)
	///   0x0d32  Containers       16 entries of 3 bytes
	///   0x0d62  Light sources    16 entries of 2 bytes    (already in UWObjectProperties)
	///   0x0d82  Food             16 entries of 1 byte
	///   0x0d92  Jewelry          16 entries of 1 byte
	///   0x0da2  Animations       16 entries of 4 bytes
	///
	/// The tables are sorted by object id and each start at a fixed id -
	/// so lookups use the raw object id, without the project's usual +1 (that
	/// only applies to strings).
	/// </summary>
	public class UWObjectClassProperties
	{
		public struct MeleeWeapon
		{
			public sbyte SlashDamage;

			public sbyte BashDamage;

			public sbyte StabDamage;

			/// <summary>Minimum charge of an attack.</summary>
			public byte MinCharge;

			/// <summary>How fast the charge builds up.</summary>
			public byte AttackSpeed;

			public byte MaxCharge;

			/// <summary>3 sword, 4 axe, 5 mace, 6 unarmed.</summary>
			public byte SkillType;

			/// <summary>0xFF means indestructible.</summary>
			public byte Durability;
		}

		public struct RangedWeapon
		{
			public int Raw;

			/// <summary>Bits 9-15 of the first word, plus 0x10 - the required ammunition.</summary>
			public int AmmunitionObjectId;

			public byte Durability;
		}

		public struct Wearable
		{
			public byte Protection;

			public byte Durability;

			/// <summary>0 not on the paperdoll (shields only), 1 body armour, 3 leggings,
			/// 4 gloves, 5 boots, 8 headgear, 9 ring.</summary>
			public byte PaperdollSlot;
		}

		public struct Critter
		{
			/// <summary>
			/// Row bytes 0-3: the armour of the four body parts (body, hands, legs, head - the
			/// numbering of UWArmourProtection.PickBodyPart), damage subtracted from a blow on
			/// that part. UW.EXE reads row[part % 4] and takes byte 0 wherever the byte is 0xFF
			/// (AttackerAppliesFinalDamage_seg022_8A5, labels 990-9DB) - see ArmourOfPart. In the
			/// data 25 of the 64 rows hold one value and three 0xFF, the rest four values.
			///
			/// Until 2026-09-20 byte 0 was read as "Level" and bytes 1-3 as the armour, which
			/// gave every creature its hands' armour for every part (deviation 49).
			/// </summary>
			public byte Armour0;

			public byte Armour1;

			public byte Armour2;

			public byte Armour3;

			public byte Vitality;

			/// <summary>Strength of the critter. A fifth of it goes into its damage.</summary>
			public byte Power;

			/// <summary>Goes into the hit calculation.</summary>
			public byte Dexterity;

			public byte Intelligence;

			/// <summary>Bits 0-3: blood splatter (0 dust, 8 red blood). Bits 4-7: remains
			/// (0 nothing, 2 rotworm corpse, 4 rubble, 6 wood chips, 8 bones, 10 green
			/// blood pool, 12 red blood pool, 14 red blood pool giant spider).</summary>
			public byte BloodAndRemains;

			/// <summary>The lower three bits of the same byte: which death sound. In
			/// uw1 only the value 1 plays one (the reference: critterobjectdat.deathsound and
			/// damage.cs).</summary>
			public byte DeathSound;

			/// <summary>0xFF: never attacks.</summary>
			public byte Passiveness;

			/// <summary>Offset 0x0B, bits 0-6: the speed while WANDERING - in the original a
			/// different value from the speed 0x0C with which a critter heads for a target
			/// (the reference: NPCWanderUpdate takes unk_b_0_7, NPC_Goto takes speed). For the rat
			/// 4 versus 6, for the vampire bat 8 versus 12 - so wandering is about two
			/// thirds as fast. What bit 7 means is open.</summary>
			public byte WanderSpeed;

			/// <summary>0 immobile, 12 maximum (e.g. vampire bat).</summary>
			public byte MovementSpeed;

			/// <summary>Trade (the reference: critterObjectDat.TradeAppraisal, offset 0x0D upper
			/// nibble): how well the critter estimates the value of goods.</summary>
			public byte TradeAppraisal;

			/// <summary>Offset 0x0D lower nibble: what UW.EXE gives a conversation as npc_level
			/// (ImportConversationVariables_ovr103_0) - from the table, not from the
			/// object. The demand's score reads it too (UWConversationTrade.fDoDemand); until
			/// 2026-09-25 that took row byte 0, the body armour, under the name "Level".</summary>
			public byte ConversationLevel;

			/// <summary>Offset 0x0E lower nibble: from which rating on an offer
			/// is accepted (see UWConversationTrade).</summary>
			public byte TradeThreshold;

			/// <summary>Offset 0x0E upper nibble: how many bad offers the critter
			/// tolerates.</summary>
			public byte TradePatience;

			/// <summary>Initial poison damage in the first minute, then reduced by 1
			/// per minute.</summary>
			public byte PoisonDamage;

			/// <summary>0x00 ethereal, 0x01 humanoid, 0x02 flying, 0x03 swimming,
			/// 0x04 crawling, 0x05 creeping, 0x11 golem, 0x51 human.</summary>
			public byte Category;

			/// <summary>Base hit chance, goes halved into the hit roll.</summary>
			public byte AttackPower;

			public byte DefensePower;

			/// <summary>Base hit chance of the first attack - the same as
			/// Attacks[0].ToHit, just under the old name.</summary>
			public byte Weapon;

			/// <summary>Damage of the first attack - the same as Attacks[0].Damage.
			/// </summary>
			public byte Damage;

			/// <summary>
			/// The critter's three attacks - see CritterAttack. Always three entries,
			/// even if only one is used.
			/// </summary>
			public CritterAttack[] Attacks;

			/// <summary>How likely anything valuable drops at all, against a
			/// roll of zero to sixteen (offset 0x26, upper nibble).</summary>
			public byte ValuableProbability;

			/// <summary>Goes into the QUANTITY of the valuable item - see
			/// UWCritterLoot (offset 0x26, lower nibble).</summary>
			public byte ValuableMultiple;

			/// <summary>Against a roll of zero to sixteen (offset 0x27, lower
			/// nibble).</summary>
			public byte FoodProbability;

			/// <summary>Which food, counted from object 0xB0 (offset 0x27, upper
			/// nibble).</summary>
			public byte FoodItemIndex;

			/// <summary>Up to two equipment items. They ALWAYS drop if the entry
			/// is enabled - they have no probability of their own.</summary>
			public int[] WeaponLoot;

			/// <summary>Up to two further items, each with its own
			/// probability.</summary>
			public CritterLootEntry[] OtherLoot;

			/// <summary>
			/// Which ammunition the critter shoots - counted from object 0x10, i.e.
			/// arrow, bolt, sling stone. It is bits 1 to 4 of byte 0x20 (the lower nibble
			/// of the value shifted past the enable bit), the same byte the first loot item
			/// also comes from: the critter throws what it leaves behind.
			/// </summary>
			public byte AmmunitionIndex;

			/// <summary>The critter's two spells (offset 0x2A and 0x2B) and the third
			/// from 0x2C, the only signed one: minus one means none.
			/// They are numbers in the rune spell list.</summary>
			public byte Spell1;

			public byte Spell2;

			public sbyte Spell3;

			/// <summary>How strongly the critter casts (offset 0x2D, bits 1 to 7). Zero
			/// means: it does not cast at all and instead attacks with a ranged weapon,
			/// if it has one.</summary>
			public byte SpellPower;

			/// <summary>Bit zero of the same byte: the critter uses its spell list.
			/// </summary>
			public bool IsCaster;

			/// <summary>
			/// How decisive the critter is (offset 0x1F, upper nibble).
			///
			/// The reference rolls against it on every decision: zero to sixteen against
			/// this value, and only on a hit does it do something. The reference itself calls the
			/// field uncertain ("likelihood of giving up on a target"); but the data say
			/// clearly what it is - the values track the speed. The bat
			/// has twelve, the goblin eight, the slug and the skeleton three. Whoever is sluggish
			/// deliberates longer.
			/// </summary>
			public byte Initiative;

			/// <summary>Lower nibble of 0x1F: how restless the critter is while wandering
			/// - the reference uses it to roll between standing and walking and for
			/// direction changes (npcai.NPCWanderUpdate).</summary>
			public byte Restlessness;

			/// <summary>Upper nibble of 0x1C: how far it strays from its home while
			/// wandering, in tiles (the reference: TravelRange).</summary>
			public byte TravelRange;

			/// <summary>Lower nibble of 0x1C: the morale - the higher, the later the
			/// retreat (the reference: maybemorale, ShouldNPCWithdraw).</summary>
			public byte Morale;

			/// <summary>Lower nibble of 0x1E: hearing range, offset against the player's
			/// stealth (the reference: noisedetectionrange).</summary>
			public byte NoiseRange;

			/// <summary>Upper nibble of 0x1E: sight range, offset against the player's
			/// visibility (the reference: sightdetectionrange).</summary>
			public byte SightRange;

			/// <summary>
			/// Byte 0x09, and it serves TWO purposes at once, which is why it used to sit in this
			/// struct twice (as GeneralType and GenericNameIndex, both reading byte 9; merged
			/// 2026-09-16).
			///
			/// It is the KIND: members of the same kind within hearing range turn hostile when one
			/// of them is attacked, and the owner field of an item names exactly this number, which
			/// is how a theft finds who to anger (the reference: generaltype).
			///
			/// And it is the GENERIC NAME: our string block 1 holds it at 371 plus the value -
			/// 371 is "a denizen of the area", 377 the green goblin, which matches the kind numbers
			/// of the critter table entry for entry (checked 2026-09-16, see
			/// Interaction.FirstOwnerNameMessage).
			/// </summary>
			public byte GeneralType;

			/// <summary>The same byte under its other name - see GeneralType.</summary>
			public byte GenericNameIndex => GeneralType;

			/// <summary>Bit 1 of 0x0A: does not take part in the group alarm (the reference:
			/// unkPassivenessProperty).</summary>
			public bool IsPassive;

			public int ExperienceWhenKilled;

			/// <summary>
			/// The whole 48-byte row as it is in OBJECTS.DAT. The creature AI (UWCritterBrain)
			/// reads a dozen bytes the named fields above do not carry, and reads them by the
			/// offsets the disassembly uses (Docs/AI/creature-ai.md section 2.3) - so they
			/// are exposed as raw row access with named accessors below instead of a field each.
			/// </summary>
			public byte[] Row;

			/// <summary>A byte of the row, 0 for a row that was never loaded.</summary>
			public int RowByte(int piOffset)
			{
				return Row == null || piOffset < 0 || piOffset >= Row.Length ? 0 : Row[piOffset];
			}

			/// <summary>Armour of body part p: row byte [p % 4] (a missile's part 4..7 lands on
			/// the same bytes); a byte of 0xFF means "use byte 0" - the original tests the VALUE
			/// it read, not byte 3 in particular (AttackerAppliesFinalDamage_seg022_8A5, labels
			/// 9B9-9DB: cmp ax, 0FFh on the fetched byte). In the data the 0xFF always fills
			/// bytes 1-3 together, so parts 1-3 all fall back to byte 0 there.</summary>
			public int ArmourOfPart(int piPart)
			{
				int liArmour = RowByte(piPart & 3);

				return liArmour == 0xFF ? RowByte(0) : liArmour;
			}

			/// <summary>Table byte 0x0A bit 7: moves with the flier handler (NPCInitialProcessing
			/// 52768) and strikes without the floor test.</summary>
			public bool IsFlier => (RowByte(0x0A) & 0x80) != 0;

			/// <summary>Table byte 0x0A bit 6: moves with the swimmer handler (52782).</summary>
			public bool IsSwimmer => (RowByte(0x0A) & 0x40) != 0;

			/// <summary>Table byte 0x0A bit 5: may climb more than one height level on a path
			/// (TraverseMultipleTiles_seg006_1477_6F6, reference reading).</summary>
			public bool CanClimb => (RowByte(0x0A) & 0x20) != 0;

			/// <summary>Table byte 0x0A bits 2-4: corpse object 0xC0 + n at death, 0 none
			/// (DropNPCRemains_seg006_5, 41019-41024).</summary>
			public int CorpseIndex => (RowByte(0x0A) >> 2) & 7;

			/// <summary>Table byte 8 bits 5-7: blood object 0xD8 + n at death (40872).</summary>
			public int BloodIndex => (RowByte(8) >> 5) & 7;

			/// <summary>Table byte 8 bits 3-4 nonzero: a damaging hit shows a blood splat, zero an
			/// impact flash (AttackerAppliesFinalDamage, label B49-C17).</summary>
			public bool BleedsOnHit => ((RowByte(8) >> 3) & 3) != 0;

			/// <summary>Table byte 0x10 low nibble: which footstep sounds the walk plays
			/// (NPCBehaviours_seg007_1798_2C4A 53455).</summary>
			public int FootstepCategory => RowByte(0x10) & 0xF;

			/// <summary>Table byte 0x10 high nibble: impact sound category for creature against
			/// creature (CombatMissImpactSound_seg022_C29).</summary>
			public int ImpactCategory => (RowByte(0x10) >> 4) & 0xF;

			/// <summary>Table byte 0x11 as a SIGNED byte: half of it goes into the attack score
			/// (NPCExecuteAttack_seg022_15DE, label 1656). AttackPower carries the same byte
			/// unsigned.</summary>
			public int AttackScoreBase => (sbyte)RowByte(0x11);

			/// <summary>Table byte 0x2E: the door skill - nonzero uses the door, then picks the
			/// lock (NPCTryToOpenDoor_seg006_1477_3123, 47474).</summary>
			public int DoorSkill => RowByte(0x2E);

			/// <summary>Table byte 0x20 bits 5-7; 1 means the creature has a missile weapon
			/// (NPC_Goal5_Attack 49030, NPCGoal9 50370).</summary>
			public int RangedType => (RowByte(0x20) >> 5) & 7;

			public bool HasMissileWeapon => RangedType == 1;

			/// <summary>Table byte 0x1D low nibble: how loud this kind is AS A TARGET of another
			/// creature's search (SearchForGoalTarget_seg007_1798_1CEB 51704). The player's row
			/// 63 is rewritten at runtime, see ICritterHost.</summary>
			public int LoudnessAsTarget => RowByte(0x1D) & 0xF;

			/// <summary>Table byte 0x1D high nibble: how visible this kind is as a target (51758).</summary>
			public int VisibilityAsTarget => (RowByte(0x1D) >> 4) & 0xF;

			/// <summary>Table byte 0x14: also the bash damage die against a door
			/// (NPCTryToOpenDoor 47570) - the damage byte of attack 0.</summary>
			public int DoorBashDie => RowByte(0x14);

			/// <summary>Table byte 0x29 + slot: the spell cast at animation 0x0D frame 4, slot
			/// 1..3 from byte 0x19 bits 2-3 (NPCInitialProcessing label 2AAD, 53276). 0xFF is
			/// none.</summary>
			public int SpellOfSlot(int piSlot)
			{
				return RowByte(0x29 + (piSlot & 3));
			}
		}

		/// <summary>
		/// One of a critter's three attacks.
		///
		/// The table lists them from offset 0x13 as three bytes per attack: hit chance,
		/// damage, frequency. Which of them comes is rolled anew on every
		/// strike - and it also determines the ANIMATION: attack zero is the
		/// bash, one the slash, two the stab (the reference: npcai sets npc_animation to
		/// attack number plus one, and our animation slots have exactly this
		/// order too, see UWCritterAnimations.SlotAttackBash).
		///
		/// THE FREQUENCIES ARE PERCENTAGES. This is written down nowhere, but follows
		/// from the data itself: for all 59 critters with attacks the three
		/// values add up to exactly one hundred (recalculated 2026-09-10). A value of zero means
		/// this attack does not occur - forty-three critters have more than one.
		/// </summary>
		public struct CritterAttack
		{
			public byte ToHit;

			public byte Damage;

			/// <summary>How often this attack comes up, in percent.</summary>
			public byte Probability;
		}

		/// <summary>A loot item with its own probability.</summary>
		public struct CritterLootEntry
		{
			public int ObjectId;

			/// <summary>Against a roll of zero to sixteen: less means it drops.
			/// Zero means never.</summary>
			public int Probability;
		}

		/// <summary>
		/// A container entry. uw-formats.txt gives only position and size for this table, but
		/// the three bytes read cleanly (decoded 2026-09-18, the reference agrees):
		/// byte 0 is the capacity in TENTHS of stones, bytes 1-2 are a 16-bit mask of what
		/// may go in.
		///
		/// The dumped values speak for themselves - pouch 2.0 stones, map case 3.0, bowl 5.0,
		/// sack/box/gold coffer 12.5, pack 25.0, urn 20.0; and exactly the four
		/// restricted containers carry a mask instead of 0xFFFF: map case 514 (scrolls),
		/// quiver 513 (ammunition), bowl 515 (edibles), rune bag 512 (runes).
		/// Quiver and rune bag have capacity 0, which the original reads as NO LIMIT (see
		/// UWContainerCapacity; the old reading "arrows weigh nothing" was wrong - arrows have a
		/// weight).
		/// </summary>
		public struct Container
		{
			/// <summary>Capacity in tenths of stones (byte 0). 0 for quiver and rune bag,
			/// whose contents weigh nothing anyway.</summary>
			public int CapacityTenthStones;

			/// <summary>
			/// What may go in (bytes 1-2 as a word):
			/// 0xFFFF everything, from 512 on a type class (512 runes, 513 ammunition,
			/// 514 scrolls and books, 515 edibles, 516 keys - the last one is unused in uw1),
			/// below 512 it would be one single object id, which no container uses.
			/// </summary>
			public int ObjectMask;
		}

		/// <summary>ObjectMask: no restriction.</summary>
		public const int ContainerMaskAll = 0xFFFF;

		/// <summary>From this value on ObjectMask names a type class, not an object id.</summary>
		public const int ContainerMaskFirstClass = 512;

		/// <summary>
		/// An entry of the animation table. The field meanings are not in
		/// uw-formats.txt; they were read off the data itself (2026-08-30): all
		/// used entries carry the same value 33 in byte 0, and the pairs of byte 2
		/// and byte 3 yield contiguous, non-overlapping ranges in ANIMO.GR
		/// (64 frames) exactly when read as start frame and frame count -
		/// the highest used range is 50+4. The fountain (object 457) thus gets
		/// frames 5 to 8, which fits four water levels.
		/// </summary>
		public struct AnimationObject
		{
			/// <summary>33 for all used entries; unused entries are 0.</summary>
			public byte Flags;

			public byte Byte1;

			/// <summary>First frame in ANIMO.GR.</summary>
			public byte StartFrame;

			/// <summary>Number of frames from StartFrame on.</summary>
			public byte FrameCount;
		}

		private const int MeleeOffset = 0x0002;

		private const int RangedOffset = 0x0082;

		private const int WearableOffset = 0x00B2;

		private const int CritterOffset = 0x0132;

		/// <summary>This many attacks each critter has.</summary>
		public const int CritterAttackCount = 3;

		/// <summary>Where in the critter entry the three attacks are.</summary>
		private const int CritterAttackOffset = 0x13;

		/// <summary>This many weapon and other-item slots each critter has.</summary>
		public const int CritterLootSlots = 2;

		private const int ContainerOffset = 0x0D32;

		private const int FoodOffset = 0x0D82;

		private const int JewelryOffset = 0x0D92;

		private const int AnimationOffset = UWObjectProperties.AnimationOffset;

		/// <summary>First object id of the respective table.</summary>
		public const int MeleeFirstId = 0x00;

		public const int RangedFirstId = 0x10;

		public const int WearableFirstId = 0x20;

		public const int CritterFirstId = 0x40;

		/// <summary>Containers start at 0x80 - object 128 is "a_sack", and the
		/// light sources behind them at 0x90 have long been established via
		/// UWObjectProperties.</summary>
		public const int ContainerFirstId = 0x80;

		/// <summary>CAUTION, the tables are NOT strictly in object id order: the
		/// food table comes before the jewelry table in the file, while for the object ids it is
		/// the other way round. Proven by the values themselves - the table at 0x0d82 yields
		/// a sensible nutrition ladder against the id range 0xB0 (meat 64, fish 48,
		/// corn 25, bread 16, cheese 12, apple 6, popcorn 2), but against 0xA0 nutrition values
		/// for coins and gems. A first attempt with consecutive ranges was
		/// therefore wrong.</summary>
		public const int FoodFirstId = 0xB0;

		public const int JewelryFirstId = 0xA0;

		/// <summary>The animation table belongs to objects 0x01C0 to 0x01CF
		/// (category ExplosionsSplatsFountainSilverTreeMovingThings), not to 0xC0 - the
		/// first approach read it against 0xC0 and thereby assigned the frame ranges to plants and
		/// bones that are not animated at all in the original. Proven by the values: the
		/// ranges point into ANIMO.GR, and ANIMO.GR is the graphics file of exactly these
		/// sixteen moving objects.</summary>
		public const int AnimationFirstId = 0x01C0;

		private readonly MeleeWeapon[] mOMelee = new MeleeWeapon[16];

		private readonly RangedWeapon[] mORanged = new RangedWeapon[16];

		private readonly Wearable[] mOWearables = new Wearable[32];

		private readonly Critter[] mOCritters = new Critter[64];

		private readonly Container[] mOContainers = new Container[16];

		/// <summary>Nutrition value per food item, one byte.</summary>
		private readonly byte[] myFoodNutrition = new byte[16];

		/// <summary>One byte per jewelry item; the docs do not give the meaning.</summary>
		private readonly byte[] myJewelry = new byte[16];

		private readonly AnimationObject[] mOAnimations = new AnimationObject[16];

		public bool IsLoaded { get; private set; }

		public UWObjectClassProperties(string psDataPath)
		{
			string lsFile = Path.Combine(psDataPath, "OBJECTS.DAT");

			if (!File.Exists(lsFile))
				return;

			byte[] lyData = File.ReadAllBytes(lsFile);

			if (lyData.Length < AnimationOffset + (16 * 4))
				return;

			fLoadMelee(lyData);
			fLoadRanged(lyData);
			fLoadWearables(lyData);
			fLoadCritters(lyData);
			fLoadRemainingTables(lyData);

			IsLoaded = true;
		}

		/// <summary>Melee weapon for an object id (0x00-0x0f).</summary>
		public bool TryGetMeleeWeapon(int piObjectId, out MeleeWeapon pOWeapon)
		{
			int liIndex = piObjectId - MeleeFirstId;

			if (!IsLoaded || liIndex < 0 || liIndex >= mOMelee.Length)
			{
				pOWeapon = default;
				return false;
			}

			pOWeapon = mOMelee[liIndex];
			return true;
		}

		/// <summary>Ranged weapon for an object id (0x10-0x1f).</summary>
		public bool TryGetRangedWeapon(int piObjectId, out RangedWeapon pOWeapon)
		{
			int liIndex = piObjectId - RangedFirstId;

			if (!IsLoaded || liIndex < 0 || liIndex >= mORanged.Length)
			{
				pOWeapon = default;
				return false;
			}

			pOWeapon = mORanged[liIndex];
			return true;
		}

		/// <summary>Armour piece for an object id (0x20-0x3f).</summary>
		public bool TryGetWearable(int piObjectId, out Wearable pOWearable)
		{
			int liIndex = piObjectId - WearableFirstId;

			if (!IsLoaded || liIndex < 0 || liIndex >= mOWearables.Length)
			{
				pOWearable = default;
				return false;
			}

			pOWearable = mOWearables[liIndex];
			return true;
		}

		/// <summary>Critter for an object id (0x40-0x7f).</summary>
		public bool TryGetCritter(int piObjectId, out Critter pOCritter)
		{
			int liIndex = piObjectId - CritterFirstId;

			if (!IsLoaded || liIndex < 0 || liIndex >= mOCritters.Length)
			{
				pOCritter = default;
				return false;
			}

			pOCritter = mOCritters[liIndex];
			return true;
		}

		/// <summary>
		/// Nutrition value of a food item (object id 0xB0-0xBF).
		///
		/// THE NUMBER IS SIGNED, and that answers the old open question.
		/// This used to say drinks carry "values from 129 to 255, which cannot be nutrition
		/// values - what they mean instead is open". Read as a signed byte
		/// they form a consistent picture (the reference: foodObjectDat.nutrition explicitly
		/// reads sbyte, "signed byte to get negative alcohol values"):
		///
		///   Meat 64, fish 48, corn 25, bread 16 and 12, cheese 12, apple 6, tuber leaves
		///   6, popcorn 2, mushroom 0 - all positive, that is the nutrition value.
		///
		///   Water -1, ale -3, port -8 - the magnitude is the DRUNKENNESS the
		///   drink causes (see UWCharacter.Drink).
		///
		///   Red and green elixir -127; those go their own way as potions.
		///   Bottle of wine 0 - in the original you cannot open it at all.
		/// </summary>
		public int GetFoodNutrition(int piObjectId)
		{
			int liIndex = piObjectId - FoodFirstId;

			return IsLoaded && liIndex >= 0 && liIndex < myFoodNutrition.Length
				? (sbyte)myFoodNutrition[liIndex] : 0;
		}

		public int GetJewelryValue(int piObjectId)
		{
			int liIndex = piObjectId - JewelryFirstId;

			return IsLoaded && liIndex >= 0 && liIndex < myJewelry.Length ? myJewelry[liIndex] : 0;
		}

		public bool TryGetContainer(int piObjectId, out Container pOContainer)
		{
			int liIndex = piObjectId - ContainerFirstId;

			if (!IsLoaded || liIndex < 0 || liIndex >= mOContainers.Length)
			{
				pOContainer = default;
				return false;
			}

			pOContainer = mOContainers[liIndex];
			return true;
		}

		public bool TryGetAnimationObject(int piObjectId, out AnimationObject pOAnimation)
		{
			int liIndex = piObjectId - AnimationFirstId;

			if (!IsLoaded || liIndex < 0 || liIndex >= mOAnimations.Length)
			{
				pOAnimation = default;
				return false;
			}

			pOAnimation = mOAnimations[liIndex];
			return true;
		}

		/// <summary>Containers, food, jewelry and animation objects. For these four
		/// tables uw-formats.txt only gives position and size, no field meanings - except
		/// for the nutrition value, which follows from the table name. The container fields
		/// were read off the data (see Container), the jewelry byte is still unknown.</summary>
		private void fLoadRemainingTables(byte[] pyData)
		{
			for (int liIndex = 0; liIndex < mOContainers.Length; liIndex++)
			{
				int liOffset = ContainerOffset + (liIndex * 3);

				if (liOffset + 2 >= pyData.Length)
					break;

				mOContainers[liIndex] = new Container
				{
					CapacityTenthStones = pyData[liOffset],
					ObjectMask = pyData[liOffset + 1] | (pyData[liOffset + 2] << 8)
				};
			}

			for (int liIndex = 0; liIndex < myFoodNutrition.Length; liIndex++)
			{
				if (FoodOffset + liIndex < pyData.Length)
					myFoodNutrition[liIndex] = pyData[FoodOffset + liIndex];
			}

			for (int liIndex = 0; liIndex < myJewelry.Length; liIndex++)
			{
				if (JewelryOffset + liIndex < pyData.Length)
					myJewelry[liIndex] = pyData[JewelryOffset + liIndex];
			}

			for (int liIndex = 0; liIndex < mOAnimations.Length; liIndex++)
			{
				int liOffset = AnimationOffset + (liIndex * 4);

				if (liOffset + 3 >= pyData.Length)
					break;

				mOAnimations[liIndex] = new AnimationObject
				{
					Flags = pyData[liOffset],
					Byte1 = pyData[liOffset + 1],
					StartFrame = pyData[liOffset + 2],
					FrameCount = pyData[liOffset + 3]
				};
			}
		}

		private void fLoadMelee(byte[] pyData)
		{
			for (int liIndex = 0; liIndex < mOMelee.Length; liIndex++)
			{
				int liOffset = MeleeOffset + (liIndex * 8);

				mOMelee[liIndex] = new MeleeWeapon
				{
					SlashDamage = (sbyte)pyData[liOffset],
					BashDamage = (sbyte)pyData[liOffset + 1],
					StabDamage = (sbyte)pyData[liOffset + 2],
					MinCharge = pyData[liOffset + 3],
					AttackSpeed = pyData[liOffset + 4],
					MaxCharge = pyData[liOffset + 5],
					SkillType = pyData[liOffset + 6],
					Durability = pyData[liOffset + 7]
				};
			}
		}

		private void fLoadRanged(byte[] pyData)
		{
			for (int liIndex = 0; liIndex < mORanged.Length; liIndex++)
			{
				int liOffset = RangedOffset + (liIndex * 3);
				int liRaw = pyData[liOffset] | (pyData[liOffset + 1] << 8);

				mORanged[liIndex] = new RangedWeapon
				{
					Raw = liRaw,
					AmmunitionObjectId = (liRaw >> 9) + 0x10,
					Durability = pyData[liOffset + 2]
				};
			}
		}

		private void fLoadWearables(byte[] pyData)
		{
			for (int liIndex = 0; liIndex < mOWearables.Length; liIndex++)
			{
				int liOffset = WearableOffset + (liIndex * 4);

				mOWearables[liIndex] = new Wearable
				{
					Protection = pyData[liOffset],
					Durability = pyData[liOffset + 1],
					PaperdollSlot = pyData[liOffset + 3]
				};
			}
		}

		private void fLoadCritters(byte[] pyData)
		{
			for (int liIndex = 0; liIndex < mOCritters.Length; liIndex++)
			{
				int liOffset = CritterOffset + (liIndex * 48);
				byte[] lyRow = new byte[48];

				if (liOffset + 48 <= pyData.Length)
					System.Array.Copy(pyData, liOffset, lyRow, 0, 48);

				mOCritters[liIndex] = new Critter
				{
					Row = lyRow,
					Armour0 = pyData[liOffset],
					Armour1 = pyData[liOffset + 1],
					Armour2 = pyData[liOffset + 2],
					Armour3 = pyData[liOffset + 3],
					Vitality = pyData[liOffset + 4],
					Power = pyData[liOffset + 5],
					Dexterity = pyData[liOffset + 6],
					Intelligence = pyData[liOffset + 7],
					BloodAndRemains = pyData[liOffset + 8],
					DeathSound = (byte)(pyData[liOffset + 8] & 0x7),
					Passiveness = pyData[liOffset + 10],
					WanderSpeed = (byte)(pyData[liOffset + 11] & 0x7F),
					MovementSpeed = pyData[liOffset + 12],
					TradeAppraisal = (byte)((pyData[liOffset + 13] >> 4) & 0xF),
					ConversationLevel = (byte)(pyData[liOffset + 13] & 0xF),
					TradeThreshold = (byte)(pyData[liOffset + 14] & 0xF),
					TradePatience = (byte)((pyData[liOffset + 14] >> 4) & 0xF),
					PoisonDamage = pyData[liOffset + 15],
					Category = pyData[liOffset + 16],
					AttackPower = pyData[liOffset + 17],
					DefensePower = pyData[liOffset + 18],
					Weapon = pyData[liOffset + 19],
					Damage = pyData[liOffset + 20],
					Attacks = fReadCritterAttacks(pyData, liOffset),
					ValuableProbability = (byte)((pyData[liOffset + 0x26] >> 4) & 0xF),
					ValuableMultiple = (byte)(pyData[liOffset + 0x26] & 0xF),
					FoodProbability = (byte)(pyData[liOffset + 0x27] & 0xF),
					FoodItemIndex = (byte)((pyData[liOffset + 0x27] >> 4) & 0xF),
					WeaponLoot = fReadWeaponLoot(pyData, liOffset),
					OtherLoot = fReadOtherLoot(pyData, liOffset),
					AmmunitionIndex = (byte)((pyData[liOffset + 0x20] >> 1) & 0xF),
					Initiative = (byte)((pyData[liOffset + 0x1F] >> 4) & 0xF),
					Restlessness = (byte)(pyData[liOffset + 0x1F] & 0xF),
					TravelRange = (byte)((pyData[liOffset + 0x1C] >> 4) & 0xF),
					Morale = (byte)(pyData[liOffset + 0x1C] & 0xF),
					NoiseRange = (byte)(pyData[liOffset + 0x1E] & 0xF),
					SightRange = (byte)((pyData[liOffset + 0x1E] >> 4) & 0xF),
					GeneralType = pyData[liOffset + 9],
					IsPassive = ((pyData[liOffset + 10] >> 1) & 0x1) != 0,
					Spell1 = pyData[liOffset + 0x2A],
					Spell2 = pyData[liOffset + 0x2B],
					Spell3 = (sbyte)pyData[liOffset + 0x2C],
					SpellPower = (byte)((pyData[liOffset + 0x2D] >> 1) & 0x7F),
					IsCaster = (pyData[liOffset + 0x2D] & 0x1) != 0,
					ExperienceWhenKilled = pyData[liOffset + 40] | (pyData[liOffset + 41] << 8)
				};
			}
		}

		/// <summary>
		/// The two equipment items, one byte each from 0x20.
		///
		/// Bit zero enables the entry. The object number sits shifted in the
		/// upper seven bits and is reassembled from two pieces: the lower
		/// four bits are the number within the group, the two above them the group
		/// itself (weapons, armour). Checked against the result: the goblin thus gets cudgel
		/// and leather cap, the skeleton shortsword and leather cap, the fighter longsword
		/// and chain boots - all fitting (2026-09-10).
		/// </summary>
		private static int[] fReadWeaponLoot(byte[] pyData, int piCritterOffset)
		{
			int[] liLoot = new int[CritterLootSlots];

			for (int liAt = 0; liAt < liLoot.Length; liAt++)
			{
				byte lyRaw = pyData[piCritterOffset + 0x20 + liAt];

				if ((lyRaw & 0x1) == 0)
				{
					liLoot[liAt] = -1;
					continue;
				}

				int liValue = (lyRaw >> 1) & 0x7F;

				liLoot[liAt] = (((liValue >> 4) & 0x3) << 4) + (liValue & 0xF);
			}

			return liLoot;
		}

		/// <summary>The two further loot items, one word each from 0x22: lower nibble
		/// probability, above it the object number.</summary>
		private static UWObjectClassProperties.CritterLootEntry[] fReadOtherLoot(byte[] pyData,
			int piCritterOffset)
		{
			CritterLootEntry[] lOLoot = new CritterLootEntry[CritterLootSlots];

			for (int liAt = 0; liAt < lOLoot.Length; liAt++)
			{
				int liOffset = piCritterOffset + 0x22 + (liAt * 2);
				int liValue = pyData[liOffset] | (pyData[liOffset + 1] << 8);

				lOLoot[liAt] = new CritterLootEntry
				{
					Probability = liValue & 0xF,
					ObjectId = (liValue >> 4) & 0xFFF
				};
			}

			return lOLoot;
		}

		/// <summary>The three attacks of a critter, three bytes each from offset 0x13.</summary>
		private static CritterAttack[] fReadCritterAttacks(byte[] pyData, int piCritterOffset)
		{
			CritterAttack[] lOAttacks = new CritterAttack[CritterAttackCount];

			for (int liAt = 0; liAt < lOAttacks.Length; liAt++)
			{
				int liOffset = piCritterOffset + CritterAttackOffset + (liAt * 3);

				lOAttacks[liAt] = new CritterAttack
				{
					ToHit = pyData[liOffset],
					Damage = pyData[liOffset + 1],
					Probability = pyData[liOffset + 2]
				};
			}

			return lOAttacks;
		}
	}
}
