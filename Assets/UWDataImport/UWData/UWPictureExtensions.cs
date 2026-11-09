using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MODS' EXTENDED PICTURES for one game (Docs/WIDESCREEN-WEAPONS-PLAN.md 4.2 and 4.3, step 2, 2026-11-09).
	/// A pack lays its pictures out as
	///
	///   pictures/UW1/WEAPONS.GR/000.png ... 0DF.png      one per picture, named by its number in hex
	///   pictures/UW1/WEAPONS.GR/extend.json              where the original lies in them (optional)
	///
	/// A picture the mods do not have stays the original's. Two mods with the same picture: the later in the
	/// set's order wins, with its own shift. UW1's left hand (WEAPONS.GR 0x70 to 0xDF) a pack does not paint is
	/// the right hand's extension mirrored, as the original's left hand mirrors its right - the artist paints
	/// 112 pictures, not 224. Each picture is composed when first asked for and kept; the originals come from
	/// the player's files (pOriginal) and stay untouched.
	/// </summary>
	public sealed class UWPictureExtensions
	{
		/// <summary>The pack's folder of pictures.</summary>
		public const string PicturesFolder = "pictures";

		/// <summary>UW1's weapon pictures: the right hand 0x00 to 0x6F, the left hand mirrors it from 0x70.</summary>
		public const int Uw1LeftHand = 0x70;

		public const int Uw1WeaponPictures = 0xE0;

		private sealed class Source
		{
			public UWModCatalog.Entry Mod;

			public string Path;

			public string Folder;
		}

		private readonly UWGame meGame;

		private readonly Func<UWTexture.TextureTypes, int, UWTexture> mOriginal;

		private readonly Dictionary<(UWTexture.TextureTypes, int), Source> mOSources = new Dictionary<(UWTexture.TextureTypes, int), Source>();

		private readonly Dictionary<(UWTexture.TextureTypes, int), UWExtendedPicture> mOMade = new Dictionary<(UWTexture.TextureTypes, int), UWExtendedPicture>();

		private readonly Dictionary<(string, string), UWPictureExtension.Shifts> mOShifts = new Dictionary<(string, string), UWPictureExtension.Shifts>();

		private readonly List<string> mOProblems = new List<string>();

		private readonly List<string> mOCredits = new List<string>();

		private readonly object mOLock = new object();

		/// <summary>The set's key (UWModSet.Key): the same mods in the same order give the same pictures.</summary>
		public string Key { get; }

		/// <summary>How many pictures the mods bring (the mirrored left hand not counted).</summary>
		public int Count => mOSources.Count;

		/// <summary>The credit of every mod that brings a picture, in order (mod.json's credit, else its name or id).</summary>
		public IReadOnlyList<string> Credits => mOCredits;

		/// <summary>What could not be used, each once: a file of no picture, an unreadable PNG, a broken extend.json.</summary>
		public List<string> Problems
		{
			get
			{
				lock (mOLock)
					return new List<string>(mOProblems);
			}
		}

		/// <summary>The pictures of the set's mods for one game; pOriginal gives the player's own picture.</summary>
		public UWPictureExtensions(UWModSet pOSet, UWGame peGame, Func<UWTexture.TextureTypes, int, UWTexture> pOriginal)
		{
			meGame = peGame;
			mOriginal = pOriginal;
			Key = pOSet?.Key ?? string.Empty;

			if (pOSet == null)
				return;

			string lsPrefix = PicturesFolder + "/" + GameFolder(peGame) + "/";

			foreach (UWModCatalog.Entry lOMod in pOSet.Mods)
			{
				bool lbAny = false;

				foreach (string lsPath in lOMod.Source.ListFiles())
				{
					if (!lsPath.StartsWith(lsPrefix, StringComparison.OrdinalIgnoreCase))
						continue;

					string[] lsParts = lsPath.Substring(lsPrefix.Length).Split('/');

					if (lsParts.Length != 2 || string.Equals(lsParts[1], "extend.json", StringComparison.OrdinalIgnoreCase))
						continue;

					if (!lsParts[1].EndsWith(".png", StringComparison.OrdinalIgnoreCase))
						continue;

					string lsName = lsParts[1].Substring(0, lsParts[1].Length - 4);

					if (!TryParseFile(lsParts[0], out UWTexture.TextureTypes leType) || !UWPictureExtension.TryParseIndex(lsName, out int liIndex))
					{
						fProblem(lOMod.Id + ": " + lsPath + " is no picture of the game's .GR files");
						continue;
					}

					mOSources[(leType, liIndex)] = new Source { Mod = lOMod, Path = lsPath, Folder = lsPath.Substring(0, lsPath.LastIndexOf('/')) };
					lbAny = true;
				}

				if (lbAny)
				{
					if (!lOMod.Source.IsZip)
						FromFolder = true;

					string lsCredit = lOMod.Manifest.Credit ?? lOMod.Manifest.Name ?? lOMod.Id;

					if (!mOCredits.Contains(lsCredit))
						mOCredits.Add(lsCredit);
				}
			}
		}

		/// <summary>The game's folder in a pack: "UW1", "UW2".</summary>
		public static string GameFolder(UWGame peGame)
		{
			return peGame == UWGame.Uw2 ? "UW2" : "UW1";
		}

		/// <summary>A .GR file's folder name ("WEAPONS.GR", "3DWIN.GR") as its kind of picture.</summary>
		public static bool TryParseFile(string psFolder, out UWTexture.TextureTypes peType)
		{
			peType = default;

			if (string.IsNullOrEmpty(psFolder) || !psFolder.EndsWith(".GR", StringComparison.OrdinalIgnoreCase))
				return false;

			string lsStem = psFolder.Substring(0, psFolder.Length - 3).ToUpperInvariant();

			if (lsStem == "3DWIN")
				lsStem = UWTexture.TextureTypes.THRREDWIN.ToString();

			return Enum.TryParse(lsStem, false, out peType) && lsStem == peType.ToString() && !UWPicturePack.IsScreen(peType);
		}

		/// <summary>Whether a mod bringing pictures lies in a folder - an artist's work in progress, read anew at every load.</summary>
		public bool FromFolder { get; private set; }

		/// <summary>
		/// Composes every picture at once (each is small, a few hundred in all), so what cannot be used is in
		/// Problems at the game's start and not only when a weapon first swings. Returns how many were made,
		/// the mirrored left hand included.
		/// </summary>
		public int ComposeAll()
		{
			List<(UWTexture.TextureTypes Type, int Index)> lOAll = new List<(UWTexture.TextureTypes Type, int Index)>(mOSources.Keys);

			if (meGame == UWGame.Uw1)
			{
				for (int liAt = Uw1LeftHand; liAt < Uw1WeaponPictures; liAt++)
				{
					if (fTwin(UWTexture.TextureTypes.WEAPONS, liAt, out _))
						lOAll.Add((UWTexture.TextureTypes.WEAPONS, liAt));
				}
			}

			int liMade = 0;

			foreach ((UWTexture.TextureTypes Type, int Index) lPicture in lOAll)
			{
				if (Get(lPicture.Type, lPicture.Index) != null)
					liMade++;
			}

			return liMade;
		}

		/// <summary>Whether the mods bring this picture (or, for UW1's left hand, its right-hand twin).</summary>
		public bool Has(UWTexture.TextureTypes peType, int piIndex)
		{
			return mOSources.ContainsKey((peType, piIndex)) || fTwin(peType, piIndex, out _);
		}

		/// <summary>The extended picture, or null where the mods have none or it cannot be used (Problems says why).</summary>
		public UWExtendedPicture Get(UWTexture.TextureTypes peType, int piIndex)
		{
			if (!Has(peType, piIndex))
				return null;

			lock (mOLock)
			{
				if (mOMade.TryGetValue((peType, piIndex), out UWExtendedPicture lOMade))
					return lOMade;

				lOMade = fMake(peType, piIndex);
				mOMade[(peType, piIndex)] = lOMade;

				return lOMade;
			}
		}

		private UWExtendedPicture fMake(UWTexture.TextureTypes peType, int piIndex)
		{
			UWTexture lOOriginal = fOriginal(peType, piIndex);

			if (lOOriginal == null)
				return null;

			bool lbMirror = !mOSources.TryGetValue((peType, piIndex), out Source lOSource);
			int liFrom = piIndex;

			if (lbMirror)
			{
				fTwin(peType, piIndex, out liFrom);
				lOSource = mOSources[(peType, liFrom)];
			}

			string lsName = lOSource.Mod.Id + ": " + lOSource.Path;
			UWPngReader.Image lOImage = UWPngReader.Read(lOSource.Mod.Source.ReadAllBytes(lOSource.Path), out string lsError);

			if (lOImage == null)
			{
				fProblem(lsName + " - " + (lsError ?? "cannot be read"));

				return null;
			}

			UWPictureExtension.Shifts lOShifts = fShifts(lOSource);

			if (lOShifts == null)
				return null;

			(int Left, int Top) lShift = lOShifts.For(liFrom, lOImage);
			UWColor32[] lyPixels = lOImage.Pixels;
			int liLeft = lShift.Left;

			if (lbMirror)
			{
				// The shift belongs to the right hand's original; its width places the mirrored one.
				UWTexture lOTwin = fOriginal(peType, liFrom);

				if (lOTwin == null)
					return null;

				lyPixels = UWPictureExtension.Mirror(lyPixels, lOImage.Width, lOImage.Height, lShift.Left, lOTwin.Width, out liLeft);
			}

			UWExtendedPicture lOMade = UWPictureExtension.Compose(lOOriginal, lyPixels, lOImage.Width, lOImage.Height, liLeft, lShift.Top, out string lsProblem);

			if (lOMade == null)
			{
				fProblem(lsName + " - " + lsProblem);

				return null;
			}

			lOMade.ModId = lOSource.Mod.Id;
			lOMade.Mirrored = lbMirror;

			return lOMade;
		}

		/// <summary>UW1's left-hand weapon picture without its own: the right hand's number, where the mods have that.</summary>
		private bool fTwin(UWTexture.TextureTypes peType, int piIndex, out int piRight)
		{
			piRight = piIndex - Uw1LeftHand;

			return meGame == UWGame.Uw1 && peType == UWTexture.TextureTypes.WEAPONS && piIndex >= Uw1LeftHand && piIndex < Uw1WeaponPictures
				&& !mOSources.ContainsKey((peType, piIndex)) && mOSources.ContainsKey((peType, piRight));
		}

		private UWTexture fOriginal(UWTexture.TextureTypes peType, int piIndex)
		{
			try
			{
				return mOriginal?.Invoke(peType, piIndex);
			}
			catch (Exception lOError) when (lOError is ArgumentOutOfRangeException || lOError is KeyNotFoundException || lOError is IndexOutOfRangeException)
			{
				fProblem(peType + " " + piIndex.ToString("X3") + ": the game has no such picture");

				return null;
			}
		}

		/// <summary>The extend.json of a source's folder, read once per mod and folder; null where it is broken.</summary>
		private UWPictureExtension.Shifts fShifts(Source pOSource)
		{
			(string, string) lKey = (pOSource.Mod.Id, pOSource.Folder);

			if (mOShifts.TryGetValue(lKey, out UWPictureExtension.Shifts lOShifts))
				return lOShifts;

			string lsJson = pOSource.Mod.Source.ReadAllText(pOSource.Folder + "/extend.json");

			if (lsJson == null)
				lOShifts = UWPictureExtension.NoShifts;
			else
			{
				lOShifts = UWPictureExtension.ParseShifts(lsJson, out string lsError);

				if (lOShifts == null)
					fProblem(pOSource.Mod.Id + ": " + pOSource.Folder + "/extend.json - " + lsError);
			}

			mOShifts[lKey] = lOShifts;

			return lOShifts;
		}

		private void fProblem(string psProblem)
		{
			lock (mOLock)
			{
				if (!mOProblems.Contains(psProblem))
					mOProblems.Add(psProblem);
			}
		}
	}
}
