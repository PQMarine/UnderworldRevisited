using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE LOAD ORDER AND THE MODS SWITCHED OFF (the Mods window, per user 2026-11-09): a file order.json in the
	/// mod folder itself, beside the mods - so it travels with a portable copy, and another program (the
	/// Underworld Editor's mod manager, if it wants) can write it without touching the port's settings:
	///
	///   { "order": ["pqmarine.widescreen-weapons", "my.uw1.mod"], "disabled": ["test.hello"] }
	///
	/// The ids as the mods' mod.json name them. A mod the file does not name comes after those it names,
	/// alphabetically by id, and is on - active when installed, as before the window. A mod still comes after
	/// what it requires, whatever the order says. No file, or one that cannot be read: every mod on,
	/// alphabetically (with a warning for the latter).
	/// </summary>
	public sealed class UWModOrder
	{
		public const string FileName = "order.json";

		/// <summary>The ids in the order the mods load.</summary>
		public List<string> Order { get; } = new List<string>();

		/// <summary>The ids of the mods switched off.</summary>
		public HashSet<string> Disabled { get; } = new HashSet<string>(StringComparer.Ordinal);

		/// <summary>The order of a mod folder, empty where it has no order.json; psWarning says why one was not read.</summary>
		public static UWModOrder Read(string psFolder, out string psWarning)
		{
			psWarning = null;

			UWModOrder lOOrder = new UWModOrder();
			string lsFile = string.IsNullOrEmpty(psFolder) ? null : Path.Combine(psFolder, FileName);

			if (lsFile == null || !File.Exists(lsFile))
				return lOOrder;

			string lsText;

			try
			{
				lsText = File.ReadAllText(lsFile);
			}
			catch (Exception lOError) when (lOError is IOException || lOError is UnauthorizedAccessException)
			{
				psWarning = FileName + ": " + lOError.Message + " - every mod on, alphabetically";

				return lOOrder;
			}

			Dictionary<string, object> lOJson = UWJsonReader.TryParseObject(lsText, out string lsError);

			if (lOJson == null)
			{
				psWarning = FileName + ": " + lsError + " - every mod on, alphabetically";

				return lOOrder;
			}

			foreach (string lsId in UWJsonReader.GetStrings(lOJson, "order"))
			{
				if (!lOOrder.Order.Contains(lsId))
					lOOrder.Order.Add(lsId);
			}

			foreach (string lsId in UWJsonReader.GetStrings(lOJson, "disabled"))
				lOOrder.Disabled.Add(lsId);

			return lOOrder;
		}

		/// <summary>Writes order.json into the folder (made where it is missing); the error, or null.</summary>
		public string Write(string psFolder)
		{
			try
			{
				Directory.CreateDirectory(psFolder);
				File.WriteAllText(Path.Combine(psFolder, FileName), ToJson(), new UTF8Encoding(false));

				return null;
			}
			catch (Exception lOError) when (lOError is IOException || lOError is UnauthorizedAccessException)
			{
				return lOError.Message;
			}
		}

		/// <summary>The file's text: one id a line, the switched-off ones sorted.</summary>
		public string ToJson()
		{
			StringBuilder lOJson = new StringBuilder();
			List<string> lsDisabled = new List<string>(Disabled);

			lsDisabled.Sort(StringComparer.Ordinal);
			lOJson.Append("{\n  \"order\": [");
			fList(lOJson, Order);
			lOJson.Append("],\n  \"disabled\": [");
			fList(lOJson, lsDisabled);
			lOJson.Append("]\n}\n");

			return lOJson.ToString();
		}

		private static void fList(StringBuilder pOJson, List<string> psIds)
		{
			for (int liAt = 0; liAt < psIds.Count; liAt++)
				pOJson.Append(liAt == 0 ? "\n    \"" : ",\n    \"").Append(psIds[liAt].Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');

			if (psIds.Count > 0)
				pOJson.Append("\n  ");
		}

		/// <summary>Where an id stands: its place in Order, or after every named one.</summary>
		public int Rank(string psId)
		{
			int liAt = Order.IndexOf(psId);

			return liAt >= 0 ? liAt : int.MaxValue;
		}

		/// <summary>Sorts ids as the file orders them, the ones it does not name after them alphabetically.</summary>
		public int Compare(string psA, string psB)
		{
			int liByRank = Rank(psA).CompareTo(Rank(psB));

			return liByRank != 0 ? liByRank : string.CompareOrdinal(psA, psB);
		}
	}
}
