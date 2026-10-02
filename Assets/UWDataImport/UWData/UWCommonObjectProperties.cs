using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Properties that EVERY object has, from "comobj.dat" (uw-formats.txt 6.2). Until now
	/// this file was completely unread, although it supplies fundamentals: height and radius
	/// (collision), mass, value and - of particular interest for this project - a bit
	/// telling whether an object can be picked up.
	///
	/// Layout: two header bytes, then 11 bytes per object id. The count is
	/// (file length - 2) / 11, so 512 entries in uw1 - exactly the object id range.
	///
	/// Indexing is by the RAW object id, without the project's usual +1: that only applies when
	/// looking up strings, not to tables that are themselves sorted by object id.
	/// </summary>
	public class UWCommonObjectProperties
	{
		public struct Entry
		{
			/// <summary>According to the docs roughly half the pixel height of the object; 0 for objects
			/// that have no collision.</summary>
			public byte Height;

			/// <summary>Bits 0-2 of the mass word. 4 for bridge and anvil, 3 for doors,
			/// 2 for all creatures.</summary>
			public byte Radius;

			/// <summary>Mass in tenths of stones (bits 4-15 of the mass word).</summary>
			public int MassTenthStones;

			/// <summary>Bit 3 of the mass word: NPCs, 3D models and some animations.</summary>
			public bool IsAnimated;

			public int Value;

			/// <summary>Flags bit 1: set for most 3D models (not for doors,
			/// projectiles, pillar, gravestone).</summary>
			public bool Is3DModel;

			/// <summary>Byte 6 bit 1: thrown at something, the object is USED on what it hits
			/// (CollideObjects_seg029_29EE_173) - the orb rock on Tybal's orb. The same bit on the
			/// STRUCK object makes it use itself on the mover: the glowing rock that joins the
			/// carried one when walked over (UWGlowingRockRules).</summary>
			public bool UsedWhenThrown;

			/// <summary>Byte 6 bit 0: solid - the collision stops a mover at this object only when
			/// it is set (CollideObjects_seg029_29EE_173). Clear on everything of height 0 and, of
			/// the objects with a height, only on the glowing rock (and the move trigger).</summary>
			public bool IsSolid;

			/// <summary>Flags bit 2: set for all switches and levers.</summary>
			public bool IsSwitch;

			/// <summary>
			/// Flags bit 3: the object is not affected by gravity.
			///
			/// Based on it, the reference sets the fall acceleration of a flying object to
			/// zero instead of minus four (ObjectCreator.InitMobileObject via
			/// UnkBit_0X13_Bit7, evaluated in motion_init). So it decides whether a
			/// projectile flies straight or follows a ballistic arc.
			///
			/// TWELVE OBJECTS CARRY IT, and the selection is so plausible that it speaks for
			/// itself: fireball, lightning and magic missile - but NOT acid. Plus
			/// the pure images such as blood, mist, explosion, splash. Arrow, bolt,
			/// sling stone and rock do not carry it and fall.
			///
			/// The user observed exactly this in the original before we looked it up:
			/// the fireball flies straight, the acid projectile in an arc (2026-09-10).
			/// </summary>
			public bool IsWeightless;

			/// <summary>Flags bit 5. According to the docs "is set when object can be picked up" - with
			/// explicit exceptions that have the bit set anyway (urn, orb,
			/// campfire, fountain, cauldron). See UWFixedScenery.</summary>
			public bool CanBePickedUp;

			/// <summary>Flags bit 7: container (according to the docs except the cauldron).</summary>
			public bool IsContainer;

			/// <summary>Bits 6-7 of byte 3. For 0 and 2 a newly created object gets
			/// quantity 1 plus the quantity bit (UW.EXE PrepareNewObjectProps_seg035_3E).</summary>
			public int QuantityClass;

			/// <summary>Whether a newly created object starts with quantity 1, see QuantityClass.</summary>
			public bool StartsWithQuantity => QuantityClass == 0 || QuantityClass == 2;

			/// <summary>Bits 2-3 of byte 6. QualityClass*6 + quality gives the index into
			/// string block 5 (condition descriptions such as "sturdy", "badly damaged").</summary>
			public int QualityClass;

			/// <summary>
			/// Bits 5-8 of the word at byte 6, 0 to 15: how much of its vertical speed a
			/// flying object keeps when it strikes floor or ceiling. UW.EXE reads it into the
			/// motion parameters at +16h (InitMotionParams_seg029_29EE_3CC); on impact the
			/// vertical speed becomes -(speed / 15) * value and the horizontal loses
			/// (15 - value) / 30 of itself (reference: DoCollision_seg031_2CFA_D1F).
			/// Rock and sling stone 1, crossbow bolt 4, boulder 0.
			/// </summary>
			public int Elasticity;

			/// <summary>
			/// Bit 4 of byte 6: the object slides further. While it moves along the ground,
			/// UW.EXE rebuilds its momentum every motion tick from the stored momentum / 0x2F times
			/// 0x29 plus 4 for this bit, and stops it once that stored value is 2 plus 2 for this
			/// bit or less (InitMotionParams_seg029_29EE_3CC; reference motion_init). All
			/// ammunition has it, creatures do not.
			/// </summary>
			public bool SlidesFurther;

			/// <summary>
			/// Bits 1-4 of byte 7: how likely a projectile breaks when it comes to rest - the
			/// chance is value / 8, followed by the culling test (reference:
			/// ObjectHitsFloorTile_seg030_2BB7_DDF). Arrow 3, bolt and sling stone 4, rock 5; 9
			/// detonates (fireball, lightning), 10 is something else. Everything else has 0.
			/// </summary>
			public int ProjectileCulling;

			/// <summary>Bits 2-5 of byte 9 - the culling priority, see ProjectileCulling.</summary>
			public int CullingPriority => (Byte9 >> 2) & 0xF;

			/// <summary>Byte 8: which damage types the object is immune to, and whether it
			/// is undead. Interpretation and evidence see UWDamageTypes.</summary>
			public byte Resistances;

			/// <summary>Bits 0-3 of byte 10.</summary>
			public int QualityType;

			/// <summary>Bit 4 of byte 10: the object has a displayable
			/// "look at" description.</summary>
			public bool HasLookDescription;

			/// <summary>Bit 7 of byte 7: the object can have an owner
			/// ("belongs to ...").</summary>
			public bool CanHaveOwner;

			/// <summary>
			/// Byte 9, without meaning in the docs ("TODO unknown", only bit 0 for NPCs and
			/// flat texture objects is described). The values are all multiples of 4.
			///
			/// According to the user's throwing tests in the original (2026-08-31) this value decides
			/// whether an item is lost in water - see SinksInLiquid.
			/// </summary>
			public byte Byte9;

			/// <summary>
			/// Whether the item is lost in water and lava AT THE LOWEST culling range. Since
			/// 2026-09-21 this is no longer the whole rule: the range is rolled per landing and
			/// the count of a stack helps as well, see UWLiquidCulling, which is also where the
			/// evidence now sits. Kept because it says in one word what the value means.
			///
			/// There is NO flag for it. Map and scroll are identical byte for
			/// byte in COMOBJ.DAT except for the money value, but behave differently in water -
			/// all 88 bit positions were checked against the observations, none separates them.
			/// Byte 9 as a number, on the other hand, separates them completely:
			///
			///   stayed:     shiny sword 60, shiny shield 60, wand 40,
			///               cup 60, rune bag 60
			///   destroyed:  gold ring 32, sack 32, scroll 32,
			///               meat 24, red and green potion 24
			///
			/// The threshold is pinned to 40, namely by a prediction made BEFORE the test:
			/// ruby (36) and sapphire (44) lie together in the same gold chest on level 1,
			/// tile 57/58 - same object class, same location. The sapphire stayed,
			/// the ruby sank. From above, the wand with 40 bounds it, which stayed.
			///
			/// Above the threshold are, among others, the Key of Infinity, the
			/// Keys of Truth, Love and Courage, the two-part keys and
			/// all rune stones - i.e. the things whose loss would make the game
			/// unwinnable. What else byte 9 means is open.
			/// </summary>
			public bool SinksInLiquid => UWLiquidCulling.AlwaysSwallows(CullingPriority);
		}

		/// <summary>From this value in byte 9 an item survives the water. From the
		/// user's throwing tests, see Entry.SinksInLiquid.</summary>
		public const int LiquidSurvivalThreshold = 40;

		private const int EntrySize = 11;

		private const int HeaderSize = 2;

		private readonly Entry[] mOEntries;

		public int Count => mOEntries.Length;

		public UWCommonObjectProperties(string psDataPath)
		{
			string lsFile = Path.Combine(psDataPath, "COMOBJ.DAT");

			if (!File.Exists(lsFile))
			{
				mOEntries = new Entry[0];
				return;
			}

			byte[] lyData = File.ReadAllBytes(lsFile);
			int liCount = (lyData.Length - HeaderSize) / EntrySize;
			mOEntries = new Entry[liCount];

			for (int liIndex = 0; liIndex < liCount; liIndex++)
			{
				int liOffset = HeaderSize + (liIndex * EntrySize);
				int liMass = lyData[liOffset + 1] | (lyData[liOffset + 2] << 8);
				byte lyFlags = lyData[liOffset + 3];

				mOEntries[liIndex] = new Entry
				{
					Height = lyData[liOffset],
					Radius = (byte)(liMass & 0x7),
					IsAnimated = (liMass & 0x8) != 0,
					MassTenthStones = liMass >> 4,
					Is3DModel = (lyFlags & 0x02) != 0,
					IsSwitch = (lyFlags & 0x04) != 0,
					IsWeightless = (lyFlags & 0x08) != 0,
					CanBePickedUp = (lyFlags & 0x20) != 0,
					IsContainer = (lyFlags & 0x80) != 0,
					QuantityClass = (lyFlags >> 6) & 0x3,
					Value = lyData[liOffset + 4] | (lyData[liOffset + 5] << 8),
					QualityClass = (lyData[liOffset + 6] >> 2) & 0x3,
					UsedWhenThrown = (lyData[liOffset + 6] & 0x02) != 0,
					IsSolid = (lyData[liOffset + 6] & 0x01) != 0,
					Elasticity = ((lyData[liOffset + 6] | (lyData[liOffset + 7] << 8)) >> 5) & 0xF,
					SlidesFurther = (lyData[liOffset + 6] & 0x10) != 0,
					ProjectileCulling = (lyData[liOffset + 7] >> 1) & 0xF,
					CanHaveOwner = (lyData[liOffset + 7] & 0x80) != 0,
					Resistances = lyData[liOffset + 8],
					Byte9 = lyData[liOffset + 9],
					QualityType = lyData[liOffset + 10] & 0xF,
					HasLookDescription = (lyData[liOffset + 10] & 0x10) != 0
				};
			}
		}

		/// <summary>Weightless by COMOBJ.DAT - false for an unknown object.</summary>
		public bool IsWeightless(int piObjectId)
		{
			Entry lOEntry;

			return TryGet(piObjectId, out lOEntry) && lOEntry.IsWeightless;
		}

		public bool TryGet(int piObjectId, out Entry pOEntry)
		{
			if (piObjectId < 0 || piObjectId >= mOEntries.Length)
			{
				pOEntry = default;
				return false;
			}

			pOEntry = mOEntries[piObjectId];
			return true;
		}
	}
}
