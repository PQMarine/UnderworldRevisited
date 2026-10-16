using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// What happens when an item is used on the character itself, as an ordered list of
	/// (predicate, handler) rows: the first row whose predicate holds takes the item, and its
	/// handler's answer is the answer of the use - unless the row passes on when its handler
	/// declines (the charged-item row: a spell that cannot be cast leaves the item to the rows
	/// after it). Since 2026-10-14 (cleanup step 2.10) instead of the precedence written out in
	/// UWItemUse.TryUseImmediately and the id if-chain of its fTryUseOnSelf; the rows are
	/// UWItemUse's (UWItemUse.UseTable), in the order those two had.
	///
	/// The table is data, built once and sealed; it holds no game state.
	/// </summary>
	public sealed class UWItemUseTable
	{
		/// <summary>One use: the item and what the rows work with.</summary>
		public sealed class Use
		{
			public readonly UWObject Item;

			public readonly DataImport Data;

			public readonly UWPlayerVitals Vitals;

			public readonly UWInventoryModel Inventory;

			public readonly IUWItemUseHost Host;

			public Use(UWObject pOItem, DataImport pOData, UWPlayerVitals pOVitals, UWInventoryModel pOInventory,
				IUWItemUseHost pIHost)
			{
				Item = pOItem;
				Data = pOData;
				Vitals = pOVitals;
				Inventory = pOInventory;
				Host = pIHost;
			}
		}

		public delegate bool Predicate(Use pOUse);

		/// <summary>True when the use is handled.</summary>
		public delegate bool Handler(Use pOUse);

		public readonly struct Row
		{
			public readonly string Name;

			public readonly Predicate Applies;

			public readonly Handler Run;

			/// <summary>When the handler answers false, the rows after this one still get the item.</summary>
			public readonly bool PassesOnWhenDeclined;

			public Row(string psName, Predicate pOApplies, Handler pORun, bool pbPassesOnWhenDeclined)
			{
				Name = psName;
				Applies = pOApplies;
				Run = pORun;
				PassesOnWhenDeclined = pbPassesOnWhenDeclined;
			}
		}

		private readonly List<Row> mORows = new List<Row>();

		private bool mbSealed;

		/// <summary>Appends a row; the order of the calls is the order of precedence.</summary>
		public UWItemUseTable Add(string psName, Predicate pOApplies, Handler pORun, bool pbPassesOnWhenDeclined = false)
		{
			if (mbSealed)
				throw new InvalidOperationException("The table is sealed.");

			mORows.Add(new Row(psName, pOApplies ?? throw new ArgumentNullException(nameof(pOApplies)),
				pORun ?? throw new ArgumentNullException(nameof(pORun)), pbPassesOnWhenDeclined));

			return this;
		}

		/// <summary>After this, Add throws.</summary>
		public void Seal()
		{
			mbSealed = true;
		}

		public IReadOnlyList<Row> Rows
		{
			get { return mORows; }
		}

		/// <summary>Runs the first row that applies (and the next ones while a row passes on).
		/// False when no row handles the item.</summary>
		public bool TryUse(Use pOUse)
		{
			foreach (Row lORow in mORows)
			{
				if (!lORow.Applies(pOUse))
					continue;

				bool lbHandled = lORow.Run(pOUse);

				if (lbHandled || !lORow.PassesOnWhenDeclined)
					return lbHandled;
			}

			return false;
		}

		/// <summary>The index of the first row whose predicate holds, or -1 - for the
		/// dispatch-coverage check; runs no handler.</summary>
		public int FindRow(Use pOUse)
		{
			for (int liAt = 0; liAt < mORows.Count; liAt++)
			{
				if (mORows[liAt].Applies(pOUse))
					return liAt;
			}

			return -1;
		}
	}
}
