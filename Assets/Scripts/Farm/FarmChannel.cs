using UnityEngine;

/// <summary>
/// Lets the UI scene find the running <see cref="Farm"/>. Like the parcel selection channel,
/// this is an asset because a UI-scene object cannot hold a reference into the map scene.
/// </summary>
[CreateAssetMenu(fileName = "FarmChannel", menuName = "Precision Farming/Farm Channel")]
public class FarmChannel : ScriptableObject
{
    public Farm Current { get; private set; }

    private void OnEnable()
    {
        // The asset outlives a play session in the editor; start every run empty.
        Current = null;
    }

    public void Register(Farm farm) { Current = farm; }

    public void Unregister(Farm farm)
    {
        if (Current == farm) Current = null;
    }
}
