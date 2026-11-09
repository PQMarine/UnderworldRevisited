using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A MOD'S mod.json (Docs/WIDESCREEN-WEAPONS-PLAN.md 4.1, Docs/MOD-SUPPORT-PLAN.md 3.1; built
	/// 2026-11-09). Two kinds share the name:
	///
	///   - OURS: id, name, version, author, kind ("pictures", ...), games (["uw1"]), licence, credit,
	///     requires (ids, each with an optional lowest version: "author.mod >= 1.0");
	///   - THE EDITOR'S MOD FORMAT 2 (.uwmod, Karl Clinckspoor's Ultima Underworld editor, its
	///     docs/mod-format.md): "format": 2, id, name, version, authors, "game": "uw1", files (each a
	///     delta against one exact original, applied by a later step). It names no requirements.
	///
	/// THE ID: lower case letters, digits, '-' and '.', by convention author.mod-name, the same over
	/// versions; a mod without a valid id is not loaded.
	/// </summary>
	public sealed class UWModManifest
	{
		public const string FileName = "mod.json";

		/// <summary>The editor's packages: a zip by this extension, mod format 2.</summary>
		public const string PackageExtension = ".uwmod";

		public const int EditorFormat = 2;

		public string Id;

		public string Name;

		public string Version;

		public List<string> Authors = new List<string>();

		/// <summary>What the mod holds ("pictures", ...); "uwmod" for the editor's packages.</summary>
		public string Kind;

		/// <summary>The games it is for ("uw1", "uw2"); empty for both.</summary>
		public List<string> Games = new List<string>();

		public string Licence;

		public string Credit;

		/// <summary>The editor's mod format 2.</summary>
		public bool IsEditorPackage;

		/// <summary>The mods it needs, each with its lowest version (null for any).</summary>
		public List<Requirement> Requires = new List<Requirement>();

		public sealed class Requirement
		{
			public string Id;

			public string LowestVersion;

			public override string ToString()
			{
				return LowestVersion == null ? Id : Id + " >= " + LowestVersion;
			}
		}

		/// <summary>Whether the mod is for this game ("uw1", "uw2"; no games named = both).</summary>
		public bool IsFor(string psGame)
		{
			if (Games.Count == 0)
				return true;

			foreach (string lsGame in Games)
			{
				if (string.Equals(lsGame, psGame, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		/// <summary>The manifest of a mod.json, null with the reason where it is none.</summary>
		public static UWModManifest Parse(string psJson, out string psError)
		{
			Dictionary<string, object> lOJson = UWJsonReader.TryParseObject(psJson, out psError);

			if (lOJson == null)
				return null;

			UWModManifest lOManifest = new UWModManifest
			{
				Id = UWJsonReader.GetString(lOJson, "id"),
				Name = UWJsonReader.GetString(lOJson, "name"),
				Version = UWJsonReader.GetString(lOJson, "version"),
				Kind = UWJsonReader.GetString(lOJson, "kind"),
				Licence = UWJsonReader.GetString(lOJson, "licence") ?? UWJsonReader.GetString(lOJson, "license"),
				Credit = UWJsonReader.GetString(lOJson, "credit")
			};

			lOManifest.IsEditorPackage = (int)UWJsonReader.GetNumber(lOJson, "format", 0) == EditorFormat;

			if (lOManifest.IsEditorPackage)
			{
				lOManifest.Kind = "uwmod";
				lOManifest.Authors = UWJsonReader.GetStrings(lOJson, "authors");
				lOManifest.Games = UWJsonReader.GetStrings(lOJson, "game");
			}
			else
			{
				lOManifest.Authors = UWJsonReader.GetStrings(lOJson, "author");
				lOManifest.Authors.AddRange(UWJsonReader.GetStrings(lOJson, "authors"));
				lOManifest.Games = UWJsonReader.GetStrings(lOJson, "games");

				foreach (string lsRequirement in UWJsonReader.GetStrings(lOJson, "requires"))
				{
					Requirement lORequirement = ParseRequirement(lsRequirement);

					if (lORequirement != null)
						lOManifest.Requires.Add(lORequirement);
				}
			}

			if (lOManifest.Name == null)
				lOManifest.Name = lOManifest.Id;

			return lOManifest;
		}

		/// <summary>"author.mod" or "author.mod >= 1.0"; null where the id is no id.</summary>
		public static Requirement ParseRequirement(string psText)
		{
			string lsText = (psText ?? string.Empty).Trim();
			string lsVersion = null;
			int liAt = lsText.IndexOf(">=", StringComparison.Ordinal);

			if (liAt >= 0)
			{
				lsVersion = lsText.Substring(liAt + 2).Trim();
				lsText = lsText.Substring(0, liAt).Trim();
			}

			return IsValidId(lsText) ? new Requirement { Id = lsText, LowestVersion = string.IsNullOrEmpty(lsVersion) ? null : lsVersion } : null;
		}

		/// <summary>Lower case letters, digits, '-' and '.'; at least one character, none other.</summary>
		public static bool IsValidId(string psId)
		{
			if (string.IsNullOrEmpty(psId))
				return false;

			foreach (char lcChar in psId)
			{
				bool lbOk = (lcChar >= 'a' && lcChar <= 'z') || (lcChar >= '0' && lcChar <= '9') || lcChar == '-' || lcChar == '.';

				if (!lbOk)
					return false;
			}

			return true;
		}

		/// <summary>Versions compared part by part as numbers ("1.10" after "1.9"); a part that is no number
		/// counts as 0. Below 0 for the first lower.</summary>
		public static int CompareVersions(string psA, string psB)
		{
			string[] lsA = (psA ?? "0").Split('.');
			string[] lsB = (psB ?? "0").Split('.');
			int liParts = Math.Max(lsA.Length, lsB.Length);

			for (int i = 0; i < liParts; i++)
			{
				int liA = i < lsA.Length && int.TryParse(lsA[i].Trim(), out int liValueA) ? liValueA : 0;
				int liB = i < lsB.Length && int.TryParse(lsB[i].Trim(), out int liValueB) ? liValueB : 0;

				if (liA != liB)
					return liA < liB ? -1 : 1;
			}

			return 0;
		}
	}
}
