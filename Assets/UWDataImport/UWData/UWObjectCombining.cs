using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Combination rules from "cmb.dat" (uw-formats.txt 6.4): which two items can be
	/// combined in the backpack into a third, e.g. pole and strong thread into a fishing
	/// pole.
	///
	/// Three 16-bit words per rule - source 1, source 2, result. The lower 9 bits are
	/// the object id, the top bit of a source says whether that item is consumed in the
	/// process. At least one of the two is always consumed. Three zero words
	/// terminate the table.
	///
	/// The order does not matter: the rule also applies when source 2 is used on
	/// source 1.
	/// </summary>
	public class UWObjectCombining
	{
		public struct Rule
		{
			public int FirstObjectId;

			public bool FirstIsConsumed;

			public int SecondObjectId;

			public bool SecondIsConsumed;

			public int ResultObjectId;
		}

		private const int ObjectIdMask = 0x1FF;

		private const int ConsumedFlag = 0x8000;

		private readonly List<Rule> mORules = new List<Rule>();

		public IReadOnlyList<Rule> Rules => mORules;

		public bool IsLoaded { get; private set; }

		public UWObjectCombining(string psDataPath)
		{
			string lsFile = Path.Combine(psDataPath, "CMB.DAT");

			if (!File.Exists(lsFile))
				return;

			byte[] lyData = File.ReadAllBytes(lsFile);

			for (int liOffset = 0; liOffset + 5 < lyData.Length; liOffset += 6)
			{
				int liFirst = lyData[liOffset] | (lyData[liOffset + 1] << 8);
				int liSecond = lyData[liOffset + 2] | (lyData[liOffset + 3] << 8);
				int liResult = lyData[liOffset + 4] | (lyData[liOffset + 5] << 8);

				if (liFirst == 0 && liSecond == 0 && liResult == 0)
					break;

				mORules.Add(new Rule
				{
					FirstObjectId = liFirst & ObjectIdMask,
					FirstIsConsumed = (liFirst & ConsumedFlag) != 0,
					SecondObjectId = liSecond & ObjectIdMask,
					SecondIsConsumed = (liSecond & ConsumedFlag) != 0,
					ResultObjectId = liResult & ObjectIdMask
				});
			}

			IsLoaded = true;
		}

		/// <summary>Looks up the rule for two items, in any order.
		/// pbFirstIsConsumed and pbSecondIsConsumed refer to the order passed in,
		/// not to the order in the file.</summary>
		public bool TryCombine(int piFirstObjectId, int piSecondObjectId, out int piResultObjectId,
			out bool pbFirstIsConsumed, out bool pbSecondIsConsumed)
		{
			foreach (Rule lORule in mORules)
			{
				if (lORule.FirstObjectId == piFirstObjectId && lORule.SecondObjectId == piSecondObjectId)
				{
					piResultObjectId = lORule.ResultObjectId;
					pbFirstIsConsumed = lORule.FirstIsConsumed;
					pbSecondIsConsumed = lORule.SecondIsConsumed;
					return true;
				}

				if (lORule.FirstObjectId == piSecondObjectId && lORule.SecondObjectId == piFirstObjectId)
				{
					piResultObjectId = lORule.ResultObjectId;
					pbFirstIsConsumed = lORule.SecondIsConsumed;
					pbSecondIsConsumed = lORule.FirstIsConsumed;
					return true;
				}
			}

			piResultObjectId = 0;
			pbFirstIsConsumed = false;
			pbSecondIsConsumed = false;
			return false;
		}
	}
}
