using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A store of numbered values of one session - the quest flags and the game variables
	/// (UWGameSession.Quests and .Variables; cleanup step 2.7, 2026-10-14). Unset numbers read as
	/// zero; All lists only what has been set, in the order it was first set, as the static
	/// dictionaries of UWQuestFlags and UWGameVariables did before.
	/// </summary>
	public sealed class UWSessionNumbers
	{
		/// <summary>No mask: the value is stored as given.</summary>
		public const int NoMask = -1;

		private readonly Dictionary<int, int> mOValues = new Dictionary<int, int>();

		private readonly int miMask;

		/// <summary>piMask is ANDed into every value stored (the game variables keep six bits).</summary>
		public UWSessionNumbers(int piMask)
		{
			miMask = piMask;
		}

		public int Get(int piNumber)
		{
			int liValue;

			return mOValues.TryGetValue(piNumber, out liValue) ? liValue : 0;
		}

		public void Set(int piNumber, int piValue)
		{
			mOValues[piNumber] = piValue & miMask;
		}

		/// <summary>Every number that has been set, for debugging.</summary>
		public IReadOnlyDictionary<int, int> All
		{
			get { return mOValues; }
		}

		public void Clear()
		{
			mOValues.Clear();
		}
	}
}
