using System.IO;
using UnityEngine;

public static class FolderUtil
{
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
