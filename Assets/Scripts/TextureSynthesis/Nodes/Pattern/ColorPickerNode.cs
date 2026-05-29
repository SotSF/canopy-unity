using NodeEditorFramework;
using NodeEditorFramework.Utilities;

using SecretFire.TextureSynth;

using UnityEngine;

// A simple color source: pick a color via a clickable saturation/value plane + hue bar,
// or drive it from signals through the three channel inputs (interpreted as H/S/V or R/G/B
// per the mode toggle). Outputs an RGBA Vector4, matching ChromaKey's keyColor and the
// SpaceshipGamePlayer color input.
[Node(false, "Pattern/ColorPicker")]
public class ColorPickerNode : TickingNode
{
    public const string ID = "ColorPickerNode";
    public override string GetID { get { return ID; } }
    public override string Title { get { return "ColorPicker"; } }

    private Vector2 _DefaultSize = new Vector2(200, 270);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("c0", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob ch0Knob;
    [ValueConnectionKnob("c1", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob ch1Knob;
    [ValueConnectionKnob("c2", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob ch2Knob;

    [ValueConnectionKnob("color", Direction.Out, typeof(Vector4), NodeSide.Right)]
    public ValueConnectionKnob colorOutputKnob;

    // Canonical state is HSV so the picker's slice stays well-defined; RGB is derived.
    public float hue, saturation = 1f, value = 1f;
    public RadioButtonSet modeSelection;

    private const int PlaneRes = 96;
    private const float PlaneSize = 120f;
    private const int HueRes = 128;
    private const float HueBarHeight = 14f;

    [System.NonSerialized] private Texture2D svTexture;
    [System.NonSerialized] private float svTextureHue = -1f;
    [System.NonSerialized] private Texture2D hueTexture;
    [System.NonSerialized] private bool draggingPlane;
    [System.NonSerialized] private bool draggingHue;

    private bool RgbMode => modeSelection != null && modeSelection.Selected == "RGB";

    public override void DoInit()
    {
        EnsureMode();
    }

    private void EnsureMode()
    {
        if (modeSelection == null || modeSelection.names.Count == 0)
            modeSelection = new RadioButtonSet(0, "HSV", "RGB");
    }

    public override void NodeGUI()
    {
        EnsureMode();
        GUILayout.BeginVertical();

            RadioButtonsHorizontal(modeSelection);

            if (RgbMode)
            {
                Color c = Color.HSVToRGB(hue, saturation, value);
                float r = ChannelRow("R", c.r, ch0Knob);
                float g = ChannelRow("G", c.g, ch1Knob);
                float b = ChannelRow("B", c.b, ch2Knob);
                Color nc = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b));
                if (nc != c)
                    Color.RGBToHSV(nc, out hue, out saturation, out value);
            }
            else
            {
                hue = ChannelRow("H", hue, ch0Knob);
                saturation = ChannelRow("S", saturation, ch1Knob);
                value = ChannelRow("V", value, ch2Knob);
            }
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                // Horizontally-centered vertical group of plane tex, hue tex, and swatch/output port
                GUILayout.BeginVertical();
                    // Clickable saturation (x) / value (y) plane for the current hue.
                    EnsurePlaneTexture(hue);
                    Rect planeRect = GUILayoutUtility.GetRect(PlaneSize, PlaneSize,
                        GUILayout.Width(PlaneSize), GUILayout.Height(PlaneSize));
                    if (Event.current.type == EventType.Repaint && svTexture != null)
                        GUI.DrawTexture(planeRect, svTexture);
                    DrawMarker(planeRect, Mathf.Clamp01(saturation), 1f - Mathf.Clamp01(value), 8f);
                    if (HandlePlaneInput(planeRect))
                        GUI.changed = true;

                    // Clickable hue bar.
                    EnsureHueTexture();
                    Rect hueRect = GUILayoutUtility.GetRect(PlaneSize, HueBarHeight,
                        GUILayout.Width(PlaneSize), GUILayout.Height(HueBarHeight));
                    if (Event.current.type == EventType.Repaint && hueTexture != null)
                        GUI.DrawTexture(hueRect, hueTexture);
                    DrawHueMarker(hueRect, Mathf.Clamp01(hue));
                    if (HandleHueInput(hueRect))
                        GUI.changed = true;

                    // Swatch + output knob.
                    GUILayout.BeginHorizontal();
                        Color outC = Color.HSVToRGB(hue, saturation, value);
                        var prevCol = GUI.color;
                        GUI.color = outC;
                        GUILayout.Box(Texture2D.whiteTexture, GUILayout.Width(44), GUILayout.Height(20));
                        GUI.color = prevCol;
                        GUILayout.FlexibleSpace();
                        colorOutputKnob.DisplayLayout();
                    GUILayout.EndHorizontal();
                    // End: swatch + output knob horizontal group

                GUILayout.EndVertical();
                // End: Horizontally centered vertical block
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        // End: Horizontal block centering color textures / output port 
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    // One labeled channel row: knob value when connected, else an editable slider.
    private float ChannelRow(string label, float val, ValueConnectionKnob knob)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(14));
        knob.SetPosition();
        float result;
        if (knob.connected())
        {
            result = Mathf.Clamp01(knob.GetValue<float>());
            GUILayout.Label(result.ToString("0.00"));
        }
        else
        {
            result = RTEditorGUI.Slider(val, 0f, 1f);
        }
        GUILayout.EndHorizontal();
        return result;
    }

    public override bool DoCalc()
    {
        // Mirror NodeGUI's input handling for frames where the node isn't drawn.
        if (RgbMode)
        {
            Color c = Color.HSVToRGB(hue, saturation, value);
            if (ch0Knob.connected()) c.r = Mathf.Clamp01(ch0Knob.GetValue<float>());
            if (ch1Knob.connected()) c.g = Mathf.Clamp01(ch1Knob.GetValue<float>());
            if (ch2Knob.connected()) c.b = Mathf.Clamp01(ch2Knob.GetValue<float>());
            Color.RGBToHSV(c, out hue, out saturation, out value);
        }
        else
        {
            if (ch0Knob.connected()) hue = ch0Knob.GetValue<float>();
            if (ch1Knob.connected()) saturation = ch1Knob.GetValue<float>();
            if (ch2Knob.connected()) value = ch2Knob.GetValue<float>();
        }

        Vector4 outColor = (Vector4)Color.HSVToRGB(hue, saturation, value);
        colorOutputKnob.SetValue(outColor);
        return true;
    }

    private bool HandlePlaneInput(Rect rect)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
        {
            draggingPlane = true;
            saturation = Mathf.Clamp01((e.mousePosition.x - rect.x) / rect.width);
            value = Mathf.Clamp01(1f - (e.mousePosition.y - rect.y) / rect.height);
            e.Use();
            return true;
        }
        if (e.type == EventType.MouseDrag && draggingPlane)
        {
            saturation = Mathf.Clamp01((e.mousePosition.x - rect.x) / rect.width);
            value = Mathf.Clamp01(1f - (e.mousePosition.y - rect.y) / rect.height);
            e.Use();
            return true;
        }
        if (e.type == EventType.MouseUp && draggingPlane)
        {
            draggingPlane = false;
            e.Use();
        }
        return false;
    }

    private bool HandleHueInput(Rect rect)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
        {
            draggingHue = true;
            hue = Mathf.Clamp01((e.mousePosition.x - rect.x) / rect.width);
            e.Use();
            return true;
        }
        if (e.type == EventType.MouseDrag && draggingHue)
        {
            hue = Mathf.Clamp01((e.mousePosition.x - rect.x) / rect.width);
            e.Use();
            return true;
        }
        if (e.type == EventType.MouseUp && draggingHue)
        {
            draggingHue = false;
            e.Use();
        }
        return false;
    }

    // The plane is drawn upright by GUI.DrawTexture (rect top = texture row PlaneRes-1), so
    // value increases up the texture rows and the rect top shows the brightest row.
    private void EnsurePlaneTexture(float h)
    {
        if (svTexture != null && Mathf.Approximately(svTextureHue, h))
            return;
        if (svTexture == null)
        {
            svTexture = new Texture2D(PlaneRes, PlaneRes, TextureFormat.RGB24, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
        }
        var px = new Color32[PlaneRes * PlaneRes];
        for (int row = 0; row < PlaneRes; row++)
        {
            float v = row / (float)(PlaneRes - 1);
            for (int x = 0; x < PlaneRes; x++)
            {
                float s = x / (float)(PlaneRes - 1);
                px[row * PlaneRes + x] = Color.HSVToRGB(h, s, v);
            }
        }
        svTexture.SetPixels32(px);
        svTexture.Apply(false);
        svTextureHue = h;
    }

    private void EnsureHueTexture()
    {
        if (hueTexture != null)
            return;
        hueTexture = new Texture2D(HueRes, 1, TextureFormat.RGB24, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[HueRes];
        for (int x = 0; x < HueRes; x++)
            px[x] = Color.HSVToRGB(x / (float)(HueRes - 1), 1f, 1f);
        hueTexture.SetPixels32(px);
        hueTexture.Apply(false);
    }

    private void DrawMarker(Rect rect, float nx, float ny, float size)
    {
        if (Event.current.type != EventType.Repaint)
            return;
        float mx = rect.x + nx * rect.width;
        float my = rect.y + ny * rect.height;
        GUI.Box(new Rect(mx - size / 2, my - size / 2, size, size), GUIContent.none);
    }

    private void DrawHueMarker(Rect rect, float nx)
    {
        if (Event.current.type != EventType.Repaint)
            return;
        float mx = rect.x + nx * rect.width;
        GUI.Box(new Rect(mx - 2, rect.y - 1, 4, rect.height + 2), GUIContent.none);
    }

    private void OnDestroy()
    {
        if (svTexture != null)
            Destroy(svTexture);
        if (hueTexture != null)
            Destroy(hueTexture);
    }
}
