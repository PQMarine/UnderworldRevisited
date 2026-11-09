using System;
using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MOD FOLDER'S CATALOG (Docs/WIDESCREEN-WEAPONS-PLAN.md 3 and 4.1, Docs/MOD-SUPPORT-PLAN.md
	/// phase 0; built 2026-11-09). Scans the folder and one level below it (Mods/Graphics/&lt;mod&gt;/):
	///
	///   - a FOLDER with a mod.json, a ZIP with one at its root or in its one top folder, an editor
	///     PACKAGE (.uwmod, a zip with its mod.json at the root);
	///   - THE ID is the mod's identity; a mod without a valid one is refused. Two with one id: the
	///     folder wins over the zip (the artist works in a folder while players have the zip), otherwise
	///     the later found is refused - each with a line;
	///   - REQUIRES (after OpenXcom's master): a mod whose requirement is missing, too old or refused is
	///     not loaded, nor one in a ring; a loaded mod comes after what it requires;
	///   - THE ORDER otherwise alphabetical by id, until the Mods window brings the player's order and
	///     on/off (active when installed: decision 2 of the widescreen plan).
	///
	/// Folder and zip names are free; files of different mods never clash, each is read in its own
	/// container (UWModSource). Nothing is applied here: UWModSet hands the mods to what reads them.
	/// </summary>
	public sealed class UWModCatalog
	{
		/// <summary>One mod found: where it lies and what its mod.json says.</summary>
		public sealed class Entry
		{
			public UWModSource Source;

			public UWModManifest Manifest;

			public string Id => Manifest.Id;

			public override string ToString()
			{
				return Manifest.Id + " " + (Manifest.Version ?? "?") + " (" + Path.GetFileName(Source.Location.TrimEnd('/', '\\')) + ")";
			}
		}

		/// <summary>The mods loaded, in their order.</summary>
		public List<Entry> Loaded { get; } = new List<Entry>();

		/// <summary>What was found but not loaded, each with why ("Name: reason").</summary>
		public List<string> Refused { get; } = new List<string>();

		/// <summary>Things worth a line in the log that refuse nothing.</summary>
		public List<string> Warnings { get; } = new List<string>();

		public string Folder { get; private set; }

		/// <summary>The catalog of a mod folder (none for a folder that does not exist).</summary>
		public static UWModCatalog Scan(string psFolder)
		{
			UWModCatalog lOCatalog = new UWModCatalog { Folder = psFolder };

			if (string.IsNullOrEmpty(psFolder) || !Directory.Exists(psFolder))
				return lOCatalog;

			List<Entry> lOFound = new List<Entry>();

			lOCatalog.fScanLevel(psFolder, lOFound, true);
			lOCatalog.fResolve(lOFound);

			return lOCatalog;
		}

		private void fScanLevel(string psFolder, List<Entry> pOFound, bool pbDescend)
		{
			string[] lsFolders;
			string[] lsFiles;

			try
			{
				lsFolders = Directory.GetDirectories(psFolder);
				lsFiles = Directory.GetFiles(psFolder);
			}
			catch (Exception lOError) when (lOError is IOException || lOError is UnauthorizedAccessException)
			{
				Warnings.Add(psFolder + ": " + lOError.Message);
				return;
			}

			Array.Sort(lsFolders, StringComparer.OrdinalIgnoreCase);
			Array.Sort(lsFiles, StringComparer.OrdinalIgnoreCase);

			foreach (string lsFolder in lsFolders)
			{
				if (File.Exists(Path.Combine(lsFolder, UWModManifest.FileName)))
					fAdd(UWModSource.Folder(lsFolder), pOFound);
				else if (pbDescend)
					fScanLevel(lsFolder, pOFound, false);
			}

			foreach (string lsFile in lsFiles)
			{
				string lsExtension = Path.GetExtension(lsFile);

				if (!string.Equals(lsExtension, ".zip", StringComparison.OrdinalIgnoreCase)
					&& !string.Equals(lsExtension, UWModManifest.PackageExtension, StringComparison.OrdinalIgnoreCase))
					continue;

				UWModSource lOZip = UWModSource.Zip(lsFile);

				if (lOZip == null)
				{
					Refused.Add(Path.GetFileName(lsFile) + ": the zip cannot be read");
					continue;
				}

				fAdd(lOZip, pOFound);
			}
		}

		private void fAdd(UWModSource pOSource, List<Entry> pOFound)
		{
			string lsName = Path.GetFileName(pOSource.Location.TrimEnd('/', '\\'));
			string lsJson = pOSource.ReadAllText(UWModManifest.FileName);

			if (lsJson == null)
			{
				Refused.Add(lsName + ": no " + UWModManifest.FileName);
				return;
			}

			UWModManifest lOManifest = UWModManifest.Parse(lsJson, out string lsError);

			if (lOManifest == null)
			{
				Refused.Add(lsName + ": " + UWModManifest.FileName + " " + lsError);
				return;
			}

			if (!UWModManifest.IsValidId(lOManifest.Id))
			{
				Refused.Add(lsName + ": no valid id (lower case letters, digits, '-' and '.')");
				return;
			}

			pOFound.Add(new Entry { Source = pOSource, Manifest = lOManifest });
		}

		/// <summary>Duplicates, then the requirements and the order.</summary>
		private void fResolve(List<Entry> pOFound)
		{
			// One per id: a folder before a zip, else the first found.
			Dictionary<string, Entry> lOById = new Dictionary<string, Entry>(StringComparer.Ordinal);

			foreach (Entry lOEntry in pOFound)
			{
				if (!lOById.TryGetValue(lOEntry.Id, out Entry lOKept))
				{
					lOById[lOEntry.Id] = lOEntry;
					continue;
				}

				Entry lOWinner = lOKept.Source.IsZip && !lOEntry.Source.IsZip ? lOEntry : lOKept;
				Entry lOLoser = lOWinner == lOKept ? lOEntry : lOKept;

				lOById[lOEntry.Id] = lOWinner;
				Warnings.Add(lOEntry.Id + ": found twice - " + lOWinner + " used, " + lOLoser + " left out");
			}

			// Requirements: drop what cannot load until nothing changes.
			Dictionary<string, Entry> lOAlive = new Dictionary<string, Entry>(lOById, StringComparer.Ordinal);
			bool lbChanged = true;

			while (lbChanged)
			{
				lbChanged = false;

				foreach (Entry lOEntry in new List<Entry>(lOAlive.Values))
				{
					string lsWhy = fMissing(lOEntry, lOAlive);

					if (lsWhy == null)
						continue;

					lOAlive.Remove(lOEntry.Id);
					Refused.Add(lOEntry + ": " + lsWhy);
					lbChanged = true;
				}
			}

			// The order: alphabetical, each after what it requires; a ring cannot be ordered.
			List<string> lsIds = new List<string>(lOAlive.Keys);

			lsIds.Sort(StringComparer.Ordinal);

			HashSet<string> lODone = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> lOVisiting = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> lOInRing = new HashSet<string>(StringComparer.Ordinal);

			foreach (string lsId in lsIds)
				fVisit(lsId, lOAlive, lODone, lOVisiting, lOInRing);

			foreach (string lsId in lsIds)
			{
				if (lOInRing.Contains(lsId))
					Refused.Add(lOAlive[lsId] + ": its requirements form a ring");
			}

			// Loaded may hold a ring's members already (visited before the ring showed): take them out,
			// and then whatever needs a mod no longer loaded.
			Loaded.RemoveAll(pOEntry => lOInRing.Contains(pOEntry.Id));

			bool lbDropped = true;

			while (lbDropped)
			{
				lbDropped = false;

				HashSet<string> lOLoadedIds = new HashSet<string>(StringComparer.Ordinal);

				foreach (Entry lOEntry in Loaded)
					lOLoadedIds.Add(lOEntry.Id);

				for (int i = Loaded.Count - 1; i >= 0; i--)
				{
					foreach (UWModManifest.Requirement lORequirement in Loaded[i].Manifest.Requires)
					{
						if (lOLoadedIds.Contains(lORequirement.Id))
							continue;

						Refused.Add(Loaded[i] + ": needs " + lORequirement + ", which is not loaded");
						Loaded.RemoveAt(i);
						lbDropped = true;
						break;
					}
				}
			}
		}

		private static string fMissing(Entry pOEntry, Dictionary<string, Entry> pOAlive)
		{
			foreach (UWModManifest.Requirement lORequirement in pOEntry.Manifest.Requires)
			{
				if (lORequirement.Id == pOEntry.Id)
					return "it requires itself";

				if (!pOAlive.TryGetValue(lORequirement.Id, out Entry lONeeded))
					return "needs " + lORequirement + ", which is not loaded";

				if (lORequirement.LowestVersion != null
					&& UWModManifest.CompareVersions(lONeeded.Manifest.Version, lORequirement.LowestVersion) < 0)
					return "needs " + lORequirement + ", found " + (lONeeded.Manifest.Version ?? "no version");
			}

			return null;
		}

		private void fVisit(string psId, Dictionary<string, Entry> pOAlive, HashSet<string> pODone, HashSet<string> pOVisiting,
			HashSet<string> pOInRing)
		{
			if (pODone.Contains(psId))
				return;

			if (pOVisiting.Contains(psId))
			{
				// A ring: every mod still being visited is in it.
				foreach (string lsMember in pOVisiting)
					pOInRing.Add(lsMember);

				return;
			}

			pOVisiting.Add(psId);

			List<string> lsNeeded = new List<string>();

			foreach (UWModManifest.Requirement lORequirement in pOAlive[psId].Manifest.Requires)
				lsNeeded.Add(lORequirement.Id);

			lsNeeded.Sort(StringComparer.Ordinal);

			foreach (string lsRequired in lsNeeded)
				fVisit(lsRequired, pOAlive, pODone, pOVisiting, pOInRing);

			pOVisiting.Remove(psId);
			pODone.Add(psId);

			if (!pOInRing.Contains(psId))
				Loaded.Add(pOAlive[psId]);
		}

		/// <summary>The loaded mods for one game ("uw1", "uw2"), in order.</summary>
		public UWModSet SetFor(string psGame)
		{
			List<Entry> lOMods = new List<Entry>();

			foreach (Entry lOEntry in Loaded)
			{
				if (lOEntry.Manifest.IsFor(psGame))
					lOMods.Add(lOEntry);
			}

			return new UWModSet(lOMods);
		}

		/// <summary>The catalog in a few lines for the log and the debug display.</summary>
		public List<string> Describe()
		{
			List<string> lsLines = new List<string>();

			lsLines.Add(Loaded.Count == 0 ? "Mods: none" : "Mods: " + Loaded.Count + " loaded");

			foreach (Entry lOEntry in Loaded)
				lsLines.Add("  " + lOEntry);

			foreach (string lsRefused in Refused)
				lsLines.Add("  not loaded: " + lsRefused);

			foreach (string lsWarning in Warnings)
				lsLines.Add("  " + lsWarning);

			return lsLines;
		}
	}

	/// <summary>
	/// THE MODS IN FORCE for one game, in their order (Docs/WIDESCREEN-WEAPONS-PLAN.md 4.1): handed to what
	/// reads them (the pictures, later the tables and the editor's packages) and part of the data's identity
	/// - the data is rebuilt when the set changes. Its key names every mod with its version.
	/// </summary>
	public sealed class UWModSet
	{
		public static readonly UWModSet Empty = new UWModSet(new List<UWModCatalog.Entry>());

		public IReadOnlyList<UWModCatalog.Entry> Mods { get; }

		public UWModSet(List<UWModCatalog.Entry> pOMods)
		{
			Mods = pOMods;
		}

		/// <summary>"id@version;..." in order - equal for the same mods in the same order.</summary>
		public string Key
		{
			get
			{
				System.Text.StringBuilder lOKey = new System.Text.StringBuilder();

				foreach (UWModCatalog.Entry lOEntry in Mods)
					lOKey.Append(lOEntry.Id).Append('@').Append(lOEntry.Manifest.Version ?? string.Empty).Append(';');

				return lOKey.ToString();
			}
		}
	}
}
