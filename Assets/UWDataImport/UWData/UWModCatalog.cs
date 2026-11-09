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
	///   - THE ORDER as the folder's order.json gives it (UWModOrder, the Mods window writes it, 2026-11-09), the
	///     mods it does not name after them alphabetically; a mod switched off there is not loaded and counts as
	///     missing for what requires it.
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

			/// <summary>The readme the Mods window shows: a README.md or README.txt at the mod's top (any case),
			/// else mod.json's description, else null. Either cut to ReadmeLimit characters.</summary>
			public string Readme()
			{
				return Readme(out _);
			}

			/// <summary>The readme, and whether it is Markdown (a README.md - UWMarkdownLite draws it).</summary>
			public string Readme(out bool pbMarkdown)
			{
				pbMarkdown = false;

				foreach (string lsPath in Source.ListFiles())
				{
					if (lsPath.Contains("/"))
						continue;

					string lsName = lsPath.ToLowerInvariant();

					if (lsName != "readme.md" && lsName != "readme.txt" && lsName != "readme")
						continue;

					string lsText = Source.ReadAllText(lsPath);

					if (!string.IsNullOrWhiteSpace(lsText))
					{
						pbMarkdown = lsName == "readme.md";

						return fCut(lsText);
					}
				}

				return string.IsNullOrWhiteSpace(Manifest.Description) ? null : fCut(Manifest.Description);
			}

			private static string fCut(string psText)
			{
				psText = psText.Replace("\r\n", "\n");

				return psText.Length > ReadmeLimit ? psText.Substring(0, ReadmeLimit) + "\n..." : psText;
			}
		}

		/// <summary>
		/// How much of a readme or a description the Mods window shows (2026-11-09): the editor's description has no
		/// limit, and one text of Unity's interface holds four corners a letter in 65535 - about 16000 letters.
		/// </summary>
		public const int ReadmeLimit = 10000;

		/// <summary>The mods loaded, in their order.</summary>
		public List<Entry> Loaded { get; } = new List<Entry>();

		/// <summary>What was found but not loaded, each with why ("Name: reason").</summary>
		public List<string> Refused { get; } = new List<string>();

		/// <summary>Things worth a line in the log that refuse nothing.</summary>
		public List<string> Warnings { get; } = new List<string>();

		public string Folder { get; private set; }

		/// <summary>The folder's order.json (UWModOrder): the load order and the mods switched off.</summary>
		public UWModOrder Order { get; private set; } = new UWModOrder();

		/// <summary>Every mod with a valid mod.json, one per id, in the order the Mods window lists them: as
		/// order.json orders them, the ones it does not name after them alphabetically.</summary>
		public List<Entry> All { get; } = new List<Entry>();

		/// <summary>The mods switched off in order.json.</summary>
		public List<Entry> Disabled { get; } = new List<Entry>();

		/// <summary>What is wrong with something in the folder that is no mod (Broken).</summary>
		public enum BrokenKind
		{
			/// <summary>A zip without a mod.json at its top or in its one top folder.</summary>
			NoManifest,

			/// <summary>A mod.json that is no JSON object.</summary>
			BadManifest,

			/// <summary>A mod.json without a valid id.</summary>
			NoId,

			/// <summary>A zip that cannot be opened.</summary>
			BadZip
		}

		/// <summary>Something in the folder that is no mod, for the Mods window: its name, where it lies, what is wrong.</summary>
		public sealed class Broken
		{
			public string Name;

			public string Location;

			public BrokenKind Kind;

			public string Why;
		}

		/// <summary>Everything in the folder that is no mod, in the order found (the window lists them in red, each
		/// with a hint how to mend it).</summary>
		public List<Broken> BrokenMods { get; } = new List<Broken>();

		private void fBroken(string psName, string psLocation, BrokenKind peKind, string psWhy)
		{
			Refused.Add(psName + ": " + psWhy);
			BrokenMods.Add(new Broken { Name = psName, Location = psLocation, Kind = peKind, Why = psWhy });
		}

		/// <summary>The lines of Refused for mods with a valid mod.json (fRefuse); the others are Unreadable.</summary>
		private readonly HashSet<string> mOModLines = new HashSet<string>(StringComparer.Ordinal);

		/// <summary>What the folder holds that is no mod: no mod.json, a broken one, no valid id, a zip that cannot
		/// be read - by its file or folder name, with why. The Mods window lists them in red.</summary>
		public List<string> Unreadable => Refused.FindAll(psLine => !mOModLines.Contains(psLine));

		/// <summary>Why a mod of All is not loaded, by id.</summary>
		private readonly Dictionary<string, string> mOReasons = new Dictionary<string, string>(StringComparer.Ordinal);

		/// <summary>Why a mod is not loaded: switched off, or the reason it was refused; null for a loaded one.</summary>
		public string ReasonOf(string psId)
		{
			return mOReasons.TryGetValue(psId, out string lsWhy) ? lsWhy : null;
		}

		/// <summary>Where a mod or a broken one lies, from the mod folder on ("Graphics/wide.zip").</summary>
		public string WhereInFolder(string psLocation)
		{
			if (string.IsNullOrEmpty(psLocation) || string.IsNullOrEmpty(Folder))
				return psLocation;

			string lsFolder = Folder.TrimEnd('/', '\\');

			return psLocation.StartsWith(lsFolder, StringComparison.OrdinalIgnoreCase)
				? psLocation.Substring(lsFolder.Length).TrimStart('/', '\\').Replace('\\', '/')
				: psLocation;
		}

		/// <summary>Whether a mod is switched off in order.json.</summary>
		public bool IsDisabled(string psId)
		{
			return Order.Disabled.Contains(psId);
		}

		/// <summary>The catalog of a mod folder (none for a folder that does not exist).</summary>
		public static UWModCatalog Scan(string psFolder)
		{
			UWModCatalog lOCatalog = new UWModCatalog { Folder = psFolder };

			if (string.IsNullOrEmpty(psFolder) || !Directory.Exists(psFolder))
				return lOCatalog;

			List<Entry> lOFound = new List<Entry>();

			lOCatalog.Order = UWModOrder.Read(psFolder, out string lsOrderWarning);

			if (lsOrderWarning != null)
				lOCatalog.Warnings.Add(lsOrderWarning);

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
					fBroken(Path.GetFileName(lsFile), lsFile, BrokenKind.BadZip, "the zip cannot be read");
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
				fBroken(lsName, pOSource.Location, BrokenKind.NoManifest, "no " + UWModManifest.FileName);
				return;
			}

			UWModManifest lOManifest = UWModManifest.Parse(lsJson, out string lsError);

			if (lOManifest == null)
			{
				fBroken(lsName, pOSource.Location, BrokenKind.BadManifest, UWModManifest.FileName + " " + lsError);
				return;
			}

			// An editor package's id as its own format takes it (UWModManifest.IsValidEditorId), ours by our rule.
			if (lOManifest.IsEditorPackage ? !UWModManifest.IsValidEditorId(lOManifest.Id) : !UWModManifest.IsValidId(lOManifest.Id))
			{
				fBroken(lsName, pOSource.Location, BrokenKind.NoId, lOManifest.IsEditorPackage
					? "no id (any text, no control characters, at most " + UWModManifest.MaxEditorIdLength + " characters)"
					: "no valid id (lower case letters, digits, '-' and '.')");
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

			// Every mod in the window's order; the ones switched off out before the requirements.
			All.AddRange(lOById.Values);
			All.Sort((pOA, pOB) => Order.Compare(pOA.Id, pOB.Id));

			Dictionary<string, Entry> lOAlive = new Dictionary<string, Entry>(lOById, StringComparer.Ordinal);

			foreach (Entry lOEntry in All)
			{
				if (!Order.Disabled.Contains(lOEntry.Id))
					continue;

				lOAlive.Remove(lOEntry.Id);
				Disabled.Add(lOEntry);
				mOReasons[lOEntry.Id] = "switched off";
			}

			// Requirements: drop what cannot load until nothing changes.
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
					fRefuse(lOEntry, lsWhy);
					lbChanged = true;
				}
			}

			// The order: order.json's, the rest alphabetically, each after what it requires; a ring cannot be ordered.
			List<string> lsIds = new List<string>(lOAlive.Keys);

			lsIds.Sort(Order.Compare);

			HashSet<string> lODone = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> lOVisiting = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> lOInRing = new HashSet<string>(StringComparer.Ordinal);

			foreach (string lsId in lsIds)
				fVisit(lsId, lOAlive, lODone, lOVisiting, lOInRing);

			foreach (string lsId in lsIds)
			{
				if (lOInRing.Contains(lsId))
					fRefuse(lOAlive[lsId], "its requirements form a ring");
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

						fRefuse(Loaded[i], "needs " + lORequirement + ", which is not loaded");
						Loaded.RemoveAt(i);
						lbDropped = true;
						break;
					}
				}
			}
		}

		/// <summary>A mod with a valid mod.json not loaded: its line in Refused and its reason for the window.</summary>
		private void fRefuse(Entry pOEntry, string psWhy)
		{
			Refused.Add(pOEntry + ": " + psWhy);
			mOModLines.Add(pOEntry + ": " + psWhy);
			mOReasons[pOEntry.Id] = psWhy;
		}

		private string fMissing(Entry pOEntry, Dictionary<string, Entry> pOAlive)
		{
			foreach (UWModManifest.Requirement lORequirement in pOEntry.Manifest.Requires)
			{
				if (lORequirement.Id == pOEntry.Id)
					return "it requires itself";

				if (!pOAlive.TryGetValue(lORequirement.Id, out Entry lONeeded))
					return "needs " + lORequirement + (Order.Disabled.Contains(lORequirement.Id) ? ", which is switched off" : ", which is not loaded");

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

			lsNeeded.Sort(Order.Compare);

			foreach (string lsRequired in lsNeeded)
				fVisit(lsRequired, pOAlive, pODone, pOVisiting, pOInRing);

			pOVisiting.Remove(psId);
			pODone.Add(psId);

			if (!pOInRing.Contains(psId))
				Loaded.Add(pOAlive[psId]);
		}

		/// <summary>Two editor's packages changing the same files: the later one's, the earlier one it gives way to and
		/// the files they share.</summary>
		public sealed class Conflict
		{
			public Entry With;

			public List<string> Files = new List<string>();
		}

		/// <summary>The conflicts per game, worked out once per scan (the Mods window asks every frame).</summary>
		private readonly Dictionary<string, Dictionary<string, Conflict>> mOConflicts = new Dictionary<string, Dictionary<string, Conflict>>(StringComparer.Ordinal);

		/// <summary>
		/// THE CONFLICT OF AN EDITOR'S PACKAGE IN THE CURRENT ORDER (the Mods window, per user 2026-11-09: after a reorder
		/// it still showed the last start's result): the loaded packages of the game in their order, each with the files
		/// its mod.json names; a package naming a file an earlier one names gives way, as UWModGameFiles refuses it when
		/// applying - here at once, from the file lists alone. Null for none. What only applying shows (another release,
		/// a damaged delta) stays the last start's.
		/// </summary>
		public Conflict ConflictOf(string psGame, string psId)
		{
			if (!mOConflicts.TryGetValue(psGame, out Dictionary<string, Conflict> lOConflicts))
			{
				lOConflicts = new Dictionary<string, Conflict>(StringComparer.Ordinal);

				Dictionary<string, Entry> lOOwner = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

				foreach (Entry lOEntry in Loaded)
				{
					if (!lOEntry.Manifest.IsEditorPackage || !lOEntry.Manifest.IsFor(psGame))
						continue;

					string lsJson = lOEntry.Source.ReadAllText(UWModManifest.FileName);
					List<UWModFileChange> lOFiles = lsJson != null ? UWModFileChange.ParseAll(lsJson, psGame, out _) : null;

					// A package whose list cannot be read is refused when applied, with its reason.
					if (lOFiles == null)
						continue;

					Conflict lOConflict = null;

					foreach (UWModFileChange lOFile in lOFiles)
					{
						if (!lOOwner.TryGetValue(lOFile.Path, out Entry lOEarlier))
							continue;

						if (lOConflict == null)
							lOConflict = new Conflict { With = lOEarlier };

						if (lOEarlier == lOConflict.With)
							lOConflict.Files.Add(lOFile.Path);
					}

					if (lOConflict != null)
					{
						lOConflict.Files.Sort(StringComparer.OrdinalIgnoreCase);
						lOConflicts[lOEntry.Id] = lOConflict;
						continue;
					}

					foreach (UWModFileChange lOFile in lOFiles)
						lOOwner[lOFile.Path] = lOEntry;
				}

				mOConflicts[psGame] = lOConflicts;
			}

			return lOConflicts.TryGetValue(psId, out Conflict lOFound) ? lOFound : null;
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
