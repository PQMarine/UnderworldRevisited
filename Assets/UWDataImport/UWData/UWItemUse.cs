namespace UWDataImport.UWData
{
	/// <summary>
	/// Using an item on the character itself - what takes effect immediately on the click
	/// instead of waiting for a target: eating and drinking, the mushroom, potions and spell
	/// scrolls, the wand with its charges, light sources, leeches, incense, and the hand-offs
	/// to bedroll, instruments, seed and fishing pole. Engine-free since 2026-09-18 (P3 of the
	/// engine separation), out of UWItemDrag, which keeps the dragging and applies items to a
	/// clicked target.
	/// </summary>
	public static class UWItemUse
	{
		// String block 1, our numbering.

		/// <summary>"You quaff the potion in one gulp."</summary>
		private const int QuaffMessage = 241;

		/// <summary>"The mushroom causes your head to spin, and your vision to blur."</summary>
		private const int MushroomMessage = 233;

		private const int WineBottleMessage = 128;

		private const int TooFullMessage = 127;

		private const int RefreshingDrinkMessage = 243;

		private const int WakeUnsteadyMessage = 244;

		/// <summary>Message when a wand breaks.</summary>
		private const int WandCracksMessage = 126;

		/// <summary>Message when all four hand slots are occupied.</summary>
		private const int HandsFullMessage = 247;

		/// <summary>"That light is already used up." (block 1).</summary>
		private const int LightUsedUpMessage = 125;

		/// <summary>"The leeches remove the poison as well as some of your skin and blood."</summary>
		private const int LeechesRemovePoisonMessage = 225;

		/// <summary>"You manage to finish eating the leeches.  Barely." - what the leeches give
		/// when they are EATEN instead of applied, see TryEat.</summary>
		private const int EatenLeechesMessage = 230;

		/// <summary>"You can only use those individually.  Take one from this group if you wish
		/// to use it." - a STACK is not eaten, see TryEat.</summary>
		private const int OnlyIndividuallyMessage = 120;

		/// <summary>The red and the green potion go their own way, with their spell (see
		/// fUseSpellItemUp). The ale is a drink since 2026-09-24 (see fEatLikeOriginal).</summary>
		private const int FirstPotionKind = 0xB;

		private const int LastPotionKind = 0xC;

		/// <summary>The mushroom's intelligence check is made against this value.</summary>
		private const int MushroomDifficulty = 0x14;

		/// <summary>some_leeches (0x125).</summary>
		public const int LeechObjectId = 0x125;

		/// <summary>Colour of the flash when the leeches bite - 0xA8 in uw1 (the reference:
		/// leech.use).</summary>
		private const int LeechFlashColour = 0xA8;

		private const float LeechFlashSeconds = 0.2f;

		private const int LeechMinimumHitPoints = 3;

		/// <summary>a_block of burning incense (277) - see fUseIncense.</summary>
		public const int BurningIncenseObjectId = 277;

		/// <summary>What is left of the block afterwards: a pile of debris.</summary>
		private const int IncenseRemainsObjectId = 213;

		/// <summary>The three visions are cutscenes 11 to 13 (files CS013 to CS015, the numbers in
		/// the file names are octal).</summary>
		private const int FirstIncenseCutscene = 0x0B;

		private const int IncenseVisionCount = 3;

		/// <summary>The object number of a broken wand rises by this much.</summary>
		private const int BrokenWandOffset = 4;

		/// <summary>The four whole wands (0x98-0x9B); the next four are the broken ones.</summary>
		private const int FirstWandObjectId = 0x98;

		private const int LastWandObjectId = 0x9B;

		/// <summary>An empty charge rolls RNG * 10 / 0x8000, 0 to 9, and fails below 4 - twice,
		/// see fTryCastFromItem.</summary>
		private const int EmptyChargeRollRange = 10;

		private const int EmptyChargeFailBelow = 4;

		/// <summary>The bedroll (UWSleep.BedrollObjectId on the Unity side).</summary>
		public const int BedrollObjectId = UWSleepRules.BedrollObjectId;

		/// <summary>The silver seed (UWSilverTree.SeedObjectId on the Unity side).</summary>
		public const int SilverSeedObjectId = 0x122;

		/// <summary>The fishing pole (UWFishing.FishingPoleObjectId on the Unity side).</summary>
		public const int FishingPoleObjectId = 0x12B;

		/// <summary>Which rune spell the item casts, or -1. Looked up in the
		/// inventory records of the save game, where the wand's own spell object hangs on it.
		/// </summary>
		public static int SpellIndexOf(UWObject pOItem, DataImport pOData)
		{
			if (pOItem == null || pOData == null || pOData.InitialPlayer == null)
				return -1;

			int liCharges;

			return UWObjectMechanics.GetObjectSpellIndex(pOItem, pOData.InitialPlayer.InventoryRecords, out liCharges);
		}

		/// <summary>What an item casts: a rune spell by its number, or a spell only items carry
		/// by its classes (UWItemSpellRules) - or nothing.</summary>
		public struct ItemSpell
		{
			public int RuneSpell;

			public int MajorClass;

			public int MinorClass;

			public bool Exists => RuneSpell >= 0 || MajorClass >= 0;
		}

		public static ItemSpell SpellOf(UWObject pOItem, DataImport pOData)
		{
			ItemSpell lOSpell = new ItemSpell { RuneSpell = SpellIndexOf(pOItem, pOData), MajorClass = -1 };

			if (lOSpell.RuneSpell < 0 && pOItem != null && pOData != null && pOData.InitialPlayer != null)
			{
				UWObjectMechanics.TryGetItemClassSpell(pOItem, pOData.InitialPlayer.InventoryRecords,
					out lOSpell.MajorClass, out lOSpell.MinorClass);
			}

			return lOSpell;
		}

		/// <summary>Casts what SpellOf found; false if there is nothing to cast.</summary>
		private static bool fCast(ItemSpell pOSpell, IUWItemUseHost pIHost)
		{
			if (pOSpell.RuneSpell >= 0)
				return pIHost.CastSpellFromObject(pOSpell.RuneSpell);

			return pOSpell.MajorClass >= 0 && pIHost.CastSpellByClass(pOSpell.MajorClass, pOSpell.MinorClass);
		}

		/// <summary>The red and the green potion - "You quaff the potion in one gulp." and their
		/// spell. The ale was one until 2026-09-24; the original drinks it (see fEatLikeOriginal).
		/// </summary>
		public static bool IsPotion(UWObject pOItem)
		{
			if (pOItem == null || pOItem.GetCategory() != UWObject.ObjectCategoryEnum.Comestibles)
				return false;

			int liKind = pOItem.ID & 0xF;

			return liKind >= FirstPotionKind && liKind <= LastPotionKind;
		}

		/// <summary>A spell scroll: a scroll that is enchanted. An unenchanted one
		/// carries a text and is read (see Interaction.TryReadBook).</summary>
		public static bool IsSpellScroll(UWObject pOItem)
		{
			return pOItem != null && pOItem.IsEnchanted
				&& pOItem.GetCategory() == UWObject.ObjectCategoryEnum.BooksAndScrolls;
		}

		/// <summary>Everything that takes effect immediately on click instead of waiting for a target.
		/// Returns whether the click was handled.</summary>
		/// <summary>
		/// EATING, as the original does it when something is dropped on the head of the
		/// paperdoll (MaybeContainerLogic_ovr121_B4B calls Eat_seg040_F0E for slot 0).
		///
		/// IT IS NOT THE ORDINARY USE PATH, and the leeches prove it: used, they bite and suck
		/// the poison out (message 225); dropped on the head they are EATEN, with message 230
		/// ("You manage to finish eating the leeches.  Barely.") and without any of their
		/// effect. The user checked all of this in the original on 2026-09-21, including that
		/// the bite stays away.
		///
		/// What is handled: the leeches, a potion (which goes down in one gulp, as when used),
		/// and everything of the comestibles - food, drink, the mushroom and the wine bottle
		/// that cannot be opened. Anything else is not eaten, and the caller lets the drop fall
		/// through into the slot, so a helmet still goes onto the head.
		/// </summary>
		public static bool TryEat(UWObject pOItem, DataImport pOData, UWPlayerVitals pOVitals,
			IUWItemUseHost pIHost)
		{
			if (pOItem == null || pIHost == null)
				return false;

			// A STACK IS NOT EATEN. The original says so before it looks at what the thing is
			// (Eat_seg040_F0E, the branch with string 120), and the user read it back from the
			// original on 2026-09-21 with a stack of five fish: "You can only use those
			// individually.  Take one from this group if you wish to use it." A single piece
			// taken off the stack is eaten - his next line was "You are too full to eat that
			// now.", which is our own fEat message.
			if (UWObjectMechanics.IsStackable(pOItem) && pOItem.Quantity > 1)
			{
				pIHost.AddGeneralMessage(OnlyIndividuallyMessage);

				return true;
			}

			return fEatLikeOriginal(pOItem, pOData, pOVitals, pIHost);
		}

		public static bool TryUseImmediately(UWObject pOItem, DataImport pOData, UWPlayerVitals pOVitals,
			UWInventoryModel pOInventory, IUWItemUseHost pIHost)
		{
			if (pOItem == null || pIHost == null)
				return false;

			// POTION AND SPELL SCROLL COME BEFORE THE WAND PATH. Both carry their spell just like
			// a wand, but have no charges: they are gone after the single use.
			// Without this branch they run into the wand's charge accounting and stay in the
			// backpack - exactly what was seen with the Freeze Time scroll (per user,
			// 2026-09-09).
			//
			// The reference decides the same way for a scroll: if it is enchanted, the spell is cast
			// and the scroll consumed, otherwise it is read (readable.Use, uw1 branch). Our
			// Interaction.TryReadBook already has the read branch, it bails out for enchanted scrolls.
			if (IsPotion(pOItem) || IsSpellScroll(pOItem))
				return fUseSpellItemUp(pOItem, pOData, pOVitals, pIHost);

			// FOOD GOES TO THE EATING ROUTINE BY ITS CLASS, before the wand path: an enchanted ale
			// is drunk and casts its spell at the end (ObjectUse's food case calls Eat_seg040_F0E,
			// which calls CastSpellFromObject_seg040_1F04). The wand path would have cast it and
			// kept the bottle.
			if (pOItem.GetCategory() == UWObject.ObjectCategoryEnum.Comestibles || IsEatenOnUse(pOItem.ID))
				return fEatLikeOriginal(pOItem, pOData, pOVitals, pIHost);

			return fTryCastFromItem(pOItem, pOData, pOInventory, pIHost)
				|| fTryUseOnSelf(pOItem, pOData, pOVitals, pOInventory, pIHost);
		}

		/// <summary>
		/// Drink a potion or read a spell scroll (reference: potion.QuaffPotion and
		/// readable.Use).
		///
		/// THE POTION IS ONLY THE SHELL. Its effect hangs on it as a separate spell object,
		/// just like with a wand - so it is looked up the same way
		/// (SpellIndexOf). Unlike the wand it has no charges: it is cast and
		/// gone.
		///
		/// IF NO SPELL IS ATTACHED, the reference instead looks for a DAMAGE TRAP in the
		/// chain - that is the poisoned potion. Its quality is the base damage, its
		/// owner field says whether it is poison. It is only searched if the potion is not a stack:
		/// on a stack the same field carries the quantity.
		/// </summary>
		private static bool fUseSpellItemUp(UWObject pOItem, DataImport pOData, UWPlayerVitals pOVitals, IUWItemUseHost pIHost)
		{
			// The message belongs to drinking. A scroll is read without a word.
			if (IsPotion(pOItem))
				pIHost.AddGeneralMessage(QuaffMessage);

			ItemSpell lOSpell = SpellOf(pOItem, pOData);

			// BOTH WAIT FOR THE WANDS' PAUSE: while it runs, the refusal sound, no spell - and the
			// potion or scroll is gone all the same. The potion because Eat_seg040_F0E ends in
			// CastSpellFromObject with the pause (read 2026-09-24); the scroll because
			// UseReadable_seg040_352B_19A8 (read 2026-09-25) sends an enchanted scroll carried in
			// the pack there with the pause as well and clears it afterwards whatever the cast
			// did. Until then ours cast a scroll at once.
			if (lOSpell.Exists)
				fCastWithPause(lOSpell, pIHost);
			else
				fApplyPotionTrap(pOItem, pOData, pOVitals);

			pIHost.ConsumeUsedItem();

			return true;
		}

		/// <summary>The damage trap of a poisoned potion - see fUseSpellItemUp.</summary>
		private static void fApplyPotionTrap(UWObject pOItem, DataImport pOData, UWPlayerVitals pOVitals)
		{
			if (pOData == null || pOData.InitialPlayer == null || pOVitals == null)
				return;

			// The same record list in which a wand's spell also hangs (see SpellIndexOf).
			UWObject lOTrap = UWObjectMechanics.FindDamageTrapInChain(pOItem, pOData.InitialPlayer.InventoryRecords);

			if (lOTrap == null)
				return;

			if (lOTrap.Owner > 0)
				pOVitals.ApplyPoison(lOTrap.Quality);
			else
				pOVitals.ApplyDamage(lOTrap.Quality, UWDamageTypes.None, 0);
		}

		/// <summary>
		/// Casts the spell of a wand or another charged item.
		///
		/// After a use the wand is briefly dead. The click still counts as handled,
		/// otherwise it would run into use mode. UW.EXE CastSpellFromObject_seg040_1F04: too
		/// soon after the last use it plays sound 0x15 at the avatar and casts nothing, and no
		/// charge is spent.
		///
		/// THE CHARGES AND THE EMPTY WAND, read 2026-09-24 (WandUsage_seg040_E21,
		/// GetItemEnchantment_seg040_352B_257C, MagicChargeUpdate_seg040_352B_2723): a charged
		/// spell object casts and loses one charge. At ZERO two rolls of 0 to 9 decide, each
		/// failing below 4: the first, before anything, makes the enchantment not found at all -
		/// nothing happens, no sound, no pause; the second, after the cast, takes the spell object
		/// off the wand, and a wand without it cracks. So an empty wand does nothing 40 % of the
		/// time, casts and holds 36 %, casts and breaks 24 %. Until then ours cast every time
		/// and broke half the time (an estimate from the user's series, 2026-09-05).
		///
		/// Breaking does NOT mean removing: the object number rises by four, 152 to 155 become
		/// 156 to 159, "a broken wand" in string block 4. Only a wand cracks; another item that
		/// loses its spell object simply keeps no magic.
		/// </summary>
		private static bool fTryCastFromItem(UWObject pOItem, DataImport pOData, UWInventoryModel pOInventory, IUWItemUseHost pIHost)
		{
			ItemSpell lOSpell = SpellOf(pOItem, pOData);

			if (!lOSpell.Exists)
				return false;

			UWObject lOSpellObject = UWObjectMechanics.FindSpellObject(pOItem, pOData.InitialPlayer.InventoryRecords);
			bool lbEmpty = lOSpellObject != null && lOSpellObject != pOItem && lOSpellObject.Quality <= 0;

			// The first roll: the empty enchantment is not found - the click does nothing at all.
			if (lbEmpty && UWRandom.Next(EmptyChargeRollRange) < EmptyChargeFailBelow)
				return true;

			if (pIHost.IsWandCoolingDown)
			{
				pIHost.PlayMagicItemRefused();

				return true;
			}

			// Do NOT fire immediately: on the click the pointer is over the inventory, not in the
			// view window. A projectile therefore waits for the target click as with rune casting -
			// exactly as the original does it (per user, 2026-09-05).
			if (!fCast(lOSpell, pIHost))
				return false;

			pIHost.StartWandCooldown();

			if (lOSpellObject != null && lOSpellObject != pOItem)
			{
				// The spell has already been cast at this point - in the original even the
				// use on which the wand breaks still triggers it (per user, 2026-09-05).
				if (lbEmpty)
				{
					// The second roll: the spell object leaves the item.
					if (UWRandom.Next(EmptyChargeRollRange) < EmptyChargeFailBelow)
					{
						// The link to the spell object is dropped - that is how the original detaches
						// it. Without this the broken wand would keep casting and name its spell
						// when looked at.
						pOItem.Quantity = 0;

						// Contents is what saving writes back as the chain, so the spell object
						// has to leave it too - until 2026-09-25 it stayed there and a cracked
						// wand cast again after a reload. Its four tenths of a stone stay in the
						// carried load, as MagicChargeUpdate never passes the inventory removal.
						if (pOItem.Contents != null)
							pOItem.Contents.Remove(lOSpellObject);

						if (pOInventory != null)
							pOInventory.KeepWeightOf(lOSpellObject, pOData.CommonObjectProperties);

						if (pOItem.ID >= FirstWandObjectId && pOItem.ID <= LastWandObjectId)
						{
							UWObjectMechanics.SetObjectId(pOItem, pOItem.ID + BrokenWandOffset, pOData.Textures);

							pIHost.AddGeneralMessage(WandCracksMessage);
						}
					}
				}
				else
					lOSpellObject.Quality = (ushort)(lOSpellObject.Quality - 1);

				if (pOInventory != null)
					pOInventory.NotifyChanged();
			}

			return true;
		}

		/// <summary>
		/// Items that act on the character itself when used instead of on a target.
		///
		/// A LIGHT SOURCE is picked up and from then on shines along; how brightly is given by the
		/// light table from OBJECTS.DAT. Burning down is handled by the inventory model.
		///
		/// Bedroll, instruments and the silver seed are handed to the host.
		/// </summary>
		private static bool fTryUseOnSelf(UWObject pOItem, DataImport pOData, UWPlayerVitals pOVitals,
			UWInventoryModel pOInventory, IUWItemUseHost pIHost)
		{
			// THE BEDROLL IS USED FROM THE BACKPACK, not from the ground - that is why it is
			// here and not with the world objects (see UWSleep).
			if (pOItem.ID == BedrollObjectId)
				return pIHost.TrySleep();

			// Mandolin and flute are played - also only from the backpack (see
			// UWInstrumentPlayer).
			if (pIHost.IsInstrument(pOItem.ID))
				return pIHost.PlayInstrument(pOItem);

			// The silver seed is planted ahead of the player (see UWSilverTree); used up when it
			// grows or vanishes on level 9, kept when it finds no soil.
			if (pOItem.ID == SilverSeedObjectId)
			{
				if (pIHost.TryPlantSeed())
					pIHost.ConsumeUsedItem();

				return true;
			}

			// Leeches: they bite (2d8, never below 3 hit points) and suck the poison out with it
			// (per user, 2026-09-17; the reference: leech.use). Only from the pack, not from the
			// ground.
			if (pOItem.ID == LeechObjectId)
				return fUseLeeches(pOVitals, pIHost);

			// Burning incense brings a vision (see fUseIncense).
			if (pOItem.ID == BurningIncenseObjectId)
				return fUseIncense(pOItem, pOData, pOInventory, pIHost);

			// Bronus' book explodes when opened (see UWExplodingBook).
			if (pOItem.ID == UWExplodingBook.BookObjectId)
				return UWExplodingBook.Open(pOItem, pOInventory, pIHost);

			// The fishing pole fishes in the water ahead (see UWFishing); it is never used up.
			if (pOItem.ID == FishingPoleObjectId)
				return pIHost.TryFish();

			UWObject.ObjectCategoryEnum leCategory = pOItem.GetCategory();

			if (leCategory == UWObject.ObjectCategoryEnum.LightSources)
				return fTryLightSource(pOItem, pOData, pOInventory, pIHost);

			if (leCategory == UWObject.ObjectCategoryEnum.Comestibles || IsEatenOnUse(pOItem.ID))
				return fEatLikeOriginal(pOItem, pOData, pOVitals, pIHost);

			return false;
		}

		/// <summary>
		/// The leeches: 2d8 damage, but never below three hit points, and any poison is gone
		/// afterwards - with its own line in the scroll. They are used up either way.
		/// </summary>
		private static bool fUseLeeches(UWPlayerVitals pOVitals, IUWItemUseHost pIHost)
		{
			if (pOVitals == null)
				return false;

			// 2d8 as the reference rolls it: two plus two rolls from zero to seven.
			int liDamage = 2 + UWRandom.Next(8) + UWRandom.Next(8);

			pOVitals.ApplyDamageKeepingMinimum(liDamage, LeechMinimumHitPoints, 0);

			if (pOVitals.Poison > 0)
			{
				pOVitals.CurePoison();

				pIHost.AddGeneralMessage(LeechesRemovePoisonMessage);
			}

			pIHost.FlashWindow(LeechFlashColour, LeechFlashSeconds);

			pIHost.ConsumeUsedItem();

			return true;
		}

		/// <summary>
		/// Burning incense: its smoke brings a vision (per user, 2026-09-17; the reference:
		/// incense.Use). The first three uses show the three visions in order, after that it is a
		/// roll among them. How many have been seen is kept in the savegame (UWPlayerData
		/// IncenseCounter). The block itself burns down to debris either way.
		///
		/// Wizard Rodrick tells the trick himself in his conversation: "Simply pass a block of
		/// incense over a torch to produce the smoke which, when inhaled, will cause one to dream."
		/// </summary>
		private static bool fUseIncense(UWObject pOItem, DataImport pOData, UWInventoryModel pOInventory, IUWItemUseHost pIHost)
		{
			UWPlayerData lOPlayer = pOData != null ? pOData.InitialPlayer : null;

			int liVision = UWRandom.Next(IncenseVisionCount);

			if (lOPlayer != null && lOPlayer.IncenseCounter < IncenseVisionCount)
			{
				lOPlayer.IncenseCounter++;
				liVision = IncenseVisionCount - lOPlayer.IncenseCounter;
			}

			// The block is used up whether or not the cutscene can play.
			UWObjectMechanics.SetObjectId(pOItem, IncenseRemainsObjectId, pOData != null ? pOData.Textures : null);

			if (pOInventory != null)
				pOInventory.NotifyChanged();

			pIHost.PlayCutscene(FirstIncenseCutscene + liVision);

			return true;
		}

		/// <summary>
		/// EATING AND DRINKING, as the original's one routine does it (Eat_seg040_F0E, read whole
		/// 2026-09-24): the head drop, the use of food from the pack, and the use of the plants,
		/// the dead rotworm and the wormy stew all land there.
		///
		/// THE NUTRITION comes from the OBJECTS.DAT table for the sixteen comestibles 0xB0-0xBF
		/// (a signed byte: meat 64, fish 48, apple 6, mushroom 0, toadstool 6, ale -3, the two
		/// potions -127, water -1, port -8, wine 0); five other things carry their own value and
		/// line: the thorny plant 4 (237), the plain plant 0x17 (234), the rotworm 4 (235), the
		/// stew 0x40 (236). A few only have a line: the leeches (230), the candle (231) - eaten,
		/// but no food. Anything else is not eaten at all.
		///
		/// POSITIVE: ChangeHunger - if it will not fit, "You are too full to eat that now." and
		/// the piece stays. Then the thing's own line, or for plain food "That ... tasted ...".
		/// The toadstool poisons after that.
		///
		/// ZERO OR NEGATIVE: the line (the mushroom's, "The water refreshes you.", "You drink the
		/// port.", "You drink the dark ale."), and between -2 and -126 the drink makes drunk -
		/// port and ale, not water, and not the potions, which only carry their spell.
		///
		/// Until 2026-09-24 ours ate only the comestibles, printed no taste and no drink line,
		/// sent the ale down the potion path without any intoxication, let water make drunk,
		/// and gave the mushroom the potion's big mana formula.
		/// </summary>
		private static bool fEatLikeOriginal(UWObject pOItem, DataImport pOData, UWPlayerVitals pOVitals,
			IUWItemUseHost pIHost)
		{
			if (pOVitals == null || pOData == null || pOData.ObjectClassProperties == null)
				return false;

			int liId = pOItem.ID;

			if (liId == WineObjectId)
			{
				pIHost.AddGeneralMessage(WineBottleMessage);

				return true;
			}

			// The two potions keep their path: their line, then their spell or trap.
			if (IsPotion(pOItem))
				return fUseSpellItemUp(pOItem, pOData, pOVitals, pIHost);

			int liNutrition = (liId >> 4) == ComestiblesClass
				? pOData.ObjectClassProperties.GetFoodNutrition(liId) : NotFood;

			// Read before the piece is consumed - see fCastWithPause.
			ItemSpell lOSpell = SpellOf(pOItem, pOData);
			int liMessage = 0;

			switch (liId)
			{
				case MushroomObjectId:
					fMushroomEffects(pOVitals);
					liMessage = MushroomMessage;
					break;

				case ToadstoolObjectId: liMessage = ToadstoolMessage; break;
				case CandleObjectId: liMessage = EatenCandleMessage; break;
				case LeechObjectId: liMessage = EatenLeechesMessage; break;
				case ThornyPlantObjectId: liMessage = ThornyPlantMessage; liNutrition = 4; break;
				case PlainPlantObjectId: liMessage = PlainPlantMessage; liNutrition = 0x17; break;
				case DeadRotwormObjectId: liMessage = RotwormMessage; liNutrition = 4; break;
				case WormyStewObjectId: liMessage = WormyStewMessage; liNutrition = 0x40; break;
				case WaterObjectId: liMessage = WaterMessage; break;
				case PortObjectId: liMessage = PortMessage; break;
				case AleObjectId: liMessage = AleMessage; break;
			}

			if (liNutrition == NotFood && liMessage == 0)
				return false;

			if (liNutrition > 0)
			{
				if (liNutrition != NotFood && !pOVitals.ChangeHunger(liNutrition))
				{
					pIHost.AddGeneralMessage(TooFullMessage);

					return true;
				}

				if (liMessage != 0)
				{
					pIHost.AddGeneralMessage(liMessage);
				}
				else
				{
					int liTaste = System.Math.Min(LastTasteStep, (UWRandom.Next(TasteRollRange) + (pOItem.Quality & 0x3F)) >> 4);

					pIHost.AddMessage(TastePrefix + UWItemDescriptions.GetBareName(liId, pOData)
						+ (pOData.GetGeneralMessage(FirstTasteMessage + liTaste) ?? string.Empty).TrimEnd('\r', '\n'));
				}

				if (liId == ToadstoolObjectId)
					pOVitals.PoisonFromToadstool();

				fCastWithPause(lOSpell, pIHost);

				pIHost.ConsumeUsedItem();

				return true;
			}

			pIHost.AddGeneralMessage(liMessage);

			pIHost.ConsumeUsedItem();

			if (liNutrition < -1 && liNutrition > -127)
				fDrink(pOVitals, -liNutrition, pIHost);

			fCastWithPause(lOSpell, pIHost);

			return true;
		}

		/// <summary>
		/// WHAT WAS EATEN MAY CARRY A SPELL (CastSpellFromObject_seg040_1F04 at the end of Eat,
		/// read 2026-09-24): an enchanted piece in the spell form (flag bit 2) casts it - the one
		/// such piece in the game is the ale on level 1 at 2/23 with Lesser Heal. It shares the
		/// wands' pause: while it runs, the refusal sound and no spell, and the piece is gone all
		/// the same. The enchantment form without that bit (the loaf on level 8 at 7/5) casts
		/// nothing. Ours cast nothing for food until then. Potions and spell scrolls take the
		/// same way (fUseSpellItemUp).
		/// </summary>
		private static void fCastWithPause(ItemSpell pOSpell, IUWItemUseHost pIHost)
		{
			if (!pOSpell.Exists)
				return;

			if (pIHost.IsWandCoolingDown)
			{
				pIHost.PlayMagicItemRefused();

				return;
			}

			if (fCast(pOSpell, pIHost))
				pIHost.StartWandCooldown();
		}

		/// <summary>The start of the taste line - a string of UW.EXE, not of the string file.</summary>
		private const string TastePrefix = "That ";

		/// <summary>" tasted putrid." (block 1); the next four up to " tasted great.".</summary>
		private const int FirstTasteMessage = 173;

		private const int LastTasteStep = 4;

		/// <summary>The roll is RNG * 20 / 0x8000, so 0 to 19.</summary>
		private const int TasteRollRange = 20;

		/// <summary>No table value - the original's 0xFF.</summary>
		private const int NotFood = 0xFF;

		/// <summary>0xB0-0xBF: (id &gt;&gt; 4) is 0xB.</summary>
		private const int ComestiblesClass = 0xB;

		private const int CandleObjectId = 0x92;
		private const int MushroomObjectId = 0xB8;
		private const int ToadstoolObjectId = 0xB9;
		private const int AleObjectId = 0xBA;
		private const int WaterObjectId = 0xBD;
		private const int PortObjectId = 0xBE;
		private const int WineObjectId = 0xBF;
		private const int ThornyPlantObjectId = 0xCE;
		private const int PlainPlantObjectId = 0xCF;
		private const int DeadRotwormObjectId = 0xD9;
		private const int WormyStewObjectId = 0x11B;

		/// <summary>The three things outside the comestibles that are eaten when USED from the
		/// pack (seg040_352B_919 for rotworm and plants, the class 4/1 use for the stew).</summary>
		public static bool IsEatenOnUse(int piObjectId)
		{
			return piObjectId == ThornyPlantObjectId || piObjectId == PlainPlantObjectId
				|| piObjectId == DeadRotwormObjectId || piObjectId == WormyStewObjectId;
		}

		// Block 1, our numbering.
		private const int EatenCandleMessage = 231;
		private const int ToadstoolMessage = 232;
		private const int PlainPlantMessage = 234;
		private const int RotwormMessage = 235;
		private const int WormyStewMessage = 236;
		private const int ThornyPlantMessage = 237;
		private const int WaterMessage = 238;
		private const int PortMessage = 239;
		private const int AleMessage = 240;
		private const int PassOutMessage = 242;

		/// <summary>
		/// The mushroom's effects before its line: MANA when a check of Intelligence against 0x14
		/// does not come out plain zero - a critical failure counts too - of 0 to 2 points
		/// (RNG * 3 / 0x8000, handed to the change routine as a negative, which adds it as it is),
		/// and one step of HALLUCINATION up to three.
		///
		/// WHAT IS SEEN meanwhile is UWHallucinationState (one of three effects, rolled once; the
		/// palette one is built). The counter runs along in the save.
		/// </summary>
		private static void fMushroomEffects(UWPlayerVitals pOVitals)
		{
			if ((int)UWSkillCheck.Check(pOVitals.Intelligence, MushroomDifficulty) != 0)
				pOVitals.AddMana(UWRandom.Next(3));

			pOVitals.AddHallucination();
		}

		/// <summary>
		/// A drink that makes drunk (port, ale) - see UWPlayerVitals.Drink. Passing out: "As the
		/// alcohol hits you, you stumble and collapse into sleep.", the sleep, and whoever wakes
		/// "You wake feeling somewhat unstable but better." with the view shaking.
		/// </summary>
		private static void fDrink(UWPlayerVitals pOVitals, int piIntoxication, IUWItemUseHost pIHost)
		{
			UWPlayerVitals.DrinkResult leResult = pOVitals.Drink(piIntoxication);

			if (leResult == UWPlayerVitals.DrinkResult.Refreshing)
				pIHost.AddGeneralMessage(RefreshingDrinkMessage);

			if (leResult != UWPlayerVitals.DrinkResult.PassedOut)
				return;

			pIHost.AddGeneralMessage(PassOutMessage);
			pIHost.SleepPassedOut();

			if (pOVitals.CurrentHP > 0f)
			{
				pIHost.AddGeneralMessage(WakeUnsteadyMessage);
				pOVitals.ShakeAfterPassingOut();
			}
		}

		/// <summary>
		/// Lights a light source.
		///
		/// ONE from the stack is lit: it turns into its burning counterpart
		/// (object number plus four) and moves into one of the four hand slots. Only there may
		/// a burning one lie, and so it cannot be stacked onto an unlit one either.
		/// If all four are occupied, nothing is lit and "Your hands are full."
		/// is reported. All per user, checked in the original (2026-09-05).
		///
		/// An already burning source is put out and stays where it is - whether a click puts it
		/// out in the original has not been checked.
		/// </summary>
		private static bool fTryLightSource(UWObject pOItem, DataImport pOData, UWInventoryModel pOInventory, IUWItemUseHost pIHost)
		{
			if (pOInventory == null)
				return true;

			UWTextures lOTextures = pOData != null ? pOData.Textures : null;

			// A burning one is put out: it turns back into its unlit
			// counterpart and stays where it is.
			if (UWObjectMechanics.Extinguish(pOItem, lOTextures))
			{
				pOInventory.ApplyCarriedLight();
				pOInventory.NotifyChanged();

				return true;
			}

			if (!UWObjectMechanics.IsUnlitLight(pOItem.ID))
				return true;

			// A BURNED-OUT ONE (quality zero, as burning down leaves it) does not light again:
			// "That light is already used up." (LightUsage_seg040_C86, before any slot is looked at).
			if (pOItem.Quality == 0)
			{
				pIHost.AddGeneralMessage(LightUsedUpMessage);

				return true;
			}

			// If it is already in the hand and not a stack, it is lit IN PLACE
			// - then no free slot is needed (per user, 2026-09-05). A
			// stack still goes the way below, so that only one of them burns.
			if (pOInventory.IsInHandSlot(pOItem) && (!pOItem.HasQuantity || pOItem.Quantity <= 1))
			{
				UWObjectMechanics.SetObjectId(pOItem, pOItem.ID + UWObjectMechanics.LitLightOffset, lOTextures);

				pOInventory.ApplyCarriedLight();
				pOInventory.NotifyChanged();

				return true;
			}

			UWObject lOLit = pOItem.CloneWithQuantity(1);

			if (lOLit == null)
				return true;

			lOLit.HasQuantity = false;
			lOLit.Quantity = 1;

			UWObjectMechanics.SetObjectId(lOLit, pOItem.ID + UWObjectMechanics.LitLightOffset, lOTextures);

			if (!pOInventory.TryPlaceInFreeHandSlot(lOLit))
			{
				pIHost.AddGeneralMessage(HandsFullMessage);

				return true;
			}

			pOInventory.TryConsumeOne(pOItem);

			// The BRIGHTEST burning light shines, not necessarily this one - a candle lit next to a
			// burning lantern must not dim the view (per user, 2026-09-18; the rule lives in
			// UWInventoryModel.GetCarriedLightObjectId). Until then the host was told this id directly.
			pOInventory.ApplyCarriedLight();

			return true;
		}
	}
}
