using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Scene singleton that owns the canopy recordings folder — the capture counterpart to
/// AudioTrackManager. Recordings live as .canrec files in a Recordings/ directory next
/// to the project (or the built player), so nodes serialize only a recording name and
/// files survive canvas reloads, domain reloads, and version control untouched.
/// </summary>
public class RecordingManager : Singleton<RecordingManager>
{
    private string[] cachedNames;

    /// <summary>Recordings/ beside Assets (editor) or beside the player build.</summary>
    public string RecordingsDir =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings"));

    protected override void OnAwake()
    {
        Refresh();
    }

    public void Refresh()
    {
        try
        {
            if (!Directory.Exists(RecordingsDir))
            {
                cachedNames = new string[0];
                return;
            }
            cachedNames = Directory.GetFiles(RecordingsDir, "*" + CanopyRecordingFormat.Extension)
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n)
                .ToArray();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RecordingManager: failed to list recordings: {e.Message}");
            cachedNames = new string[0];
        }
    }

    public string[] RecordingNames
    {
        get
        {
            if (cachedNames == null) Refresh();
            return cachedNames;
        }
    }

    public string PathFor(string name) =>
        Path.Combine(RecordingsDir, name + CanopyRecordingFormat.Extension);

    public bool Exists(string name) => File.Exists(PathFor(name));

    /// <summary>Opens the recordings folder in the OS file browser (Explorer/Finder).</summary>
    public void OpenRecordingsFolder()
    {
        string dir = RecordingsDir;
        try
        {
            Directory.CreateDirectory(dir);
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            System.Diagnostics.Process.Start("open", $"\"{dir}\"");
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            System.Diagnostics.Process.Start("explorer.exe", dir.Replace('/', '\\'));
#else
            Application.OpenURL("file://" + dir);
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RecordingManager: couldn't open '{dir}': {e.Message}");
        }
    }

    public CanopyRecordingFile CreateFile(string name, int width, int height, float nominalFps)
    {
        var file = CanopyRecordingFile.CreateNew(PathFor(name), width, height, nominalFps);
        Refresh();
        return file;
    }

    /// <summary>Null if the file is missing or unreadable (logged, not thrown).</summary>
    public CanopyRecordingFile Open(string name, bool writable)
    {
        string path = PathFor(name);
        if (!File.Exists(path))
        {
            Debug.LogError($"RecordingManager: no recording named '{name}' in {RecordingsDir}");
            return null;
        }
        try
        {
            return CanopyRecordingFile.Open(path, writable);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RecordingManager: failed to open '{name}': {e.Message}");
            return null;
        }
    }
}
