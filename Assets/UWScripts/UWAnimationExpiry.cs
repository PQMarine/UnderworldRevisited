using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// An animated object of the level whose entry on the animation list has frames left
/// (UWAnimationOverlay.Fate): when they run out, update_animobj's toast_animobj takes it off the
/// map (EFFECT.C, rem_anim_from_map) or, for a class without the remove flag, leaves it standing
/// on its picture. Only a save made while such an effect ran brings one in - a blow's blood
/// (put_effect, length 2) saved within a moment (per user in the original, 2026-10-17: it plays
/// once after loading and goes; ours looped it for ever).
/// </summary>
public class UWAnimationExpiry : MonoBehaviour
{
    private UWLevelLoader mOLevelLoader;

    private UWObject mOObject;

    private float mfEndAt;

    private bool mbGoes;

    public void Initialise(UWLevelLoader pOLevelLoader, UWObject pOObject, float pfSeconds, bool pbGoes)
    {
        mOLevelLoader = pOLevelLoader;
        mOObject = pOObject;
        mfEndAt = Time.time + pfSeconds;
        mbGoes = pbGoes;
    }

    private void Update()
    {
        if (Time.time < mfEndAt)
            return;

        enabled = false;

        if (!mbGoes)
        {
            UWAnimatedBillboard lOAnimated = GetComponent<UWAnimatedBillboard>();

            if (lOAnimated != null)
                lOAnimated.enabled = false;

            if (mOLevelLoader != null && mOLevelLoader.CurrentLevel != null)
                mOLevelLoader.CurrentLevel.Animations.Remove(mOObject);

            return;
        }

        UWDebris.Remove(mOLevelLoader, GetComponent<UWEntityInfo>(), mOObject);
    }
}
