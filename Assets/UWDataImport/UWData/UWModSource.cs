using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace UWDataImport.UWData
{
	/// <summary>
	/// ONE MOD'S CONTAINER (Docs/WIDESCREEN-WEAPONS-PLAN.md 4.1, built 2026-11-09): a folder or a zip -
	/// ours, or the editor's .uwmod - READ IN PLACE, never unpacked. Its files by their path inside the mod,
	/// '/'-separated, as mod.json and the code name them; a zip whose files all lie in one top folder is
	/// read from inside that folder (players zip a mod's folder). A zip is opened for each read, so no file
	/// stays locked while the game runs and a mod can be swapped between two loads.
	/// </summary>
	public sealed class UWModSource
	{
		/// <summary>Where the mod lies: its folder or its zip.</summary>
		public string Location { get; }

		public bool IsZip { get; }

		/// <summary>A zip's top folder its files lie in ("" for none), with its '/'.</summary>
		private readonly string msZipRoot = string.Empty;

		private UWModSource(string psLocation, bool pbZip, string psZipRoot)
		{
			Location = psLocation;
			IsZip = pbZip;
			msZipRoot = psZipRoot ?? string.Empty;
		}

		public static UWModSource Folder(string psFolder)
		{
			return new UWModSource(psFolder, false, null);
		}

		/// <summary>A zip, read from its one top folder where every file lies in it; null where it cannot be read.</summary>
		public static UWModSource Zip(string psZip)
		{
			try
			{
				using (ZipArchive lOZip = ZipFile.OpenRead(psZip))
				{
					string lsTop = null;
					bool lbOneTop = true;

					foreach (ZipArchiveEntry lOEntry in lOZip.Entries)
					{
						string lsName = fClean(lOEntry.FullName);
						int liSlash = lsName.IndexOf('/');

						if (liSlash < 0)
						{
							if (lsName.Length > 0)
								lbOneTop = false;

							continue;
						}

						string lsFirst = lsName.Substring(0, liSlash + 1);

						if (lsTop == null)
							lsTop = lsFirst;
						else if (!string.Equals(lsTop, lsFirst, StringComparison.OrdinalIgnoreCase))
							lbOneTop = false;
					}

					// A zip that holds mod.json at its root is read from its root.
					bool lbRootManifest = lOZip.GetEntry(UWModManifest.FileName) != null;

					return new UWModSource(psZip, true, lbOneTop && lsTop != null && !lbRootManifest ? lsTop : null);
				}
			}
			catch (Exception lOError) when (lOError is IOException || lOError is InvalidDataException || lOError is UnauthorizedAccessException)
			{
				return null;
			}
		}

		/// <summary>Whether the mod has the file.</summary>
		public bool Has(string psPath)
		{
			return ReadAllBytes(psPath) != null;
		}

		/// <summary>The file's bytes, null where the mod has none.</summary>
		public byte[] ReadAllBytes(string psPath)
		{
			string lsPath = fClean(psPath);

			try
			{
				if (!IsZip)
				{
					string lsFile = Path.Combine(Location, lsPath.Replace('/', Path.DirectorySeparatorChar));

					return File.Exists(lsFile) ? File.ReadAllBytes(lsFile) : null;
				}

				using (ZipArchive lOZip = ZipFile.OpenRead(Location))
				{
					ZipArchiveEntry lOEntry = fFind(lOZip, msZipRoot + lsPath);

					if (lOEntry == null)
						return null;

					using (Stream lOStream = lOEntry.Open())
					using (MemoryStream lOCopy = new MemoryStream())
					{
						lOStream.CopyTo(lOCopy);

						return lOCopy.ToArray();
					}
				}
			}
			catch (Exception lOError) when (lOError is IOException || lOError is InvalidDataException || lOError is UnauthorizedAccessException)
			{
				return null;
			}
		}

		/// <summary>The file as UTF-8 text, null where the mod has none.</summary>
		public string ReadAllText(string psPath)
		{
			byte[] lyBytes = ReadAllBytes(psPath);

			return lyBytes != null ? System.Text.Encoding.UTF8.GetString(lyBytes) : null;
		}

		/// <summary>Every file's path in the mod, '/'-separated.</summary>
		public List<string> ListFiles()
		{
			List<string> lsFiles = new List<string>();

			try
			{
				if (!IsZip)
				{
					if (!Directory.Exists(Location))
						return lsFiles;

					foreach (string lsFile in Directory.GetFiles(Location, "*", SearchOption.AllDirectories))
						lsFiles.Add(fClean(lsFile.Substring(Location.Length)));

					return lsFiles;
				}

				using (ZipArchive lOZip = ZipFile.OpenRead(Location))
				{
					foreach (ZipArchiveEntry lOEntry in lOZip.Entries)
					{
						string lsName = fClean(lOEntry.FullName);

						if (lsName.Length == 0 || lsName.EndsWith("/", StringComparison.Ordinal))
							continue;

						if (msZipRoot.Length > 0)
						{
							if (!lsName.StartsWith(msZipRoot, StringComparison.OrdinalIgnoreCase))
								continue;

							lsName = lsName.Substring(msZipRoot.Length);
						}

						lsFiles.Add(lsName);
					}
				}
			}
			catch (Exception lOError) when (lOError is IOException || lOError is InvalidDataException || lOError is UnauthorizedAccessException)
			{
			}

			return lsFiles;
		}

		/// <summary>An entry by its path, the case ignored (a zip made on Windows or Linux).</summary>
		private static ZipArchiveEntry fFind(ZipArchive pOZip, string psPath)
		{
			ZipArchiveEntry lOEntry = pOZip.GetEntry(psPath);

			if (lOEntry != null)
				return lOEntry;

			foreach (ZipArchiveEntry lOCandidate in pOZip.Entries)
			{
				if (string.Equals(fClean(lOCandidate.FullName), psPath, StringComparison.OrdinalIgnoreCase))
					return lOCandidate;
			}

			return null;
		}

		private static string fClean(string psPath)
		{
			return (psPath ?? string.Empty).Replace('\\', '/').TrimStart('/');
		}

		public override string ToString()
		{
			return Location;
		}
	}
}
