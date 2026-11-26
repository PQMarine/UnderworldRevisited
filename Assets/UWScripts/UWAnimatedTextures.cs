using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnderworldRevisited.Build;

/// <summary>
/// Animates lava and water on walls and floors.
///
/// The original does not animate textures for this, but the palette itself: certain
/// colour ranges cycle through palette 0, and every texture that uses these colours moves
/// along with them (uw-formats.txt 3.1.4, see UWPaletteRotation). Because our textures
/// sit in the Texture2DArray with final colours, the same effect is achieved here like this: the
/// affected textures are prepared in all rotation steps
/// (UWTextureArrayBuilder.BuildAnimatedSlices), and per step only the matching
/// image is copied into the slice.
///
/// The number of steps per second is not stored anywhere in the data - see UWSettings.
/// </summary>
public class UWAnimatedTextures : MonoBehaviour
{
    private Texture2DArray mOArray;
    private List<UWTextureArrayBuilder.AnimatedSlice> mOSlices;
    private float mfStepsPerSecond;
    private int miStep = -1;
    private bool mbCanCopyOnGpu;

    /// <summary>
    /// False on the palette path (UWPaletteRenderPath.fSetSideEffects): it rotates the colours in the shader and never
    /// reads the colour array, but its Palette effects read the same normal array, whose water still has to move
    /// (2026-11-26) - so the animator stays on there and copies only the relief.
    /// </summary>
    public bool CopiesColours = true;

    public void Initialise(Texture2DArray pOArray, List<UWTextureArrayBuilder.AnimatedSlice> pOSlices, float pfStepsPerSecond)
    {
        mOArray = pOArray;
        mOSlices = pOSlices;
        mfStepsPerSecond = pfStepsPerSecond;

        // Copying a Texture2D into a slice of a Texture2DArray counts as a copy between
        // different texture types. Where the graphics card cannot do that, it takes the
        // slower route via SetPixels32 and Apply.
        mbCanCopyOnGpu = (SystemInfo.copyTextureSupport & CopyTextureSupport.DifferentTypes) != 0;

        miStep = -1;
    }

    private void Update()
    {
        if (mOArray == null || mOSlices == null || mOSlices.Count == 0 || mfStepsPerSecond <= 0f)
            return;

        // The palette rotation's clock, shared with the palette path (UWFlicker.RotationStep).
        int liStep = UWDataImport.UWData.UWFlicker.RotationStep(Time.time, mfStepsPerSecond);

        if (liStep == miStep)
            return;

        miStep = liStep;

        // Without mipmap recalculation (in fCopyStates): the rotation only swaps colours within a narrow range, the
        // difference is not visible at a distance, and an Apply with mipmaps over the whole array per step would be
        // disproportionately expensive.
        if (CopiesColours)
            fCopyStates(mOArray, mOSlices, liStep);

        fCopyStates(UWTextureCache.CurrentNormalArray, UWTextureCache.CurrentAnimatedNormals, liStep);
    }

    /// <summary>
    /// THE WATER'S RELIEF AT THE SAME STEP (per user 2026-11-26: its shadows stood still while the colours flowed): the
    /// normal array in force (UWTextureCache, shared by Remastered and the Palette effects) takes the step's normal map of
    /// each moving water slice, as the colours above.
    /// </summary>
    private void fCopyStates(Texture2DArray pOArray, List<UWTextureArrayBuilder.AnimatedSlice> pOSlices, int piStep)
    {
        if (pOArray == null || pOSlices == null || pOSlices.Count == 0)
            return;

        bool lbNeedsApply = false;

        foreach (UWTextureArrayBuilder.AnimatedSlice lOSlice in pOSlices)
        {
            if (lOSlice.States == null || lOSlice.States.Length == 0 || lOSlice.Slice >= pOArray.depth)
                continue;

            Texture2D lOState = lOSlice.States[((piStep % lOSlice.States.Length) + lOSlice.States.Length) % lOSlice.States.Length];

            if (lOState == null)
                continue;

            if (mbCanCopyOnGpu)
            {
                Graphics.CopyTexture(lOState, 0, pOArray, lOSlice.Slice);
                continue;
            }

            pOArray.SetPixels32(lOState.GetPixels32(), lOSlice.Slice);
            lbNeedsApply = true;
        }

        if (lbNeedsApply)
            pOArray.Apply(false, false);
    }
}
