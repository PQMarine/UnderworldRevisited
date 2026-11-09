using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// ONE FILE A .uwmod CHANGES (the Underworld Editor's mod format 2, docs/mod-format.md "The package"): its path in
	/// the game folder, the original it was made from (null for a file the mod adds), the result, and the delta
	/// in the package that turns one into the other.
	/// </summary>
	public sealed class UWModFileChange
	{
		public string Path;

		public string BaseSha256;

		public long BaseSize;

		public bool Adds;

		public string TargetSha256;

		public long TargetSize;

		public string DeltaPath;

		public string DeltaSha256;

		/// <summary>The files a .uwmod's mod.json lists, checked against the format's rules, or null with the reason.</summary>
		public static List<UWModFileChange> ParseAll(string psJson, string psGame, out string psError)
		{
			Dictionary<string, object> lOJson = UWJsonReader.TryParseObject(psJson, out psError);

			if (lOJson == null)
				return null;

			List<UWModFileChange> lOFiles = new List<UWModFileChange>();
			HashSet<string> lOSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (object lOItem in UWJsonReader.GetList(lOJson, "files") ?? new List<object>())
			{
				if (!(lOItem is Dictionary<string, object> lOFile))
				{
					psError = "a file entry is no object";

					return null;
				}

				Dictionary<string, object> lOBase = UWJsonReader.GetObject(lOFile, "base");
				Dictionary<string, object> lOTarget = UWJsonReader.GetObject(lOFile, "target");
				Dictionary<string, object> lODelta = UWJsonReader.GetObject(lOFile, "delta");
				UWModFileChange lOChange = new UWModFileChange
				{
					Path = UWJsonReader.GetString(lOFile, "path"),
					Adds = lOBase == null,
					BaseSha256 = lOBase != null ? UWJsonReader.GetString(lOBase, "sha256") : null,
					BaseSize = lOBase != null ? (long)UWJsonReader.GetNumber(lOBase, "size", -1) : 0,
					TargetSha256 = lOTarget != null ? UWJsonReader.GetString(lOTarget, "sha256") : null,
					TargetSize = lOTarget != null ? (long)UWJsonReader.GetNumber(lOTarget, "size", -1) : -1,
					DeltaPath = lODelta != null ? UWJsonReader.GetString(lODelta, "path") : null,
					DeltaSha256 = lODelta != null ? UWJsonReader.GetString(lODelta, "sha256") : null
				};

				string lsWhy = fCheck(lOChange, psGame);

				if (lsWhy != null)
				{
					psError = (lOChange.Path ?? "a file") + ": " + lsWhy;

					return null;
				}

				if (!lOSeen.Add(lOChange.Path))
				{
					psError = lOChange.Path + ": listed twice";

					return null;
				}

				lOFiles.Add(lOChange);
			}

			return lOFiles;
		}

		/// <summary>
		/// The format's rules for one file (docs/mod-format.md, "Rules"): the game's program or a file under DATA,
		/// CRIT, CUTS or SOUND, '/'-separated, no "..", never UW.CFG; a file the mod adds only under SOUND (the
		/// speech and effects the game opens by number); hashes and sizes given.
		/// </summary>
		private static string fCheck(UWModFileChange pOChange, string psGame)
		{
			string lsPath = pOChange.Path;

			if (string.IsNullOrEmpty(lsPath) || lsPath.Contains("\\") || lsPath.StartsWith("/", StringComparison.Ordinal) || lsPath.Contains(":"))
				return "no path in the game folder";

			foreach (string lsPart in lsPath.Split('/'))
			{
				if (lsPart.Length == 0 || lsPart == "." || lsPart == "..")
					return "no path in the game folder";
			}

			string lsProgram = string.Equals(psGame, "uw2", StringComparison.OrdinalIgnoreCase) ? "UW2.EXE" : "UW.EXE";
			bool lbProgram = string.Equals(lsPath, lsProgram, StringComparison.OrdinalIgnoreCase);
			string lsFolder = lsPath.Contains("/") ? lsPath.Substring(0, lsPath.IndexOf('/')).ToUpperInvariant() : null;

			if (!lbProgram && lsFolder != "DATA" && lsFolder != "CRIT" && lsFolder != "CUTS" && lsFolder != "SOUND")
				return "not a file a mod may change (the program or DATA, CRIT, CUTS, SOUND)";

			if (pOChange.Adds && lsFolder != "SOUND")
				return "a mod adds files only under SOUND";

			if (!pOChange.Adds && (pOChange.BaseSha256 == null || pOChange.BaseSize < 0))
				return "no original named";

			if (pOChange.TargetSha256 == null || pOChange.TargetSize < 0 || pOChange.DeltaPath == null || pOChange.DeltaSha256 == null)
				return "no result or no delta named";

			return null;
		}
	}

	/// <summary>
	/// THE GAME'S FILES WITH THE EDITOR'S PACKAGES APPLIED (Docs/MOD-SUPPORT-PLAN.md, built 2026-11-09): the .uwmod
	/// mods in force for one game, applied to the files extracted from the player's own GOG or Steam image. The
	/// result is a copy beside the extraction (GameData/UW1-mods), which the game then reads as its data - every
	/// reader of the game's files takes it as it is, and the player's installation is never written.
	///
	///   - A package is applied whole or not at all: every file's original must match its SHA-256 and size,
	///     every delta its SHA-256, every result its SHA-256 and size - else the package is refused with the
	///     reason, as format 2 itself refuses a mod made for another release.
	///   - Packages apply in the catalog's order. A later one changing a file an earlier one changed finds no
	///     original of its own and is refused as conflicting with it.
	///   - The copy is kept while the same packages apply to the same extraction (a marker with the key) and
	///     made anew when they change; with none applied the extraction itself is the game's data.
	/// </summary>
	public sealed class UWModGameFiles
	{
		/// <summary>The end of a refusal for a file an earlier package changed or added: the Mods window shows those from
		/// the current order itself (UWModCatalog.ConflictOf) and leaves the last start's out.</summary>
		public const string ConflictMark = "the two mods conflict";

		/// <summary>The marker in the copy: the key it was made for.</summary>
		public const string MarkerName = "mods.txt";

		/// <summary>The extraction's own marker (UWGogInstall).</summary>
		public const string ExtractionMarker = "extracted.txt";

		/// <summary>The game folder the game reads: the copy, or the extraction where nothing applied.</summary>
		public string Root { get; private set; }

		/// <summary>The packages applied, as "id version".</summary>
		public List<string> Applied { get; } = new List<string>();

		/// <summary>The packages not applied, each with why.</summary>
		public List<string> Refused { get; } = new List<string>();

		/// <summary>The files changed or added, '/'-separated.</summary>
		public List<string> Changed { get; } = new List<string>();

		/// <summary>Why the copy could not be made (the extraction is used then), null where it could.</summary>
		public string Problem { get; private set; }

		/// <summary>
		/// The key of a set of packages over an extraction: the extraction's marker and, per package, its id,
		/// version and the SHA-256 of its mod.json (which names every delta's hash). Equal keys give equal copies.
		/// </summary>
		public static string KeyOf(string psExtraction, IEnumerable<UWModCatalog.Entry> pOPackages)
		{
			StringBuilder lOKey = new StringBuilder();
			string lsMarker = Path.Combine(psExtraction, ExtractionMarker);

			lOKey.Append(File.Exists(lsMarker) ? File.ReadAllText(lsMarker).Trim() : "?");

			foreach (UWModCatalog.Entry lOEntry in pOPackages)
			{
				byte[] lyManifest = lOEntry.Source.ReadAllBytes(UWModManifest.FileName);

				lOKey.Append('\n').Append(lOEntry.Id).Append(' ').Append(lOEntry.Manifest.Version ?? "?").Append(' ')
					.Append(lyManifest != null ? Sha256(lyManifest) : "?");
			}

			return lOKey.ToString();
		}

		/// <summary>
		/// The game folder with the packages applied: psExtraction the extracted original (GameData/UW1), psCopy the
		/// copy's folder (GameData/UW1-mods), psGame "uw1" or "uw2". Never throws; Problem says what went wrong.
		/// </summary>
		public static UWModGameFiles Build(string psExtraction, string psCopy, IList<UWModCatalog.Entry> pOPackages, string psGame)
		{
			UWModGameFiles lOResult = new UWModGameFiles { Root = psExtraction };

			if (pOPackages == null || pOPackages.Count == 0)
				return lOResult;

			try
			{
				lOResult.fBuild(psExtraction, psCopy, pOPackages, psGame);
			}
			catch (Exception lOError) when (lOError is IOException || lOError is UnauthorizedAccessException)
			{
				lOResult.Problem = "the game's files with the mods could not be written: " + lOError.Message;
				lOResult.Root = psExtraction;
			}

			return lOResult;
		}

		private void fBuild(string psExtraction, string psCopy, IList<UWModCatalog.Entry> pOPackages, string psGame)
		{
			// The files as the packages have made them so far, by path; the originals are read on demand.
			Dictionary<string, byte[]> lOChanged = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> lOChangedBy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			foreach (UWModCatalog.Entry lOEntry in pOPackages)
			{
				string lsName = lOEntry.Id + " " + (lOEntry.Manifest.Version ?? "?");
				string lsWhy = fApplyPackage(lOEntry, psExtraction, psGame, lOChanged, lOChangedBy, out Dictionary<string, byte[]> lOMine);

				if (lsWhy != null)
				{
					Refused.Add(lOEntry.Id + ": " + lsWhy);
					continue;
				}

				foreach (KeyValuePair<string, byte[]> lOPair in lOMine)
				{
					lOChanged[lOPair.Key] = lOPair.Value;
					lOChangedBy[lOPair.Key] = lOEntry.Id;
				}

				Applied.Add(lsName);
			}

			if (Applied.Count == 0)
				return;

			Changed.AddRange(lOChanged.Keys);
			Changed.Sort(StringComparer.OrdinalIgnoreCase);

			// The copy: kept where it was made for the same key, else the extraction copied and the changes written.
			string lsKey = KeyOf(psExtraction, fAppliedEntries(pOPackages));
			string lsMarker = Path.Combine(psCopy, MarkerName);

			if (File.Exists(lsMarker) && File.ReadAllText(lsMarker) == lsKey)
			{
				Root = psCopy;

				return;
			}

			string lsPartial = psCopy + ".partial";

			if (Directory.Exists(lsPartial))
				Directory.Delete(lsPartial, true);

			fCopyFolder(psExtraction, lsPartial);

			string lsOldMarker = Path.Combine(lsPartial, ExtractionMarker);

			if (File.Exists(lsOldMarker))
				File.Delete(lsOldMarker);

			foreach (KeyValuePair<string, byte[]> lOPair in lOChanged)
			{
				string lsFile = fFind(lsPartial, lOPair.Key) ?? Path.Combine(lsPartial, lOPair.Key.Replace('/', Path.DirectorySeparatorChar));

				Directory.CreateDirectory(Path.GetDirectoryName(lsFile));
				File.WriteAllBytes(lsFile, lOPair.Value);
			}

			File.WriteAllText(Path.Combine(lsPartial, MarkerName), lsKey);

			if (Directory.Exists(psCopy))
				Directory.Delete(psCopy, true);

			Directory.Move(lsPartial, psCopy);
			Root = psCopy;
		}

		private List<UWModCatalog.Entry> fAppliedEntries(IList<UWModCatalog.Entry> pOPackages)
		{
			List<UWModCatalog.Entry> lOApplied = new List<UWModCatalog.Entry>();

			foreach (UWModCatalog.Entry lOEntry in pOPackages)
			{
				if (Applied.Contains(lOEntry.Id + " " + (lOEntry.Manifest.Version ?? "?")))
					lOApplied.Add(lOEntry);
			}

			return lOApplied;
		}

		/// <summary>One package against the files as earlier packages left them: its changes, or why not.</summary>
		private static string fApplyPackage(UWModCatalog.Entry pOEntry, string psExtraction, string psGame, Dictionary<string, byte[]> pOChanged,
			Dictionary<string, string> pOChangedBy, out Dictionary<string, byte[]> pOMine)
		{
			pOMine = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

			string lsJson = pOEntry.Source.ReadAllText(UWModManifest.FileName);

			if (lsJson == null)
				return "its mod.json cannot be read";

			List<UWModFileChange> lOFiles = UWModFileChange.ParseAll(lsJson, psGame, out string lsError);

			if (lOFiles == null)
				return lsError;

			if (lOFiles.Count == 0)
				return "it changes no file";

			foreach (UWModFileChange lOFile in lOFiles)
			{
				byte[] lyCurrent = null;
				bool lbChangedBefore = pOChanged.TryGetValue(lOFile.Path, out lyCurrent);

				if (!lbChangedBefore)
				{
					string lsOriginal = fFind(psExtraction, lOFile.Path);

					lyCurrent = lsOriginal != null ? File.ReadAllBytes(lsOriginal) : null;
				}

				if (lOFile.Adds)
				{
					if (lyCurrent != null)
						return lbChangedBefore
							? lOFile.Path + " was added by " + pOChangedBy[lOFile.Path] + " before - " + ConflictMark
							: lOFile.Path + " is already there - a mod may only add a file the game lacks";

					lyCurrent = new byte[0];
				}
				else if (lyCurrent == null)
					return lOFile.Path + " is not in the game";
				else if (lyCurrent.LongLength != lOFile.BaseSize || !string.Equals(Sha256(lyCurrent), lOFile.BaseSha256, StringComparison.OrdinalIgnoreCase))
					return lbChangedBefore
						? lOFile.Path + " was changed by " + pOChangedBy[lOFile.Path] + " before - " + ConflictMark
						: lOFile.Path + " is not the file the mod was made for (another release of the game?)";

				byte[] lyDelta = pOEntry.Source.ReadAllBytes(lOFile.DeltaPath);

				if (lyDelta == null)
					return lOFile.DeltaPath + " is missing in the package";

				if (!string.Equals(Sha256(lyDelta), lOFile.DeltaSha256, StringComparison.OrdinalIgnoreCase))
					return lOFile.DeltaPath + " is damaged (its SHA-256 differs)";

				byte[] lyResult;

				try
				{
					lyResult = UWModDelta.Apply(lyCurrent, lyDelta);
				}
				catch (InvalidDataException lOError)
				{
					return lOFile.DeltaPath + ": " + lOError.Message;
				}

				if (lyResult.LongLength != lOFile.TargetSize || !string.Equals(Sha256(lyResult), lOFile.TargetSha256, StringComparison.OrdinalIgnoreCase))
					return lOFile.Path + ": the result is not the one the mod promises";

				pOMine[lOFile.Path] = lyResult;
			}

			return null;
		}

		/// <summary>A '/'-separated path's file in a folder, each part matched without regard to case (the extraction
		/// keeps the image's names); null where it is not there.</summary>
		public static string FindFile(string psRoot, string psPath)
		{
			return fFind(psRoot, psPath);
		}

		private static string fFind(string psRoot, string psPath)
		{
			string lsAt = psRoot;
			string[] lsParts = psPath.Split('/');

			for (int liPart = 0; liPart < lsParts.Length; liPart++)
			{
				bool lbLast = liPart == lsParts.Length - 1;
				string lsFound = null;

				if (!Directory.Exists(lsAt))
					return null;

				foreach (string lsEntry in lbLast ? Directory.GetFiles(lsAt) : Directory.GetDirectories(lsAt))
				{
					if (string.Equals(Path.GetFileName(lsEntry), lsParts[liPart], StringComparison.OrdinalIgnoreCase))
					{
						lsFound = lsEntry;
						break;
					}
				}

				if (lsFound == null)
					return null;

				lsAt = lsFound;
			}

			return lsAt;
		}

		private static void fCopyFolder(string psFrom, string psTo)
		{
			Directory.CreateDirectory(psTo);

			foreach (string lsFile in Directory.GetFiles(psFrom))
				File.Copy(lsFile, Path.Combine(psTo, Path.GetFileName(lsFile)));

			foreach (string lsFolder in Directory.GetDirectories(psFrom))
				fCopyFolder(lsFolder, Path.Combine(psTo, Path.GetFileName(lsFolder)));
		}

		/// <summary>Lower-case hex of the bytes' SHA-256.</summary>
		public static string Sha256(byte[] pyData)
		{
			using (SHA256 lOSha = SHA256.Create())
			{
				byte[] lyHash = lOSha.ComputeHash(pyData);
				StringBuilder lOHex = new StringBuilder(lyHash.Length * 2);

				foreach (byte lyByte in lyHash)
					lOHex.Append(lyByte.ToString("x2"));

				return lOHex.ToString();
			}
		}
	}
}
