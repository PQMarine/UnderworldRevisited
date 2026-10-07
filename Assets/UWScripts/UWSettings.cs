using System.IO;
using UnityEngine;
using UWDataImport.UWData;

namespace UnderworldRevisited
{
    /// <summary>
    /// Project-wide settings. Among other things, replaces the hard-wired path
    /// "C:\UW\DATA" that used to sit directly in the loading code.
    /// </summary>
    public sealed class UWSettings : ScriptableObject
    {
        /// <summary>Name in the Resources folder, so the runtime can reach it without a scene reference.</summary>
        public const string ResourceName = "UWSettings";

        /// <summary>File by which a valid data directory is recognised.</summary>
        public const string MarkerFile = "LEV.ARK";

        [Tooltip("The GOG installation of Ultima Underworld (the folder with game.gog) or game.gog itself. The game data is extracted from the image once into a local cache. Used when DataPath is empty or invalid. Empty: common GOG installation folders are searched.")]
        public string GogInstallPath = string.Empty;

        [Tooltip("Optional: a directory with already extracted game files (LEV.ARK, STRINGS.PAK, ...), with CRIT, CUTS, SOUND and UW.EXE next to it. Takes precedence over GogInstallPath when valid.")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("DataPath")]
        private string mDataPath = string.Empty;

        /// <summary>
        /// The DATA folder the game loads from: DataPath from the settings if it is valid,
        /// otherwise the DATA folder extracted from the GOG image (see UWDataImport.UWGogInstall).
        /// </summary>
        public string DataPath
        {
            get
            {
                if (IsValidDataPath(mDataPath))
                    return mDataPath;

                string lsExtracted = fResolveGogDataPath();

                return lsExtracted ?? mDataPath;
            }
            set
            {
                mDataPath = value;
            }
        }

        [Tooltip("Folder that holds the save game folders SAVE1 to SAVE4, e.g. the UNDEROM1 folder of the GOG installation. Empty: that UNDEROM1 folder when the data comes from game.gog, otherwise the folder above the data folder, as in the original installation. The game no longer loads a save game at launch - loading happens from the main menu only.")]
        public string SavegameFolder = string.Empty;

        [Header("Diagnostics only")]
        [Tooltip("Save game folder read by the editor diagnostic tools (Underworld Revisited/Diagnostics menu). Has no effect on the game.")]
        public string DiagnosticSavegamePath = string.Empty;

        [Tooltip("Pretends no game was found, so the first-run setup screen appears even though the GOG installation would be detected. Only until a folder is chosen there; delete settings.json in the persistent data folder to see it again.")]
        public bool ForceFirstRunSetup;

        [Header("Sound")]
        [Tooltip("Music from the AdLib pieces (AW*.XMI) through the OPL2 emulation, see UWAudioEngine and UWMusic.")]
        public bool MusicEnabled = true;

        [Range(0f, 1f)]
        public float MusicVolume = 0.6f;

        [Tooltip("Sound effects from SOUNDS.DAT and UW.AD through the OPL2 emulation, see UWSoundEffects.")]
        public bool SoundEnabled = true;

        [Range(0f, 1f)]
        public float SoundVolume = 0.8f;

        [Tooltip("Point keeps the hard pixel edges of the original.")]
        public FilterMode TextureFilterMode = FilterMode.Point;

        [Tooltip("Show the classic 320x200 screen at 4:3, every original pixel 1.2 times taller than wide, as on the CRT the game was made for and as DOSBox shows it. For comparing screenshots with DOSBox (per user, 2026-09-17). The 3D view keeps its proportions either way; off, the picture is wider and the original pixels are square. Takes effect immediately.")]
        public bool DisplayAs4By3;

        /// <summary>
        /// Which path determines the colour of a pixel.
        ///
        /// MEANT TO BE SWITCHABLE, so it can become an option later. Both paths use the
        /// same meshes, the same UVs and the same slice layout - geometry and object
        /// placement notice nothing of the switch. Only the textures and the materials
        /// have to be swapped.
        ///
        /// WHAT HAS TO BE SWITCHED TOGETHER:
        ///
        ///   1. Texture array UWTextureArrayBuilder.Build versus BuildIndexed.
        ///   2. Materials     UW/Dungeon versus UW/DungeonPalette, likewise Billboard and
        ///                    Decal, once palette versions of them exist.
        ///   3. Light         the palette path has no point lights. The player light
        ///                    must be off, ambient floor and near-range capping are dropped.
        ///   4. Animation     UWAnimatedTextures copies water and lava frames into the
        ///                    slices and must not run in the palette path - there the
        ///                    rotation table turns through a single shader value.
        ///   5. Per frame     UWShadePalette.ApplyGlobals with light level and rotation step.
        ///
        /// Holding both texture sets at once costs memory. Whoever wants to switch while
        /// the game is running is best off building the second one at the first switch.
        /// </summary>
        public enum RenderModeEnum
        {
            /// <summary>Palette indices in the textures, lighting through SHADES.DAT and
            /// LIGHT.DAT as in the original. See UWShadePalette.</summary>
            Palette = 1,

            /// <summary>Lit by URP, with relief, specular highlights, glow and the rest -
            /// every one of them adjustable on its own below, so turning them all off gives
            /// the plain URP look. See UWRemasterRenderer and
            /// UWTextureArrayBuilder.BuildNormals.</summary>
            Remastered = 2
        }

        /// <summary>Which curve brings the brightness values to the screen (Remastered
        /// mode only).</summary>
        public enum RemasterTonemappingEnum
        {
            Off,

            Neutral,

            Aces
        }

        [Tooltip("Palette computes like the original via palette indices, light level and distance. Remastered lights with URP; every one of its effects can be turned off on its own below.")]
        public RenderModeEnum RenderMode = RenderModeEnum.Palette;

        /// <summary>Are the Remastered effects running right now? Not the setting alone: F6
        /// switches the palette path on without changing it (UWPaletteRenderToggle) - otherwise
        /// ground shadows, lights, relief and torch shadows would stay on (per user,
        /// 2026-09-13).</summary>
        public bool IsRemasteredActive
        {
            // Anything that is not the palette path is the URP path. Written this way on
            // purpose: a settings asset saved before 2026-09-16 may still hold the value of the
            // removed "Modern" mode, and it should land here rather than nowhere.
            get { return RenderMode != RenderModeEnum.Palette && !UWPaletteRenderToggle.IsPaletteActive; }
        }

        [Tooltip("Ambient floor, so that unlit corners are not completely black.")]
        [Range(0f, 1f)]
        public float AmbientFloor = 0.08f;

        [Tooltip("Caps the contribution of individual point lights (torches) at close range, so walls right next to a light source do not burn out.")]
        [Range(0.3f, 5f)]
        public float PointLightCap = 1f;

        [Header("Remastered")]

        [Tooltip("How steep the relief from the height map (UWHeightMapBuilder) turns out. Since the height spans 0 to 1 instead of only varying by a third, 4 is too much: the joint edges tilted almost horizontal and went black. 2 is clearer than 1.2 (per user: less pronounced than hoped). Takes effect when BUILDING the normal array; a change while the game runs rebuilds it (takes a moment).")]
        [Range(0.5f, 12f)]
        public float RemasterHeightStrength = 2f;

        [Tooltip("How strongly the relief acts in the shader. 0 is flat, i.e. the plain URP look without relief.")]
        [Range(0f, 4f)]
        public float RemasterNormalStrength = 1f;

        [Tooltip("Strength of the specular highlight on walls and floor. Only this makes the relief visibly move along with the torch.")]
        [Range(0f, 2f)]
        public float RemasterSpecularStrength = 1.2f;

        [Tooltip("Sharpness of the specular highlight. High values give a small, hard reflection, low values a broad sheen.")]
        [Range(1f, 128f)]
        public float RemasterSpecularPower = 24f;

        [Tooltip("Strength of the glow around bright spots (torches, lava). 0 switches it off.")]
        [Range(0f, 3f)]
        public float RemasterBloomIntensity = 0.35f;

        [Tooltip("Brightness above which something glows. Lower means moderately bright surfaces glimmer too.")]
        [Range(0f, 3f)]
        public float RemasterBloomThreshold = 1.1f;

        [Tooltip("How far the glow bleeds out.")]
        [Range(0f, 1f)]
        public float RemasterBloomScatter = 0.7f;

        [Tooltip("Ambient occlusion: dark corners and joints. Strength and radius are in the renderer file UWUniversalRenderer at the ScreenSpaceAmbientOcclusion feature - URP keeps them internal.")]
        public bool RemasterAmbientOcclusion = true;

        [Tooltip("How much a surface reflects HEAD-ON. 0.04 is the physical value for stone - with it the highlight from a torch at the eye is almost invisible. Higher makes it visible; from about 0.2 everything looks polished or wet.")]
        [Range(0.01f, 0.4f)]
        public float RemasterSpecularBase = 0.06f;

        [Tooltip("How far the light wraps around the edges of the relief. 0 is hard Lambert - flanks facing away from the light go pitch black. Higher fills them in, as if there were scattered light.")]
        [Range(0f, 1f)]
        public float RemasterWrapLighting = 0.35f;

        [Tooltip("How rough the surfaces are. Roughness is stored per pixel in the texture (derived from its grain); this slider multiplies it. Lower means smoother and thus glossier - above 1 everything turns matte.")]
        [Range(0f, 2f)]
        public float RemasterRoughness = 1f;

        [Tooltip("How deep the walls appear (parallax, in fractions of a texture tile). 0 switches it off. It costs computation per pixel.")]
        [Range(0f, 0.2f)]
        public float RemasterParallaxDepth = 0.03f;

        [Tooltip("How many height levels the relief is rounded to. Few levels suit the hard pixels and look calmer in motion than a stepless relief.")]
        [Range(1f, 16f)]
        public float RemasterParallaxLevels = 4f;

        [Tooltip("Maximum number of steps the view ray takes. More means cleaner edges and more computation.")]
        [Range(4f, 48f)]
        public float RemasterParallaxSteps = 16f;

        [Tooltip("Reverses the direction of the parallax - for comparison only. Correct is OFF, since the handedness of the tangent frame is right (2026-09-13).")]
        public bool RemasterParallaxInvert = false;

        [Tooltip("How dark the relief's self-shadow gets: stones cast shadows into the joints, and the shadow moves with the light. 0 switches it off. Costs eight texture reads per pixel and light.")]
        [Range(0f, 1f)]
        public float RemasterSelfShadow = 0.8f;

        [Tooltip("How tall the relief counts for the self-shadow, in fractions of a texture tile (64 texels). Higher means longer shadows.")]
        [Range(0.001f, 0.1f)]
        public float RemasterSelfShadowDepth = 0.1f;

        [Tooltip("Maximum distance the shadow ray travels, in fractions of a texture tile. 0.15 is just under ten texels.")]
        [Range(0.01f, 0.3f)]
        public float RemasterSelfShadowReach = 0.15f;

        [Tooltip("How quickly the shadow becomes fully dark. Higher means hard edges, lower a soft falloff.")]
        [Range(0.5f, 16f)]
        public float RemasterSelfShadowSoftness = 10f;

        [Tooltip("How strongly recessed spots (joint rims, set-back stones) are permanently darkened, regardless of light direction. Also works with the light in hand, where the self-shadow disappears behind the stones.")]
        [Range(0f, 1f)]
        public float RemasterCavity = 0.8f;

        [Tooltip("How strongly lava glows by itself. It is recognised by palette range 16 to 23 - the same colours the original rotates.")]
        [Range(0f, 4f)]
        public float RemasterEmissiveStrength = 1f;

        [Tooltip("Torches, candles, lanterns, campfires, fire elementals, light stones, mushrooms and orbs become real light sources. In the original only the player's torch gives light - this is an intended deviation, in this mode only.")]
        public bool RemasterTorchLights = true;

        [Tooltip("Point lights cast shadows: the player light and the larger fires (torch, lantern, campfire). Costs computation.")]
        public bool RemasterTorchShadows = true;

        [Tooltip("This many torches and fires in the world cast shadows at the same time - the ones nearest the player. Each shadowed point light needs six shadow maps; previously all of them cast shadows, and URP had to squeeze up to 18 maps into the atlas (stutter). The player light does not count.")]
        [Range(0, 8)]
        public int RemasterMaxShadowLights = 2;

        [Tooltip("Lava lights its surroundings - one light per three tiles, at most 48 per level.")]
        public bool RemasterLavaLights = true;

        [Tooltip("Factor on the player's CARRIED light. It was built for the mode in which it is the only light source, and otherwise outshines the placed lights.")]
        [Range(0f, 2f)]
        public float RemasterCarriedLightFactor = 0.6f;

        [Tooltip("Where a torch or candle shines, relative to the player centre (x right, y up, z forward; the eye sits at y 20, the body has radius 12). To the side rather than at the eye, so the light casts shadows you can see. Keep horizontal values below 12, otherwise at a wall the light pokes into the wall.")]
        public Vector3 RemasterHandLightOffset = new Vector3(9f, 8f, 7f);

        [Tooltip("Where a lantern shines: at the belt, in front of the belly.")]
        public Vector3 RemasterBeltLightOffset = new Vector3(3f, -4f, 9f);

        [Tooltip("Where a light spell shines (In Lor and similar, without a carried item): in the left hand.")]
        public Vector3 RemasterMagicLightOffset = new Vector3(-8f, 10f, 6f);

        [Tooltip("How strongly the bright spots of glowing sprites glow themselves (flames, light stones, mushrooms, orbs). 0 switches it off.")]
        [Range(0f, 3f)]
        public float RemasterSpriteGlow = 1f;

        [Tooltip("How strongly the LIGHT of open fire flickers - placed torches, candles, campfires, fire elementals and the torch or candle in hand. The lantern burns behind glass and hardly flickers.")]
        [Range(0f, 0.8f)]
        public float RemasterFireFlicker = 0.35f;

        [Tooltip("By how many units the light of open fire jitters. This makes the shadows dance; a tile is 64 units wide.")]
        [Range(0f, 6f)]
        public float RemasterFireJitter = 1.5f;

        [Tooltip("How strongly the GLOW OF THE SPRITE of a fire flickers along. Only very slightly - a pulsing flame image looks odd, whereas the light may flicker strongly (per user, 2026-09-12).")]
        [Range(0f, 0.3f)]
        public float RemasterSpriteFlicker = 0.06f;

        [Tooltip("How many sparks rise from open fire (torch, candle, campfire, fire elemental). 0 switches them off.")]
        [Range(0f, 3f)]
        public float RemasterFireSparks = 1f;

        [Tooltip("How dense the smoke above open fire is. 0 switches it off.")]
        [Range(0f, 1f)]
        public float RemasterFireSmoke = 0.6f;

        [Tooltip("How much dust floats in the air in front of the player. It is only visible where light falls. 0 switches it off.")]
        [Range(0f, 3f)]
        public float RemasterDust = 1f;

        [Tooltip("How dark the soft spot under creatures and items is. The AO already darkens a bit around the feet, hence restrained. 0 switches it off.")]
        [Range(0f, 1f)]
        public float RemasterGroundShadow = 0.65f;

        [Tooltip("Size of the ground shadow relative to the width of the object (for creatures, their footprint).")]
        [Range(0.3f, 3f)]
        public float RemasterGroundShadowSize = 1.2f;

        [Tooltip("Factor on the brightness of all additional lights.")]
        [Range(0f, 3f)]
        public float RemasterLightBrightness = 1f;

        [Tooltip("How bright and dark values reach the screen. Neutral keeps the colours, ACES gives a filmic contrast.")]
        public RemasterTonemappingEnum RemasterTonemapping = RemasterTonemappingEnum.Neutral;

        [Tooltip("Which sub-tile number the tile centre means. 3.5 is the centre of the eight sub-tiles and measured against the original: the secret door on 35/58 with sub-tile 0 stands exactly four wall texture pixels from the tile edge, and (0 - 3.5) times eight gives exactly minus 28, i.e. four units in front of the edge at 32.")]
        [Range(3f, 4f)]
        public float DoorSubTileReference = DefaultDoorSubTileReference;

        /// <summary>The measured reference point, also used where no settings asset exists
        /// (editor tools, see UWObjectSpawner.fGetDoorTranslation).</summary>
        public const float DefaultDoorSubTileReference = 3.5f;

        [Tooltip("Places doors at their sub-tile position instead of always in the tile centre. Applies equally to door leaf, frame, portcullis and secret doors.")]
        public bool UseDoorSubTilePosition = true;

        [Tooltip("On every swing, logs what the ray hit and how far away it was. Debugging only.")]
        public bool LogAttacks;

        [Tooltip("Lets the Resist Blows spell add its value to the armour of all four body parts. The original writes that value into one nibble and reads it from another, so the spell protects against nothing there. Off is faithful to the original.")]
        public bool ResistBlowsAddsArmour;

        [Tooltip("Position of the eyes at the top edge, measured from the centre of the top screen edge in original pixels. Y is negative downwards.")]
        public Vector2 EyesPosition = new Vector2(0f, -8f);

        [Tooltip("How long the eyes show the state of the enemy that was hit (seconds). A new hit resets the time.")]
        public float EyesSeconds = 4f;

        [Tooltip("Duration of the eyes fading in and out (seconds).")]
        public float EyesFadeSeconds = 0.3f;

        [Tooltip("Frames per second when the eyes play their frame sequence on a colour change.")]
        public float EyesFramesPerSecond = 8f;

        [Tooltip("Fine offset of the hit effect relative to the enemy's body centre (world units, negative downwards). The centre is the starting point, the variation comes on top.")]
        public float HitEffectHeightOffset;

        [Tooltip("Fixed part of the charge time in seconds that every weapon needs alike - presumably the wind-up itself. Found from two measurements in the original.")]
        public float AttackChargeBaseSeconds = 0.73f;

        [Tooltip("Duration of one charge step. Per step the original adds the weapon's speed value to a counter up to 100.")]
        public float AttackChargeStepSeconds = 0.072f;

        [Tooltip("How many frames before the end of the swing animation the blow lands. 2 means second-to-last frame, 3 third-to-last. Counted from the end because the animation length differs by attack type - an axe chop has four frames, a thrust six.")]
        [Range(1, 4)]
        public int AttackConnectFramesFromEnd = 2;

        [Tooltip("Random height variation of the hit effect in world units. The value is afterwards clamped back into the enemy's extent, so the effect stays visible.")]
        public float HitEffectHeightVariation = 8f;

        [Tooltip("Delay between the blow and the visible hit effect (seconds) - in the original it appears when the weapon animation reaches the middle of the screen.")]
        public float HitEffectDelaySeconds;

        [Tooltip("How long a blood splatter is visible on a hit (seconds).")]
        public float HitEffectSeconds = 0.6f;

        [Tooltip("How far the impact flash on an OBJECT (barrel, chest, door) is pulled towards the player, in world units (a tile is 64). Without it the flash sits in the middle of the barrel and the model hides it. Creatures are not affected - their effect stays on the figure.")]
        public float HitEffectTowardsPlayer = 12f;

        [Header("Water and lava")]
        [Tooltip("How far above the tile floor the feet still count as 'in the liquid'. Above that you walk over it dry, for example on a bridge.")]
        public float LiquidEntryTolerance = 8f;

        [Tooltip("View height while swimming above the tile's floor height. The user's first estimate was 16 to 32; readjusted to 20 in comparison with the original. 17 since 2026-09-17 from UW.EXE: the camera sits 0xA4 above the body (seg034_2F89_BCD, 41 units), swimming subtracts the swim counter, which starts at 0x60 (24 units, per UnderworldGodot); failed swimming checks raise it further and the view sinks towards about 11.")]
        public float SwimViewHeight = 17f;

        [Tooltip("How strongly the view sways while swimming without skill (units up and down). Halved on 2026-09-16: at 8 the view went so deep at the bottom of the sway that a lurker in front of it looked as if it were floating above the water.")]
        public float SwimSwayUnskilled = 4f;

        [Tooltip("How strongly the view still sways at full swimming skill.")]
        public float SwimSwaySkilled = 1f;

        [Tooltip("Sways per second.")]
        public float SwimSwayFrequency = 0.5f;

        [Tooltip("How far the image tilts sideways while swimming without skill, in degrees. Small in the original, but faster than the up and down (per user, 2026-09-01).")]
        public float SwimRollUnskilled = 1.5f;

        [Tooltip("How far the image still tilts at full swimming skill.")]
        public float SwimRollSkilled = 0.3f;

        [Tooltip("Tilts per second. Considerably faster than the up and down.")]
        public float SwimRollFrequency = 2.5f;

        [Tooltip("Classic scheme: keys 1 (down) and 3 (up) tilt the view by this many degrees per step. 2 levels it again (per user, 2026-09-13: 15 for now).")]
        [Range(1f, 30f)]
        public float ClassicLookStepDegrees = 15f;

        [Tooltip("Classic scheme: this many steps per direction (per user, 2026-09-13: three fixed steps).")]
        [Range(1, 6)]
        public int ClassicLookSteps = 3;


        [Tooltip("How fast the view follows the target height, in units per second. Mainly affects the transition when entering and leaving.")]
        public float SwimViewFollowSpeed = 60f;

        [Tooltip("Swimming speed without skill, as a fraction of walking speed. The original's speed table says 3 of 10; in comparison that still felt too fast, because our base speed is a value of its own and not the original's.")]
        [Range(0.1f, 1f)]
        public float SwimSpeedUnskilled = 0.26f;

        [Tooltip("Swimming speed at full skill. In UW1 there is NO skill bonus on speed, hence the same value - the bonus is new in UW2.")]
        [Range(0.1f, 1f)]
        public float SwimSpeedSkilled = 0.26f;

        [Tooltip("Swimming skill value at which speed and view steadiness are fully maxed out.")]
        public float SwimSkillForFullEffect = 15f;

        [Tooltip("Seconds between two swimming checks while the swim counter is above 0x50 (see UWPlayerTerrain.fUpdateSwimCounter). Each failure lowers the view, above 0x78 the character drowns. The reference makes this check in its periodic player loop, so the interval is that loop's length: 21 seconds (UWPlayerTick). Fits the user's earlier estimate of about one minute until an unpractised swimmer takes damage: two to three failed checks. Replaces the guessed SwimExhaustion settings (2026-09-17).")]
        public float SwimCheckInterval = UWPlayerTick.Seconds;

        [Tooltip("How strongly the current changes swimming speed. 0.5 means one and a half times as fast with the current, half as fast against it. OFF, because the observation it was based on is a bug of the original - see UWPlayerTerrain.fUpdateFlow.")]
        [Range(0f, 1f)]
        public float WaterFlowStrength = 0f;

        [Tooltip("Direction of the current in degrees around the vertical axis: 0 north (+Z), 90 east (+X), 180 south, 270 west. In UW1 all water carries the same direction, because all water tiles have the same terrain value. 225 is southwest, per the user's observation on level 1.")]
        [Range(0f, 360f)]
        public float WaterFlowHeadingDegrees = 225f;

        [Tooltip("Speed on lava, as a fraction of walking speed. Confirmed by the original's speed table (5 of 10).")]
        [Range(0.1f, 1f)]
        public float LavaSpeedFactor = 0.5f;

        [Tooltip("Seconds between two lava damage rolls. UW.EXE rolls once per game frame (GameObjectLoop_seg034_2F89_518 -> WalkOnSurfaceTypes_seg034_93D); 1/32 s uses the same game loop rate as the void effects (VoidEffectTicksPerSecond, per user, 2026-09-14). The earlier 0.5 s did far too little damage.")]
        public float LavaDamageInterval = 1f / 32f;

        [Tooltip("How often a roll hits. One in five in the original.")]
        [Range(0f, 1f)]
        public float LavaDamageChance = 0.2f;

        [Tooltip("Damage per hit. 1 in the original.")]
        public int LavaDamage = 1;

        [Header("Creatures")]
        [Tooltip("Writes every experience gain to the console. Levelling up is otherwise hard to observe, because it happens rarely.")]
        public bool LogExperience = true;

        // The cadence, speed scale, attack range and the wander and withdraw timers that used
        // to live here are gone since 2026-09-20: the creature brain
        // (UWCritterBrain) takes all of them from the original - the 16-slot clock, the
        // speed bytes of the creature table, the melee distances of NPC_Goal5_Attack. What
        // stays is the physics substitute: the clearances below.

        [Tooltip("Additional distance a creature keeps from diagonal walls. Diagonals need more than straight walls, because the creature's image follows the camera and overlaps a 45-degree wall differently at every rotation. 22 since 2026-09-28 (was 24): the original tests the corners of a box of the COMOBJ radius (2 eighths, 16 units), which against a 45-degree wall is 16 * 1.41 = 22.6 - and a diagonal corridor of two slant tiles is 45.25 wide, so 24 on each side shut every one of them (per user: Biden stuck at level 4, 15/33).")]
        public float CritterDiagonalClearance = 22f;

        [Tooltip("World units per radius point from COMOBJ.DAT. Every creature has radius 2 there. Larger values make creatures wider - then you can no longer get past a figure in a one-tile passage. 4 since 2026-09-17 (was 6, as wide as the player): in a SAVE4 of the original the player stood 20.25 units in front of Drog (player fine 64/160 on 13/35, Drog at eighths 2/2 plus 0xF), and our box of half width 12 plus the player radius 12 pushed the loaded player 4 units back, which made Drog look a quarter too small (per user with screenshots and the F1 position 816 / 2252 instead of 2248). With 4 the two may come within 20 units. Both have radius 2 in COMOBJ.DAT (goblin 71, adventurer 127), so the original evidently does not treat the radii as solid circles.")]
        public float CritterRadiusScale = 4f;

        [Tooltip("Distance a creature keeps from solid tiles (world units, 64 = one tile). Its image turns to face the camera and would otherwise partly stick into the wall. 0 switches the distance off.")]
        public float CritterWallClearance = 16f;

        [Tooltip("Ethereal void (level 9): game loop passes per second that roll for the effects, each with a chance of 1 in 32 (flash, vitality drain, shake, compass). The original rate depends on the machine speed. Under DOSBox (30000 cycles) effects come several times per second, faster than a machine of the time; set to about one effect per second (per user, 2026-09-14): 32.")]
        [Range(0f, 200f)]
        public float VoidEffectTicksPerSecond = 32f;

        [Tooltip("Instruments: a note sounds this long if no other follows. UW.EXE uses 0x40 timer ticks - how much that is in seconds is not verified.")]
        [Range(0.05f, 2f)]
        public float InstrumentNoteSeconds = 0.35f;

        [Tooltip("Health and mana flask fill animation after the resurrection: seconds per flask level (13 levels). 0.25 s as observed in the original (per user, 2026-09-14).")]
        [Range(0f, 0.5f)]
        public float FlaskFillStepSeconds = 0.25f;

        [Tooltip("Pull into the moongate at the end: seconds per step (64 steps). UW.EXE draws one frame per step without a wait; the original's frame rate is taken as 15 frames per second (per user, 2026-09-14: 0.1 too slow, one step per frame at 60 fps far too fast). 0 = one step per frame.")]
        [Range(0f, 0.2f)]
        public float MoongateStepSeconds = 1f / 15f;

        [Tooltip("Void creatures (level 9, goal 0xB): drift speed in world units per second. The reference sets speed 4 - not verified in world units, eyeballed.")]
        [Range(0f, 64f)]
        public float VoidCreatureDriftSpeed = 8f;

        [Tooltip("Void creatures: at most this far (in tiles) from the starting point. The reference does not limit this explicitly.")]
        [Range(0f, 3f)]
        public float VoidCreatureDriftRadius = 0.5f;

        [Tooltip("Void creatures: a new random direction this often (seconds) - in the reference once per AI pass.")]
        [Range(0.05f, 5f)]
        public float VoidCreatureTurnSeconds = 0.5f;

        [Tooltip("Distance a creature keeps from drops (world units): spots where the floor lies lower than it can climb. In the original creatures avoid the edge (per user, 2026-09-12: in our version a goblin on a ramp slid along the edge). Larger than the wall clearance, because a fall is worse than a wall.")]
        public float CritterDropClearance = 24f;

        [Tooltip("Distance a WATER creature (the lurker) keeps from the shore, in world units. Stepping onto land is refused anyway; this keeps it from pressing against the bank, where half its picture would lie on dry ground (per user on the original, 2026-09-13). 0 switches it off.")]
        public float CritterShoreClearance = 16f;

        [Tooltip("Smallest collider height of a creature, in world units (one unit per picture pixel). The player climbs anything up to 16 units, so a flesh slug (15 pixels) or a rotworm (17) would be a step to walk onto instead of a body to bump into.")]
        public float CritterMinimumColliderHeight = 24f;

        [Tooltip("How far a water creature is lifted above the tile floor, in world units. The painted ripple in its image lies three to four rows below the ground point, so at 0 the creature sits too deep. 10 was measured in the game: on level 1 the water tile floor is 16 and the lurker fits at y 26 (per user in the scene view, 2026-09-16). Negative sinks it. 0 since 2026-09-17: in the original a lurker lies exactly on the water floor (SAVE4, z 8 on floor 1), and its image is no longer cut off because the water floor no longer covers it (see UWOwnTile.hlsl). The lift of 10 made it look as if floating in front of walls.")]
        public float SwimmerHeightOffset = 0f;

        [Tooltip("Distance a WATER creature keeps from a HIGH edge - a solid tile or a floor more than one height level above the water - in world units. Larger than the shore distance, because against a wall rising out of the water the creature looked as if it were floating (per user, 2026-09-16). 24 since 2026-09-17: in a SAVE4 of the original a lurker pressed against a wall lay at ypos 5, 17 to 24 units from it; the floating came from the height, not from the distance. Like the shore distance it is a direction rule: it may not get closer than it already is.")]
        public float SwimmerHighEdgeClearance = 24f;

        [Tooltip("The same for a WATER creature, and it has to be considerably larger. You fight a lurker from the SHORE, which lies one floor level (16 units) above the water. Standing there the player climbs another 16, so anything whose top is at or below the water floor plus 32 can be walked onto. A whole tile of 64 leaves a clear margin - in the original you cannot stand on a lurker from the shore either (per user, 2026-09-16).")]
        public float SwimmerMinimumColliderHeight = 64f;

        [Tooltip("Vertical scale of creature images around their ground point; the width stays one unit per pixel. 1.3 since 2026-09-17: Drog on level 1 (SAVE4) stood at the same spot as in the original but looked squashed (per user with screenshots: he has to get bigger in any case). Measured about 1.35 from the screenshots, which was too much by eye; 1.25 still a little, 1.2 per user - all judged with square pixels. With DisplayAs4By3 on both screenshots side by side (Drog seen from behind in a corridor, same frame) he was as wide as in the original but about 8 percent shorter and looked bulkier (per user), so 1.3. Takes effect for frames built after the change, so reload the level.")]
        public float CritterSpriteHeightScale = 1.3f;

        [Tooltip("Horizontal shift of the wall texture on door frames and lintels, in tiles. 0.5 since 2026-09-17: the lintel above the portcullis behind Drog on level 1 needed its texture shifted (per user against the original, first estimate a quarter tile, confirmed at 0.5). Half a tile is exactly the difference between our frame UVs (-0.5 to 0.5 around the door centre) and the wall mesh (0 to 1 per tile), so the texture now lines up with the walls. Applied to every door frame since 2026-09-17 (per user); the setting was called PortcullisFrameTextureShift before. Takes effect on reload.")]
        [UnityEngine.Serialization.FormerlySerializedAs("PortcullisFrameTextureShift")]
        public float DoorFrameTextureShift = 0.5f;

        [Tooltip("Diagnosis of the creature AI: the Shift+F1 overlay then shows ONLY the looked-at creature, with the last eight decisions - goal changes, the flee roll with its numbers, refused steps. Debugging only, off for playing. Used for the lurker session of 2026-09-16/17 and switched off again afterwards; the tool stays for the next creature.")]
        public bool CritterTrace;

        [Tooltip("Logs why a pursuing creature could not take a step: tile, tile floor, what the ground ray found and how high the creature itself stands. At most one line per second and creature. Debugging only.")]
        public bool CritterLogBlockedSteps;

        [Tooltip("Largest height difference a creature overcomes in one step (world units).")]
        public float CritterMaxStepHeight = 16f;

        [Tooltip("Rotation steps per second for lava and water on walls and floors. The original moves the palette for this (uw-formats.txt 3.1.4); how fast is not stored anywhere in the data. Same value as AnimatedObjectFramesPerSecond: the fountain has four frames, the water four rotation steps, and in the original both run at the same rate (user, 2026-08-30). Also applies to creatures: the flames of the fire elemental sit on the same palette slots and, in the original as in ours, run at the same rate as lava and water (user, 2026-09-08).")]
        [Range(1f, 30f)]
        public float PaletteRotationStepsPerSecond = 4f;

        [Tooltip("Frames per second for the animated objects (fountain water, silver tree, smoke). Not stored anywhere in the original data - none of the tables for these objects contains a time value.")]
        [Range(1f, 15f)]
        public float AnimatedObjectFramesPerSecond = 4f;

        [Tooltip("Height offset of the animated objects in world units (one pixel = one unit). In the data the fountain water sits one height step (= 2 units) above its basin; in the original that is a hint at the draw order, not at the height, and in our version the order is handled by the depth offset in the shader. The fitting value by eye is -4 (user, 2026-08-30), i.e. two height steps - why it is exactly double is not resolved.")]
        [Range(-8f, 8f)]
        public float AnimatedObjectHeightOffset = -4f;

        [Tooltip("Like PointLightCap, but for objects (billboards, doors, 3D models). A separate value, because their flat solid colours look brighter at the same light intensity than a muted stone texture.")]
        [Range(0.3f, 5f)]
        public float ObjectPointLightCap = 0.6f;

        private static UWSettings mOInstance;

        public static UWSettings Instance
        {
            get
            {
                if (mOInstance == null)
                    mOInstance = Resources.Load<UWSettings>(ResourceName);

                return mOInstance;
            }
        }

        public bool IsDataPathValid
        {
            get { return IsValidDataPath(DataPath); }
        }

        /// <summary>
        /// The original uw.exe contains a few real 3D models (shrine/ankh, door frame,
        /// bridge, ...) that exist nowhere else in the game - see UW3DModelImport.
        /// Optional: without the .exe the affected objects fall back to their 2D sprite.
        ///
        /// Looked up in the data folder first and then in the folder above it - in a normal
        /// installation (e.g. GOG: UNDEROM1\UW.EXE next to UNDEROM1\DATA) it lies above. If
        /// neither exists, the path in the data folder is returned (IsExePathValid is false).
        /// </summary>
        public string ExePath
        {
            get
            {
                if (string.IsNullOrEmpty(DataPath))
                    return null;

                string lsInData = Path.Combine(DataPath, "UW.EXE");

                if (File.Exists(lsInData))
                    return lsInData;

                try
                {
                    DirectoryInfo lOParent = Directory.GetParent(DataPath.TrimEnd('\\', '/'));
                    string lsAbove = lOParent == null ? null : Path.Combine(lOParent.FullName, "UW.EXE");

                    if (lsAbove != null && File.Exists(lsAbove))
                        return lsAbove;
                }
                catch
                {
                }

                return lsInData;
            }
        }

        public bool IsExePathValid
        {
            get { return !string.IsNullOrEmpty(ExePath) && File.Exists(ExePath); }
        }

        /// <summary>
        /// The folder that holds the four save game folders SAVE1 to SAVE4 - for loading
        /// from within the game (see UWSavegameSlots).
        ///
        /// In the original they sit NEXT TO the data folder: under UNDEROM1 are DATA, SAVE1
        /// to SAVE4 and a few more. If SavegameFolder is set, it is used as is; otherwise
        /// the folder above the data folder.
        ///
        /// WHY BOTH WAYS: the data folder may live somewhere other than the save games (for
        /// example extracted data in one place, the save games in the installation). If
        /// SavegameFolder is set in the settings, it reliably points to the right place.
        /// </summary>
        public string SavegameRoot
        {
            get
            {
                // An explicit folder wins; the data folder and the save games may live in
                // different places (e.g. extracted data elsewhere, saves in the GOG installation).
                if (!string.IsNullOrEmpty(SavegameFolder))
                    return SavegameFolder.TrimEnd('\\', '/');

                // Data from the GOG image: the save games are in the installation's UNDEROM1
                // folder, where the original (DOSBox) writes them too.
                if (!IsValidDataPath(mDataPath) && fResolveGogDataPath() != null && msGogSavegameFolder != null)
                    return msGogSavegameFolder;

                string lsFrom = DataPath;

                if (string.IsNullOrEmpty(lsFrom))
                    return null;

                try
                {
                    DirectoryInfo lOParent = Directory.GetParent(lsFrom.TrimEnd('\\', '/'));

                    return lOParent == null ? null : lOParent.FullName;
                }
                catch
                {
                    return null;
                }
            }
        }

        public static bool IsValidDataPath(string psPath)
        {
            return !string.IsNullOrEmpty(psPath) && File.Exists(Path.Combine(psPath, MarkerFile));
        }

        /// <summary>
        /// Common GOG installation folders of Ultima Underworld (the folder holding game.gog),
        /// searched when GogInstallPath is empty. ON LINUX (since the Linux build, 2026-10-05)
        /// the usual homes of a GOG game: the GOG installer's and Heroic's "GOG Games" folders,
        /// Lutris' "Games", and the Wine, Steam Proton and Lutris prefixes that hold the Windows
        /// installer's folder - game.gog is the CD image every GOG release of the game carries,
        /// however it was installed (innoextract included).
        /// </summary>
        public static string[] GetCandidateGogPaths()
        {
            if (Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor)
            {
                string lsHome = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

                if (string.IsNullOrEmpty(lsHome))
                    lsHome = "/root";

                return new string[]
                {
                    Path.Combine(lsHome, "GOG Games/Ultima Underworld"),
                    Path.Combine(lsHome, "GOG Games/Ultima Underworld 1+2"),
                    Path.Combine(lsHome, "Games/Ultima Underworld"),
                    Path.Combine(lsHome, "Games/Heroic/Ultima Underworld 1+2"),
                    Path.Combine(lsHome, "Games/gog/ultima-underworld-1-2"),
                    Path.Combine(lsHome, ".wine/drive_c/GOG Games/Ultima Underworld"),
                    Path.Combine(lsHome, ".wine/drive_c/Program Files (x86)/GOG Galaxy/Games/Ultima Underworld"),
                    Path.Combine(lsHome, ".steam/steam/steamapps/compatdata/pfx/drive_c/GOG Games/Ultima Underworld"),
                    Path.Combine(lsHome, ".local/share/Steam/steamapps/compatdata/pfx/drive_c/GOG Games/Ultima Underworld"),
                    "/opt/GOG Games/Ultima Underworld",
                    "/usr/local/games/Ultima Underworld"
                };
            }

            return new string[]
            {
                @"C:\GOG Games\Ultima Underworld",
                @"D:\GOG Games\Ultima Underworld",
                @"C:\Program Files (x86)\GOG Galaxy\Games\Ultima Underworld",
                @"C:\Program Files (x86)\GOG.com\Ultima Underworld",
                @"C:\Ultima Underworld",
                @"D:\Ultima Underworld"
            };
        }

        /// <summary>The data folder the game would use (see DataPath), or null if there is
        /// none - kept for the callers that search when the configured path is invalid.</summary>
        public static string AutoDetectDataPath()
        {
            UWSettings lOSettings = Instance;
            string lsPath = lOSettings != null ? lOSettings.DataPath : null;

            return IsValidDataPath(lsPath) ? lsPath : null;
        }

        /// <summary>The DATA folder extracted from the GOG image, resolved once per session.</summary>
        private static string msGogDataPath;

        private static string msGogSavegameFolder;

        /// <summary>The game.gog the data was extracted from this session, or null - the manual
        /// lies next to it (UWManual).</summary>
        public static string GogImagePath { get; private set; }

        private static string msGogResolvedFor;

        /// <summary>Why the last GOG image could not be used, or null - for the setup screen.</summary>
        public static string LastGogError { get; private set; }

        /// <summary>Finds game.gog (GogInstallPath or the common folders), extracts the UW1
        /// data into the cache on first use and returns its DATA folder, or null.</summary>
        private string fResolveGogDataPath()
        {
            // The asset's path first (set by a developer), then the folder the player chose in the
            // setup screen (UWUserSettings, kept outside the asset), then the common folders.
            string lsInstall = !string.IsNullOrEmpty(GogInstallPath) ? GogInstallPath : UWUserSettings.GogInstallPath;
            string lsKey = (lsInstall ?? string.Empty) + (ForceFirstRunSetup ? "|setup" : string.Empty);

            if (msGogResolvedFor == lsKey)
                return msGogDataPath;

            msGogResolvedFor = lsKey;
            msGogDataPath = null;
            msGogSavegameFolder = null;
            LastGogError = null;

            // For testing the setup screen on a machine where the game would be found anyway.
            if (ForceFirstRunSetup && string.IsNullOrEmpty(UWUserSettings.GogInstallPath))
                return null;

            string lsImage = UWDataImport.UWGogInstall.FindImage(lsInstall);

            if (lsImage == null && string.IsNullOrEmpty(lsInstall))
            {
                foreach (string lsCandidate in GetCandidateGogPaths())
                {
                    lsImage = UWDataImport.UWGogInstall.FindImage(lsCandidate);

                    if (lsImage != null)
                        break;
                }
            }

            if (lsImage == null)
                return null;

            string lsCache = Path.Combine(Application.persistentDataPath, "GameData");
            string lsError;
            string lsData = UWDataImport.UWGogInstall.EnsureExtracted(lsImage, lsCache, out lsError);

            if (lsData == null)
            {
                Debug.LogError("UWSettings: " + lsError);
                LastGogError = lsError;
                return null;
            }

            msGogDataPath = lsData;
            msGogSavegameFolder = UWDataImport.UWGogInstall.GetSavegameFolder(lsImage);
            GogImagePath = lsImage;

            return msGogDataPath;
        }
    }
}
