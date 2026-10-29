using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The conversation functions by their import name, the way the original binds them: load_script
	/// (BABL.C) and Converse (CONVERSE.C) call bab_fun(name, function) for each built-in, and an
	/// import the game never bound runs "unbound". The virtual machine keeps its eight built-ins
	/// (UWConversationVM.fCallImported: the menus, print, babl_ask, random, say, get_quest,
	/// set_quest); everything else it hands to the host, and UWConversationSession.CallUnknown
	/// looks the name up here. A name with no entry is noted in MissingFunctions and answered with
	/// 0, as before.
	///
	/// UNTIL 2026-10-14 (cleanup step 2.10) three levels did this by hand: CallUnknown checked the
	/// function id of gronk_door, then UWConversationTrade.Handles, whose list had to agree with
	/// the switch in UWConversationTrade.Call, then its own switch. Now each function is
	/// registered once by the class that implements it (UWConversationSession.RegisterFunctions,
	/// UWConversationTrade.RegisterFunctions). gronk_door is found by its name like the rest;
	/// the dispatch-coverage check of the self-check proves that every conversation in CNV.ARK
	/// imports it under the id 0x25 the old test used, and that no CALLI reaches an id without
	/// an import, so the two keys select the same calls.
	///
	/// The table is data, built once and sealed; it holds no game state, so each game's profile owns
	/// one (IUWGameProfile.ConversationFunctions, UW2 plan phase 0 step 3) and a mod can register into
	/// an unsealed copy.
	/// </summary>
	public sealed class UWConversationFunctionTable
	{
		/// <summary>Who implements a function: the session itself or its trade
		/// (UWConversationTrade). A trade function is carried out only while the session has a
		/// trade, and an accepted deal is swapped right after it.</summary>
		public enum Provider
		{
			Session,
			Trade
		}

		/// <summary>One call from the machine: the import name and id, the argument values in
		/// push order, the pointers behind them, and the machine to read and write its memory.
		/// </summary>
		public sealed class Call
		{
			public readonly string Name;

			public readonly int FunctionId;

			public readonly IReadOnlyList<int> Arguments;

			public readonly IReadOnlyList<int> Addresses;

			public readonly UWConversationVM Vm;

			public Call(string psName, int piFunctionId, IReadOnlyList<int> pOArguments,
				IReadOnlyList<int> pOAddresses, UWConversationVM pOVm)
			{
				Name = psName;
				FunctionId = piFunctionId;
				Arguments = pOArguments;
				Addresses = pOAddresses;
				Vm = pOVm;
			}

			/// <summary>An argument by its index, 0 when the call passed fewer.</summary>
			public int Argument(int piIndex)
			{
				return UWConversationVM.Argument(Arguments, piIndex);
			}
		}

		/// <summary>A function: its result is the value of the result register.</summary>
		public delegate int Function(UWConversationSession pOSession, Call pOCall);

		public readonly struct Entry
		{
			public readonly string Name;

			public readonly Provider Provider;

			public readonly Function Function;

			public Entry(string psName, Provider peProvider, Function pOFunction)
			{
				Name = psName;
				Provider = peProvider;
				Function = pOFunction;
			}
		}

		private readonly Dictionary<string, Entry> mOEntries = new Dictionary<string, Entry>(StringComparer.Ordinal);

		private bool mbSealed;

		/// <summary>Registers a function under its import name. A name may be registered once.</summary>
		public UWConversationFunctionTable Register(string psName, Provider peProvider, Function pOFunction)
		{
			if (mbSealed)
				throw new InvalidOperationException("The table is sealed.");

			if (string.IsNullOrEmpty(psName))
				throw new ArgumentException("A function needs a name.", nameof(psName));

			if (mOEntries.ContainsKey(psName))
				throw new InvalidOperationException(psName + " is registered twice.");

			mOEntries.Add(psName, new Entry(psName, peProvider, pOFunction ?? throw new ArgumentNullException(nameof(pOFunction))));

			return this;
		}

		/// <summary>After this, Register throws.</summary>
		public void Seal()
		{
			mbSealed = true;
		}

		public bool TryGet(string psName, out Entry pOEntry)
		{
			if (psName == null)
			{
				pOEntry = default;

				return false;
			}

			return mOEntries.TryGetValue(psName, out pOEntry);
		}

		public bool Contains(string psName)
		{
			return psName != null && mOEntries.ContainsKey(psName);
		}

		/// <summary>The registered names, sorted - for the coverage check.</summary>
		public IReadOnlyList<string> Names
		{
			get
			{
				List<string> lONames = new List<string>(mOEntries.Keys);

				lONames.Sort(StringComparer.Ordinal);

				return lONames;
			}
		}

		/// <summary>The first game's functions: the session's and the trade's, sealed - a new table each
		/// call, for its profile (IUWGameProfile.ConversationFunctions).</summary>
		public static UWConversationFunctionTable CreateDefault()
		{
			UWConversationFunctionTable lOTable = new UWConversationFunctionTable();

			UWConversationSession.RegisterFunctions(lOTable);
			UWConversationTrade.RegisterFunctions(lOTable);

			lOTable.Seal();

			return lOTable;
		}
	}
}
