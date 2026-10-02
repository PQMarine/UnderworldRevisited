using UnityEngine;
using UnderworldRevisited;
using UnderworldRevisited.Build;

/// <summary>
/// A being of the Ethereal Void (level 9): skull, lightning and eye (0x4F, 0x7D, 0x7E).
/// Without vitality in the creature table, with goal 0xB in the level data.
///
/// The reference npcai.cs, goal 0xB: on every pass speed 4, a random direction, a
/// slight pitch and the next image. The images are advanced by UWCritterAnimator; here
/// only the drifting around. The reference does not limit how far it moves away from its
/// starting point - with a direction re-rolled every round it stays nearby anyway.
/// Here the distance is limited nonetheless (UWSettings.VoidCreatureDriftRadius), so that none
/// drifts into a wall. Speed and interval are judged by eye.
/// </summary>
public class UWVoidCreature : MonoBehaviour
{
    /// <summary>Goal 0xB in the level data.</summary>
    public const int VoidGoal = 0xB;

    private Vector3 mOHome;

    private Vector3 mODirection;

    private float mfNextTurn;

    private void Start()
    {
        mOHome = transform.position;
        fPickDirection();
    }

    private void Update()
    {
        if (UWCharacter.TimeIsFrozen)
            return;

        UWSettings lOSettings = UWSettings.Instance;
        float lfSpeed = lOSettings != null ? lOSettings.VoidCreatureDriftSpeed : 8f;
        float lfRadius = (lOSettings != null ? lOSettings.VoidCreatureDriftRadius : 0.5f) * UWLevelMeshBuilder.TileSpacing;
        float lfTurnSeconds = lOSettings != null ? lOSettings.VoidCreatureTurnSeconds : 0.5f;

        if (Time.time >= mfNextTurn)
        {
            mfNextTurn = Time.time + lfTurnSeconds;
            fPickDirection();
        }

        Vector3 lOPosition = transform.position + (mODirection * lfSpeed * Time.deltaTime);
        Vector3 lOOffset = lOPosition - mOHome;

        // At the edge, head back towards the centre instead of sticking there.
        if (lOOffset.magnitude > lfRadius)
        {
            lOPosition = mOHome + (lOOffset.normalized * lfRadius);
            mODirection = -lOOffset.normalized;
        }

        transform.position = lOPosition;
    }

    /// <summary>Any horizontal direction, plus a little up or down (the reference
    /// rolls the pitch 0xF to 0x11).</summary>
    private void fPickDirection()
    {
        float lfAngle = Random.Range(0f, Mathf.PI * 2f);

        mODirection = new Vector3(Mathf.Cos(lfAngle), Random.Range(-0.15f, 0.15f), Mathf.Sin(lfAngle)).normalized;
    }
}
