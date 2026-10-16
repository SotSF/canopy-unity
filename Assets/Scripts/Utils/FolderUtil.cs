using System.IO;
using UnityEngine;

public static class FolderUtil
{
    /// <summary>
    /// Files/&lt;subfolder&gt; beside the built player (or beside Assets/ in the editor), where
    /// user-supplied runtime content lives next to the Files/NodeEditor/ canvas XML exports.
    /// ImportExportFormat.RuntimeIOPath is CWD-relative; anchoring on dataPath lands in the
    /// same game folder without depending on the launch directory.
    /// </summary>
    public static string GameFilesDir(string subfolder) =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Files", subfolder));

    /// <summary>Opens a folder in the OS file browser (Explorer/Finder), creating it first.</summary>
    public static void Reveal(string dir)
    {
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
            Debug.LogError($"FolderUtil: couldn't open '{dir}': {e.Message}");
        }
    }
}
