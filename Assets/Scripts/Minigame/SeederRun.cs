using UnityEngine;

/// <summary>
/// The ticket for one run of the seeder's mini game: where the machine is being sent, and
/// what it managed once it got there.
///
/// An asset rather than a scene object because the mini game is its own scene, and Unity
/// cannot serialise a reference that crosses from one scene into another - the same reason
/// ParcelSelectionChannel is an asset.
///
/// It carries the parcel's measurements rather than the Parcel itself, and that is the whole
/// point of the separation: a Parcel lives in the map scene, so depending on one would drag
/// the map back in and the mini game could only ever be played through it. With nothing but
/// data here, Seeder.unity opens and plays on its own off whatever is left in this asset.
/// </summary>
[CreateAssetMenu(fileName = "SeederRun", menuName = "Precision Farming/Seeder Run")]
public class SeederRun : ScriptableObject
{
    [Tooltip("The parcel's size in cells. What the mini game lays out as ground.")]
    [SerializeField] Vector2Int footprint = new Vector2Int(5, 7);

    [Tooltip("Whose field is being worked. Shown as the run's title.")]
    [SerializeField] string parcelName = "Parcel";

    /// <summary>Raised when a run ends, carrying the share of the parcel that got seed.</summary>
    public event System.Action<float> Finished;

    public Vector2Int Footprint { get { return footprint; } }

    public string ParcelName { get { return parcelName; } }

    /// <summary>The share of the parcel the last run covered, from 0 to 1.</summary>
    public float Coverage { get; private set; }

    void OnEnable()
    {
        // The asset outlives a play session in the editor, and so would a subscriber from
        // the last one - which by then points at a destroyed object.
        Finished = null;
    }

    /// <summary>
    /// Sends the machine to a parcel. Called from the map side before the scene is loaded,
    /// so that what the mini game finds waiting for it is a footprint and a name.
    /// </summary>
    public void Send(Vector2Int cells, string field)
    {
        footprint = cells;
        parcelName = field;
    }

    /// <summary>Reports what the run managed, on the way back out to the map.</summary>
    public void Report(float coverage)
    {
        Coverage = Mathf.Clamp01(coverage);
        if (Finished != null) Finished(Coverage);
    }
}
