using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>How a row's value is set off.</summary>
	public enum UWDebugEmphasis
	{
		Normal = 0,

		/// <summary>Secondary: "(no record)", "none set".</summary>
		Dim = 1,

		/// <summary>Something wrong or dangerous: poisoned, a trap that is not built.</summary>
		Warning = 2,

		/// <summary>Worth a look: the creature's last event, an immunity.</summary>
		Accent = 3
	}

	[Flags]
	public enum UWDebugRowFlags
	{
		None = 0,

		/// <summary>Changes all the time (a position, an age): never lights up when it changes.</summary>
		Live = 1,

		/// <summary>One cell of the section's grid: consecutive cells are laid out side by side.</summary>
		Cell = 2,

		/// <summary>A small heading inside the section: its label alone, the rows under it belong to it.</summary>
		Sub = 4,

		/// <summary>A log line keyed by its identity: it lights up when it is new, not when it changes.</summary>
		Log = 8
	}

	/// <summary>One line of a section: a label and its value.</summary>
	public sealed class UWDebugRow
	{
		/// <summary>The row's identity within its section, for the highlight of a changed value (the
		/// label unless given).</summary>
		public string Key;

		public string Label;

		public string Value;

		public UWDebugEmphasis Emphasis;

		public UWDebugRowFlags Flags;

		public bool Is(UWDebugRowFlags peFlag)
		{
			return (Flags & peFlag) != 0;
		}
	}

	/// <summary>A foldable group of rows under a heading.</summary>
	public sealed class UWDebugSection
	{
		/// <summary>The section's stable name: the fold state is kept by it (UWDebugFolds), so it never
		/// changes once a section exists.</summary>
		public readonly string Id;

		public readonly string Title;

		public readonly List<UWDebugRow> Rows = new List<UWDebugRow>();

		public UWDebugSection(string psId, string psTitle)
		{
			Id = psId;
			Title = psTitle;
		}

		public UWDebugSection Add(string psLabel, string psValue, UWDebugEmphasis peEmphasis = UWDebugEmphasis.Normal,
			UWDebugRowFlags peFlags = UWDebugRowFlags.None, string psKey = null)
		{
			Rows.Add(new UWDebugRow
			{
				Key = psKey ?? psLabel,
				Label = psLabel ?? string.Empty,
				Value = psValue ?? string.Empty,
				Emphasis = peEmphasis,
				Flags = peFlags
			});

			return this;
		}

		public UWDebugSection Live(string psLabel, string psValue, string psKey = null)
		{
			return Add(psLabel, psValue, UWDebugEmphasis.Normal, UWDebugRowFlags.Live, psKey);
		}

		public UWDebugSection Cell(string psLabel, string psValue, string psKey = null)
		{
			return Add(psLabel, psValue, UWDebugEmphasis.Normal, UWDebugRowFlags.Cell, psKey);
		}

		/// <summary>A grid cell whose value ticks on its own (a frame, a phase): it never lights up.</summary>
		public UWDebugSection LiveCell(string psLabel, string psValue, string psKey = null)
		{
			return Add(psLabel, psValue, UWDebugEmphasis.Normal, UWDebugRowFlags.Cell | UWDebugRowFlags.Live, psKey);
		}

		public UWDebugSection Sub(string psLabel)
		{
			return Add(psLabel, string.Empty, UWDebugEmphasis.Dim, UWDebugRowFlags.Sub, "sub:" + psLabel);
		}

		/// <summary>The row of that key, or null.</summary>
		public UWDebugRow Find(string psKey)
		{
			foreach (UWDebugRow lORow in Rows)
			{
				if (lORow.Key == psKey)
					return lORow;
			}

			return null;
		}
	}

	/// <summary>
	/// THE DEBUG PANEL'S REPORT (cleanup 8c, 2026-10-25): what the F1 overlay showed as one white text
	/// block, as sections of label/value rows - Place, Player, Look target, Creature, Game variables,
	/// Trap log - built from a plain snapshot (UWDebugSnapshot) without Unity. The kit's panel
	/// (UWDebugPanel) draws whatever sections and rows come; a second game (UW2) adds a section here,
	/// or rows to one, without touching the drawer, and the self-check (group DebugReport) builds the
	/// report from fixture snapshots and holds it against the old overlay's text, so no line of it is
	/// lost.
	///
	/// THE TEXTS STAY ENGLISH LITERALS (developer tool, not translated, cleanup 8c), and numbers are
	/// written in the invariant culture.
	/// </summary>
	public sealed class UWDebugReport
	{
		public const string PlaceId = "place";

		public const string PlayerId = "player";

		public const string LookId = "look";

		public const string CreatureId = "creature";

		public const string VariablesId = "variables";

		public const string TrapsId = "traps";

		/// <summary>The CritterTrace mode's one section (its own view).</summary>
		public const string CritterTraceId = "critter";

		public readonly List<UWDebugSection> Sections = new List<UWDebugSection>();

		/// <summary>The CritterTrace mode: one section, the creature alone.</summary>
		public bool IsCritterTrace;

		/// <summary>The line under the sections: the key that hides the panel.</summary>
		public string Footer = string.Empty;

		/// <summary>The section of that id, or null.</summary>
		public UWDebugSection Find(string psId)
		{
			foreach (UWDebugSection lOSection in Sections)
			{
				if (lOSection.Id == psId)
					return lOSection;
			}

			return null;
		}

		/// <summary>The whole report as text, a line per row (the titles, labels and values), for the
		/// log and the self-check.</summary>
		public string ToText()
		{
			StringBuilder lOText = new StringBuilder();

			foreach (UWDebugSection lOSection in Sections)
			{
				lOText.Append("[").Append(lOSection.Title).Append("]\n");

				foreach (UWDebugRow lORow in lOSection.Rows)
					lOText.Append(lORow.Label).Append(": ").Append(lORow.Value).Append('\n');
			}

			lOText.Append(Footer).Append('\n');

			return lOText.ToString();
		}

		// ================================================= Building

		private static string fF(string psFormat, params object[] pOArguments)
		{
			return string.Format(CultureInfo.InvariantCulture, psFormat, pOArguments);
		}

		private static string fBit(bool pbValue)
		{
			return pbValue ? "1" : "0";
		}

		/// <summary>The report of a snapshot: in the CritterTrace mode the creature alone (its own view),
		/// otherwise the six sections.</summary>
		public static UWDebugReport Build(UWDebugSnapshot pOSnapshot)
		{
			UWDebugReport lOReport = new UWDebugReport();

			lOReport.Footer = fF("{0} hides", pOSnapshot.HideKey);

			// CREATURES ONLY: while a creature's behaviour is being hunted down, everything else is in
			// the way - position, backpack, game variables, trap log (per user, 2026-09-16: "and
			// clearer, maybe just the creature data on F1 for a while"). The switch sits next to the
			// trace it belongs to, UWSettings.CritterTrace.
			if (pOSnapshot.CritterTrace)
			{
				lOReport.IsCritterTrace = true;

				UWDebugSection lOSection = new UWDebugSection(CritterTraceId, "Creature (trace mode)");

				lOSection.Add("Level", fF("{0}", pOSnapshot.Level));
				fAddCreature(lOSection, pOSnapshot);
				lOReport.Sections.Add(lOSection);

				return lOReport;
			}

			lOReport.Sections.Add(fPlace(pOSnapshot));
			lOReport.Sections.Add(fPlayer(pOSnapshot));
			lOReport.Sections.Add(fLookTarget(pOSnapshot.Look));

			UWDebugSection lOCreature = new UWDebugSection(CreatureId, "Creature");

			fAddCreature(lOCreature, pOSnapshot);
			lOReport.Sections.Add(lOCreature);
			lOReport.Sections.Add(fVariables(pOSnapshot));
			lOReport.Sections.Add(fTraps(pOSnapshot));

			return lOReport;
		}

		// ------------------------------------------------- Place

		/// <summary>The compass names in UWHeadings' eighths: north is +Z.</summary>
		private static readonly string[] msCompass = { "North", "Northeast", "East", "Southeast", "South", "Southwest", "West", "Northwest" };

		/// <summary>Compass direction in the level's coordinate system: north is +Z.</summary>
		public static string Compass(float pfYaw)
		{
			return msCompass[UWHeadings.DegreesToEighth(pfYaw)];
		}

		private static UWDebugSection fPlace(UWDebugSnapshot pOSnapshot)
		{
			UWDebugSection lOSection = new UWDebugSection(PlaceId, "Place");

			lOSection.Add("Level", fF("{0}", pOSnapshot.Level));

			// The player's rules.json, only when there is one (cleanup phase 10).
			if (!string.IsNullOrEmpty(pOSnapshot.Rules))
				lOSection.Add("Rules", pOSnapshot.Rules, pOSnapshot.RulesProblem ? UWDebugEmphasis.Warning : UWDebugEmphasis.Accent);

			if (!pOSnapshot.HasPlace)
				return lOSection;

			lOSection.Add("Tile", fF("{0}, {1}", pOSnapshot.TileX, pOSnapshot.TileZ));
			lOSection.Live("Position", fF("{0:F0} / {1:F0} / {2:F0}", pOSnapshot.X, pOSnapshot.Y, pOSnapshot.Z));

			if (pOSnapshot.HasTile)
			{
				lOSection.Add("Tile type", pOSnapshot.TileType);
				lOSection.Add("Floor", fF("{0}", pOSnapshot.FloorHeight));
			}

			lOSection.Live("Heading", fF("{0:F0} degrees", pOSnapshot.Yaw));
			lOSection.Add("Compass", Compass(pOSnapshot.Yaw));

			// Which traps and triggers lie on the player's own tile: major class 6 has no
			// representation in the world, so you cannot tell that you are standing on them.
			for (int liAt = 0; liAt < pOSnapshot.TileTraps.Count; liAt++)
			{
				UWDebugTileTrap lOTrap = pOSnapshot.TileTraps[liAt];
				string lsValue = fF("{0} (q {1}, own {2})", lOTrap.Name, lOTrap.Quality, lOTrap.Owner);

				if (lOTrap.LinkedName != null)
					lsValue += " -> " + lOTrap.LinkedName;

				lOSection.Add("On this tile", lsValue, UWDebugEmphasis.Accent, UWDebugRowFlags.None, "tiletrap" + liAt);
			}

			// The same spot once more in the ORIGINAL'S UNITS, exactly as the save game stores it: the
			// fields at 0x54 and 0x56 hold the fine position X and Y, whose upper byte is the tile, the
			// one at 0x58 zpos times eight, the one at 0x5A the heading as a full circle in 16 bits.
			// That is why the field number is shown right next to it.
			lOSection.Sub("Original (save game fields)");
			lOSection.Live("x", fF("{0} (0x54={0:X4})", pOSnapshot.OriginalX), "orig-x");
			lOSection.Live("y", fF("{0} (0x56={0:X4})", pOSnapshot.OriginalY), "orig-y");
			lOSection.Live("zpos", fF("{0} (0x58={1:X4})", pOSnapshot.OriginalZ >> 3, pOSnapshot.OriginalZ), "orig-z");
			lOSection.Live("heading", fF("{0} (0x5A={0:X4})", pOSnapshot.OriginalHeading), "orig-heading");

			// THE MOTION CORE'S OWN VIEW (stage 3, 2026-10-06): the params block, the contact state and
			// what the last step found under and around the player - the floor samples and the collision
			// records (item ids with bottom and top). For a player standing where he should not (per
			// user: on the edge of a water slope a level too high).
			UWDebugMotion lOMotion = pOSnapshot.Motion;

			if (lOMotion == null)
				return lOSection;

			lOSection.Sub("Core");
			lOSection.Live("Position", fF("x {0} y {1} z {2} (fine)", lOMotion.X, lOMotion.Y, lOMotion.Z), "core-pos");
			lOSection.Add("Speed", fF("{0}  vz {1}  gravity {2}", lOMotion.Speed, lOMotion.Vz, lOMotion.Gravity), UWDebugEmphasis.Normal,
				UWDebugRowFlags.Live, "core-speed");
			lOSection.Add("Contact", fF("0x{0:X}", lOMotion.Contact), UWDebugEmphasis.Normal, UWDebugRowFlags.None, "core-contact");
			lOSection.Add("State", lOMotion.State + (lOMotion.Precise ? (lOMotion.FineTicks ? ", precise, fine ticks" : ", precise") : string.Empty),
				UWDebugEmphasis.Normal, UWDebugRowFlags.None, "core-state");
			lOSection.Add("Flags", fF("0x{0:X}  all 0x{1:X}", lOMotion.Flags, lOMotion.AllFlags), UWDebugEmphasis.Normal,
				UWDebugRowFlags.Live, "core-flags");
			lOSection.Add("Floors", fF("centre floor {0}  max floor {1}", lOMotion.CentreFloor, lOMotion.MaxFloor), UWDebugEmphasis.Normal,
				UWDebugRowFlags.Live, "core-floors");
			lOSection.Add("Records", fF("{0} (overlap {1} from {2})", lOMotion.Count, lOMotion.Overlap, lOMotion.First), UWDebugEmphasis.Normal,
				UWDebugRowFlags.Live, "core-records");

			for (int liAt = 0; liAt < lOMotion.Records.Count; liAt++)
			{
				UWDebugMotion.Record lORecord = lOMotion.Records[liAt];

				lOSection.Live(fF("record {0}", liAt), fF("item 0x{0:X3} index {1} at {2}/{3} z {4}..{5} flags 0x{6:X}",
					lORecord.ItemId, lORecord.Index, lORecord.Dx, lORecord.Dy, lORecord.Bottom, lORecord.Top, lORecord.Flags));
			}

			return lOSection;
		}

		// ------------------------------------------------- Player

		/// <summary>The parts of UWArmourProtection in the order the snapshot keeps them.</summary>
		private static readonly string[] msArmourParts = { "Body", "Hands", "Legs", "Head" };

		private static UWDebugSection fPlayer(UWDebugSnapshot pOSnapshot)
		{
			UWDebugSection lOSection = new UWDebugSection(PlayerId, "Player");

			// Backpack fill level, originally added for testing the inventory setup.
			if (pOSnapshot.HasBackpack)
				lOSection.Add("Backpack", fF("{0}/{1}", pOSnapshot.BackpackCount, pOSnapshot.BackpackSize));

			UWDebugPlayer lOPlayer = pOSnapshot.Player;

			if (lOPlayer == null)
				return lOSection;

			// Hit points, mana and poison - so a hit can be attributed. Without this you see the
			// flask twitch while testing, but no number.
			lOSection.Add("Health", fF("{0}/{1}", lOPlayer.HitPoints, lOPlayer.MaxHitPoints));
			lOSection.Add("Mana", fF("{0}/{1}", lOPlayer.Mana, lOPlayer.MaxMana));

			if (lOPlayer.Poison > 0)
				lOSection.Add("Poisoned", fF("{0}", lOPlayer.Poison), UWDebugEmphasis.Warning);

			// WHERE THE LAST HIT CAME FROM. The damage type reveals the source: physical is melee, fire
			// a fireball or lava, missile an arrow, untyped something like starvation or a trap. The
			// combat shake of that hit, if it shook at all (level 0 sets a zero duration and shows
			// nothing, as set_effect(0x20, 0) in the original; 2026-10-19).
			if (lOPlayer.HasLastHit)
			{
				string lsType = lOPlayer.LastDamageType == UWDamageTypes.None ? "untyped" : DescribeDamageTypes(lOPlayer.LastDamageType);

				lOSection.Add("Last hit", fF("{0} ({1}), {2}", lOPlayer.LastDamage, lsType,
					lOPlayer.Shook ? fF("shake level {0}", lOPlayer.ShakeLevel) : "no shake"));
				lOSection.Live("Last hit age", fF("{0:F1}s ago", lOPlayer.LastHitAge));
			}

			// Armour and enchantments of the equipment worn - the original shows NO NUMBER for any of
			// this (per user, 2026-09-09). THE TWO COLUMNS ARE DIFFERENT THINGS (see
			// UWArmourProtection): armour subtracts DAMAGE, protection lowers the CHANCE TO HIT - and it
			// only counts on head and body.
			lOSection.Sub("Armour / protection");

			for (int liPart = 0; liPart < msArmourParts.Length; liPart++)
				lOSection.Add(msArmourParts[liPart], fF("{0} / {1}", lOPlayer.Armour[liPart], lOPlayer.Protection[liPart]));

			// Both values, because they differ: the skill is what the status page shows, the check
			// value is what a creature rolls against (skill + half the weapon skill, see
			// UWPlayerCritterRow).
			lOSection.Add("Defence", fF("{0} (skill {1} + half of {2})", lOPlayer.DefenceCheck, lOPlayer.Defence, lOPlayer.DefenceSkill));

			// The resistance of spell class 2 is already included in the armour values above. It is
			// still shown separately, because otherwise you cannot see whether a high number comes
			// from the armour or from the spell.
			if (lOPlayer.Resistance > 0)
				lOSection.Add("Resistance", fF("+{0}", lOPlayer.Resistance));

			// What the character is currently immune to (the damage is deflected entirely, see
			// UWDamagePipeline).
			if (lOPlayer.Proof != 0)
				lOSection.Add("Immune to", DescribeDamageTypes(lOPlayer.Proof), UWDebugEmphasis.Accent);

			if (lOPlayer.Stealth != 0)
				lOSection.Add("Stealth", fF("0x{0:X}", lOPlayer.Stealth));

			if (lOPlayer.DragonSkinBoots)
				lOSection.Add("Dragon skin boots", "lava-proof", UWDebugEmphasis.Accent);

			foreach (UWDebugEnchantment lOEnchantment in lOPlayer.Enchantments)
			{
				string lsValue = fF("object {0}, spell {1}-{2:X}", lOEnchantment.ObjectId, lOEnchantment.Major, lOEnchantment.Minor);

				if (lOEnchantment.Protection > 0)
					lsValue += fF(", Protection +{0}", lOEnchantment.Protection);

				lOSection.Add(lOEnchantment.Slot, lsValue, UWDebugEmphasis.Normal, UWDebugRowFlags.None, "enchant:" + lOEnchantment.Slot);
			}

			return lOSection;
		}

		/// <summary>
		/// A resistance byte in words - the magic level as a number, the rest as names. The bit layout
		/// is in UWDamageTypes; the names are those of the spells that set them (Flameproof, Poison
		/// Resistance, Missile Protection).
		/// </summary>
		public static string DescribeDamageTypes(int piBits)
		{
			StringBuilder lOText = new StringBuilder();

			int liMagicLevel = piBits & 0x3;

			if (liMagicLevel > 0)
				lOText.AppendFormat(CultureInfo.InvariantCulture, "Magic {0}/3", liMagicLevel);

			fAppendDamageTypeName(lOText, piBits, UWDamageTypes.Physical, "Physical");
			fAppendDamageTypeName(lOText, piBits, UWDamageTypes.PlainFire, "Fire");
			fAppendDamageTypeName(lOText, piBits, UWDamageTypes.Poison, "Poison");
			fAppendDamageTypeName(lOText, piBits, 0x20, "Cold");
			fAppendDamageTypeName(lOText, piBits, UWDamageTypes.Missile, "Missile");

			return lOText.ToString();
		}

		private static void fAppendDamageTypeName(StringBuilder pOText, int piBits, int piMask, string psName)
		{
			if ((piBits & piMask) == 0)
				return;

			if (pOText.Length > 0)
				pOText.Append(", ");

			pOText.Append(psName);
		}

		// ------------------------------------------------- Look target

		/// <summary>What lies under the crosshair: for geometry the tile with its texture number, for
		/// objects the id and texture source - exactly the details needed to find a spot again.</summary>
		private static UWDebugSection fLookTarget(UWDebugLook pOLook)
		{
			UWDebugSection lOSection = new UWDebugSection(LookId, "Look target");

			switch (pOLook.Kind)
			{
				case UWDebugLookKind.NoCamera:
					lOSection.Add("Target", "no camera", UWDebugEmphasis.Dim);
					break;

				case UWDebugLookKind.Nothing:
					lOSection.Add("Target", "nothing", UWDebugEmphasis.Dim);
					break;

				case UWDebugLookKind.Object:
					lOSection.Add("Object", fF("0x{0:X4} ({1})", pOLook.ObjectId, pOLook.Category));
					lOSection.Add("Texture", pOLook.Texture);
					lOSection.Add("Tile", fF("{0}, {1}", pOLook.TileX, pOLook.TileY));
					lOSection.Add("Height", fF("{0}", pOLook.Height));
					lOSection.Add("Heading", fF("{0}", pOLook.Heading));
					lOSection.Add("Description", pOLook.Description);
					break;

				case UWDebugLookKind.Surface:
					lOSection.Add("Surface", pOLook.Surface);
					lOSection.Add("Tile", fF("{0}, {1}", pOLook.TileX, pOLook.TileY));
					lOSection.Add("Texture", fF("{0}", pOLook.TextureIndex));
					lOSection.Add("Tile type", pOLook.TileType);
					break;

				default:
					lOSection.Add("Collider", pOLook.ColliderName);

					if (pOLook.HasEntity)
						lOSection.Add("Description", pOLook.Description);

					break;
			}

			return lOSection;
		}

		// ------------------------------------------------- Creature

		/// <summary>
		/// What the creature shown is currently up to. WHY IT IS HERE: from the outside you can read
		/// almost nothing off a creature - whether a fire elemental throws no fireball because it never
		/// enters the spell branch, because its roll fails, or because the projectile does not appear
		/// (2026-09-10, exactly this case). Shown are the numbers from the creature table and the
		/// record that determine its behaviour, and what it is doing with them; the looked-at creature
		/// first (per user, 2026-09-10: the nearest was a spider beside the fire elemental).
		/// </summary>
		private static void fAddCreature(UWDebugSection pOSection, UWDebugSnapshot pOSnapshot)
		{
			if (pOSnapshot.CreatureCount == 0)
			{
				pOSection.Add("Creature", "none on this level", UWDebugEmphasis.Dim);

				return;
			}

			UWDebugCreature lOCreature = pOSnapshot.Creature;

			if (lOCreature == null)
				return;

			pOSection.Add("Name", lOCreature.Name);
			pOSection.Add("Chosen", fF("{0} of {1} on the level", lOCreature.LookedAt ? "looked at" : "nearest", pOSnapshot.CreatureCount));
			pOSection.Live("Distance", fF("{0:F0} away ({1:F1} tiles)", lOCreature.Distance, lOCreature.Distance / UWWorldScale.TileSize));
			pOSection.Add("State", lOCreature.State);
			pOSection.Add("Attack", lOCreature.AttackKind);

			if (lOCreature.HasRecord)
				fAddCreatureRecord(pOSection, lOCreature);
			else
				pOSection.Add("Record", "(no record)", UWDebugEmphasis.Dim);

			if (lOCreature.Trace.Count == 0)
				return;

			// The trace, oldest line first, keyed by its text: a new line lights up.
			pOSection.Sub("Trace");

			for (int liAt = 0; liAt < lOCreature.Trace.Count; liAt++)
			{
				string lsLine = lOCreature.Trace[liAt];

				pOSection.Add(string.Empty, lsLine, UWDebugEmphasis.Normal, UWDebugRowFlags.Log, fUniqueKey(pOSection, "trace:" + lsLine));
			}
		}

		/// <summary>The record's numbers that decide the creature's behaviour, as a grid, then its alarm
		/// and its last step.</summary>
		private static void fAddCreatureRecord(UWDebugSection pOSection, UWDebugCreature pOCreature)
		{
			pOSection.Add("Last event", pOCreature.LastEvent, UWDebugEmphasis.Accent);

			// The record: goal, target, slot and interval, animation and frame, the temper bits. What
			// ticks by itself while the creature only walks (the slot, the phase, the frames, the fine
			// position and heading, the body's height) is live and does not light up, so a change of
			// goal, target, animation or the temper bits stands out.
			pOSection.Cell("Goal", fF("{0}", pOCreature.Goal));
			pOSection.Cell("Gtarg", fF("{0}", pOCreature.GTarg));
			pOSection.Cell("Backup", fF("{0}", pOCreature.BackupGoal));
			pOSection.Cell("Attitude", fF("{0}", pOCreature.Attitude));
			pOSection.Cell("Hp", fF("{0}", pOCreature.HitPoints));
			pOSection.Cell("Index", fF("{0}", pOCreature.Index));

			// The kin alarm as this creature sees it - added 2026-09-27 for the summoned ally that
			// stayed mellow next to a creature the player hit.
			pOSection.Cell("Ally", fBit(pOCreature.IsAlly));
			pOSection.Cell("Passive", fBit(pOCreature.IsPassive));
			pOSection.Cell("Hearing", fF("{0}", pOCreature.NoiseRange));
			pOSection.Cell("Kind", fF("{0}", pOCreature.Kind));

			pOSection.LiveCell("Slot", fF("{0}", pOCreature.DueSlot));
			pOSection.LiveCell("Phase", fF("{0}", pOCreature.Phase));
			pOSection.Cell("Interval", fF("{0}", pOCreature.Interval));
			pOSection.Cell("Anim", fF("0x{0:X2}", pOCreature.Animation));
			pOSection.LiveCell("Frame", fF("{0}", pOCreature.Frame));
			pOSection.Cell("Speed", fF("{0}", pOCreature.Speed));
			pOSection.LiveCell("Heading", fF("{0}", pOCreature.FineHeading));
			pOSection.Cell("Facing", fF("{0}+{1}", pOCreature.FacingEighth, pOCreature.FacingResidual));

			pOSection.Cell("Confirmed", fBit(pOCreature.Confirmed));
			pOSection.Cell("Heard", fBit(pOCreature.Heard));
			pOSection.Cell("Blocked", fBit(pOCreature.Blocked));
			pOSection.Cell("Path", fBit(pOCreature.HasPath));
			pOSection.Cell("Straight", fBit(pOCreature.Straight));
			pOSection.Cell("Stand", fBit(pOCreature.Stand));
			pOSection.Cell("Relentless", fBit(pOCreature.Relentless));
			pOSection.Cell("Charge", fF("{0}", pOCreature.Charge));
			pOSection.Cell("Pitch", fF("{0}", pOCreature.Pitch));

			pOSection.Cell("Destination", fF("{0}/{1} h{2}", pOCreature.DestinationX, pOCreature.DestinationY, pOCreature.DestinationHeight));
			pOSection.Cell("Tile", fF("{0}/{1}", pOCreature.TileX, pOCreature.TileY));
			pOSection.LiveCell("Fine", fF("{0}/{1}", pOCreature.FineX, pOCreature.FineY));
			pOSection.Cell("Z", fF("{0}", pOCreature.ZPos));
			pOSection.Cell("Shots", fF("{0}", pOCreature.Shots));

			if (pOCreature.HasStats)
			{
				pOSection.Cell("Vitality", fF("{0}", pOCreature.Vitality));
				pOSection.Cell("Morale", fF("{0}", pOCreature.Morale));
				pOSection.Cell("Initiative", fF("{0}", pOCreature.Initiative));
				pOSection.Cell("Hear", fF("{0}", pOCreature.NoiseRange));
				pOSection.Cell("See", fF("{0}", pOCreature.SightRange));
				pOSection.Cell("Door skill", fF("{0}", pOCreature.DoorSkill));
				pOSection.Cell("Mover", pOCreature.Mover + (pOCreature.IsCaster ? " caster" : string.Empty));
			}

			pOSection.LiveCell("y", fF("{0:F0}", pOCreature.BodyY), "body-y");
			pOSection.Cell("Tile floor", fF("{0}", pOCreature.TileFloor));
			pOSection.Cell("Picture slot", fF("0x{0:X2}", pOCreature.PictureSlot));
			pOSection.LiveCell("Picture frame", fF("{0}", pOCreature.PictureFrame));

			pOSection.Add("Alarm", pOCreature.HasAlarm
				? fF("victim {0} kind {1} at {2}/{3}, age {4} of {5}, fresh {6}, manhattan {7}", pOCreature.AlarmVictim, pOCreature.AlarmKind,
					pOCreature.AlarmTileX, pOCreature.AlarmTileY, pOCreature.AlarmAge, UWCritterRules.KinAlarmPitTicks, fBit(pOCreature.AlarmFresh),
					pOCreature.AlarmManhattan)
				: "none");

			if (pOCreature.Collided)
				pOSection.Add("Last step", fF("collided: object {0} item 0x{1:X3}{2}", pOCreature.HitObjectIndex, pOCreature.HitObjectItemId,
					pOCreature.HitClosedDoor ? " (closed door)" : string.Empty));
		}

		/// <summary>A key not yet taken in the section (two equal trace lines get two).</summary>
		private static string fUniqueKey(UWDebugSection pOSection, string psKey)
		{
			string lsKey = psKey;

			for (int liAt = 2; pOSection.Find(lsKey) != null; liAt++)
				lsKey = psKey + "#" + liAt.ToString(CultureInfo.InvariantCulture);

			return lsKey;
		}

		// ------------------------------------------------- Game variables, trap log

		/// <summary>
		/// The game variables that are not zero - the state of the puzzles, as a grid. Without them the
		/// variable traps reveal nothing: they have no representation in the world, and yet whether a
		/// counter is already at seven decides whether the next check passes.
		/// </summary>
		private static UWDebugSection fVariables(UWDebugSnapshot pOSnapshot)
		{
			UWDebugSection lOSection = new UWDebugSection(VariablesId, "Game variables");

			foreach (KeyValuePair<int, int> lOPair in pOSnapshot.Variables)
			{
				if (lOPair.Value == 0)
					continue;

				lOSection.Cell(fF("{0}", lOPair.Key), fF("{0}", lOPair.Value));
			}

			if (lOSection.Rows.Count == 0)
				lOSection.Add("Variables", "none set", UWDebugEmphasis.Dim);

			return lOSection;
		}

		/// <summary>The most recently triggered traps, latest first, with their age in seconds; a line
		/// is keyed by its time stamp, so a trap that fires lights up.</summary>
		private static UWDebugSection fTraps(UWDebugSnapshot pOSnapshot)
		{
			UWDebugSection lOSection = new UWDebugSection(TrapsId, "Trap log");

			for (int liAt = pOSnapshot.Traps.Count - 1; liAt >= 0; liAt--)
			{
				UWTrapLog.Entry lOEntry = pOSnapshot.Traps[liAt];
				bool lbNotBuilt = lOEntry.Text != null && lOEntry.Text.EndsWith("NOT BUILT", StringComparison.Ordinal);

				lOSection.Add(fF("{0:F0}s ago", pOSnapshot.Now - lOEntry.Time), lOEntry.Text,
					lbNotBuilt ? UWDebugEmphasis.Warning : UWDebugEmphasis.Normal, UWDebugRowFlags.Log,
					fUniqueKey(lOSection, "trap:" + lOEntry.Time.ToString("R", CultureInfo.InvariantCulture)));
			}

			if (lOSection.Rows.Count == 0)
				lOSection.Add("Traps", "none fired yet", UWDebugEmphasis.Dim);

			return lOSection;
		}
	}
}
