using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S CREATURE ART (UW2 plan phase 2 step 1, 2026-10-29), from the CRIT folder next
	/// to DATA, as Underworld Exhumed's matched sources read it:
	///
	/// - AS.AN, 0x80 bytes: per creature type (item id - 0x40) its critter map and palette, 0xFF for
	///   none (uw2 critter/CRPAGES.C:50, preload_cr into grs_3dinf; 3d/PGCACHE.ASM's header, E049).
	/// - CR.AN, 0x4000 bytes: per critter map eight sequences of 0x40 bytes - eight view angles of
	///   eight bytes, the sequence's frame count in byte 7 (critter/AI.C:107, struct Seq; :1011,
	///   set_cur_seq_len reads seqptr[map * 8 + sequence].len).
	/// - PG.MP, 0x100 bytes: per critter map eight bytes, the last frame number in each of its pages
	///   (CmapFrm). The renderer takes the first of the first seven not below the frame, else the
	///   eighth (PGCACHE.ASM do_uwcrit, port/3d/pgcache_x.c L7EBB-L7EC8).
	/// - CRmm.0p, the pages: mm the map in two octal digits, p the page (CRPAGES.C:98-108, the name
	///   "crit\cr??.0?"). A page holds 32-byte palettes from 0 (palette n at n x 32, each 32 indices
	///   into the main palette) and a word per frame number at 0x80, the frame's offset, 0 when the
	///   frame is in another page; a frame is width, height, hot spot x and y, the format, then the
	///   pixels (PGCACHE.ASM parse_cpg, :907). The formats are uncmp_tab's indexes times two (:118):
	///   6 the 5-bit runs, 8 the 4-bit runs - the first game's critter encodings, decoded by
	///   UWRleDecoder after a word of length as there (UWCritterAnimations).
	///
	/// The first game's art (ASSOC.ANM and CRxxPAGE.Nyy) is UWCritterAnimations'.
	/// </summary>
	public sealed class UWCritterArtUw2
	{
		public sealed class Frame
		{
			public int Width;

			public int Height;

			public int HotspotX;

			public int HotspotY;

			/// <summary>The format byte: 6 or 8 (see the class).</summary>
			public int Format;

			/// <summary>Width x Height indices into one of the page's palettes, row by row from the top;
			/// null for a format this class does not decode.</summary>
			public byte[] Pixels;
		}

		public sealed class Page
		{
			/// <summary>The page's palettes, 32 main-palette indices each.</summary>
			public byte[][] Palettes;

			/// <summary>The frames in this page by their number.</summary>
			public readonly Dictionary<int, Frame> Frames = new Dictionary<int, Frame>();
		}

		public const string AssociationFile = "AS.AN";

		public const string SequenceFile = "CR.AN";

		public const string PageMapFile = "PG.MP";

		public const int TypeCount = 64;

		public const int MapCount = 32;

		public const int SequenceCount = 8;

		public const int AngleCount = 8;

		public const int AngleSize = 8;

		public const int SequenceSize = AngleCount * AngleSize;

		public const int PagesPerMap = 8;

		/// <summary>Where a sequence keeps its frame count (struct Seq's len).</summary>
		public const int SequenceLengthByte = 7;

		public const int PaletteSize = 32;

		public const int PaletteCount = FrameTableOffset / PaletteSize;

		public const int FrameTableOffset = 0x80;

		public const int FrameNumbers = 0x100;

		/// <summary>The byte for "none" in AS.AN.</summary>
		public const int NoMap = 0xFF;

		public const int FormatFiveBitRuns = 6;

		public const int FormatFourBitRuns = 8;

		/// <summary>A frame's header: width, height, hot spot x and y, the format, the length word.</summary>
		public const int FrameHeaderSize = 7;

		private readonly string msCritPath;

		private readonly byte[] myAssociations;

		private readonly byte[] mySequences;

		private readonly byte[] myPageMap;

		private readonly Dictionary<int, Page> mOPages = new Dictionary<int, Page>();

		/// <summary>Whether AS.AN, CR.AN and PG.MP were read whole.</summary>
		public bool IsLoaded { get; }

		/// <summary>The CRIT folder next to the data folder psDataPath.</summary>
		public UWCritterArtUw2(string psDataPath)
		{
			string lsRoot = Path.GetDirectoryName(psDataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			msCritPath = Path.Combine(lsRoot, "CRIT");

			myAssociations = fRead(AssociationFile, TypeCount * 2);
			mySequences = fRead(SequenceFile, MapCount * SequenceCount * SequenceSize);
			myPageMap = fRead(PageMapFile, MapCount * PagesPerMap);

			IsLoaded = myAssociations != null && mySequences != null && myPageMap != null;
		}

		private byte[] fRead(string psName, int piLength)
		{
			string lsFile = Path.Combine(msCritPath, psName);

			if (!File.Exists(lsFile))
				return null;

			byte[] lyData = File.ReadAllBytes(lsFile);

			return lyData.Length >= piLength ? lyData : null;
		}

		/// <summary>The critter map of a creature type (0 to 63), NoMap for none.</summary>
		public int MapOf(int piType)
		{
			return IsLoaded && piType >= 0 && piType < TypeCount ? myAssociations[piType * 2] : NoMap;
		}

		/// <summary>The palette (of the map's pages) a creature type is drawn with.</summary>
		public int PaletteOf(int piType)
		{
			return IsLoaded && piType >= 0 && piType < TypeCount ? myAssociations[(piType * 2) + 1] : 0;
		}

		/// <summary>PG.MP's byte: the last frame number in a page of a map.</summary>
		public int LastFrameOfPage(int piMap, int piPage)
		{
			return myPageMap[(piMap * PagesPerMap) + piPage];
		}

		/// <summary>The page of a map holding a frame, as the renderer picks it.</summary>
		public int PageOfFrame(int piMap, int piFrame)
		{
			for (int liPage = 0; liPage < PagesPerMap - 1; liPage++)
			{
				if (piFrame <= LastFrameOfPage(piMap, liPage))
					return liPage;
			}

			return PagesPerMap - 1;
		}

		/// <summary>A sequence's frame count (CR.AN, byte 7 of its first angle).</summary>
		public int SequenceLength(int piMap, int piSequence)
		{
			return mySequences[(((piMap * SequenceCount) + piSequence) * SequenceSize) + SequenceLengthByte];
		}

		/// <summary>The eight bytes of a sequence in one view angle, as stored.</summary>
		public byte[] SequenceBytes(int piMap, int piSequence, int piAngle)
		{
			byte[] lyResult = new byte[AngleSize];
			System.Array.Copy(mySequences, (((piMap * SequenceCount) + piSequence) * SequenceSize) + (piAngle * AngleSize), lyResult, 0, AngleSize);

			return lyResult;
		}

		/// <summary>The page file's name: "CR" + the map in two octal digits + ".0" + the page.</summary>
		public static string PageFileName(int piMap, int piPage)
		{
			return "CR" + (char)('0' + ((piMap >> 3) & 7)) + (char)('0' + (piMap & 7)) + ".0" + (char)('0' + (piPage & 7));
		}

		/// <summary>A page of a map, decoded and cached; null when its file is missing.</summary>
		public Page GetPage(int piMap, int piPage)
		{
			int liKey = (piMap * PagesPerMap) + piPage;

			if (mOPages.TryGetValue(liKey, out Page lOCached))
				return lOCached;

			string lsFile = Path.Combine(msCritPath, PageFileName(piMap, piPage));
			Page lOPage = File.Exists(lsFile) ? ReadPage(File.ReadAllBytes(lsFile)) : null;
			mOPages[liKey] = lOPage;

			return lOPage;
		}

		/// <summary>A frame of a map from the page the renderer would take it from; null when it is not there.</summary>
		public Frame GetFrame(int piMap, int piFrame)
		{
			Page lOPage = GetPage(piMap, PageOfFrame(piMap, piFrame));

			return lOPage != null && lOPage.Frames.TryGetValue(piFrame, out Frame lOFrame) ? lOFrame : null;
		}

		/// <summary>
		/// HOW BIG THE SECOND GAME DRAWS A CREATURE (UW2 plan phase 2, the size comparison of 2026-10-29 with
		/// a DOS save made beside the castle's cook: ours stood about twice as tall). in_scalebm sizes a
		/// sprite by its picture times the bytes at +0x0A and +0x0C of its record (Underworld Exhumed uw2
		/// 3d/TMAPOPS.ASM, do_scalebm). Read from UW2.EXE's data segment (file 0x5EF2C on): the object
		/// records hold 0x20 and 0x28 - the first game's 4 and 5 in eighths, the same 4:5 its objects and
		/// critters both have (UW.EXE, file 0x554E8 on) -, the critter record (do_uwcrit's, C848) 0x18 and
		/// 0x18. So against an object's pixel (the first game's unit) a creature's pixel is 0x18 / 0x20 wide
		/// and as high: three quarters, square.
		/// </summary>
		public const int CreatureScale = 0x18;

		public const int ObjectScaleX = 0x20;

		/// <summary>A creature pixel in the first game's units, both ways (CreatureScale / ObjectScaleX).</summary>
		public const float CreaturePixelScale = (float)CreatureScale / ObjectScaleX;

		/// <summary>No picture (CR.AN's 0xFF): the renderer draws nothing.</summary>
		public const int NoPicture = 0xFF;

		/// <summary>The goal that draws a creature in palette 3 (do_uwcrit's flag, OBJ_GOAL == 0xF;
		/// uw2 3d/DRAWOBJ.C:204).</summary>
		public const int StoneGoal = 0xF;

		public const int StonePalette = 3;

		/// <summary>
		/// THE PICTURE A CREATURE SHOWS (UW2 plan phase 2 step 3; Underworld Exhumed uw2 3d/DRAWOBJ.C:197-215):
		/// CR.AN's byte at ((map x 8 + sequence) x 8 + view) x 8 + frame, the view the creature's facing
		/// against the camera as in the first game (dirtab is the first game's ViewByAngle,
		/// UWCritterAnimations.GetViewDirection). NoPicture when the byte is 0xFF or the type has no map.
		/// </summary>
		public int PictureFor(int piType, int piSequence, int piView, int piFrame)
		{
			int liMap = MapOf(piType);

			if (liMap == NoMap)
				return NoPicture;

			return SequenceBytes(liMap, piSequence & (SequenceCount - 1), piView & (AngleCount - 1))[piFrame & (AngleSize - 1)];
		}

		/// <summary>The sequence a creature record holds: byte 0x15 bits 0-5 (OBJ_SEQ; object.h:272).</summary>
		public static int SequenceOf(UWNpc pONpc)
		{
			return pONpc != null && pONpc.RawNpcBytes != null ? pONpc.RawNpcBytes[UWNpcRecordCodec.AnimationByte] & UWNpcRecordCodec.AnimationMask : 0;
		}

		/// <summary>The frame a creature record holds: word 0x0B bits 12-15 (OBJ_FRAME; object.h:226).</summary>
		public static int FrameOf(UWNpc pONpc)
		{
			return pONpc != null && pONpc.RawNpcBytes != null ? (UWBinary.Read16(pONpc.RawNpcBytes, UWNpcRecordCodec.GoalWord) >> 12) & 0xF : 0;
		}

		/// <summary>The palette a creature is drawn with: its type's, palette 3 at the stone goal.</summary>
		public int DrawPaletteOf(int piType, int piGoal)
		{
			return piGoal == StoneGoal ? StonePalette : PaletteOf(piType);
		}

		/// <summary>A page file's palettes and frames.</summary>
		public static Page ReadPage(byte[] pyData)
		{
			Page lOPage = new Page { Palettes = new byte[PaletteCount][] };

			for (int liPalette = 0; liPalette < PaletteCount; liPalette++)
			{
				lOPage.Palettes[liPalette] = new byte[PaletteSize];
				System.Array.Copy(pyData, liPalette * PaletteSize, lOPage.Palettes[liPalette], 0, PaletteSize);
			}

			for (int liFrame = 0; liFrame < FrameNumbers && FrameTableOffset + (2 * liFrame) + 1 < pyData.Length; liFrame++)
			{
				int liOffset = UWBinary.Read16(pyData, FrameTableOffset + (2 * liFrame));

				if (liOffset == 0 || liOffset + FrameHeaderSize > pyData.Length)
					continue;

				Frame lOFrame = new Frame
				{
					Width = pyData[liOffset],
					Height = pyData[liOffset + 1],
					HotspotX = pyData[liOffset + 2],
					HotspotY = pyData[liOffset + 3],
					Format = pyData[liOffset + 4]
				};

				if (lOFrame.Format == FormatFiveBitRuns || lOFrame.Format == FormatFourBitRuns)
				{
					lOFrame.Pixels = UWRleDecoder.Decode(pyData, liOffset + FrameHeaderSize,
						lOFrame.Format == FormatFiveBitRuns ? 5 : 4, lOFrame.Width * lOFrame.Height);
				}

				lOPage.Frames[liFrame] = lOFrame;
			}

			return lOPage;
		}
	}
}
