using UnityEngine;

/// <summary>
/// Announces which parcel the player picked. This is an asset rather than a
/// scene object on purpose: the map scene and the UI scene both point at it, and
/// Unity cannot serialise a reference that crosses from one scene into another.
/// </summary>
[CreateAssetMenu(fileName = "ParcelSelection", menuName = "Precision Farming/Parcel Selection Channel")]
public class ParcelSelectionChannel : ScriptableObject
{
    /// <summary>Raised on every pick. A null parcel means the player deselected.</summary>
    public event System.Action<Parcel> Selected;

    void OnEnable()
    {
        // The asset outlives a play session in the editor, and so would a
        // subscriber from the last one - which by then points at a destroyed
        // object. Every run starts with nobody listening.
        Selected = null;
    }

    public void Raise(Parcel parcel)
    {
        if (Selected != null) Selected(parcel);
    }
}
