using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT THE DEBUG PANEL SHOWS, AS PLAIN VALUES (cleanup 8c, 2026-10-25): the F1 overlay's numbers
	/// (UWDebugOverlay), filled once a frame by the Unity side from the camera, the character, the
	/// creature under the crosshair and the level, and turned into sections of rows by the engine-free
	/// report (UWDebugReport.Build). Until then the overlay formatted its one text block straight from
	/// the scene, so nothing of it could be checked without Unity, and a second game (UW2) would have
	/// had to edit the drawing code to add a line.
	///
	/// Every field is what the old overlay read at the same place (fBuildText, fAppendCharacter,
	/// fAppendArmour, fAppendTileTraps, fAppendOriginalUnits, fDescribeLookTarget,
	/// fAppendNearestCritter, UWCritter.DescribeCombatStats and GetTrace); the enum values the overlay
	/// printed by name arrive as their names. A part that is null was absent there (no camera, no
	/// character, no motion core, no creature shown).
	/// </summary>
	public sealed class UWDebugSnapshot
	{
		/// <summary>The level as the player counts it (CurrentLevelIndex + 1).</summary>
		public int Level;

		/// <summary>UWSettings.CritterTrace: the creature alone, everything else in the way (per user,
		/// 2026-09-16).</summary>
		public bool CritterTrace;

		/// <summary>The overlay's key as the binding shows it ("Shift+F1").</summary>
		public string HideKey = string.Empty;

		/// <summary>The game clock (Time.time) the ages of the trap log and the last hit count from.</summary>
		public float Now;

		/// <summary>What the player's rules.json did to this game (UWRulesReport.Summary; cleanup phase
		/// 10, 2026-10-25); empty without the file, and then the row is left out.</summary>
		public string Rules = string.Empty;

		/// <summary>The file had problems (a malformed file, an unknown key, a wrong value).</summary>
		public bool RulesProblem;

		// ------------------------------------------------- Place

		/// <summary>A camera to stand at: without one the place and the player's lines were left out.</summary>
		public bool HasPlace;

		/// <summary>The eye's world position.</summary>
		public float X;

		public float Y;

		public float Z;

		public int TileX;

		public int TileZ;

		/// <summary>The tile under the eye exists (its type and floor height then).</summary>
		public bool HasTile;

		public string TileType = string.Empty;

		public int FloorHeight;

		/// <summary>The camera's yaw in degrees (0 = north, +Z).</summary>
		public float Yaw;

		/// <summary>The four position fields of the save game (UWViewpoint): fine x and y, z in eighths
		/// of a zpos step, the heading as a full circle in 16 bits.</summary>
		public int OriginalX;

		public int OriginalY;

		public int OriginalZ;

		public int OriginalHeading;

		/// <summary>The motion core's own view, null when the player does not move on it.</summary>
		public UWDebugMotion Motion;

		/// <summary>The traps and triggers on the player's own tile.</summary>
		public readonly List<UWDebugTileTrap> TileTraps = new List<UWDebugTileTrap>();

		// ------------------------------------------------- Player

		/// <summary>The backpack's fill (shown when there is an inventory).</summary>
		public bool HasBackpack;

		public int BackpackCount;

		public int BackpackSize;

		/// <summary>The character's vitals and armour, null without a character.</summary>
		public UWDebugPlayer Player;

		// ------------------------------------------------- Look target, creature

		public UWDebugLook Look = new UWDebugLook();

		/// <summary>The creatures the world's search found on the level; 0 is "none on this level".</summary>
		public int CreatureCount;

		/// <summary>The looked-at creature, else the nearest; null when none could be chosen.</summary>
		public UWDebugCreature Creature;

		// ------------------------------------------------- Game variables, trap log

		/// <summary>The game variables in UWGameVariables.All's order, zero ones included (the report
		/// shows only the set ones).</summary>
		public readonly List<KeyValuePair<int, int>> Variables = new List<KeyValuePair<int, int>>();

		/// <summary>The trap log, oldest first, as UWTrapLog keeps it.</summary>
		public readonly List<UWTrapLog.Entry> Traps = new List<UWTrapLog.Entry>();
	}

	/// <summary>A trap or trigger on the player's tile, and the trap or trigger it links to.</summary>
	public struct UWDebugTileTrap
	{
		public string Name;

		public int Quality;

		public int Owner;

		/// <summary>The linked trap or trigger's name, null when it links to none.</summary>
		public string LinkedName;
	}

	/// <summary>Hit points, mana, poison, the last hit, armour and enchantments of the character.</summary>
	public sealed class UWDebugPlayer
	{
		public int HitPoints;

		public int MaxHitPoints;

		public int Mana;

		public int MaxMana;

		public int Poison;

		/// <summary>A hit was taken (LastDamageTime at or above 0).</summary>
		public bool HasLastHit;

		public int LastDamage;

		/// <summary>UWDamageTypes bits of the last hit, None for an untyped one.</summary>
		public int LastDamageType;

		/// <summary>Seconds since the last hit.</summary>
		public float LastHitAge;

		/// <summary>The last hit shook the view (its shake within a tenth of a second of the hit).</summary>
		public bool Shook;

		public int ShakeLevel;

		/// <summary>Armour and protection by UWArmourProtection's parts: body, hands, legs, head.</summary>
		public readonly int[] Armour = new int[4];

		public readonly int[] Protection = new int[4];

		public int DefenceCheck;

		public int Defence;

		/// <summary>The weapon skill whose half the check adds, by name.</summary>
		public string DefenceSkill = string.Empty;

		public int Resistance;

		/// <summary>UWDamageTypes bits the character is immune to.</summary>
		public int Proof;

		public int Stealth;

		public bool DragonSkinBoots;

		/// <summary>The worn enchantments that take effect, in UWArmorItemMap slot order.</summary>
		public readonly List<UWDebugEnchantment> Enchantments = new List<UWDebugEnchantment>();
	}

	/// <summary>An enchantment worn in an armour slot or on a ring.</summary>
	public struct UWDebugEnchantment
	{
		/// <summary>The slot's name (UWArmorItemMap.BodySlot).</summary>
		public string Slot;

		public int ObjectId;

		public int Major;

		public int Minor;

		/// <summary>The protection enchantment's strength, 0 for none.</summary>
		public int Protection;
	}

	/// <summary>What the crosshair's ray hit.</summary>
	public enum UWDebugLookKind
	{
		NoCamera = 0,

		Nothing = 1,

		/// <summary>An object of the level (UWEntityInfo with object data).</summary>
		Object = 2,

		/// <summary>A floor, ceiling or wall of a tile (UWLevelChunk).</summary>
		Surface = 3,

		/// <summary>Anything else with a collider; HasEntity when it carries a description.</summary>
		Collider = 4
	}

	public sealed class UWDebugLook
	{
		public UWDebugLookKind Kind;

		public int ObjectId;

		/// <summary>The object's category (UWObject.GetCategory) by name.</summary>
		public string Category = string.Empty;

		/// <summary>The object's texture as "type index", "?" without one.</summary>
		public string Texture = string.Empty;

		public int TileX;

		public int TileY;

		/// <summary>The object's ZPos.</summary>
		public int Height;

		public int Heading;

		/// <summary>The entity's description, "-" when it has none.</summary>
		public string Description = string.Empty;

		/// <summary>"floor", "ceiling" or "wall".</summary>
		public string Surface = string.Empty;

		/// <summary>The surface's texture number, -1 without a tile.</summary>
		public int TextureIndex;

		/// <summary>The surface tile's type by name, "?" without a tile.</summary>
		public string TileType = string.Empty;

		public string ColliderName = string.Empty;

		/// <summary>The collider belongs to an entity (its description shown).</summary>
		public bool HasEntity;
	}

	/// <summary>The motion core's params, contact and last step (stage 3, 2026-10-06).</summary>
	public sealed class UWDebugMotion
	{
		public int X;

		public int Y;

		public int Z;

		public int Speed;

		public int Vz;

		public int Gravity;

		public int Contact;

		public string State = string.Empty;

		public bool Precise;

		public bool FineTicks;

		public int Flags;

		public int AllFlags;

		public int CentreFloor;

		public int MaxFloor;

		/// <summary>The low bytes of the core's record count and overlap, the first as a signed byte.</summary>
		public int Count;

		public int Overlap;

		public int First;

		public readonly List<Record> Records = new List<Record>();

		public struct Record
		{
			public int ItemId;

			public int Index;

			public int Dx;

			public int Dy;

			public int Bottom;

			public int Top;

			public int Flags;
		}

		/// <summary>The view of a player's motion, as fAppendOriginalUnits read it.</summary>
		public static UWDebugMotion From(UWPlayerMotion pOMotion)
		{
			if (pOMotion == null)
				return null;

			UWMotionCore lOCore = pOMotion.Core;
			UWDebugMotion lOView = new UWDebugMotion
			{
				X = pOMotion.Params.X,
				Y = pOMotion.Params.Y,
				Z = pOMotion.Params.Z,
				Speed = pOMotion.Params.Speed,
				Vz = pOMotion.Params.Vz,
				Gravity = pOMotion.Params.Gravity,
				Contact = pOMotion.Params.Contact,
				State = pOMotion.CurrentState.ToString(),
				Precise = pOMotion.Precise,
				FineTicks = pOMotion.FineTicks,
				Flags = lOCore.Flags,
				AllFlags = lOCore.AllFlags,
				CentreFloor = lOCore.CentreFloor,
				MaxFloor = lOCore.MaxFloor,
				Count = lOCore.Count & 0xFF,
				Overlap = lOCore.Overlap & 0xFF,
				First = (sbyte)lOCore.First
			};

			for (int liAt = 0; liAt < lOView.Count && liAt < lOCore.Records.Length; liAt++)
			{
				UWMotionCore.Record lORecord = lOCore.Records[liAt];

				lOView.Records.Add(new Record
				{
					ItemId = lORecord.Body.ItemId & 0x1FF,
					Index = lORecord.Body.Index,
					Dx = lORecord.Dx,
					Dy = lORecord.Dy,
					Bottom = lORecord.Bottom,
					Top = lORecord.Top,
					Flags = lORecord.Flags
				});
			}

			return lOView;
		}
	}

	/// <summary>
	/// The creature the overlay shows: which one and why, the record's numbers that decide its
	/// behaviour (UWCritter.DescribeCombatStats until cleanup 8c), its picture and its trace.
	/// </summary>
	public sealed class UWDebugCreature
	{
		/// <summary>The creature's object name in the scene.</summary>
		public string Name = string.Empty;

		/// <summary>Under the crosshair; else the nearest.</summary>
		public bool LookedAt;

		/// <summary>Horizontal distance from the eye, world units.</summary>
		public float Distance;

		/// <summary>UWCritter.State and AttackKind by name.</summary>
		public string State = string.Empty;

		public string AttackKind = string.Empty;

		/// <summary>The creature has a record (else "(no record)" and nothing of it).</summary>
		public bool HasRecord;

		public int Goal;

		public int GTarg;

		public int BackupGoal;

		public int Attitude;

		public int HitPoints;

		public int Index;

		public bool IsAlly;

		public bool IsPassive;

		/// <summary>The stats row's noise range (shown as hearing, and as hear among the stats).</summary>
		public int NoiseRange;

		public int Kind;

		/// <summary>The kin alarm is raised (UWCritterAlarm.Index not 0).</summary>
		public bool HasAlarm;

		public int AlarmVictim;

		public int AlarmKind;

		public int AlarmTileX;

		public int AlarmTileY;

		/// <summary>The alarm's age in creature clock ticks.</summary>
		public long AlarmAge;

		public bool AlarmFresh;

		/// <summary>The creature's Manhattan distance to the alarm's tile.</summary>
		public int AlarmManhattan;

		public int DueSlot;

		public int Phase;

		public int Interval;

		public int Animation;

		public int Frame;

		public int Speed;

		public int FineHeading;

		public int FacingEighth;

		public int FacingResidual;

		public bool Confirmed;

		public bool Heard;

		public bool Blocked;

		public bool HasPath;

		public bool Straight;

		public bool Stand;

		public bool Relentless;

		public int Charge;

		public int Pitch;

		public int DestinationX;

		public int DestinationY;

		public int DestinationHeight;

		public int TileX;

		public int TileY;

		public int FineX;

		public int FineY;

		public int ZPos;

		public int Shots;

		/// <summary>The creature table's row is there (vitality to door skill, the mover, the caster).</summary>
		public bool HasStats;

		public int Vitality;

		public int Morale;

		public int Initiative;

		public int SightRange;

		public int DoorSkill;

		/// <summary>"flier", "swimmer" or "walker".</summary>
		public string Mover = string.Empty;

		public bool IsCaster;

		public bool Collided;

		public int HitObjectIndex;

		public int HitObjectItemId;

		public bool HitClosedDoor;

		/// <summary>The body's world height, the tile floor under it (-1 without a tile), the picture's
		/// slot and frame (-1 without an animator), the brain's last event.</summary>
		public float BodyY;

		public int TileFloor;

		public int PictureSlot;

		public int PictureFrame;

		public string LastEvent = string.Empty;

		/// <summary>The trace, oldest line first, each "12.3s what" (empty while tracing is off).</summary>
		public readonly List<string> Trace = new List<string>();

		/// <summary>
		/// The record's and the actor's part, as DescribeCombatStats read it: the record, the stats row,
		/// the kin alarm at the creature clock's tick and phase. The scene's part (index, picture, last
		/// event, trace, the body's height) the Unity side fills.
		/// </summary>
		public void FillRecord(UWCritterRecord pORecord, UWCreatureActor pOActor, UWCritterAlarm pOAlarm, long piClock, int piPhase)
		{
			HasRecord = pORecord != null && pOActor != null;

			if (!HasRecord)
				return;

			UWObjectClassProperties.Critter lOStats = pOActor.Stats;

			Goal = pORecord.Goal;
			GTarg = pORecord.GTarg;
			BackupGoal = pORecord.BackupGoal;
			Attitude = pORecord.Attitude;
			HitPoints = pORecord.HitPoints;
			IsAlly = pORecord.IsAlly;
			IsPassive = lOStats.IsPassive;
			NoiseRange = lOStats.NoiseRange;
			Kind = lOStats.GeneralType;

			HasAlarm = pOAlarm != null && pOAlarm.Index != 0;

			if (HasAlarm)
			{
				AlarmVictim = pOAlarm.Index;
				AlarmKind = pOAlarm.Kind;
				AlarmTileX = pOAlarm.TileX;
				AlarmTileY = pOAlarm.TileY;
				AlarmAge = piClock - pOAlarm.Clock;
				AlarmFresh = pOAlarm.IsFresh(piClock);
				AlarmManhattan = System.Math.Abs(pORecord.TileX - pOAlarm.TileX) + System.Math.Abs(pORecord.TileY - pOAlarm.TileY);
			}

			DueSlot = pORecord.DueSlot;
			Phase = piPhase;
			Interval = pORecord.Interval;
			Animation = pORecord.Animation;
			Frame = pORecord.Frame;
			Speed = pORecord.Speed;
			FineHeading = pORecord.FineHeading;
			FacingEighth = pORecord.FacingEighth;
			FacingResidual = pORecord.FacingResidual;

			Confirmed = pORecord.TargetConfirmed;
			Heard = pORecord.HeardSomething;
			Blocked = pORecord.Blocked;
			HasPath = pORecord.HasPath;
			Straight = pORecord.StraightLineKnown;
			Stand = pORecord.MadeStand;
			Relentless = pORecord.Relentless;
			Charge = pORecord.ChargeIndex;
			Pitch = pORecord.Pitch;

			DestinationX = pORecord.DestinationX;
			DestinationY = pORecord.DestinationY;
			DestinationHeight = pORecord.DestinationHeight;
			TileX = pORecord.TileX;
			TileY = pORecord.TileY;
			FineX = pORecord.FineX;
			FineY = pORecord.FineY;
			ZPos = pORecord.ZPos;
			Shots = pOActor.ShotsFired;

			HasStats = pOActor.HasStats;

			if (HasStats)
			{
				Vitality = lOStats.Vitality;
				Morale = lOStats.Morale;
				Initiative = lOStats.Initiative;
				SightRange = lOStats.SightRange;
				DoorSkill = lOStats.DoorSkill;
				Mover = pOActor.IsFlying ? "flier" : (pOActor.IsSwimming ? "swimmer" : "walker");
				IsCaster = lOStats.IsCaster;
			}

			StepResult lOStep = pOActor.LastStep;

			Collided = lOStep.Collided;
			HitObjectIndex = lOStep.HitObjectIndex;
			HitObjectItemId = lOStep.HitObjectItemId;
			HitClosedDoor = lOStep.HitClosedDoor;
		}
	}
}
