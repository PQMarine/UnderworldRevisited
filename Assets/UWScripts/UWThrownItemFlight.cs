using UnityEngine;

/// <summary>
/// Original: a thrown item visibly flies through the air instead of appearing immediately at
/// its target position - a very simple sine arc animation between
/// start (player position) and the target already computed by UWLevelLoader.SpawnDroppedObject
/// (flat on the floor), no real physics/Rigidbody system. Attaches itself to the
/// already spawned object (see UWLevelLoader.SpawnDroppedObject), sets the exact target
/// position at the end and removes itself - afterwards the object remains a
/// normal, resting world object.
/// </summary>
public class UWThrownItemFlight : MonoBehaviour
{
    private Vector3 mOStart;
    private Vector3 mOEnd;
    private float mfDuration;
    private float mfArcHeight;
    private float mfElapsed;

    /// <summary>If the throw lands in water, the item is gone - then it vanishes
    /// on impact instead of staying there (per user, 2026-08-31).</summary>
    private bool mbDestroyOnArrival;

    /// <summary>What happens at the MOMENT OF LANDING, with the landing point: the splash of
    /// something that falls into water (UWLevelLoader.fSplashAt). It belongs here and not on
    /// the throw, because in the original the picture appears where the object comes to rest
    /// (ObjectHitsFloorTileDestroyTalismans_seg029_C6F) - and it comes whether the object then
    /// sinks or stays afloat.</summary>
    private System.Action<Vector3> mFOnArrived;

    /// <summary>How long the item stays in place before falling. In the original an
    /// impacted bolt first hangs briefly in the air before it falls
    /// (per user, 2026-09-07).</summary>
    private float mfDelay;

    public void Begin(Vector3 pOStart, Vector3 pOEnd, float pfDuration, float pfArcHeight,
        bool pbDestroyOnArrival = false, float pfDelay = 0f,
        System.Action<Vector3> pFOnArrived = null)
    {
        mbDestroyOnArrival = pbDestroyOnArrival;
        mFOnArrived = pFOnArrived;
        mfDelay = pfDelay;
        mOStart = pOStart;
        mOEnd = pOEnd;
        mfDuration = Mathf.Max(0.01f, pfDuration);
        mfArcHeight = pfArcHeight;
        mfElapsed = 0f;

        transform.position = mOStart;
    }

    private void Update()
    {
        if (mfDelay > 0f)
        {
            mfDelay -= Time.deltaTime;

            return;
        }

        mfElapsed += Time.deltaTime;
        float lfT = Mathf.Clamp01(mfElapsed / mfDuration);

        Vector3 lOFlatPos = Vector3.Lerp(mOStart, mOEnd, lfT);
        float lfArcOffset = Mathf.Sin(lfT * Mathf.PI) * mfArcHeight;

        transform.position = lOFlatPos + new Vector3(0f, lfArcOffset, 0f);

        if (lfT >= 1f)
        {
            transform.position = mOEnd;

            if (mFOnArrived != null)
                mFOnArrived(transform.position);

            if (mbDestroyOnArrival)
                Destroy(gameObject);
            else
                Destroy(this);
        }
    }
}
