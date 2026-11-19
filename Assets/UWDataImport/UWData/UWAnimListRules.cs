using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ORIGINAL'S ANIMATION LIST WHEN SEVERAL END AT ONCE (2026-11-18, per user with two portcullises shutting at once:
	/// "Tock, Tock" in the original, one tone in ours, the animations themselves in step): toast_animobj (obj/EFFECT.C,
	/// both games) ends an entry and moves the list's LAST entry into its place, and update_animobj's loop goes on with the
	/// next index - so the entry moved in is not looked at in this step and ends one animation step later (a quarter of a
	/// second of game time, GAME_TIME &gt;&gt; 6). A moving door's sound comes with its end, so two doors shutting in the
	/// same step sound one after the other.
	///
	/// Ours keeps no such list; the doors' shut sounds of one frame are put through this order instead, as if they were
	/// the list's last entries in the order they asked.
	/// </summary>
	public static class UWAnimListRules
	{
		/// <summary>One animation step: 0x40 of the 256-a-second game clock.</summary>
		public const float StepSeconds = 0x40 / 256f;

		/// <summary>
		/// One step of update_animobj over piCount entries that all end now, in list order 0..piCount-1: which end in this
		/// step (in the order they end), and which are left for the next step (in their new list order). The left ones go
		/// through the same in the next step.
		/// </summary>
		public static void EndInOneStep(int piCount, List<int> pOEndNow, List<int> pOLeft)
		{
			pOEndNow.Clear();
			pOLeft.Clear();

			List<int> lOList = new List<int>(piCount);

			for (int liAt = 0; liAt < piCount; liAt++)
				lOList.Add(liAt);

			int liCount = piCount;

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				pOEndNow.Add(lOList[liAt]);

				if (--liCount > 0 && liCount != liAt)
					lOList[liAt] = lOList[liCount];
			}

			for (int liAt = 0; liAt < liCount; liAt++)
				pOLeft.Add(lOList[liAt]);
		}
	}
}
