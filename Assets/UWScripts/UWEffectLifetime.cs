using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The picture of an effect - blood, a splash, smoke, a flash (UWLevelLoader.SpawnEffectAt) -
/// holds a static slot of its level while it lives, as the original's put_effect takes one with
/// CreateObj (decision 8 of Docs/CLEANUP-PLAN.md, 2026-10-12). It gives the slot back when the
/// picture goes: its seconds ran out, the level was torn down, the scene was left. Giving it back
/// twice does nothing (UWWorldEdit.ReleaseEffectSlot), so an effect of several nodes may carry one
/// each.
/// </summary>
public class UWEffectLifetime : MonoBehaviour
{
    private UWWorldEdit mOEdit;

    private int miToken;

    /// <summary>The slot this picture holds: the token UWWorldEdit.TryTakeEffectSlot gave.</summary>
    public void Hold(UWWorldEdit pOEdit, int piToken)
    {
        mOEdit = pOEdit;
        miToken = piToken;
    }

    private void OnDestroy()
    {
        if (mOEdit != null)
            mOEdit.ReleaseEffectSlot(miToken);

        mOEdit = null;
    }
}
