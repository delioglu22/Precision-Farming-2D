using System.IO;
using UnityEngine;

// Save data is plain fields that JsonUtility can write. Fields are matched by FarmField.Id,
// never by object identity, so a save survives scene edits that keep those ids.

/// <summary>Read first to learn which layout the rest of the file uses.</summary>
[System.Serializable]
public class SaveHeader
{
    public int version;
}

/// <summary>Version 3: soil, explicit Start/Repeat, research, soil controllers and paid-crop snapshots.</summary>
[System.Serializable]
public class FarmSaveV3
{
    public int version;
    public int coins;
    public int introStep;
    public bool goalReached;
    public string[] research;
    public int installCredits;
    public bool creditGranted;
    public int automaticReturns;
    public FieldSaveV3[] fields;
}

[System.Serializable]
public class FieldSaveV3
{
    public string id;
    public bool owned;
    public int use;
    public int assignedSet;
    public int fertility;
    public FarmPlan cashPlan;
    public int manualCrop;
    public bool repeat;
    public int status;
    public int pause;
    public bool pendingClear;
    public bool controllerInstalled;
    public bool controllerEnabled;
    public bool controllerRecovering;
    public bool hasActive;
    public CropForecast active;
    public float elapsed;
    public bool hasLast;
    public CropForecast last;
    public int cashCompleted;
    public int recoveryCompleted;
    public int manualRecoveryCompleted;
}

/// <summary>
/// Versions 1 and 2, read as they were written. Their plans stored enum positions
/// (density Low/Standard/High, care Off/Moderate/High as 0/1/2), which must be translated,
/// never read as percentages.
/// </summary>
[System.Serializable]
public class LegacySave
{
    public int version;
    public int coins;
    public int introStep;
    public bool goalReached;
    public LegacyField[] fields;
}

[System.Serializable]
public class LegacyField
{
    public string id;
    public bool owned;
    public bool farming;
    public int use;
    public int assignedSet;
    public bool pendingStop;
    public LegacyPlan plan;
    public bool running;
    public float cycleTime;
    public LegacyPlan activePlan;
    public bool hasResult;
    public LegacyResult lastResult;
    public int cropsSold;
}

[System.Serializable]
public struct LegacyPlan
{
    public int density;
    public int fertilizer;
    public int watering;

    private static readonly int[] DensityPercent = { 25, 50, 100 };
    private static readonly int[] CarePercent = { 0, 50, 100 };

    public bool IsValid
    {
        get { return InRange(density) && InRange(fertilizer) && InRange(watering); }
    }

    private static bool InRange(int v) { return v >= 0 && v <= 2; }

    public FarmPlan ToPercent()
    {
        return new FarmPlan(DensityPercent[density], CarePercent[fertilizer], CarePercent[watering]);
    }
}

[System.Serializable]
public struct LegacyResult
{
    public int income;
    public int seedCost;
    public int fertilizerCost;
    public int waterCost;
}

/// <summary>
/// Reads and writes the save file. A file that exists but cannot be understood is never
/// overwritten: the caller is told, and only an explicit "new farm" moves it aside.
/// </summary>
public static class FarmSaveFile
{
    public const int Version = 3;

    public enum Outcome { Loaded, Legacy, Missing, Invalid }

    public static string PathFor(string fileName)
    {
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    /// <summary>
    /// Loads the file into either a version 3 save or a legacy one. Structural checks happen
    /// here; checks that need the scene (known ids, set numbers) are done by Farm.
    /// </summary>
    public static Outcome TryLoad(string fileName, out FarmSaveV3 current, out LegacySave legacy, out string problem)
    {
        current = null;
        legacy = null;
        problem = null;
        string path = PathFor(fileName);
        if (!File.Exists(path)) return Outcome.Missing;

        string json;
        SaveHeader header;
        try
        {
            json = File.ReadAllText(path);
            header = JsonUtility.FromJson<SaveHeader>(json);
        }
        catch (System.Exception)
        {
            problem = "The save file is damaged.";
            return Outcome.Invalid;
        }
        if (header == null || header.version < 1) { problem = "The save file is empty or damaged."; return Outcome.Invalid; }
        if (header.version > Version) { problem = "The save was made by a newer version of the game."; return Outcome.Invalid; }

        try
        {
            if (header.version == Version)
            {
                current = JsonUtility.FromJson<FarmSaveV3>(json);
                if (current == null || current.fields == null) { problem = "The save file is empty or damaged."; return Outcome.Invalid; }
                return Outcome.Loaded;
            }
            legacy = JsonUtility.FromJson<LegacySave>(json);
            if (legacy == null || legacy.fields == null) { problem = "The save file is empty or damaged."; return Outcome.Invalid; }
            return Outcome.Legacy;
        }
        catch (System.Exception)
        {
            problem = "The save file is damaged.";
            return Outcome.Invalid;
        }
    }

    /// <summary>Keeps an untouched copy of an older save before it is ever rewritten.</summary>
    public static string KeepMigrationBackup(string fileName, int oldVersion)
    {
        string path = PathFor(fileName);
        string backup = path + ".v" + oldVersion + "-backup";
        if (File.Exists(path) && !File.Exists(backup)) File.Copy(path, backup);
        return backup;
    }

    /// <summary>
    /// Writes to a temporary file, then swaps it in with File.Replace, which keeps the previous
    /// save as "farm.json.bak". If writing fails part-way, the old save is still there.
    /// </summary>
    public static void Write(string fileName, FarmSaveV3 data)
    {
        string path = PathFor(fileName);
        string temp = path + ".tmp";
        string backup = path + ".bak";
        File.WriteAllText(temp, JsonUtility.ToJson(data, true));
        if (!File.Exists(path))
        {
            File.Move(temp, path);
            return;
        }
        try
        {
            File.Replace(temp, path, backup);
        }
        catch (System.Exception)
        {
            // Fallback for file systems without Replace: keep a copy first, then overwrite.
            File.Copy(path, backup, true);
            File.Copy(temp, path, true);
            File.Delete(temp);
        }
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
