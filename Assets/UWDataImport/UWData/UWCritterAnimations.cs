using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The creature animations from the "crit" folder (uw-formats.txt 3.6.1). Until now
	/// monsters in the project were plain still images from OBJECTS.GR - this is the
	/// data foundation for letting them walk, attack and die.
	///
	/// "assoc.anm" assigns each of the 64 NPC types one of 32 animations and an
	/// auxiliary palette. Several creatures share the same animation and differ
	/// only by palette - bat and vampire bat, for example.
	///
	/// The images are in "crXXpage.nYY", XX animation number and YY page number, both
	/// OCTAL. An animation can have several pages.
	/// </summary>
	public class UWCritterAnimations
	{
		/// <summary>A single image of an animation, already decoded into palette
		/// indices.</summary>
		public class Frame
		{
			public int Width;

			public int Height;

			/// <summary>Anchor point: when drawing, this point of the image should always sit
			/// at the same spot, so the figure does not jump when the frames
			/// change.</summary>
			public int HotspotX;

			public int HotspotY;

			/// <summary>Width*Height palette indices, row by row from top to bottom.</summary>
			public byte[] Pixels;
		}

		/// <summary>One page of an animation: segment lists, auxiliary palettes and images.</summary>
		public class Page
		{
			/// <summary>First slot covered by this page.</summary>
			public int SlotBase;

			/// <summary>Per slot the index into Segments; 0xFF means "not used".</summary>
			public byte[] SlotSegments;

			/// <summary>Per segment up to eight frame indices, padded with 0xFF.</summary>
			public byte[][] Segments;

			/// <summary>Per auxiliary palette 32 indices into the main palette.</summary>
			public byte[][] AuxPalettes;

			public Frame[] Frames;
		}

		/// <summary>Slots whose meaning is documented. The values from 0x20 are
		/// facing directions while standing, those from 0x80 the same directions while walking.</summary>
		public const int SlotCombatIdle = 0x00;

		public const int SlotAttackBash = 0x01;

		public const int SlotAttackSlash = 0x02;

		public const int SlotAttackThrust = 0x03;

		/// <summary>The ranged attack - slinging, throwing, shooting. The reference calls
		/// exactly this five its ranged attack animation (npcai.ANIMATION_RANGEDATTACK, uw1).
		/// </summary>
		public const int SlotRangedAttack = 0x05;

		/// <summary>Spellcasting (npcai.ANIMATION_MAGICATTACK, uw1).</summary>
		public const int SlotMagicAttack = 0x0D;

		public const int SlotWalkTowardsPlayer = 0x07;

		public const int SlotDeath = 0x0C;

		/// <summary>Standing, facing away from the player. The eight directions run up to 0x27,
		/// 0x24 is "facing the player".</summary>
		public const int SlotIdleFirstDirection = 0x20;

		/// <summary>Like SlotIdleFirstDirection, but walking.</summary>
		public const int SlotWalkFirstDirection = 0x80;

		public const int DirectionCount = 8;

		/// <summary>The view seen from straight in front (slot 0x24).</summary>
		public const int FrontView = 4;

		/// <summary>
		/// AnimationHeadingTable_6C2: the picture direction for the creature's facing relative to
		/// the camera's yaw, in 32 steps of 11.25 degrees (0 = the creature looks the way the
		/// camera looks, i.e. the viewer sees its back). NOT EVEN: the four straight views (back,
		/// right, front, left) take five steps each, 56.25 degrees, the four diagonals three, 33.75.
		/// </summary>
		public static readonly int[] ViewByAngle =
		{
			0, 0, 0, 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4, 4,
			4, 4, 4, 5, 5, 5, 6, 6, 6, 6, 6, 7, 7, 7, 0, 0
		};

		/// <summary>
		/// THE PICTURE DIRECTION as the original's renderer picks it, NPC_seg032_2DCA_216 (line
		/// 110171, the NPC case of RenderingObjects) - READ 2026-10-06: the facing eighth (word 2
		/// bits 7-9) times four, plus 0x20, minus the CAMERA'S YAW in 32nds (the 16-bit yaw
		/// &gt;&gt; 11, the camera's own angle plus its cardinal from the table at 45C), masked to
		/// 0x1F, looked up in ViewByAngle. It is the camera's direction of view that counts, not
		/// the line from the camera to the creature: every creature on the screen is judged
		/// against the same yaw.
		/// </summary>
		public static int GetViewDirection(int piFacingEighth, int piCameraYaw)
		{
			return ViewByAngle[(((piFacingEighth & 7) * 4) + 0x20 - ((piCameraYaw & 0xFFFF) >> 11)) & 0x1F];
		}

		/// <summary>Whether this view shows the target-relative pages (combat, ranged, spell,
		/// back-off): the front and the two front diagonals, views 3 to 5 - the renderer's
		/// ((view + 5) &amp; 7) &lt; 3. From the other five the standing picture of that view.</summary>
		public static bool ShowsTargetPage(int piView)
		{
			return ((piView + 5) & 7) < 3;
		}

		/// <summary>
		/// The slot the renderer draws for an animation (byte 0x15 bits 0-5) in a view: from 0x20
		/// on the direction sets, (animation - 0x20) * 8 + 0x20 + view - so 0x20 standing gives
		/// 0x20-0x27 and 0x2C walking 0x80-0x87; dying (0x0C) from every side; any other
		/// animation its own page where ShowsTargetPage, else the standing picture of the view.
		/// </summary>
		public static int GetSlot(int piAnimation, int piView)
		{
			if (piAnimation >= SlotIdleFirstDirection)
				return ((piAnimation - SlotIdleFirstDirection) * DirectionCount) + SlotIdleFirstDirection + piView;

			if (piAnimation == SlotDeath || ShowsTargetPage(piView))
				return piAnimation;

			return SlotIdleFirstDirection + piView;
		}

		/// <summary>NPC object ids start at 0x40; the mapping table is
		/// zero-based.</summary>
		public const int NpcFirstObjectId = 0x40;

		private const int AnimationCount = 32;

		private const int NpcCount = 64;

		private const int NameLength = 8;

		/// <summary>Compression type 06 means 5 bits per word.</summary>
		private const int WordSizeType06 = 5;

		private const int WordSizeType08 = 4;

		private readonly string msCritPath;

		private readonly string[] msAnimationNames = new string[AnimationCount];

		private readonly byte[] myNpcAnimation = new byte[NpcCount];

		private readonly byte[] myNpcAuxPalette = new byte[NpcCount];

		private readonly Dictionary<string, Page> mOPageCache = new Dictionary<string, Page>();

		public bool IsLoaded { get; private set; }

		/// <summary>Name of the animation as stored in assoc.anm ("bat", "gngob32", ...).
		/// Empty means: this animation does not exist in this installation.</summary>
		public string GetAnimationName(int piAnimation)
		{
			return piAnimation >= 0 && piAnimation < AnimationCount ? msAnimationNames[piAnimation] : string.Empty;
		}

		/// <summary>Animation number of an NPC object. -1 if there is none (0xFF in the
		/// data).</summary>
		public int GetAnimationForObject(int piObjectId)
		{
			int liIndex = piObjectId - NpcFirstObjectId;

			if (!IsLoaded || liIndex < 0 || liIndex >= NpcCount || myNpcAnimation[liIndex] == 0xFF)
				return -1;

			return myNpcAnimation[liIndex];
		}

		/// <summary>Auxiliary palette of an NPC object - distinguishes creatures that share
		/// the same images.</summary>
		public int GetAuxPaletteForObject(int piObjectId)
		{
			int liIndex = piObjectId - NpcFirstObjectId;

			return !IsLoaded || liIndex < 0 || liIndex >= NpcCount ? 0 : myNpcAuxPalette[liIndex];
		}

		public UWCritterAnimations(string psDataPath)
		{
			// The "crit" folder sits NEXT TO "data", not inside it - just like "cuts".
			string lsRoot = Path.GetDirectoryName(psDataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			msCritPath = Path.Combine(lsRoot, "CRIT");

			string lsAssoc = Path.Combine(msCritPath, "ASSOC.ANM");

			if (!File.Exists(lsAssoc))
				return;

			byte[] lyData = File.ReadAllBytes(lsAssoc);

			if (lyData.Length < (AnimationCount * NameLength) + (NpcCount * 2))
				return;

			for (int liIndex = 0; liIndex < AnimationCount; liIndex++)
			{
				StringBuilder lOName = new StringBuilder();

				for (int liChar = 0; liChar < NameLength; liChar++)
				{
					byte lyChar = lyData[(liIndex * NameLength) + liChar];

					if (lyChar == 0)
						break;

					lOName.Append((char)lyChar);
				}

				msAnimationNames[liIndex] = lOName.ToString();
			}

			int liTableOffset = AnimationCount * NameLength;

			for (int liIndex = 0; liIndex < NpcCount; liIndex++)
			{
				myNpcAnimation[liIndex] = lyData[liTableOffset + (liIndex * 2)];
				myNpcAuxPalette[liIndex] = lyData[liTableOffset + (liIndex * 2) + 1];
			}

			IsLoaded = true;
		}

		/// <summary>Loads one page of an animation. piAnimation and piPage are written into
		/// the file name as OCTAL NUMBERS - "cr20page.n01" is animation 16
		/// decimal. The result is cached.</summary>
		public Page GetPage(int piAnimation, int piPage)
		{
			string lsFile = string.Format("CR{0}PAGE.N{1}", fToOctal(piAnimation, 2), fToOctal(piPage, 2));

			if (mOPageCache.TryGetValue(lsFile, out Page lOCached))
				return lOCached;

			Page lOPage = fLoadPage(Path.Combine(msCritPath, lsFile));
			mOPageCache[lsFile] = lOPage;

			return lOPage;
		}

		private static string fToOctal(int piValue, int piDigits)
		{
			string lsResult = System.Convert.ToString(piValue, 8);

			return lsResult.PadLeft(piDigits, '0');
		}

		private Page fLoadPage(string psFile)
		{
			if (!File.Exists(psFile))
				return null;

			byte[] lyData = File.ReadAllBytes(psFile);
			int liCursor = 0;

			Page lOPage = new Page
			{
				SlotBase = lyData[liCursor++]
			};

			int liSlotCount = lyData[liCursor++];
			lOPage.SlotSegments = new byte[liSlotCount];

			for (int liIndex = 0; liIndex < liSlotCount; liIndex++)
				lOPage.SlotSegments[liIndex] = lyData[liCursor++];

			int liSegmentCount = lyData[liCursor++];
			lOPage.Segments = new byte[liSegmentCount][];

			for (int liIndex = 0; liIndex < liSegmentCount; liIndex++)
			{
				lOPage.Segments[liIndex] = new byte[8];

				for (int liFrame = 0; liFrame < 8; liFrame++)
					lOPage.Segments[liIndex][liFrame] = lyData[liCursor++];
			}

			int liPaletteCount = lyData[liCursor++];
			lOPage.AuxPalettes = new byte[liPaletteCount][];

			for (int liIndex = 0; liIndex < liPaletteCount; liIndex++)
			{
				lOPage.AuxPalettes[liIndex] = new byte[32];

				for (int liEntry = 0; liEntry < 32; liEntry++)
					lOPage.AuxPalettes[liIndex][liEntry] = lyData[liCursor++];
			}

			int liOffsetCount = lyData[liCursor++];
			liCursor++; // Compression type of the whole file, always 06 according to the docs.

			int[] liOffsets = new int[liOffsetCount];

			for (int liIndex = 0; liIndex < liOffsetCount; liIndex++)
			{
				liOffsets[liIndex] = lyData[liCursor] | (lyData[liCursor + 1] << 8);
				liCursor += 2;
			}

			lOPage.Frames = new Frame[liOffsetCount];

			for (int liIndex = 0; liIndex < liOffsetCount; liIndex++)
				lOPage.Frames[liIndex] = fLoadFrame(lyData, liOffsets[liIndex]);

			return lOPage;
		}

		private static Frame fLoadFrame(byte[] pyData, int piOffset)
		{
			if (piOffset < 0 || piOffset + 7 > pyData.Length)
				return null;

			int liWidth = pyData[piOffset];
			int liHeight = pyData[piOffset + 1];
			int liCompression = pyData[piOffset + 4];
			int liWordSize = liCompression == 8 ? WordSizeType08 : WordSizeType06;

			// The data length in the header counts WORDS, not bytes - the decoder reads
			// until the image is full anyway, it only serves as an estimate.
			Frame lOFrame = new Frame
			{
				Width = liWidth,
				Height = liHeight,
				HotspotX = pyData[piOffset + 2],
				HotspotY = pyData[piOffset + 3],
				Pixels = UWRleDecoder.Decode(pyData, piOffset + 7, liWordSize, liWidth * liHeight)
			};

			return lOFrame;
		}
	}
}
