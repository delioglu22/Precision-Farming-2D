using System.IO;
using UnityEngine;

/// <summary>
/// Everything a farm needs to resume, as plain data that JsonUtility can write. Fields are
/// matched by their <see cref="FarmField.Id"/>, never by object identity, so a save survives
/// scene edits that do not rename those ids.
/// </summary>
[System.Serializable]
public class FarmSaveData
{
    public int version;
    public int coins;
    public int introStep;
    public bool goalReached;
    public FieldSaveData[] fields;
}

[System.Serializable]
public class FieldSaveData
{
    public string id;
    public bool owned;
    public bool farming;        // version 1 only knew this; version 2 keeps it for readability
    public int use;             // FarmField.Use, version 2
    public int assignedSet;     // version 2
    public bool pendingStop;    // version 2
    public FarmPlan plan;
    public bool running;
    public float cycleTime;
    public FarmPlan activePlan;
    public bool hasResult;
    public HarvestResult lastResult;
    public int cropsSold;
}

/// <summary>
/// Reads and writes the save file. A file that exists but cannot be understood is never
/// overwritten here: the caller is told, and only an explicit "new farm" moves it aside.
/// </summary>
public static class FarmSaveFile
{
    // 1: introduction farm (coins, plans, crops). 2: land use, equipment sets, the depot and
    // pending stops. A version 1 save is upgraded when read; sets are then handed out again.
    public const int Version = 2;

    public enum Outcome { Loaded, Missing, Invalid }

    public static string PathFor(string fileName)
    {
        return System.IO.Path.Combine(Application.persistentDataPath, fileName);
    }

    public static Outcome TryLoad(string fileName, out FarmSaveData data, out string problem)
    {
        data = null;
        problem = null;
        string path = PathFor(fileName);
        if (!File.Exists(path)) return Outcome.Missing;

        try
        {
            data = JsonUtility.FromJson<FarmSaveData>(File.ReadAllText(path));
        }
        catch (System.Exception)
        {
            problem = "The save file is damaged.";
            return Outcome.Invalid;
        }

        if (data == null || data.fields == null) problem = "The save file is empty or damaged.";
        else if (data.version < 1 || data.version > Version) problem = "The save was made by a newer version of the game.";
        if (problem != null) { data = null; return Outcome.Invalid; }

        if (data.version == 1)
        {
            foreach (FieldSaveData f in data.fields)
            {
                f.use = f.owned && f.farming ? 1 : 0;
                f.assignedSet = 0;
                f.pendingStop = false;
            }
            data.version = Version;
        }
        return Outcome.Loaded;
    }

    /// <summary>
    /// Writes to a temporary file first and then swaps it in, so a crash mid-write leaves
    /// the previous save intact rather than half a file.
    /// </summary>
    public static void Write(string fileName, FarmSaveData data)
    {
        string path = PathFor(fileName);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonUtility.ToJson(data, true));
        if (File.Exists(path)) File.Delete(path);
        File.Move(temp, path);
    }

    /// <summary>Keeps an unreadable save under a new name so nothing is silently lost.</summary>
    public static string SetAside(string fileName)
    {
        string path = PathFor(fileName);
        if (!File.Exists(path)) return null;
        string kept = path + ".invalid-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Move(path, kept);
        return kept;
    }
}
