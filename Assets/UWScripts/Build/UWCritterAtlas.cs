using System.Collections.Generic;
using UnityEngine;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// The images of a creature animation in a single texture, together with the data needed
    /// for drawing.
    ///
    /// A creature is determined by two numbers: the animation number (which images) and
    /// the auxiliary palette (which colours). A grey and a green goblin therefore share
    /// the same images and differ only in the palette - that is why there is one atlas
    /// per combination of both, not per creature type.
    ///
    /// The animation is spread over several files ("pages"), each with its own images and
    /// its own slot list. Here they are merged: the images of all pages follow one another
    /// consecutively, and SlotFrames maps a slot directly to its image sequence.
    /// </summary>
    public class UWCritterAtlas
    {
        public Texture2D Texture;

        /// <summary>The same texture, but with PALETTE INDICES instead of colours - for the
        /// palette renderer. From the same packing run, so the UV rectangles apply to
        /// both (see UWIndexAtlas).</summary>
        public Texture2D IndexTexture;

        /// <summary>Per image number, its position in the atlas.</summary>
        public Rect[] UvRects;

        /// <summary>Per image number, its size in pixels.</summary>
        public Vector2Int[] Sizes;

        /// <summary>Per image number, the anchor point, measured from the top left. It lies at
        /// the feet: for a 35x45 goblin image it is at 16/43, i.e. horizontally
        /// centred and two rows above the bottom edge.</summary>
        public Vector2Int[] Hotspots;

        /// <summary>Per slot, the image sequence, already stripped of the 0xFF padding values.</summary>
        public Dictionary<int, int[]> SlotFrames = new Dictionary<int, int[]>();

        /// <summary>Largest image of the animation. The collision body is sized by it,
        /// so that it does not change size with every frame change.</summary>
        public Vector2Int MaxSize;

        /// <summary>Contains pixels that tint the background instead of
        /// overwriting it (see UWTransparencyTables) - then it has to be drawn with
        /// blending.</summary>
        public bool IsTranslucent;

        /// <summary>The image sequence of a slot, or null if it is unused.</summary>
        public int[] GetFramesForSlot(int piSlot)
        {
            int[] lOFrames;

            return SlotFrames.TryGetValue(piSlot, out lOFrames) ? lOFrames : null;
        }
    }
}
