using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public enum TextureSource { Project, Disk }

/// <summary>
/// Scene singleton serving named textures to TexturePickerNodes from two sources:
/// Texture2Ds baked into the build under Resources/StaticTextures, and PNG/JPG files
/// dropped into Files/Textures/ beside the built player (or beside Assets/ in the editor),
/// next to the Files/NodeEditor/ canvas XML exports. Both are addressed by name, so canvases serialize
/// only a string. Disk images decode on first request and stay cached for the session;
/// grid thumbnails decode one per frame and are downscaled, so paging through a folder of
/// photos never holds every full-res image in memory.
/// </summary>
public class TextureLibrary : Singleton<TextureLibrary>
{
    public const string BuiltinResourcePath = "StaticTextures";
    const int ThumbnailSize = 128;
    static readonly string[] DiskExtensions = { ".png", ".jpg", ".jpeg" };

    class DiskImage
    {
        public Texture2D texture;
        public DateTime writeTime;
    }

    private Texture2D[] builtins;
    private Dictionary<string, Texture2D> builtinsByName;
    private string[] diskNames = new string[0];
    private readonly Dictionary<string, DiskImage> diskImages = new Dictionary<string, DiskImage>();
    // A null value means the decode failed; it isn't retried until Refresh
    private readonly Dictionary<string, Texture> diskThumbnails = new Dictionary<string, Texture>();
    private readonly Queue<string> thumbnailQueue = new Queue<string>();
    private readonly HashSet<string> thumbnailQueued = new HashSet<string>();

    // ImportExportFormat.RuntimeIOPath is the CWD-relative "Files/NodeEditor/"; anchoring
    // on dataPath lands in the same game folder without depending on the launch directory
    public string DiskDir =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Files", "Textures"));

    protected override void OnAwake()
    {
        LoadBuiltins();
        try
        {
            // Created up front so "Open dir" always has somewhere to land
            Directory.CreateDirectory(DiskDir);
        }
        catch (Exception e)
        {
            Debug.LogError($"TextureLibrary: couldn't create '{DiskDir}': {e.Message}");
        }
        RefreshDiskNames();
    }

    private void OnDestroy()
    {
        foreach (var entry in diskImages.Values)
        {
            if (entry.texture != null) Destroy(entry.texture);
        }
        diskImages.Clear();
        ClearThumbnails();
    }

    void LoadBuiltins()
    {
        builtinsByName = new Dictionary<string, Texture2D>();
        foreach (var tex in Resources.LoadAll<Texture2D>(BuiltinResourcePath))
        {
            // Resources names drop the extension, so foo.png and foo.jpg collide; first wins
            if (!builtinsByName.ContainsKey(tex.name))
            {
                builtinsByName[tex.name] = tex;
            }
        }
        builtins = builtinsByName.Values
            .OrderBy(t => t.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    // ---------------------------------------------------------------- lookup

    public int Count(TextureSource source) =>
        source == TextureSource.Project ? builtins.Length : diskNames.Length;

    public string NameAt(TextureSource source, int index) =>
        source == TextureSource.Project ? builtins[index].name : diskNames[index];

    public int IndexOf(TextureSource source, string name) =>
        source == TextureSource.Project
            ? Array.FindIndex(builtins, t => t.name == name)
            : Array.IndexOf(diskNames, name);

    /// <summary>Full-resolution texture, or null if the name is unknown or undecodable.
    /// Disk images decode synchronously on first request.</summary>
    public Texture2D Get(TextureSource source, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (source == TextureSource.Project)
        {
            builtinsByName.TryGetValue(name, out var tex);
            return tex;
        }
        return GetDisk(name);
    }

    /// <summary>Grid thumbnail: the texture itself for project textures; for disk images
    /// a downscaled copy, or null while it's still queued (see ThumbnailFailed).</summary>
    public Texture Thumbnail(TextureSource source, int index)
    {
        if (source == TextureSource.Project) return builtins[index];
        string name = diskNames[index];
        if (diskImages.TryGetValue(name, out var entry) && entry.texture != null) return entry.texture;
        if (diskThumbnails.TryGetValue(name, out var thumb)) return thumb;
        if (thumbnailQueued.Add(name)) thumbnailQueue.Enqueue(name);
        return null;
    }

    public bool ThumbnailFailed(TextureSource source, int index) =>
        source == TextureSource.Disk
        && diskThumbnails.TryGetValue(diskNames[index], out var thumb) && thumb == null;

    // ---------------------------------------------------------------- disk

    public void RefreshDiskNames()
    {
        try
        {
            diskNames = Directory.Exists(DiskDir)
                ? Directory.GetFiles(DiskDir)
                    .Where(p => DiskExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
                    .Select(Path.GetFileName)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : new string[0];
        }
        catch (Exception e)
        {
            Debug.LogError($"TextureLibrary: failed to list '{DiskDir}': {e.Message}");
            diskNames = new string[0];
        }
    }

    /// <summary>
    /// Rescans the folder, drops thumbnails, and re-decodes any cached image whose file
    /// changed on disk. Replaced textures are destroyed at end of frame, so anything
    /// holding one must re-fetch it via Get before then.
    /// </summary>
    public void Refresh()
    {
        RefreshDiskNames();
        ClearThumbnails();
        foreach (var name in diskImages.Keys.ToList())
        {
            var entry = diskImages[name];
            string path = PathFor(name);
            // A deleted file keeps serving its last good copy until the session ends
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) == entry.writeTime) continue;
            var fresh = Decode(path);
            if (fresh == null) continue;
            if (entry.texture != null) Destroy(entry.texture);
            diskImages[name] = new DiskImage { texture = fresh, writeTime = File.GetLastWriteTimeUtc(path) };
        }
    }

    Texture2D GetDisk(string name)
    {
        if (!IsPlainFileName(name)) return null;
        if (diskImages.TryGetValue(name, out var entry) && entry.texture != null) return entry.texture;
        string path = PathFor(name);
        if (!File.Exists(path)) return null;
        var tex = Decode(path);
        if (tex == null) return null;
        diskImages[name] = new DiskImage { texture = tex, writeTime = File.GetLastWriteTimeUtc(path) };
        return tex;
    }

    string PathFor(string name) => Path.Combine(DiskDir, name);

    // Names come from serialized canvases: refuse anything that would escape the folder
    static bool IsPlainFileName(string name) =>
        !string.IsNullOrEmpty(name) && Path.GetFileName(name) == name;

    static Texture2D Decode(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            // HideAndDontSave: runtime textures must never be written into a canvas asset
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = Path.GetFileName(path),
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (tex.LoadImage(bytes, markNonReadable: true)) return tex;
            Destroy(tex);
            Debug.LogError($"TextureLibrary: '{path}' isn't a decodable PNG/JPG.");
        }
        catch (Exception e)
        {
            Debug.LogError($"TextureLibrary: failed to read '{path}': {e.Message}");
        }
        return null;
    }

    public void OpenDiskFolder()
    {
        FolderUtil.Reveal(DiskDir);
    }

    // ---------------------------------------------------------------- thumbnails

    // One decode per frame, in Update rather than OnGUI: the Blits swap the active render
    // target, which clobbers whatever the GUI pass is drawing into
    void Update()
    {
        if (thumbnailQueue.Count == 0) return;
        string name = thumbnailQueue.Dequeue();
        thumbnailQueued.Remove(name);
        if (diskThumbnails.ContainsKey(name) || diskImages.ContainsKey(name)) return;
        var full = IsPlainFileName(name) ? Decode(PathFor(name)) : null;
        diskThumbnails[name] = full != null ? Downscale(full) : null;
        if (full != null) Destroy(full);
    }

    void ClearThumbnails()
    {
        foreach (var thumb in diskThumbnails.Values)
        {
            if (thumb != null) Destroy(thumb);
        }
        diskThumbnails.Clear();
        thumbnailQueue.Clear();
        thumbnailQueued.Clear();
    }

    // Successive half-size bilinear blits (each one an exact 2x2 box filter) so large
    // photos don't alias the way a single bilinear blit straight to 128px would
    static RenderTexture Downscale(Texture2D full)
    {
        float scale = Mathf.Min(1f, (float)ThumbnailSize / Mathf.Max(full.width, full.height));
        int w = Mathf.Max(1, Mathf.RoundToInt(full.width * scale));
        int h = Mathf.Max(1, Mathf.RoundToInt(full.height * scale));
        var thumb = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
        {
            name = full.name + " (thumb)",
            hideFlags = HideFlags.HideAndDontSave,
        };
        var previous = RenderTexture.active;
        Texture src = full;
        RenderTexture temp = null;
        int sw = full.width, sh = full.height;
        while (sw > w * 2 || sh > h * 2)
        {
            sw = Mathf.Max(w, sw / 2);
            sh = Mathf.Max(h, sh / 2);
            var next = RenderTexture.GetTemporary(sw, sh, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            next.filterMode = FilterMode.Bilinear;
            Graphics.Blit(src, next);
            if (temp != null) RenderTexture.ReleaseTemporary(temp);
            src = temp = next;
        }
        Graphics.Blit(src, thumb);
        if (temp != null) RenderTexture.ReleaseTemporary(temp);
        RenderTexture.active = previous;
        return thumb;
    }
}
