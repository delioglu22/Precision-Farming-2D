using UnityEngine;

/// <summary>
/// The ticket for one run of the drone's mini game: which parcel it is fertilizing, and what
/// it managed once it got there.
///
/// An asset rather than a scene object for the same reason as <see cref="SeederRun"/>: the
/// mini game is its own scene, so a reference cannot cross into it, only data can.
///
/// Tiles needed and the drone's own stat are placeholders until a parcel carries a real
/// fertility reading (see docs/design.md, "Not decided yet" and "The drone") - once it does,
/// <see cref="Send"/> gains that input and these two fields stop being edited by hand.
/// </summary>
[CreateAssetMenu(fileName = "DroneRun", menuName = "Precision Farming/Drone Run")]
public class DroneRun : ScriptableObject
{
    [Tooltip("The parcel's size in cells. What the mini game lays its grid over.")]
    [SerializeField] private Vector2Int footprint = new Vector2Int(5, 7);

    [Tooltip("Whose field is being worked. Shown as the run's title.")]
    [SerializeField] private string parcelName = "Parcel";

    [Tooltip("Placeholder until fertility exists on Parcel: how many tiles light up this run.")]
    [SerializeField] private int tilesNeeded = 8;

    [Tooltip("Placeholder for the drone's own stat: how many lit tiles it can hold on screen at once.")]
    [SerializeField] private int maxOnScreen = 5;

    /// <summary>Raised when a run ends, carrying the score: correct taps over total taps.</summary>
    public event System.Action<float> Finished;

    public Vector2Int Footprint { get { return footprint; } }

    public string ParcelName { get { return parcelName; } }

    public int TilesNeeded { get { return tilesNeeded; } }

    public int MaxOnScreen { get { return maxOnScreen; } }

    /// <summary>The score the last run ended with, from 0 to 1.</summary>
    public float Score { get; private set; }

    void OnEnable()
    {
        // The asset outlives a play session in the editor, and so would a subscriber from
        // the last one - which by then points at a destroyed object.
        Finished = null;
    }

    /// <summary>
    /// Sends the drone to a parcel. Called from the map side before the scene is loaded, so
    /// that what the mini game finds waiting for it is a footprint and a name.
    /// </summary>
    public void Send(Vector2Int cells, string field)
    {
        footprint = cells;
        parcelName = field;
    }

    /// <summary>Reports what the run scored, on the way back out to the map.</summary>
    public void Report(float score)
    {
        Score = Mathf.Clamp01(score);
        if (Finished != null) Finished(Score);
    }
}
