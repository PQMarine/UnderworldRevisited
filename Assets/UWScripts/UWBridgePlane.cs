using UnityEngine;

/// <summary>
/// Takes a bridge's deck plane out of the painter's partition when the bridge's object goes
/// (UWOwnTile.UnregisterBridge): the plate over the Wine of Compassion, level 6 27/50, is removed
/// by a delete trap when it is lifted, and the bottle under it must show again.
/// </summary>
public class UWBridgePlane : MonoBehaviour
{
    private int miTileX;
    private int miTileZ;
    private int miGeneration;

    public void Set(int piTileX, int piTileZ)
    {
        miTileX = piTileX;
        miTileZ = piTileZ;
        miGeneration = UWOwnTile.DoorPlanesGeneration;
    }

    private void OnDestroy()
    {
        UWOwnTile.UnregisterBridge(miTileX, miTileZ, miGeneration);
    }
}
