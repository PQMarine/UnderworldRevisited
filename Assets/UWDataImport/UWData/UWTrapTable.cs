using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// One trap kind or one a_do action: the trap, the trigger that supplies the target tile, the
	/// switch that started the chain (the eight-position switch reads its flags), and the host.
	/// True means the kind is built and did its work; false is recorded as NOT BUILT in the trap
	/// log (UWTrapLog). The chain goes on either way (UWTrapRules.fRunTrapChain).
	/// </summary>
	public delegate bool UWTrapHandler(UWObject pOTrap, UWObject pOTrigger, UWObject pOSwitchObject, IUWTrapHost pIHost);

	/// <summary>
	/// A fixed array of trap handlers with a name per entry, the common part of UWTrapTable and
	/// UWDoActionTable. Filled once and then sealed (2026-10-14, cleanup step 2.10): the tables
	/// are data, not global state, so a later UWGameSession can hold its own copy and a mod can
	/// register into an unsealed one before the session starts.
	/// </summary>
	public abstract class UWTrapHandlerArray
	{
		private readonly UWTrapHandler[] mOHandlers;

		private readonly string[] msNames;

		private bool mbSealed;

		protected UWTrapHandlerArray(int piCount)
		{
			mOHandlers = new UWTrapHandler[piCount];
			msNames = new string[piCount];
		}

		/// <summary>How many entries the array has (32 kinds, 64 actions).</summary>
		public int Count
		{
			get { return mOHandlers.Length; }
		}

		/// <summary>After this, Register throws.</summary>
		public void Seal()
		{
			mbSealed = true;
		}

		/// <summary>The entry for a key, or -1 when the key is outside the array.</summary>
		protected abstract int IndexOf(int piKey);

		/// <summary>Registers the handler of one key. A key may be registered once.</summary>
		protected void RegisterAt(int piKey, string psName, UWTrapHandler pOHandler)
		{
			if (mbSealed)
				throw new InvalidOperationException("The table is sealed.");

			int liIndex = IndexOf(piKey);

			if (liIndex < 0)
				throw new ArgumentOutOfRangeException(nameof(piKey), piKey, "outside the table");

			if (mOHandlers[liIndex] != null)
				throw new InvalidOperationException(string.Format("0x{0:X} is registered twice.", piKey));

			mOHandlers[liIndex] = pOHandler ?? throw new ArgumentNullException(nameof(pOHandler));
			msNames[liIndex] = psName;
		}

		/// <summary>Whether a handler is registered for this key.</summary>
		public bool IsHandled(int piKey)
		{
			int liIndex = IndexOf(piKey);

			return liIndex >= 0 && mOHandlers[liIndex] != null;
		}

		/// <summary>The name the handler was registered with, or null.</summary>
		public string NameOf(int piKey)
		{
			int liIndex = IndexOf(piKey);

			return liIndex >= 0 ? msNames[liIndex] : null;
		}

		/// <summary>Runs the handler of the key; false (not built) when there is none.</summary>
		protected bool RunAt(int piKey, UWObject pOTrap, UWObject pOTrigger, UWObject pOSwitchObject, IUWTrapHost pIHost)
		{
			int liIndex = IndexOf(piKey);
			UWTrapHandler lOHandler = liIndex >= 0 ? mOHandlers[liIndex] : null;

			return lOHandler != null && lOHandler(pOTrap, pOTrigger, pOSwitchObject, pIHost);
		}
	}

	/// <summary>
	/// The trap kinds by object id - the switch over OBJ_INMAJOR(trap) in UseTrap (TRIGGER.C):
	/// 32 entries for 0x180-0x19F, indexed by id &amp; 0x1F. Until 2026-10-14 a chain of fifteen
	/// "if (ID == ...)" in UWTrapRules.fRunTrapInner. A kind without an entry - and any object
	/// that is not a trap at all - is not built: the log says so and the chain goes on, as
	/// UseTrap does for the kinds it has no case for.
	/// </summary>
	public sealed class UWTrapTable : UWTrapHandlerArray
	{
		/// <summary>The first trap id (a_damage trap).</summary>
		public const int FirstId = UWObjectIds.FirstTrigger - KindCount;

		/// <summary>0x180-0x19F; from 0x1A0 on they are triggers.</summary>
		public const int KindCount = 32;

		public UWTrapTable()
			: base(KindCount)
		{
		}

		protected override int IndexOf(int piKey)
		{
			return piKey >= FirstId && piKey < FirstId + KindCount ? piKey & (KindCount - 1) : -1;
		}

		public UWTrapTable Register(int piTrapId, string psName, UWTrapHandler pOHandler)
		{
			RegisterAt(piTrapId, psName, pOHandler);

			return this;
		}

		/// <summary>Fires the trap by its kind. False when the kind is not built.</summary>
		public bool Run(UWObject pOTrap, UWObject pOTrigger, UWObject pOSwitchObject, IUWTrapHost pIHost)
		{
			return pOTrap != null && RunAt(pOTrap.ID, pOTrap, pOTrigger, pOSwitchObject, pIHost);
		}
	}

	/// <summary>
	/// The a_do trap's actions by quality - the switch in do_trap_hack (TRIGGER.C): 64 entries,
	/// one per six-bit quality. Until 2026-10-14 a switch nested in UWTrapRules.fRunTrapInner.
	/// A quality without an entry is not built (the original returns 2 and does nothing).
	/// </summary>
	public sealed class UWDoActionTable : UWTrapHandlerArray
	{
		/// <summary>The quality field has six bits.</summary>
		public const int ActionCount = 64;

		public UWDoActionTable()
			: base(ActionCount)
		{
		}

		protected override int IndexOf(int piKey)
		{
			return piKey >= 0 && piKey < ActionCount ? piKey : -1;
		}

		public UWDoActionTable Register(int piQuality, string psName, UWTrapHandler pOHandler)
		{
			RegisterAt(piQuality, psName, pOHandler);

			return this;
		}

		/// <summary>Runs the action the trap's quality names. False when it is not built.</summary>
		public bool Run(UWObject pOTrap, UWObject pOTrigger, UWObject pOSwitchObject, IUWTrapHost pIHost)
		{
			return pOTrap != null && RunAt(pOTrap.Quality, pOTrap, pOTrigger, pOSwitchObject, pIHost);
		}
	}

	/// <summary>
	/// The set-variable trap's operations by the trap's heading, each with the sign the trap log
	/// shows: UseTrap's TRAP_SET_VARIABLE case (TRIGGER.C) - add, subtract, set, and, or, xor,
	/// shift left. Until 2026-10-14 two parallel switches in UWTrapRules (fFireSetVariableTrap and
	/// fGetOperationName) that had to agree by hand. The quest-flag form (variable number zero)
	/// reads the heading differently and is not in this table (UWTrapRules.fSetQuestFlagFromTrap).
	/// </summary>
	public static class UWVariableTrapOperations
	{
		private readonly struct Operation
		{
			public readonly string Name;

			public readonly Func<int, int, int> Apply;

			public Operation(string psName, Func<int, int, int> pOApply)
			{
				Name = psName;
				Apply = pOApply;
			}
		}

		private static readonly Operation[] mOOperations =
		{
			new Operation("+", (piOld, piOperand) => piOld + piOperand),
			new Operation("-", (piOld, piOperand) => piOld - piOperand),
			new Operation(":=", (piOld, piOperand) => piOperand),
			new Operation("&", (piOld, piOperand) => piOld & piOperand),
			new Operation("|", (piOld, piOperand) => piOld | piOperand),
			new Operation("^", (piOld, piOperand) => piOld ^ piOperand),
			new Operation("<<", (piOld, piOperand) => piOld << piOperand),
		};

		/// <summary>Headings 0 to 6 are operations.</summary>
		public static int Count
		{
			get { return mOOperations.Length; }
		}

		/// <summary>The operation's sign for the trap log, "?" for an unknown heading.</summary>
		public static string NameOf(int piOperation)
		{
			return piOperation >= 0 && piOperation < mOOperations.Length ? mOOperations[piOperation].Name : "?";
		}

		/// <summary>Applies the operation; false for an unknown heading (the result is then the
		/// old value).</summary>
		public static bool TryApply(int piOperation, int piOld, int piOperand, out int piResult)
		{
			if (piOperation < 0 || piOperation >= mOOperations.Length)
			{
				piResult = piOld;

				return false;
			}

			piResult = mOOperations[piOperation].Apply(piOld, piOperand);

			return true;
		}
	}
}
