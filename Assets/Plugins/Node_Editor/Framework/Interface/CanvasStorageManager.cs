using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Example canvases bundled with the app, listed under File > Load Example. Entries are
/// Resources paths (e.g. "CanvasSaves/MIDIMixAssignedFluidMinis"), not canvas references:
/// a scene component referencing canvas assets loads every one of them with the scene, and
/// in a built player that starts all their nodes. By path, a canvas is only loaded when it's
/// picked. In the inspector, drop a canvas asset (from any Resources folder) onto an entry
/// to record its path.
/// </summary>
public class CanvasStorageManager : MonoBehaviour
{
    [Tooltip("Resources paths of canvases offered under File > Load Example. Drop a canvas asset from a Resources folder onto an entry.")]
    [CanvasResourcePath]
    public List<string> exampleCanvases = new List<string>();

    /// <summary>Example entries from the scene's CanvasStorageManager; empty if there's none.</summary>
    public static List<string> Examples
    {
        get
        {
            var manager = FindFirstObjectByType<CanvasStorageManager>();
            return manager != null
                ? manager.exampleCanvases.Where(p => !string.IsNullOrWhiteSpace(p)).ToList()
                : new List<string>();
        }
    }

    public static string DisplayName(string resourcePath) =>
        resourcePath.Substring(resourcePath.LastIndexOf('/') + 1);
}

/// <summary>Marks a string as a NodeCanvas Resources path (drawn with a canvas drop field).</summary>
public class CanvasResourcePathAttribute : PropertyAttribute { }
