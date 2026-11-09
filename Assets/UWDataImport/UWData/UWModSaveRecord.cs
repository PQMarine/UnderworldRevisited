using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MODS A SAVE GAME WAS MADE WITH (per user, 2026-11-09): the editor's packages applied to the game's files when
	/// it was saved, kept in the save's UWR.json (UWHelpNotes) - the original's files untouched, so the save stays the
	/// DOS game's. A save holds its levels but not the tables, texts and program the packages changed; loaded with other
	/// packages, things in it may not fit (an item whose values a mod raised, a conversation that moved). Loading
	/// compares, and the game says what differs. Only the packages that change the game's files count: a picture pack
	/// changes nothing a save holds.
	///
	/// A line per package: its id, a TAB, its version, a TAB, its name. A package counts as the same where id and
	/// version are; another version is missing in the one and new in the other.
	/// </summary>
	public static class UWModSaveRecord
	{
		/// <summary>One package as the record holds it.</summary>
		public sealed class Package
		{
			public string Id;

			public string Version;

			public string Name;

			public string Key => Id + "\t" + Version;

			/// <summary>As the game names it to the player: the name and the version.</summary>
			public string Shown => (string.IsNullOrEmpty(Name) ? Id : Name) + (string.IsNullOrEmpty(Version) ? string.Empty : " " + Version);
		}

		/// <summary>The record's line of a package.</summary>
		public static string Line(string psId, string psVersion, string psName)
		{
			return fClean(psId) + "\t" + fClean(psVersion) + "\t" + fClean(psName);
		}

		private static string fClean(string psText)
		{
			return (psText ?? string.Empty).Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
		}

		/// <summary>The packages of a record's lines; a line that is none is left out.</summary>
		public static List<Package> Parse(IEnumerable<string> psLines)
		{
			List<Package> lOPackages = new List<Package>();

			if (psLines == null)
				return lOPackages;

			foreach (string lsLine in psLines)
			{
				string[] lsFields = (lsLine ?? string.Empty).Split('\t');

				if (lsFields.Length < 2 || lsFields[0].Length == 0)
					continue;

				lOPackages.Add(new Package { Id = lsFields[0], Version = lsFields[1], Name = lsFields.Length > 2 ? lsFields[2] : null });
			}

			return lOPackages;
		}

		/// <summary>What differs between the record of a save and the packages in force now: the saved ones not in force
		/// (pOMissing) and the ones in force not saved with (pONew), each in its list's order. False where nothing differs.</summary>
		public static bool Compare(List<Package> pOSaved, List<Package> pONow, out List<Package> pOMissing, out List<Package> pONew)
		{
			HashSet<string> lONow = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> lOSaved = new HashSet<string>(StringComparer.Ordinal);

			foreach (Package lOPackage in pONow)
				lONow.Add(lOPackage.Key);

			foreach (Package lOPackage in pOSaved)
				lOSaved.Add(lOPackage.Key);

			pOMissing = pOSaved.FindAll(pOPackage => !lONow.Contains(pOPackage.Key));
			pONew = pONow.FindAll(pOPackage => !lOSaved.Contains(pOPackage.Key));

			return pOMissing.Count > 0 || pONew.Count > 0;
		}
	}
}
