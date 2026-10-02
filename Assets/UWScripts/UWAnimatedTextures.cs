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

        int liStep = Mathf.FloorToInt(Time.time * mfStepsPerSecond);

        if (liStep == miStep)
            return;

        miStep = liStep;

        bool lbNeedsApply = false;

        for (int i = 0; i < mOSlices.Count; i++)
        {
            UWTextureArrayBuilder.AnimatedSlice lOSlice = mOSlices[i];

            if (lOSlice.States == null || lOSlice.States.Length == 0)
                continue;

            Texture2D lOState = lOSlice.States[((liStep % lOSlice.States.Length) + lOSlice.States.Length) % lOSlice.States.Length];

            if (lOState == null)
                continue;

            if (mbCanCopyOnGpu)
            {
                Graphics.CopyTexture(lOState, 0, mOArray, lOSlice.Slice);
                continue;
            }

            mOArray.SetPixels32(lOState.GetPixels32(), lOSlice.Slice);
            lbNeedsApply = true;
        }

        // Without mipmap recalculation: the rotation only swaps colours within a narrow
        // range, the difference is not visible at a distance, and an Apply with
        // mipmaps over the whole array per step would be disproportionately expensive.
        if (lbNeedsApply)
            mOArray.Apply(false, false);
    }
}
