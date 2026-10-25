using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>What a setting is to the one who changes it (cleanup step 8.3, 2026-10-24): the kind
	/// of row it gets. The stored type is the def's own (UWIntSettingDef for a Choice, or for a
	/// switch kept as a number like MotionSmoothing).</summary>
	public enum UWSettingKind
	{
		/// <summary>On or off.</summary>
		Bool,

		/// <summary>A whole number.</summary>
		Int,

		Float,

		/// <summary>One of a few numbered choices: a mode, a device, a preset, a tab.</summary>
		Choice,

		Text,

		/// <summary>Lines of text (the changed key bindings).</summary>
		List
	}

	/// <summary>Where a setting lives (cleanup step 8.3, 2026-10-24).</summary>
	public enum UWSettingStore
	{
		/// <summary>settings.json, through UWUserSettings.</summary>
		User,

		/// <summary>settings.json as well, and part of the modern interface's layout: the layout
		/// editor's snapshot takes exactly these (UWUserSettings.TakeLayoutSnapshot).</summary>
		Layout,

		/// <summary>graphics.json (UWGraphicsDetail). No entries yet.</summary>
		Graphics,

		/// <summary>The live values of the UWSettings asset, the developer's tuning. No entries
		/// yet.</summary>
		Live
	}

	/// <summary>
	/// What the game makes of a stored value (cleanup step 8.3, 2026-10-24). Each rule is one that
	/// a property of UWUserSettings applied by hand until then; they are the reading rules of older
	/// settings files and must stay as they are (Tools/Fixtures/settings pins every one of them).
	/// Reading never writes the value back: a file keeps what it holds until the player changes the
	/// setting, so a file read and saved again stays the same byte for byte.
	/// </summary>
	public enum UWSettingNormalize
	{
		/// <summary>Read and written as stored.</summary>
		None,

		/// <summary>Read and written within Min to Max.</summary>
		Clamp,

		/// <summary>Zero or below reads as Fallback (a value an older build wrote before the
		/// setting existed, or never wrote: MouseLookSpeed, StickLookSpeed, EasyMovementInterval),
		/// anything else within Min to Max; written within Min to Max.</summary>
		NotPositiveIsFallback,

		/// <summary>A value that is not one of Choices reads as Fallback (OriginalFrameTicks,
		/// SmoothRampTicks); written as given.</summary>
		OneOfOrFallback,

		/// <summary>A value outside Min to Max reads as Fallback (the three Off/Original/Smooth
		/// modes read Original); written as given.</summary>
		InRangeOrFallback,

		/// <summary>A switch kept as a number: anything but 0 reads as on; written as 1 or 0
		/// (MotionSmoothing, MotionViewStepped).</summary>
		NonZeroIsOn,

		/// <summary>Below 0 is "not chosen yet" and reads as what WhenUnset works out from the
		/// other settings (InterfacePreset); written as given.</summary>
		UnsetIsComputed,

		/// <summary>No text reads and is written as the empty one.</summary>
		NullIsEmpty,

		/// <summary>No list reads as a new empty one, which stays (KeyBindings).</summary>
		NullIsNewList
	}

	/// <summary>How a row shows a number beside its slider or between its - and + (cleanup step 8.4,
	/// 2026-10-24). The drawer writes the text; the format also says by how much a value must move
	/// to count as changed, so the file is written once a shown step changes and not on every frame
	/// of a drag (as each slider row of the setup dialogs did by hand until then).</summary>
	public enum UWSettingFormat
	{
		/// <summary>No value shown.</summary>
		None,

		/// <summary>A share as whole per cent, "85 %": the volumes, the look speeds, the strengths,
		/// the deadzones. Changed when the per cent changes.</summary>
		Percent,

		/// <summary>"0.85": the colour help. Changed when the value moves at all.</summary>
		TwoDecimals,

		/// <summary>Seconds as whole milliseconds, "250 ms": the easy movement.</summary>
		Milliseconds,

		/// <summary>Whole degrees, "90°": the view angle.</summary>
		Degrees,

		/// <summary>A whole multiple, "2x": the world's resolution.</summary>
		Factor,

		/// <summary>The palette's shade steps, "1.5 steps", "Off" at 0. Changed when the value
		/// moves at all.</summary>
		ShadeSteps,

		/// <summary>A whole number kept as per cent, "120 %": the layout editor's sizes.</summary>
		WholePercent,

		/// <summary>A whole number as it is: the layout editor's widths and lines.</summary>
		Number
	}

	/// <summary>
	/// ONE SETTING OF settings.json, DESCRIBED (cleanup step 8.3, 2026-10-24; Docs/CLEANUP-PLAN.md
	/// 8.7): its key, which is the JSON field name and the UWUserData field and is never renamed,
	/// what kind of row it is, where it lives, its limits and the rule by which a stored value is
	/// read (Normalize). UWUserSettingsTable lists one per field of UWUserData, in the file's order.
	///
	/// WHAT THE PLAYER SEES OF IT (cleanup step 8.4, 2026-10-24): its label, hint and choice texts
	/// with a stable id each (the text table of step 8.5, UWText, maps the ids), how a
	/// number is shown (Format), when its row is greyed (EnabledWhen) or left out (VisibleWhen),
	/// and what a change through a row means beyond the value (OnChange). The setup dialogs' page
	/// tables (UWSetupPage) and the layout editor's inspector take them from here; a row may still
	/// say it otherwise where one setting has two rows. Nothing here draws.
	/// </summary>
	public abstract class UWSettingDef
	{
		/// <summary>The JSON field name and the UWUserData field - never renamed.</summary>
		public readonly string Key;

		public readonly UWSettingKind Kind;

		public UWSettingStore Store = UWSettingStore.User;

		public UWSettingNormalize Normalize = UWSettingNormalize.None;

		/// <summary>The row's label, its id for the text table ("setting.Key").</summary>
		public string LabelId;

		/// <summary>The label in the language in force (UWText, step 8.5), null without an id.</summary>
		public string Label => LabelId == null ? null : UWText.T(LabelId);

		/// <summary>The explanation under the row, null where it has none; its id
		/// ("setting.Key.hint").</summary>
		public string HintId;

		public string Hint => HintId == null ? null : UWText.T(HintId);

		/// <summary>A choice row's texts, one per choice in the order of the choices (for an int
		/// setting with Choices one per value there, else one per number from 0; for a switch first
		/// off, then on - unless Invert); their ids ("setting.Key.choice").</summary>
		public string[] ChoiceIds;

		private string[] msChoiceLabels;

		private int miChoiceLabelsOf = -1;

		/// <summary>The choice texts in the language in force, null without ids. Kept until the
		/// language changes (UWText.LanguageVersion): a row asks for them on every pass.</summary>
		public string[] ChoiceLabels
		{
			get
			{
				if (ChoiceIds == null)
					return null;

				if (msChoiceLabels == null || miChoiceLabelsOf != UWText.LanguageVersion)
				{
					msChoiceLabels = Array.ConvertAll(ChoiceIds, UWText.T);
					miChoiceLabelsOf = UWText.LanguageVersion;
				}

				return msChoiceLabels;
			}
		}

		/// <summary>A switch shown the other way round: the stored true is the row's first choice
		/// (the layout's "hidden" switches shown as Shown On / Off).</summary>
		public bool Invert;

		public UWSettingFormat Format = UWSettingFormat.None;

		/// <summary>The row takes input only while this holds (null: always), else it is drawn
		/// greyed - a setting without effect is greyed (per user, 2026-10-06).</summary>
		public Func<UWUserData, bool> EnabledWhen;

		/// <summary>The row is there only while this holds (null: always).</summary>
		public Func<UWUserData, bool> VisibleWhen;

		/// <summary>What else a change through a row does, after the value is stored and the file
		/// written (null: nothing). The engine side hangs its effects on here (UWSetupPage).</summary>
		public Action<UWUserData> OnChange;

		/// <summary>Whether the row is greyed for these settings.</summary>
		public bool IsEnabled(UWUserData pOData)
		{
			return EnabledWhen == null || EnabledWhen(pOData);
		}

		public bool IsVisible(UWUserData pOData)
		{
			return VisibleWhen == null || VisibleWhen(pOData);
		}

		protected UWSettingDef(string psKey, UWSettingKind peKind)
		{
			Key = psKey;
			Kind = peKind;
		}

		/// <summary>The stored value, boxed, exactly as the file holds it - for the layout
		/// snapshot and the self-check. The game reads through the typed Read.</summary>
		public abstract object RawOf(UWUserData pOData);

		/// <summary>Puts a stored value back as it was taken by RawOf.</summary>
		public abstract void SetRaw(UWUserData pOData, object pOValue);

		/// <summary>The value a fresh start has (the field's initializer in UWUserData).</summary>
		public object Default
		{
			get { return RawOf(new UWUserData()); }
		}

		/// <summary>Mathf.Clamp's rule, without the engine: below the minimum the minimum, above
		/// the maximum the maximum, anything else (NaN too) as it is.</summary>
		public static float Clamp(float pfValue, float pfMin, float pfMax)
		{
			if (pfValue < pfMin)
				return pfMin;

			if (pfValue > pfMax)
				return pfMax;

			return pfValue;
		}

		public static int Clamp(int piValue, int piMin, int piMax)
		{
			if (piValue < piMin)
				return piMin;

			if (piValue > piMax)
				return piMax;

			return piValue;
		}

		public override string ToString()
		{
			return Key;
		}
	}

	/// <summary>A setting kept as a float.</summary>
	public sealed class UWFloatSettingDef : UWSettingDef
	{
		public float Min;

		public float Max;

		/// <summary>What a slider and its arrows move by; 0 = no step of its own.</summary>
		public float Step;

		/// <summary>What NotPositiveIsFallback reads instead of zero or below.</summary>
		public float Fallback;

		private readonly Func<UWUserData, float> mFGet;

		private readonly Action<UWUserData, float> mFSet;

		public UWFloatSettingDef(string psKey, Func<UWUserData, float> pFGet, Action<UWUserData, float> pFSet)
			: base(psKey, UWSettingKind.Float)
		{
			mFGet = pFGet;
			mFSet = pFSet;
		}

		/// <summary>The setting as the game uses it.</summary>
		public float Read(UWUserData pOData)
		{
			return ReadValue(mFGet(pOData));
		}

		/// <summary>Stores a new value, within the limits where the setting has them.</summary>
		public void Write(UWUserData pOData, float pfValue)
		{
			mFSet(pOData, WriteValue(pfValue));
		}

		/// <summary>A stored number as the game reads it.</summary>
		public float ReadValue(float pfStored)
		{
			switch (Normalize)
			{
				case UWSettingNormalize.Clamp:
					return Clamp(pfStored, Min, Max);

				case UWSettingNormalize.NotPositiveIsFallback:
					return Clamp(pfStored <= 0f ? Fallback : pfStored, Min, Max);

				default:
					return pfStored;
			}
		}

		/// <summary>A number as it is stored.</summary>
		public float WriteValue(float pfValue)
		{
			return Normalize == UWSettingNormalize.None ? pfValue : Clamp(pfValue, Min, Max);
		}

		public override object RawOf(UWUserData pOData)
		{
			return mFGet(pOData);
		}

		public override void SetRaw(UWUserData pOData, object pOValue)
		{
			mFSet(pOData, (float)pOValue);
		}
	}

	/// <summary>A setting kept as an int: a number, a numbered choice, or a switch kept as a
	/// number (NonZeroIsOn).</summary>
	public sealed class UWIntSettingDef : UWSettingDef
	{
		public int Min;

		public int Max;

		/// <summary>What OneOfOrFallback and InRangeOrFallback read instead of a value they do not
		/// take.</summary>
		public int Fallback;

		/// <summary>The values OneOfOrFallback takes.</summary>
		public int[] Choices;

		/// <summary>The number that means "not chosen yet" when the setting has one (-1), else
		/// null. ControlScheme and DisplayMode read it as it is - their owners decide what it
		/// means (UWControlScheme, UWDisplayMode); InterfacePreset works it out (WhenUnset).</summary>
		public int? Unset;

		/// <summary>What UnsetIsComputed reads for a stored value below 0.</summary>
		public Func<UWUserData, int> WhenUnset;

		private readonly Func<UWUserData, int> mFGet;

		private readonly Action<UWUserData, int> mFSet;

		public UWIntSettingDef(string psKey, UWSettingKind peKind, Func<UWUserData, int> pFGet, Action<UWUserData, int> pFSet)
			: base(psKey, peKind)
		{
			mFGet = pFGet;
			mFSet = pFSet;
		}

		/// <summary>The setting as the game uses it.</summary>
		public int Read(UWUserData pOData)
		{
			int liStored = mFGet(pOData);

			switch (Normalize)
			{
				case UWSettingNormalize.Clamp:
					return Clamp(liStored, Min, Max);

				case UWSettingNormalize.OneOfOrFallback:
					return Array.IndexOf(Choices, liStored) >= 0 ? liStored : Fallback;

				case UWSettingNormalize.InRangeOrFallback:
					return liStored >= Min && liStored <= Max ? liStored : Fallback;

				case UWSettingNormalize.UnsetIsComputed:
					return liStored < 0 ? WhenUnset(pOData) : liStored;

				default:
					return liStored;
			}
		}

		/// <summary>Stores a new value, within the limits where the setting has them.</summary>
		public void Write(UWUserData pOData, int piValue)
		{
			mFSet(pOData, Normalize == UWSettingNormalize.Clamp ? Clamp(piValue, Min, Max) : piValue);
		}

		/// <summary>A switch kept as a number (NonZeroIsOn): anything but 0 is on.</summary>
		public bool ReadOn(UWUserData pOData)
		{
			return mFGet(pOData) != 0;
		}

		/// <summary>Stores a switch kept as a number: 1 or 0.</summary>
		public void WriteOn(UWUserData pOData, bool pbOn)
		{
			mFSet(pOData, pbOn ? 1 : 0);
		}

		/// <summary>The choice a row shows for the stored value: its place among Choices where the
		/// setting has them (-1 for none of them), else the number itself (cleanup step 8.4).</summary>
		public int IndexOf(UWUserData pOData)
		{
			int liValue = Read(pOData);

			return Choices != null ? Array.IndexOf(Choices, liValue) : liValue;
		}

		/// <summary>The value a row's choice stores: the one at that place among Choices, else the
		/// number itself.</summary>
		public int ValueAt(int piIndex)
		{
			return Choices != null ? Choices[piIndex] : piIndex;
		}

		public override object RawOf(UWUserData pOData)
		{
			return mFGet(pOData);
		}

		public override void SetRaw(UWUserData pOData, object pOValue)
		{
			mFSet(pOData, (int)pOValue);
		}
	}

	/// <summary>A switch kept as a bool.</summary>
	public sealed class UWBoolSettingDef : UWSettingDef
	{
		private readonly Func<UWUserData, bool> mFGet;

		private readonly Action<UWUserData, bool> mFSet;

		public UWBoolSettingDef(string psKey, Func<UWUserData, bool> pFGet, Action<UWUserData, bool> pFSet)
			: base(psKey, UWSettingKind.Bool)
		{
			mFGet = pFGet;
			mFSet = pFSet;
		}

		public bool Read(UWUserData pOData)
		{
			return mFGet(pOData);
		}

		public void Write(UWUserData pOData, bool pbValue)
		{
			mFSet(pOData, pbValue);
		}

		public override object RawOf(UWUserData pOData)
		{
			return mFGet(pOData);
		}

		public override void SetRaw(UWUserData pOData, object pOValue)
		{
			mFSet(pOData, (bool)pOValue);
		}
	}

	/// <summary>A text: a path, or one of the layout's opaque strings (ModernLayout, ModernBacks -
	/// their owners read them, the table only keeps them).</summary>
	public sealed class UWTextSettingDef : UWSettingDef
	{
		private readonly Func<UWUserData, string> mFGet;

		private readonly Action<UWUserData, string> mFSet;

		public UWTextSettingDef(string psKey, Func<UWUserData, string> pFGet, Action<UWUserData, string> pFSet)
			: base(psKey, UWSettingKind.Text)
		{
			mFGet = pFGet;
			mFSet = pFSet;
		}

		public string Read(UWUserData pOData)
		{
			string lsStored = mFGet(pOData);

			return Normalize == UWSettingNormalize.NullIsEmpty ? lsStored ?? string.Empty : lsStored;
		}

		public void Write(UWUserData pOData, string psValue)
		{
			mFSet(pOData, Normalize == UWSettingNormalize.NullIsEmpty ? psValue ?? string.Empty : psValue);
		}

		public override object RawOf(UWUserData pOData)
		{
			return mFGet(pOData);
		}

		public override void SetRaw(UWUserData pOData, object pOValue)
		{
			mFSet(pOData, (string)pOValue);
		}
	}

	/// <summary>Lines of text, changed in place by their owner (UWKeyBindings).</summary>
	public sealed class UWListSettingDef : UWSettingDef
	{
		private readonly Func<UWUserData, List<string>> mFGet;

		private readonly Action<UWUserData, List<string>> mFSet;

		public UWListSettingDef(string psKey, Func<UWUserData, List<string>> pFGet, Action<UWUserData, List<string>> pFSet)
			: base(psKey, UWSettingKind.List)
		{
			mFGet = pFGet;
			mFSet = pFSet;
		}

		/// <summary>The list itself, to change in place; NullIsNewList puts an empty one in where
		/// there is none.</summary>
		public List<string> Read(UWUserData pOData)
		{
			List<string> lOList = mFGet(pOData);

			if (lOList == null && Normalize == UWSettingNormalize.NullIsNewList)
			{
				lOList = new List<string>();
				mFSet(pOData, lOList);
			}

			return lOList;
		}

		public override object RawOf(UWUserData pOData)
		{
			return mFGet(pOData);
		}

		public override void SetRaw(UWUserData pOData, object pOValue)
		{
			mFSet(pOData, (List<string>)pOValue);
		}
	}
}
