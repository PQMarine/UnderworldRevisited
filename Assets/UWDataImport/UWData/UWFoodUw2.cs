using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S EATING (UseFood, uw2 obj/USEITEMS.C:186): its own ids and lines, the nutrition from the same
	/// table, no toadstool's poison, and what is left of the meal: meat on a stick a stick, a piece of meat a bone, a
	/// honeycomb a lump of wax, a bottle an empty bottle. Eaten when used from the pack, or dropped on the head. The dream
	/// plant's way into the Void comes with the Void. The potions (0xE1-0xE7, UseReag) are drunk here too since 2026-11-16:
	/// "You quaff the potion in one gulp." and the drink's sound; their spell is the caller's (UWCarriedSpellRulesUw2.CheckSpell).
	/// The sounds: food by the hunger after the meal (0x25 from 0xC0, 0x1F above 0x5A, else 0x21), a potion or a drink
	/// 0x1E, nothing for a thing without nutrition.
	/// </summary>
	public static class UWFoodUw2
	{
		public enum OutcomeEnum
		{
			NotFood,

			/// <summary>"You are too full to eat that now.": nothing happened.</summary>
			TooFull,

			Eaten
		}

		/// <summary>Not a thing of the food's class, nutrition's mark in UseFood.</summary>
		private const int NoNutrition = 0xFF;

		private const int FirstFoodClass = 0xB0;

		private const int LastFoodClass = 0xBF;

		private const int Candle = 0x92;

		private const int Mushroom = 0xB9;

		private const int Honeycomb = 0xBA;

		private const int Ale = 0xBB;

		private const int Water = 0xBC;

		private const int Wine = 0xBD;

		private const int MeatOnAStick = 0xBE;

		private const int PieceOfMeat = 0xB0;

		private const int BoneLeft = 0xC5;

		private const int PlantThorny = 0xCE;

		private const int PlantPlain = 0xCF;

		private const int WaxLeft = 0xD2;

		private const int Eyeball = 0x110;

		private const int DreamPlant = 0x114;

		private const int Leeches = 0x125;

		private const int BottleLeft = 0x13D;

		private const int StickLeft = 0x13E;

		private const int ThornyNutrition = 4;

		private const int PlainNutrition = 0x17;

		// The game's lines; our block counts one higher.
		private const int TooFullString = 0x8C;

		private const int LeechesString = 0xF4;

		private const int CandleString = 0xF5;

		private const int EyeballString = 0xF6;

		private const int MushroomString = 0xF7;

		private const int PlainString = 0xF8;

		private const int ThornyString = 0xFB;

		private const int WaterString = 0xFC;

		private const int WineString = 0xFD;

		private const int AleString = 0xFE;

		/// <summary>"You quaff the potion in one gulp." (0xFF).</summary>
		private const int QuaffString = 0xFF;

		// play_effect_here's sounds in UseFood.
		public const int DrinkSound = 0x1E;

		public const int FullSound = 0x25;

		public const int FedSound = 0x1F;

		public const int HungrySound = 0x21;

		private const int FullHungerFrom = 0xC0;

		private const int FedHungerAbove = 0x5A;

		private const int PassOutString = 0x100;

		private const int RefreshedString = 0x101;

		private const int WakeString = 0x102;

		/// <summary>The first of the tastes, " tasted putrid." (0xBB).</summary>
		private const int FirstTasteString = 0xBB;

		/// <summary>The same in our numbering (one higher), for UWItemMessages.Taste and the game-text pack's
		/// pieces of a frame (UWTextFragments).</summary>
		internal const int FirstTasteMessage = FirstTasteString + 1;

		private const int LastTasteStep = 4;

		private const int TasteRoll = 20;

		/// <summary>The drink's limits: only a stronger drink than -1 hits, 0x7F is no drink.</summary>
		private const int WeakestDrink = -1;

		private const int NoDrink = -0x7F;

		/// <summary>What UseFood takes as eaten when used from the pack: the food's class, and the plants USEITEMS
		/// sends there.</summary>
		public static bool IsEatenOnUse(int piId)
		{
			return (piId >= FirstFoodClass && piId <= LastFoodClass) || piId == PlantThorny || piId == PlantPlain || piId == DreamPlant
				|| UWCarriedSpellRulesUw2.IsPotion(piId);
		}

		/// <summary>The things dropped on the head (ItemFitsSlot's slot 0): those, and the rest of UseFood's list.</summary>
		public static bool IsEatenFromHead(int piId)
		{
			return IsEatenOnUse(piId) || piId == Candle || piId == Eyeball || piId == Leeches;
		}

		/// <summary>The thing the meal leaves, 0 for none.</summary>
		public static int LeftoverOf(int piId)
		{
			switch (piId)
			{
				case MeatOnAStick: return StickLeft;
				case PieceOfMeat: return BoneLeft;
				case Honeycomb: return WaxLeft;
				case Ale:
				case Water:
				case Wine: return BottleLeft;
				default: return 0;
			}
		}

		private static int fLineOf(int piId, ref int piNutrition)
		{
			switch (piId)
			{
				case Mushroom: return MushroomString;
				case Eyeball: return EyeballString;
				case Candle: return CandleString;
				case Leeches: return LeechesString;
				case Ale: return AleString;
				case Wine: return WineString;
				case Water: return WaterString;

				case PlantThorny:
					piNutrition = ThornyNutrition;

					return ThornyString;

				case PlantPlain:
				case DreamPlant:
					piNutrition = PlainNutrition;

					return PlainString;

				default:
					return UWCarriedSpellRulesUw2.IsPotion(piId) ? QuaffString : 0;
			}
		}

		/// <summary>The meal. pfPassOut: what follows when the drink wins (the sleep); the player's lines go to pfSay.</summary>
		public static OutcomeEnum Eat(UWObject pOFood, DataImport pOData, UWPlayerVitals pOVitals, Action<string> pfSay, Action pfPassOut,
			Action<int> pfSound = null)
		{
			int liId = pOFood.ID;
			int liNutrition = liId >= FirstFoodClass && liId <= LastFoodClass ? pOData.ObjectClassProperties.GetFoodNutrition(liId) : NoNutrition;
			int liLine = fLineOf(liId, ref liNutrition);

			if (liNutrition == NoNutrition && liLine == 0)
				return OutcomeEnum.NotFood;

			if (liId == Mushroom)
				UWItemUse.MushroomEffects(pOVitals);

			if (liNutrition > 0)
			{
				if (liNutrition != NoNutrition && !pOVitals.ChangeHunger(liNutrition))
				{
					pfSay(fLine(pOData, TooFullString));

					return OutcomeEnum.TooFull;
				}

				if (pfSound != null)
					pfSound(UWCarriedSpellRulesUw2.IsPotion(liId) ? DrinkSound
						: pOVitals.Hunger >= FullHungerFrom ? FullSound
						: pOVitals.Hunger > FedHungerAbove ? FedSound
						: HungrySound);

				if (liLine != 0)
				{
					pfSay(fLine(pOData, liLine));

					return OutcomeEnum.Eaten;
				}

				int liTaste = Math.Min(LastTasteStep, ((UWRandom.Next(UWOriginalRandom.Range) * TasteRoll / UWOriginalRandom.Range) + pOFood.Quality) >> 4);

				pfSay(UWItemMessages.Taste(pOData, liId, liTaste, FirstTasteMessage));

				return OutcomeEnum.Eaten;
			}

			if (liNutrition < 0 && pfSound != null)
				pfSound(DrinkSound);

			pfSay(fLine(pOData, liLine));

			if (liNutrition < WeakestDrink && liNutrition > NoDrink)
				fDrink(pOData, pOVitals, -liNutrition, pfSay, pfPassOut);

			return OutcomeEnum.Eaten;
		}

		private static void fDrink(DataImport pOData, UWPlayerVitals pOVitals, int piStrength, Action<string> pfSay, Action pfPassOut)
		{
			UWPlayerVitals.DrinkResult leResult = pOVitals.Drink(piStrength);

			if (leResult == UWPlayerVitals.DrinkResult.Refreshing)
				pfSay(fLine(pOData, RefreshedString));

			if (leResult != UWPlayerVitals.DrinkResult.PassedOut)
				return;

			pfSay(fLine(pOData, PassOutString));

			if (pfPassOut != null)
				pfPassOut();

			if (pOVitals.CurrentHP > 0)
			{
				pfSay(fLine(pOData, WakeString));
				pOVitals.ShakeAfterPassingOut();
			}
		}

		private static string fLine(DataImport pOData, int piString)
		{
			return UWText.Block(pOData, 1, piString + 1, string.Empty);
		}
	}
}
