using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;
using UnderworldRevisited;
using UnderworldRevisited.Build;

/// <summary>
/// Draws a creature as an animated image instead of a static sprite.
///
/// The images live in the "crit" folder (see UWCritterAnimations): per creature an
/// animation number and an auxiliary palette, and within them slots for the individual
/// movements. Since 2026-09-20 this class has NO TIMER OF ITS OWN: the creature
/// brain advances the frame with every update of the creature (spec 1.5 - walking four
/// frames per second, a swing frame by frame to the blow at frame 4, dying four updates),
/// and UWCritter hands the Motion's animation slot and frame over through ShowMotion. The
/// animator maps the original's slot numbers to the atlas pages (deviation 21):
///   0x00       the combat idle
///   0x01-0x03  melee attack (bash, slash, thrust), 0x05 ranged, 0x0D spell
///   0x07       backing off (the walking pictures, the body facing the target)
///   0x0C       dying
///   0x20       standing - one of the eight direction pages 0x20-0x27 by the view
///   0x2C       walking - one of the eight direction pages 0x80-0x87 by the view
///
/// The void creatures (UWVoidCreature, no UWCritter and no brain) keep looping their idle
/// pictures on a fixed 0.375 s cadence, the original's standing interval.
/// </summary>
public class UWCritterAnimator : MonoBehaviour
{
    /// <summary>Atlases and materials apply to all creatures of the same kind and
    /// are built only once. The key is animation number and auxiliary palette.</summary>
    private static readonly Dictionary<int, UWCritterAtlas> mOAtlasCache = new Dictionary<int, UWCritterAtlas>();

    private static readonly Dictionary<int, Material> mOMaterialCache = new Dictionary<int, Material>();

    /// <summary>The cadence of a creature without a brain (the void creatures): the standing
    /// interval of 6 slots at 16 PIT ticks of 1/256 s.</summary>
    private const float VoidFrameSeconds = 6f * 16f / 256f;

    private UWCritter mOCritter;
    private UWDamageable mODamageable;
    private MeshFilter mOFilter;
    private MeshRenderer mORenderer;

    private UWCritterAtlas mOAtlas;
    private readonly Dictionary<int, Mesh> mOMeshCache = new Dictionary<int, Mesh>();

    private int[] mOCurrentFrames;
    private int miCurrentSlot = -1;
    private int miFrameInSegment;

    /// <summary>Which slot and which frame within it are showing - for the F1 overlay, so a
    /// misplaced image can be traced back to its frame (2026-09-16).</summary>
    public int CurrentSlot => miCurrentSlot;

    public int CurrentFrameInSegment => miFrameInSegment;

    /// <summary>The brain's animation slot (the original's numbering) and frame, as the last
    /// Motion set them; -1 before the first update.</summary>
    private int miAnimation = -1;

    private int miFrame;

    private float mfNextVoidFrameTime;

    /// <summary>The death animation has reached its last frame - the brain's frame 3 of
    /// animation 0x0C (UWCritter.OnDeathFinished). After that the remains can appear (see
    /// UWCritterRemains).</summary>
    public bool DeathFinished { get; private set; }

    /// <summary>A big object's radius in eighths of a tile - COMOBJ byte 1 bits 0-2 when bit 3 (in
    /// our table IsAnimated) marks it big - or 0; see UWOwnTile.BigRadiusProperty.</summary>
    private static int fGetBigRadius(DataImport pOData, int piObjectId)
    {
        UWCommonObjectProperties.Entry lOEntry;

        if (pOData.CommonObjectProperties == null || !pOData.CommonObjectProperties.TryGet(piObjectId, out lOEntry)
            || !lOEntry.IsAnimated)
            return 0;

        return lOEntry.Radius;
    }

    public void Initialise(DataImport pOData, int piObjectId, Material pOTemplate, Material pOTranslucentTemplate)
    {
        if (pOData == null || pOData.CritterAnimations == null || !pOData.CritterAnimations.IsLoaded)
            return;

        int liAnimation = pOData.CritterAnimations.GetAnimationForObject(piObjectId);

        if (liAnimation < 0)
            return;

        int liAuxPalette = pOData.CritterAnimations.GetAuxPaletteForObject(piObjectId);
        int liBigRadius = fGetBigRadius(pOData, piObjectId);
        int liKey = (liAnimation << 8) | liAuxPalette;
        int liMaterialKey = liKey | (liBigRadius << 16);

        if (!mOAtlasCache.TryGetValue(liKey, out mOAtlas))
        {
            mOAtlas = UWCritterAtlasBuilder.Build(pOData, liAnimation, liAuxPalette, fGetFilterMode());
            mOAtlasCache[liKey] = mOAtlas;
        }

        if (mOAtlas == null || mOAtlas.Texture == null)
            return;

        Material lOMaterial;

        if (!mOMaterialCache.TryGetValue(liMaterialKey, out lOMaterial))
        {
            Material lOSource = mOAtlas.IsTranslucent && pOTranslucentTemplate != null ? pOTranslucentTemplate : pOTemplate;

            if (lOSource == null)
                return;

            lOMaterial = new Material(lOSource);
            lOMaterial.name = "UW Creature " + liAnimation + "/" + liAuxPalette;
            lOMaterial.mainTexture = mOAtlas.Texture;

            // Its own index atlas for Remastered's hallucination and ghosts (UWHallucination.hlsl) -
            // the template carries the object atlas's.
            lOMaterial.SetTexture("_IndexTex", mOAtlas.IndexTexture != null ? mOAtlas.IndexTexture : Texture2D.blackTexture);

            // The creature's own tile does not cover it, only what is nearer (see UWOwnTile). First
            // built for the lurker, whose image hangs twelve rows under the water; since
            // 2026-09-17 for every creature (per user: Drog's feet sank into the floor), and since
            // 2026-09-22 with the walls and the ceiling as well (per user: a goblin attacking at a
            // wall had an arm in it).
            UWOwnTile.Enable(lOMaterial);

            // A BIG OBJECT'S RADIUS, as the original's object sorting reads it (per user,
            // 2026-09-28: the draw order of the original) - see UWOwnTile.BigRadiusProperty.
            if (lOMaterial.HasProperty(UWOwnTile.BigRadiusProperty))
                lOMaterial.SetFloat(UWOwnTile.BigRadiusProperty, liBigRadius);

            mOMaterialCache[liMaterialKey] = lOMaterial;
        }

        mOFilter = GetComponent<MeshFilter>();
        mORenderer = GetComponent<MeshRenderer>();

        if (mOFilter == null || mORenderer == null)
            return;

        mORenderer.sharedMaterial = lOMaterial;

        // The collider so far comes from the static object sprite (usually 16x16) and is
        // far too small for a creature - a goblin is 68x58. It follows the largest frame of
        // the animation so it does not jump on frame changes, and is square in plan so the
        // creature can be hit from any direction.
        BoxCollider lOCollider = GetComponent<BoxCollider>();

        if (lOCollider != null && mOAtlas.MaxSize.x > 0 && mOAtlas.MaxSize.y > 0)
        {
            lOCollider.size = new Vector3(mOAtlas.MaxSize.x, mOAtlas.MaxSize.y, mOAtlas.MaxSize.x);
            lOCollider.center = new Vector3(0f, mOAtlas.MaxSize.y * 0.5f, 0f);
        }

        // THE CREATURE'S OWN ANIMATION WINS over a standing picture: the record carries the
        // animation and frame a save was written in, and the original shows exactly that from
        // the first frame. This component is added AFTER UWCritter (UWObjectSpawner), so the
        // creature could not hand it over earlier - it is fetched here.
        if (miAnimation < 0 && mOCritter != null)
        {
            int liStoredAnimation;
            int liStoredFrame;

            if (mOCritter.TryGetStoredPicture(out liStoredAnimation, out liStoredFrame))
            {
                miAnimation = liStoredAnimation;
                miFrame = liStoredFrame;
            }
        }

        if (miAnimation >= 0)
            fApply();

        // Otherwise the creature stands until the brain's first Motion.
        if (miCurrentSlot < 0)
            fSetSlot(UWCritterAnimations.SlotIdleFirstDirection + fGetViewDirection(), 0);

        // THE VOID CREATURES (lightning, eye, skull) have no standing pages - page 0 is
        // empty for them, and without a slot they would stay invisible (per user, 2026-09-14).
        // Their motion lies in the front idle image, which loops for them.
        if (miCurrentSlot < 0)
            fSetSlot(UWCritterAnimations.SlotIdleFirstDirection + FrontViewDirection, 0);

        if (miCurrentSlot < 0)
            fSetSlot(UWCritterAnimations.SlotCombatIdle, 0);
    }

    /// <summary>How many creature atlases have been built so far. The palette renderer
    /// uses it to detect that a KIND has been added and its colour atlas ->
    /// index atlas mapping is stale (see UWPaletteRenderToggle).</summary>
    public static int AtlasCount
    {
        get { return mOAtlasCache.Count; }
    }

    /// <summary>
    /// Enters, for every creature texture built so far, its index version - for the
    /// palette renderer, see UWPaletteRenderToggle.
    /// </summary>
    public static void CollectIndexTextures(System.Collections.Generic.Dictionary<Texture, Texture> pOInto)
    {
        if (pOInto == null)
            return;

        foreach (UWCritterAtlas lOAtlas in mOAtlasCache.Values)
        {
            if (lOAtlas != null && lOAtlas.Texture != null && lOAtlas.IndexTexture != null)
                pOInto[lOAtlas.Texture] = lOAtlas.IndexTexture;
        }
    }

    /// <summary>Clears the shared caches. Needed when the data set changes,
    /// otherwise creatures would show the images of the previous installation.</summary>
    public static void ClearCaches()
    {
        mOAtlasCache.Clear();
        mOMaterialCache.Clear();
    }

    private void Awake()
    {
        mOCritter = GetComponent<UWCritter>();
        mODamageable = GetComponent<UWDamageable>();
    }

    /// <summary>The brain's Motion: this slot at this frame until the next update
    /// (deviation 20). The direction pages are chosen in Update by the view.</summary>
    public void ShowMotion(int piAnimation, int piFrame)
    {
        miAnimation = piAnimation;
        miFrame = piFrame;

        fApply();
    }

    /// <summary>The brain reached frame 3 of the death animation (UWCritter.OnDeathFinished).</summary>
    public void MarkDeathFinished()
    {
        DeathFinished = true;
    }

    private void Update()
    {
        if (mOAtlas == null || mOFilter == null)
            return;

        // A creature without a brain (the void creatures) loops the pictures of its slot on
        // the standing cadence; everything else shows exactly what the brain said.
        if (mOCritter == null)
        {
            fLoopWithoutBrain();

            return;
        }

        fApply();
    }

    private void fLoopWithoutBrain()
    {
        if (UWCharacter.TimeIsFrozen || mOCurrentFrames == null || mOCurrentFrames.Length == 0
            || Time.time < mfNextVoidFrameTime)
            return;

        mfNextVoidFrameTime = Time.time + VoidFrameSeconds;

        // Without a brain a dead creature still shows its death pictures once.
        if (mODamageable != null && mODamageable.IsDestroyed && miCurrentSlot != UWCritterAnimations.SlotDeath)
        {
            fSetSlot(UWCritterAnimations.SlotDeath, 0);

            return;
        }

        if (miCurrentSlot == UWCritterAnimations.SlotDeath && miFrameInSegment >= mOCurrentFrames.Length - 1)
        {
            DeathFinished = true;

            return;
        }

        miFrameInSegment = (miFrameInSegment + 1) % mOCurrentFrames.Length;
        fShowFrame(mOCurrentFrames[miFrameInSegment]);
    }

    /// <summary>Direction 4, i.e. slot 0x24, is "facing the viewer" - see
    /// UWCritterAnimations.SlotIdleFirstDirection and fGetViewDirection.</summary>
    private const int FrontViewDirection = 4;

    /// <summary>The atlas slot and frame for the brain's animation and the current view.</summary>
    private void fApply()
    {
        if (mOAtlas == null || mOFilter == null)
            return;

        if (miAnimation < 0)
        {
            // UNTIL THE BRAIN'S FIRST MOTION the creature shows its standing picture, and that
            // picture has to follow the facing. The atlas is built before UWCritter.Initialise
            // has made the record, so the first choice was made with facing 0 - north - and
            // nothing corrected it while the brain had not spoken yet (per user, 2026-09-20: a
            // freshly loaded reaper looked north where the original has it face the player).
            int liStanding = UWCritterAnimations.SlotIdleFirstDirection + fGetViewDirection();

            if (liStanding != miCurrentSlot)
                fSetSlot(liStanding, 0);

            return;
        }

        int liWanted = fGetAtlasSlot(miAnimation);

        // THE COMBAT FRAMES EXIST ONLY FROM THE FRONT. They lie on page 0 and relate to the
        // viewer, not to a compass direction. Seen from any other side the creature shows the
        // idle image for that direction - first seen with time standing still (per user on the
        // original, 2026-09-08), when a creature fighting the player stays put and one walks
        // round it. It holds ALWAYS: a creature fighting ANOTHER creature does not face the
        // player, and ours showed its combat front to him from behind (per user, 2026-09-27: a
        // summoned mage fighting a reaper "looks at me all the time" - "not so in the
        // original"; from the side or behind he sees its side or back). When the player stands
        // in front of it again, the combat frame is back. Dying is shown from every side.
        if (liWanted < UWCritterAnimations.SlotIdleFirstDirection && liWanted != UWCritterAnimations.SlotDeath)
        {
            int liView = fGetViewDirection();

            if (liView != FrontViewDirection)
                liWanted = UWCritterAnimations.SlotIdleFirstDirection + liView;
        }

        if (liWanted != miCurrentSlot)
        {
            // A SLOT THE CREATURE DOES NOT HAVE MUST NOT LEAVE THE OLD PICTURE STANDING when
            // that picture points somewhere else entirely. A reaper has no walking and no
            // back-off pages, and the original stores exactly that state next to the player
            // (save written by the original, 2026-09-20: goal 5, animation 0x07, facing 6,
            // moving heading east, speed 2) while showing a picture that looks at the player.
            // Our fallback "keep the current frames" then kept the standing picture of the
            // compass direction, so the reaper looked west past the player (per user).
            if (!fSetSlot(liWanted, miFrame) && fIsTowardsTarget(miAnimation))
                fSetSlot(UWCritterAnimations.SlotCombatIdle, miFrame);

            return;
        }

        fShowFrameOfSegment(miFrame);
    }

    /// <summary>The original's slot numbers onto the atlas pages: 0x20 and 0x2C are the
    /// direction sets, 0x07 the walking pictures facing the target, the rest as they are.</summary>
    private int fGetAtlasSlot(int piAnimation)
    {
        switch (piAnimation)
        {
            case UWCritterBrain.AnimStanding:
                return UWCritterAnimations.SlotIdleFirstDirection + fGetViewDirection();

            case UWCritterBrain.AnimWalking:
                return UWCritterAnimations.SlotWalkFirstDirection + fGetViewDirection();

            case UWCritterBrain.AnimBackOff:
                return UWCritterAnimations.SlotWalkTowardsPlayer;

            default:
                return piAnimation;
        }
    }

    /// <summary>
    /// Which of the eight direction images the viewer sees. Slot 0x20 shows the
    /// figure from behind, 0x24 from the front - so the index depends on how the creature stands
    /// relative to the camera, not on where it looks in the world.
    /// </summary>
    private int fGetViewDirection()
    {
        // THE VIEWPOINT, not the player: during a detached camera the
        // creature must show the side that matches THAT view (see UWViewpoint.Current).
        Camera lOCamera = UWViewpoint.Current;

        if (lOCamera == null || mOCritter == null)
            return FrontViewDirection;

        Vector3 lOToCamera = lOCamera.transform.position - transform.position;
        lOToCamera.y = 0f;

        if (lOToCamera.sqrMagnitude <= 0.0001f)
            return FrontViewDirection;

        float lfToCamera = Mathf.Atan2(lOToCamera.x, lOToCamera.z) * Mathf.Rad2Deg;
        float lfRelative = Mathf.DeltaAngle(lfToCamera, mOCritter.FacingDegrees);

        // 0 degrees means "looks at the camera" and should map to 4, 180 degrees to 0.
        int liIndex = Mathf.RoundToInt((lfRelative + 180f) / 45f) % UWCritterAnimations.DirectionCount;

        return liIndex < 0 ? liIndex + UWCritterAnimations.DirectionCount : liIndex;
    }

    /// <summary>
    /// Do this animation's pictures face the TARGET rather than a compass direction? The
    /// combat pages 0 to 3 and the back-off page 7 do (deviation 21); standing and walking do
    /// not. A creature without pictures for one of these falls back to the combat page, which
    /// every fighting creature has, instead of keeping a stale compass picture.
    /// </summary>
    private static bool fIsTowardsTarget(int piAnimation)
    {
        return piAnimation == UWCritterBrain.AnimBackOff
            || (piAnimation >= UWCritterAnimations.SlotCombatIdle
                && piAnimation <= UWCritterAnimations.SlotAttackThrust);
    }

    /// <summary>Puts the pictures of this slot on the creature; false when it has none.</summary>
    private bool fSetSlot(int piSlot, int piFrame)
    {
        int[] lOFrames = mOAtlas.GetFramesForSlot(piSlot);

        // Unused slots do occur - a creature without frames for one then keeps
        // its current ones instead of vanishing.
        if (lOFrames == null || lOFrames.Length == 0)
            return false;

        miCurrentSlot = piSlot;
        mOCurrentFrames = lOFrames;
        miFrameInSegment = -1;

        fShowFrameOfSegment(piFrame);

        return true;
    }

    /// <summary>The brain's frame within the current segment. A segment shorter than the
    /// brain's count loops (the walk's 0..3 over fewer pictures), the death pictures stay on
    /// their last one.</summary>
    private void fShowFrameOfSegment(int piFrame)
    {
        if (mOCurrentFrames == null || mOCurrentFrames.Length == 0)
            return;

        int liLength = mOCurrentFrames.Length;
        int liFrame = miCurrentSlot == UWCritterAnimations.SlotDeath
            ? Mathf.Min(Mathf.Max(0, piFrame), liLength - 1)
            : ((piFrame % liLength) + liLength) % liLength;

        if (liFrame == miFrameInSegment)
            return;

        miFrameInSegment = liFrame;
        fShowFrame(mOCurrentFrames[liFrame]);
    }

    private void fShowFrame(int piFrame)
    {
        if (piFrame < 0 || mOAtlas.Sizes == null || piFrame >= mOAtlas.Sizes.Length)
            return;

        Mesh lOMesh;

        if (!mOMeshCache.TryGetValue(piFrame, out lOMesh))
        {
            lOMesh = fBuildMesh(piFrame);
            mOMeshCache[piFrame] = lOMesh;
        }

        if (lOMesh != null)
            mOFilter.sharedMesh = lOMesh;
    }

    /// <summary>
    /// A quad at original size. Horizontally it hangs from the image's hotspot, which lies
    /// at the feet - so the figure stays at the same spot on frame changes instead of
    /// jumping, even though the frames differ in size. Vertically it hangs from the hotspot
    /// as well: the rows below it lie under the creature's ground point - for the lurker the
    /// part below the waterline, which is why only the head and the humps look out of the
    /// water (comparison shot from the original, per user, 2026-09-16). Scaled taller than one
    /// unit per pixel around the ground point (UWSettings.CritterSpriteHeightScale; Drog
    /// looked squashed next to the original, per user with screenshots, 2026-09-17).
    /// </summary>
    private Mesh fBuildMesh(int piFrame)
    {
        Vector2Int lOSize = mOAtlas.Sizes[piFrame];

        if (lOSize.x <= 0 || lOSize.y <= 0)
            return null;

        Vector2Int lOHotspot = mOAtlas.Hotspots[piFrame];
        Rect lOUv = mOAtlas.UvRects[piFrame];

        float lfLeft = -lOHotspot.x;
        float lfRight = lOSize.x - lOHotspot.x;

        UWSettings lOSettings = UWSettings.Instance;
        float lfHeightScale = lOSettings != null ? lOSettings.CritterSpriteHeightScale : 1f;

        float lfBottom = -(lOSize.y - lOHotspot.y) * lfHeightScale;
        float lfTop = lOHotspot.y * lfHeightScale;

        Mesh lOMesh = new Mesh();
        lOMesh.name = "UWCreatureFrame " + piFrame;

        lOMesh.vertices = new Vector3[]
        {
            new Vector3(lfLeft, lfBottom, 0f),
            new Vector3(lfRight, lfBottom, 0f),
            new Vector3(lfRight, lfTop, 0f),
            new Vector3(lfLeft, lfTop, 0f)
        };

        lOMesh.uv = new Vector2[]
        {
            new Vector2(lOUv.xMin, lOUv.yMin),
            new Vector2(lOUv.xMax, lOUv.yMin),
            new Vector2(lOUv.xMax, lOUv.yMax),
            new Vector2(lOUv.xMin, lOUv.yMax)
        };

        lOMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        lOMesh.normals = new Vector3[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        lOMesh.RecalculateBounds();

        return lOMesh;
    }

    private static FilterMode fGetFilterMode()
    {
        return UWSettings.Instance != null ? UWSettings.Instance.TextureFilterMode : FilterMode.Point;
    }
}
