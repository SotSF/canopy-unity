using System;
using System.IO;
using NodeEditorFramework;
using SecretFire.TextureSynth;
using UnityEngine;

/// <summary>
/// Outputs one texture chosen from a paged 3x3 grid: either a texture baked into the
/// project (Resources/StaticTextures) or a PNG/JPG from the on-disk Textures folder (see
/// TextureLibrary). Picking collapses the grid to a preview of the choice; Reselect
/// brings it back. Only the source and name serialize, so the canvas doesn't depend on
/// the project's texture list the way TextureBank's per-texture ports did. A texture that
/// goes missing keeps its name (user data) and outputs nothing until it reappears or
/// another is picked.
/// </summary>
[Node(false, "Texture/TexturePicker")]
public class TexturePickerNode : TextureSynthNode
{
    public override string GetID => "TexturePickerNode";
    public override string Title => "TexturePicker";

    private Vector2 _DefaultSize = new Vector2(236, 120);
    public override Vector2 DefaultSize => _DefaultSize;
    public override Vector2 MinSize => new Vector2(236, 60);
    public override bool AutoLayout => true;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 118)]
    public ValueConnectionKnob textureOutputKnob;

    const int Columns = 3;
    const int PageSize = Columns * Columns;
    const int TileSize = 64;
    const int PreviewWidth = 212;
    const int PreviewMaxHeight = 160;
    static readonly string[] SourceLabels = { "Project", "Disk" };
    static readonly Color SelectedTint = new Color(1f, 0.85f, 0.3f);

    public TextureSource source = TextureSource.Project;
    public string textureName = "";

    [NonSerialized] private Texture2D current;
    [NonSerialized] private bool picking;
    [NonSerialized] private TextureSource browseSource;
    [NonSerialized] private int page;
    // Clicks latch here and apply on the next Layout event: changing the control list
    // mid-event (collapsing the grid, paging onto a short page) throws GUILayout errors
    [NonSerialized] private Action pending;

    static GUIStyle _paddedStyle, _tileStyle, _captionStyle;
    static GUIStyle PaddedStyle => _paddedStyle ??= new GUIStyle { padding = new RectOffset(6, 6, 4, 6) };
    static GUIStyle TileStyle => _tileStyle ??= new GUIStyle(GUI.skin.button) { padding = new RectOffset(2, 2, 2, 2) };
    static GUIStyle CaptionStyle => _captionStyle ??= new GUIStyle(GUI.skin.label)
    {
        alignment = TextAnchor.UpperCenter,
        fontSize = 10,
        clipping = TextClipping.Clip,
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        _paddedStyle = null;
        _tileStyle = null;
        _captionStyle = null;
    }

    bool ShowGrid => picking || string.IsNullOrEmpty(textureName);

    // ---------------------------------------------------------------- GUI

    public override void NodeGUI()
    {
        if (pending != null && Event.current.type == EventType.Layout)
        {
            var action = pending;
            pending = null;
            action();
        }

        CenterOutputKnob();
        GUILayout.BeginVertical(PaddedStyle);
        var library = Application.isPlaying ? TextureLibrary.Instance : null;
        if (library == null)
        {
            GUILayout.Label("Enter play mode to pick textures");
        }
        else if (ShowGrid)
        {
            DrawGrid(library);
        }
        else
        {
            DrawSelection(library);
        }
        GUILayout.EndVertical();
    }

    void DrawGrid(TextureLibrary library)
    {
        int tab = GUILayout.Toolbar((int)browseSource, SourceLabels);
        if (tab != (int)browseSource)
        {
            pending = () =>
            {
                browseSource = (TextureSource)tab;
                page = 0;
                if (browseSource == TextureSource.Disk) library.RefreshDiskNames();
            };
        }

        int count = library.Count(browseSource);
        if (browseSource == TextureSource.Disk)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{count} image{(count == 1 ? "" : "s")}");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open dir", GUILayout.Width(60)))
            {
                library.OpenDiskFolder();
            }
            if (GUILayout.Button("Reload", GUILayout.Width(56)))
            {
                pending = () => RefreshDisk(library);
            }
            GUILayout.EndHorizontal();
        }

        if (count == 0)
        {
            GUILayout.Label(browseSource == TextureSource.Disk
                ? "Drop PNG/JPG files into the folder, then Reload"
                : $"No textures in Resources/{TextureLibrary.BuiltinResourcePath}");
        }
        else
        {
            int pageCount = (count + PageSize - 1) / PageSize;
            page = Mathf.Clamp(page, 0, pageCount - 1);
            for (int row = 0; row < Columns; row++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                for (int col = 0; col < Columns; col++)
                {
                    DrawTile(library, page * PageSize + row * Columns + col, count);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUI.enabled = page > 0;
            if (GUILayout.Button("◀", GUILayout.Width(28))) pending = () => page--;
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{page + 1} / {pageCount}");
            GUILayout.FlexibleSpace();
            GUI.enabled = page < pageCount - 1;
            if (GUILayout.Button("▶", GUILayout.Width(28))) pending = () => page++;
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        if (!string.IsNullOrEmpty(textureName) && GUILayout.Button("Cancel"))
        {
            pending = () => picking = false;
        }
    }

    void DrawTile(TextureLibrary library, int index, int count)
    {
        // Slots past the end draw an invisible, disabled tile so every page (and the
        // node's auto-layout size) stays the same shape
        bool empty = index >= count;
        string name = empty ? "" : library.NameAt(browseSource, index);
        Texture thumb = empty ? null : library.Thumbnail(browseSource, index);
        bool failed = !empty && thumb == null && library.ThumbnailFailed(browseSource, index);
        bool isCurrent = !empty && browseSource == source && name == textureName;

        var prevColor = GUI.color;
        var prevBackground = GUI.backgroundColor;
        if (empty)
        {
            GUI.color = Color.clear;
            GUI.enabled = false;
        }
        else if (isCurrent)
        {
            GUI.backgroundColor = SelectedTint;
        }

        GUILayout.BeginVertical(GUILayout.Width(TileSize));
        var content = thumb != null ? new GUIContent(thumb) : new GUIContent(failed ? "⚠" : "…");
        if (GUILayout.Button(content, TileStyle, GUILayout.Width(TileSize), GUILayout.Height(TileSize)) && !failed)
        {
            var clickedSource = browseSource;
            pending = () => Select(clickedSource, name);
        }
        GUI.backgroundColor = prevBackground;
        string caption = browseSource == TextureSource.Disk ? Path.GetFileNameWithoutExtension(name) : name;
        GUILayout.Label(NodeUIElements.TrimName(caption), CaptionStyle, GUILayout.Width(TileSize));
        GUILayout.EndVertical();

        GUI.color = prevColor;
        GUI.enabled = true;
    }

    void DrawSelection(TextureLibrary library)
    {
        if (current != null)
        {
            float height = Mathf.Clamp(PreviewWidth * (float)current.height / current.width, 32f, PreviewMaxHeight);
            Rect r = GUILayoutUtility.GetRect(PreviewWidth, height, GUILayout.Width(PreviewWidth), GUILayout.Height(height));
            GUI.Box(r, GUIContent.none);
            GUI.DrawTexture(r, current, ScaleMode.ScaleToFit);
            GUILayout.Label(textureName);
        }
        else
        {
            GUILayout.Label($"⚠ '{textureName}' not found");
        }

        GUILayout.BeginHorizontal();
        string sourceLabel = SourceLabels[(int)source];
        GUILayout.Label(current != null ? $"{current.width}x{current.height} · {sourceLabel}" : sourceLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reselect", GUILayout.Width(70)))
        {
            pending = () => OpenGrid(library);
        }
        GUILayout.EndHorizontal();
    }

    void OpenGrid(TextureLibrary library)
    {
        library.RefreshDiskNames();
        browseSource = source;
        int index = library.IndexOf(source, textureName);
        page = index >= 0 ? index / PageSize : 0;
        picking = true;
    }

    void Select(TextureSource newSource, string name)
    {
        source = newSource;
        textureName = name;
        picking = false;
        NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    // Recalculates every picker, not just this one: a changed file's old texture is
    // destroyed at end of frame, and a picker whose file was missing may now resolve
    void RefreshDisk(TextureLibrary library)
    {
        library.Refresh();
        var canvas = NodeEditor.curNodeCanvas;
        foreach (var node in canvas.nodes)
        {
            if (node is TexturePickerNode) canvas.OnNodeChange(node);
        }
    }

    // ---------------------------------------------------------------- calc

    // Positioned every pass rather than via the attribute: the auto-layout width isn't
    // known up front, and wires draw before NodeGUI, so DoCalc sets it too
    void CenterOutputKnob()
    {
        textureOutputKnob?.SetPosition(rect.width / 2);
    }

    public override bool DoCalc()
    {
        var library = Application.isPlaying ? TextureLibrary.Instance : null;
        current = library != null ? library.Get(source, textureName) : null;
        if (textureOutputKnob == null) return true;
        CenterOutputKnob();
        if (current != null)
        {
            textureOutputKnob.SetValue<Texture>(current);
        }
        else
        {
            textureOutputKnob.ResetValue();
        }
        return true;
    }
}
